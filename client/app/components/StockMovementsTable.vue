<template>
  <v-data-table-server
    :headers="headers"
    :items="rows"
    :items-length="total"
    :loading="loading"
    :items-per-page="pageSize"
    :items-per-page-options="[25, 50, 100]"
    density="compact"
    hover
    @update:options="load"
  >
    <template #item.createdAt="{ item }">
      <span class="text-no-wrap">{{ new Date(item.createdAt).toLocaleString() }}</span>
    </template>
    <template #item.type="{ item }">
      <v-chip size="x-small" variant="tonal" :color="TYPE_COLORS[item.type] || 'grey'">{{ TYPE_LABELS[item.type] || item.type }}</v-chip>
    </template>
    <template #item.qty="{ item }">
      <span :class="item.qty < 0 ? 'text-error' : 'text-success'" class="font-weight-medium">
        {{ item.qty > 0 ? '+' : '' }}{{ formatQty(item.qty) }}
      </span>
    </template>
    <template #item.unitCost="{ item }">
      {{ item.unitCost != null ? `$${formatPrice(item.unitCost)}` : '—' }}
    </template>
    <template #item.partNumber="{ item }">
      <NuxtLink :to="`/our-inventory/${item.stockItemId}`" class="text-primary text-decoration-none font-weight-medium">{{ item.partNumber }}</NuxtLink>
      <span class="text-caption text-medium-emphasis"> · {{ item.condition }}</span>
    </template>
    <template #item.reference="{ item }">
      <NuxtLink v-if="item.poId" :to="`/purchase-orders/${item.poId}`" class="text-primary text-decoration-none">{{ item.reference }}</NuxtLink>
      <span v-else>{{ item.reference }}</span>
      <div v-if="item.reason" class="text-caption text-medium-emphasis">{{ item.reason }}</div>
    </template>
  </v-data-table-server>
</template>

<script setup lang="ts">
/** The Our Stock ledger for one lot or one part (`/our-inventory/movements`). Read-only. */
const props = defineProps<{
  stockItemId?: number | string
  partNumberId?: number | string
  showPart?: boolean
  /** Log-page filters: movement types, date range and free-text search. */
  types?: string[]
  from?: string
  to?: string
  search?: string
}>()

const api = useApi()
const authStore = useAuthStore()

const TYPE_LABELS: Record<string, string> = {
  Receipt: 'Receipt', Issue: 'Issue', Adjust: 'Adjust', TransferOut: 'Transfer out', TransferIn: 'Transfer in',
  ReturnToSupplier: 'Return to supplier', CustomerReturn: 'Customer return', Opening: 'Opening',
}
const TYPE_COLORS: Record<string, string> = {
  Receipt: 'success', Opening: 'success', CustomerReturn: 'success', TransferIn: 'info',
  Issue: 'error', ReturnToSupplier: 'error', TransferOut: 'warning', Adjust: 'purple',
}

const headers = computed(() => [
  { title: 'Date', key: 'createdAt', sortable: false },
  ...(props.showPart ? [{ title: 'P/N', key: 'partNumber', sortable: false }, { title: 'Warehouse', key: 'warehouseName', sortable: false }] : []),
  { title: 'Type', key: 'type', sortable: false },
  { title: 'Qty', key: 'qty', sortable: false, align: 'end' as const },
  ...(authStore.isAdmin ? [{ title: 'Unit Cost', key: 'unitCost', sortable: false, align: 'end' as const }] : []),
  { title: 'Reference', key: 'reference', sortable: false },
  { title: 'Track #', key: 'trackNumber', sortable: false },
  { title: 'By', key: 'createdByName', sortable: false },
])

const rows = ref<any[]>([])
const total = ref(0)
const loading = ref(false)
const pageSize = ref(25)
let lastOptions = { page: 1, itemsPerPage: 25 }

const formatQty = (q: number) => Number(q).toLocaleString(undefined, { maximumFractionDigits: 2 })

async function load(options: { page: number; itemsPerPage: number } = lastOptions) {
  lastOptions = options
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(options.page), pageSize: String(options.itemsPerPage) })
    if (props.stockItemId) params.set('stockItemId', String(props.stockItemId))
    if (props.partNumberId) params.set('partNumberId', String(props.partNumberId))
    ;(props.types || []).forEach(t => params.append('type', t))
    if (props.from) params.set('from', props.from)
    if (props.to) params.set('to', props.to)
    if (props.search) params.set('search', props.search)
    const res = await api.get<any>(`/our-inventory/movements?${params}`)
    rows.value = res.items || []
    total.value = res.totalCount || 0
  } catch {
    rows.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

watch(() => [props.stockItemId, props.partNumberId, props.types, props.from, props.to, props.search], () => load({ ...lastOptions, page: 1 }), { deep: true })
defineExpose({ reload: () => load() })
</script>
