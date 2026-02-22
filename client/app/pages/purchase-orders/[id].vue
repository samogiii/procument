<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/purchase-orders" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">PO {{ po.poNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <StatusChip :status="po.status ?? '—'" size="default" />
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="4">
        <StatCard icon="mdi-truck-delivery" color="primary" label="Supplier" :value="po.supplierName" />
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ po.totalAmount?.toLocaleString() || '0' }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-file-document-outline" color="info" label="Linked RFQ" :value="po.rfqName" />
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
