<template>
  <v-menu v-model="open" :close-on-content-click="false" location="bottom start">
    <template #activator="{ props: activator }">
      <slot name="activator" :props="activator" />
    </template>
    <v-card min-width="320" class="task-due-menu">
      <div class="d-flex flex-wrap ga-1 pa-2">
        <v-btn size="small" variant="tonal" @click="pick(today)">Today</v-btn>
        <v-btn size="small" variant="tonal" @click="pick(addDays(today, 1))">Tomorrow</v-btn>
        <v-btn size="small" variant="tonal" @click="pick(addDays(today, 7))">Next week</v-btn>
        <v-spacer />
        <v-btn v-if="modelValue" size="small" variant="text" color="error" prepend-icon="mdi-calendar-remove" @click="pick(null)">Clear</v-btn>
      </div>
      <!-- Opens on the card's date, or on the server's today when it has none -->
      <v-date-picker
        v-if="open"
        :model-value="modelValue ? fromDateKey(modelValue) : null"
        v-model:month="month"
        v-model:year="year"
        hide-header
        show-adjacent-months
        density="compact"
        width="320"
        @update:model-value="onPicked"
      />
      <div class="text-caption text-medium-emphasis px-3 pb-2">Server date: {{ today }}</div>
    </v-card>
  </v-menu>
</template>

<script setup lang="ts">
import { addDays, fromDateKey, toDateKey } from '~/utils/taskBoard'

/** Due-date picker of a task card: quick picks plus a month picker. Emits yyyy-MM-dd or null. */
const props = defineProps<{
  modelValue: string | null
  /** Server's today (yyyy-MM-dd). */
  today: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string | null] }>()

const open = ref(false)
const month = ref(0)
const year = ref(2026)

watch(open, (isOpen) => {
  if (!isOpen) return
  const start = fromDateKey(props.modelValue ?? props.today)
  month.value = start.getMonth()
  year.value = start.getFullYear()
})

function pick(value: string | null) {
  open.value = false
  if (value !== props.modelValue) emit('update:modelValue', value)
}

function onPicked(value: unknown) {
  const date = Array.isArray(value) ? value[0] : value
  if (date instanceof Date) pick(toDateKey(date))
  else if (typeof date === 'string' && date) pick(date.slice(0, 10))
}
</script>
