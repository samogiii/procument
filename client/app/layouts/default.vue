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
        <template v-for="item in navItems" :key="item.to || item.title">
          <!-- Nested Group -->
          <v-list-group v-if="item.children" :value="item.title" style="--indent-padding: 0px">
            <template #activator="{ props }">
              <v-list-item
                v-bind="props"
                :prepend-icon="item.icon"
                :title="item.title"
                rounded="lg"
                class="mb-1"
              />
            </template>

            <v-list-item
              v-for="sub in item.children"
              :key="sub.to"
              :to="sub.to"
              :prepend-icon="sub.icon"
              :title="sub.title"
              rounded="lg"
              class="mb-1 child-nav-item"
              active-color="primary"
              @click="mobile ? drawer = false : undefined"
            >
              <template v-if="sub.badge" #append>
                <v-chip
                  size="x-small"
                  color="error"
                  variant="flat"
                  style="height:18px; min-width:18px; font-size:10px; font-weight:700; padding:0 5px;"
                >
                  {{ sub.badge }}
                </v-chip>
              </template>
            </v-list-item>
          </v-list-group>

          <!-- Regular Item -->
          <v-list-item
            v-else
            :to="item.to"
            :prepend-icon="item.icon"
            :title="item.title"
            rounded="lg"
            class="mb-1"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          >
            <template v-if="item.badge" #append>
              <v-chip
                size="x-small"
                color="error"
                variant="flat"
                style="height:18px; min-width:18px; font-size:10px; font-weight:700; padding:0 5px;"
              >
                {{ item.badge }}
              </v-chip>
            </template>
          </v-list-item>
        </template>

        <!-- Admin Section -->
        <template v-if="authStore.isAdmin">
          <v-divider class="my-2" />
          <v-list-subheader v-if="mobile || !rail">ADMIN</v-list-subheader>
          <v-list-item
            v-if="authStore.isSuperAdmin"
            to="/users"
            prepend-icon="mdi-account-group"
            title="Users"
            rounded="lg"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          />
          <v-list-item
            v-if="authStore.isSuperAdmin"
            to="/menu-access"
            prepend-icon="mdi-shield-key-outline"
            title="Menu Access"
            rounded="lg"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          />
          
          <v-list-item
            v-if="authStore.companyPresets"
            to="/company-presets"
            prepend-icon="mdi-domain"
            title="Company Presets"
            rounded="lg"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          />
          <v-list-item
            v-if="authStore.syncApp"
            to="/satellite-sync"
            prepend-icon="mdi-web-sync"
            title="Sync Application"
            rounded="lg"
            active-color="primary"
            @click="mobile ? drawer = false : undefined"
          />
          <v-list-item
            v-if="authStore.systemActivity"
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
          <v-card style="min-width: 360px; max-height: 480px; overflow: hidden;">
            <!-- Header -->
            <div class="d-flex align-center pa-3 pb-2" style="gap:8px;">
              <span class="text-body-1 font-weight-bold">Notifications</span>
              <v-spacer />
              <!-- Unread count badge -->
              <v-chip v-if="unreadCount > 0" size="x-small" color="error" variant="flat" prepend-icon="mdi-circle-small">
                {{ unreadCount }} new
              </v-chip>
              <!-- Total count -->
              <v-chip v-if="notifications.length" size="x-small" color="default" variant="tonal">
                {{ notifications.length }} total
              </v-chip>
              <!-- Mark all read -->
              <v-btn
                v-if="unreadCount > 0"
                size="x-small"
                variant="text"
                color="primary"
                @click="markAllRead"
              >
                Mark all read
              </v-btn>
            </div>
            <v-divider />

            <v-list v-if="notifications.length" density="compact" style="max-height: 390px; overflow-y: auto;">
              <v-list-item
                v-for="n in notifications"
                :key="n.id"
                class="py-2 px-3 cursor-pointer"
                style="border-bottom: 1px solid rgba(var(--v-border-color), 0.08);"
                @click="onNotificationClick(n)"
              >
                <template #prepend>
                  <!-- Icon with NEW dot badge if unread -->
                  <div class="d-flex flex-column align-center mr-3" style="gap:4px; min-width:40px; position:relative;">
                    <v-badge
                      :model-value="!n.isRead"
                      color="error"
                      dot
                      floating
                      location="top end"
                    >
                      <v-avatar :color="notifColor(n.type)" size="34" variant="tonal">
                        <v-icon :icon="notifIcon(n.type)" size="18" />
                      </v-avatar>
                    </v-badge>
                    <!-- Triggered-by user initials below icon -->
                    <v-avatar
                      v-if="n.triggeredByUserName"
                      :color="notifColor(n.type)"
                      size="18"
                      style="font-size:8px; font-weight:700;"
                    >
                      {{ n.triggeredByUserName.slice(0, 2).toUpperCase() }}
                    </v-avatar>
                  </div>
                </template>

                <!-- Message + meta -->
                <div>
                  <div
                    class="text-body-2"
                    :class="n.isRead ? 'text-medium-emphasis' : 'font-weight-medium'"
                    style="white-space: normal; line-height:1.35;"
                  >
                    {{ n.message }}
                  </div>
                  <div class="d-flex align-center mt-1" style="gap:6px;">
                    <v-chip size="x-small" :color="notifColor(n.type)" variant="tonal" style="height:16px; font-size:9px;">
                      {{ notifLabel(n.type) }}
                    </v-chip>
                    <span class="text-caption opacity-60">{{ n.entityNumber }}</span>
                    <span class="text-caption opacity-50">&middot; {{ timeAgo(n.createdAt) }}</span>
                  </div>
                </div>

                <template #append>
                  <v-btn
                    icon="mdi-close"
                    size="x-small"
                    variant="text"
                    class="ml-1"
                    @click.stop="dismissNotification(n.id)"
                  />
                </template>
              </v-list-item>
            </v-list>

            <div v-else class="pa-8 text-center">
              <v-icon icon="mdi-bell-check-outline" size="40" color="success" class="mb-2 opacity-60" />
              <div class="text-caption text-medium-emphasis">All caught up!</div>
            </div>
          </v-card>
        </v-menu>
        <!-- Theme picker -->
        <v-menu offset="8" :close-on-content-click="true">
          <template #activator="{ props: menuProps }">
            <v-btn icon variant="text" size="small" class="ml-1" v-bind="menuProps">
              <v-icon v-if="appTheme.currentTheme.value === 'procumentDark'" icon="mdi-weather-night" />
              <v-icon v-else-if="appTheme.currentTheme.value === 'procumentFrost'" icon="mdi-snowflake" />
              <v-icon v-else icon="mdi-weather-sunny" />
            </v-btn>
          </template>
          <v-card min-width="160" rounded="lg" elevation="4">
            <v-list density="compact" nav>
              <v-list-item
                rounded="lg"
                :active="appTheme.currentTheme.value === 'procumentDark'"
                @click="appTheme.setTheme('procumentDark')"
              >
                <template #prepend>
                  <span class="theme-swatch" style="background:#1565C0;box-shadow:0 0 0 2px #0D1117" />
                </template>
                <v-list-item-title>Dark</v-list-item-title>
              </v-list-item>
              <v-list-item
                rounded="lg"
                :active="appTheme.currentTheme.value === 'procumentLight'"
                @click="appTheme.setTheme('procumentLight')"
              >
                <template #prepend>
                  <span class="theme-swatch" style="background:#1565C0;box-shadow:0 0 0 2px #E2E8F0" />
                </template>
                <v-list-item-title>Light</v-list-item-title>
              </v-list-item>
              <v-list-item
                rounded="lg"
                :active="appTheme.currentTheme.value === 'procumentFrost'"
                @click="appTheme.setTheme('procumentFrost')"
              >
                <template #prepend>
                  <span class="theme-swatch" style="background:linear-gradient(135deg,#90D5EC,#AADDEC);box-shadow:0 0 0 2px #D4D4D4" />
                </template>
                <v-list-item-title>Frost</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-card>
        </v-menu>
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
  { title: 'Total Project', icon: 'mdi-table-large', to: '/total-pn', adminOnly: true, ilsOnly: false },
  { title: 'Action Center', icon: 'mdi-alert-circle-outline', to: '/attention', actionCenter: true, adminOnly: false, ilsOnly: false },
  { title: 'RFQs', icon: 'mdi-file-document-outline', to: '/rfqs', adminOnly: false, ilsOnly: false },
  // { title: 'RFQ Items', icon: 'mdi-format-list-checks', to: '/rfq-items', adminOnly: false },
  { title: 'RFQ Items', icon: 'mdi-truck-cargo-container', to: '/procument', adminOnly: false, ilsOnly: false },
  { title: 'Quotes', icon: 'mdi-currency-usd', to: '/quotes', adminOnly: false, ilsOnly: false },
  { title: 'Sales Order', icon: 'mdi-receipt-text-outline', to: '/invoices', adminOnly: true, ilsOnly: false },
  { title: 'Checking Purchase Items', icon: 'mdi-clipboard-edit-outline', to: '/procurements', adminOnly: false, ilsOnly: false },
  { title: 'Purchase Orders', icon: 'mdi-package-variant-closed', to: '/purchase-orders', adminOnly: false, ilsOnly: false },
  
  { title: 'Shipping', icon: 'mdi-warehouse', to: '/shipping', inventoryOnly: true },
  { title: 'Shipping Control', icon: 'mdi-truck-delivery-outline', to: '/total-shipping', inventoryOnly: true,  },

  { title: 'Invoices', icon: 'mdi-receipt-text-outline', to: '/final-invoices', adminOnly: true, ilsOnly: false },
  // ── Shipping workflow ──
  {
    title: 'Shipping',
    icon: 'mdi-truck-delivery-outline',
    children: [
      { title: 'Warehouse Shippings', icon: 'mdi-warehouse', to: '/shipping', expertSydVisible: true },
      { title: 'Shipping Control', icon: 'mdi-truck-delivery-outline', to: '/total-shipping', shippingMenu: true, ilsOnly: false, inventoryVisible: true, expertSydVisible: true },
      { title: 'Ready for SN#', icon: 'mdi-package-check', to: '/shipping/ready-for-sn', shippingMenu: true, ilsOnly: false, expertSydVisible: true },
      { title: 'Shipment', icon: 'mdi-file-document-check-outline', to: '/shipment-notes', shippingMenu: true, ilsOnly: false, expertSydVisible: true },
    ]
  },
  {
    title: 'Payment',
    icon: 'mdi-cash-multiple',
    children:[
      { title: 'Payment Withdraw', icon: 'mdi-cash-multiple', to: '/payment', paymentMenu: true },
      { title: 'Payment Deposit', icon: 'mdi-cash-plus', to: '/payment/customer-payments', paymentMenu: true },
      { title: 'Wallets', icon: 'mdi-wallet-outline', to: '/payment-control', paymentMenu: true, paymentOnly: true },
    ]
  },
  { title: 'ILS', icon: 'mdi-warehouse', to: '/ils', ilsMenu: true, ilsOnly: true },
  { title: 'Cap List', icon: 'mdi-format-list-checks', to: '/caplist', capList: true, ilsOnly: false },
  { title: 'Inventory', icon: 'mdi-archive-outline', to: '/inventory', adminOnly: true, ilsOnly: false },
  { title: 'Catalog', icon: 'mdi-database-outline', to: '/catalog', adminOnly: true, ilsOnly: false },

  { title: 'Customers', icon: 'mdi-domain', to: '/catalog/customers', customerMenu: true },
  { title: 'Supplier Requests', icon: 'mdi-account-clock-outline', to: '/catalog/supplier-requests', supplierRequests: true, ilsOnly: false },
  { title: 'Task Manager', icon: 'mdi-view-list', to: '/tasks', taskManager: true, ilsOnly: false },

  
] as any[]

// Mirrors the route allowlist enforced in middleware/auth.global.ts.
// Experts get a tight whitelist; Expert SYD additionally sees ILS.
const EXPERT_NAV_PATHS = new Set<string>([
  '/rfqs', '/procument', '/quotes', '/procurements', '/purchase-orders'
])
const EXPERT_SYD_NAV_PATHS = new Set<string>([...EXPERT_NAV_PATHS, '/ils', '/total-shipping', '/shipment-notes', '/shipping/ready-for-sn', '/shipping', '/inventory', '/catalog'])

// Map unread notifications → nav route badges
const navBadges = computed(() => {
  const counts: Record<string, number> = {}
  for (const n of notifications.value) {
    if (n.isRead) continue
    let route = ''
    switch (n.type) {
      // Inventory user sees: new tracks assigned to their warehouse, and reviewed parts
      case 'TrackAdded':
      case 'PartAccepted':
      case 'PartRejected':
        route = '/shipping'
        break
      // Admin sees: inventory submitted parts, tracks rejected by inventory
      case 'TrackSubmitted':
      case 'TrackRejected':
        route = '/total-shipping'
        break
      // Admin sees: parts accepted and ready to create SN#
      case 'ReadyForSN':
        route = '/shipping/ready-for-sn'
        break
      // Existing types → purchase orders
      case 'Rejection':
      case 'PendingApproval':
      case 'StatusChange':
        route = '/purchase-orders'
        break
    }
    if (route) counts[route] = (counts[route] ?? 0) + 1
  }
  return counts
})

const navItems = computed(() => {
  const filterItem = (item: any) => {
    // ── Inventory: show only inventory-specific pages ──────────────────────
    if (authStore.user?.role === 'Inventory') {
      return item.inventoryOnly === true || item.inventoryVisible === true
    }

    // ── Experts: hard whitelist by route, regardless of adminOnly/ilsOnly flags ──
    if (authStore.user?.role === 'Expert') {
      const allowed = authStore.user?.name === 'SYD' ? EXPERT_SYD_NAV_PATHS : EXPERT_NAV_PATHS
      return allowed.has(item.to) || (item.children && item.children.some((c: any) => allowed.has(c.to)))
    }

    // If user is strictly in the Payment role, they only see paymentOnly items
    if (authStore.user?.role === 'Payment') {
      return item.paymentOnly === true
    }

    // inventoryOnly items: only visible to Inventory role (handled at the start of filterItem)
    if (item.inventoryOnly) return false

    // ── Gated menu flags — checked against FeaturePermissions lists ──────────
    if (item.paymentMenu      && !authStore.paymentMenu)      return false
    if (item.shippingMenu     && !authStore.shippingMenu)     return false
    if (item.ilsMenu          && !authStore.ilsMenu)          return false
    if (item.capList          && !authStore.capList)          return false
    if (item.supplierRequests && !authStore.supplierRequests) return false
    if (item.customerMenu     && !authStore.customerMenu)     return false
    if (item.actionCenter     && !authStore.actionCenter)     return false
    if (item.taskManager      && !authStore.taskManager)      return false

    // Payment-only items (Wallets): further restricted to Payment role / SuperAdmin
    if (item.paymentOnly) return authStore.isPayment
    // ILS-only pages: only for users with ilsMenu access
    if (item.ilsOnly && !authStore.ilsMenu) return false
    // Admin-only pages: only for Admin/SuperAdmin
    if (item.adminOnly && !authStore.isAdmin) return false
    return true
  }

  return allNavItems.filter(filterItem).map(item => {
    const processItem = (i: any) => {
      if (i.title === 'Task Manager') {
        return { ...i, badge: tasksPendingCount.value > 0 ? tasksPendingCount.value : undefined }
      }
      const count = navBadges.value[i.to]
      return count ? { ...i, badge: count } : i
    }

    if (item.children) {
      const filteredChildren = item.children.filter(filterItem).map(processItem)
      if (filteredChildren.length === 0) return null
      return { ...item, children: filteredChildren }
    }

    return processItem(item)
  }).filter(Boolean)
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

// ──── Auto Sync (MGH & System Admin) ────
const isAutoSyncUser = computed(() => {
  const name = authStore.user?.name
  return name === 'MGH' || name === 'System Admin'
})

let lastSyncTime = 0
const SYNC_COOLDOWN = 30000 // 30 seconds cooldown to avoid spamming

async function triggerAutoSync() {
  if (!isAutoSyncUser.value) return
  
  // Basic cooldown check
  const now = Date.now()
  if (now - lastSyncTime < SYNC_COOLDOWN) return
  
  try {
    console.log('[AutoSync] Triggering background sync...')
    await api.post('/sync/trigger-all')
    lastSyncTime = Date.now()
    console.log('[AutoSync] Sync completed.')
  } catch (e) {
    console.warn('[AutoSync] Sync failed', e)
  }
}

// ──── Notifications ────

const api = useApi()
const router = useRouter()

const notifications = ref<any[]>([])
const unreadCount = ref(0)
const tasksPendingCount = ref(0)
const rejections = ref<any[]>([])
const showRejectionModal = ref(false)

let pollTimer: ReturnType<typeof setInterval> | null = null
let tokenCheckTimer: ReturnType<typeof setInterval> | null = null

async function loadNotifications() {
  try {
    const [list, count, taskCount] = await Promise.all([
      api.get<any[]>('/notifications'),
      api.get<number>('/notifications/unread-count'),
      api.get<number>('/tasks/pending-count'),
    ])
    notifications.value = list
    unreadCount.value = count
    tasksPendingCount.value = taskCount
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
    TrackNumber: '/shipping/track-numbers/',
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

async function markAllRead() {
  try {
    await api.patch('/notifications/read-all', {})
    notifications.value.forEach(n => (n.isRead = true))
    unreadCount.value = 0
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

function notifLabel(type: string) {
  switch (type) {
    case 'TrackAdded':     return 'Track Added'
    case 'TrackSubmitted': return 'Submitted'
    case 'PartAccepted':   return 'Accepted'
    case 'PartRejected':   return 'Rejected'
    case 'TrackRejected':  return 'Track Rejected'
    case 'ReadyForSN':     return 'Ready for SN#'
    case 'Rejection':      return 'Rejected'
    case 'PendingApproval':return 'Pending'
    default:               return 'Info'
  }
}

function notifIcon(type: string) {
  switch (type) {
    case 'TrackAdded':     return 'mdi-truck-plus-outline'
    case 'TrackSubmitted': return 'mdi-package-variant'
    case 'PartAccepted':   return 'mdi-check-circle-outline'
    case 'PartRejected':   return 'mdi-close-circle-outline'
    case 'TrackRejected':  return 'mdi-alert-circle-outline'
    case 'ReadyForSN':     return 'mdi-package-check'
    case 'Rejection':      return 'mdi-close-circle'
    case 'PendingApproval':return 'mdi-alert-circle'
    default:               return 'mdi-bell-outline'
  }
}

function notifColor(type: string) {
  switch (type) {
    case 'TrackAdded':     return 'primary'
    case 'TrackSubmitted': return 'warning'
    case 'PartAccepted':   return 'success'
    case 'PartRejected':   return 'error'
    case 'TrackRejected':  return 'error'
    case 'ReadyForSN':     return 'success'
    case 'Rejection':      return 'error'
    case 'PendingApproval':return 'warning'
    default:               return 'info'
  }
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
    triggerAutoSync() // Auto sync on load/refresh
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

// Watch for route changes to trigger auto sync
watch(() => route.path, () => {
  if (authStore.isAuthenticated) {
    triggerAutoSync()
  }
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
  if (tokenCheckTimer) clearInterval(tokenCheckTimer)
})
</script>

<style>
.child-nav-item.v-list-item {
  padding-inline-start: 16px !important;
}
</style>
