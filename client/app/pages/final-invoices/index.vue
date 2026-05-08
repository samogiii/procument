<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">Final Invoices</h1>
      <v-spacer />
      <v-btn prepend-icon="mdi-plus" color="primary" @click="showAddDialog = true">Create Final Invoice</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
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
          <v-select
            v-model="customerFilter"
            :items="customerOptions"
            item-title="title"
            item-value="value"
            label="Customer"
            hide-details
            class="mx-2"
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
        </div>
    <v-data-table
      :headers="headers"
      :items="filteredInvoices"
      :search="search"
      :loading="loading"
      density="comfortable"
      :items-per-page="50"
      hover
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
    </v-data-table>
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
const invoices = ref<any[]>([])
const loading = ref(false)
const search = ref('')
const customerFilter = ref<string[]>([])
const dateFrom = ref<string | null>(null)
const dateTo = ref<string | null>(null)

const customerOptions = computed(() => {
  const map = new Map<string, string>()
  invoices.value.forEach((inv: any) => {
    if (inv.customerName && !map.has(inv.customerName))
      map.set(inv.customerName, inv.customerCode || '')
  })
  return Array.from(map.entries())
    .map(([name, code]) => ({ title: code || '—', value: name }))
    .sort((a, b) => a.title.localeCompare(b.title))
})

const filteredInvoices = computed(() => {
  let result = invoices.value
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
})

const headers = [
  { title: 'Invoice #', key: 'invoiceNumber', sortable: true },
  { title: 'Customer', key: 'customerCode', sortable: true },
  { title: 'Proforma Ref', key: 'proformaInvoiceNumber', sortable: true },
  { title: 'Total', key: 'totalAmount', sortable: true },
  { title: 'Status', key: 'status', sortable: true },
  { title: 'Date', key: 'createdAt', sortable: true },
]

const { statusColor } = useStatusColor()

onMounted(async () => {
  loading.value = true
  try {
    const accumulated: any[] = []
    let page = 1
    while (true) {
      const res = await api.get<any>(`/final-invoices?page=${page}&pageSize=200`)
      const batch: any[] = Array.isArray(res) ? res : (res.items ?? res.Items ?? [])
      const total: number = (!Array.isArray(res) && res != null) ? (res.totalCount ?? res.TotalCount ?? batch.length) : batch.length
      accumulated.push(...batch)
      if (batch.length < 200 || accumulated.length >= total) break
      page++
    }
    invoices.value = accumulated
  } catch {}
  finally { loading.value = false }
})

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
