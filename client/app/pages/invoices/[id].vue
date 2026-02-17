<template>
  <div>
    <div class="d-flex align-center mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/invoices" class="mr-2" />
      <h1 class="text-h5 font-weight-bold">Invoice {{ invoice.invoiceNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <StatusChip :status="invoice.status ?? '—'" size="default" />
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-outline" color="primary" label="Customer" :value="invoice.customerName" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-currency-usd" color="success" label="Total">
          ${{ invoice.totalAmount?.toLocaleString() || '0' }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar-clock" color="warning" label="Due Date"
          :value="invoice.dueDate ? new Date(invoice.dueDate).toLocaleDateString() : undefined"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar-check" color="info" label="Paid Date"
          :value="invoice.paidDate ? new Date(invoice.paidDate).toLocaleDateString() : 'Unpaid'"
        />
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

const itemHeaders = [
  { title: 'Qty', key: 'qty' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total', key: 'totalPrice' },
]

onMounted(async () => {
  try { invoice.value = await api.get(`/invoices/${route.params.id}`) } catch {}
})
</script>
