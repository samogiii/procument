<template>
  <div>
    <div class="d-flex align-center mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/quotes" class="mr-2" />
      <h1 class="text-h5 font-weight-bold">Quote {{ quote.quoteNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <v-chip :color="statusColor" class="ml-2">{{ quote.status || '—' }}</v-chip>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Customer</p>
          <p class="text-body-1 font-weight-medium">{{ quote.customerName || '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Total Amount</p>
          <p class="text-body-1 font-weight-medium">${{ quote.totalAmount?.toLocaleString() || '0' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Valid Until</p>
          <p class="text-body-1 font-weight-medium">{{ quote.validUntil ? new Date(quote.validUntil).toLocaleDateString() : '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">RFQ</p>
          <p class="text-body-1 font-weight-medium">{{ quote.rfqName || '—' }}</p>
        </v-card>
      </v-col>
    </v-row>

    <v-card class="glass-card">
      <v-card-title>Line Items</v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="quote.items || []" density="comfortable" />
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const quote = ref<any>({})

const statusColor = computed(() => {
  const map: Record<string, string> = { Draft: 'grey', Sent: 'info', Accepted: 'success', Rejected: 'error' }
  return map[quote.value.status] || 'grey'
})

const itemHeaders = [
  { title: 'Part', key: 'partNumberName' },
  { title: 'Qty', key: 'qty' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total', key: 'totalPrice' },
  { title: 'Condition', key: 'condition' },
]

onMounted(async () => {
  try { quote.value = await api.get(`/quotes/${route.params.id}`) } catch {}
})
</script>
