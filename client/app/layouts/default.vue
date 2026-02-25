<template>
  <v-app>
    <v-navigation-drawer
      v-model="drawer"
      :rail="!mobile && rail"
      :temporary="mobile"
      :permanent="!mobile"
      color="surface"
      class="border-e-thin"
    >
      <!-- Logo -->
      <v-list-item
        :prepend-icon="(!mobile && rail) ? 'mdi-airplane' : undefined"
        class="pa-4"
        @click="mobile ? undefined : rail = !rail"
      >
        <template v-if="mobile || !rail" #default>
          <div class="d-flex align-center">
            <v-icon icon="mdi-airplane" color="primary" size="28" class="mr-3" />
            <span class="text-h6 font-weight-bold text-gradient">Procument</span>
          </div>
        </template>
      </v-list-item>

      <v-divider />

      <!-- Nav Items -->
      <v-list density="compact" nav>
        <v-list-item
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          :prepend-icon="item.icon"
          :title="item.title"
          rounded="lg"
          class="mb-1"
          active-color="primary"
          @click="mobile ? drawer = false : undefined"
        />

        <!-- Admin Section -->
        <template v-if="authStore.isAdmin">
          <v-divider class="my-2" />
          <v-list-subheader v-if="mobile || !rail">ADMIN</v-list-subheader>
          <v-list-item
            to="/users"
            prepend-icon="mdi-account-group"
            title="Users"
            rounded="lg"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          />
        </template>
      </v-list>

      <template #append>
        <v-divider />
        <v-list-item class="pa-2">
          <template #prepend>
            <v-avatar color="primary" size="32">
              <span class="text-caption">{{ authStore.userInitials }}</span>
            </v-avatar>
          </template>
          <v-list-item-title class="text-body-2">{{ authStore.user?.name }}</v-list-item-title>
          <v-list-item-subtitle class="text-caption">{{ authStore.user?.role }}</v-list-item-subtitle>
          <template #append>
            <v-btn icon="mdi-logout" variant="text" size="small" @click="logout" />
          </template>
        </v-list-item>
      </template>
    </v-navigation-drawer>

    <!-- Top Bar -->
    <v-app-bar flat color="surface" class="border-b-thin" density="compact">
      <v-app-bar-nav-icon v-if="mobile" @click="drawer = !drawer" />
      <v-app-bar-title v-if="mobile">
        <span class="text-body-2 font-weight-bold text-gradient">Procument</span>
      </v-app-bar-title>
      <template #append>
        <v-menu :close-on-content-click="false" location="bottom end" max-width="400">
          <template #activator="{ props: bellProps }">
            <v-btn icon variant="text" size="small" v-bind="bellProps">
              <v-badge :content="unreadCount" :model-value="unreadCount > 0" color="error" floating>
                <v-icon icon="mdi-bell-outline" />
              </v-badge>
            </v-btn>
          </template>
          <v-card style="min-width: 340px; max-height: 420px;">
            <v-card-title class="d-flex align-center text-body-1 font-weight-bold pa-3 pb-1">
              Notifications
              <v-spacer />
              <v-chip v-if="unreadCount > 0" size="x-small" color="error" variant="flat">{{ unreadCount }}</v-chip>
            </v-card-title>
            <v-divider />
            <v-list v-if="notifications.length" density="compact" style="max-height: 320px; overflow-y: auto;">
              <v-list-item
                v-for="n in notifications"
                :key="n.id"
                :class="{ 'bg-surface-variant': !n.isRead }"
                class="py-2"
                @click="onNotificationClick(n)"
              >
                <template #prepend>
                  <v-icon
                    :icon="n.type === 'Rejection' ? 'mdi-close-circle' : n.type === 'PendingApproval' ? 'mdi-alert-circle' : 'mdi-information'"
                    :color="n.type === 'Rejection' ? 'error' : n.type === 'PendingApproval' ? 'warning' : 'info'"
                    size="20"
                  />
                </template>
                <v-list-item-title class="text-body-2" style="white-space: normal;">{{ n.message }}</v-list-item-title>
                <v-list-item-subtitle class="text-caption">
                  {{ n.entityName }} {{ n.entityNumber }} &middot; {{ timeAgo(n.createdAt) }}
                </v-list-item-subtitle>
                <template #append>
                  <v-btn icon="mdi-close" size="x-small" variant="text" @click.stop="dismissNotification(n.id)" />
                </template>
              </v-list-item>
            </v-list>
            <div v-else class="pa-6 text-center text-caption text-medium-emphasis">
              No notifications
            </div>
          </v-card>
        </v-menu>
        <v-btn icon="mdi-cog-outline" variant="text" size="small" class="ml-1" />
      </template>
    </v-app-bar>

    <v-main>
      <v-container fluid class="main-content">
        <slot />

        <!-- Rejection Modal on Login -->
        <v-dialog v-model="showRejectionModal" max-width="550" persistent>
          <v-card>
            <v-card-title class="d-flex align-center text-h6">
              <v-icon icon="mdi-alert-circle" color="error" class="mr-2" />
              Rejected Items
            </v-card-title>
            <v-divider />
            <v-card-text style="max-height: 400px; overflow-y: auto;">
              <p class="text-body-2 text-medium-emphasis mb-4">The following items have been rejected by admin:</p>
              <v-card
                v-for="r in rejections"
                :key="r.id"
                variant="outlined"
                class="mb-3 pa-3"
                :style="{ borderLeftColor: '#ef4444', borderLeftWidth: '3px' }"
              >
                <div class="d-flex align-center gap-2 mb-1">
                  <v-chip size="x-small" color="error" variant="flat">{{ r.entityName }}</v-chip>
                  <span class="text-body-2 font-weight-medium">{{ r.entityNumber }}</span>
                </div>
                <p class="text-body-2 mb-1">{{ r.message }}</p>
                <p v-if="r.rejectionNote" class="text-body-2 font-italic" style="color: #fbbf24;">
                  "{{ r.rejectionNote }}"
                </p>
              </v-card>
            </v-card-text>
            <v-card-actions>
              <v-spacer />
              <v-btn color="primary" variant="flat" @click="dismissAllRejections">Got it</v-btn>
            </v-card-actions>
          </v-card>
        </v-dialog>
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
const { mobile } = useDisplay()
const drawer = ref(!mobile.value)
const rail = ref(false)
const route = useRoute()
const authStore = useAuthStore()

watch(mobile, (isMobile) => {
  drawer.value = !isMobile
  if (isMobile) rail.value = false
})

const navItems = [
  { title: 'Dashboard', icon: 'mdi-view-dashboard', to: '/dashboard' },
  { title: 'RFQs', icon: 'mdi-file-document-outline', to: '/rfqs' },
  { title: 'Quotes', icon: 'mdi-currency-usd', to: '/quotes' },
  { title: 'Proforma Invoices', icon: 'mdi-receipt-text-outline', to: '/invoices' },
  { title: 'Invoices', icon: 'mdi-receipt-text-outline', to: '/final-invoices' },
  { title: 'Purchase Orders', icon: 'mdi-package-variant-closed', to: '/purchase-orders' },
  { title: 'Catalog', icon: 'mdi-database-outline', to: '/catalog' },
]

const pageTitle = computed(() => {
  const name = route.name as string | undefined
  if (!name) return 'Procument'
  return name.charAt(0).toUpperCase() + name.slice(1).replace(/-/g, ' ')
})

async function logout() {
  authStore.logout()
  await navigateTo('/login')
}

// ──── Notifications ────

const api = useApi()
const router = useRouter()

const notifications = ref<any[]>([])
const unreadCount = ref(0)
const rejections = ref<any[]>([])
const showRejectionModal = ref(false)

let pollTimer: ReturnType<typeof setInterval> | null = null

async function loadNotifications() {
  try {
    const [list, count] = await Promise.all([
      api.get<any[]>('/notifications'),
      api.get<number>('/notifications/unread-count'),
    ])
    notifications.value = list
    unreadCount.value = count
  } catch (e) { console.warn('[Notifications] Load failed', e) }
}

async function loadRejections() {
  try {
    const list = await api.get<any[]>('/notifications/rejections')
    if (list.length > 0) {
      rejections.value = list
      showRejectionModal.value = true
    }
  } catch (e) { console.warn('[Notifications] Load rejections failed', e) }
}

function onNotificationClick(n: any) {
  // Mark read
  api.patch(`/notifications/${n.id}/read`, {}).catch(() => {})
  n.isRead = true
  unreadCount.value = Math.max(0, unreadCount.value - 1)

  // Navigate to entity
  const routes: Record<string, string> = {
    Quote: '/quotes/',
    Invoice: '/invoices/',
    PurchaseOrder: '/purchase-orders/',
  }
  const base = routes[n.entityName]
  if (base) router.push(base + n.entityId)
}

async function dismissNotification(id: number) {
  try {
    await api.patch(`/notifications/${id}/dismiss`, {})
    notifications.value = notifications.value.filter(n => n.id !== id)
    unreadCount.value = Math.max(0, unreadCount.value - 1)
  } catch {}
}

async function dismissAllRejections() {
  try {
    await api.post('/notifications/dismiss-rejections', {})
  } catch {}
  showRejectionModal.value = false
  rejections.value = []
  await loadNotifications()
}

function timeAgo(dateStr: string) {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  const days = Math.floor(hrs / 24)
  return `${days}d ago`
}

onMounted(() => {
  if (authStore.isAuthenticated) {
    loadNotifications()
    loadRejections()
    pollTimer = setInterval(loadNotifications, 30000)
  }
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>
