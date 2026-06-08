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
        v-model="customerSearch"
        label="Customer Code"
        prepend-inner-icon="mdi-domain"
        hide-details
        clearable
        style="min-width: 160px; max-width: 260px;"
      />
    </template>

    <!-- Column filter: Sales Order # -->
    <template #header.invoiceNumber="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="invoiceNumber"
        :label="column.title"
        :options="cfInvoiceNumberOptions"
        :selected="colFilter.selected['invoiceNumber'] || new Set()"
        :search="colFilter.search['invoiceNumber'] || ''"
        @toggle="(v) => colFilter.toggle('invoiceNumber', v)"
        @select-all="() => colFilter.selectAll('invoiceNumber', cfInvoiceNumberOptions)"
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
        :options="cfCustomerOptions"
        :selected="colFilter.selected['customerCode'] || new Set()"
        :search="colFilter.search['customerCode'] || ''"
        @toggle="(v) => colFilter.toggle('customerCode', v)"
        @select-all="() => colFilter.selectAll('customerCode', cfCustomerOptions)"
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
        :options="cfStatusOptions"
        :selected="colFilter.selected['status'] || new Set()"
        :search="colFilter.search['status'] || ''"
        @toggle="(v) => colFilter.toggle('status', v)"
        @select-all="() => colFilter.selectAll('status', cfStatusOptions)"
        @clear-all="() => colFilter.clearAll('status')"
        @update:search="(v) => colFilter.search['status'] = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <template #item.status="{ item }">
      <StatusChip :status="item.status" />
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

const customerSearch = ref('')

// ── Column filters ──
const colFilter = useColFilterPersisted('invoices')
const cfCustomerOptions = ref<string[]>([])
const cfStatusOptions = ref<string[]>([])
const cfInvoiceNumberOptions = ref<string[]>([])

const extraParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = {}
  if (customerSearch.value?.trim()) p.customer = customerSearch.value.trim()
  if (colFilter.isActive('customerCode')) p.customerCodes = colFilter.getSelected('customerCode')
  if (colFilter.isActive('status')) p.statuses = colFilter.getSelected('status')
  if (colFilter.isActive('invoiceNumber')) p.invoiceNumbers = colFilter.getSelected('invoiceNumber')
  return p
})

// Load all filter options from the dedicated endpoint (full DB, not current page only)
async function loadAllInvoiceFilterOptions() {
  try {
    const res = await api.get<any>('/invoices/filter-options')
    cfStatusOptions.value = (res.statuses || []).sort()
    cfCustomerOptions.value = [...new Set((res.customers || [])
      .map((c: any) => c.code || '-'))]
      .sort()
    cfInvoiceNumberOptions.value = (res.invoiceNumbers || []).sort()
  } catch {}
}

// Keep customFilter to satisfy DataListPage prop but stop using it for col filter options
function invoiceCustomFilter(items: any[]): any[] {
  return items
}

onMounted(() => {
  loadAllInvoiceFilterOptions()
})

// formatPrice helper
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
  { title: 'Subject', key: 'subject' },
  { title: 'Deadline', key: 'deadlineDate' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]

async function fetchQuotes(search: string) {
  loadingQuotes.value = true
  try {
    // Single call — backend accepts List<string> status so we pass both statuses at once.
    // The backend also applies the user's base/assigned-customer filter automatically.
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

// Initial fetch when dialog opens
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
