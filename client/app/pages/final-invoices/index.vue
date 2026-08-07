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
            :items="customerSelectItems"
            label="Customer Code"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 260px;"
          >
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllCustomers ? 'Show available only' : 'Show all customers'"
                :prepend-icon="showAllCustomers ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllCustomers = !showAllCustomers"
              />
            </template>
          </v-autocomplete>
          <v-autocomplete
            v-model="statusesFilter"
            :items="statusSelectItems"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 240px;"
          >
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllStatuses ? 'Show available only' : 'Show all statuses'"
                :prepend-icon="showAllStatuses ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllStatuses = !showAllStatuses"
              />
            </template>
          </v-autocomplete>
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
          <!-- Header filters share the top-bar refs: :options is what the other filters
               still leave selectable, :all-options is everything ("Show all"). -->
          <template #header.customerCode="{ column, toggleSort, isSorted, sortBy }">
            <ColFilterMenu
              col-key="customerCode"
              :label="column.title"
              :options="cfCustomerAvailable"
              :all-options="cfCustomerOptions"
              :selected="customerSet"
              :search="colSearch.customerCode"
              :loading="cfLoading"
              :is-sorted="isSorted(column)"
              :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
              @toggle="(v) => toggleColFilter(customerCodesFilter, v)"
              @select-all="(vals) => setColFilter(customerCodesFilter, vals)"
              @clear-all="() => setColFilter(customerCodesFilter, [])"
              @update:search="(v) => colSearch.customerCode = v"
              @sort-click="toggleSort(column)"
            />
          </template>
          <template #header.status="{ column, toggleSort, isSorted, sortBy }">
            <ColFilterMenu
              col-key="status"
              :label="column.title"
              :options="cfStatusAvailable"
              :all-options="cfStatusOptions"
              :selected="statusSet"
              :search="colSearch.status"
              :loading="cfLoading"
              :is-sorted="isSorted(column)"
              :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
              @toggle="(v) => toggleColFilter(statusesFilter, v)"
              @select-all="(vals) => setColFilter(statusesFilter, vals)"
              @clear-all="() => setColFilter(statusesFilter, [])"
              @update:search="(v) => colSearch.status = v"
              @sort-click="toggleSort(column)"
            />
          </template>

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
/**
 * `available` is refetched on every filter change and holds only the values that still
 * return rows once the *other* filters are applied; `all` is the unfiltered list, which
 * the header menus expose behind "Show all" and the dropdowns behind their toggle.
 */
type FiOptions = { statuses: string[]; customers: string[] }
const EMPTY_FI_OPTIONS: FiOptions = { statuses: [], customers: [] }

const cfOptions = useCascadingOptions<FiOptions>(
  async (cascading) => {
    const params = new URLSearchParams()
    if (cascading) {
      if (search.value?.trim()) params.set('search', search.value.trim())
      if (pnSearch.value?.trim()) params.set('pnSearch', pnSearch.value.trim())
      if (createdFrom.value) params.set('createdFrom', createdFrom.value)
      if (createdTo.value) params.set('createdTo', createdTo.value)
      ;(customerCodesFilter.value || []).forEach(c => params.append('customerCodes', c))
      ;(statusesFilter.value || []).forEach(s => params.append('statuses', s))
    }
    const qs = params.toString()
    const res = await api.get<any>(`/final-invoices/filter-options${qs ? `?${qs}` : ''}`)
    return {
      statuses: (res.statuses || []).sort(),
      customers: ([...new Set((res.customers || []).map((c: any) => c.code || '-'))] as string[]).sort(),
    }
  },
  EMPTY_FI_OPTIONS,
)
const cfLoading = cfOptions.loading

const cfCustomerOptions = computed(() => cfOptions.all.value.customers)
const cfStatusOptions = computed(() => cfOptions.all.value.statuses)
const cfCustomerAvailable = computed(() => cfOptions.available.value.customers)
const cfStatusAvailable = computed(() => cfOptions.available.value.statuses)

const showAllCustomers = ref(false)
const showAllStatuses = ref(false)

/** Dropdown items: available only, plus anything already picked so a chip stays removable. */
function withSelected(available: string[], all: string[], picked: string[], showAll: boolean) {
  if (showAll) return all
  const extra = picked.filter(p => !available.includes(p))
  return extra.length ? [...available, ...extra] : available
}
const customerSelectItems = computed(() =>
  withSelected(cfCustomerAvailable.value, cfCustomerOptions.value, customerCodesFilter.value || [], showAllCustomers.value))
const statusSelectItems = computed(() =>
  withSelected(cfStatusAvailable.value, cfStatusOptions.value, statusesFilter.value || [], showAllStatuses.value))

// ColFilterMenu takes Sets; the page keeps arrays because they go straight into the query.
const customerSet = computed(() => new Set(customerCodesFilter.value))
const statusSet = computed(() => new Set(statusesFilter.value))
const colSearch = reactive<Record<string, string>>({ customerCode: '', status: '' })

function toggleColFilter(arr: string[], val: string) {
  const idx = arr.indexOf(val)
  if (idx >= 0) arr.splice(idx, 1)
  else arr.push(val)
}
function setColFilter(arr: string[], vals: string[]) {
  arr.splice(0, arr.length, ...vals)
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

// Watch all filter values and trigger re-fetch — plus re-narrow the other filters' options
watch([search, pnSearch, customerCodesFilter, statusesFilter, createdFrom, createdTo], () => {
  currentOptions.value = { ...currentOptions.value, page: 1 }
  scheduleReload()
  cfOptions.refreshDebounced()
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
  cfOptions.init()
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
