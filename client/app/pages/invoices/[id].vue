<template>
  <div>
    <div class="d-flex align-center mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/invoices" class="mr-2" />
      <h1 class="text-h5 font-weight-bold">Invoice {{ invoice.invoiceNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <v-chip :color="statusColor" class="ml-2">{{ invoice.status || '—' }}</v-chip>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Customer</p>
          <p class="text-body-1 font-weight-medium">{{ invoice.customerName || '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Total</p>
          <p class="text-body-1 font-weight-medium">${{ invoice.totalAmount?.toLocaleString() || '0' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Due Date</p>
          <p class="text-body-1 font-weight-medium">{{ invoice.dueDate ? new Date(invoice.dueDate).toLocaleDateString() : '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Paid Date</p>
          <p class="text-body-1 font-weight-medium">{{ invoice.paidDate ? new Date(invoice.paidDate).toLocaleDateString() : 'Unpaid' }}</p>
        </v-card>
      </v-col>
    </v-row>

    <v-card class="glass-card">
      <v-card-title>Line Items</v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="invoice.items || []" density="comfortable" />
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const invoice = ref<any>({})

const statusColor = computed(() => {
  const map: Record<string, string> = { Pending: 'warning', Paid: 'success', Overdue: 'error' }
  return map[invoice.value.status] || 'grey'
})

const itemHeaders = [
  { title: 'Qty', key: 'qty' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total', key: 'totalPrice' },
]

onMounted(async () => {
  try { invoice.value = await api.get(`/invoices/${route.params.id}`) } catch {}
})
</script>
