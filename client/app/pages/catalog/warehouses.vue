<template>
  <div>
    <div class="d-flex align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Warehouses</h1>
        <p class="text-caption text-medium-emphasis mt-1">Manage warehouses and their assigned Inventory users</p>
      </div>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">Add Warehouse</v-btn>
    </div>

    <!-- Table -->
    <v-card>
      <v-data-table
        :headers="headers"
        :items="warehouses"
        :loading="loading"
        item-value="id"
        show-expand
        v-model:expanded="expanded"
        class="elevation-0"
        @update:expanded="onExpandedChange"
      >
        <template #item.type="{ item }">
          <v-chip
            :color="item.type === 'OurWarehouse' ? 'primary' : 'secondary'"
            size="x-small"
            variant="tonal"
          >
            {{ item.type === 'OurWarehouse' ? 'Our Warehouse' : 'Forwarded' }}
          </v-chip>
        </template>

        <template #item.isActive="{ item }">
          <v-icon :icon="item.isActive ? 'mdi-check-circle' : 'mdi-close-circle'" :color="item.isActive ? 'success' : 'error'" size="18" />
        </template>

        <template #item.actions="{ item }">
          <div class="d-flex gap-1 justify-end">
            <v-btn icon="mdi-pencil" size="x-small" variant="tonal" color="primary" @click="openEdit(item)" />
            <v-btn icon="mdi-delete" size="x-small" variant="tonal" color="error" @click="confirmDelete(item)" />
          </div>
        </template>

        <!-- Expanded row: Assigned Users -->
        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="pa-4 bg-surface-variant">
              <div class="d-flex align-center gap-2 mb-3">
                <v-icon icon="mdi-account-group" size="18" color="primary" />
                <span class="text-subtitle-2 font-weight-bold">Assigned Inventory Users</span>
                <v-btn
                  size="x-small"
                  variant="tonal"
                  color="primary"
                  prepend-icon="mdi-plus"
                  class="ml-2"
                  @click="openAssignUser(item)"
                >
                  Assign User
                </v-btn>
              </div>
              <v-progress-circular v-if="usersLoading[item.id]" indeterminate size="20" class="ml-2" />
              <div v-else-if="!warehouseUsers[item.id]?.length" class="text-caption text-medium-emphasis">
                No users assigned yet.
              </div>
              <div v-else class="d-flex flex-wrap gap-2">
                <v-chip
                  v-for="user in warehouseUsers[item.id]"
                  :key="user.id"
                  closable
                  size="small"
                  @click:close="removeUser(item.id, user.id)"
                >
                  {{ user.name }}
                </v-chip>
              </div>
            </td>
          </tr>
        </template>
      </v-data-table>
    </v-card>

    <!-- Create / Edit Dialog -->
    <v-dialog v-model="dialog" max-width="540" persistent>
      <v-card>
        <v-card-title class="d-flex align-center text-h6 pa-4 pb-2">
          <v-icon :icon="editingId ? 'mdi-pencil' : 'mdi-plus'" class="mr-2" color="primary" />
          {{ editingId ? 'Edit Warehouse' : 'New Warehouse' }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.name" label="System Name" :rules="[v => !!v || 'Required']" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.displayName" label="Display Name (on PDF)" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-select
                v-model="form.type"
                :items="[{ title: 'Our Warehouse', value: 'OurWarehouse' }, { title: 'Forwarded', value: 'Forwarded' }]"
                label="Type"
                variant="outlined"
                density="compact"
              />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.address" label="Bill To Address" variant="outlined" density="compact" rows="2" hide-details auto-grow />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.shipToAddress" label="Ship To Address" placeholder="Leave empty to use Bill To Address" variant="outlined" density="compact" rows="2" hide-details auto-grow />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.phone" label="Phone" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.email" label="Email" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="form.fedexAccount" label="FedEx Account" variant="outlined" density="compact" prepend-inner-icon="mdi-truck-fast" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" @click="save">
            {{ editingId ? 'Save' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Assign User Dialog -->
    <v-dialog v-model="assignDialog" max-width="400">
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">Assign Inventory User</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-autocomplete
            v-model="selectedUserId"
            :items="inventoryUsers"
            item-title="name"
            item-value="id"
            label="Select User"
            variant="outlined"
            density="compact"
            clearable
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="assignDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="assigning" :disabled="!selectedUserId" @click="assignUser">
            Assign
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirm -->
    <v-dialog v-model="deleteDialog" max-width="380">
      <v-card>
        <v-card-title class="text-h6 pa-4">Delete Warehouse?</v-card-title>
        <v-card-text class="pa-4 pt-0 text-body-2">
          Are you sure you want to delete <strong>{{ deletingItem?.name }}</strong>? This action cannot be undone.
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="deleteWarehouse">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snack" :color="snackColor" timeout="3000" location="top right">{{ snackMsg }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()

definePageMeta({ layout: 'default' })

const headers = [
  { title: 'Name', key: 'name', sortable: true },
  { title: 'Display Name', key: 'displayName', sortable: true },
  { title: 'Type', key: 'type', sortable: true },
  { title: 'Address', key: 'address', sortable: false },
  { title: 'Phone', key: 'phone', sortable: false },
  { title: 'Email', key: 'email', sortable: false },
  { title: 'Active', key: 'isActive', sortable: true },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
  { title: '', key: 'data-table-expand' },
]

const warehouses = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const assigning = ref(false)
const dialog = ref(false)
const deleteDialog = ref(false)
const assignDialog = ref(false)
const editingId = ref<number | null>(null)
const deletingItem = ref<any>(null)
const assigningWarehouseId = ref<number | null>(null)
const selectedUserId = ref<number | null>(null)
const inventoryUsers = ref<any[]>([])
const expanded = ref<any[]>([])
const warehouseUsers = ref<Record<number, any[]>>({})
const usersLoading = ref<Record<number, boolean>>({})

const form = reactive({
  name: '',
  displayName: '',
  type: 'OurWarehouse',
  address: '',
  shipToAddress: '',
  phone: '',
  email: '',
  fedexAccount: '',
})

const snack = ref(false)
const snackMsg = ref('')
const snackColor = ref('success')

function notify(msg: string, color = 'success') {
  snackMsg.value = msg
  snackColor.value = color
  snack.value = true
}

async function loadWarehouses() {
  loading.value = true
  try {
    warehouses.value = await api.get('/warehouses')
  } finally {
    loading.value = false
  }
}

async function loadWarehouseUsers(warehouseId: number) {
  usersLoading.value[warehouseId] = true
  try {
    warehouseUsers.value[warehouseId] = await api.get(`/warehouses/${warehouseId}/users`)
  } finally {
    usersLoading.value[warehouseId] = false
  }
}

async function loadInventoryUsers() {
  try {
    const all = await api.get('/users')
    inventoryUsers.value = all.filter((u: any) => u.role === 'Inventory')
  } catch {
    inventoryUsers.value = []
  }
}

function onExpandedChange(newExpanded: any[]) {
  newExpanded.forEach(id => {
    if (!warehouseUsers.value[id] && !usersLoading.value[id]) {
      loadWarehouseUsers(id)
    }
  })
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { name: '', displayName: '', type: 'OurWarehouse', address: '', shipToAddress: '', phone: '', email: '', fedexAccount: '' })
  dialog.value = true
}

function openEdit(item: any) {
  editingId.value = item.id
  Object.assign(form, {
    name: item.name,
    displayName: item.displayName ?? '',
    type: item.type,
    address: item.address ?? '',
    shipToAddress: item.shipToAddress ?? '',
    phone: item.phone ?? '',
    email: item.email ?? '',
    fedexAccount: item.fedexAccount ?? '',
  })
  dialog.value = true
}

async function save() {
  if (!form.name.trim()) return
  saving.value = true
  try {
    if (editingId.value) {
      await api.put(`/warehouses/${editingId.value}`, form)
      notify('Warehouse updated')
    } else {
      await api.post('/warehouses', form)
      notify('Warehouse created')
    }
    dialog.value = false
    await loadWarehouses()
  } catch {
    notify('Failed to save', 'error')
  } finally {
    saving.value = false
  }
}

function confirmDelete(item: any) {
  deletingItem.value = item
  deleteDialog.value = true
}

async function deleteWarehouse() {
  if (!deletingItem.value) return
  deleting.value = true
  try {
    await api.delete(`/warehouses/${deletingItem.value.id}`)
    notify('Warehouse deleted')
    deleteDialog.value = false
    await loadWarehouses()
  } catch {
    notify('Failed to delete', 'error')
  } finally {
    deleting.value = false
  }
}

async function openAssignUser(item: any) {
  assigningWarehouseId.value = item.id
  selectedUserId.value = null
  await loadInventoryUsers()
  assignDialog.value = true
}

async function assignUser() {
  if (!assigningWarehouseId.value || !selectedUserId.value) return
  assigning.value = true
  try {
    await api.post(`/warehouses/${assigningWarehouseId.value}/users/${selectedUserId.value}`, {})
    notify('User assigned')
    assignDialog.value = false
    await loadWarehouseUsers(assigningWarehouseId.value)
  } catch {
    notify('Failed to assign user', 'error')
  } finally {
    assigning.value = false
  }
}

async function removeUser(warehouseId: number, userId: number) {
  try {
    await api.delete(`/warehouses/${warehouseId}/users/${userId}`)
    notify('User removed')
    await loadWarehouseUsers(warehouseId)
  } catch {
    notify('Failed to remove user', 'error')
  }
}

onMounted(loadWarehouses)
</script>
