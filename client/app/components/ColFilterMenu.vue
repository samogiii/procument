<template>
  <div class="cf-th-inner">
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
    <v-menu :close-on-content-click="false" max-width="280">
      <template #activator="{ props: mp }">
        <v-btn
          v-bind="mp"
          :icon="isActive ? 'mdi-filter' : 'mdi-filter-outline'"
          size="x-small"
          variant="text"
          :color="isActive ? 'primary' : undefined"
          class="cf-filter-btn"
          @click.stop
        />
      </template>
      <v-card class="pa-2" min-width="240">
        <v-text-field
          :model-value="search"
          @update:model-value="$emit('update:search', $event)"
          placeholder="Search…"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          class="mb-2"
        />
        <div style="max-height:220px; overflow-y:auto;">
          <v-progress-linear v-if="loading" indeterminate color="primary" height="2" class="mb-1" />
          <v-checkbox
            v-for="opt in filteredOpts"
            :key="opt.value"
            :model-value="selected.has(opt.value)"
            density="compact"
            hide-details
            :class="{ 'cf-unavailable': opt.unavailable }"
            @update:model-value="$emit('toggle', opt.value)"
          >
            <template #label>
              <span class="text-body-2">{{ opt.title }}</span>
              <v-icon
                v-if="opt.unavailable"
                icon="mdi-eye-off-outline"
                size="13"
                class="ml-1 text-disabled"
                title="No rows with this value under the current filters"
              />
            </template>
          </v-checkbox>
          <div v-if="filteredOpts.length === 0" class="text-caption text-medium-emphasis pa-2">No options</div>
        </div>
        <v-divider class="my-1" />
        <div class="d-flex gap-1">
          <v-btn size="x-small" variant="text" color="primary" @click="$emit('selectAll', filteredOpts.map(o => o.value))">All</v-btn>
          <v-btn size="x-small" variant="text" color="error" :disabled="!isActive" @click="$emit('clearAll')">None</v-btn>
        </div>
        <template v-if="hiddenCount > 0">
          <v-divider class="mt-1 mb-1" />
          <v-list-item
            :title="showAll ? 'Show available only' : `Show all (${hiddenCount} hidden)`"
            :prepend-icon="showAll ? 'mdi-filter' : 'mdi-filter-off'"
            density="compact"
            class="text-caption text-medium-emphasis"
            @click.stop="showAll = !showAll"
          />
        </template>
      </v-card>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
type RawOption = string | number | { title?: string; value: string | number }
interface NormalizedOption { title: string; value: string; unavailable: boolean }

const props = defineProps<{
  colKey: string
  label: string
  /** "Available" options — values that still exist under the *other* active filters. */
  options: readonly RawOption[]
  /** Full option list — every value in the dataset; falls back to `options` when omitted. */
  allOptions?: readonly RawOption[]
  selected: Set<string>
  search: string
  /** Shows a progress bar while the parent refreshes the available list. */
  loading?: boolean
  sortable?: boolean
  isSorted?: boolean
  sortDesc?: boolean
}>()

defineEmits<{
  toggle: [val: string]
  /** Payload is the list currently visible in the menu (after search + show-all). */
  selectAll: [vals: string[]]
  clearAll: []
  'update:search': [val: string]
  sortClick: []
}>()

const showAll = ref(false)

function normalize(list: readonly RawOption[] | undefined): { title: string; value: string }[] {
  if (!list) return []
  return list.map(o =>
    o !== null && typeof o === 'object'
      ? { title: String(o.title ?? o.value ?? ''), value: String(o.value ?? '') }
      : { title: String(o), value: String(o) }
  )
}

/** Values that exist under the other active filters. */
const available = computed(() => normalize(props.options))
const availableSet = computed(() => new Set(available.value.map(o => o.value)))

/** Every value in the dataset — only differs from `available` when allOptions is given. */
const full = computed(() => (props.allOptions ? normalize(props.allOptions) : available.value))

/** How many values the cascade is hiding right now. */
const hiddenCount = computed(() => full.value.filter(o => !availableSet.value.has(o.value)).length)

const isActive = computed(() => props.selected.size > 0)

/**
 * Visible list. In "available only" mode we still render values the user has
 * already selected — otherwise a selection could become impossible to undo.
 */
const visibleOpts = computed<NormalizedOption[]>(() => {
  const source = showAll.value ? full.value : available.value
  const out: NormalizedOption[] = source.map(o => ({ ...o, unavailable: !availableSet.value.has(o.value) }))
  if (!showAll.value && props.selected.size) {
    const seen = new Set(out.map(o => o.value))
    for (const o of full.value) {
      if (props.selected.has(o.value) && !seen.has(o.value)) out.push({ ...o, unavailable: true })
    }
    for (const v of props.selected) {
      if (!seen.has(v) && !out.some(o => o.value === v)) out.push({ title: v, value: v, unavailable: true })
    }
  }
  return out
})

const filteredOpts = computed(() => {
  const s = (props.search || '').toLowerCase()
  return s ? visibleOpts.value.filter(o => o.title.toLowerCase().includes(s) || o.value.toLowerCase().includes(s)) : visibleOpts.value
})
</script>

<style scoped>
.cf-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.cf-filter-btn { opacity: 0.5; flex-shrink: 0; }
.cf-filter-btn:hover, .cf-filter-btn.v-btn--active { opacity: 1; }
.cursor-pointer { cursor: pointer; }
.cf-sort-hint { opacity: 0.25; }
.cf-th-inner:hover .cf-sort-hint { opacity: 0.6; }
.cf-unavailable { opacity: 0.45; }
</style>
