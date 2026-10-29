<template>
  <v-card class="task-calendar" variant="flat">
    <div class="d-flex align-center flex-wrap ga-2 pa-3">
      <v-btn icon="mdi-chevron-left" variant="text" size="small" @click="calendar?.prev()" />
      <v-btn variant="tonal" size="small" @click="focus = today">Today</v-btn>
      <v-btn icon="mdi-chevron-right" variant="text" size="small" @click="calendar?.next()" />
      <span class="text-h6 ml-2">{{ title }}</span>
      <v-spacer />
      <span class="text-caption text-medium-emphasis mr-2">
        Drag a card to another day to move its due date{{ undated ? ` · ${undated} without a date` : '' }}
      </span>
    </div>
    <v-divider />

    <v-calendar
      ref="calendar"
      v-model="focus"
      type="month"
      :events="events"
      :event-color="(e: any) => e.color"
      event-more
      :weekdays="[1, 2, 3, 4, 5, 6, 0]"
      :now="today"
      class="task-calendar__grid"
      @click:event="(_e: Event, scope: any) => emit('open', scope.event.cardId)"
      @click:date="(_e: Event, day: any) => emit('create', day.date)"
      @dragover:day="onDragOver"
      @dragleave:day="onDragLeave"
      @drop:day="onDrop"
    >
      <template #event="{ event }">
        <div
          class="task-calendar__event"
          :class="{ 'task-calendar__event--done': event.done, 'task-calendar__event--late': event.late }"
          draggable="true"
          :title="event.name"
          @dragstart="onDragStart($event, event.cardId)"
          @dragend="dragCardId = null; dropDate = null"
        >
          <v-icon icon="mdi-flag" size="11" class="mr-1" />{{ event.name }}
        </div>
      </template>
      <template #day="{ date }">
        <div v-if="dropDate === date" class="task-calendar__drop" />
      </template>
    </v-calendar>
  </v-card>
</template>

<script setup lang="ts">
import { TASK_PRIORITY, fromDateKey, type TaskCard } from '~/utils/taskBoard'

/** Calendar view of a board: every card with a due date sits on that day (server dates, yyyy-MM-dd). */
const props = defineProps<{
  cards: TaskCard[]
  /** Column ids of Done columns. */
  doneColumnIds: Set<number>
  today: string
}>()

const emit = defineEmits<{
  open: [cardId: number]
  /** Clicked a day number: new card due that day. */
  create: [date: string]
  moveDate: [cardId: number, date: string]
}>()

const calendar = ref<any>(null)
const focus = ref(props.today)
const dragCardId = ref<number | null>(null)
const dropDate = ref<string | null>(null)

const events = computed(() => props.cards
  .filter(c => !!c.dueDate)
  .map((c) => {
    const done = props.doneColumnIds.has(c.columnId)
    return {
      name: c.title,
      start: c.dueDate!,
      timed: false,
      cardId: c.id,
      done,
      late: !done && c.dueDate! < props.today,
      color: done ? 'grey' : TASK_PRIORITY[c.priority]?.color ?? 'primary',
    }
  }))

const undated = computed(() => props.cards.filter(c => !c.dueDate).length)

const title = computed(() => fromDateKey(String(focus.value)).toLocaleDateString(undefined, { month: 'long', year: 'numeric' }))

function onDragStart(event: DragEvent, cardId: number) {
  dragCardId.value = cardId
  event.dataTransfer?.setData('text/plain', String(cardId))
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move'
}

function onDragOver(event: Event, day: { date: string }) {
  if (dragCardId.value === null) return
  event.preventDefault()
  dropDate.value = day.date
}

function onDragLeave(_event: Event, day: { date: string }) {
  if (dropDate.value === day.date) dropDate.value = null
}

function onDrop(event: Event, day: { date: string }) {
  event.preventDefault()
  const cardId = dragCardId.value
  dragCardId.value = null
  dropDate.value = null
  const card = props.cards.find(c => c.id === cardId)
  if (card && card.dueDate !== day.date) emit('moveDate', card.id, day.date)
}

watch(() => props.today, (today) => {
  focus.value = today
})
</script>

<style scoped>
.task-calendar__grid {
  min-height: 72vh;
}

.task-calendar__grid :deep(.v-calendar-weekly__day) {
  min-height: 110px;
}


.task-calendar__event {
  display: block;
  font-size: 0.75rem;
  padding: 0 4px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  cursor: grab;
  color: #fff;
}

.task-calendar__event--done {
  text-decoration: line-through;
  opacity: 0.75;
}

.task-calendar__event--late {
  box-shadow: inset 3px 0 0 #b71c1c;
}

.task-calendar__drop {
  position: absolute;
  inset: 0;
  border: 2px dashed rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.08);
  pointer-events: none;
}
</style>
