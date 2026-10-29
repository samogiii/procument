<template>
  <div class="task-page">
    <!-- ── Toolbar ─────────────────────────────────────────────────────────── -->
    <div class="task-toolbar d-flex align-center flex-wrap ga-2 px-4 py-3">
      <div class="mr-2">
        <div class="text-h6 font-weight-bold d-flex align-center">
          <v-icon icon="mdi-view-column-outline" class="mr-2" color="primary" />Task Manager
        </div>
        <div v-if="owner?.isOther" class="text-caption text-warning">Viewing {{ owner.name }}'s board</div>
      </div>

      <v-btn-toggle v-model="view" mandatory density="compact" variant="text" color="primary" class="mr-2">
        <v-btn value="board" size="small" prepend-icon="mdi-view-column">Board</v-btn>
        <v-btn value="calendar" size="small" prepend-icon="mdi-calendar-month">Calendar</v-btn>
      </v-btn-toggle>

      <v-autocomplete
        v-if="isSuperAdmin"
        :model-value="selectedUserId"
        :items="users"
        item-title="name"
        item-value="id"
        density="compact"
        variant="outlined"
        hide-details
        prepend-inner-icon="mdi-account"
        placeholder="My board"
        clearable
        style="max-width: 220px;"
        @update:model-value="selectUser"
      >
        <template #item="{ props: itemProps, item }">
          <v-list-item v-bind="itemProps" :subtitle="`${item.raw.role} · ${item.raw.cardCount} cards`" />
        </template>
      </v-autocomplete>

      <v-spacer />

      <v-text-field
        v-model="search"
        prepend-inner-icon="mdi-magnify"
        placeholder="Search cards, tags, refs…"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        style="max-width: 260px; min-width: 180px;"
      />
      <v-select
        v-model="priorityFilter"
        :items="TASK_PRIORITIES.map(p => p.value)"
        multiple
        chips
        closable-chips
        density="compact"
        variant="outlined"
        hide-details
        placeholder="Priority"
        prepend-inner-icon="mdi-flag-outline"
        style="max-width: 220px; min-width: 150px;"
      />
      <v-autocomplete
        v-model="tagFilter"
        :items="tags"
        item-title="name"
        item-value="id"
        multiple
        chips
        closable-chips
        density="compact"
        variant="outlined"
        hide-details
        placeholder="Tags"
        prepend-inner-icon="mdi-tag-outline"
        style="max-width: 260px; min-width: 150px;"
      >
        <template #chip="{ props: chipProps, item }">
          <v-chip v-bind="chipProps" size="small" :color="item.raw.color" variant="flat" label>{{ item.raw.name }}</v-chip>
        </template>
      </v-autocomplete>
      <v-btn color="primary" prepend-icon="mdi-plus" :disabled="!boardColumns.length" @click="openNewCard()">Add Task</v-btn>
      <v-btn icon="mdi-refresh" variant="text" size="small" :loading="loading" title="Reload" @click="loadBoard" />
    </div>
    <v-divider />

    <div v-if="loading && !boardColumns.length" class="d-flex justify-center align-center" style="height: 60vh;">
      <v-progress-circular indeterminate color="primary" size="48" />
    </div>
    <v-alert v-else-if="loadError" type="error" variant="tonal" class="ma-4">{{ loadError }}</v-alert>

    <!-- ── Board ───────────────────────────────────────────────────────────── -->
    <div v-else-if="view === 'board'" class="task-board">
      <div
        v-for="column in visibleColumns"
        :key="column.meta.id"
        class="task-column"
        :class="{ 'task-column--drop': columnDropId === column.meta.id }"
        :style="{ '--col': column.meta.color }"
        @dragover="onColumnDragOver($event, column.meta.id)"
        @drop="onColumnDrop($event, column.meta.id)"
      >
        <div
          class="task-column__head"
          draggable="true"
          @dragstart="onColumnDragStart($event, column.meta.id)"
          @dragend="endDrag"
        >
          <span class="task-column__pill">
            <v-icon :icon="column.meta.isDone ? 'mdi-check-circle' : 'mdi-circle-slice-5'" size="14" class="mr-1" />{{ column.meta.name }}
          </span>
          <span class="text-body-2 text-medium-emphasis ml-2">{{ column.cards.length }}<template v-if="column.cards.length !== column.total"> / {{ column.total }}</template></span>
          <v-spacer />
          <v-menu location="bottom end">
            <template #activator="{ props: menuProps }">
              <v-btn v-bind="menuProps" icon="mdi-dots-horizontal" size="x-small" variant="text" />
            </template>
            <v-list density="compact">
              <v-list-item prepend-icon="mdi-pencil" title="Edit column" @click="openColumnEdit(column.meta)" />
              <v-list-item prepend-icon="mdi-plus" title="Add column after" @click="openColumnEdit(null, column.meta.id)" />
              <v-list-item prepend-icon="mdi-delete-outline" title="Delete column" base-color="error" :disabled="column.total > 0" @click="deleteColumn(column.meta)" />
            </v-list>
          </v-menu>
          <v-btn icon="mdi-plus" size="x-small" variant="text" title="Add a task on top" @click="openComposer(column.meta.id, true)" />
        </div>

        <div class="task-column__body" @dragover="onCardsDragOver($event, column.meta.id)">
          <!-- Composer on top -->
          <div v-if="composer.columnId === column.meta.id && composer.top" class="task-composer">
            <input
              v-model="composer.title"
              v-autofocus
              class="task-composer__input"
              placeholder="Task name + Enter"
              maxlength="500"
              @keydown.enter.prevent="createFromComposer"
              @keydown.esc="closeComposer"
              @blur="!composer.title.trim() && closeComposer()"
            >
          </div>

          <template v-for="card in column.cards" :key="card.id">
            <div v-if="dropTarget.columnId === column.meta.id && dropTarget.beforeId === card.id" class="task-drop-line" />
            <div :data-card-id="card.id" :class="{ 'task-dragging': drag.cardId === card.id }" @dragstart="onCardDragStart($event, card.id)" @dragend="endDrag">
              <TaskBoardCard
                :card="card"
                :tag-map="tagMap"
                :tags="tags"
                :today="today"
                :done="column.meta.isDone"
                :sending="sendingIds.has(card.id)"
                @open="openCardId = card.id"
                @refs="refsCard = card"
                @delete="deleteCard(card.id)"
                @field="(field, value) => updateField(card.id, field, value)"
                @tags="(names) => setTags(card.id, names)"
                @comment="(text) => addComment(card.id, text)"
              />
            </div>
          </template>
          <div v-if="dropTarget.columnId === column.meta.id && dropTarget.beforeId === null" class="task-drop-line" />

          <!-- Composer at the bottom -->
          <div v-if="composer.columnId === column.meta.id && !composer.top" class="task-composer">
            <input
              v-model="composer.title"
              v-autofocus
              class="task-composer__input"
              placeholder="Task name + Enter"
              maxlength="500"
              @keydown.enter.prevent="createFromComposer"
              @keydown.esc="closeComposer"
              @blur="!composer.title.trim() && closeComposer()"
            >
          </div>
          <button v-else type="button" class="task-column__add" @click="openComposer(column.meta.id, false)">
            <v-icon icon="mdi-plus" size="16" class="mr-1" />Add Task
          </button>
        </div>
      </div>

      <button type="button" class="task-board__add-column" @click="openColumnEdit(null)">
        <v-icon icon="mdi-plus" class="mr-1" />Add column
      </button>
    </div>

    <!-- ── Calendar ────────────────────────────────────────────────────────── -->
    <div v-else class="pa-4">
      <TaskBoardCalendar
        :cards="calendarCards"
        :done-column-ids="doneColumnIds"
        :today="today"
        @open="(id) => (openCardId = id)"
        @create="(date) => openNewCard(date)"
        @move-date="(id, date) => updateField(id, 'dueDate', date)"
      />
    </div>

    <!-- ── Dialogs ─────────────────────────────────────────────────────────── -->
    <TaskCardDialog
      ref="cardDialog"
      :card-id="openCardId"
      :columns="columnList"
      :tags="tags"
      :today="today"
      :current-user-name="currentUserName"
      @close="openCardId = null"
      @changed="applyDetail"
      @move="(id, columnId) => moveCard(id, columnId, columnSize(columnId))"
      @delete="deleteCard"
      @notify="notify"
    />

    <TaskRefsDialog :card="refsCard" @close="refsCard = null" @saved="applyDetail" @notify="notify" />

    <!-- New card (toolbar button, or a day of the calendar) -->
    <v-dialog v-model="newCard.open" max-width="520">
      <v-card>
        <v-card-title class="pa-4">New task</v-card-title>
        <v-card-text class="pt-0">
          <v-text-field v-model="newCard.title" label="Task name" autofocus maxlength="500" @keydown.enter="createNewCard" />
          <v-select v-model="newCard.columnId" :items="columnList" item-title="name" item-value="id" label="Column" />
          <div class="d-flex align-center ga-3">
            <v-btn-toggle v-model="newCard.priority" mandatory density="compact" variant="outlined" divided>
              <v-btn v-for="p in TASK_PRIORITIES" :key="p.value" :value="p.value" size="small">
                <v-icon :icon="p.icon" :color="p.color" size="16" class="mr-1" />{{ p.value }}
              </v-btn>
            </v-btn-toggle>
            <TaskDueMenu v-model="newCard.dueDate" :today="today">
              <template #activator="{ props: dueProps }">
                <v-btn v-bind="dueProps" size="small" variant="tonal" prepend-icon="mdi-calendar">
                  {{ newCard.dueDate ? dueDateLabel(newCard.dueDate, today) : 'Due date' }}
                </v-btn>
              </template>
            </TaskDueMenu>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="newCard.open = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="newCard.saving" :disabled="!newCard.title.trim() || !newCard.columnId" @click="createNewCard">Create</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Add / edit column -->
    <v-dialog v-model="columnEdit.open" max-width="420">
      <v-card>
        <v-card-title class="pa-4">{{ columnEdit.id ? 'Edit column' : 'New column' }}</v-card-title>
        <v-card-text class="pt-0">
          <v-text-field v-model="columnEdit.name" label="Name" autofocus maxlength="100" @keydown.enter="saveColumn" />
          <div class="text-caption text-medium-emphasis mb-1">Color</div>
          <div class="d-flex flex-wrap ga-2 mb-3">
            <button
              v-for="color in TASK_COLUMN_COLORS"
              :key="color"
              type="button"
              class="task-swatch"
              :class="{ 'task-swatch--active': columnEdit.color === color }"
              :style="{ background: color }"
              @click="columnEdit.color = color"
            />
          </div>
          <v-switch v-model="columnEdit.isDone" color="success" hide-details density="compact" label="Done column (cards here are finished — no overdue warning)" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="columnEdit.open = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="columnEdit.saving" :disabled="!columnEdit.name.trim()" @click="saveColumn">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" timeout="3500">{{ snackbar.text }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import { createKanban } from '@vuetify/v0'
import type { KanbanColumnTicketInput, SortableTicketInput } from '@vuetify/v0'
import { useAuthStore } from '@/stores/auth'
import {
  TASK_COLUMN_COLORS,
  TASK_PRIORITIES,
  dueDateLabel,
  taskRefText,
  toDateKey,
  type TaskBoard,
  type TaskBoardUser,
  type TaskCard,
  type TaskCardDetail,
  type TaskColumn,
  type TaskPriority,
  type TaskTag,
} from '~/utils/taskBoard'

type ColumnMeta = Pick<TaskColumn, 'id' | 'name' | 'color' | 'isDone'>

/** Kanban tickets carry only ids: order lives in the kanban, card data in `cards`. */
interface CardTicket extends SortableTicketInput { value: number }
interface ColumnTicket extends KanbanColumnTicketInput<CardTicket> { value: number }

const VIEW_STORAGE_KEY = 'task-board-view'

const api = useApi()
const authStore = useAuthStore()
const route = useRoute()
const router = useRouter()

const isSuperAdmin = computed(() => authStore.isSuperAdmin)
const currentUserName = computed(() => authStore.user?.name ?? '')

const vAutofocus = { mounted: (el: HTMLElement) => el.focus() }

// ── Board state ──────────────────────────────────────────────────────────────

const loading = ref(true)
const loadError = ref('')
const owner = ref<{ id: number; name: string; isOther: boolean } | null>(null)
const today = ref(toDateKey(new Date()))
const tags = ref<TaskTag[]>([])
const tagMap = computed(() => new Map(tags.value.map(t => [t.id, t])))

const kanban = createKanban<CardTicket, ColumnTicket>()
/** The kanban registries are not reactive; every structural change bumps this. */
const version = shallowRef(0)
const columnMeta = shallowReactive(new Map<number, ColumnMeta>())
const cards = shallowReactive(new Map<number, TaskCard>())
const sendingIds = shallowReactive(new Set<number>())

function bump() {
  version.value++
}

function columnTicket(id: number) {
  return kanban.columns.get(id)
}

/** Columns in order, each with its card ids in order. */
const boardColumns = computed(() => {
  void version.value
  return [...kanban.columns.values()]
    .sort((a, b) => a.index - b.index)
    .map(col => ({
      meta: columnMeta.get(col.id as number),
      cardIds: [...col.items.values()].sort((a, b) => a.index - b.index).map(t => t.id as number),
    }))
    .filter((col): col is { meta: ColumnMeta; cardIds: number[] } => !!col.meta)
})

const columnList = computed(() => boardColumns.value.map(c => c.meta))
const doneColumnIds = computed(() => new Set(columnList.value.filter(c => c.isDone).map(c => c.id)))

function columnSize(columnId: number) {
  return boardColumns.value.find(c => c.meta.id === columnId)?.cardIds.length ?? 0
}

// ── Filters ──────────────────────────────────────────────────────────────────

const search = ref<string | null>('')
const priorityFilter = ref<TaskPriority[]>([])
const tagFilter = ref<number[]>([])

const filtersActive = computed(() => !!search.value?.trim() || priorityFilter.value.length > 0 || tagFilter.value.length > 0)

function matches(card: TaskCard) {
  if (priorityFilter.value.length && !priorityFilter.value.includes(card.priority)) return false
  if (tagFilter.value.length && !tagFilter.value.some(id => card.tagIds.includes(id))) return false
  const q = search.value?.trim().toLowerCase()
  if (!q) return true
  return card.title.toLowerCase().includes(q)
    || card.tagIds.some(id => tagMap.value.get(id)?.name.toLowerCase().includes(q))
    || card.refs.some(r => taskRefText(r).toLowerCase().includes(q))
    || !!card.lastComment?.text.toLowerCase().includes(q)
}

const visibleColumns = computed(() => boardColumns.value.map((col) => {
  const all = col.cardIds.map(id => cards.get(id)).filter((c): c is TaskCard => !!c)
  return { meta: col.meta, total: all.length, cards: filtersActive.value ? all.filter(matches) : all }
}))

const calendarCards = computed(() => visibleColumns.value.flatMap(c => c.cards))

// ── Loading ──────────────────────────────────────────────────────────────────

const users = ref<TaskBoardUser[]>([])
const selectedUserId = computed<number | null>(() => {
  const raw = Number(route.query.user)
  return isSuperAdmin.value && Number.isFinite(raw) && raw > 0 ? raw : null
})

function selectUser(id: number | null) {
  router.replace({ query: { ...route.query, user: id ? String(id) : undefined } })
}

async function loadUsers() {
  if (!isSuperAdmin.value) return
  try {
    users.value = await api.get<TaskBoardUser[]>('/task-board/users')
  } catch {
    users.value = []
  }
}

let loadGeneration = 0

async function loadBoard() {
  const generation = ++loadGeneration
  loading.value = true
  loadError.value = ''
  try {
    const board = await api.get<TaskBoard>('/task-board', {
      query: selectedUserId.value ? { userId: selectedUserId.value } : {},
    })
    if (generation !== loadGeneration) return
    applyBoard(board)
  } catch (error: any) {
    if (generation !== loadGeneration) return
    loadError.value = error?.status === 403 || error?.response?.status === 403
      ? 'You do not have access to this board.'
      : error?.data?.message || 'Could not load the board.'
  } finally {
    if (generation === loadGeneration) loading.value = false
  }
}

function applyBoard(board: TaskBoard) {
  kanban.dispose()
  columnMeta.clear()
  cards.clear()
  for (const column of board.columns) {
    columnMeta.set(column.id, { id: column.id, name: column.name, color: column.color, isDone: column.isDone })
    const ticket = kanban.columns.register({ id: column.id, value: column.id })
    for (const card of column.cards) {
      cards.set(card.id, card)
      ticket.items.register({ id: card.id, value: card.id })
    }
  }
  tags.value = board.tags
  today.value = board.serverToday
  owner.value = { id: board.ownerUserId, name: board.ownerName, isOther: board.isOtherUser }
  bump()
}

/** A card save returned the full card: refresh it on the board, plus the board's tags. */
function applyDetail(detail: TaskCardDetail) {
  if (cards.has(detail.card.id)) cards.set(detail.card.id, detail.card)
  tags.value = detail.tags
}

// ── Snackbar ─────────────────────────────────────────────────────────────────

const snackbar = reactive({ show: false, text: '', color: 'success' })

function notify(text: string, color = 'success') {
  snackbar.text = text
  snackbar.color = color
  snackbar.show = true
}

function errorText(error: any, fallback: string) {
  return error?.data?.message || fallback
}

// ── Card actions ─────────────────────────────────────────────────────────────

const openCardId = ref<number | null>(null)
const refsCard = ref<TaskCard | null>(null)
const cardDialog = ref<{ reload: () => void } | null>(null)

async function updateField(cardId: number, field: 'title' | 'description' | 'priority' | 'dueDate', value: string | null) {
  const card = cards.get(cardId)
  if (!card) return
  // Shown at once; the server's answer replaces it.
  if (field === 'priority') cards.set(cardId, { ...card, priority: value as TaskPriority })
  if (field === 'dueDate') cards.set(cardId, { ...card, dueDate: value })
  try {
    applyDetail(await api.patch<TaskCardDetail>(`/task-board/cards/${cardId}`, { field, value }))
    if (openCardId.value === cardId) cardDialog.value?.reload()
  } catch (error: any) {
    cards.set(cardId, card)
    notify(errorText(error, 'Could not save the change.'), 'error')
  }
}

async function setTags(cardId: number, names: string[]) {
  try {
    applyDetail(await api.put<TaskCardDetail>(`/task-board/cards/${cardId}/tags`, { names }))
  } catch (error: any) {
    notify(errorText(error, 'Could not save the tags.'), 'error')
  }
}

async function addComment(cardId: number, text: string) {
  sendingIds.add(cardId)
  try {
    applyDetail(await api.post<TaskCardDetail>(`/task-board/cards/${cardId}/comments`, { text }))
    if (openCardId.value === cardId) cardDialog.value?.reload()
  } catch (error: any) {
    notify(errorText(error, 'Could not send the message.'), 'error')
  } finally {
    sendingIds.delete(cardId)
  }
}

async function deleteCard(cardId: number) {
  const card = cards.get(cardId)
  if (!card || !confirm(`Delete "${card.title}" with its messages and history?`)) return
  try {
    tags.value = await api.delete<TaskTag[]>(`/task-board/cards/${cardId}`)
    columnTicket(card.columnId)?.items.unregister(cardId)
    cards.delete(cardId)
    if (openCardId.value === cardId) openCardId.value = null
    bump()
    notify('Card deleted.')
  } catch (error: any) {
    notify(errorText(error, 'Could not delete the card.'), 'error')
  }
}

/**
 * Moves the card to `index` of the target column — the index in that column's list without the card,
 * which is what both kanban.transfer / items.move and the server expect.
 */
async function moveCard(cardId: number, toColumnId: number, index: number) {
  const card = cards.get(cardId)
  if (!card) return
  const moved = kanban.transfer(cardId, toColumnId, index)
  if (!moved) return
  if (card.columnId !== toColumnId) cards.set(cardId, { ...card, columnId: toColumnId })
  bump()
  try {
    await api.put(`/task-board/cards/${cardId}/move`, { columnId: toColumnId, index })
    if (openCardId.value === cardId) cardDialog.value?.reload()
  } catch (error: any) {
    notify(errorText(error, 'Could not move the card — the board was reloaded.'), 'error')
    await loadBoard()
  }
}

function registerCard(card: TaskCard, top: boolean) {
  const ticket = columnTicket(card.columnId)
  if (!ticket) return
  cards.set(card.id, card)
  ticket.items.register({ id: card.id, value: card.id })
  if (top) ticket.items.move(card.id, 0)
  bump()
}

async function createCard(body: { columnId: number; title: string; priority?: TaskPriority; dueDate?: string | null; top?: boolean }) {
  const card = await api.post<TaskCard>('/task-board/cards', body)
  registerCard(card, !!body.top)
  return card
}

// Inline composer of a column

const composer = reactive({ columnId: null as number | null, top: false, title: '', saving: false })

function openComposer(columnId: number, top: boolean) {
  composer.columnId = columnId
  composer.top = top
  composer.title = ''
}

function closeComposer() {
  composer.columnId = null
  composer.title = ''
}

async function createFromComposer() {
  const title = composer.title.trim()
  if (!title || composer.columnId === null || composer.saving) return
  composer.saving = true
  try {
    await createCard({ columnId: composer.columnId, title, top: composer.top })
    composer.title = '' // stays open for the next one
  } catch (error: any) {
    notify(errorText(error, 'Could not create the card.'), 'error')
  } finally {
    composer.saving = false
  }
}

// New card dialog

const newCard = reactive({
  open: false,
  saving: false,
  title: '',
  columnId: null as number | null,
  priority: 'Normal' as TaskPriority,
  dueDate: null as string | null,
})

function openNewCard(dueDate: string | null = null) {
  newCard.title = ''
  newCard.columnId = columnList.value.find(c => !c.isDone)?.id ?? columnList.value[0]?.id ?? null
  newCard.priority = 'Normal'
  newCard.dueDate = dueDate
  newCard.open = true
}

async function createNewCard() {
  const title = newCard.title.trim()
  if (!title || !newCard.columnId || newCard.saving) return
  newCard.saving = true
  try {
    await createCard({ columnId: newCard.columnId, title, priority: newCard.priority, dueDate: newCard.dueDate })
    newCard.open = false
  } catch (error: any) {
    notify(errorText(error, 'Could not create the card.'), 'error')
  } finally {
    newCard.saving = false
  }
}

// ── Columns ──────────────────────────────────────────────────────────────────

const columnEdit = reactive({
  open: false,
  saving: false,
  id: null as number | null,
  /** New column goes right after this one (null = at the end). */
  afterId: null as number | null,
  name: '',
  color: TASK_COLUMN_COLORS[0]!,
  isDone: false,
})

function openColumnEdit(column: ColumnMeta | null, afterId: number | null = null) {
  columnEdit.id = column?.id ?? null
  columnEdit.afterId = afterId
  columnEdit.name = column?.name ?? ''
  columnEdit.color = column?.color ?? TASK_COLUMN_COLORS[0]!
  columnEdit.isDone = column?.isDone ?? false
  columnEdit.open = true
}

async function saveColumn() {
  const name = columnEdit.name.trim()
  if (!name || columnEdit.saving) return
  columnEdit.saving = true
  try {
    if (columnEdit.id) {
      const saved = await api.put<TaskColumn>(`/task-board/columns/${columnEdit.id}`, { name, color: columnEdit.color, isDone: columnEdit.isDone })
      columnMeta.set(saved.id, { id: saved.id, name: saved.name, color: saved.color, isDone: saved.isDone })
    } else {
      const saved = await api.post<TaskColumn>('/task-board/columns', { userId: owner.value?.id, name, color: columnEdit.color })
      if (columnEdit.isDone) await api.put(`/task-board/columns/${saved.id}`, { isDone: true })
      columnMeta.set(saved.id, { id: saved.id, name: saved.name, color: saved.color, isDone: columnEdit.isDone })
      kanban.columns.register({ id: saved.id, value: saved.id })
      bump()
      if (columnEdit.afterId !== null) {
        const after = columnTicket(columnEdit.afterId)
        if (after) await moveColumn(saved.id, after.index + 1)
      }
    }
    columnEdit.open = false
  } catch (error: any) {
    notify(errorText(error, 'Could not save the column.'), 'error')
  } finally {
    columnEdit.saving = false
  }
}

async function deleteColumn(column: ColumnMeta) {
  if (!confirm(`Delete the column "${column.name}"?`)) return
  try {
    await api.delete(`/task-board/columns/${column.id}`)
    kanban.columns.unregister(column.id)
    columnMeta.delete(column.id)
    bump()
  } catch (error: any) {
    notify(errorText(error, 'Could not delete the column.'), 'error')
  }
}

async function moveColumn(columnId: number, index: number) {
  if (!kanban.columns.move(columnId, index)) return
  bump()
  try {
    await api.put('/task-board/columns/order', { userId: owner.value?.id, ids: boardColumns.value.map(c => c.meta.id) })
  } catch (error: any) {
    notify(errorText(error, 'Could not reorder the columns — the board was reloaded.'), 'error')
    await loadBoard()
  }
}

// ── Drag and drop (native HTML5; the kanban does the bookkeeping) ────────────

const drag = reactive({ cardId: null as number | null, columnId: null as number | null })
const dropTarget = reactive({ columnId: null as number | null, beforeId: null as number | null })
const columnDropId = ref<number | null>(null)

function endDrag() {
  drag.cardId = null
  drag.columnId = null
  dropTarget.columnId = null
  dropTarget.beforeId = null
  columnDropId.value = null
}

function onCardDragStart(event: DragEvent, cardId: number) {
  event.stopPropagation()
  drag.cardId = cardId
  event.dataTransfer?.setData('text/plain', `card:${cardId}`)
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

function onColumnDragStart(event: DragEvent, columnId: number) {
  drag.columnId = columnId
  event.dataTransfer?.setData('text/plain', `column:${columnId}`)
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

/** Over a column's cards: the drop goes before the first card whose middle is below the pointer. */
function onCardsDragOver(event: DragEvent, columnId: number) {
  if (drag.cardId === null) return
  event.preventDefault()
  event.stopPropagation()
  let beforeId: number | null = null
  for (const el of (event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('[data-card-id]')) {
    const rect = el.getBoundingClientRect()
    if (event.clientY < rect.top + rect.height / 2) {
      beforeId = Number(el.dataset.cardId)
      break
    }
  }
  dropTarget.columnId = columnId
  dropTarget.beforeId = beforeId
}

function onColumnDragOver(event: DragEvent, columnId: number) {
  if (drag.cardId !== null) {
    // Header / empty part of the column: drop at the end.
    event.preventDefault()
    dropTarget.columnId = columnId
    dropTarget.beforeId = null
  } else if (drag.columnId !== null) {
    event.preventDefault()
    columnDropId.value = columnId === drag.columnId ? null : columnId
  }
}

function onColumnDrop(event: DragEvent, columnId: number) {
  event.preventDefault()
  const cardId = drag.cardId
  const draggedColumnId = drag.columnId
  const beforeId = dropTarget.beforeId
  endDrag()

  if (cardId !== null) {
    if (beforeId === cardId) return
    const ids = (boardColumns.value.find(c => c.meta.id === columnId)?.cardIds ?? []).filter(id => id !== cardId)
    // "Before a card" is its place in the full column (filters may hide cards); otherwise the very end.
    const index = beforeId !== null && ids.includes(beforeId) ? ids.indexOf(beforeId) : ids.length
    const card = cards.get(cardId)
    if (card?.columnId === columnId && columnTicket(columnId)?.items.get(cardId)?.index === index) return
    moveCard(cardId, columnId, index)
  } else if (draggedColumnId !== null && draggedColumnId !== columnId) {
    const target = columnTicket(columnId)
    if (target) moveColumn(draggedColumnId, target.index)
  }
}

// ── View ─────────────────────────────────────────────────────────────────────

function readView(): 'board' | 'calendar' {
  try {
    return localStorage.getItem(VIEW_STORAGE_KEY) === 'calendar' ? 'calendar' : 'board'
  } catch {
    return 'board'
  }
}

const view = ref<'board' | 'calendar'>(readView())

watch(view, (value) => {
  try {
    localStorage.setItem(VIEW_STORAGE_KEY, value)
  } catch { /* storage blocked — the view is just not remembered */ }
})

watch(selectedUserId, () => {
  openCardId.value = null
  refsCard.value = null
  closeComposer()
  loadBoard()
})

onMounted(() => {
  loadBoard()
  loadUsers()
})

onBeforeUnmount(() => kanban.dispose())
</script>

<style scoped>
.task-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 100px);
  min-height: 480px;
}

.task-board {
  flex: 1;
  min-height: 0;
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 16px;
  overflow-x: auto;
}

.task-column {
  --col: #6E7681;
  flex: 0 0 280px;
  max-height: 100%;
  display: flex;
  flex-direction: column;
  border-radius: 12px;
  background: color-mix(in srgb, var(--col) 9%, rgb(var(--v-theme-surface)));
  border: 1px solid transparent;
  transition: border-color 0.15s ease;
}

.task-column--drop {
  border-color: rgb(var(--v-theme-primary));
}

.task-column__head {
  display: flex;
  align-items: center;
  padding: 10px 10px 6px;
  cursor: grab;
}

.task-column__pill {
  display: inline-flex;
  align-items: center;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  color: #fff;
  background: var(--col);
  border-radius: 6px;
  padding: 2px 8px;
  max-width: 170px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-column__body {
  flex: 1;
  min-height: 60px;
  overflow-y: auto;
  padding: 4px 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.task-column__add {
  display: flex;
  align-items: center;
  font-size: 0.85rem;
  color: var(--col);
  filter: brightness(1.3);
  padding: 6px 8px;
  border-radius: 8px;
  text-align: left;
}

.task-column__add:hover {
  background: rgba(var(--v-theme-on-surface), 0.06);
}

.task-composer__input {
  width: 100%;
  font-size: 0.9rem;
  padding: 10px 12px;
  border-radius: 10px;
  border: 1px solid rgb(var(--v-theme-primary));
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  outline: none;
}

.task-drop-line {
  height: 3px;
  border-radius: 2px;
  background: rgb(var(--v-theme-primary));
  margin: -3px 0;
}

.task-dragging {
  opacity: 0.4;
}

.task-board__add-column {
  flex: 0 0 200px;
  display: flex;
  align-items: center;
  justify-content: center;
  height: 44px;
  border-radius: 12px;
  border: 1px dashed rgba(var(--v-border-color), 0.4);
  color: rgba(var(--v-theme-on-surface), 0.7);
}

.task-board__add-column:hover {
  background: rgba(var(--v-theme-on-surface), 0.05);
}

.task-swatch {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  border: 2px solid transparent;
}

.task-swatch--active {
  border-color: rgb(var(--v-theme-on-surface));
}
</style>
