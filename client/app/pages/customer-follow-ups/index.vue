<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold">Customer Follow Ups</h1>
        <div class="text-caption text-medium-emphasis">
          Click a cell to edit it — Enter or leaving the cell saves (Shift+Enter for a new line), Tab moves to the next cell, Esc cancels.
        </div>
      </div>
      <v-spacer />
      <v-chip variant="tonal" size="small">{{ displayRows.length }} / {{ rows.length }} rows</v-chip>
      <v-chip v-if="activeFilterCount > 0" color="primary" size="small" closable @click:close="colFilter.clearAllFilters()">
        {{ activeFilterCount }} filter{{ activeFilterCount > 1 ? 's' : '' }} active
      </v-chip>
      <v-chip
        v-for="hidden in hiddenCounts"
        :key="hidden.rule.key"
        size="small"
        variant="tonal"
        color="grey"
        prepend-icon="mdi-eye-off-outline"
        :title="`Rows with ${COLUMN[hidden.rule.key].label} ${hidden.rule.value} are hidden — click (or pick ${hidden.rule.value} in the ${COLUMN[hidden.rule.key].label} filter) to show them`"
        @click="showHiddenRows(hidden.rule)"
      >
        {{ hidden.count }} {{ hidden.rule.chip }} hidden
      </v-chip>
      <v-chip
        v-if="unreadCount > 0"
        size="small"
        variant="flat"
        class="cfu-unread-chip"
        prepend-icon="mdi-message-badge-outline"
        title="Rows with messages you have not read are highlighted"
      >
        {{ unreadCount }} unread chat{{ unreadCount === 1 ? '' : 's' }}
      </v-chip>
      <v-text-field
        v-model="search"
        prepend-inner-icon="mdi-magnify"
        placeholder="Search rows..."
        density="compact"
        hide-details
        clearable
        variant="outlined"
        style="max-width: 260px;"
      />
      <v-btn variant="tonal" color="primary" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>

      <!-- Column order & widths -->
      <v-menu :close-on-content-click="false" location="bottom end">
        <template #activator="{ props }">
          <v-btn v-bind="props" variant="tonal" color="secondary" prepend-icon="mdi-view-column">Columns</v-btn>
        </template>
        <v-card min-width="280" max-width="340" class="pa-3">
          <div class="text-subtitle-2 font-weight-bold mb-2">Column order</div>
          <v-divider class="mb-2" />
          <div
            v-for="column in orderedColumns"
            :key="column.key"
            class="d-flex align-center py-1 px-2 rounded cfu-menu-row"
            :class="{
              'menu-dragging': menuDragKey === column.key,
              'menu-drop-before': menuDropTarget?.key === column.key && !menuDropTarget.after,
              'menu-drop-after': menuDropTarget?.key === column.key && menuDropTarget.after,
            }"
            draggable="true"
            @dragstart="menuDragKey = column.key"
            @dragover="onMenuDragOver($event, column.key)"
            @drop.prevent="onMenuDrop(column.key)"
            @dragend="onMenuDragEnd"
          >
            <v-icon icon="mdi-drag-vertical" size="16" class="menu-drag-handle mr-1" />
            <span class="text-body-2">{{ column.label }}</span>
            <v-spacer />
            <span v-if="layout.widths[column.key]" class="text-caption text-medium-emphasis">{{ layout.widths[column.key] }}px</span>
          </div>
          <v-divider class="my-2" />
          <div class="d-flex align-center">
            <span class="text-caption text-medium-emphasis">
              Drag headers to move them, drag a header's right edge to resize, double-click the edge for the default width.
            </span>
            <v-btn
              size="x-small"
              variant="text"
              color="primary"
              class="ml-2 flex-shrink-0"
              :disabled="!layout.order.length && !Object.keys(layout.widths).length"
              @click="layoutStore.reset(TABLE_KEY)"
            >
              Reset
            </v-btn>
          </div>
        </v-card>
      </v-menu>

      <v-btn color="primary" prepend-icon="mdi-plus" :loading="adding" @click="addRow">Add Row</v-btn>
    </div>

    <v-card class="glass-card">
      <div v-if="loading && !rows.length" class="d-flex justify-center pa-12">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <div v-else ref="tableScroll" class="cfu-scroll">
        <table ref="cfuTable" class="cfu-table" :style="columnWidthVars">
          <thead>
            <tr
              class="cfu-head-row"
              @mousedown="onHeaderMouseDown"
              @dblclick="onHeaderDblClick"
              @dragstart="onHeaderDragStart"
              @dragover="onHeaderDragOver"
              @dragleave="onHeaderDragLeave"
              @drop="onHeaderDrop"
              @dragend="onHeaderDragEnd"
            >
              <th v-for="key in orderedKeys" :key="key" :data-col="key" draggable="true" :style="cellStyles[key]">
                <ColFilterMenu
                  :col-key="key"
                  :label="COLUMN[key].label"
                  :options="filterOptions[key].available"
                  :all-options="filterOptions[key].all"
                  :selected="colFilter.selected[key] || EMPTY_SET"
                  :search="colFilter.search[key] || ''"
                  :is-sorted="sortKey === key"
                  :sort-desc="sortDesc"
                  @toggle="(value) => colFilter.toggle(key, value)"
                  @select-all="(values) => colFilter.selectAll(key, values)"
                  @clear-all="colFilter.clearAll(key)"
                  @update:search="(value) => colFilter.search[key] = value"
                  @sort-click="toggleSort(key)"
                />
              </th>
              <th class="cfu-actions" />
            </tr>
          </thead>
          <tbody>
            <CustomerFollowUpRow
              v-for="row in displayRows"
              :key="row.id"
              :row="row"
              :columns="orderedKeys"
              :editing-key="editing?.id === row.id ? editing.key : null"
              :options="editorOptions"
              :cell-styles="cellStyles"
              :today="today"
              :is-new="newRowIds.has(row.id)"
              @edit="(key) => startEdit(row.id, key)"
              @commit="(key, value, move) => commitCell(row.id, key, value, move)"
              @cancel="editing = null"
              @delete="rowToDelete = row"
              @documents="openDocuments(row)"
              @messages="openMessages(row)"
              @rfq="openRfq(row)"
              @refs="openRefs(row)"
            />
            <tr v-if="!displayRows.length">
              <td :colspan="orderedKeys.length + 1" class="text-center pa-8 text-medium-emphasis">
                <v-icon icon="mdi-table-off" size="36" color="grey" class="mb-1" /><br>
                {{ rows.length ? 'No rows match the filters.' : 'No follow ups yet — click Add Row to start.' }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </v-card>

    <v-dialog :model-value="!!rowToDelete" max-width="420" @update:model-value="(open) => !open && (rowToDelete = null)">
      <v-card v-if="rowToDelete">
        <v-card-title class="text-subtitle-1 font-weight-bold">Delete row #{{ rowToDelete.id }}?</v-card-title>
        <v-card-text class="text-body-2">
          {{ rowToDelete.customerCode || 'No customer' }} — {{ rowToDelete.requestedItem || 'no requested item' }}
          <div v-if="rowToDelete.documentCount" class="text-error mt-2">
            Its {{ documentCountText(rowToDelete.documentCount) }} will be deleted too.
          </div>
          <div v-if="rowToDelete.messageCount" class="text-error mt-2">
            Its {{ messageCountText(rowToDelete.messageCount) }} will be deleted too.
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="rowToDelete = null">Cancel</v-btn>
          <v-btn color="error" variant="tonal" :loading="deleting" @click="deleteRow">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <CustomerFollowUpRfqDialog
      :row="rfqRow"
      @close="rfqRow = null"
      @saved="replaceRow"
      @notify="showSnack"
    />

    <CustomerFollowUpRefsDialog
      :row="refsRow"
      @close="refsRow = null"
      @saved="replaceRow"
      @notify="showSnack"
    />

    <CustomerFollowUpDocumentsDialog
      :row="documentsRow"
      @close="closeDocuments"
      @count="setDocumentCount"
      @notify="showSnack"
    />

    <CustomerFollowUpMessagesDialog
      :row="messagesRow"
      @close="closeMessages"
      @message-count="setMessageCount"
      @unread-change="setUnread"
      @documents-added="addDocumentCount"
      @notify="showSnack"
    />

    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="4000">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import {
  BLANK_FILTER_VALUE,
  DIALOG_COLUMN_TYPES,
  documentCountText,
  FOLLOW_UP_COLUMN_BY_KEY as COLUMN,
  FOLLOW_UP_COLUMNS,
  followUpCellText,
  localToday,
  messageCountText,
  normalizeFollowUpRow,
  type EditableFollowUpKey,
  type FollowUpColumn,
  type FollowUpKey,
  type FollowUpRow,
} from '~/utils/customerFollowUps'
import { MIN_COLUMN_WIDTH, useTableLayoutStore } from '~/stores/tableLayout'

const api = useApi()
const layoutStore = useTableLayoutStore()
const colFilter = useColFilterPersisted('customer-follow-ups')

const TABLE_KEY = 'customer-follow-ups'
const RESIZE_EDGE_PX = 7
const EMPTY_SET = new Set<string>()

interface FollowUpOptions {
  customers: string[]
  assignees: string[]
  categories: string[]
  issueStatuses: string[]
  /** Open, In Progress, Done (Procurement and Sales Status). */
  workStatuses: string[]
  /** Menu Access "followUpProcurement": Procurement Status Done rows are hidden for this user. */
  isProcurementUser: boolean
}

const rows = ref<FollowUpRow[]>([])
const options = ref<FollowUpOptions>({
  customers: [], assignees: [], categories: [], issueStatuses: [], workStatuses: ['Open', 'In Progress', 'Done'], isProcurementUser: false,
})
const loading = ref(true)
const adding = ref(false)
const deleting = ref(false)
const search = ref('')
const sortKey = ref<FollowUpKey>('id')
const sortDesc = ref(true)
const editing = ref<{ id: number; key: EditableFollowUpKey } | null>(null)
/** Rows added in this visit stay on top and visible whatever the filters, until the next load. */
const newRowIds = ref(new Set<number>())
const rowToDelete = ref<FollowUpRow | null>(null)
const today = localToday()
const tableScroll = ref<HTMLElement | null>(null)
const cfuTable = ref<HTMLTableElement | null>(null)

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

function errorMessage(error: any, fallback: string) {
  return error?.data?.message || error?.response?._data?.message || fallback
}

// ── Load ─────────────────────────────────────────────────────────────────────

async function load() {
  loading.value = true
  editing.value = null
  try {
    const [list, opts] = await Promise.all([
      api.get<any[]>('/customer-follow-ups'),
      api.get<FollowUpOptions>('/customer-follow-ups/options'),
    ])
    rows.value = list.map(normalizeFollowUpRow)
    options.value = opts
    newRowIds.value = new Set()
    settledThisVisitIds.value = new Set()
  } catch (error) {
    showSnack(errorMessage(error, 'Could not load follow ups.'), 'error')
  } finally {
    loading.value = false
  }
}

// ── Column order & widths (per user, in the tableLayout store) ──────────────

const EMPTY_LAYOUT = { order: [] as string[], widths: {} as Record<string, number> }
const layout = computed(() => layoutStore.layouts[layoutStore.storageKey(TABLE_KEY)] ?? EMPTY_LAYOUT)

/**
 * Columns in the user's order. A column missing from a saved order (e.g. one added after the
 * user last reordered) goes right after the column that precedes it by default.
 */
const orderedColumns = computed<FollowUpColumn[]>(() => {
  const result = layout.value.order
    .map(key => COLUMN[key as FollowUpKey])
    .filter((column): column is FollowUpColumn => !!column)
  const placed = new Set(result.map(column => column.key))
  FOLLOW_UP_COLUMNS.forEach((column, index) => {
    if (placed.has(column.key)) return
    const previousKey = FOLLOW_UP_COLUMNS[index - 1]?.key
    const at = previousKey ? result.findIndex(c => c.key === previousKey) : -1
    result.splice(index === 0 ? 0 : at < 0 ? result.length : at + 1, 0, column)
    placed.add(column.key)
  })
  return result
})

const orderedKeys = computed(() => orderedColumns.value.map(column => column.key))

function moveColumn(fromKey: string, toKey: string, placeAfter: boolean) {
  if (fromKey === toKey) return
  const keys = orderedKeys.value.filter(key => key !== fromKey) as string[]
  const targetIndex = keys.indexOf(toKey)
  if (targetIndex < 0) return
  keys.splice(placeAfter ? targetIndex + 1 : targetIndex, 0, fromKey)
  layoutStore.setOrder(TABLE_KEY, keys)
}

/** Widths live in CSS variables on the table, so resizing repaints without re-rendering any row. */
const columnWidthVars = computed(() => Object.fromEntries(FOLLOW_UP_COLUMNS.map(column =>
  [`--cfu-w-${column.key}`, `${layout.value.widths[column.key] ?? column.width}px`])))

/** One shared style object per column — never changes, so rows don't re-render on resize. */
const cellStyles: Record<string, Record<string, string>> = Object.fromEntries(FOLLOW_UP_COLUMNS.map((column) => {
  const width = `var(--cfu-w-${column.key})`
  return [column.key, { width, minWidth: width, maxWidth: width }]
}))

function headerCellAt(event: Event) {
  return (event.target as HTMLElement | null)?.closest<HTMLTableCellElement>('th[data-col]') ?? null
}

function isOnResizeEdge(event: MouseEvent, th: HTMLElement) {
  return event.clientX >= th.getBoundingClientRect().right - RESIZE_EDGE_PX
}

function onHeaderMouseDown(event: MouseEvent) {
  const th = headerCellAt(event)
  if (!th || event.button !== 0 || !isOnResizeEdge(event, th)) return
  // Stops the header's own drag-to-move and any text selection.
  event.preventDefault()
  const key = th.dataset.col!
  const startX = event.clientX
  const startWidth = th.getBoundingClientRect().width
  let width = Math.round(startWidth)

  const onMove = (move: MouseEvent) => {
    width = Math.max(MIN_COLUMN_WIDTH, Math.round(startWidth + move.clientX - startX))
    cfuTable.value?.style.setProperty(`--cfu-w-${key}`, `${width}px`)
  }
  const onUp = () => {
    window.removeEventListener('mousemove', onMove)
    window.removeEventListener('mouseup', onUp)
    document.body.classList.remove('cfu-resizing')
    layoutStore.setWidth(TABLE_KEY, key, width)
  }
  document.body.classList.add('cfu-resizing')
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

/** Double-clicking a resize edge gives the column back its default width. */
function onHeaderDblClick(event: MouseEvent) {
  const th = headerCellAt(event)
  if (!th || !isOnResizeEdge(event, th)) return
  layoutStore.clearWidth(TABLE_KEY, th.dataset.col!)
}

let draggedColumnKey: string | null = null
let dropMarkedCell: HTMLElement | null = null

function clearDropMark() {
  dropMarkedCell?.classList.remove('cfu-drop-before', 'cfu-drop-after')
  dropMarkedCell = null
}

function dropsAfter(event: DragEvent, th: HTMLElement) {
  const rect = th.getBoundingClientRect()
  return event.clientX > rect.left + rect.width / 2
}

function onHeaderDragStart(event: DragEvent) {
  const th = headerCellAt(event)
  if (!th) return
  draggedColumnKey = th.dataset.col!
  th.classList.add('cfu-dragging')
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', draggedColumnKey)
  }
}

function onHeaderDragOver(event: DragEvent) {
  const th = headerCellAt(event)
  if (!th || !draggedColumnKey) return
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  if (dropMarkedCell !== th) clearDropMark()
  dropMarkedCell = th
  const after = dropsAfter(event, th)
  th.classList.toggle('cfu-drop-after', after)
  th.classList.toggle('cfu-drop-before', !after)
}

function onHeaderDragLeave(event: DragEvent) {
  const th = headerCellAt(event)
  if (th && th === dropMarkedCell && !th.contains(event.relatedTarget as Node | null)) clearDropMark()
}

function onHeaderDrop(event: DragEvent) {
  const th = headerCellAt(event)
  if (!th || !draggedColumnKey) return
  event.preventDefault()
  moveColumn(draggedColumnKey, th.dataset.col!, dropsAfter(event, th))
  clearDropMark()
}

function onHeaderDragEnd(event: DragEvent) {
  headerCellAt(event)?.classList.remove('cfu-dragging')
  draggedColumnKey = null
  clearDropMark()
}

// Reordering from the Columns menu works the same way as on the headers.
const menuDragKey = ref<string | null>(null)
const menuDropTarget = ref<{ key: string; after: boolean } | null>(null)

function onMenuDragOver(event: DragEvent, key: string) {
  if (!menuDragKey.value) return
  event.preventDefault()
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  menuDropTarget.value = { key, after: event.clientY > rect.top + rect.height / 2 }
}

function onMenuDrop(key: string) {
  if (menuDragKey.value && menuDropTarget.value?.key === key) {
    moveColumn(menuDragKey.value, key, menuDropTarget.value.after)
  }
  onMenuDragEnd()
}

function onMenuDragEnd() {
  menuDragKey.value = null
  menuDropTarget.value = null
}

// ── Search, Excel-style filters, sort ────────────────────────────────────────

function filterValue(row: FollowUpRow, key: FollowUpKey) {
  return followUpCellText(row, key) || BLANK_FILTER_VALUE
}

const activeFilterKeys = computed(() =>
  FOLLOW_UP_COLUMNS.map(column => column.key).filter(key => (colFilter.selected[key]?.size ?? 0) > 0))

const activeFilterCount = computed(() => activeFilterKeys.value.length)

const searchedRows = computed(() => {
  const query = (search.value ?? '').trim().toLowerCase()
  if (!query) return rows.value
  return rows.value.filter(row => FOLLOW_UP_COLUMNS.some(column =>
    column.type !== 'docs' && column.type !== 'chat' && followUpCellText(row, column.key).toLowerCase().includes(query)))
})

/**
 * Rows hidden until their value is picked in that column's filter:
 * - Issue Status Closed and Sales Status Done — for everyone;
 * - Procurement Status Done — for procurement users only (sales keeps seeing them).
 */
interface HideRule { key: 'issueStatus' | 'salesStatus' | 'procurementStatus'; value: string; chip: string }

const hideRules = computed<HideRule[]>(() => [
  { key: 'issueStatus', value: 'Closed', chip: 'closed' },
  { key: 'salesStatus', value: 'Done', chip: 'sales done' },
  ...(options.value.isProcurementUser ? [{ key: 'procurementStatus', value: 'Done', chip: 'procurement done' } as HideRule] : []),
])

function matchesRule(row: FollowUpRow, rule: HideRule) {
  return (row[rule.key] ?? '').trim().toLowerCase() === rule.value.toLowerCase()
}

/** The rule's value is picked in its column filter, so its rows show. */
function ruleLifted(rule: HideRule) {
  return [...(colFilter.selected[rule.key] ?? [])].some(value => value.trim().toLowerCase() === rule.value.toLowerCase())
}

const activeHideRules = computed(() => hideRules.value.filter(rule => !ruleLifted(rule)))

/** Rows closed / set to Done during this visit stay on screen (so it doesn't vanish mid-edit) until the next load. */
const settledThisVisitIds = ref(new Set<number>())

/**
 * The first active filter the row fails (or null). With `exceptKey`, that column's filter is ignored.
 * A row hidden by a rule counts as failing that column's filter, so e.g. "Done" still shows in that
 * column's filter list.
 */
function failedFilter(row: FollowUpRow, exceptKey?: FollowUpKey): FollowUpKey | null {
  if (!settledThisVisitIds.value.has(row.id)) {
    for (const rule of activeHideRules.value) {
      if (rule.key !== exceptKey && matchesRule(row, rule)) return rule.key
    }
  }
  for (const key of activeFilterKeys.value) {
    if (key !== exceptKey && !colFilter.selected[key]!.has(filterValue(row, key))) return key
  }
  return null
}

/** Per active rule: rows that would show if only that rule were lifted. */
const hiddenCounts = computed(() => activeHideRules.value
  .map(rule => ({
    rule,
    count: searchedRows.value.filter(row =>
      matchesRule(row, rule) && !settledThisVisitIds.value.has(row.id) && failedFilter(row, rule.key) === null).length,
  }))
  .filter(hidden => hidden.count > 0))

/** Same as picking the rule's value (e.g. "Done") in its column filter. */
function showHiddenRows(rule: HideRule) {
  const value = filterOptions.value[rule.key].all.find(v => v.trim().toLowerCase() === rule.value.toLowerCase()) ?? rule.value
  colFilter.toggle(rule.key, value)
}

const filteredRows = computed(() => searchedRows.value.filter(row =>
  newRowIds.value.has(row.id) || failedFilter(row) === null))

function compareValues(a: string, b: string) {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' })
}

const displayRows = computed(() => {
  const key = sortKey.value
  const direction = sortDesc.value ? -1 : 1
  return [...filteredRows.value].sort((a, b) => {
    const pinned = Number(newRowIds.value.has(b.id)) - Number(newRowIds.value.has(a.id))
    if (pinned) return pinned
    if (key === 'id') return (a.id - b.id) * direction
    const left = followUpCellText(a, key)
    const right = followUpCellText(b, key)
    // Empty cells always sort last.
    if (!left || !right) return left ? -1 : right ? 1 : b.id - a.id
    return compareValues(left, right) * direction || b.id - a.id
  })
})

function toggleSort(key: FollowUpKey) {
  if (sortKey.value === key) {
    sortDesc.value = !sortDesc.value
  } else {
    sortKey.value = key
    sortDesc.value = false
  }
}

/**
 * Filter values per column. "available" excludes the column's own filter (so a filter can
 * be widened again); "all" lists every value in the sheet.
 */
const filterOptions = computed(() => {
  const all: Record<string, Set<string>> = {}
  const available: Record<string, Set<string>> = {}
  for (const column of FOLLOW_UP_COLUMNS) {
    all[column.key] = new Set()
    available[column.key] = new Set()
  }
  for (const row of rows.value) {
    for (const column of FOLLOW_UP_COLUMNS) all[column.key]!.add(filterValue(row, column.key))
  }
  for (const row of searchedRows.value) {
    const failed = failedFilter(row)
    if (failed === null) {
      for (const column of FOLLOW_UP_COLUMNS) available[column.key]!.add(filterValue(row, column.key))
    } else if (failedFilter(row, failed) === null) {
      // Fails only this column's filter — its value is still available in this column.
      available[failed]!.add(filterValue(row, failed))
    }
  }
  const sorted = (values: Set<string>) => [...values].sort((a, b) =>
    a === BLANK_FILTER_VALUE ? -1 : b === BLANK_FILTER_VALUE ? 1 : compareValues(a, b))
  return Object.fromEntries(FOLLOW_UP_COLUMNS.map(column => [column.key, {
    all: sorted(all[column.key]!),
    available: sorted(available[column.key]!),
  }])) as Record<FollowUpKey, { all: string[]; available: string[] }>
})

// ── Editing ──────────────────────────────────────────────────────────────────

const editorOptions = computed<Partial<Record<FollowUpKey, string[]>>>(() => ({
  customerCode: options.value.customers,
  category: options.value.categories,
  assign: options.value.assignees,
  procurementStatus: options.value.workStatuses,
  salesStatus: options.value.workStatuses,
  issueStatus: options.value.issueStatuses,
}))

type FreeOptionList = 'customers' | 'assignees' | 'categories' | 'issueStatuses'

const OPTION_LIST_BY_KEY: Partial<Record<FollowUpKey, FreeOptionList>> = {
  customerCode: 'customers',
  category: 'categories',
  assign: 'assignees',
  issueStatus: 'issueStatuses',
}

/** A value typed into a dropdown cell joins that dropdown (the server lists it from now on too). */
function rememberOption(key: FollowUpKey, value: string) {
  const listName = OPTION_LIST_BY_KEY[key]
  if (!listName || !value) return
  const list = options.value[listName]
  if (list.some(item => item.toLowerCase() === value.toLowerCase())) return
  options.value = { ...options.value, [listName]: [...list, value] }
}

/** Columns edited in place — the Row Id and the dialog cells (Documents, Messages, RFQ Ref#, Other Refs) are not. */
function isEditableKey(key: FollowUpKey): key is EditableFollowUpKey {
  const type = COLUMN[key].type
  return type !== 'id' && !DIALOG_COLUMN_TYPES.has(type)
}

/** Status values that hide a row (Closed / Done) — a row set to one now stays until the next load. */
function settlesRow(key: EditableFollowUpKey, value: string | null) {
  const rule = hideRules.value.find(r => r.key === key)
  return !!rule && value?.toLowerCase() === rule.value.toLowerCase()
}

function startEdit(id: number, key: FollowUpKey) {
  if (!isEditableKey(key)) return
  editing.value = { id, key }
}

/** The next / previous editable column in the user's order, for Tab and Shift+Tab. */
function neighbourKey(key: EditableFollowUpKey, move: 1 | -1): EditableFollowUpKey | null {
  const keys = orderedKeys.value.filter(isEditableKey)
  const index = keys.indexOf(key)
  return keys[index + move] ?? null
}

async function commitCell(id: number, key: EditableFollowUpKey, rawValue: string, move: 0 | 1 | -1) {
  // Ignore the blur that follows Enter / Tab / a pick — that edit is already handled.
  if (editing.value?.id !== id || editing.value.key !== key) return
  const next = move ? neighbourKey(key, move) : null
  editing.value = next ? { id, key: next } : null

  const index = rows.value.findIndex(row => row.id === id)
  if (index < 0) return
  const previousValue = rows.value[index]![key]
  const value = rawValue.trim() || null
  if ((previousValue ?? null) === value) return

  // Show the new value at once; only this cell is reverted if the save fails.
  rows.value[index] = { ...rows.value[index]!, [key]: value }
  if (settlesRow(key, value)) settledThisVisitIds.value = new Set([...settledThisVisitIds.value, id])
  if (value) rememberOption(key, value)
  // Category RFQ asks for the RFQ right away (pick one or create it).
  if (key === 'category' && value?.toLowerCase() === 'rfq' && !rows.value[index]!.rfq) {
    editing.value = null
    rfqRow.value = rows.value[index]!
  }
  try {
    const saved = normalizeFollowUpRow(await api.patch<any>(`/customer-follow-ups/${id}`, { field: key, value }))
    const current = rows.value.findIndex(row => row.id === id)
    if (current >= 0) {
      // Merge only this cell (plus audit info, and the Procurement Status a procurement user's edit
      // moves to In Progress) so a faster save of another cell is not overwritten.
      rows.value[current] = {
        ...rows.value[current]!,
        [key]: saved[key],
        procurementStatus: saved.procurementStatus,
        modifiedBy: saved.modifiedBy,
        modifyAt: saved.modifyAt,
      }
    }
  } catch (error) {
    const current = rows.value.findIndex(row => row.id === id)
    if (current >= 0 && rows.value[current]![key] === value) {
      rows.value[current] = { ...rows.value[current]!, [key]: previousValue }
    }
    showSnack(errorMessage(error, `Could not save ${COLUMN[key].label}.`), 'error')
  }
}

// ── Documents ────────────────────────────────────────────────────────────────

/** The row whose documents dialog is open. */
const documentsRow = ref<FollowUpRow | null>(null)

function openDocuments(row: FollowUpRow) {
  editing.value = null
  documentsRow.value = row
}

function closeDocuments() {
  const row = documentsRow.value
  documentsRow.value = null
  if (row) refreshProcurementStatus(row.id)
}

/** The dialog reports the count after loading, uploading or deleting — only that row re-renders. */
function setDocumentCount(rowId: number, count: number) {
  const index = rows.value.findIndex(row => row.id === rowId)
  if (index < 0 || rows.value[index]!.documentCount === count) return
  rows.value[index] = { ...rows.value[index]!, documentCount: count }
}

// ── Messages ─────────────────────────────────────────────────────────────────

/** The row whose chat is open. */
const messagesRow = ref<FollowUpRow | null>(null)

function openMessages(row: FollowUpRow) {
  editing.value = null
  messagesRow.value = row
}

function closeMessages() {
  const row = messagesRow.value
  messagesRow.value = null
  if (row) refreshProcurementStatus(row.id)
}

/**
 * Uploading a file or chatting is work too: the server moves an Open Procurement Status to In Progress
 * for a procurement user. Those dialogs don't return the row, so its status is read once they close.
 */
async function refreshProcurementStatus(rowId: number) {
  const row = rows.value.find(r => r.id === rowId)
  if (!options.value.isProcurementUser || row?.procurementStatus !== 'Open') return
  try {
    const saved = normalizeFollowUpRow(await api.get<any>(`/customer-follow-ups/${rowId}`))
    const index = rows.value.findIndex(r => r.id === rowId)
    if (index >= 0 && rows.value[index]!.procurementStatus !== saved.procurementStatus) {
      rows.value[index] = { ...rows.value[index]!, procurementStatus: saved.procurementStatus }
    }
  } catch {
    // The next load shows it.
  }
}

// ── RFQ Ref# and Other Refs ──────────────────────────────────────────────────

/** The row whose RFQ dialog is open. */
const rfqRow = ref<FollowUpRow | null>(null)
/** The row whose Other Refs dialog is open. */
const refsRow = ref<FollowUpRow | null>(null)

function openRfq(row: FollowUpRow) {
  editing.value = null
  rfqRow.value = row
}

function openRefs(row: FollowUpRow) {
  editing.value = null
  refsRow.value = row
}

/** A dialog saved the row: take the server's RFQ, refs and statuses; keep this user's chat state. */
function replaceRow(raw: any) {
  const saved = normalizeFollowUpRow(raw)
  const index = rows.value.findIndex(row => row.id === saved.id)
  if (index < 0) return
  const current = rows.value[index]!
  const next = {
    ...current,
    rfq: saved.rfq,
    refs: saved.refs,
    category: saved.category,
    procurementStatus: saved.procurementStatus,
    modifiedBy: saved.modifiedBy,
    modifyAt: saved.modifyAt,
  }
  rows.value[index] = next
  // The open dialog shows the row it was given — keep it current (e.g. the RFQ dialog after Unlink).
  if (rfqRow.value?.id === saved.id) rfqRow.value = next
  if (refsRow.value?.id === saved.id) refsRow.value = next
}

/** Per user: the dialog marks the chat read when it opens, or unread with its "Mark as unread" button. */
function setUnread(rowId: number, unread: boolean) {
  const index = rows.value.findIndex(row => row.id === rowId)
  if (index < 0 || rows.value[index]!.hasUnreadMessages === unread) return
  rows.value[index] = { ...rows.value[index]!, hasUnreadMessages: unread }
}

const unreadCount = computed(() => displayRows.value.filter(row => row.hasUnreadMessages).length)

const MESSAGE_STATUS_POLL_MS = 15000
let messageStatusTimer: ReturnType<typeof setInterval> | null = null

/**
 * Picks up messages other users sent while this page is open. Only rows whose count or unread
 * flag changed are replaced, so the rest of the sheet does not re-render.
 */
async function refreshMessageStatus() {
  if (document.visibilityState !== 'visible' || loading.value) return
  try {
    const list = await api.get<{ id: number; messageCount: number; hasUnreadMessages: boolean }[]>('/customer-follow-ups/message-status')
    const byId = new Map(list.map(status => [status.id, status]))
    rows.value.forEach((row, index) => {
      const status = byId.get(row.id)
      const messageCount = status?.messageCount ?? 0
      // The open chat is being read right now — it marks itself read.
      const hasUnreadMessages = messagesRow.value?.id === row.id ? false : status?.hasUnreadMessages ?? false
      if (row.messageCount !== messageCount || row.hasUnreadMessages !== hasUnreadMessages) {
        rows.value[index] = { ...row, messageCount, hasUnreadMessages }
      }
    })
  } catch {
    // A missed poll is harmless — the next one catches up.
  }
}

function setMessageCount(rowId: number, count: number) {
  const index = rows.value.findIndex(row => row.id === rowId)
  if (index < 0 || rows.value[index]!.messageCount === count) return
  rows.value[index] = { ...rows.value[index]!, messageCount: count }
}

/** Files sent in the chat are row documents too (negative when a message with a file is deleted). */
function addDocumentCount(rowId: number, delta: number) {
  const row = rows.value.find(r => r.id === rowId)
  if (row) setDocumentCount(rowId, Math.max(0, row.documentCount + delta))
}

async function addRow() {
  adding.value = true
  try {
    const created = normalizeFollowUpRow(await api.post<any>('/customer-follow-ups', { dateOfRequest: localToday() }))
    rows.value = [created, ...rows.value]
    newRowIds.value = new Set([...newRowIds.value, created.id])
    tableScroll.value?.scrollTo({ top: 0 })
    const firstKey = orderedKeys.value.find(isEditableKey)
    if (firstKey) startEdit(created.id, firstKey)
  } catch (error) {
    showSnack(errorMessage(error, 'Could not add a row.'), 'error')
  } finally {
    adding.value = false
  }
}

async function deleteRow() {
  const row = rowToDelete.value
  if (!row) return
  deleting.value = true
  try {
    await api.delete(`/customer-follow-ups/${row.id}`)
    rows.value = rows.value.filter(r => r.id !== row.id)
    if (editing.value?.id === row.id) editing.value = null
    if (documentsRow.value?.id === row.id) documentsRow.value = null
    if (messagesRow.value?.id === row.id) messagesRow.value = null
    if (rfqRow.value?.id === row.id) rfqRow.value = null
    if (refsRow.value?.id === row.id) refsRow.value = null
    rowToDelete.value = null
    showSnack(`Row #${row.id} deleted.`)
  } catch (error) {
    showSnack(errorMessage(error, 'Could not delete the row.'), 'error')
  } finally {
    deleting.value = false
  }
}

onMounted(() => {
  layoutStore.layout(TABLE_KEY)
  load()
  messageStatusTimer = setInterval(refreshMessageStatus, MESSAGE_STATUS_POLL_MS)
})

onBeforeUnmount(() => {
  if (messageStatusTimer) clearInterval(messageStatusTimer)
})
</script>

<style scoped>
.cfu-scroll {
  overflow: auto;
  max-height: calc(100vh - 210px);
  border-radius: 8px;
}

.cfu-table {
  width: max-content;
  min-width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  font-size: 12px;
}

.cfu-table thead th {
  position: sticky;
  top: 0;
  z-index: 4;
  background: rgb(var(--v-theme-surface));
  border-bottom: 2px solid rgba(var(--v-theme-primary), 0.3);
  padding: 6px 8px;
  text-align: left;
  font-weight: 700;
  text-transform: uppercase;
  font-size: 10px;
  letter-spacing: 0.4px;
  overflow: hidden;
  user-select: none;
  color: rgb(var(--v-theme-on-surface));
}

.cfu-table thead th :deep(.cf-th-inner) {
  white-space: normal;
  justify-content: space-between;
}

/* Movable, resizable headers: the right edge is the resize grip. */
.cfu-table thead th[data-col] { cursor: grab; }

.cfu-table thead th[data-col]::after {
  content: '';
  position: absolute;
  top: 0;
  right: 0;
  width: 7px;
  height: 100%;
  cursor: col-resize;
}

.cfu-table thead th[data-col]:hover::after { background: rgba(var(--v-theme-primary), 0.35); }
.cfu-table thead th.cfu-dragging { opacity: 0.45; }
.cfu-table thead th.cfu-drop-before { box-shadow: inset 3px 0 0 rgb(var(--v-theme-primary)) !important; }
.cfu-table thead th.cfu-drop-after { box-shadow: inset -3px 0 0 rgb(var(--v-theme-primary)) !important; }

.cfu-table :deep(td) {
  padding: 5px 8px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.12);
  border-right: 1px solid rgba(var(--v-border-color), 0.06);
  vertical-align: top;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.cfu-table :deep(td.cfu-cell) { cursor: text; }
.cfu-table :deep(td.cfu-cell-id) { cursor: default; color: rgba(var(--v-theme-on-surface), 0.6); font-weight: 600; }
.cfu-table :deep(td.cfu-cell:not(.cfu-cell-id):hover) { background: rgba(var(--v-theme-primary), 0.05); }

.cfu-table :deep(td.cfu-editing) {
  padding: 2px;
  box-shadow: inset 0 0 0 2px rgb(var(--v-theme-primary));
  background: rgb(var(--v-theme-surface));
}

.cfu-table :deep(tr.cfu-row-new) { background: rgba(var(--v-theme-success), 0.08); }
.cfu-table :deep(tr.cfu-row-closed) { color: rgba(var(--v-theme-on-surface), 0.55); }

/* Unread chat for this user: tinted row with an accent bar until they open its messages. */
.cfu-table :deep(tr.cfu-row-unread) { background: rgba(99, 102, 241, 0.11); font-weight: 600; }
.cfu-table :deep(tr.cfu-row-unread:hover) { background: rgba(99, 102, 241, 0.16); }
.cfu-table :deep(tr.cfu-row-unread > td:first-child) { box-shadow: inset 4px 0 0 #6366f1; }
.cfu-table :deep(.cfu-chat-unread) {
  background: linear-gradient(135deg, #f43f5e, #ec4899);
  box-shadow: 0 2px 8px rgba(244, 63, 94, 0.45);
}
.cfu-table :deep(.cfu-chat-dot) {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #fff;
  animation: cfu-pulse 1.6s ease-in-out infinite;
}

@keyframes cfu-pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.35; transform: scale(0.7); }
}

.cfu-unread-chip {
  color: #fff !important;
  background: linear-gradient(135deg, #6366f1, #8b5cf6) !important;
}
.cfu-table :deep(td.cfu-overdue) { color: rgb(var(--v-theme-error)); font-weight: 700; }

.cfu-table :deep(.cfu-status) {
  display: inline-block;
  padding: 1px 8px;
  border-radius: 10px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}

.cfu-table :deep(.cfu-empty-icon) { opacity: 0.25; }
.cfu-table :deep(td.cfu-cell-docs) { cursor: pointer; }
.cfu-table :deep(td.cfu-cell-docs:hover .cfu-empty-icon) { opacity: 0.7; }
.cfu-table :deep(.cfu-docs) {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-weight: 600;
  color: rgb(var(--v-theme-primary));
}

.cfu-table :deep(td.cfu-cell-rfq),
.cfu-table :deep(td.cfu-cell-refs) { cursor: pointer; }
.cfu-table :deep(td.cfu-cell-rfq:hover .cfu-empty-icon),
.cfu-table :deep(td.cfu-cell-refs:hover .cfu-empty-icon) { opacity: 0.7; }
.cfu-table :deep(.cfu-rfq-wanted) { opacity: 0.8; color: rgb(var(--v-theme-warning)); }
.cfu-table :deep(.cfu-link) {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-weight: 600;
  color: rgb(var(--v-theme-primary));
  text-decoration: none;
}
.cfu-table :deep(.cfu-link:hover) { text-decoration: underline; }
.cfu-table :deep(.cfu-rfq-status) {
  margin-left: 4px;
  font-size: 10px;
  color: rgba(var(--v-theme-on-surface), 0.6);
}
.cfu-table :deep(.cfu-refs) { display: inline-flex; flex-wrap: wrap; gap: 3px; }
.cfu-table :deep(.cfu-ref-chip) {
  padding: 0 7px;
  border-radius: 10px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
  text-decoration: none;
  color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.1);
}
.cfu-table :deep(.cfu-ref-chip:hover) { background: rgba(var(--v-theme-primary), 0.2); }
.cfu-table :deep(.cfu-ref-missing) {
  color: rgb(var(--v-theme-error));
  background: rgba(var(--v-theme-error), 0.1);
  text-decoration: line-through;
}

.cfu-table :deep(td.cfu-cell-chat) { cursor: pointer; }
.cfu-table :deep(td.cfu-cell-chat:hover .cfu-empty-icon) { opacity: 0.7; }
.cfu-table :deep(.cfu-chat) {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 1px 9px 1px 6px;
  border-radius: 999px;
  font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, #6366f1, #8b5cf6);
  box-shadow: 0 2px 6px rgba(99, 102, 241, 0.35);
}

.cfu-table :deep(.cfu-actions) {
  width: 36px;
  min-width: 36px;
  text-align: center;
  padding: 4px 2px;
}

.cfu-table :deep(.cfu-delete-btn) {
  opacity: 0.35;
  border: 0;
  background: transparent;
  color: inherit;
  cursor: pointer;
  border-radius: 4px;
  padding: 2px;
}

.cfu-table :deep(tr:hover .cfu-delete-btn) { opacity: 0.8; }
.cfu-table :deep(.cfu-delete-btn:hover) { opacity: 1; color: rgb(var(--v-theme-error)); }

.cfu-menu-row { cursor: grab; }
.cfu-menu-row:hover { background-color: rgba(var(--v-theme-primary), 0.08); }
.menu-drag-handle { opacity: 0.5; }
.menu-dragging { opacity: 0.45; }
.menu-drop-before { box-shadow: inset 0 2px 0 rgb(var(--v-theme-primary)); }
.menu-drop-after { box-shadow: inset 0 -2px 0 rgb(var(--v-theme-primary)); }
</style>

<style>
/* Editors inside Customer Follow Ups cells (also used by SheetComboInput). */
.sheet-editor-input {
  width: 100%;
  font: inherit;
  font-size: 12px;
  color: inherit;
  background: transparent;
  border: 0;
  outline: none;
  padding: 3px 6px;
}

.sheet-editor-textarea {
  resize: vertical;
  min-height: 60px;
  white-space: pre-wrap;
}

/* While a Customer Follow Ups column is being resized the pointer stays a resize cursor everywhere. */
body.cfu-resizing,
body.cfu-resizing * {
  cursor: col-resize !important;
  user-select: none !important;
}
</style>
