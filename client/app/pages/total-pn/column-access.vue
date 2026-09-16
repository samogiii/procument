<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Total Project Column Access</h1>
        <p class="text-body-2 text-medium-emphasis mt-1 mb-0">
          Choose which Total Project columns each user can see. Hidden columns are also removed from that user's Columns menu.
        </p>
      </div>
      <v-spacer />
      <v-btn to="/total-pn" variant="tonal" prepend-icon="mdi-table-large">Open Total Project</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <v-autocomplete
          v-model="selectedUserId"
          :items="users"
          item-title="name"
          item-value="id"
          label="Select user"
          variant="outlined"
          density="comfortable"
          clearable
          :loading="usersLoading"
          @update:model-value="loadUserColumns"
        />

        <template v-if="selectedUserId">
          <v-divider class="mb-4" />
          <div class="d-flex flex-wrap align-center gap-2 mb-4">
            <span class="text-subtitle-1 font-weight-bold">Visible columns for {{ selectedUserName }}</span>
            <v-spacer />
            <v-btn size="small" variant="text" @click="setAll(false)">Hide all</v-btn>
            <v-btn size="small" variant="text" color="primary" @click="setAll(true)">Show all</v-btn>
          </div>

          <div v-if="columnsLoading" class="d-flex justify-center py-10">
            <v-progress-circular indeterminate color="primary" />
          </div>
          <v-row v-else dense>
            <v-col v-for="column in columns" :key="column.key" cols="12" sm="6" md="4">
              <v-switch
                v-model="column.isVisible"
                :label="column.label"
                color="primary"
                density="compact"
                hide-details
                inset
              />
            </v-col>
          </v-row>
        </template>
      </v-card-text>
      <v-card-actions v-if="selectedUserId && !columnsLoading" class="px-6 pb-5">
        <v-spacer />
        <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :loading="saving" @click="save">
          Save column access
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()

if (!authStore.isSuperAdmin) navigateTo('/dashboard')

interface UserOption { id: number; name: string }
interface ColumnOption { key: string; label: string; isVisible: boolean }
interface ColumnAccessResponse { userId: number; userName: string; columns: ColumnOption[] }

const users = ref<UserOption[]>([])
const selectedUserId = ref<number | null>(null)
const selectedUserName = ref('')
const columns = ref<ColumnOption[]>([])
const usersLoading = ref(false)
const columnsLoading = ref(false)
const saving = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

function showSnack(message: string, color = 'success') {
  snackbarText.value = message
  snackbarColor.value = color
  snackbar.value = true
}

async function loadUserColumns(userId: number | null) {
  columns.value = []
  selectedUserName.value = ''
  if (!userId) return
  columnsLoading.value = true
  try {
    const response = await api.get<ColumnAccessResponse>(`/po-items/total-pn/column-access/${userId}`)
    selectedUserName.value = response.userName
    columns.value = response.columns
  } catch {
    showSnack('Could not load column access.', 'error')
  } finally {
    columnsLoading.value = false
  }
}

function setAll(isVisible: boolean) {
  columns.value.forEach(column => { column.isVisible = isVisible })
}

async function save() {
  if (!selectedUserId.value) return
  saving.value = true
  try {
    await api.put(`/po-items/total-pn/column-access/${selectedUserId.value}`, {
      visibleColumns: columns.value.filter(column => column.isVisible).map(column => column.key)
    })
    showSnack(`Column access saved for ${selectedUserName.value}.`)
  } catch {
    showSnack('Could not save column access.', 'error')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  usersLoading.value = true
  try {
    const response = await api.get<any[]>('/users')
    users.value = response
      .filter(user => user.isActive !== false)
      .map(user => ({ id: user.id, name: user.name }))
      .sort((a, b) => a.name.localeCompare(b.name))
  } catch {
    showSnack('Could not load users.', 'error')
  } finally {
    usersLoading.value = false
  }
})
</script>
