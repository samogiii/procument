<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">Final Invoices</h1>
      <v-spacer />
    </div>

    <v-data-table
      :headers="headers"
      :items="invoices"
      :loading="loading"
      class="glass-card"
      density="comfortable"
      hover
      @click:row="(_: any, { item }: any) => navigateTo(`/final-invoices/${item.id}`)"
    >
      <template #item.status="{ item }">
        <v-chip :color="statusColor(item.status)" size="small">{{ item.status }}</v-chip>
      </template>
      <template #item.totalAmount="{ item }">
        ${{ item.totalAmount?.toLocaleString(undefined, { minimumFractionDigits: 2 }) }}
      </template>
      <template #item.createdAt="{ item }">
        {{ new Date(item.createdAt).toLocaleDateString() }}
      </template>
    </v-data-table>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const invoices = ref<any[]>([])
const loading = ref(false)

const headers = [
  { title: 'Invoice #', key: 'invoiceNumber', sortable: true },
  { title: 'Customer', key: 'customerName', sortable: true },
  { title: 'Proforma Ref', key: 'proformaInvoiceNumber', sortable: true },
  { title: 'Total', key: 'totalAmount', sortable: true },
  { title: 'Status', key: 'status', sortable: true },
  { title: 'Date', key: 'createdAt', sortable: true },
]

const { statusColor } = useStatusColor()

onMounted(async () => {
  loading.value = true
  try {
    invoices.value = await api.get<any[]>('/final-invoices')
  } catch {}
  finally { loading.value = false }
})
</script>
