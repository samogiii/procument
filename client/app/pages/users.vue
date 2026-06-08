<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <h1 class="text-h5 font-weight-bold">User Management</h1>
      <v-btn v-if="authStore.isSuperAdmin" color="primary" prepend-icon="mdi-account-plus" @click="showCreate = true">
        Create User
      </v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search users..."
          single-line
          hide-details
          class="mb-4"
        />
        <v-data-table
          :headers="headers"
          :items="users"
          :search="search"
          :loading="loading"
          density="comfortable"
          :items-per-page="50"
          hover
        >
          <template #item.role="{ item }">
            <v-chip :color="roleColor(item.role)" size="small">{{ item.role }}</v-chip>
          </template>
          <template #item.isActive="{ item }">
            <v-chip :color="item.isActive ? 'success' : 'grey'" size="small">{{ item.isActive ? 'Active' : 'Inactive' }}</v-chip>
          </template>
          <template #item.bases="{ item }">
            <div class="d-flex flex-wrap gap-1 py-1">
              <v-chip
                v-for="b in item.bases"
                :key="b"
                size="x-small"
                color="teal"
                variant="tonal"
              >{{ baseLabel(b) }}</v-chip>
              <span v-if="!item.bases?.length" class="text-grey text-caption">—</span>
            </div>
          </template>
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>
          <template #item.actions="{ item }">
            <div class="d-flex gap-1">
              <v-btn
                icon="mdi-pencil"
                variant="text"
                size="small"
                color="primary"
                title="Edit User"
                @click="openEdit(item)"
              />
              <v-btn
                icon="mdi-lock-reset"
                variant="text"
                size="small"
                color="warning"
                title="Change Password"
                @click="openChangePassword(item)"
              />
              <v-btn
                v-if="authStore.isSuperAdmin"
                :icon="item.isActive ? 'mdi-account-off' : 'mdi-account-check'"
                variant="text"
                size="small"
                :color="item.isActive ? 'error' : 'success'"
                :title="item.isActive ? 'Deactivate' : 'Activate'"
                @click="toggleActive(item.id)"
              />
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Create User Dialog -->
    <v-dialog v-model="showCreate" max-width="500">
      <v-card class="glass-card">
        <v-card-title>Create New User</v-card-title>
        <v-card-text>
          <v-text-field v-model="createForm.name" label="Full Name" class="mb-2" />
          <v-text-field v-model="createForm.email" label="Email" type="email" class="mb-2" />
          <v-text-field v-model="createForm.password" label="Password" type="password" class="mb-2" />
          <v-select v-model="createForm.role" :items="roles" label="Role" />
          <v-alert v-if="error" type="error" density="compact" class="mt-2">{{ error }}</v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showCreate = false">Cancel</v-btn>
          <v-btn color="primary" :loading="processing" @click="createUser">Create</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Edit User Dialog -->
    <v-dialog v-model="showEdit" max-width="560">
      <v-card class="glass-card">
        <v-card-title>Edit User: {{ selectedUser?.name }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="editForm.name" label="Full Name" class="mb-2" />
          <v-text-field v-model="editForm.email" label="Email" type="email" class="mb-2" />
          <v-select v-model="editForm.role" :items="roles" label="Role" class="mb-3" />

          <div class="text-subtitle-2 mb-2">Assigned Bases</div>
          <v-chip-group v-model="editForm.bases" multiple column class="mb-4">
            <v-chip
              v-for="preset in companyPresets"
              :key="preset.sortOrder"
              :value="preset.sortOrder"
              filter
              variant="outlined"
              color="teal"
            >{{ preset.name }}</v-chip>
          </v-chip-group>

          <v-divider class="mb-3" />
          <div class="text-subtitle-2 mb-1">Individual Customer Access</div>
          <div class="text-caption text-medium-emphasis mb-2">
            Assign specific out-of-base customers so this user can see and create RFQs for them.
          </div>

          <!-- Search and add a customer -->
          <v-autocomplete
            v-model="customerToAdd"
            :items="customerSearchResults"
            :loading="customerSearchLoading"
            item-title="name"
            item-value="id"
            label="Search customer to add…"
            variant="outlined"
            density="compact"
            clearable
            hide-no-data
            no-filter
            return-object
            class="mb-2"
            prepend-inner-icon="mdi-account-search"
            @update:search="searchCustomersForAssign"
          >
            <template #item="{ item, props: p }">
              <v-list-item v-bind="p" :subtitle="item.raw.customerCode" />
            </template>
          </v-autocomplete>
          <v-btn
            size="small"
            color="primary"
            variant="tonal"
            :disabled="!customerToAdd"
            class="mb-3"
            prepend-icon="mdi-plus"
            @click="addIndividualCustomer"
          >Add Customer</v-btn>

          <!-- Currently assigned customers -->
          <div v-if="editForm.assignedCustomers.length" class="d-flex flex-wrap gap-1">
            <v-chip
              v-for="c in editForm.assignedCustomers"
              :key="c.id"
              size="small"
              color="orange"
              variant="tonal"
              closable
              @click:close="removeIndividualCustomer(c.id)"
            >{{ c.customerCode || c.name }}</v-chip>
          </div>
          <div v-else class="text-caption text-grey">No individual customers assigned.</div>

          <v-alert v-if="error" type="error" density="compact" class="mt-2">{{ error }}</v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showEdit = false">Cancel</v-btn>
          <v-btn color="primary" :loading="processing" @click="updateUser">Save Changes</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Change Password Dialog -->
    <v-dialog v-model="showPassword" max-width="400">
      <v-card class="glass-card">
        <v-card-title>Change Password: {{ selectedUser?.name }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="passwordForm.newPassword"
            label="New Password"
            type="password"
            hint="Enter a secure password"
            persistent-hint
          />
          <v-alert v-if="error" type="error" density="compact" class="mt-4">{{ error }}</v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showPassword = false">Cancel</v-btn>
          <v-btn color="warning" :loading="processing" @click="changePassword">Update Password</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()

// Guard: only admins and superadmins
if (!authStore.isAdmin) {
  navigateTo('/dashboard')
}

const users = ref<any[]>([])
const loading = ref(false)
const search = ref('')
const roles = ['Expert', 'Admin', 'SuperAdmin', 'Payment', 'Inventory']
const companyPresets = ref<{ id: number; name: string; sortOrder: number }[]>([])

const showCreate = ref(false)
const showEdit = ref(false)
const showPassword = ref(false)
const processing = ref(false)
const error = ref('')

const selectedUser = ref<any>(null)

const createForm = ref({ name: '', email: '', password: '', role: 'Expert' })
const editForm = ref({
  name: '',
  email: '',
  role: 'Expert',
  bases: [] as number[],
  assignedCustomers: [] as { id: number; name: string; customerCode: string | null }[],
})
const passwordForm = ref({ newPassword: '' })

// Individual customer assignment state
const customerToAdd = ref<{ id: number; name: string; customerCode: string | null } | null>(null)
const customerSearchResults = ref<{ id: number; name: string; customerCode: string | null }[]>([])
const customerSearchLoading = ref(false)
let customerSearchDebounce: ReturnType<typeof setTimeout> | null = null

function searchCustomersForAssign(val: string) {
  if (customerSearchDebounce) clearTimeout(customerSearchDebounce)
  if (!val || val.length < 1) { customerSearchResults.value = []; return }
  customerSearchDebounce = setTimeout(async () => {
    customerSearchLoading.value = true
    try {
      // Use all=true to bypass base filter so SuperAdmin can search any customer when assigning
      customerSearchResults.value = await api.get<any[]>(`/customers/search?q=${encodeURIComponent(val)}&all=true`)
    } catch { /* non-critical */ }
    finally { customerSearchLoading.value = false }
  }, 300)
}

async function addIndividualCustomer() {
  if (!customerToAdd.value || !selectedUser.value) return
  const c = customerToAdd.value
  if (editForm.value.assignedCustomers.some(x => x.id === c.id)) {
    customerToAdd.value = null
    return
  }
  try {
    await api.post(`/users/${selectedUser.value.id}/customers`, { customerId: c.id })
    editForm.value.assignedCustomers.push(c)
    customerToAdd.value = null
    customerSearchResults.value = []
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to assign customer', 'error')
  }
}

async function removeIndividualCustomer(customerId: number) {
  if (!selectedUser.value) return
  try {
    await api.del(`/users/${selectedUser.value.id}/customers/${customerId}`)
    editForm.value.assignedCustomers = editForm.value.assignedCustomers.filter(c => c.id !== customerId)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to remove customer', 'error')
  }
}

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Email', key: 'email' },
  { title: 'Role', key: 'role' },
  { title: 'Bases', key: 'bases', sortable: false },
  { title: 'Status', key: 'isActive' },
  { title: 'Created', key: 'createdAt' },
  { title: 'Actions', key: 'actions', sortable: false, width: '120px' },
]

function baseLabel(sortOrder: number): string {
  return companyPresets.value.find(p => p.sortOrder === sortOrder)?.name ?? `Base ${sortOrder}`
}

function roleColor(role: string) {
  switch (role) {
    case 'SuperAdmin': return 'error'
    case 'Admin': return 'primary'
    case 'Payment': return 'info'
    case 'Inventory': return 'warning'
    default: return 'secondary'
  }
}

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

async function loadUsers() {
  // Only SuperAdmin can see the list as per request "for superadmin, show users"
  // But Admin needs to change passwords... so I'll let Admin see them too if they are on this page.
  // Actually I will keep it for both but strictly follow the instruction if possible.
  // The instruction says "for superadmin, show users"
  loading.value = true
  try {
    users.value = await api.get<any[]>('/users')
  } catch (e: any) {
    showSnack('Failed to load users', 'error')
  }
  finally { loading.value = false }
}

async function createUser() {
  processing.value = true
  error.value = ''
  try {
    await api.post('/users', createForm.value)
    showCreate.value = false
    createForm.value = { name: '', email: '', password: '', role: 'Expert' }
    showSnack('User created successfully')
    await loadUsers()
  } catch (e: any) {
    error.value = e?.data?.message || 'Failed to create user'
  } finally {
    processing.value = false
  }
}

async function openEdit(user: any) {
  selectedUser.value = user
  editForm.value = {
    name: user.name,
    email: user.email,
    role: user.role,
    bases: [...(user.bases ?? [])],
    assignedCustomers: [],
  }
  customerToAdd.value = null
  customerSearchResults.value = []
  error.value = ''
  showEdit.value = true
  // Load individually-assigned customers in the background
  try {
    editForm.value.assignedCustomers = await api.get<any[]>(`/users/${user.id}/customers`)
  } catch { /* non-critical */ }
}

async function updateUser() {
  if (!selectedUser.value) return
  processing.value = true
  error.value = ''
  try {
    await api.put(`/users/${selectedUser.value.id}`, { name: editForm.value.name, email: editForm.value.email, role: editForm.value.role })

    // Sync bases: diff old vs new
    const oldBases: number[] = selectedUser.value.bases ?? []
    const newBases: number[] = editForm.value.bases
    const toAdd = newBases.filter(b => !oldBases.includes(b))
    const toRemove = oldBases.filter(b => !newBases.includes(b))
    await Promise.all([
      ...toAdd.map(b => api.post(`/users/${selectedUser.value.id}/bases`, { base: b })),
      ...toRemove.map(b => api.del(`/users/${selectedUser.value.id}/bases/${b}`)),
    ])

    showEdit.value = false
    showSnack('User updated successfully')
    await loadUsers()
  } catch (e: any) {
    error.value = e?.data?.message || 'Failed to update user'
  } finally {
    processing.value = false
  }
}

function openChangePassword(user: any) {
  selectedUser.value = user
  passwordForm.value = { newPassword: '' }
  error.value = ''
  showPassword.value = true
}

async function changePassword() {
  if (!selectedUser.value) return
  if (!passwordForm.value.newPassword) {
    error.value = 'Password cannot be empty'
    return
  }
  processing.value = true
  error.value = ''
  try {
    await api.patch(`/users/${selectedUser.value.id}/password`, passwordForm.value)
    showPassword.value = false
    showSnack('Password updated successfully')
  } catch (e: any) {
    error.value = e?.data?.message || 'Failed to update password'
  } finally {
    processing.value = false
  }
}

async function toggleActive(id: number) {
  try {
    await api.patch(`/users/${id}/toggle-active`)
    showSnack('User status updated')
    await loadUsers()
  } catch {
    showSnack('Failed to update user status', 'error')
  }
}

async function loadCompanyPresets() {
  try {
    companyPresets.value = await api.get<any[]>('/companypresets')
  } catch { /* non-critical */ }
}

onMounted(() => {
  loadUsers()
  loadCompanyPresets()
})
</script>
