<template>
  <DataListPage
    title="Sales Order"
    :headers="headers"
    api-url="/invoices"
    detail-route="/invoices"
    show-select
    v-model="selectedInvoices"
    :server-side="true"
    :extra-params="extraParams"
    page-key="invoices"
    :status-options="['Draft', 'Pending', 'Running', 'Waiting For PrePayment', 'Delivered', 'Finish', 'Cancelled']"
    :custom-filter="invoiceCustomFilter"
  >
    <template #filters>
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
        v-model="customerCodesDropdown"
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
    </template>

    <!-- Column filter: Sales Order # -->
    <template #header.invoiceNumber="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="invoiceNumber"
        :label="column.title"
        :options="cfInvoiceNumberAvailable"
        :all-options="cfInvoiceNumberOptions"
        :selected="colFilter.selected['invoiceNumber'] || new Set()"
        :search="colFilter.search['invoiceNumber'] || ''"
        :loading="cfLoading"
        @toggle="(v) => colFilter.toggle('invoiceNumber', v)"
        @select-all="(vals) => colFilter.selectAll('invoiceNumber', vals)"
        @clear-all="() => colFilter.clearAll('invoiceNumber')"
        @update:search="(v) => colFilter.search['invoiceNumber'] = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <!-- Column filter: Customer -->
    <template #header.customerCode="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="customerCode"
        :label="column.title"
        :options="cfCustomerAvailable"
        :all-options="cfCustomerOptions"
        :selected="colFilter.selected['customerCode'] || new Set()"
        :search="colFilter.search['customerCode'] || ''"
        :loading="cfLoading"
        @toggle="(v) => colFilter.toggle('customerCode', v)"
        @select-all="(vals) => colFilter.selectAll('customerCode', vals)"
        @clear-all="() => colFilter.clearAll('customerCode')"
        @update:search="(v) => colFilter.search['customerCode'] = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <!-- Column filter: Status -->
    <template #header.status="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="status"
        :label="column.title"
        :options="cfStatusAvailable"
        :all-options="cfStatusOptions"
        :selected="colFilter.selected['status'] || new Set()"
        :search="colFilter.search['status'] || ''"
        :loading="cfLoading"
        @toggle="(v) => colFilter.toggle('status', v)"
        @select-all="(vals) => colFilter.selectAll('status', vals)"
        @clear-all="() => colFilter.clearAll('status')"
        @update:search="(v) => colFilter.search['status'] = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <!-- Column filter: Base -->
    <template #header.customerBase="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="customerBase"
        :label="column.title"
        :options="cfBaseAvailable"
        :all-options="cfBaseOptions"
        :selected="colFilter.selected['customerBase'] || new Set()"
        :search="colFilter.search['customerBase'] || ''"
        :loading="cfLoading"
        :is-sorted="isSorted(column)"
        :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
        @toggle="(v) => colFilter.toggle('customerBase', v)"
        @select-all="(vals) => colFilter.selectAll('customerBase', vals)"
        @clear-all="() => colFilter.clearAll('customerBase')"
        @update:search="(v) => colFilter.search['customerBase'] = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <!-- Column filter: Subject -->
    <template #header.subject="{ column, toggleSort }">
      <ColFilterMenu
        col-key="subject"
        :label="column.title"
        :options="cfSubjectAvailable"
        :all-options="cfSubjectOptions"
        :selected="colFilter.selected['subject'] || new Set()"
        :search="colFilter.search['subject'] || ''"
        :loading="cfLoading"
        @toggle="(v) => colFilter.toggle('subject', v)"
        @select-all="(vals) => colFilter.selectAll('subject', vals)"
        @clear-all="() => colFilter.clearAll('subject')"
        @update:search="(v) => colFilter.search['subject'] = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <template #item.status="{ item }">
      <StatusChip :status="item.status" />
    </template>

    <!-- Base comes from the invoice's customer (Customer.Base) — every invoice belongs
         to exactly one base. Shown as the company preset name that owns that base. -->
    <template #item.customerBase="{ item }">
      <v-chip v-if="item.customerBase != null" size="x-small" color="secondary" variant="tonal">
        {{ baseLabel(item.customerBase) }}
      </v-chip>
      <span v-else class="text-medium-emphasis">—</span>
    </template>

    <template #before-table="{ totalAmountSum }">
      <div v-if="totalAmountSum != null" class="d-flex justify-end mb-2">
        <v-chip color="success" variant="tonal" size="small" prepend-icon="mdi-sigma">
          Total: ${{ formatPrice(totalAmountSum) }}
        </v-chip>
      </div>
    </template>

    <template #item.totalAmount="{ item }">
      ${{ formatPrice(item.totalAmount) }}
    </template>

    <template #item.customerPODate="{ item }">
      {{ item.customerPODate ? new Date(item.customerPODate).toLocaleDateString() : '-' }}
    </template>

    <template #item.createdAt="{ item }">
      {{ item.createdAt ? new Date(item.createdAt).toLocaleDateString() : '-' }}
    </template>

    <template #item.deadlineDate="{ item }">
      {{ item.deadlineDate ? new Date(item.deadlineDate).toLocaleDateString() : '-' }}
    </template>

    <template #item.actions="{ item }">
      <v-btn icon="mdi-eye" variant="text" size="small" :to="`/invoices/${item.id}`" />
    </template>

    <template #actions>
      <v-btn
        color="secondary"
        variant="tonal"
        prepend-icon="mdi-shield-account"
        class="mr-2"
        @click="showPermissionDialog = true"
        v-if="isAdmin"
      >
        Permissions {{ selectedInvoices.length > 0 ? `(${selectedInvoices.length})` : '' }}
      </v-btn>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="showCreateDialog = true">
        Create Sales Order
      </v-btn>
    </template>

    <v-dialog v-model="showCreateDialog" max-width="600">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center">
          Create Sales Order
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="showCreateDialog = false" />
        </v-card-title>
        <v-card-text>
          <p class="mb-4 text-grey-lighten-1">Select one or more Quotes to create a Sales Order from.</p>

          <v-autocomplete
            v-model="selectedQuotes"
            :items="filteredQuotes"
            :loading="loadingQuotes"
            item-title="quoteNumber"
            item-value="id"
            label="Search Quotes (Sent or Accepted)"
            placeholder="Type to search..."
            return-object
            multiple
            chips
            closable-chips
            variant="outlined"
            prepend-inner-icon="mdi-magnify"
            clearable
            no-filter
            @update:search="fetchQuotes"
          >
            <template #item="{ props, item }">
              <v-list-item v-bind="props" :subtitle="item.raw.customerCode">
                <template #append>
                   <v-chip size="x-small" :color="statusColor(item.raw.status)" class="ml-2">{{ item.raw.status }}</v-chip>
                </template>
              </v-list-item>
            </template>
          </v-autocomplete>
          <p v-if="selectedQuotes.length > 0" class="text-caption text-primary mt-1">
            Selected Quotes for: <strong>{{ selectedQuotes[0].customerCode }}</strong>
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showCreateDialog = false">Cancel</v-btn>
          <v-btn color="primary" :disabled="selectedQuotes.length === 0" @click="proceedToCreate">
            Proceed
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <BulkPermissionManager
      v-model="showPermissionDialog"
      entity-name="Invoice"
      :preselected-ids="selectedInvoices"
    />
  </DataListPage>
</template>

<script setup lang="ts">
const router = useRouter()
const api = useApi()
const { statusColor } = useStatusColor()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const showCreateDialog = ref(false)
const showPermissionDialog = ref(false)
const loadingQuotes = ref(false)
const availableQuotes = ref<any[]>([])
const selectedQuotes = ref<any[]>([])
const selectedInvoices = ref<number[]>([])

// ── Top-bar filters (persisted) ──
const { filters: pf, clearFilters: clearPageFilters, hasActiveFilters: hasPageFilters } = usePageFilters('invoices-filters', {
  customerCodesDropdown: [] as string[],
  pnSearch: '',
  createdFrom: '',
  createdTo: '',
})
const customerCodesDropdown = pf.customerCodesDropdown
const pnSearch = pf.pnSearch
const createdFrom = pf.createdFrom
const createdTo = pf.createdTo

// ── Column filters ──
const colFilter = useColFilterPersisted('invoices')

// DataListPage owns the search box + status chips under the same pageKey; usePageFilters
// hands back the identical refs, so the filter-options call sees exactly what the list sees.
const { filters: dlp } = usePageFilters('invoices', { search: '', status: [] as string[] })

const hasActiveFilters = computed(() => hasPageFilters.value || colFilter.hasAny())

function clearFilters() {
  clearPageFilters()
  colFilter.clearAllFilters()
}

const extraParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = {}

  // Merge top-bar dropdown + column filter for customer codes
  const allCodes = [...new Set([
    ...(customerCodesDropdown.value || []),
    ...colFilter.getSelected('customerCode'),
  ])] as string[]
  if (allCodes.length) p.customerCodes = allCodes

  if (pnSearch.value?.trim()) p.pnSearch = pnSearch.value.trim()
  if (createdFrom.value) p.createdFrom = createdFrom.value
  if (createdTo.value) p.createdTo = createdTo.value
  if (colFilter.isActive('status')) p.statuses = colFilter.getSelected('status')
  if (colFilter.isActive('invoiceNumber')) p.invoiceNumbers = colFilter.getSelected('invoiceNumber')
  if (colFilter.isActive('subject')) p.subjects = colFilter.getSelected('subject')
  if (colFilter.isActive('customerBase')) p.bases = colFilter.getSelected('customerBase')
  return p
})

/**
 * Two snapshots per column: `available` is recomputed from the backend on every filter
 * change and only contains values that still return rows once the *other* filters are
 * applied; `all` is the unfiltered list ColFilterMenu reveals behind "Show all".
 */
type InvoiceOptions = { statuses: string[]; customers: string[]; invoiceNumbers: string[]; subjects: string[]; bases: string[] }
const EMPTY_INVOICE_OPTIONS: InvoiceOptions = { statuses: [], customers: [], invoiceNumbers: [], subjects: [], bases: [] }

const cfOptions = useCascadingOptions<InvoiceOptions>(
  async (cascading) => {
    const params = new URLSearchParams()
    if (cascading) {
      if (dlp.search.value?.trim()) params.set('search', dlp.search.value.trim())
      if (pnSearch.value?.trim()) params.set('pnSearch', pnSearch.value.trim())
      if (createdFrom.value) params.set('createdFrom', createdFrom.value)
      if (createdTo.value) params.set('createdTo', createdTo.value)
      const statuses = [...new Set([...(dlp.status.value || []), ...colFilter.getSelected('status')])]
      statuses.forEach(s => params.append('statuses', s))
      const codes = [...new Set([...(customerCodesDropdown.value || []), ...colFilter.getSelected('customerCode')])]
      codes.forEach(c => params.append('customerCodes', c))
      colFilter.getSelected('invoiceNumber').forEach(v => params.append('invoiceNumbers', v))
      colFilter.getSelected('subject').forEach(v => params.append('subjects', v))
      colFilter.getSelected('customerBase').forEach(v => params.append('bases', v))
    }
    const qs = params.toString()
    const res = await api.get<any>(`/invoices/filter-options${qs ? `?${qs}` : ''}`)
    return {
      statuses: (res.statuses || []).sort(),
      customers: ([...new Set((res.customers || []).map((c: any) => c.code || '-'))] as string[]).sort(),
      invoiceNumbers: (res.invoiceNumbers || []).sort(),
      subjects: (res.subjects || []).sort(),
      // Kept as strings — colFilter stores selections in a Set<string>.
      bases: (res.bases || []).map((b: any) => String(b)),
    }
  },
  EMPTY_INVOICE_OPTIONS,
)
const cfLoading = cfOptions.loading

const cfStatusOptions = computed(() => cfOptions.all.value.statuses)
const cfCustomerOptions = computed(() => cfOptions.all.value.customers)
const cfInvoiceNumberOptions = computed(() => cfOptions.all.value.invoiceNumbers)
const cfSubjectOptions = computed(() => cfOptions.all.value.subjects)

const cfStatusAvailable = computed(() => cfOptions.available.value.statuses)
const cfCustomerAvailable = computed(() => cfOptions.available.value.customers)
const cfInvoiceNumberAvailable = computed(() => cfOptions.available.value.invoiceNumbers)
const cfSubjectAvailable = computed(() => cfOptions.available.value.subjects)

// Base menu shows preset names but filters on the numeric base, hence {title, value}.
const cfBaseOptions = computed(() => cfOptions.all.value.bases.map(b => ({ title: baseLabel(Number(b)), value: b })))
const cfBaseAvailable = computed(() => cfOptions.available.value.bases.map(b => ({ title: baseLabel(Number(b)), value: b })))

function invoiceCustomFilter(items: any[]): any[] {
  return items
}

// ── Base column ──
// Customer.Base is a number; the readable name is the company preset with that sortOrder.
const companyPresets = ref<{ id: number; name: string; sortOrder: number }[]>([])
function baseLabel(base: number | null | undefined): string {
  if (base == null) return '—'
  return companyPresets.value.find(p => p.sortOrder === base)?.name ?? `Base ${base}`
}

onMounted(async () => {
  cfOptions.init()
  try {
    companyPresets.value = await api.get<any[]>('/companypresets')
  } catch { /* falls back to "Base N" */ }
})

// Any filter change re-narrows the remaining columns.
watch([extraParams, () => dlp.search.value, () => dlp.status.value], () => cfOptions.refreshDebounced(), { deep: true })

function formatPrice(v: number | null | undefined) {
  if (v == null) return '0.00'
  return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

const filteredQuotes = computed(() => {
  if (selectedQuotes.value.length === 0) return availableQuotes.value
  const targetCustomer = selectedQuotes.value[0].customerName
  return availableQuotes.value.filter(q => q.customerName === targetCustomer)
})

const headers = [
  { title: 'Sales Order #', key: 'invoiceNumber' },
  { title: 'Customer', key: 'customerCode' },
  { title: 'Base', key: 'customerBase', width: '110px' },
  { title: 'Subject', key: 'subject' },
  { title: 'PO Date', key: 'customerPODate' },
  { title: 'Created At', key: 'createdAt' },
  { title: 'Deadline', key: 'deadlineDate' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]

async function fetchQuotes(search: string) {
  loadingQuotes.value = true
  try {
    const result = await api.get<any>('/quotes', {
      query: {
        status: ['Sent', 'Accepted'],
        pageSize: 5000,
        page: 1,
        ...(search ? { search } : {}),
      },
    })

    availableQuotes.value = (result.items || [])
      .filter((q: any) => !search ||
        q.quoteNumber?.toLowerCase().includes(search.toLowerCase()) ||
        q.customerName?.toLowerCase().includes(search.toLowerCase()) ||
        q.customerCode?.toLowerCase().includes(search.toLowerCase())
      )
  } catch (e) {
    console.error(e)
  } finally {
    loadingQuotes.value = false
  }
}

watch(showCreateDialog, (val) => {
  if (val) fetchQuotes('')
})

function proceedToCreate() {
  if (selectedQuotes.value.length > 0) {
    const primaryId = selectedQuotes.value[0].id
    const additionalIds = selectedQuotes.value.slice(1).map(q => q.id).join(',')
    router.push({
      path: `/quotes/${primaryId}/create-invoice`,
      query: additionalIds ? { additionalIds } : {}
    })
  }
}
</script>
