<template>
  <div class="cf-th-inner">
    <span class="cursor-pointer d-flex align-center gap-1" @click="$emit('sortClick')">
      {{ label }}
      <v-icon v-if="isSorted" :icon="sortDesc ? 'mdi-arrow-down' : 'mdi-arrow-up'" size="13" color="primary" />
      <v-icon v-else icon="mdi-unfold-more-horizontal" size="13" class="cf-sort-hint" />
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
          <v-checkbox
            v-for="val in filteredOpts"
            :key="val"
            :label="val"
            :model-value="selected.has(val)"
            density="compact"
            hide-details
            :class="{ 'opacity-40': isUnavailable(val) }"
            @update:model-value="$emit('toggle', val)"
          />
          <div v-if="filteredOpts.length === 0" class="text-caption text-medium-emphasis pa-2">No options</div>
        </div>
        <v-divider class="my-1" />
        <div class="d-flex gap-1">
          <v-btn size="x-small" variant="text" color="primary" @click="$emit('selectAll')">All</v-btn>
          <v-btn size="x-small" variant="text" color="error" :disabled="!isActive" @click="$emit('clearAll')">None</v-btn>
        </div>
        <v-divider class="mt-1 mb-1" />
        <v-list-item
          :title="showAll ? 'Show available only' : 'Show all'"
          :prepend-icon="showAll ? 'mdi-filter' : 'mdi-filter-off'"
          density="compact"
          class="text-caption text-medium-emphasis"
          @click.stop="showAll = !showAll"
        />
      </v-card>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  colKey: string
  label: string
  /** "Available" options — values from the current page / current filtered view */
  options: string[]
  /** Full options — all values from DB or full dataset; falls back to options when not provided */
  allOptions?: string[]
  selected: Set<string>
  search: string
  sortable?: boolean
  isSorted?: boolean
  sortDesc?: boolean
}>()

defineEmits<{
  toggle: [val: string]
  selectAll: []
  clearAll: []
  'update:search': [val: string]
  sortClick: []
}>()

const showAll = ref(false)

/** Full list to show when showAll=true */
const effectiveAll = computed(() => props.allOptions ?? props.options)

const isActive = computed(() => props.selected.size > 0)

const filteredOpts = computed(() => {
  const source = showAll.value ? effectiveAll.value : props.options
  const s = (props.search || '').toLowerCase()
  return s ? source.filter(v => v.toLowerCase().includes(s)) : source
})

/** Returns true when the item exists in allOptions but not in options (greyed out when showAll=true) */
function isUnavailable(val: string): boolean {
  return showAll.value && !props.options.includes(val)
}
</script>

<style scoped>
.cf-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.cf-filter-btn { opacity: 0.5; flex-shrink: 0; }
.cf-filter-btn:hover, .cf-filter-btn.v-btn--active { opacity: 1; }
.cursor-pointer { cursor: pointer; }
.cf-sort-hint { opacity: 0.25; }
.cf-th-inner:hover .cf-sort-hint { opacity: 0.6; }
</style>
