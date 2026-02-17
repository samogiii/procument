<template>
  <div>
    <PageHeader title="Dashboard" />

    <!-- Stats Row -->
    <v-row class="mb-6">
      <v-col v-for="stat in stats" :key="stat.title" cols="12" sm="6" md="3">
        <StatCard :icon="stat.icon" :color="stat.color" :label="stat.title" :value="stat.value" />
      </v-col>
    </v-row>

    <v-row>
      <!-- Recent Activity (admins only) -->
      <v-col cols="12" :md="isAdmin ? 8 : 12">
        <v-card class="glass-card" v-if="isAdmin">
          <v-card-title class="d-flex align-center">
            <v-icon icon="mdi-clock-outline" class="mr-2" size="20" />
            Recent Activity
          </v-card-title>
          <v-card-text>
            <v-list v-if="recentActivity.length" bg-color="transparent" density="compact">
              <v-list-item
                v-for="item in recentActivity"
                :key="item.id"
                :subtitle="formatTime(item.timestamp)"
              >
                <template #prepend>
                  <v-avatar :color="actionColor(item.action)" size="32" variant="tonal" class="mr-3">
                    <v-icon :icon="actionIcon(item.action)" size="16" />
                  </v-avatar>
                </template>
                <v-list-item-title class="text-body-2">
                  <span class="font-weight-medium">{{ item.userName || 'System' }}</span>
                  <span class="text-medium-emphasis"> {{ item.action.toLowerCase() }} </span>
                  <span class="font-weight-medium">{{ item.entityName }}</span>
                  <span class="text-medium-emphasis"> #{{ item.entityId }}</span>
                </v-list-item-title>
              </v-list-item>
            </v-list>
            <div v-else class="text-center text-medium-emphasis pa-6">
              No recent activity
            </div>
          </v-card-text>
        </v-card>

        <!-- Non-admin welcome -->
        <v-card v-else class="glass-card">
          <v-card-text class="text-center pa-8">
            <v-icon icon="mdi-hand-wave-outline" size="48" color="primary" class="mb-4" />
            <h3 class="text-h6 mb-2">Welcome to Procument</h3>
            <p class="text-medium-emphasis">Use the sidebar to navigate to RFQs, Quotes, and more.</p>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Quick Actions -->
      <v-col v-if="isAdmin" cols="12" md="4">
        <v-card class="glass-card">
          <v-card-title class="d-flex align-center">
            <v-icon icon="mdi-lightning-bolt" class="mr-2" size="20" />
            Quick Actions
          </v-card-title>
          <v-card-text>
            <v-btn color="primary" block class="mb-2" prepend-icon="mdi-plus" to="/rfqs">
              New RFQ
            </v-btn>
            <v-btn color="secondary" variant="outlined" block class="mb-2" prepend-icon="mdi-plus" to="/quotes">
              New Quote
            </v-btn>
            <v-btn variant="outlined" block prepend-icon="mdi-plus" to="/purchase-orders">
              New PO
            </v-btn>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()

const isAdmin = computed(() => authStore.isAdmin)
const loading = ref(true)
const recentActivity = ref<any[]>([])

const stats = ref([
  { title: 'Total RFQs',      value: '—', icon: 'mdi-file-document-outline',  color: 'primary' },
  { title: 'Total Quotes',    value: '—', icon: 'mdi-currency-usd',           color: 'info' },
  { title: 'Pending RFQs',    value: '—', icon: 'mdi-file-clock-outline',     color: 'warning' },
  { title: 'Pending Quotes',  value: '—', icon: 'mdi-clock-alert-outline',    color: 'secondary' },
  { title: 'Total Users',     value: '—', icon: 'mdi-account-group-outline',  color: 'success' },
])

onMounted(async () => {
  try {
    const data = await api.get<any>('/dashboard')
    stats.value[0].value = String(data.totalRfqs ?? 0)
    stats.value[1].value = String(data.totalQuotes ?? 0)
    stats.value[2].value = String(data.pendingRfqs ?? 0)
    stats.value[3].value = String(data.pendingQuotes ?? 0)
    stats.value[4].value = String(data.totalUsers ?? 0)
    recentActivity.value = data.recentActivity ?? []
  } catch (e) {
    console.error('[Dashboard] Failed to load stats', e)
  } finally {
    loading.value = false
  }
})

// ─── Helpers ───
function actionIcon(action: string): string {
  const map: Record<string, string> = {
    Create: 'mdi-plus-circle-outline',
    Update: 'mdi-pencil-outline',
    Delete: 'mdi-delete-outline',
    Login:  'mdi-login',
    Logout: 'mdi-logout',
  }
  return map[action] || 'mdi-information-outline'
}

function actionColor(action: string): string {
  const map: Record<string, string> = {
    Create: 'success',
    Update: 'info',
    Delete: 'error',
    Login:  'primary',
    Logout: 'grey',
  }
  return map[action] || 'grey'
}

function formatTime(ts: string): string {
  if (!ts) return ''
  const d = new Date(ts)
  const now = new Date()
  const diff = now.getTime() - d.getTime()
  const mins  = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days  = Math.floor(diff / 86400000)

  if (mins < 1)   return 'just now'
  if (mins < 60)  return `${mins}m ago`
  if (hours < 24) return `${hours}h ago`
  if (days < 7)   return `${days}d ago`
  return d.toLocaleDateString()
}
</script>
