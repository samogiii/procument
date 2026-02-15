<template>
  <div>
    <div class="d-flex align-center mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/purchase-orders" class="mr-2" />
      <h1 class="text-h5 font-weight-bold">PO {{ po.poNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <v-chip :color="statusColor" class="ml-2">{{ po.status || '—' }}</v-chip>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Supplier</p>
          <p class="text-body-1 font-weight-medium">{{ po.supplierName || '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Total Amount</p>
          <p class="text-body-1 font-weight-medium">${{ po.totalAmount?.toLocaleString() || '0' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Linked RFQ</p>
          <p class="text-body-1 font-weight-medium">{{ po.rfqName || '—' }}</p>
        </v-card>
      </v-col>
    </v-row>

    <v-card class="glass-card">
      <v-card-title>Line Items</v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="po.items || []" density="comfortable" />
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const po = ref<any>({})

const statusColor = computed(() => {
  const map: Record<string, string> = { Draft: 'grey', Sent: 'info', Received: 'success', Cancelled: 'error' }
  return map[po.value.status] || 'grey'
})

const itemHeaders = [
  { title: 'Part', key: 'partNumberName' },
  { title: 'Qty', key: 'qty' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total', key: 'totalPrice' },
  { title: 'Condition', key: 'condition' },
]

onMounted(async () => {
  try { po.value = await api.get(`/purchase-orders/${route.params.id}`) } catch {}
})
</script>
