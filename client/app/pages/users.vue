<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <h1 class="text-h5 font-weight-bold">User Management</h1>
      <v-btn color="primary" prepend-icon="mdi-account-plus" @click="showCreate = true">
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
            <v-chip :color="item.role === 'Admin' ? 'primary' : 'secondary'" size="small">{{ item.role }}</v-chip>
          </template>
          <template #item.isActive="{ item }">
            <v-chip :color="item.isActive ? 'success' : 'grey'" size="small">{{ item.isActive ? 'Active' : 'Inactive' }}</v-chip>
          </template>
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>
          <template #item.actions="{ item }">
            <v-btn
              :icon="item.isActive ? 'mdi-account-off' : 'mdi-account-check'"
              variant="text"
              size="small"
              :color="item.isActive ? 'error' : 'success'"
              @click="toggleActive(item.id)"
            />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Create User Dialog -->
    <v-dialog v-model="showCreate" max-width="500">
      <v-card class="glass-card">
        <v-card-title>Create New User</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="Full Name" class="mb-2" />
          <v-text-field v-model="form.email" label="Email" type="email" class="mb-2" />
          <v-text-field v-model="form.password" label="Password" type="password" class="mb-2" />
          <v-select v-model="form.role" :items="['Expert', 'Admin']" label="Role" />
          <v-alert v-if="createError" type="error" density="compact" class="mt-2">{{ createError }}</v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showCreate = false">Cancel</v-btn>
          <v-btn color="primary" :loading="creating" @click="createUser">Create</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()

// Guard: only admins
if (!authStore.isAdmin) {
  navigateTo('/dashboard')
}

const users = ref<any[]>([])
const loading = ref(false)
const showCreate = ref(false)
const creating = ref(false)
const createError = ref('')
const search = ref('')
const form = ref({ name: '', email: '', password: '', role: 'Expert' })

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Email', key: 'email' },
  { title: 'Role', key: 'role' },
  { title: 'Status', key: 'isActive' },
  { title: 'Created', key: 'createdAt' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]

async function loadUsers() {
  loading.value = true
  try {
    users.value = await api.get<any[]>('/users')
  } catch {}
  finally { loading.value = false }
}

async function createUser() {
  creating.value = true
  createError.value = ''
  try {
    await api.post('/users', form.value)
    showCreate.value = false
    form.value = { name: '', email: '', password: '', role: 'Expert' }
    await loadUsers()
  } catch (e: any) {
    createError.value = e?.data?.message || 'Failed to create user'
  } finally {
    creating.value = false
  }
}

async function toggleActive(id: number) {
  try {
    await api.patch(`/users/${id}/toggle-active`)
    await loadUsers()
  } catch {}
}

onMounted(loadUsers)
</script>
