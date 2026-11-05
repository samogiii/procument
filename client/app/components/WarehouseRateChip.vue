<template>
  <v-tooltip v-if="rate" location="bottom" max-width="340">
    <template #activator="{ props: tip }">
      <v-chip v-bind="tip" size="small" variant="tonal" :color="rate.stale ? 'warning' : 'teal'" prepend-icon="mdi-bank-outline">
        BOC $1 = ¥{{ rate.cnyPerUsd }}
        <span class="ml-1 text-medium-emphasis">(+{{ rate.markup }} = {{ round(rate.withMarkup) }})</span>
      </v-chip>
    </template>
    <div>Bank of China 中行折算价, read by the warehouse.</div>
    <div v-if="rate.publishedAt">Published {{ rate.publishedAt }}</div>
    <div v-if="rate.fetchedAt">Read {{ new Date(rate.fetchedAt).toLocaleString() }}</div>
    <div v-if="rate.syncedAt">Brought here by the sync of {{ new Date(rate.syncedAt).toLocaleString() }}</div>
    <div v-if="rate.stale">BOC could not be reached then: this is the last good rate.</div>
    <div class="mt-1">The warehouse saves rate + {{ rate.markup }} with each POP.</div>
  </v-tooltip>
</template>

<script setup lang="ts">
type WarehouseRate = {
  cnyPerUsd: number
  withMarkup: number
  markup: number
  publishedAt: string | null
  fetchedAt: string | null
  stale: boolean
  syncedAt: string | null
}

const api = useApi()
const rate = ref<WarehouseRate | null>(null)
const round = (v: number) => Math.round(v * 10000) / 10000

async function load() {
  try { rate.value = await api.get<WarehouseRate | null>('/warehouse-sync/payments/rate') } catch { /* no sync yet, or no rights */ }
}

// A sync on another tab or panel brings a newer rate: look again now and then.
let timer: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  load()
  timer = setInterval(load, 60_000)
})
onBeforeUnmount(() => clearInterval(timer))
defineExpose({ load })
</script>
