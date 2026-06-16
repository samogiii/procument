<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">Final Invoices</h1>
      <v-spacer />
      <v-btn prepend-icon="mdi-plus" color="primary" @click="showAddDialog = true">Create Final Invoice</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <!-- Filter bar -->
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search..."
            single-line
            hide-details
            class="flex-grow-1"
            style="min-width: 180px;"
          />
          <v-text-field
            v-model="pnSearch"
            label="Search by P/N"
            prepend-inner-icon="mdi-cog-outline"
            hide-details
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 160px; max-width: 260px;"
          />
          <v-autocomplete
            v-model="customerCodesFilter"
            :items="cfCustomerOptions"
            label="Customer Code"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 260px;"
          />
          <v-autocomplete
            v-model="statusesFilter"
            :items="cfStatusOptions"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 240px;"
          />
          <v-text-field
            v-model="createdFrom"
            label="Created From"
            type="date"
            hide-details
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 160px; max-width: 200px;"
          />
          <v-text-field
            v-model="createdTo"
            label="Created To"
            type="date"
            hide-details
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 160px; max-width: 200px;"
          />
          <v-btn
            v-if="hasActiveFilters"
            variant="tonal"
            color="error"
            size="small"
            prepend-icon="mdi-filter-off"
            class="align-self-center"
            @click="clearFilters"
          >
            Clear
          </v-btn>
        </div>

        <v-data-table-server
          :headers="headers"
          :items="serverItems"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="50"
          density="comfortable"
          hover
          @update:options="onTableOptions"
          @click:row="(_: any, { item }: any) => navigateTo(`/final-invoices/${item.id}`)"
        >
          <template #item.status="{ item }">
            <v-chip :color="statusColor(item.status)" size="small">{{ item.status }}</v-chip>
          </template>
          <template #item.totalAmount="{ item }">
            ${{ formatPrice(item.totalAmount) }}
          </template>
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>
          <template #item.dueDate="{ item }">
            {{ item.dueDate ? new Date(item.dueDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.paidDate="{ item }">
            {{ item.paidDate ? new Date(item.paidDate).toLocaleDateString() : '—' }}
          </template>
        </v-data-table-server>
      </v-card-text>
    </v-card>

    <v-dialog v-model="showAddDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title>Create Final Invoice</v-card-title>
        <v-card-text>
          <div v-if="loadingProformas" class="d-flex justify-center my-4">
            <v-progress-circular indeterminate color="primary" />
          </div>
          <v-select
            v-else
            v-model="selectedProformaId"
            :items="eligibleProformas"
            item-title="displayText"
            item-value="id"
            label="Select Sales Order"
            variant="outlined"
            density="comfortable"
            hide-details
            placeholder="Choose an eligible Proforma..."
          />
          <div v-if="!loadingProformas && eligibleProformas.length === 0" class="text-caption text-error mt-2">
            No Sales Order are currently eligible. (Requires at least one Completed PO).
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showAddDialog = false">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="tonal"
            :disabled="!selectedProformaId || eligibleProformas.length === 0"
            :loading="creating"
            @click="createFinalInvoice"
          >
            Create
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const { statusColor } = useStatusColor()

// ─── Filter state (persisted) ───
const { filters: pf, clearFilters: clearPageFilters, hasActiveFilters } = usePageFilters('final-invoices', {
  search: '',
  pnSearch: '',
  customerCodesFilter: [] as string[],
  statusesFilter: [] as string[],
  createdFrom: '',
  createdTo: '',
})
const search = pf.search
const pnSearch = pf.pnSearch
const customerCodesFilter = pf.customerCodesFilter
const statusesFilter = pf.statusesFilter
const createdFrom = pf.createdFrom
const createdTo = pf.createdTo

function clearFilters() {
  clearPageFilters()
  reload()
}

// ─── Filter option lists ───
const cfCustomerOptions = ref<string[]>([])
const cfStatusOptions = ref<string[]>([])

async function loadFilterOptions() {
  try {
    const res = await api.get<any>('/final-invoices/filter-options')
    cfStatusOptions.value = (res.statuses || []).sort()
    cfCustomerOptions.value = ([...new Set((res.customers || [])
      .map((c: any) => c.code || '-'))] as string[]).sort()
  } catch {}
}

// ─── Server-side data ───
const serverItems = ref<any[]>([])
const totalItems = ref(0)
const loading = ref(false)
const currentOptions = ref<any>({ page: 1, itemsPerPage: 50, sortBy: [] })

let debounceTimer: ReturnType<typeof setTimeout> | null = null
function scheduleReload() {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => reload(), 350)
}

// Watch all filter values and trigger re-fetch
watch([search, pnSearch, customerCodesFilter, statusesFilter, createdFrom, createdTo], () => {
  currentOptions.value = { ...currentOptions.value, page: 1 }
  scheduleReload()
}, { deep: true })

async function reload() {
  await onTableOptions(currentOptions.value)
}

async function onTableOptions(opts: any) {
  currentOptions.value = opts
  loading.value = true
  try {
    const params = new URLSearchParams({
      page: String(opts.page ?? 1),
      pageSize: String(opts.itemsPerPage ?? 50),
    })
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (pnSearch.value?.trim()) params.set('pnSearch', pnSearch.value.trim())
    if (createdFrom.value) params.set('createdFrom', createdFrom.value)
    if (createdTo.value) params.set('createdTo', createdTo.value)
    ;(customerCodesFilter.value || []).forEach(c => params.append('customerCodes', c))
    ;(statusesFilter.value || []).forEach(s => params.append('statuses', s))

    // Sort
    const sortItem = opts.sortBy?.[0]
    if (sortItem?.key) {
      params.set('sortBy', sortItem.key)
      params.set('sortDesc', String(sortItem.order === 'desc'))
    }

    const res = await api.get<any>(`/final-invoices?${params}`)
    serverItems.value = res.items ?? res.Items ?? []
    totalItems.value = res.totalCount ?? res.TotalCount ?? serverItems.value.length
  } finally {
    loading.value = false
  }
}

const headers = [
  { title: 'Invoice #', key: 'invoiceNumber', sortable: true },
  { title: 'Customer', key: 'customerCode', sortable: true },
  { title: 'Proforma Ref', key: 'proformaInvoiceNumber', sortable: true },
  { title: 'Total', key: 'totalAmount', sortable: true },
  { title: 'Status', key: 'status', sortable: true },
  { title: 'Due Date', key: 'dueDate', sortable: true },
  { title: 'Paid Date', key: 'paidDate', sortable: true },
  { title: 'Created', key: 'createdAt', sortable: true },
]

function formatPrice(v: number | null | undefined) {
  if (v == null) return '0.00'
  return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

onMounted(() => {
  loadFilterOptions()
})

// ─── Create dialog ───
const showAddDialog = ref(false)
const selectedProformaId = ref<number | null>(null)
const eligibleProformas = ref<any[]>([])
const loadingProformas = ref(false)
const creating = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

watch(showAddDialog, async (val) => {
  if (val) {
    selectedProformaId.value = null
    loadingProformas.value = true
    try {
      const data = await api.get<any[]>('/final-invoices/eligible-proformas')
      eligibleProformas.value = data.map((p: any) => ({
        ...p,
        displayText: `${p.invoiceNumber} - ${p.customerName} ($${formatPrice(p.totalAmount)})`
      }))
    } catch {
      showSnack('Failed to load eligible proformas', 'error')
    } finally {
      loadingProformas.value = false
    }
  }
})

async function createFinalInvoice() {
  if (!selectedProformaId.value) return
  creating.value = true
  try {
    const result = await api.post<any>('/final-invoices', { proformaInvoiceId: selectedProformaId.value })
    showAddDialog.value = false
    showSnack('Final invoice created!', 'success')
    navigateTo(`/final-invoices/${result.id}`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to create final invoice', 'error')
  } finally {
    creating.value = false
  }
}
</script>
