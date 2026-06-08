<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Menu Access Control</h1>
        <p class="text-medium-emphasis text-body-2 mt-1">
          Control which users can see each gated menu section. SuperAdmin always has full access.
        </p>
      </div>
    </div>

    <v-row>
      <v-col
        v-for="group in groups"
        :key="group.feature"
        cols="12"
        md="6"
        lg="4"
      >
        <v-card class="glass-card h-100">
          <v-card-title class="d-flex align-center gap-2 pb-1">
            <v-icon :icon="featureIcon(group.feature)" size="20" color="primary" />
            {{ featureLabel(group.feature) }}
          </v-card-title>
          <v-card-subtitle class="text-caption pb-2">{{ group.feature }}</v-card-subtitle>

          <v-card-text>
            <!-- Current users -->
            <div class="d-flex flex-wrap gap-1 mb-3 min-height-chip">
              <v-chip
                v-for="name in group.userNames"
                :key="name"
                size="small"
                color="primary"
                variant="tonal"
                closable
                :disabled="saving === group.feature + name"
                @click:close="removeUser(group.feature, name)"
              >{{ name }}</v-chip>
              <span v-if="!group.userNames.length" class="text-caption text-grey">
                No users — only SuperAdmin can access
              </span>
            </div>

            <!-- Add user -->
            <v-autocomplete
              v-model="addTarget[group.feature]"
              :items="availableUsers(group)"
              item-title="name"
              item-value="name"
              label="Add user…"
              density="compact"
              variant="outlined"
              hide-details
              clearable
              @update:model-value="(v) => v && addUser(group.feature, v)"
            />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="2500">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()

// Guard: SuperAdmin only
if (!authStore.isSuperAdmin) navigateTo('/dashboard')

interface PermGroup { feature: string; userNames: string[] }
interface UserItem   { id: number; name: string }

const groups   = ref<PermGroup[]>([])
const allUsers = ref<UserItem[]>([])
const saving   = ref('')
const addTarget = ref<Record<string, string | null>>({})

const snackbar      = ref(false)
const snackbarText  = ref('')
const snackbarColor = ref('success')

function showSnack(msg: string, color = 'success') {
  snackbarText.value  = msg
  snackbarColor.value = color
  snackbar.value      = true
}

const FEATURE_LABELS: Record<string, string> = {
  paymentMenu:     'Payment Menu',
  companyPresets:  'Company Presets',
  syncApp:         'Sync Application',
  systemActivity:  'System Activity',
  supplierRequests:'Supplier Requests',
  capList:         'Cap List',
  ils:             'ILS',
  shippingMenu:    'Shipping',
  customerMenu:    'Customer Menu',
  isAmir:          'Amir Access',
  newRFQ:          'New RFQ',
  ilsUsers:        'ILS Users',
  isPDFSelection:  'PDF Selection',
  actionCenter:    'Action Center',
  taskManager:     'Task Manager',
}

const FEATURE_ICONS: Record<string, string> = {
  paymentMenu:     'mdi-cash-multiple',
  companyPresets:  'mdi-office-building-cog',
  syncApp:         'mdi-sync',
  systemActivity:  'mdi-clipboard-text-clock',
  supplierRequests:'mdi-truck-delivery',
  capList:         'mdi-format-list-bulleted',
  ils:             'mdi-layers',
  shippingMenu:    'mdi-package-variant-closed',
  customerMenu:    'mdi-account-group',
  isAmir:          'mdi-shield-account',
  newRFQ:          'mdi-file-plus',
  ilsUsers:        'mdi-account-key',
  isPDFSelection:  'mdi-file-pdf-box',
  actionCenter:    'mdi-alert-circle-outline',
  taskManager:     'mdi-view-list',
}

function featureLabel(f: string) { return FEATURE_LABELS[f] ?? f }
function featureIcon(f: string)  { return FEATURE_ICONS[f] ?? 'mdi-menu' }

function availableUsers(group: PermGroup) {
  return allUsers.value.filter(u => !group.userNames.includes(u.name))
}

async function addUser(feature: string, userName: string) {
  saving.value = feature + userName
  try {
    await api.post('/menu-permissions', { feature, userName })
    const g = groups.value.find(g => g.feature === feature)
    if (g && !g.userNames.includes(userName)) g.userNames.push(userName)
    // Refresh the auth store so the current SuperAdmin sees changes immediately
    await authStore.loadMenuPermissions()
    showSnack(`${userName} added to ${featureLabel(feature)}`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to add user', 'error')
  } finally {
    saving.value = ''
    addTarget.value[feature] = null
  }
}

async function removeUser(feature: string, userName: string) {
  saving.value = feature + userName
  try {
    await api.del(`/menu-permissions/${encodeURIComponent(feature)}/${encodeURIComponent(userName)}`)
    const g = groups.value.find(g => g.feature === feature)
    if (g) g.userNames = g.userNames.filter(n => n !== userName)
    await authStore.loadMenuPermissions()
    showSnack(`${userName} removed from ${featureLabel(feature)}`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to remove user', 'error')
  } finally {
    saving.value = ''
  }
}

onMounted(async () => {
  const [perms, users] = await Promise.all([
    api.get<PermGroup[]>('/menu-permissions'),
    api.get<UserItem[]>('/users'),
  ])
  groups.value   = perms
  allUsers.value = users.map((u: any) => ({ id: u.id, name: u.name }))
    .sort((a: UserItem, b: UserItem) => a.name.localeCompare(b.name))
})
</script>

<style scoped>
.min-height-chip { min-height: 36px; }
</style>
