<template>
  <DataListPage
    title="Our Stock"
    :headers="headers"
    api-url="/our-inventory/stock"
    detail-route="/our-inventory"
    :server-side="true"
    :extra-params="extraParams"
    page-key="our-stock"
    :hide-clear-button="true"
    :show-total-sum="authStore.isAdmin"
    search-placeholder="Search P/N, description, bin or certificate"
  >
    <template #actions>
      <v-menu>
        <template #activator="{ props: menuProps }">
          <v-btn v-bind="menuProps" variant="tonal" size="small" prepend-icon="mdi-file-chart-outline" append-icon="mdi-chevron-down"
            :loading="!!stockPdf.loading.value || exporting">Report</v-btn>
        </template>
        <v-list density="compact">
          <v-list-item prepend-icon="mdi-file-pdf-box" :title="authStore.isAdmin ? 'Stock valuation (PDF)' : 'Stock report (PDF)'" @click="openReport" />
          <v-list-item prepend-icon="mdi-microsoft-excel" title="Export to Excel" @click="exportExcel" />
        </v-list>
      </v-menu>
      <v-btn v-if="authStore.isAdmin" variant="tonal" size="small" prepend-icon="mdi-database-import-outline" @click="showImport = true">Import opening stock</v-btn>
      <v-btn v-if="authStore.isAdmin" variant="tonal" size="small" prepend-icon="mdi-truck-check-outline" to="/our-inventory/receive">Receive</v-btn>
      <v-btn color="primary" variant="flat" size="small" prepend-icon="mdi-cart-arrow-down" to="/our-inventory/purchase-orders/new">New Stock PO</v-btn>
    </template>

    <template #filters>
      <v-autocomplete v-model="warehouseFilter" :items="options.warehouses" item-title="name" item-value="id" label="Warehouse"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 150px; max-width: 240px;" />
      <v-autocomplete v-model="presetFilter" :items="options.companyPresets" item-title="name" item-value="id" label="Company"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 150px; max-width: 240px;" />
      <v-autocomplete v-model="conditionFilter" :items="options.conditions" label="Condition"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2" style="min-width: 130px; max-width: 200px;" />
      <v-btn :variant="onlyAvailable ? 'tonal' : 'outlined'" :color="onlyAvailable ? 'success' : undefined" size="small" class="align-self-center"
        prepend-icon="mdi-check-circle-outline" @click="onlyAvailable = !onlyAvailable">Available only</v-btn>
      <v-btn :variant="lowStock ? 'tonal' : 'outlined'" :color="lowStock ? 'warning' : undefined" size="small" class="align-self-center"
        prepend-icon="mdi-alert-outline" @click="lowStock = !lowStock">Low stock</v-btn>
      <v-btn v-if="hasActiveFilters" variant="tonal" color="error" size="small" prepend-icon="mdi-filter-off" class="align-self-center" @click="clearFilters">Clear</v-btn>
    </template>

    <template #item.partNumber="{ item }">
      <span class="font-weight-medium text-primary">{{ item.partNumber }}</span>
      <div v-if="item.description" class="text-caption text-medium-emphasis text-truncate" style="max-width: 240px">{{ item.description }}</div>
    </template>
    <template #item.condition="{ item }">
      <v-chip size="x-small" variant="tonal">{{ item.condition }}</v-chip>
      <div v-if="item.certName" class="text-caption text-medium-emphasis">{{ item.certName }}</div>
    </template>
    <template #item.qtyOnHand="{ item }">{{ fmtQty(item.qtyOnHand) }}</template>
    <template #item.qtyReserved="{ item }">
      <span :class="item.qtyReserved > 0 ? 'text-warning' : 'text-medium-emphasis'">{{ fmtQty(item.qtyReserved) }}</span>
    </template>
    <template #item.qtyAvailable="{ item }">
      <v-chip size="small" variant="tonal" :color="item.isLowStock ? 'warning' : (item.qtyAvailable > 0 ? 'success' : 'grey')">
        {{ fmtQty(item.qtyAvailable) }}
      </v-chip>
      <div v-if="item.minQty != null" class="text-caption text-medium-emphasis">min {{ fmtQty(item.minQty) }}</div>
    </template>
    <template #item.avgUnitCost="{ item }">{{ item.avgUnitCost != null ? `$${formatPrice(item.avgUnitCost)}` : '—' }}</template>
    <template #item.totalAmount="{ item }">{{ item.totalAmount != null ? `$${formatPrice(item.totalAmount)}` : '—' }}</template>
    <template #item.binLocation="{ item }">{{ item.binLocation || '—' }}</template>
    <template #item.updatedAt="{ item }">{{ new Date(item.updatedAt).toLocaleDateString() }}</template>

    <DocPreviewModal
      :open="stockPdf.preview.open.value"
      :blob-url="stockPdf.preview.blobUrl.value"
      :file-name="stockPdf.preview.fileName.value"
      :mime-type="stockPdf.preview.mimeType.value"
      @close="stockPdf.preview.close()"
    />
    <OpeningStockImportDialog v-model="showImport" @imported="onImported" />
    <v-snackbar v-model="importSnack" color="success" :timeout="4000" location="bottom end">{{ importMessage }}</v-snackbar>
    <v-snackbar :model-value="!!reportError" color="error" :timeout="4000" location="bottom end" @update:model-value="reportError = ''">{{ reportError }}</v-snackbar>
  </DataListPage>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()

const headers = computed(() => [
  { title: 'P/N', key: 'partNumber', sortable: false },
  { title: 'Cond.', key: 'condition', sortable: false },
  { title: 'Warehouse', key: 'warehouseName', sortable: false },
  { title: 'Company', key: 'companyPresetName', sortable: false },
  { title: 'Bin', key: 'binLocation', sortable: false },
  { title: 'On hand', key: 'qtyOnHand', sortable: false, align: 'end' as const },
  { title: 'Reserved', key: 'qtyReserved', sortable: false, align: 'end' as const },
  { title: 'Available', key: 'qtyAvailable', sortable: false, align: 'end' as const },
  ...(authStore.isAdmin ? [
    { title: 'Avg cost', key: 'avgUnitCost', sortable: false, align: 'end' as const },
    { title: 'Value', key: 'totalAmount', sortable: false, align: 'end' as const },
  ] : []),
  { title: 'Updated', key: 'updatedAt', sortable: false },
])

// Shares DataListPage's page-key so one Clear resets search as well.
// The header total is the stock value over all pages; the API only sends it to Admin / SuperAdmin.
const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('our-stock', {
  search: '',
  warehouse: [] as number[],
  preset: [] as number[],
  condition: [] as string[],
  onlyAvailable: false,
  lowStock: false,
})
const warehouseFilter = pf.warehouse
const presetFilter = pf.preset
const conditionFilter = pf.condition
const onlyAvailable = pf.onlyAvailable
const lowStock = pf.lowStock

const refreshKey = ref(0)
const extraParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = {}
  if (warehouseFilter.value?.length) p.warehouseIds = warehouseFilter.value.map(String)
  if (presetFilter.value?.length) p.companyPresetIds = presetFilter.value.map(String)
  if (conditionFilter.value?.length) p.conditions = conditionFilter.value
  if (onlyAvailable.value) p.onlyAvailable = 'true'
  if (lowStock.value) p.lowStock = 'true'
  // Bumped after an import so DataListPage re-fetches; the API ignores it.
  if (refreshKey.value) p._r = String(refreshKey.value)
  return p
})

type Opt = { id: number; name: string }
const options = shallowRef<{ warehouses: Opt[]; companyPresets: Opt[]; conditions: string[] }>({ warehouses: [], companyPresets: [], conditions: [] })
let optionsTimer: any = null
async function loadOptions() {
  try {
    const params = new URLSearchParams()
    if (pf.search.value) params.set('search', pf.search.value)
    for (const [k, v] of Object.entries(extraParams.value)) (Array.isArray(v) ? v : [v]).forEach(x => params.append(k, x))
    options.value = await api.get<any>(`/our-inventory/stock/filter-options?${params}`)
  } catch {}
}
watch([extraParams, () => pf.search.value], () => { clearTimeout(optionsTimer); optionsTimer = setTimeout(loadOptions, 250) }, { deep: true })
onMounted(loadOptions)

const fmtQty = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })

// ── Opening stock import ──
const showImport = ref(false)
const importSnack = ref(false)
const importMessage = ref('')
function onImported(res: any) {
  importMessage.value = `Imported ${fmtQty(res.units)} unit(s) into ${res.lotsTouched} lot(s)${res.newPartNumbers ? `, ${res.newPartNumbers} new part number(s)` : ''}`
  importSnack.value = true
  refreshKey.value++
  loadOptions()
}

// ── Report (PDF) and Excel export — both use the filters currently applied to the list ──
const stockPdf = useStockPdf()
const exporting = ref(false)
const reportError = ref('')
watch(stockPdf.error, (v) => { if (v) reportError.value = v })

function currentParams() {
  const params = new URLSearchParams()
  if (pf.search.value) params.set('search', pf.search.value)
  for (const [k, v] of Object.entries(extraParams.value)) if (k !== '_r') (Array.isArray(v) ? v : [v]).forEach(x => params.append(k, x))
  return params
}

/** Human-readable summary of the active filters, printed on the report. */
function filterLabel() {
  const names = (ids: number[], list: Opt[]) => ids.map(id => list.find(o => o.id === id)?.name ?? `#${id}`).join(', ')
  const parts: string[] = []
  if (pf.search.value) parts.push(`search "${pf.search.value}"`)
  if (warehouseFilter.value?.length) parts.push(`warehouse ${names(warehouseFilter.value, options.value.warehouses)}`)
  if (presetFilter.value?.length) parts.push(`company ${names(presetFilter.value, options.value.companyPresets)}`)
  if (conditionFilter.value?.length) parts.push(`condition ${conditionFilter.value.join(', ')}`)
  if (onlyAvailable.value) parts.push('available only')
  if (lowStock.value) parts.push('below minimum')
  return parts.join(' · ')
}

function openReport() {
  stockPdf.openStockReport(currentParams(), filterLabel())
}

async function exportExcel() {
  exporting.value = true
  try {
    const report = await api.get<any>(`/our-inventory/stock/valuation?${currentParams()}`)
    const rows = report.groups.flatMap((g: any) => g.items.map((i: any) => ({
      'Company': g.companyPresetName,
      'Warehouse': g.warehouseName,
      'Part Number': i.partNumber,
      'Description': i.description || '',
      'Condition': i.condition,
      'Certificate': i.certName || '',
      'Tag Date': i.tagDate ? String(i.tagDate).slice(0, 10) : '',
      'Bin': i.binLocation || '',
      'On Hand': Number(i.qtyOnHand),
      'Reserved': Number(i.qtyReserved),
      'Available': Number(i.qtyAvailable),
      'Min Qty': i.minQty != null ? Number(i.minQty) : '',
      ...(report.includesCost ? { 'Avg Unit Cost': Number(i.avgUnitCost), 'Value': Number(i.totalAmount) } : {}),
    })))
    downloadExcel(rows, `Our-Stock-${new Date().toISOString().slice(0, 10)}`, 'Our Stock')
  } catch (e: any) {
    reportError.value = e?.data?.message || 'Export failed'
  } finally {
    exporting.value = false
  }
}
</script>
