<template>
  <div
    class="task-card"
    :class="{ 'task-card--done': done, 'task-card--aog': card.priority === 'AOG' && !done }"
    :draggable="armed"
    @pointerdown.capture="onPointerDown"
    @click="emit('open')"
  >
    <div class="d-flex align-start ga-1">
      <div class="task-card__title flex-grow-1">{{ card.title }}</div>
      <v-menu location="bottom end">
        <template #activator="{ props: menuProps }">
          <v-btn v-bind="menuProps" icon="mdi-dots-horizontal" size="x-small" variant="text" density="comfortable" class="task-card__more" @click.stop />
        </template>
        <v-list density="compact">
          <v-list-item prepend-icon="mdi-open-in-app" title="Open" @click="emit('open')" />
          <v-list-item prepend-icon="mdi-link-variant" title="References" @click="emit('refs')" />
          <v-list-item prepend-icon="mdi-delete-outline" title="Delete" base-color="error" @click="emit('delete')" />
        </v-list>
      </v-menu>
    </div>

    <!-- Tags -->
    <div v-if="cardTags.length" class="d-flex flex-wrap ga-1 mt-2">
      <span v-for="tag in cardTags" :key="tag.id" class="task-tag" :style="{ background: tag.color }">{{ tag.name }}</span>
    </div>

    <!-- References -->
    <div v-if="card.refs.length" class="d-flex flex-wrap ga-1 mt-2">
      <a
        v-for="ref in card.refs"
        :key="`${ref.type}:${ref.id}`"
        class="task-ref"
        :class="{ 'task-ref--missing': ref.missing }"
        :href="router.resolve(TASK_REF_TYPE[ref.type].path(ref.id)).href"
        target="_blank"
        draggable="false"
        @click.stop
      >
        <v-icon :icon="TASK_REF_TYPE[ref.type].icon" size="12" class="mr-1" />{{ taskRefText(ref) }}
      </a>
    </div>

    <!-- Latest message -->
    <div v-if="card.lastComment" class="task-card__last mt-2" :title="card.lastComment.text">
      <v-icon icon="mdi-message-text-outline" size="13" class="mr-1" />
      <b>{{ card.lastComment.userName }}:</b> {{ card.lastComment.text }}
    </div>

    <!-- Quick fields, like ClickUp: due date, priority, tags, references, messages -->
    <div class="d-flex align-center flex-wrap ga-1 mt-2" @click.stop>
      <TaskDueMenu :model-value="card.dueDate" :today="today" @update:model-value="(v) => emit('field', 'dueDate', v)">
        <template #activator="{ props: dueProps }">
          <button v-bind="dueProps" type="button" class="task-chip" :class="dueClass" :title="card.dueDate ? `Due ${card.dueDate}` : 'Set due date'">
            <v-icon icon="mdi-calendar-blank-outline" size="15" />
            <span v-if="card.dueDate">{{ dueDateLabel(card.dueDate, today) }}</span>
          </button>
        </template>
      </TaskDueMenu>

      <v-menu location="bottom start">
        <template #activator="{ props: prioProps }">
          <button v-bind="prioProps" type="button" class="task-chip" title="Priority">
            <v-icon :icon="priority.icon" size="15" :color="priority.color" />
            <span>{{ card.priority }}</span>
          </button>
        </template>
        <v-list density="compact">
          <v-list-item
            v-for="p in TASK_PRIORITIES"
            :key="p.value"
            :active="p.value === card.priority"
            @click="p.value !== card.priority && emit('field', 'priority', p.value)"
          >
            <template #prepend><v-icon :icon="p.icon" :color="p.color" /></template>
            <v-list-item-title>{{ p.value }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>

      <v-menu v-model="tagsOpen" :close-on-content-click="false" location="bottom start">
        <template #activator="{ props: tagProps }">
          <button v-bind="tagProps" type="button" class="task-chip" title="Tags">
            <v-icon icon="mdi-tag-outline" size="15" />
          </button>
        </template>
        <v-card min-width="300" class="pa-2">
          <TaskTagInput v-if="tagsOpen" v-model="draftTags" :tags="tags" autofocus />
        </v-card>
      </v-menu>

      <button type="button" class="task-chip" title="References" @click="emit('refs')">
        <v-icon icon="mdi-link-variant" size="15" />
        <span v-if="card.refs.length">{{ card.refs.length }}</span>
      </button>

      <v-spacer />
      <v-icon v-if="card.hasDescription" icon="mdi-text" size="14" class="text-medium-emphasis" title="Has a description" />
      <span v-if="card.commentCount" class="task-chip task-chip--static" title="Messages">
        <v-icon icon="mdi-comment-outline" size="14" />{{ card.commentCount }}
      </span>
    </div>

    <!-- Write an update: Enter sends, the card then shows it as the latest message -->
    <input
      v-model="message"
      class="task-card__input mt-2"
      placeholder="Write an update + Enter"
      maxlength="4000"
      :disabled="sending"
      @click.stop
      @keydown.enter.prevent="send"
    >
  </div>
</template>

<script setup lang="ts">
import {
  TASK_PRIORITIES,
  TASK_PRIORITY,
  TASK_REF_TYPE,
  dueDateColor,
  dueDateLabel,
  taskRefText,
  type TaskCard,
  type TaskTag,
} from '~/utils/taskBoard'

/** One card of the board. The page owns the data and does every save; this only emits. */
const props = defineProps<{
  card: TaskCard
  /** Board tags by id. */
  tagMap: Map<number, TaskTag>
  tags: TaskTag[]
  today: string
  /** The card sits in a Done column. */
  done: boolean
  /** A message save is in flight. */
  sending: boolean
}>()

const emit = defineEmits<{
  open: []
  refs: []
  delete: []
  field: [field: 'title' | 'priority' | 'dueDate', value: string | null]
  tags: [names: string[]]
  comment: [text: string]
}>()

const router = useRouter()
const message = ref('')
const tagsOpen = ref(false)
const draftTags = ref<string[]>([])
/** Off while the pointer is down on the message input, so text can be selected there. */
const armed = ref(true)

const priority = computed(() => TASK_PRIORITY[props.card.priority] ?? TASK_PRIORITY.Normal)
const cardTags = computed(() => props.card.tagIds.map(id => props.tagMap.get(id)).filter((t): t is TaskTag => !!t))

const dueClass = computed(() => {
  const color = dueDateColor(props.card.dueDate, props.today, props.done)
  return color ? `text-${color}` : undefined
})

function onPointerDown(event: PointerEvent) {
  armed.value = !(event.target as HTMLElement).closest('input, textarea, .v-field')
}

function send() {
  const text = message.value.trim()
  if (!text || props.sending) return
  emit('comment', text)
  message.value = ''
}

// The tag menu saves once, when it closes.
watch(tagsOpen, (open) => {
  const current = cardTags.value.map(t => t.name)
  if (open) {
    draftTags.value = current
    return
  }
  const changed = draftTags.value.length !== current.length
    || draftTags.value.some(n => !current.some(c => c.toLowerCase() === n.toLowerCase()))
  if (changed) emit('tags', draftTags.value)
})
</script>

<style scoped>
.task-card {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), 0.14);
  border-radius: 10px;
  padding: 10px 12px;
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.task-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.6);
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.18);
}

.task-card--aog {
  border-left: 3px solid #EF5350;
}

.task-card--done .task-card__title {
  text-decoration: line-through;
  opacity: 0.7;
}

.task-card__title {
  font-size: 0.92rem;
  line-height: 1.3;
  word-break: break-word;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.task-card__more {
  opacity: 0;
  margin: -4px -6px 0 0;
}

.task-card:hover .task-card__more {
  opacity: 1;
}

.task-card__last {
  font-size: 0.78rem;
  color: rgba(var(--v-theme-on-surface), 0.75);
  background: rgba(var(--v-theme-on-surface), 0.05);
  border-radius: 6px;
  padding: 4px 6px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  word-break: break-word;
}

.task-tag {
  font-size: 0.7rem;
  color: #fff;
  border-radius: 4px;
  padding: 1px 6px;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-ref {
  font-size: 0.72rem;
  color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.12);
  border-radius: 4px;
  padding: 1px 6px;
  text-decoration: none;
  white-space: nowrap;
}

.task-ref:hover {
  text-decoration: underline;
}

.task-ref--missing {
  color: rgb(var(--v-theme-error));
  background: rgba(var(--v-theme-error), 0.12);
}

.task-chip {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 0.75rem;
  border: 1px solid rgba(var(--v-border-color), 0.2);
  border-radius: 6px;
  padding: 1px 6px;
  min-height: 22px;
  color: rgba(var(--v-theme-on-surface), 0.8);
  background: transparent;
}

button.task-chip:hover {
  background: rgba(var(--v-theme-on-surface), 0.08);
}

.task-chip--static {
  border: none;
}

.task-card__input {
  width: 100%;
  font-size: 0.78rem;
  border: 1px solid rgba(var(--v-border-color), 0.16);
  border-radius: 6px;
  padding: 4px 8px;
  color: rgb(var(--v-theme-on-surface));
  background: rgba(var(--v-theme-on-surface), 0.03);
  outline: none;
  cursor: text;
}

.task-card__input:focus {
  border-color: rgb(var(--v-theme-primary));
}
</style>
