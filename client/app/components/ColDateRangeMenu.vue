<template>
  <div class="cf-th-inner" :class="{ 'cf-th-inner--filtered': isActive }">
    <span
      class="d-flex align-center gap-1"
      :class="{ 'cursor-pointer': sortable !== false }"
      @click="sortable !== false && $emit('sortClick')"
    >
      {{ label }}
      <template v-if="sortable !== false">
        <v-icon v-if="isSorted" :icon="sortDesc ? 'mdi-arrow-down' : 'mdi-arrow-up'" size="13" color="primary" />
        <v-icon v-else icon="mdi-unfold-more-horizontal" size="13" class="cf-sort-hint" />
      </template>
    </span>
    <v-menu v-model="open" :close-on-content-click="false" location="bottom end">
      <template #activator="{ props: mp }">
        <v-btn
          v-bind="mp"
          :icon="isActive ? 'mdi-calendar-filter' : 'mdi-calendar-filter-outline'"
          size="x-small"
          variant="text"
          :color="isActive ? 'primary' : undefined"
          :title="isActive ? rangeLabel(from, to) : `Filter ${label} by date range`"
          class="cf-filter-btn"
          @click.stop
        />
      </template>
      <v-card class="pa-2 cf-date-range" min-width="300">
        <div class="d-flex flex-wrap gap-1 mb-2">
          <v-chip
            v-for="p in presets"
            :key="p.label"
            size="x-small"
            variant="outlined"
            class="cursor-pointer"
            @click="applyRange(...p.range())"
          >
            {{ p.label }}
          </v-chip>
        </div>
        <v-date-picker
          v-model="draft"
          multiple="range"
          bg-color="transparent"
          hide-header
          show-adjacent-months
          density="compact"
          width="100%"
        />
        <div class="text-caption text-medium-emphasis px-1 mb-1">
          {{ draftFrom ? rangeLabel(draftFrom, draftTo) : 'Pick a start date, then an end date' }}
        </div>
        <v-divider class="my-1" />
        <div class="d-flex gap-1">
          <v-btn size="x-small" variant="text" color="error" :disabled="!isActive && !draft.length" @click="applyRange('', '')">Clear</v-btn>
          <v-spacer />
          <v-btn size="x-small" variant="text" @click="open = false">Cancel</v-btn>
          <v-btn size="x-small" variant="tonal" color="primary" :disabled="!draftFrom" @click="applyRange(draftFrom, draftTo)">Apply</v-btn>
        </div>
      </v-card>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
/**
 * Excel-style header filter for a date column: one range picker instead of separate
 * From / To fields. Values are `YYYY-MM-DD` strings (both ends inclusive) so the page can
 * pass them straight to the backend's `…From` / `…To` query params.
 */
// `sortable` defaults to true — an absent boolean prop would otherwise be cast to false.
const props = withDefaults(defineProps<{
  label: string
  from: string
  to: string
  sortable?: boolean
  isSorted?: boolean
  sortDesc?: boolean
}>(), { sortable: true })

const emit = defineEmits<{
  /** Both empty strings = filter cleared. A single picked day comes back as from === to. */
  update: [from: string, to: string]
  sortClick: []
}>()

const open = ref(false)
const draft = ref<Date[]>([])

const isActive = computed(() => !!(props.from || props.to))

function toIso(d: Date): string {
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

function fromIso(s: string): Date {
  const [y, m, d] = s.split('-').map(Number)
  return new Date(y!, m! - 1, d!)
}

function rangeLabel(from: string, to: string): string {
  const fmt = (s: string) => fromIso(s).toLocaleDateString()
  if (from && to) return from === to ? fmt(from) : `${fmt(from)} – ${fmt(to)}`
  return from ? `From ${fmt(from)}` : `Until ${fmt(to)}`
}

// The range picker emits every day in the range; only the two ends matter.
const draftSorted = computed(() => [...draft.value].sort((a, b) => a.getTime() - b.getTime()))
const draftFrom = computed(() => (draftSorted.value.length ? toIso(draftSorted.value[0]!) : ''))
const draftTo = computed(() => (draftSorted.value.length ? toIso(draftSorted.value.at(-1)!) : ''))

// Re-seed the picker from the applied filter every time the menu opens, so Cancel discards edits.
watch(open, (isOpen) => {
  if (!isOpen) return
  const f = props.from || props.to
  const t = props.to || props.from
  draft.value = f ? (f === t ? [fromIso(f)] : [fromIso(f), fromIso(t)]) : []
})

function applyRange(from: string, to: string) {
  emit('update', from, to)
  open.value = false
}

function daysAgo(n: number): Date {
  const d = new Date()
  d.setHours(0, 0, 0, 0)
  d.setDate(d.getDate() - n)
  return d
}

const presets: { label: string; range: () => [string, string] }[] = [
  { label: 'Today', range: () => [toIso(daysAgo(0)), toIso(daysAgo(0))] },
  { label: 'Yesterday', range: () => [toIso(daysAgo(1)), toIso(daysAgo(1))] },
  { label: 'Last 7 days', range: () => [toIso(daysAgo(6)), toIso(daysAgo(0))] },
  { label: 'Last 30 days', range: () => [toIso(daysAgo(29)), toIso(daysAgo(0))] },
  {
    label: 'This month',
    range: () => { const n = new Date(); return [toIso(new Date(n.getFullYear(), n.getMonth(), 1)), toIso(new Date(n.getFullYear(), n.getMonth() + 1, 0))] },
  },
  {
    label: 'Last month',
    range: () => { const n = new Date(); return [toIso(new Date(n.getFullYear(), n.getMonth() - 1, 1)), toIso(new Date(n.getFullYear(), n.getMonth(), 0))] },
  },
  {
    label: 'This year',
    range: () => { const y = new Date().getFullYear(); return [`${y}-01-01`, `${y}-12-31`] },
  },
]
</script>

<style scoped>
.cf-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.cf-th-inner--filtered { color: rgb(var(--v-theme-primary)); font-weight: 700; }
.cf-filter-btn { opacity: 0.5; flex-shrink: 0; }
.cf-filter-btn:hover, .cf-filter-btn.v-btn--active { opacity: 1; }
.cursor-pointer { cursor: pointer; }
.cf-sort-hint { opacity: 0.25; }
.cf-th-inner:hover .cf-sort-hint { opacity: 0.6; }

/* The app theme leaves selected days nearly invisible; make the picked range obvious. */
.cf-date-range :deep(.v-date-picker-month__day--selected .v-btn) {
  background-color: rgb(var(--v-theme-primary)) !important;
  color: rgb(var(--v-theme-on-primary)) !important;
}

/* Same full-header highlight ColFilterMenu uses for an active column filter. */
:global(th:has(.cf-th-inner--filtered)) {
  background: linear-gradient(rgba(var(--v-theme-primary), 0.14), rgba(var(--v-theme-primary), 0.14)), rgb(var(--v-theme-surface)) !important;
  box-shadow: inset 0 -2px 0 rgb(var(--v-theme-primary));
}
</style>
