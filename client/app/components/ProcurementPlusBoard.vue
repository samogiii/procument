<template>
  <!--
    Parts (every track + saved-RFQ items, tinted by origin) / Project / WeChat. Two views: by part (paged cards — part, average price per condition,
    suppliers underneath) and by supplier. Filtering, sorting and paging run on the server.
  -->
  <div>
    <!-- Toolbar -->
    <div class="board-bar mb-2">
      <v-btn-toggle v-model="view" mandatory density="compact" variant="outlined" divided color="primary">
        <v-btn value="parts" size="small" prepend-icon="mdi-view-agenda-outline">By part</v-btn>
        <v-btn value="suppliers" size="small" prepend-icon="mdi-domain">By supplier</v-btn>
      </v-btn-toggle>
      <v-text-field
        v-model="search"
        :placeholder="view === 'parts' ? 'Search P/N, alt, description, supplier' : 'Search supplier or P/N'"
        prepend-inner-icon="mdi-magnify"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        style="max-width: 320px; min-width: 200px"
      />
      <template v-if="view === 'parts'">
        <v-select
          v-model="sort"
          :items="SORTS"
          item-title="title"
          item-value="value"
          density="compact"
          variant="outlined"
          hide-details
          prepend-inner-icon="mdi-sort"
          style="max-width: 210px; min-width: 170px"
        />
        <v-switch v-model="showHidden" label="Removed parts" color="primary" density="compact" hide-details inset class="flex-grow-0" />
      </template>
      <v-spacer />
      <v-btn
        v-if="isAll"
        variant="tonal"
        size="small"
        color="deep-purple"
        prepend-icon="mdi-content-copy"
        :loading="copying"
        title="Copy every part number on this tab (with the filters above), one per line — e.g. for an ILS search"
        @click="copyAll"
      >
        Copy all P/Ns
      </v-btn>
      <v-btn color="primary" variant="tonal" size="small" prepend-icon="mdi-plus" @click="emit('add-part')">
        Add {{ track === 'ILS' || isAll ? '' : meta.title + ' ' }}parts
      </v-btn>
      <v-btn size="small" variant="text" icon="mdi-refresh" :loading="loading" title="Refresh" @click="load" />
    </div>

    <template v-if="view === 'parts'">
      <!-- Parts tab: where each part comes from (card tint) — click one to show only those -->
      <div v-if="isAll" class="d-flex align-center flex-wrap gap-2 mb-2">
        <span class="text-caption font-weight-bold text-medium-emphasis">From</span>
        <v-chip-group v-model="origin" selected-class="chip--on" class="py-0">
          <v-chip
            v-for="o in PART_ORIGINS"
            :key="o.value"
            :value="o.value"
            size="small"
            :color="o.color"
            variant="tonal"
            :prepend-icon="o.icon"
            filter
            :class="`origin-chip--${o.value}`"
          >
            {{ o.title }} <span class="ml-1 font-weight-bold">{{ counts.origins?.[o.value] ?? 0 }}</span>
          </v-chip>
        </v-chip-group>
      </div>

      <!-- Status filter -->
      <div class="d-flex align-center flex-wrap gap-2 mb-3">
        <v-chip-group v-model="status" mandatory selected-class="chip--on" class="py-0">
          <v-chip v-for="f in statusFilters" :key="f.value" :value="f.value" size="small" variant="outlined" :color="f.color" filter>
            {{ f.title }} <span class="ml-1 font-weight-bold">{{ f.count }}</span>
          </v-chip>
        </v-chip-group>
        <v-spacer />
        <template v-if="selected.size">
          <span class="text-body-2"><strong>{{ selected.size }}</strong> selected</span>
          <v-btn
            v-for="t in PART_TRACKS.filter(t => t.value !== track)"
            :key="t.value"
            size="small"
            variant="tonal"
            :color="t.color"
            :prepend-icon="t.icon"
            @click="emit('move', [...selected], t.value)"
          >
            To {{ t.title }}
          </v-btn>
          <v-btn size="small" variant="tonal" prepend-icon="mdi-eye-off-outline" @click="emit('hide', [...selected])">Remove</v-btn>
          <v-btn v-if="showHidden" size="small" variant="tonal" prepend-icon="mdi-eye-outline" @click="emit('show', [...selected])">Show again</v-btn>
          <v-btn size="small" variant="text" color="error" prepend-icon="mdi-delete-outline" @click="emit('delete', [...selected])">Delete</v-btn>
          <v-btn size="small" variant="text" @click="selected = new Set()">Clear</v-btn>
        </template>
      </div>

      <!-- Page header row: select page + expand -->
      <div v-if="items.length" class="d-flex align-center gap-2 mb-2 px-1">
        <v-checkbox-btn
          :model-value="pageSelected"
          :indeterminate="!pageSelected && items.some(b => selected.has(b.part.id))"
          density="compact"
          class="flex-grow-0"
          title="Select this page"
          @update:model-value="togglePage"
        />
        <span class="text-caption text-medium-emphasis">
          {{ rangeText }}
        </span>
        <v-spacer />
        <v-btn size="x-small" variant="text" prepend-icon="mdi-unfold-more-horizontal" @click="cardsKey++; startOpen = true">Open all</v-btn>
        <v-btn size="x-small" variant="text" prepend-icon="mdi-unfold-less-horizontal" @click="cardsKey++; startOpen = false">Close all</v-btn>
      </div>

      <v-progress-linear v-if="loading" indeterminate color="primary" height="2" class="mb-2" />
      <div v-if="!loading && !items.length" class="board-empty">
        <v-icon :icon="total || search || status !== 'all' ? 'mdi-filter-remove-outline' : meta.icon" size="48" class="mb-2" :color="meta.color" />
        <template v-if="search || status !== 'all'">
          <div class="text-body-1 font-weight-medium">No parts match these filters</div>
          <v-btn variant="text" color="primary" class="mt-2" @click="search = ''; status = 'all'">Clear filters</v-btn>
        </template>
        <template v-else>
          <div class="text-body-1 font-weight-medium">{{ emptyTitle }}</div>
          <div class="text-body-2 text-medium-emphasis mb-3">{{ emptyHint }}</div>
          <v-btn color="primary" variant="flat" prepend-icon="mdi-playlist-plus" @click="emit('add-part')">Add parts</v-btn>
        </template>
      </div>

      <div :key="cardsKey">
        <ProcurementPlusPartCard
          v-for="b in items"
          :key="b.part.id"
          :board="b"
          :show-owner="showOwner"
          :has-files="hasFiles"
          :start-open="startOpen"
          selectable
          :selected="selected.has(b.part.id)"
          @toggle="toggle(b.part.id)"
          @add-line="openAddLine"
          @edit-line="openEdit"
          @changed="onChanged"
          @toast="toast"
          @edit-part="(p: PpPart) => emit('edit-part', p)"
          @move="(ids: number[], t: string) => emit('move', ids, t)"
          @hide="(ids: number[]) => emit('hide', ids)"
          @show="(ids: number[]) => emit('show', ids)"
          @delete="(ids: number[]) => emit('delete', ids)"
          @add-offer="addOffer"
          @add-to-list="(b: PpBoardPart) => ensurePart(b).then(id => id && load())"
        />
      </div>

      <!-- Paging -->
      <div v-if="total > pageSize || page > 1" class="d-flex align-center justify-center flex-wrap gap-3 mt-2">
        <v-pagination v-model="page" :length="pageCount" :total-visible="7" density="comfortable" rounded="circle" />
        <v-select
          v-model="pageSize"
          :items="[10, 25, 50, 100]"
          label="Per page"
          density="compact"
          variant="outlined"
          hide-details
          style="max-width: 120px"
        />
      </div>
    </template>

    <ProcurementPlusSupplierView v-else :track="track" :search="debouncedSearch" @changed="emit('changed')" />

    <ProcurementPlusAddLineDialog
      v-model="addLine.open"
      :part-id="addLine.partId"
      :part-number="addLine.partNumber"
      @added="onLineAdded"
    />
    <EmailQuoteEditDialog
      v-model="edit.open"
      :row="edit.row"
      :accepted="edit.accepted"
      api-base="/procurement-plus/quotes"
      suppliers-url="/procurement-plus/suppliers"
      @updated="patchLine"
    />
    <v-snackbar v-model="snack" :color="snackColor" :timeout="5000">{{ snackText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import { PART_ORIGINS } from '~/utils/procurementPlus'
import type { PpBoardCounts, PpBoardPage, PpBoardPart, PpPart, PpQuoteLine } from '~/utils/procurementPlus'

const props = withDefaults(defineProps<{
  /** All (the Parts tab: every track + saved-RFQ items not on the list), ILS, Project or WeChat. */
  track: string
  /** SuperAdmin "All users": show whose part it is. */
  showOwner?: boolean
  /** Files are uploaded (ILS): parts with no row in them are flagged. */
  hasFiles?: boolean
}>(), { showOwner: false, hasFiles: false })
const emit = defineEmits<{
  loaded: [count: number]
  changed: []
  'add-part': []
  'edit-part': [part: PpPart]
  move: [ids: number[], track: string]
  hide: [ids: number[]]
  show: [ids: number[]]
  delete: [ids: number[]]
}>()

const SORTS = [
  { title: 'Newest added', value: 'added' },
  { title: 'Soonest RFQ deadline', value: 'deadline' },
  { title: 'Part number A→Z', value: 'pn' },
  { title: 'Most suppliers', value: 'suppliers' },
]

const api = useProcurementPlusApi()
const isAll = computed(() => props.track === 'All')
const meta = computed(() => trackMeta(props.track))
const view = ref<'parts' | 'suppliers'>('parts')

// ── Filters (remembered per board in this browser) ──
const storeKey = `pp-board-${props.track}`
const saved = (() => { try { return JSON.parse(localStorage.getItem(storeKey) || '{}') } catch { return {} } })()
const search = ref<string>('')
const debouncedSearch = ref('')
const status = ref<string>('all')
/** Parts tab: only parts from this tab (parts / project / wechat / rfq); null = all. */
const origin = ref<string | null>(null)
const sort = ref<string>(saved.sort || 'added')
const pageSize = ref<number>(saved.pageSize || 25)
const showHidden = ref(false)
const page = ref(1)
watch([sort, pageSize], () => {
  try { localStorage.setItem(storeKey, JSON.stringify({ sort: sort.value, pageSize: pageSize.value })) } catch { /* private mode */ }
})

let searchTimer: ReturnType<typeof setTimeout> | null = null
watch(search, (q) => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => { debouncedSearch.value = (q || '').trim() }, 350)
})

// ── Data ──
const items = ref<PpBoardPart[]>([])
const total = ref(0)
const counts = ref<PpBoardCounts>({ all: 0, noSupplier: 0, sourcing: 0, ready: 0, done: 0, notInFiles: 0 })
const loading = ref(false)
const cardsKey = ref(0)
const startOpen = ref<boolean | null>(null)
const pageCount = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))
const rangeText = computed(() => {
  const from = (page.value - 1) * pageSize.value + 1
  return `${from}–${from + items.value.length - 1} of ${total.value} part${total.value === 1 ? '' : 's'}`
})

const statusFilters = computed(() => [
  { value: 'all', title: 'All', count: counts.value.all, color: undefined },
  { value: 'none', title: 'No supplier', count: counts.value.noSupplier, color: 'error' },
  { value: 'sourcing', title: 'Sourcing', count: counts.value.sourcing, color: 'info' },
  { value: 'ready', title: 'Ready for Done', count: counts.value.ready, color: 'success' },
  { value: 'done', title: 'Done', count: counts.value.done, color: 'success' },
  ...(props.track === 'ILS' || isAll.value ? [{ value: 'nofiles', title: 'Not in files', count: counts.value.notInFiles, color: 'warning' }] : []),
])

/** The board's filters as query parameters (also used by Copy all). */
function filterParams() {
  const params = new URLSearchParams({
    track: props.track,
    includeHidden: String(showHidden.value),
    status: status.value,
    sort: sort.value,
  })
  if (debouncedSearch.value) params.set('search', debouncedSearch.value)
  if (isAll.value && origin.value) params.set('origin', origin.value)
  return params
}

let loadSeq = 0
async function load() {
  if (view.value !== 'parts') return
  const seq = ++loadSeq
  loading.value = true
  try {
    const params = filterParams()
    params.set('page', String(page.value))
    params.set('pageSize', String(pageSize.value))
    const res = await api.get<PpBoardPage>(`/procurement-plus/board?${params}`)
    if (seq !== loadSeq) return // a newer request is on its way
    items.value = res.items
    total.value = res.total
    counts.value = res.counts
    if (res.page !== page.value) page.value = res.page
    // Parts tab: the tab badge counts every origin, not only the one picked.
    emit('loaded', isAll.value ? Object.values(res.counts.origins ?? {}).reduce((n, c) => n + c, 0) : res.counts.all)
  } finally {
    if (seq === loadSeq) loading.value = false
  }
}

// New filter → back to page 1; page change → just load.
watch([debouncedSearch, status, origin, sort, pageSize, showHidden, () => api.asUser.value], () => {
  if (page.value !== 1) page.value = 1
  else load()
})
watch(page, load)
watch(view, (v) => { if (v === 'parts') load() })

// ── Selection (kept across pages) ──
const selected = ref(new Set<number>())
// Saved-RFQ items not on the list (id < 0) cannot be selected — they are not parts yet.
const selectable = computed(() => items.value.filter(b => b.part.id > 0))
const pageSelected = computed(() => selectable.value.length > 0 && selectable.value.every(b => selected.value.has(b.part.id)))
function toggle(id: number) {
  const next = new Set(selected.value)
  next.has(id) ? next.delete(id) : next.add(id)
  selected.value = next
}
function togglePage() {
  const next = new Set(selected.value)
  const on = !pageSelected.value
  for (const b of selectable.value) on ? next.add(b.part.id) : next.delete(b.part.id)
  selected.value = next
}

const emptyTitle = computed(() => props.track === 'ILS' || isAll.value ? 'No parts yet' : `No ${meta.value.title} parts`)
const emptyHint = computed(() => props.track === 'ILS' || isAll.value
  ? 'Pick parts from your RFQs (or add custom ones), load RFQs on By RFQ, upload the ILS files — their suppliers show under each part.'
  : `Add a part as ${meta.value.title}, or move "No supplier" parts here from Suppliers available.`)

async function onChanged() {
  await load()
  emit('changed')
}

// ── Edit a line (autosaves) ──
const edit = reactive<{ open: boolean; row: any; accepted: boolean }>({ open: false, row: null, accepted: false })
function openEdit(l: PpQuoteLine) {
  edit.row = l.line
  edit.accepted = l.status === 'Accepted'
  edit.open = true
}
function patchLine(row: any) {
  for (const b of items.value) {
    const l = b.lines.find(x => x.line.id === row.id) ?? b.emailLines?.find(x => x.line.id === row.id)
    if (l) { l.line = { ...l.line, ...row }; break }
  }
}

// ── Add a supplier under a part ──
const addLine = reactive({ open: false, partId: 0, partNumber: '' })
async function openAddLine(b: PpBoardPart) {
  const id = await ensurePart(b)
  if (id) Object.assign(addLine, { open: true, partId: id, partNumber: b.part.partNumber })
}

/** The part's id — a saved-RFQ item not on the list (id < 0) is put on it first (as a Parts / ILS part). */
async function ensurePart(b: PpBoardPart): Promise<number | null> {
  if (b.part.id > 0) return b.part.id
  try {
    const res = await api.post<{ ids: number[] }>('/procurement-plus/parts', { track: 'ILS', items: [{ rfqItemId: b.part.rfqItemId }] })
    const id = res.ids?.[0] ?? null
    if (!id) toast('Could not add the part', 'error')
    else emit('changed')
    return id
  } catch (e: any) {
    toast(e?.data?.message || 'Could not add the part', 'error')
    return null
  }
}

// ── A file row → a supplier line (price, qty, condition from the file) ──
async function addOffer(b: PpBoardPart, rows: PpOfferRow[], methods: string[]) {
  const id = await ensurePart(b)
  if (!id || !rows.length) return
  let added = 0
  try {
    // One line per picked P/N (the part itself / each alternative).
    for (const row of rows) {
      await api.post<PpQuoteLine>(`/procurement-plus/parts/${id}/lines`, { offerId: row.offerId, methods })
      added++
    }
    const first = rows[0]!
    toast(`${first.preferredName || first.supplierName || first.companyRaw}: ${added} line${added === 1 ? '' : 's'} added to ${b.part.partNumber}`)
  } catch (e: any) {
    toast(e?.data?.message || 'Could not add the supplier', 'error')
  }
  if (added) await onChanged()
}

// ── Copy every part number on the tab (all pages, with the filters) ──
const copying = ref(false)
async function copyAll() {
  copying.value = true
  try {
    const names = await api.get<string[]>(`/procurement-plus/board/part-numbers?${filterParams()}`)
    if (!names.length) { toast('No part numbers to copy', 'warning'); return }
    const ok = copyText(names.join('\n'))
    toast(ok ? `${names.length} part number${names.length === 1 ? '' : 's'} copied` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
  } catch (e: any) {
    toast(e?.data?.message || 'Could not load the part numbers', 'error')
  } finally {
    copying.value = false
  }
}
async function onLineAdded(line: PpQuoteLine) {
  await onChanged()
  openEdit(line) // straight into its price
}

const snack = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') { snackText.value = text; snackColor.value = color; snack.value = true }

onMounted(load)
defineExpose({ load })
</script>

<style scoped>
.board-bar { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; }
.board-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center;
  padding: 48px 16px; border-radius: 12px;
  border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
}
:deep(.chip--on) { font-weight: 700; }
</style>
