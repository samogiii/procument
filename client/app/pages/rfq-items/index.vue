<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">RFQ Items</h1>
      <v-spacer />
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search..."
            single-line
            hide-details
            class="flex-grow-1 mx-2"
            style="min-width: 180px;"
          />
          <v-text-field
            v-model="pnSearch"
            prepend-inner-icon="mdi-barcode"
            label="Part Number"
            single-line
            hide-details
            clearable
            class="mx-2"
            style="min-width: 160px; max-width: 260px;"
          />
          <v-select
            v-model="statusFilter"
            :items="statusSelectItems"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            class="mr-2"
            style="min-width: 120px; max-width: 200px;"
          >
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllStatuses ? 'Show available only' : 'Show all statuses'"
                :prepend-icon="showAllStatuses ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllStatuses = !showAllStatuses"
              />
            </template>
          </v-select>
          <v-select
            v-model="userFilter"
            :items="userOptions"
            item-title="name"
            item-value="id"
            label="User"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            class="mx-2"
            style="min-width: 140px; max-width: 240px;"
          >
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllUsers ? 'Show available only' : 'Show all users'"
                :prepend-icon="showAllUsers ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllUsers = !showAllUsers"
              />
            </template>
          </v-select>
          <v-text-field
            v-model="customerSearch"
            prepend-inner-icon="mdi-domain"
            label="Customer"
            single-line
            hide-details
            clearable
            style="min-width: 140px; max-width: 260px;"
          />
          <v-btn
            v-if="hasActiveFilters"
            variant="tonal"
            color="error"
            size="small"
            prepend-icon="mdi-filter-off"
            class="align-self-center"
            @click="clearFilters"
          >
            Clear
          </v-btn>
        </div>

        <v-data-table-server
          :headers="headers"
          :items="serverItems"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
          @update:options="onTableOptions"
          @click:row="(_: any, { item }: any) => navigateTo(`/rfqs/${item.rfqId}?itemId=${item.itemId}`)"
        >
          <template #item.status="{ item }">
            <v-chip
              size="small"
              :color="rfqStatusColor(item.status || 'Open')"
              variant="tonal"
            >
              {{ item.status || 'Open' }}
            </v-chip>
          </template>
          <template #item.assignedUsers="{ item }">
            <div class="d-flex flex-wrap gap-1">
              <v-chip
                v-for="user in item.assignedUsers"
                :key="user.id"
                size="x-small"
                color="primary"
                variant="tonal"
                prepend-icon="mdi-account"
              >
                {{ user.name }}
              </v-chip>
              <span v-if="!item.assignedUsers?.length" class="text-medium-emphasis">—</span>
            </div>
          </template>
          <template #item.leadTime="{ item }">
            <span :class="{ 'text-error font-weight-bold': isLeadTimeExpired(item.leadTime) }" :style="isLeadTimeUrgent(item.leadTime) ? 'font-weight: 600;' : ''">
              {{ new Date(item.leadTime).toLocaleDateString() }}
              <v-icon v-if="isLeadTimeUrgent(item.leadTime)" icon="mdi-alert" size="14" color="warning" class="ml-1" title="Lead time expires within 3 days" />
              <v-icon v-else-if="isLeadTimeExpired(item.leadTime)" icon="mdi-alert-circle" size="14" color="error" class="ml-1" title="Lead time has expired" />
            </span>
          </template>
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>
          <template #item.customerName="{ item }">
            {{ item.customerName }}<span v-if="item.customerCode" class="text-medium-emphasis ml-1">({{ item.customerCode }})</span>
          </template>
        </v-data-table-server>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const { statusColor: rfqStatusColor } = useStatusColor()

const ALL_RFQ_STATUSES = ['Open', 'In Progress', 'Quoted', 'No Quote', 'Closed', 'Cancelled']

// ─── Filters ───
const search = ref('')
const pnSearch = ref('')
const statusFilter = ref<string[]>([])
const userFilter = ref<number[]>([])
const customerSearch = ref('')

const hasActiveFilters = computed(() =>
  search.value || pnSearch.value || statusFilter.value.length > 0
  || userFilter.value.length > 0 || customerSearch.value
)

function clearFilters() {
  search.value = ''
  pnSearch.value = ''
  statusFilter.value = []
  userFilter.value = []
  customerSearch.value = ''
}

// ─── Server-side data ───
const serverItems = ref<any[]>([])
const totalItems = ref(0)
const loading = ref(false)
const currentOptions = ref({ page: 1, itemsPerPage: 50 })

// Debounced text refs
const dSearch = ref('')
const dPn = ref('')
const dCustomer = ref('')
let searchTimer: any = null
let pnTimer: any = null
let customerTimer: any = null

watch(search, val => { if (searchTimer) clearTimeout(searchTimer); searchTimer = setTimeout(() => { dSearch.value = val; reload() }, 350) })
watch(pnSearch, val => { if (pnTimer) clearTimeout(pnTimer); pnTimer = setTimeout(() => { dPn.value = val ?? ''; reload() }, 350) })
watch(customerSearch, val => { if (customerTimer) clearTimeout(customerTimer); customerTimer = setTimeout(() => { dCustomer.value = val ?? ''; reload() }, 350) })
watch(statusFilter, () => reload())
watch(userFilter, () => reload())

function reload() {
  onTableOptions({ ...currentOptions.value, page: 1 })
  // Re-narrow the remaining filters to what the new result set still contains.
  cfOptions.refreshDebounced()
}

async function onTableOptions(opts: { page: number; itemsPerPage: number }) {
  currentOptions.value = { page: opts.page, itemsPerPage: opts.itemsPerPage }
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(opts.page), pageSize: String(opts.itemsPerPage) })
    if (dSearch.value) params.set('search', dSearch.value)
    if (dPn.value) params.set('pnSearch', dPn.value)
    if (dCustomer.value) params.set('customerSearch', dCustomer.value)
    statusFilter.value.forEach(s => params.append('statuses', s))
    userFilter.value.forEach(id => params.append('userIds', String(id)))
    const res = await api.get<any>(`/rfqs/items?${params}`)
    serverItems.value = res.items ?? res.Items ?? []
    totalItems.value = res.totalCount ?? res.TotalCount ?? serverItems.value.length
  } finally {
    loading.value = false
  }
}

/**
 * ─── Cascading filter options ───
 * `available` holds the values that still return rows once the *other* filters are
 * applied, `all` the unfiltered lists. Each dropdown shows available-only by default
 * with a "Show all" row, so a second filter never offers dead-end values.
 */
type ItemOptions = { statuses: string[]; users: { id: number; name: string }[] }
const EMPTY_ITEM_OPTIONS: ItemOptions = { statuses: [], users: [] }

const cfOptions = useCascadingOptions<ItemOptions>(
  async (cascading) => {
    const params = new URLSearchParams()
    if (cascading) {
      if (dSearch.value) params.set('search', dSearch.value)
      if (dPn.value) params.set('pnSearch', dPn.value)
      if (dCustomer.value) params.set('customerSearch', dCustomer.value)
      statusFilter.value.forEach(s => params.append('statuses', s))
      userFilter.value.forEach(id => params.append('userIds', String(id)))
    }
    const qs = params.toString()
    const res = await api.get<any>(`/rfqs/items/filter-options${qs ? `?${qs}` : ''}`)
    return {
      statuses: res.statuses || [],
      users: (res.users || []).map((u: any) => ({ id: u.id, name: u.name })),
    }
  },
  EMPTY_ITEM_OPTIONS,
)

const showAllStatuses = ref(false)
const showAllUsers = ref(false)

// Full status list = the hardcoded set plus anything the data actually contains.
const allStatusOptions = computed(() =>
  [...new Set([...ALL_RFQ_STATUSES, ...cfOptions.all.value.statuses])])
const statusSelectItems = computed(() => {
  if (showAllStatuses.value) return allStatusOptions.value
  const available = new Set(cfOptions.available.value.statuses)
  // Keep selected values listed, or the chip could not be removed from the menu.
  return allStatusOptions.value.filter(s => available.has(s) || statusFilter.value.includes(s))
})

const allUserOptions = computed(() => cfOptions.all.value.users)
const userOptions = computed(() => {
  if (showAllUsers.value) return allUserOptions.value
  const available = cfOptions.available.value.users
  const extra = allUserOptions.value.filter(u => userFilter.value.includes(u.id) && !available.some(a => a.id === u.id))
  return extra.length ? [...available, ...extra] : available
})

const headers = [
  { title: 'RFQ #', key: 'rfqId', width: '80px' },
  { title: 'RFQ Name', key: 'rfqName' },
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Description', key: 'description' },
  { title: 'Qty', key: 'qty', width: '80px' },
  { title: 'Condition', key: 'condition', width: '100px' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Status', key: 'status', width: '110px' },
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  { title: 'Deadline', key: 'leadTime' },
  { title: 'Received Date', key: 'createdAt' },
]

function isLeadTimeUrgent(dateStr: string) {
  if (!dateStr) return false
  const diff = new Date(dateStr).getTime() - Date.now()
  const daysLeft = diff / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 3
}

function isLeadTimeExpired(dateStr: string) {
  if (!dateStr) return false
  return new Date(dateStr).getTime() < Date.now()
}

onMounted(() => {
  cfOptions.init()
})
</script>
