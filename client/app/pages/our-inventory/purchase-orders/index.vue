<template>
  <DataListPage
    title="Stock Purchase Orders"
    :headers="headers"
    api-url="/our-inventory/purchase-orders"
    :status-options="STATUS_OPTIONS"
    detail-route="/purchase-orders"
    :server-side="true"
    :extra-params="extraParams"
    page-key="stock-pos"
    :show-total-sum="true"
    :hide-clear-button="true"
    search-placeholder="Search PO #, subject or supplier PI #"
  >
    <template #actions>
      <v-btn color="primary" variant="flat" size="small" prepend-icon="mdi-plus" to="/our-inventory/purchase-orders/new">New Stock PO</v-btn>
    </template>

    <template #filters>
      <v-autocomplete v-model="supplierFilter" :items="options.suppliers" item-title="name" item-value="id" label="Supplier"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 160px; max-width: 260px;" :loading="optionsLoading" />
      <v-autocomplete v-model="presetFilter" :items="options.companyPresets" item-title="name" item-value="id" label="Company"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 150px; max-width: 240px;" :loading="optionsLoading" />
      <v-autocomplete v-model="warehouseFilter" :items="options.warehouses" item-title="name" item-value="id" label="Warehouse"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 150px; max-width: 240px;" :loading="optionsLoading" />
      <v-text-field v-model="createdFrom" type="date" label="Created from" clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 150px; max-width: 190px;" />
      <v-text-field v-model="createdTo" type="date" label="Created to" clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 150px; max-width: 190px;" />
      <v-btn v-if="hasActiveFilters" variant="tonal" color="error" size="small" prepend-icon="mdi-filter-off" class="align-self-center" @click="clearFilters">Clear</v-btn>
    </template>

    <template #item.poNumber="{ item }">
      <span class="font-weight-medium text-primary">{{ item.poNumber }}</span>
      <div v-if="item.subject" class="text-caption text-medium-emphasis text-truncate" style="max-width: 220px">{{ item.subject }}</div>
    </template>
    <template #item.status="{ item }">
      <v-chip size="small" variant="tonal" :color="stockPoStatusColor(item.status)">{{ item.status }}</v-chip>
    </template>
    <template #item.totalAmount="{ item }">${{ formatPrice(item.totalAmount) }}</template>
    <template #item.paidAmount="{ item }">
      <span :class="item.paidAmount >= item.totalAmount && item.totalAmount > 0 ? 'text-success' : ''">${{ formatPrice(item.paidAmount) }}</span>
      <div v-if="item.prStatus" class="text-caption text-medium-emphasis">PR: {{ item.prStatus }}</div>
    </template>
    <template #item.received="{ item }">
      <div class="d-flex align-center gap-2" style="min-width: 120px">
        <v-progress-linear :model-value="receivedPct(item)" :color="receivedPct(item) >= 100 ? 'success' : 'primary'" height="6" rounded />
        <span class="text-caption text-no-wrap">{{ fmtQty(item.qtyReceived) }}/{{ item.qtyOrdered }}</span>
      </div>
    </template>
    <template #item.expectedDeliveryDate="{ item }">
      {{ item.expectedDeliveryDate ? new Date(item.expectedDeliveryDate).toLocaleDateString() : '—' }}
    </template>
    <template #item.createdAt="{ item }">{{ new Date(item.createdAt).toLocaleDateString() }}</template>
    <template #item.actions="{ item }">
      <v-btn v-if="item.status === 'Draft'" icon="mdi-pencil" variant="text" size="small" title="Edit draft"
        :to="`/our-inventory/purchase-orders/new?id=${item.id}`" @click.stop />
      <v-btn icon="mdi-eye" variant="text" size="small" :to="`/purchase-orders/${item.id}`" @click.stop />
    </template>
  </DataListPage>
</template>

<script setup lang="ts">
const api = useApi()

const STATUS_OPTIONS = [
  'Draft', 'Waiting For Admin Approval', 'Waiting For Supplier Documents', 'Waiting For PR', 'Waiting For Payment',
  'PR Rejected', 'Payment Done', 'Ship to Warehouse', 'Received in Warehouse', 'Completed', 'Cancelled',
]

const headers = [
  { title: 'PO #', key: 'poNumber', sortable: false },
  { title: 'Status', key: 'status', sortable: false },
  { title: 'Supplier', key: 'supplierName', sortable: false },
  { title: 'Company', key: 'companyPresetName', sortable: false },
  { title: 'Warehouse', key: 'destinationWarehouseName', sortable: false },
  { title: 'Total', key: 'totalAmount', sortable: false, align: 'end' as const },
  { title: 'Paid', key: 'paidAmount', sortable: false, align: 'end' as const },
  { title: 'Received', key: 'received', sortable: false },
  { title: 'Expected', key: 'expectedDeliveryDate', sortable: false },
  { title: 'Created', key: 'createdAt', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]

// Same key as DataListPage's page-key, so one Clear resets search, status and these filters together.
const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('stock-pos', {
  search: '',
  status: [] as string[],
  supplier: [] as number[],
  preset: [] as number[],
  warehouse: [] as number[],
  createdFrom: '',
  createdTo: '',
})
const supplierFilter = pf.supplier
const presetFilter = pf.preset
const warehouseFilter = pf.warehouse
const createdFrom = pf.createdFrom
const createdTo = pf.createdTo

const extraParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = {}
  if (supplierFilter.value?.length) p.supplierIds = supplierFilter.value.map(String)
  if (presetFilter.value?.length) p.companyPresetIds = presetFilter.value.map(String)
  if (warehouseFilter.value?.length) p.warehouseIds = warehouseFilter.value.map(String)
  if (createdFrom.value) p.createdFrom = createdFrom.value
  if (createdTo.value) p.createdTo = createdTo.value
  return p
})

// Dropdowns only offer values that still return rows under the other filters (exclude-self on the API).
type Opt = { id: number; name: string }
const options = shallowRef<{ suppliers: Opt[]; companyPresets: Opt[]; warehouses: Opt[] }>({ suppliers: [], companyPresets: [], warehouses: [] })
const optionsLoading = ref(false)
let optionsTimer: any = null
async function loadOptions() {
  optionsLoading.value = true
  try {
    const params = new URLSearchParams()
    for (const [k, v] of Object.entries(extraParams.value)) (Array.isArray(v) ? v : [v]).forEach(x => params.append(k, x))
    options.value = await api.get<any>(`/our-inventory/purchase-orders/filter-options?${params}`)
  } catch {} finally { optionsLoading.value = false }
}
watch(extraParams, () => { clearTimeout(optionsTimer); optionsTimer = setTimeout(loadOptions, 250) }, { deep: true })
onMounted(loadOptions)

const fmtQty = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })
const receivedPct = (item: any) => item.qtyOrdered ? Math.min(100, (Number(item.qtyReceived) / item.qtyOrdered) * 100) : 0
</script>
