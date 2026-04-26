<template>
  <v-card class="glass-card">
    <v-card-title class="d-flex align-center justify-space-between">
      <span>Manage Permissions</span>
      <v-btn icon="mdi-refresh" variant="text" size="small" @click="loadPermissions" :loading="loading" />
    </v-card-title>
    
    <v-card-text>
      <!-- Add New Permission -->
      <div v-if="canManage" class="mb-4 pa-3  rounded border border-opacity-25">
        <p class="text-caption font-weight-bold mb-2">Assign New Permission</p>
        <div class="d-flex gap-2">
          <v-select
            v-model="newPerm.userId"
            :items="users"
            item-title="name"
            item-value="id"
            label="User"
            density="compact"
            hide-details
            class="flex-grow-1"
          />
          <v-select
            v-model="newPerm.permission"
            :items="['Edit', 'View']"
            label="Role"
            density="compact"
            hide-details
            style="max-width: 140px;"
          />
          <v-btn
            color="primary"
            icon="mdi-plus"
            size="small"
            class="mt-1"
            :loading="assigning"
            @click="assignPermission"
            :disabled="!newPerm.userId || !newPerm.permission"
          />
        </div>
      </div>

      <!-- List Permissions -->
      <v-table density="compact" class="bg-transparent">
        <thead>
          <tr>
            <th>User</th>
            <th>Role</th>
            <th class="text-right" v-if="canManage">Action</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="perm in permissions" :key="perm.id">
            <td>
              <div class="d-flex align-center gap-2">
                <v-avatar color="primary" size="24" variant="tonal">
                  <span class="text-caption">{{ getInitials(perm.user?.name) }}</span>
                </v-avatar>
                <span class="text-body-2">{{ perm.user?.name || 'Unknown' }}</span>
              </div>
            </td>
            <td>
              <v-chip
                size="x-small"
                :color="perm.permission === 'Edit' ? 'success' : 'info'"
                variant="tonal"
              >
                {{ perm.permission }}
              </v-chip>
            </td>
            <td class="text-right" v-if="canManage">
              <v-btn
                icon="mdi-delete"
                size="x-small"
                variant="text"
                color="error"
                @click="revokePermission(perm)"
                :loading="revoking === perm.id"
              />
            </td>
          </tr>
          <tr v-if="permissions.length === 0">
            <td colspan="3" class="text-center text-caption text-medium-emphasis py-4">
              No permissions assigned specifically.
            </td>
          </tr>
        </tbody>
      </v-table>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
const props = defineProps<{
  entityName: string
  entityId: string
}>()

const api = useApi()
const authStore = useAuthStore()

// State
const permissions = ref<any[]>([])
const users = ref<any[]>([])
const loading = ref(false)
const assigning = ref(false)
const revoking = ref<number | null>(null)
const newPerm = ref({ userId: null, permission: 'Edit' })

// Current user can manage if Admin
const canManage = computed(() => authStore.isAdmin)

onMounted(async () => {
  await Promise.all([
    loadPermissions(),
    loadUsers()
  ])
})

async function loadPermissions() {
  loading.value = true
  try {
    permissions.value = await api.get<any[]>(`/permissions/${props.entityName}/${props.entityId}`)
  } catch (e) {
    console.error('Failed to load permissions', e)
  } finally {
    loading.value = false
  }
}

async function loadUsers() {
  if (!canManage.value) return
  try {
    const allUsers = await api.get<any[]>('/users')
    const allowed = ['GHS', 'SNP', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']
    users.value = allUsers.filter(u => allowed.includes(u.name))
  } catch {} 
}

async function assignPermission() {
  if (!newPerm.value.userId) return
  
  assigning.value = true
  try {
    await api.post('/permissions/assign', {
      userId: newPerm.value.userId,
      entityName: props.entityName,
      entityId: props.entityId,
      permission: newPerm.value.permission
    })
    
    // Reset and reload
    newPerm.value.userId = null
    await loadPermissions()
  } catch (e) {
    console.error('Failed to assign', e)
  } finally {
    assigning.value = false
  }
}

async function revokePermission(perm: any) {
  if (!confirm(`Revoke ${perm.permission} from ${perm.user?.name}?`)) return

  revoking.value = perm.id
  try {
    // Controller now expects [FromBody] AssignPermissionRequest
    await api.post('/permissions/revoke', {
      userId: perm.userId,
      entityName: props.entityName,
      entityId: props.entityId,
      permission: perm.permission
    })
    await loadPermissions()
  } catch (e) {
    console.error('Failed to revoke', e)
  } finally {
    revoking.value = null
  }
}

function getInitials(name?: string) {
  if (!name) return '?'
  return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
}
</script>

