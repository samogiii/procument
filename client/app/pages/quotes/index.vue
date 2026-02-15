<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <h1 class="text-h5 font-weight-bold">Quotes</h1>
      <v-btn color="primary" prepend-icon="mdi-plus">New Quote</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex gap-3 mb-4">
          <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search..." single-line hide-details class="flex-grow-1" />
          <v-select v-model="statusFilter" :items="['All', 'Draft', 'Sent', 'Accepted', 'Rejected']" label="Status" hide-details style="max-width: 160px" />
        </div>
        <v-data-table-server
          :headers="headers"
          :items="items"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="10"
          @update:options="loadItems"
        >
          <template #item.status="{ item }">
            <v-chip :color="statusColor(item.status)" size="small">{{ item.status }}</v-chip>
          </template>
          <template #item.totalAmount="{ item }">
            ${{ item.totalAmount?.toLocaleString() || '0' }}
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`/quotes/${item.id}`" />
          </template>
        </v-data-table-server>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const search = ref('')
const statusFilter = ref('All')
const loading = ref(false)
const items = ref<any[]>([])
const totalItems = ref(0)

const headers = [
  { title: 'Quote #', key: 'quoteNumber' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]

function statusColor(status: string) {
  const map: Record<string, string> = { Draft: 'grey', Sent: 'info', Accepted: 'success', Rejected: 'error' }
  return map[status] || 'grey'
}

async function loadItems(options: any) {
  loading.value = true
  try {
    const res = await api.get<any>(`/quotes?page=${options.page}&pageSize=${options.itemsPerPage}`)
    items.value = res.items || []
    totalItems.value = res.totalCount || 0
  } catch {}
  finally { loading.value = false }
}
</script>
