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
          <v-list-item
            to="/company-presets"
            prepend-icon="mdi-domain"
            title="Company Presets"
            rounded="lg"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          />
          <v-list-item
            to="/admin/audit"
            prepend-icon="mdi-timeline-clock-outline"
            title="System Activity"
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
                :class="{ 'bg-info': !n.isRead }"
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
        <v-btn
          :icon="appTheme.isDark.value ? 'mdi-weather-sunny' : 'mdi-weather-night'"
          variant="text"
          size="small"
          class="ml-1"
          @click="appTheme.toggle()"
        />
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
const appTheme = useAppTheme()
appTheme.init()

watch(mobile, (isMobile) => {
  drawer.value = !isMobile
  if (isMobile) rail.value = false
})

const allNavItems = [
  { title: 'Dashboard', icon: 'mdi-view-dashboard', to: '/dashboard', adminOnly: false, ilsOnly: false },
  { title: 'RFQs', icon: 'mdi-file-document-outline', to: '/rfqs', adminOnly: false, ilsOnly: false },
  // { title: 'RFQ Items', icon: 'mdi-format-list-checks', to: '/rfq-items', adminOnly: false },
  { title: 'RFQ Items', icon: 'mdi-truck-cargo-container', to: '/procument', adminOnly: false, ilsOnly: false },
  { title: 'Quotes', icon: 'mdi-currency-usd', to: '/quotes', adminOnly: false, ilsOnly: false },
  { title: 'Proforma Invoices', icon: 'mdi-receipt-text-outline', to: '/invoices', adminOnly: true, ilsOnly: false },
  { title: 'Purchase Orders', icon: 'mdi-package-variant-closed', to: '/purchase-orders', adminOnly: false, ilsOnly: false },
  { title: 'Payment', icon: 'mdi-cash-multiple', to: '/payment', paymentOnly: true },
  { title: 'Invoices', icon: 'mdi-receipt-text-outline', to: '/final-invoices', adminOnly: true, ilsOnly: false },
  { title: 'ILS', icon: 'mdi-warehouse', to: '/ils', adminOnly: true, ilsOnly: true },
  { title: 'Cap List', icon: 'mdi-format-list-checks', to: '/caplist', adminOnly: true, ilsOnly: false },
  { title: 'Inventory', icon: 'mdi-archive-outline', to: '/inventory', adminOnly: true, ilsOnly: false },
  { title: 'Catalog', icon: 'mdi-database-outline', to: '/catalog', adminOnly: true, ilsOnly: false },
  { title: 'Customers', icon: 'mdi-domain', to: '/catalog/customers', customerMenu:true },
  { title: 'Supplier Requests', icon: 'mdi-account-clock-outline', to: '/catalog/supplier-requests', adminOnly: true, ilsOnly: false },
] as any[]

const navItems = computed(() => {
  return allNavItems.filter(item => {
    // Payment-only items: visible to Payment role and Admin
    if (item.paymentOnly) return authStore.isPayment
    // ILS-only pages: only for ILS users OR Admin
    if (item.ilsOnly && !authStore.ilsUsers && !authStore.isAdmin) return false
    // Admin-only pages (non-ILS): only for Admin
    if (item.adminOnly && !item.ilsOnly && !authStore.isAdmin) return false
    if (item.customerMenu && !authStore.customerMenu) return false
    return true
  })
})

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
let tokenCheckTimer: ReturnType<typeof setInterval> | null = null

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

  // Periodic token expiry check — every 30s
  tokenCheckTimer = setInterval(() => {
    if (authStore.user?.token && authStore.isTokenExpired) {
      authStore.logout()
      navigateTo('/login')
    }
  }, 30000)
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
  if (tokenCheckTimer) clearInterval(tokenCheckTimer)
})
</script>
