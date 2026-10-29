<template>
  <v-dialog :model-value="cardId !== null" max-width="1280" scrollable @update:model-value="(open) => !open && emit('close')">
    <v-card class="task-dialog">
      <!-- Header: column (status) + close -->
      <div class="d-flex align-center ga-2 px-4 py-3 border-b">
        <v-menu v-if="card" location="bottom start">
          <template #activator="{ props: colProps }">
            <v-btn v-bind="colProps" size="small" variant="flat" :color="column?.color" class="text-uppercase font-weight-bold" append-icon="mdi-chevron-down">
              {{ column?.name ?? '—' }}
            </v-btn>
          </template>
          <v-list density="compact">
            <v-list-item v-for="c in columns" :key="c.id" :active="c.id === card.columnId" @click="c.id !== card.columnId && emit('move', card.id, c.id)">
              <template #prepend><v-icon icon="mdi-circle" size="12" :color="c.color" class="mr-2" /></template>
              <v-list-item-title>{{ c.name }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>
        <span v-if="card" class="text-caption text-medium-emphasis">#{{ card.id }} · created by {{ card.createdBy }} · {{ formatDateTime(card.createdAt) }}</span>
        <v-spacer />
        <v-btn v-if="card" icon="mdi-delete-outline" variant="text" size="small" color="error" title="Delete card" @click="emit('delete', card.id)" />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('close')" />
      </div>

      <div v-if="loading && !card" class="d-flex justify-center align-center" style="height: 60vh;">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <div v-else-if="card" class="task-dialog__body">
        <!-- ── Left: the card ───────────────────────────────────────────── -->
        <div class="task-dialog__main pa-5">
          <v-textarea
            v-model="title"
            variant="plain"
            auto-grow
            rows="1"
            hide-details
            class="task-dialog__title"
            maxlength="500"
            @blur="saveTitle"
            @keydown.enter.prevent="($event.target as HTMLElement).blur()"
          />

          <div class="task-fields mt-4">
            <div class="task-fields__label"><v-icon icon="mdi-flag-outline" size="16" class="mr-2" />Priority</div>
            <div>
              <v-btn-toggle :model-value="card.priority" mandatory density="compact" variant="outlined" divided @update:model-value="(v) => saveField('priority', v)">
                <v-btn v-for="p in TASK_PRIORITIES" :key="p.value" :value="p.value" size="small">
                  <v-icon :icon="p.icon" :color="p.color" size="16" class="mr-1" />{{ p.value }}
                </v-btn>
              </v-btn-toggle>
            </div>

            <div class="task-fields__label"><v-icon icon="mdi-calendar-blank-outline" size="16" class="mr-2" />Due date</div>
            <div>
              <TaskDueMenu :model-value="card.dueDate" :today="today" @update:model-value="(v) => saveField('dueDate', v)">
                <template #activator="{ props: dueProps }">
                  <v-btn v-bind="dueProps" size="small" variant="tonal" :color="dueColor ?? undefined" prepend-icon="mdi-calendar">
                    {{ card.dueDate ? `${dueDateLabel(card.dueDate, today)} · ${card.dueDate}` : 'Set date' }}
                  </v-btn>
                </template>
              </TaskDueMenu>
            </div>

            <div class="task-fields__label"><v-icon icon="mdi-tag-outline" size="16" class="mr-2" />Tags</div>
            <div>
              <TaskTagInput :model-value="cardTagNames" :tags="tags" @update:model-value="saveTags" />
            </div>

            <div class="task-fields__label"><v-icon icon="mdi-link-variant" size="16" class="mr-2" />References</div>
            <div class="d-flex flex-wrap align-center ga-1">
              <v-chip
                v-for="ref in card.refs"
                :key="`${ref.type}:${ref.id}`"
                size="small"
                :color="ref.missing ? 'error' : 'primary'"
                variant="tonal"
                :prepend-icon="TASK_REF_TYPE[ref.type].icon"
                :href="ref.missing ? undefined : router.resolve(TASK_REF_TYPE[ref.type].path(ref.id)).href"
                target="_blank"
              >
                {{ taskRefText(ref) }}
              </v-chip>
              <v-btn size="small" variant="text" color="primary" :prepend-icon="card.refs.length ? 'mdi-pencil' : 'mdi-plus'" @click="refsOpen = true">
                {{ card.refs.length ? 'Edit' : 'Add RFQ / Quote / PI / PO / FI' }}
              </v-btn>
            </div>
          </div>

          <div class="d-flex align-center mt-6 mb-2">
            <span class="text-subtitle-2 font-weight-bold"><v-icon icon="mdi-text" size="18" class="mr-1" />Description</span>
            <v-spacer />
            <template v-if="descriptionDirty">
              <v-btn size="small" variant="text" @click="description = detail?.description ?? ''">Cancel</v-btn>
              <v-btn size="small" color="primary" variant="flat" :loading="savingDescription" @click="saveDescription">Save</v-btn>
            </template>
          </div>
          <v-textarea
            v-model="description"
            variant="outlined"
            auto-grow
            rows="5"
            hide-details
            placeholder="Add a description…"
          />
        </div>

        <!-- ── Right: messages + timeline ───────────────────────────────── -->
        <div class="task-dialog__side">
          <div class="d-flex align-center px-4 pt-3 pb-2">
            <span class="text-subtitle-2 font-weight-bold">Activity</span>
            <v-spacer />
            <v-btn-toggle v-model="filter" mandatory density="compact" variant="text" color="primary">
              <v-btn value="all" size="x-small">All</v-btn>
              <v-btn value="messages" size="x-small">Messages</v-btn>
              <v-btn value="changes" size="x-small">Changes</v-btn>
            </v-btn-toggle>
          </div>

          <div class="px-4 pb-3">
            <v-textarea
              v-model="message"
              variant="outlined"
              density="compact"
              rows="2"
              auto-grow
              max-rows="6"
              hide-details
              placeholder="Write a message… (Enter sends, Shift+Enter new line)"
              :disabled="sending"
              maxlength="4000"
              @keydown.enter.exact.prevent="sendMessage"
            >
              <template #append-inner>
                <v-btn icon="mdi-send" size="small" variant="text" color="primary" :loading="sending" :disabled="!message.trim()" @click="sendMessage" />
              </template>
            </v-textarea>
          </div>
          <v-divider />

          <div class="task-dialog__timeline px-4 py-3">
            <p v-if="!visibleActivities.length" class="text-caption text-medium-emphasis text-center my-6">Nothing yet.</p>
            <div v-for="a in visibleActivities" :key="a.id" class="task-activity" :class="{ 'task-activity--comment': a.kind === 'Comment' }">
              <template v-if="a.kind === 'Comment'">
                <div class="d-flex align-center ga-2 mb-1">
                  <v-avatar size="24" :color="isMine(a) ? 'primary' : 'secondary'" class="text-caption font-weight-bold">{{ userInitials(a.userName) }}</v-avatar>
                  <span class="text-body-2 font-weight-bold">{{ a.userName }}</span>
                  <span class="text-caption text-medium-emphasis">{{ formatDateTime(a.createdAt) }}</span>
                  <v-spacer />
                  <v-btn v-if="isMine(a)" icon="mdi-delete-outline" size="x-small" variant="text" title="Delete message" @click="deleteMessage(a)" />
                </div>
                <div class="task-activity__bubble">{{ a.text }}</div>
              </template>
              <div v-else class="d-flex align-start ga-2 text-caption text-medium-emphasis">
                <v-icon :icon="ACTIVITY_ICONS[a.kind] ?? 'mdi-circle-small'" size="16" class="mt-0" />
                <div>
                  <b>{{ a.userName }}</b> {{ a.text }}
                  <div class="task-activity__time">{{ formatDateTime(a.createdAt) }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </v-card>

    <TaskRefsDialog
      :card="refsOpen && card ? card : null"
      @close="refsOpen = false"
      @saved="applyDetail"
      @notify="(text, color) => emit('notify', text, color)"
    />
  </v-dialog>
</template>

<script setup lang="ts">
import {
  TASK_PRIORITIES,
  TASK_REF_TYPE,
  dueDateColor,
  dueDateLabel,
  formatDateTime,
  taskRefText,
  userInitials,
  type TaskActivity,
  type TaskCardDetail,
  type TaskColumn,
  type TaskTag,
} from '~/utils/taskBoard'

/**
 * The card modal: title, priority, due date, tags, references and description on the left;
 * the message box and the whole timeline (messages + every change) on the right.
 * Every save returns the full card from the server, which the page merges into the board.
 */
const props = defineProps<{
  cardId: number | null
  columns: Pick<TaskColumn, 'id' | 'name' | 'color' | 'isDone'>[]
  tags: TaskTag[]
  today: string
  currentUserName: string
}>()

const emit = defineEmits<{
  close: []
  /** The card changed on the server (also brings the board's tag list). */
  changed: [detail: TaskCardDetail]
  move: [cardId: number, columnId: number]
  delete: [cardId: number]
  notify: [text: string, color: string]
}>()

const ACTIVITY_ICONS: Record<string, string> = {
  Created: 'mdi-plus-circle-outline',
  Moved: 'mdi-swap-horizontal',
  Title: 'mdi-format-title',
  Description: 'mdi-text',
  Priority: 'mdi-flag-outline',
  DueDate: 'mdi-calendar',
  Tag: 'mdi-tag-outline',
  Ref: 'mdi-link-variant',
}

const api = useApi()
const router = useRouter()

const detail = ref<TaskCardDetail | null>(null)
const loading = ref(false)
const title = ref('')
const description = ref('')
const savingDescription = ref(false)
const message = ref('')
const sending = ref(false)
const refsOpen = ref(false)
const filter = ref<'all' | 'messages' | 'changes'>('all')

const card = computed(() => detail.value?.card ?? null)
const column = computed(() => props.columns.find(c => c.id === card.value?.columnId) ?? null)
const tagById = computed(() => new Map(props.tags.map(t => [t.id, t])))
const cardTagNames = computed(() => (card.value?.tagIds ?? []).map(id => tagById.value.get(id)?.name).filter((n): n is string => !!n))
const dueColor = computed(() => dueDateColor(card.value?.dueDate ?? null, props.today, !!column.value?.isDone))
const descriptionDirty = computed(() => !!detail.value && description.value !== (detail.value.description ?? ''))

const visibleActivities = computed(() => {
  const all = detail.value?.activities ?? []
  if (filter.value === 'messages') return all.filter(a => a.kind === 'Comment')
  if (filter.value === 'changes') return all.filter(a => a.kind !== 'Comment')
  return all
})

function isMine(a: TaskActivity) {
  return a.userName.toLowerCase() === props.currentUserName.toLowerCase()
}

function applyDetail(next: TaskCardDetail) {
  const descriptionWasClean = !descriptionDirty.value
  detail.value = next
  title.value = next.card.title
  if (descriptionWasClean) description.value = next.description ?? ''
  emit('changed', next)
}

async function load(id: number) {
  loading.value = true
  detail.value = null
  try {
    const next = await api.get<TaskCardDetail>(`/task-board/cards/${id}`)
    if (props.cardId !== id) return
    detail.value = next
    title.value = next.card.title
    description.value = next.description ?? ''
  } catch (error: any) {
    emit('notify', error?.data?.message || 'Could not load the card.', 'error')
    emit('close')
  } finally {
    loading.value = false
  }
}

async function run(request: () => Promise<TaskCardDetail>, failure: string) {
  try {
    applyDetail(await request())
    return true
  } catch (error: any) {
    emit('notify', error?.data?.message || failure, 'error')
    return false
  }
}

function saveField(field: 'title' | 'description' | 'priority' | 'dueDate', value: unknown) {
  if (!card.value) return Promise.resolve(false)
  const id = card.value.id
  return run(() => api.patch<TaskCardDetail>(`/task-board/cards/${id}`, { field, value: value ?? null }), 'Could not save the change.')
}

function saveTitle() {
  const next = title.value.trim()
  if (!card.value) return
  if (!next) {
    title.value = card.value.title
    return
  }
  if (next !== card.value.title) saveField('title', next)
}

async function saveDescription() {
  savingDescription.value = true
  const value = description.value
  if (await saveField('description', value)) description.value = detail.value?.description ?? ''
  savingDescription.value = false
}

function saveTags(names: string[]) {
  if (!card.value) return
  const id = card.value.id
  run(() => api.put<TaskCardDetail>(`/task-board/cards/${id}/tags`, { names }), 'Could not save the tags.')
}

async function sendMessage() {
  const text = message.value.trim()
  if (!text || !card.value || sending.value) return
  sending.value = true
  const id = card.value.id
  if (await run(() => api.post<TaskCardDetail>(`/task-board/cards/${id}/comments`, { text }), 'Could not send the message.')) message.value = ''
  sending.value = false
}

function deleteMessage(a: TaskActivity) {
  if (!card.value || !confirm('Delete this message?')) return
  const id = card.value.id
  run(() => api.delete<TaskCardDetail>(`/task-board/cards/${id}/comments/${a.id}`), 'Could not delete the message.')
}

/** The page moved the card (header menu, drag on the board) — re-read the timeline. */
function reload() {
  if (props.cardId !== null) load(props.cardId)
}

defineExpose({ reload })

watch(() => props.cardId, (id) => {
  refsOpen.value = false
  message.value = ''
  filter.value = 'all'
  if (id !== null) load(id)
  else detail.value = null
}, { immediate: true })
</script>

<style scoped>
.task-dialog__body {
  display: flex;
  min-height: 0;
  height: 78vh;
}

.task-dialog__main {
  flex: 1;
  min-width: 0;
  overflow-y: auto;
}

.task-dialog__side {
  width: 420px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-on-surface), 0.02);
}

.task-dialog__timeline {
  flex: 1;
  overflow-y: auto;
}

.task-dialog__title :deep(textarea) {
  font-size: 1.5rem;
  font-weight: 700;
  line-height: 1.3;
}

.task-fields {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 12px 16px;
  align-items: center;
}

.task-fields__label {
  display: flex;
  align-items: center;
  font-size: 0.85rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
}

.task-activity {
  margin-bottom: 14px;
}

.task-activity__bubble {
  white-space: pre-wrap;
  word-break: break-word;
  font-size: 0.875rem;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), 0.14);
  border-radius: 8px;
  padding: 8px 10px;
  margin-left: 32px;
}

.task-activity__time {
  font-size: 0.7rem;
  opacity: 0.75;
}

@media (max-width: 960px) {
  .task-dialog__body {
    flex-direction: column;
    height: auto;
  }

  .task-dialog__side {
    width: 100%;
    border-left: none;
    border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  }

  .task-fields {
    grid-template-columns: 1fr;
  }
}
</style>
