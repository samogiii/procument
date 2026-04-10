<template>
  <div>
    <div class="d-flex align-center mb-6">
      <div>
        <h1 class="text-h5 text-sm-h4 font-weight-bold">Dashboard</h1>
        <p class="text-body-2 text-medium-emphasis mt-1">
          {{ isAdmin ? (selectedUserId ? `Viewing ${selectedUserName}'s statistics` : 'Global overview of your procurement pipeline') : 'Your procurement overview' }}
        </p>
      </div>
      <v-spacer />
      <v-select
        v-if="isAdmin"
        v-model="selectedUserId"
        :items="userFilterItems"
        item-title="text"
        item-value="value"
        density="compact"
        variant="outlined"
        hide-details
        prepend-inner-icon="mdi-account-filter"
        style="max-width: 220px;"
        class="mr-3"
      />
      <v-chip v-if="isAdmin" color="primary" variant="flat" prepend-icon="mdi-shield-crown" size="small">Admin</v-chip>
    </div>

    <div v-if="loading" class="d-flex justify-center align-center" style="min-height: 300px;">
      <v-progress-circular indeterminate color="primary" size="48" />
    </div>

    <template v-else>
      <v-progress-linear v-if="refreshing" indeterminate color="primary" class="mb-2" height="3" />
      <!-- Tabs for admin -->
      <v-tabs v-if="isAdmin" v-model="activeTab" color="primary" class="mb-4">
        <v-tab value="overview" prepend-icon="mdi-chart-box-outline">Overview</v-tab>
        <v-tab value="po-items" prepend-icon="mdi-format-list-bulleted">All PO Items</v-tab>
      </v-tabs>

      <!-- ═══ TAB: OVERVIEW ═══ -->
      <div v-show="activeTab === 'overview' || !isAdmin">
      <!-- KPI Cards -->
      <v-row class="mb-2">
        <v-col v-for="s in statCards" :key="s.title" cols="6" sm="4" :md="isAdmin ? 2 : 3">
          <v-card class="glass-card pa-4 text-center cursor-pointer" :style="{ borderTop: '3px solid ' + s.borderColor }" @click="navigateTo(s.to)">
            <v-icon :icon="s.icon" :color="s.color" size="28" class="mb-2" />
            <div class="text-h5 font-weight-bold">{{ s.value }}</div>
            <div class="text-caption text-medium-emphasis">{{ s.title }}</div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Financial Highlights -->
      <v-row class="mb-2">
        <v-col cols="12" sm="6" md="3">
          <v-card class="glass-card pa-4 cursor-pointer" @click="navigateTo('/quotes')">
            <div class="d-flex align-center mb-2">
              <v-avatar color="success" size="36" variant="tonal"><v-icon icon="mdi-cash-multiple" size="20" /></v-avatar>
              <div class="ml-3">
                <div class="text-caption text-medium-emphasis">Sent Quotes</div>
                <div class="text-h6 font-weight-bold">${{ fmtNum(d.totalQuoteValue) }}</div>
              </div>
            </div>
            <div class="text-caption text-success clickable-sub" @click.stop="navigateTo('/quotes?status=Accepted')">Accepted: ${{ fmtNum(d.acceptedQuoteValue) }}</div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" md="3">
          <v-card class="glass-card pa-4 cursor-pointer" @click="navigateTo('/purchase-orders')">
            <div class="d-flex align-center mb-2">
              <v-avatar color="info" size="36" variant="tonal"><v-icon icon="mdi-package-variant" size="20" /></v-avatar>
              <div class="ml-3">
                <div class="text-caption text-medium-emphasis">PO Value</div>
                <div class="text-h6 font-weight-bold">${{ fmtNum(d.totalPOValue) }}</div>
              </div>
            </div>
            <div class="text-caption text-info">{{ d.acceptedPOs }}/{{ d.totalPOs }} accepted</div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" md="3">
          <v-card class="glass-card pa-4 cursor-pointer" @click="navigateTo('/invoices')">
            <div class="d-flex align-center mb-2">
              <v-avatar color="warning" size="36" variant="tonal"><v-icon icon="mdi-receipt-text" size="20" /></v-avatar>
              <div class="ml-3">
                <div class="text-caption text-medium-emphasis">Invoice Value</div>
                <div class="text-h6 font-weight-bold">${{ fmtNum(d.totalInvoiceValue) }}</div>
              </div>
            </div>
            <div class="text-caption text-warning clickable-sub" @click.stop="navigateTo('/invoices?status=Paid')">Paid: ${{ fmtNum(d.paidInvoiceValue) }}</div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" md="3">
          <v-card class="glass-card pa-4">
            <div class="d-flex align-center mb-2">
              <v-avatar color="secondary" size="36" variant="tonal"><v-icon icon="mdi-trending-up" size="20" /></v-avatar>
              <div class="ml-3">
                <div class="text-caption text-medium-emphasis">Collection Rate</div>
                <div class="text-h6 font-weight-bold">{{ collectionRate }}%</div>
              </div>
            </div>
            <v-progress-linear :model-value="collectionRate" color="secondary" rounded height="6" class="mt-1" />
          </v-card>
        </v-col>
      </v-row>

      <!-- Monthly Revenue Trend -->
      <v-row class="mb-2">
        <v-col cols="12" lg="8">
          <v-card class="glass-card pa-4">
            <div class="text-subtitle-1 font-weight-bold mb-3">
              <v-icon icon="mdi-chart-line" class="mr-1" size="20" /> Monthly Revenue Trend
            </div>
            <client-only>
              <apexchart type="area" height="300" :options="revenueTrendOpts" :series="revenueTrendSeries" />
            </client-only>
          </v-card>
        </v-col>
        <v-col cols="12" lg="4">
          <v-card class="glass-card pa-4">
            <div class="text-subtitle-1 font-weight-bold mb-3">
              <v-icon icon="mdi-chart-donut" class="mr-1" size="20" /> Quote Status
            </div>
            <client-only>
              <apexchart type="donut" height="280" :options="quoteDonutOpts" :series="quoteDonutSeries" />
            </client-only>
          </v-card>
        </v-col>
      </v-row>

      <!-- Status Donuts + Monthly Volume -->
      <v-row class="mb-2">
        <v-col cols="12" sm="6" lg="4">
          <v-card class="glass-card pa-4">
            <div class="text-subtitle-1 font-weight-bold mb-3">
              <v-icon icon="mdi-chart-donut-variant" class="mr-1" size="20" /> PO Status
            </div>
            <client-only>
              <apexchart type="donut" height="260" :options="poDonutOpts" :series="poDonutSeries" />
            </client-only>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" lg="4">
          <v-card class="glass-card pa-4">
            <div class="text-subtitle-1 font-weight-bold mb-3">
              <v-icon icon="mdi-chart-donut-variant" class="mr-1" size="20" /> Invoice Status
            </div>
            <client-only>
              <apexchart type="donut" height="260" :options="invoiceDonutOpts" :series="invoiceDonutSeries" />
            </client-only>
          </v-card>
        </v-col>
        <v-col cols="12" lg="4">
          <v-card class="glass-card pa-4">
            <div class="text-subtitle-1 font-weight-bold mb-3">
              <v-icon icon="mdi-chart-bar" class="mr-1" size="20" /> Monthly Volume
            </div>
            <client-only>
              <apexchart type="bar" height="260" :options="volumeBarOpts" :series="volumeBarSeries" />
            </client-only>
          </v-card>
        </v-col>
      </v-row>

      <!-- Admin Only: Per-User + Top Suppliers + Activity -->
      <template v-if="isAdmin">
        <v-row class="mb-2">
          <v-col cols="12" lg="6">
            <v-card class="glass-card pa-4">
              <div class="text-subtitle-1 font-weight-bold mb-3">
                <v-icon icon="mdi-account-group" class="mr-1" size="20" /> Per-User Performance
              </div>
              <client-only>
                <apexchart type="bar" height="300" :options="userBarOpts" :series="userBarSeries" />
              </client-only>
            </v-card>
          </v-col>
          <v-col cols="12" lg="6">
            <v-card class="glass-card pa-4">
              <div class="text-subtitle-1 font-weight-bold mb-3">
                <v-icon icon="mdi-truck" class="mr-1" size="20" /> Top Suppliers by PO Value
              </div>
              <client-only>
                <apexchart type="bar" height="300" :options="supplierBarOpts" :series="supplierBarSeries" />
              </client-only>
            </v-card>
          </v-col>
        </v-row>

        <!-- Recent Activity -->
        <v-row>
          <v-col cols="12" md="8">
            <v-card class="glass-card">
              <v-card-title class="d-flex align-center text-subtitle-1">
                <v-icon icon="mdi-clock-outline" class="mr-2" size="20" /> Recent Activity
              </v-card-title>
              <v-card-text>
                <v-list v-if="recentActivity.length" bg-color="transparent" density="compact">
                  <v-list-item v-for="item in recentActivity" :key="item.id" :subtitle="formatTime(item.timestamp)">
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
                <div v-else class="text-center text-medium-emphasis pa-6">No recent activity</div>
              </v-card-text>
            </v-card>
          </v-col>
          <v-col cols="12" md="4">
            <v-card class="glass-card pa-4">
              <div class="text-subtitle-1 font-weight-bold mb-3">
                <v-icon icon="mdi-lightning-bolt" class="mr-1" size="20" /> Quick Actions
              </div>
              <v-btn color="primary" block class="mb-2" prepend-icon="mdi-plus" to="/rfqs">New RFQ</v-btn>
              <v-btn color="secondary" variant="outlined" block class="mb-2" prepend-icon="mdi-plus" to="/quotes">New Quote</v-btn>
              <v-btn variant="outlined" block prepend-icon="mdi-plus" to="/purchase-orders">New PO</v-btn>
            </v-card>
          </v-col>
        </v-row>
      </template>
      </div><!-- end overview tab -->

      <!-- ═══ TAB: ALL PO ITEMS (admin only) ═══ -->
      <div v-if="isAdmin" v-show="activeTab === 'po-items'">
        <v-card class="glass-card">
          <v-card-title class="d-flex align-center pa-4">
            <v-icon icon="mdi-format-list-bulleted" class="mr-2" size="22" />
            All PO Items
            <v-chip class="ml-2" size="small" color="primary" variant="tonal">{{ poItems.length }}</v-chip>
            <v-spacer />
            <v-text-field
              v-model="poItemSearch"
              prepend-inner-icon="mdi-magnify"
              placeholder="Search PO items..."
              hide-details
              density="compact"
              variant="outlined"
              clearable
              style="max-width: 320px;"
              class="mr-2"
            />
          </v-card-title>
          <v-divider />
          <v-data-table
            :headers="poItemHeaders"
            :items="poItems"
            :search="poItemSearch"
            :loading="poItemsLoading"
            density="compact"
            hover
            class="po-items-table"
            :items-per-page="50"
            :sort-by="[{ key: 'poNumber', order: 'desc' }]"
          >
            <template #item.poNumber="{ item }">
              <NuxtLink :to="`/purchase-orders/${item.poId}`" class="text-primary font-weight-medium text-decoration-none">
                {{ item.poNumber }}
              </NuxtLink>
            </template>
            <template #item.poStatus="{ item }">
              <v-chip :color="poStatusColor(item.poStatus)" size="x-small" variant="flat">{{ item.poStatus }}</v-chip>
            </template>
            <template #item.buyPrice="{ item }">
              ${{ formatPrice(item.buyPrice) }}
            </template>
            <template #item.totalBuyPrice="{ item }">
              ${{ formatPrice(item.totalBuyPrice) }}
            </template>
            <template #item.sellPrice="{ item }">
              <span :class="item.sellPrice ? '' : 'text-medium-emphasis'">
                {{ item.sellPrice ? '$' + formatPrice(item.sellPrice) : '—' }}
              </span>
            </template>
            <template #item.totalSellPrice="{ item }">
              <span :class="item.totalSellPrice ? '' : 'text-medium-emphasis'">
                {{ item.totalSellPrice ? '$' + formatPrice(item.totalSellPrice) : '—' }}
              </span>
            </template>
            <template #item.altPart="{ item }">
              <v-chip v-if="item.altPart" size="x-small" color="info" variant="tonal">{{ item.altPart }}</v-chip>
              <span v-else class="text-medium-emphasis">—</span>
            </template>
            <template #item.priority="{ item }">
              <v-chip v-if="item.priority" size="x-small" :color="priorityColor(item.priority)" variant="flat">{{ item.priority }}</v-chip>
              <span v-else class="text-medium-emphasis">—</span>
            </template>
            <template #item.notes="{ item }">
              <span class="text-truncate d-inline-block" style="max-width: 150px;" :title="item.notes">{{ item.notes || '—' }}</span>
            </template>
          </v-data-table>
        </v-card>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const loading = ref(true)
const d = ref<any>({})
const activeTab = ref('overview')
const recentActivity = ref<any[]>([])

// ─── User filter (admin) ───
const users = ref<any[]>([])
const selectedUserId = ref<number>(0)
const refreshing = ref(false)

const userFilterItems = computed(() => [
  { text: 'All Users', value: 0 },
  ...users.value.map((u: any) => ({ text: `${u.name} (${u.role})`, value: u.id }))
])

const selectedUserName = computed(() => {
  if (!selectedUserId.value) return ''
  const u = users.value.find((u: any) => u.id === selectedUserId.value)
  return u?.name || ''
})

async function loadDashboard(initial = false) {
  if (initial) loading.value = true
  else refreshing.value = true
  try {
    const params = selectedUserId.value ? `?userId=${selectedUserId.value}` : ''
    const data = await api.get<any>(`/dashboard${params}`)
    d.value = data
    recentActivity.value = data.recentActivity ?? []
  } catch (e) {
    console.error('[Dashboard] Failed to load stats', e)
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

watch(selectedUserId, () => {
  loadDashboard()
})

// ─── PO Items tab state ───
const poItems = ref<any[]>([])
const poItemsLoading = ref(false)
const poItemSearch = ref('')
const poItemHeaders = [
  { title: 'Part Number', key: 'partNumber', sortable: true },
  { title: 'Description', key: 'partDescription', sortable: true },
  { title: 'Supplier', key: 'supplierName', sortable: true },
  { title: 'PO Number', key: 'poNumber', sortable: true },
  { title: 'RFQ Ref', key: 'rfqReference', sortable: true },
  { title: 'Lead Time', key: 'leadTime', sortable: true },
  { title: 'Buy Price', key: 'buyPrice', sortable: true, align: 'end' as const },
  { title: 'Sell Price', key: 'sellPrice', sortable: true, align: 'end' as const },
  { title: 'Total Sell', key: 'totalSellPrice', sortable: true, align: 'end' as const },
  { title: 'Status', key: 'poStatus', sortable: true },
  { title: 'Qty', key: 'qty', sortable: true, align: 'center' as const },
  { title: 'Condition', key: 'condition', sortable: true },
  { title: 'Customer', key: 'customerName', sortable: true },
  { title: 'Alt Part', key: 'altPart', sortable: true },
  { title: 'Priority', key: 'priority', sortable: true },
  { title: 'Notes', key: 'notes', sortable: false },
  { title: 'Assigned To', key: 'assignedTo', sortable: true },
]

async function loadPOItems() {
  if (poItems.value.length > 0) return
  poItemsLoading.value = true
  try {
    poItems.value = await api.get<any[]>('/dashboard/po-items')
  } catch (e) {
    console.error('[Dashboard] Failed to load PO items', e)
  } finally {
    poItemsLoading.value = false
  }
}

watch(activeTab, (tab) => {
  if (tab === 'po-items') loadPOItems()
})

const monthNames = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec']

// ─── Theme colors (reactive) ───
const { isDark } = useAppTheme()
const C = computed(() => isDark.value ? {
  bg: '#0D1117', surface: '#161B22', surfaceVar: '#1C2333',
  primary: '#1565C0', secondary: '#00BCD4', accent: '#FF6D00',
  error: '#EF5350', warning: '#FF6D00', info: '#29B6F6', success: '#66BB6A',
  text: '#E6EDF3', muted: '#8B949E',
} : {
  bg: '#F5F7FA', surface: '#FFFFFF', surfaceVar: '#EEF2F6',
  primary: '#1565C0', secondary: '#0097A7', accent: '#E65100',
  error: '#D32F2F', warning: '#E65100', info: '#0288D1', success: '#2E7D32',
  text: '#1A2332', muted: '#64748b',
})

const statusColors: Record<string, string> = {
  Draft: '#8B949E', Sent: '#29B6F6', Accepted: '#66BB6A', Rejected: '#EF5350',
  Completed: '#00BCD4', Cancelled: '#6B7280', Pending: '#FF6D00', Paid: '#66BB6A', Overdue: '#EF5350',
  Accept: '#66BB6A', 'Waiting For Payment': '#FF9800', 'Payment Done': '#009688',
  'Ship To Warehouse 1': '#3F51B5', 'Ship To Warehouse 2': '#673AB7', 'Ship To Warehouse 3': '#607D8B',
  'Ship To Customer': '#FF5722',
  Open: '#29B6F6', 'In Progress': '#FFC107', 'No Quote': '#7E57C2', Quoted: '#FF9800', Closed: '#78909C',
}

// ─── Computed KPIs ───
const statCards = computed(() => {
  const v = d.value
  const c = C.value
  const base = [
    { title: 'RFQs', value: v.totalRfqs ?? 0, icon: 'mdi-file-document-outline', color: 'primary', borderColor: c.primary, to: '/rfqs' },
    { title: 'Assigned RFQs', value: v.rfqCount ?? 0, icon: 'mdi-file-check-outline', color: 'purple', borderColor: '#9C27B0', to: '/rfqs' },
    { title: 'Quotes', value: v.totalQuotes ?? 0, icon: 'mdi-currency-usd', color: 'info', borderColor: c.info, to: '/quotes' },
    { title: 'Invoices', value: v.totalInvoices ?? 0, icon: 'mdi-receipt-text-outline', color: 'warning', borderColor: c.warning, to: '/invoices' },
    { title: 'POs', value: v.totalPOs ?? 0, icon: 'mdi-package-variant-closed', color: 'secondary', borderColor: c.secondary, to: '/purchase-orders' },
    { title: 'Pending RFQs', value: v.pendingRfqs ?? 0, icon: 'mdi-file-clock-outline', color: 'error', borderColor: c.error, to: '/rfqs?status=Open' },
  ]
  if (isAdmin.value) base.push({ title: 'Users', value: v.totalUsers ?? 0, icon: 'mdi-account-group', color: 'success', borderColor: c.success, to: '/users' })
  return base
})

const collectionRate = computed(() => {
  const total = d.value.totalInvoiceValue || 0
  const paid = d.value.paidInvoiceValue || 0
  return total > 0 ? Math.round((paid / total) * 100) : 0
})

// ─── Chart base config ───
const chartBase = computed(() => {
  const c = C.value
  return {
    chart: { background: 'transparent', foreColor: c.text, toolbar: { show: false }, animations: { enabled: true, easing: 'easeinout', speed: 800, dynamicAnimation: { speed: 400 } } },
    theme: { mode: (isDark.value ? 'dark' : 'light') as 'dark' | 'light' },
    grid: { borderColor: isDark.value ? '#21262d' : '#e2e8f0', strokeDashArray: 4 },
    tooltip: { theme: isDark.value ? 'dark' : 'light', style: { fontSize: '12px' } },
    legend: { labels: { colors: c.muted }, fontSize: '12px' },
  }
})

// ─── Revenue Trend (area) ───
function getMonthLabels(data: any[]) {
  return data.map((m: any) => monthNames[(m.month || 1) - 1] + ' ' + String(m.year).slice(2))
}

const revenueTrendOpts = computed(() => {
  const labels = getMonthLabels(d.value.monthlyQuotes || [])
  const base = chartBase.value
  const c = C.value
  return {
    ...base,
    chart: { ...base.chart, type: 'area', height: 300, sparkline: { enabled: false } },
    xaxis: { categories: labels, labels: { style: { colors: c.muted, fontSize: '11px' } } },
    yaxis: { labels: { style: { colors: c.muted }, formatter: (v: number) => '$' + fmtNum(v) } },
    stroke: { curve: 'smooth', width: 3 },
    fill: { type: 'gradient', gradient: { shadeIntensity: 1, opacityFrom: 0.4, opacityTo: 0.05, stops: [0, 100] } },
    colors: [c.success, c.info, c.warning],
    dataLabels: { enabled: false },
  }
})

const revenueTrendSeries = computed(() => {
  const mq = d.value.monthlyQuotes || []
  const mp = d.value.monthlyPOs || []
  const mi = d.value.monthlyInvoices || []
  return [
    { name: 'Quotes', data: mq.map((m: any) => Number(m.totalValue) || 0) },
    { name: 'POs', data: mp.map((m: any) => Number(m.totalValue) || 0) },
    { name: 'Invoices', data: mi.map((m: any) => Number(m.totalValue) || 0) },
  ]
})

// ─── Donut builder ───
function buildDonut(dist: any[], title: string) {
  const labels = (dist || []).map((s: any) => s.status || 'Unknown')
  const series = (dist || []).map((s: any) => s.count)
  const colors = labels.map((l: string) => statusColors[l] || '#6B7280')
  const base = chartBase.value
  const c = C.value
  const opts = {
    ...base,
    chart: { ...base.chart, type: 'donut' },
    labels, colors,
    plotOptions: { pie: { donut: { size: '65%', labels: { show: true, total: { show: true, label: 'Total', color: c.muted, fontSize: '13px', formatter: (w: any) => w.globals.seriesTotals.reduce((a: number, b: number) => a + b, 0) } } } } },
    stroke: { width: 2, colors: [c.surface] },
    dataLabels: { enabled: false },
  }
  return { opts, series }
}

const quoteDonutOpts = computed(() => buildDonut(d.value.quoteStatusDistribution, 'Quotes').opts)
const quoteDonutSeries = computed(() => buildDonut(d.value.quoteStatusDistribution, 'Quotes').series)
const poDonutOpts = computed(() => buildDonut(d.value.poStatusDistribution, 'POs').opts)
const poDonutSeries = computed(() => buildDonut(d.value.poStatusDistribution, 'POs').series)
const invoiceDonutOpts = computed(() => buildDonut(d.value.invoiceStatusDistribution, 'Invoices').opts)
const invoiceDonutSeries = computed(() => buildDonut(d.value.invoiceStatusDistribution, 'Invoices').series)

// ─── Volume Bar ───
const volumeBarOpts = computed(() => {
  const labels = getMonthLabels(d.value.monthlyQuotes || [])
  const base = chartBase.value
  const c = C.value
  return {
    ...base,
    chart: { ...base.chart, type: 'bar', stacked: true },
    xaxis: { categories: labels, labels: { style: { colors: c.muted, fontSize: '11px' } } },
    yaxis: { labels: { style: { colors: c.muted } } },
    colors: [c.success, c.info, c.warning],
    plotOptions: { bar: { borderRadius: 4, columnWidth: '55%' } },
    dataLabels: { enabled: false },
  }
})

const volumeBarSeries = computed(() => [
  { name: 'Quotes', data: (d.value.monthlyQuotes || []).map((m: any) => m.count) },
  { name: 'POs', data: (d.value.monthlyPOs || []).map((m: any) => m.count) },
  { name: 'Invoices', data: (d.value.monthlyInvoices || []).map((m: any) => m.count) },
])

// ─── Per-User Bar (admin) ───
const userBarOpts = computed(() => {
  const users = d.value.userQuoteStats || []
  const base = chartBase.value
  const c = C.value
  return {
    ...base,
    chart: { ...base.chart, type: 'bar' },
    xaxis: { categories: users.map((u: any) => u.userName), labels: { style: { colors: c.muted, fontSize: '11px' } } },
    yaxis: { labels: { style: { colors: c.muted }, formatter: (v: number) => '$' + fmtNum(v) } },
    colors: [c.primary, c.success, c.error, c.warning],
    plotOptions: { bar: { borderRadius: 4, columnWidth: '60%' } },
    dataLabels: { enabled: false },
  }
})

const userBarSeries = computed(() => {
  const users = d.value.userQuoteStats || []
  return [
    { name: 'Total Value', data: users.map((u: any) => Number(u.totalValue) || 0) },
    { name: 'Accepted', data: users.map((u: any) => u.acceptedCount) },
    { name: 'Rejected', data: users.map((u: any) => u.rejectedCount) },
    { name: 'RFQ Count', data: users.map((u: any) => u.rfqCount || 0) },
  ]
})

// ─── Top Suppliers Bar (admin) ───
const supplierBarOpts = computed(() => {
  const sups = d.value.topSuppliers || []
  const base = chartBase.value
  const c = C.value
  return {
    ...base,
    chart: { ...base.chart, type: 'bar' },
    xaxis: { categories: sups.map((s: any) => s.supplierName || 'Unknown'), labels: { style: { colors: c.muted, fontSize: '11px' }, rotate: -30, trim: true, maxHeight: 80 } },
    yaxis: { labels: { style: { colors: c.muted }, formatter: (v: number) => '$' + fmtNum(v) } },
    colors: [c.secondary],
    plotOptions: { bar: { borderRadius: 4, columnWidth: '50%', distributed: true } },
    dataLabels: { enabled: false },
    legend: { show: false },
  }
})

const supplierBarSeries = computed(() => [
  { name: 'PO Value', data: (d.value.topSuppliers || []).map((s: any) => Number(s.totalValue) || 0) },
])

// ─── Load data ───
onMounted(async () => {
  // Load users list for admin filter
  if (isAdmin.value) {
    try {
      users.value = await api.get<any[]>('/dashboard/users')
    } catch (e) {
      console.error('[Dashboard] Failed to load users', e)
    }
  }
  await loadDashboard(true)
})

// ─── Helpers ───
function fmtNum(v: number | undefined): string {
  if (!v) return '0'
  if (v >= 1_000_000) return (v / 1_000_000).toFixed(1) + 'M'
  if (v >= 1_000) return (v / 1_000).toFixed(1) + 'K'
  return v.toLocaleString()
}

function actionIcon(action: string): string {
  const m: Record<string, string> = { Create: 'mdi-plus-circle-outline', Update: 'mdi-pencil-outline', Delete: 'mdi-delete-outline', Login: 'mdi-login', Logout: 'mdi-logout', UpdateStatus: 'mdi-swap-horizontal' }
  return m[action] || 'mdi-information-outline'
}

function actionColor(action: string): string {
  const m: Record<string, string> = { Create: 'success', Update: 'info', Delete: 'error', Login: 'primary', Logout: 'grey', UpdateStatus: 'warning' }
  return m[action] || 'grey'
}

function poStatusColor(status: string): string {
  const m: Record<string, string> = {
    Sent: 'info', Accept: 'success', 'Waiting For Payment': 'warning', 'Payment Done': 'teal',
    'Ship To Warehouse 1': 'indigo', 'Ship To Warehouse 2': 'deep-purple', 'Ship To Warehouse 3': 'blue-grey',
    'Ship To Customer': 'orange', Completed: 'green', Cancelled: 'grey',
  }
  return m[status] || 'grey'
}

function priorityColor(p: string): string {
  const lp = (p || '').toLowerCase()
  if (lp === 'high' || lp === 'urgent') return 'error'
  if (lp === 'medium') return 'warning'
  if (lp === 'low') return 'success'
  return 'grey'
}

function formatTime(ts: string): string {
  if (!ts) return ''
  const diff = Date.now() - new Date(ts).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  return `${Math.floor(hrs / 24)}d ago`
}
</script>

<style scoped>
.clickable-sub {
  cursor: pointer;
  border-radius: 4px;
  padding: 2px 4px;
  margin: -2px -4px;
  transition: background 0.15s;
}
.clickable-sub:hover {
  background: rgba(var(--v-theme-on-surface), 0.08);
  text-decoration: underline;
}
</style>
