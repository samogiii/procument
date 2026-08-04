<template>
  <div class="cf-th-inner">
    <span class="cursor-pointer d-flex align-center gap-1" @click="$emit('sortClick')">
      {{ label }}
      <v-icon v-if="isSorted" :icon="sortDesc ? 'mdi-arrow-down' : 'mdi-arrow-up'" size="13" color="primary" />
      <v-icon v-else icon="mdi-unfold-more-horizontal" size="13" class="cf-sort-hint" />
    </span>
    <v-menu :close-on-content-click="false" max-width="260">
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
      <v-card class="pa-3" min-width="230">
        <div class="text-caption text-medium-emphasis mb-2">Price range</div>
        <v-text-field
          :model-value="min"
          @update:model-value="$emit('update:min', toNum($event))"
          label="From"
          type="number"
          :prefix="prefix"
          density="compact"
          variant="outlined"
          hide-details
          class="mb-2"
        />
        <v-text-field
          :model-value="max"
          @update:model-value="$emit('update:max', toNum($event))"
          label="To"
          type="number"
          :prefix="prefix"
          density="compact"
          variant="outlined"
          hide-details
        />
        <div v-if="bounds" class="text-caption text-medium-emphasis mt-2">
          Data range: {{ prefix }}{{ formatPrice(bounds.lo) }} – {{ prefix }}{{ formatPrice(bounds.hi) }}
        </div>
        <v-divider class="my-2" />
        <div class="d-flex gap-1">
          <v-btn size="x-small" variant="text" color="primary" :disabled="!bounds" @click="$emit('selectAll')">All</v-btn>
          <v-btn size="x-small" variant="text" color="error" :disabled="!isActive" @click="$emit('clearAll')">Clear</v-btn>
        </div>
      </v-card>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
import { formatPrice } from '~/utils/formatPrice'

const props = defineProps<{
  colKey: string
  label: string
  min: number | null
  max: number | null
  /** Lowest / highest value present in the current data — shown as a hint and used by "All". */
  bounds?: { lo: number; hi: number } | null
  /** Currency symbol shown inside the inputs. */
  prefix?: string
  isSorted?: boolean
  sortDesc?: boolean
}>()

defineEmits<{
  'update:min': [val: number | null]
  'update:max': [val: number | null]
  selectAll: []
  clearAll: []
  sortClick: []
}>()

const isActive = computed(() => props.min != null || props.max != null)

/** Empty input clears the bound rather than filtering on 0. */
function toNum(v: string | number | null): number | null {
  if (v === '' || v == null) return null
  const n = Number(v)
  return Number.isFinite(n) ? n : null
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
