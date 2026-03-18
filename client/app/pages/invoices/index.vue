<template>
  <DataListPage
    title="Proforma Invoices"
    :headers="headers"
    api-url="/invoices"
    :status-options="['All', 'Pending', 'Paid', 'Overdue']"
    detail-route="/invoices"
    show-select
    v-model="selectedInvoices"
    :custom-filter="applyFilters"
    page-key="invoices"
  >
    <template #filters>
      <v-select
        v-model="customerFilter"
        :items="customerOptions"
        label="Customer"
        hide-details
        multiple
        chips
        closable-chips
        clearable
        style="min-width: 140px; max-width: 260px;"
      />
      <!-- <v-text-field
        v-model="dateFrom"
        label="From"
        type="date"
        hide-details
        clearable
        style="min-width: 130px; max-width: 160px;"
      />
      <v-text-field
        v-model="dateTo"
        label="To"
        type="date"
        hide-details
        clearable
        style="min-width: 130px; max-width: 160px;"
      /> -->
    </template>

    <template #item.status="{ item }">
      <StatusChip :status="item.status" />
    </template>

    <template #item.totalAmount="{ item }">
      ${{ formatPrice(item.totalAmount) }}
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
        Create Proforma Invoice
      </v-btn>
    </template>

    <v-dialog v-model="showCreateDialog" max-width="600">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center">
          Create Proforma Invoice
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="showCreateDialog = false" />
        </v-card-title>
        <v-card-text>
          <p class="mb-4 text-grey-lighten-1">Select a Quote to create a proforma invoice from.</p>
          
          <v-autocomplete
            v-model="selectedQuote"
            :items="availableQuotes"
            :loading="loadingQuotes"
            item-title="quoteNumber"
            item-value="id"
            label="Search Quote (Sent or Accepted)"
            placeholder="Type to search..."
            return-object
            variant="outlined"
            prepend-inner-icon="mdi-magnify"
            clearable
            no-filter
            @update:search="fetchQuotes"
          >
            <template #item="{ props, item }">
              <v-list-item v-bind="props" :subtitle="item.raw.customerName">
                <template #append>
                   <v-chip size="x-small" :color="statusColor(item.raw.status)" class="ml-2">{{ item.raw.status }}</v-chip>
                </template>
              </v-list-item>
            </template>
          </v-autocomplete>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showCreateDialog = false">Cancel</v-btn>
          <v-btn color="primary" :disabled="!selectedQuote" @click="proceedToCreate">
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
const selectedQuote = ref<any>(null)
const selectedInvoices = ref<number[]>([])

const customerFilter = ref<string[]>([])
const dateFrom = ref<string | null>(null)
const dateTo = ref<string | null>(null)

const customers = ref<any[]>([])

onMounted(async () => {
  try {
    const res = await api.get<any>('/invoices?pageSize=9999')
    const items = Array.isArray(res) ? res : (res.items || res.Items || [])
    const custSet = new Set<string>()
    items.forEach((inv: any) => { if (inv.customerName) custSet.add(inv.customerName) })
    customers.value = Array.from(custSet).sort()
  } catch {}
})

const customerOptions = computed(() => customers.value)

function applyFilters(items: any[]) {
  let result = items
  if (customerFilter.value?.length) {
    result = result.filter((item: any) => customerFilter.value.includes(item.customerName))
  }
  if (dateFrom.value) {
    const from = new Date(dateFrom.value).getTime()
    result = result.filter((item: any) => new Date(item.createdAt).getTime() >= from)
  }
  if (dateTo.value) {
    const to = new Date(dateTo.value).getTime() + 86400000
    result = result.filter((item: any) => new Date(item.createdAt).getTime() < to)
  }
  return result
}

const headers = [
  { title: 'Proforma Invoice #', key: 'invoiceNumber' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]

async function fetchQuotes(search: string) {
  loadingQuotes.value = true
  try {
    // Ideally we filter by search term AND status 'Sent' or 'Accepted'
    // For now, let's fetch recent Sent/Accepted quotes. 
    // Since we added status filter to getAll, we can use it.
    // However, our backend GetAllAsync takes a single status. We might need to fetch twice or update backend to accept list.
    // For simplicity, let's just fetch "Accepted" for now as prime candidate, or maybe allow user to toggle?
    // User request: "said for witch quote do you want create".
    // Let's fetch both if possible, or just fetch all and filter client side if the list is small?
    // Or better, let's fetch "Accepted" quotes as default.
    
    // Better approach: Backend support or separate calls. 
    // Let's call twice: Accepted and Sent.
    
    const [sent, accepted] = await Promise.all([
      api.get<any>('/quotes', { query: { status: 'Sent', pageSize: 50 }, params: { status: 'Sent', pageSize: 50 } }), // Try both or check nuxt $fetch. $fetch uses 'query' or 'params'.
      api.get<any>('/quotes', { query: { status: 'Accepted', pageSize: 50 } })
    ])
    
    availableQuotes.value = [...(sent.items || []), ...(accepted.items || [])]
      .filter(q => !search || q.quoteNumber.toLowerCase().includes(search.toLowerCase()) || q.customerName.toLowerCase().includes(search.toLowerCase()))
      
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
  if (selectedQuote.value) {
    router.push(`/quotes/${selectedQuote.value.id}/create-invoice`)
  }
}
</script>
