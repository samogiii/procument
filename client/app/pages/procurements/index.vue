<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Procurements</h1>
        <p class="text-caption text-medium-emphasis">Snapshot editing layer before Purchase Orders.</p>
      </div>
      <v-spacer />
      <div class="d-flex flex-wrap gap-2">
        <v-btn
          v-if="isAdmin"
          variant="tonal"
          color="info"
          prepend-icon="mdi-account-cog-outline"
          @click="showManageAssignments = true"
        >
          Manage Assignments
        </v-btn>
        <v-btn
          v-if="isAdmin"
          variant="tonal"
          color="primary"
          prepend-icon="mdi-shield-account-outline"
          @click="showAssignDialog = true"
        >
          Assign Users
        </v-btn>
      </div>
    </div>

    <v-card class="glass-card">
      <v-data-table-server
        v-model:items-per-page="pageSize"
        v-model:page="page"
        :headers="headers"
        :items="items"
        :items-length="totalCount"
        :loading="loading"
        density="comfortable"
        hover
        @update:options="loadProcurements"
      >
        <!-- # Index -->
        <template #item.index="{ index }">
          <span class="text-caption text-medium-emphasis">{{ (page - 1) * pageSize + index + 1 }}</span>
        </template>

        <!-- Procurement Number -->
        <template #item.procurementNumber="{ item }">
          <NuxtLink :to="`/procurements/${item.id}`" class="text-decoration-none font-weight-bold cell-pn">
            {{ item.procurementNumber }}
          </NuxtLink>
        </template>

        <!-- Status -->
        <template #item.status="{ item }">
          <v-chip :color="statusColor(item.status)" size="x-small" variant="flat" class="text-uppercase font-weight-bold">
            {{ item.status }}
          </v-chip>
        </template>

        <!-- Assigned Users -->
        <template #item.assignedUsers="{ item }">
          <div v-if="isAdmin" class="d-flex flex-wrap gap-1 align-center">
            <template v-if="item._assignedUsers?.length">
              <v-chip
                v-for="u in item._assignedUsers.slice(0, 3)"
                :key="u.userId"
                size="x-small"
                variant="tonal"
                color="primary"
                class="px-1"
              >
                {{ u.userName }}
              </v-chip>
              <v-chip v-if="item._assignedUsers.length > 3" size="x-small" variant="text">
                +{{ item._assignedUsers.length - 3 }}
              </v-chip>
            </template>
            <span v-else class="text-caption text-medium-emphasis">—</span>
          </div>
        </template>

        <!-- Created -->
        <template #item.createdAt="{ item }">
          <span class="text-caption">{{ new Date(item.createdAt).toLocaleDateString() }}</span>
        </template>

        <!-- Actions -->
        <template #item.actions="{ item }">
          <v-btn icon="mdi-chevron-right" variant="text" size="small" :to="`/procurements/${item.id}`" />
        </template>
      </v-data-table-server>
    </v-card>

    <BulkPermissionManager
      v-model="showAssignDialog"
      entity-name="Procurement"
    />

    <ProcurementAssignmentManager
      v-model="showManageAssignments"
    />
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const items = ref<any[]>([])
const loading = ref(false)
const showAssignDialog = ref(false)
const showManageAssignments = ref(false)

const headers = computed(() => {
  const h = [
    { title: '#', key: 'index', sortable: false, width: '50px' },
    { title: 'Procurement #', key: 'procurementNumber' },
    { title: 'Invoice #', key: 'invoiceNumber' },
    { title: 'Customer', key: 'customerName' },
    { title: 'Items', key: 'itemCount', align: 'center' as const },
    { title: 'Status', key: 'status', align: 'center' as const },
  ]
  if (isAdmin.value) {
    h.push({ title: 'Assigned Users', key: 'assignedUsers', sortable: false })
  }
  h.push({ title: 'Created', key: 'createdAt' })
  h.push({ title: '', key: 'actions', sortable: false, align: 'end' as const })
  return h
})

function statusColor(status: string) {
  const map: Record<string, string> = {
    Open: 'grey',
    Sourcing: 'info',
    InProgress: 'warning',
    Finalized: 'success',
    Cancelled: 'error'
  }
  return map[status] || 'grey'
}

async function loadProcurements() {
  loading.value = true
  try {
    const res = await api.get<any>(`/procurements?page=${page.value}&pageSize=${pageSize.value}`)
    items.value = res.items || []
    totalCount.value = res.totalCount || 0
    
    // Load assigned users for each row (if admin)
    if (isAdmin.value) {
      items.value.forEach(item => loadRowPermissions(item))
    }
  } catch (e) {
    console.error('[Procurements] Load failed', e)
  } finally {
    loading.value = false
  }
}

async function loadRowPermissions(item: any) {
  try {
    // pattern from purchase-orders/index.vue
    item._assignedUsers = await api.get<any[]>(`/permissions/Procurement/${item.id}`)
  } catch {
    item._assignedUsers = []
  }
}

onMounted(() => {
  loadProcurements()
})
</script>

<style scoped>
.cell-pn {
  font-family: 'Roboto Mono', monospace;
  color: rgb(var(--v-theme-primary));
}
</style>
