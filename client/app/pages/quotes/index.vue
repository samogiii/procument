<style scoped>
.glass-card {
  background: rgba(30, 41, 59, 0.7) !important;
  border: 1px solid rgba(51, 65, 85, 0.5) !important;
  backdrop-filter: blur(10px);
}
:deep(.clickable-rows tbody tr) {
  cursor: pointer !important;
  transition: background-color 0.2s;
}
:deep(.clickable-rows tbody tr:hover) {
  background-color: rgba(255, 255, 255, 0.05) !important;
}
</style>
<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <h1 class="text-h5 font-weight-bold">Quotes</h1>
      <div>
        <v-btn
          v-if="isAdmin"
          prepend-icon="mdi-shield-account"
          variant="tonal"
          class="mr-2"
          @click="showBulkPerms = true"
        >
          Manage Permissions
        </v-btn>
      </div>
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
          hover
          @update:options="loadItems"
          @click:row="(e, { item }) => navigateTo(`/quotes/${item.id}`)"
          class="clickable-rows"
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

    <!-- Bulk Permission Manager -->
    <BulkPermissionManager v-model="showBulkPerms" entity-name="Quote" />
  </div>
  
</template>

<script setup lang="ts">

const api = useApi()
const authStore = useAuthStore()
const search = ref('')
const statusFilter = ref('All')
const loading = ref(false)
const items = ref<any[]>([])
const totalItems = ref(0)
const showBulkPerms = ref(false)

const isAdmin = computed(() => authStore.isAdmin)


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

<style scoped>
.glass-card {
  background: rgba(30, 41, 59, 0.7) !important;
  border: 1px solid rgba(51, 65, 85, 0.5) !important;
  backdrop-filter: blur(10px);
}

:deep(.clickable-rows tbody tr) {
  cursor: pointer !important;
  transition: background-color 0.2s;
}
:deep(.clickable-rows tbody tr:hover) {
  background-color: rgba(255, 255, 255, 0.05) !important;
}
</style>
