<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Procurement Items</h1>
        <p class="text-caption text-medium-emphasis">All items across active procurements.</p>
      </div>
      <v-spacer />
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search part number..."
            single-line
            hide-details
            clearable
            class="flex-grow-1"
            style="min-width: 180px;"
          />
          <v-select
            v-model="statusFilter"
            :items="statusOptions"
            label="Item Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
          />
          <v-select
            v-model="procStatusFilter"
            :items="procStatusOptions"
            label="Proc Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
          />
          <v-autocomplete
            v-model="customerFilter"
            :items="customerOptions"
            label="Customer"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 240px;"
          />
          <v-autocomplete
            v-if="isAdmin"
            v-model="userFilter"
            :items="userOptions"
            item-title="name"
            item-value="id"
            label="Assigned User"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 240px;"
          />
          <v-btn
            v-if="hasActiveFilters"
            variant="tonal"
            color="error"
            size="small"
            prepend-icon="mdi-filter-off"
            class="align-self-center"
            @click="clearFilters"
          >
            Clear
          </v-btn>
        </div>

        <v-data-table
          :headers="headers"
          :items="filteredItems"
          :search="search"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
          item-value="id"
        >
          <!-- # -->
          <template #item.index="{ index }">
            <span class="text-caption text-medium-emphasis">{{ index + 1 }}</span>
          </template>

          <!-- Part Number -->
          <template #item.partNumberName="{ item }">
            <span class="font-weight-bold cell-pn">{{ item.partNumberName || '—' }}</span>
            <div v-if="item.partNumberDescription" class="text-caption text-medium-emphasis text-truncate" style="max-width: 200px;">
              {{ item.partNumberDescription }}
            </div>
          </template>

          <!-- Item Status -->
          <template #item.itemStatus="{ item }">
            <v-chip :color="itemStatusColor(item.itemStatus)" size="x-small" variant="flat" class="font-weight-bold text-uppercase">
              {{ item.itemStatus }}
            </v-chip>
          </template>

          <!-- Proc Status -->
          <template #item.procurementStatus="{ item }">
            <v-chip :color="procStatusColor(item.procurementStatus)" size="x-small" variant="tonal" class="font-weight-bold text-uppercase">
              {{ item.procurementStatus }}
            </v-chip>
          </template>

          <!-- Current Supplier -->
          <template #item.currentSupplierName="{ item }">
            <span :class="item.currentSupplierName ? '' : 'text-medium-emphasis'">
              {{ item.currentSupplierName || '—' }}
            </span>
          </template>

          <!-- Assigned Users -->
          <template #item.assignedUsers="{ item }">
            <div class="d-flex flex-wrap gap-1 align-center">
              <v-chip
                v-for="u in item.assignedUsers.slice(0, 3)"
                :key="u.id"
                size="x-small"
                variant="tonal"
                color="primary"
                class="px-1"
              >
                {{ u.userName }}
              </v-chip>
              <v-chip v-if="item.assignedUsers.length > 3" size="x-small" variant="text">
                +{{ item.assignedUsers.length - 3 }}
              </v-chip>
              <span v-if="!item.assignedUsers.length" class="text-caption text-medium-emphasis">—</span>
            </div>
          </template>

          <!-- Created -->
          <template #item.createdAt="{ item }">
            <span class="text-caption">{{ new Date(item.createdAt).toLocaleDateString() }}</span>
          </template>

          <!-- Actions -->
          <template #item.actions="{ item }">
            <div class="d-flex align-center gap-1">
              <v-btn
                v-if="isAdmin"
                icon="mdi-account-plus-outline"
                variant="text"
                size="x-small"
                color="primary"
                title="Assign user to this item"
                @click.stop="openAssign(item)"
              />
              <v-btn
                icon="mdi-open-in-new"
                variant="text"
                size="x-small"
                color="grey"
                :to="`/procurements/${item.procurementId}`"
                title="Open procurement"
              />
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Assign User Dialog -->
    <v-dialog v-model="showAssignDialog" max-width="400">
      <v-card>
        <v-card-title class="pa-4">
          <v-icon icon="mdi-account-plus-outline" color="primary" class="mr-2" />
          Assign User to Item
        </v-card-title>
        <v-card-text>
          <div class="text-caption text-medium-emphasis mb-3">
            Part: <strong>{{ assignTarget?.partNumberName }}</strong>
          </div>
          <v-autocomplete
            v-model="assignUserId"
            :items="users"
            item-title="name"
            item-value="id"
            label="Select User"
            variant="outlined"
            density="compact"
            autofocus
          />
        </v-card-text>
        <v-card-actions class="justify-end pa-4">
          <v-btn variant="text" @click="showAssignDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :disabled="!assignUserId" :loading="assigning" @click="doAssign">
            Assign
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const loading = ref(false)
const allItems = ref<any[]>([])
const users = ref<any[]>([])

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const showAssignDialog = ref(false)
const assignTarget = ref<any>(null)
const assignUserId = ref<number | null>(null)
const assigning = ref(false)

// ── Filters ──
const search = ref('')
const statusFilter = ref<string[]>([])
const procStatusFilter = ref<string[]>([])
const customerFilter = ref<string[]>([])
const userFilter = ref<number[]>([])

const hasActiveFilters = computed(() =>
  search.value.trim() !== '' ||
  statusFilter.value.length > 0 ||
  procStatusFilter.value.length > 0 ||
  customerFilter.value.length > 0 ||
  userFilter.value.length > 0
)

function clearFilters() {
  search.value = ''
  statusFilter.value = []
  procStatusFilter.value = []
  customerFilter.value = []
  userFilter.value = []
}

const statusOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach(i => { if (i.itemStatus) set.add(i.itemStatus) })
  return Array.from(set).sort()
})

const procStatusOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach(i => { if (i.procurementStatus) set.add(i.procurementStatus) })
  return Array.from(set).sort()
})

const customerOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach(i => { if (i.customerName) set.add(i.customerName) })
  return Array.from(set).sort()
})

const userOptions = computed(() => {
  const map = new Map<number, string>()
  allItems.value.forEach(i =>
    (i.assignedUsers || []).forEach((u: any) => {
      if (u.userId && u.userName) map.set(u.userId, u.userName)
    })
  )
  return Array.from(map, ([id, name]) => ({ id, name }))
})

const filteredItems = computed(() => {
  let result = allItems.value
  if (statusFilter.value.length)
    result = result.filter(i => statusFilter.value.includes(i.itemStatus))
  if (procStatusFilter.value.length)
    result = result.filter(i => procStatusFilter.value.includes(i.procurementStatus))
  if (customerFilter.value.length)
    result = result.filter(i => customerFilter.value.includes(i.customerName))
  if (userFilter.value.length)
    result = result.filter(i =>
      (i.assignedUsers || []).some((u: any) => userFilter.value.includes(u.userId))
    )
  if (search.value.trim()) {
    const q = search.value.trim().toLowerCase()
    result = result.filter(i =>
      (i.partNumberName || '').toLowerCase().includes(q) ||
      (i.partNumberDescription || '').toLowerCase().includes(q) ||
      (i.currentSupplierName || '').toLowerCase().includes(q)
    )
  }
  return result
})

const headers = computed(() => {
  const h: any[] = [
    { title: '#', key: 'index', sortable: false, width: '50px' },
    { title: 'Part Number', key: 'partNumberName' },
    { title: 'Qty', key: 'qty', width: '60px', align: 'center' },
    { title: 'Cond', key: 'condition', width: '70px' },
    { title: 'Item Status', key: 'itemStatus', width: '110px' },
    { title: 'Proc Status', key: 'procurementStatus', width: '110px' },
    { title: 'Customer', key: 'customerName', width: '100px' },
    { title: 'Supplier', key: 'currentSupplierName' },
  ]
  if (isAdmin.value) {
    h.push({ title: 'Assigned Users', key: 'assignedUsers', sortable: false })
  }
  h.push({ title: 'Created', key: 'createdAt', width: '100px' })
  h.push({ title: '', key: 'actions', sortable: false, align: 'end', width: '80px' })
  return h
})

function itemStatusColor(status: string) {
  const map: Record<string, string> = { Open: 'grey', Sourcing: 'info', Ready: 'success', Cancelled: 'error', Returned: 'warning' }
  return map[status] || 'grey'
}

function procStatusColor(status: string) {
  const map: Record<string, string> = { Open: 'grey', Sourcing: 'info', InProgress: 'warning', Finalized: 'success', Cancelled: 'error' }
  return map[status] || 'grey'
}

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Data Loading ──
async function loadData() {
  loading.value = true
  try {
    const data = await api.get<any[]>('/procurements/items')
    allItems.value = Array.isArray(data) ? data : []
  } catch (e) {
    console.error('[ProcurementItems] Load failed', e)
  } finally {
    loading.value = false
  }
}

async function loadUsers() {
  if (users.value.length) return
  try {
    const allUsers = await api.get<any[]>('/users')
    const allowed = ['GHS', 'SNP', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']
    users.value = allUsers.filter((u: any) => allowed.includes(u.name) || allowed.includes(u.username))
  } catch {
    users.value = []
  }
}

// ── Assign Dialog ──
function openAssign(item: any) {
  assignTarget.value = item
  assignUserId.value = null
  showAssignDialog.value = true
  loadUsers()
}

async function doAssign() {
  if (!assignUserId.value || !assignTarget.value) return
  assigning.value = true
  try {
    await api.post('/permissions/assign', {
      userId: assignUserId.value,
      entityName: 'Procurement',
      entityId: String(assignTarget.value.id),
      permission: 'Edit',
    })
    showSnack('User assigned')
    showAssignDialog.value = false
    // Update local state to reflect the new assignment
    const user = users.value.find(u => u.id === assignUserId.value)
    if (user) {
      const item = allItems.value.find(i => i.id === assignTarget.value.id)
      if (item) {
        item.assignedUsers = item.assignedUsers || []
        const alreadyAssigned = item.assignedUsers.some((u: any) => u.userId === user.id)
        if (!alreadyAssigned) {
          item.assignedUsers.push({ userId: user.id, userName: user.name })
        }
      }
    }
  } catch {
    showSnack('Assignment failed', 'error')
  } finally {
    assigning.value = false
  }
}

onMounted(loadData)
</script>

<style scoped>
.cell-pn {
  font-family: 'Roboto Mono', monospace;
  color: rgb(var(--v-theme-primary));
}
</style>
