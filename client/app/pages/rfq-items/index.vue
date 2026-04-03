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
          <v-autocomplete
            v-model="partNumberFilter"
            :items="partNumberOptions"
            label="Part Number"
            variant="outlined"
            hide-details
            single-line
            multiple
            chips
            closable-chips
            clearable
            class="mx-2"
            style="min-width: 160px; max-width: 260px;"
          />
          <v-select
            v-model="statusFilter"
            :items="statusOptions"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            class="mr-2"
            style="min-width: 120px; max-width: 200px;"
          />
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
          />
          <v-select
            v-model="customerFilter"
            :items="customerOptions"
            label="Customer"
            hide-details
            multiple
            chips
            closable-chips
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

        <v-data-table
          :headers="headers"
          :items="filteredItems"
          :search="search"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
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
        </v-data-table>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const { statusColor: rfqStatusColor } = useStatusColor()

// Guard: admin only
// if (!authStore.isAdmin) {
//   navigateTo('/dashboard')
// }

const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('rfq-items', {
  search: '',
  status: [] as string[],
  user: [] as number[],
  customer: [] as string[],
  partNumber: [] as string[],
})
const search = pf.search
const loading = ref(false)
const allItems = ref<any[]>([])
const statusFilter = pf.status
const userFilter = pf.user
const customerFilter = pf.customer
const partNumberFilter = pf.partNumber

const statusOptions = ['Open', 'In Progress', 'No Quote', 'Quoted', 'Closed', 'Completed', 'Cancelled']

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
  { title: 'Lead Time', key: 'leadTime' },
  { title: 'Created', key: 'createdAt' },
]

const partNumberOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach((item: any) => { if (item.partNumberName) set.add(item.partNumberName) })
  return Array.from(set).sort()
})

const userOptions = computed(() => {
  const map = new Map<number, string>()
  allItems.value.forEach((item: any) => {
    ;(item.assignedUsers || []).forEach((u: any) => {
      if (u.id && u.name) map.set(u.id, u.name)
    })
  })
  return Array.from(map, ([id, name]) => ({ id, name }))
})

const customerOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach((item: any) => { if (item.customerName) set.add(item.customerName) })
  return Array.from(set).sort()
})

const filteredItems = computed(() => {
  let result = allItems.value
  if (statusFilter.value?.length) {
    result = result.filter((item: any) => statusFilter.value.includes(item.status || 'Open'))
  }
  if (userFilter.value?.length) {
    result = result.filter((item: any) =>
      (item.assignedUsers || []).some((u: any) => userFilter.value.includes(u.id))
    )
  }
  if (customerFilter.value?.length) {
    result = result.filter((item: any) => customerFilter.value.includes(item.customerName))
  }
  if (partNumberFilter.value?.length) {
    result = result.filter((item: any) => partNumberFilter.value.includes(item.partNumberName))
  }
  return result
})

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

onMounted(async () => {
  loading.value = true
  try {
    const rfqs = await api.get<any[]>('/rfqs')
    const flat: any[] = []
    ;(rfqs || []).forEach((rfq: any) => {
      const assignedUsers = [...(rfq.views || []), ...(rfq.edits || [])]
        .filter((u: any, i: number, arr: any[]) => arr.findIndex((x: any) => x.id === u.id) === i)
      ;(rfq.items || []).forEach((item: any) => {
        flat.push({
          itemId: item.id,
          rfqId: rfq.id,
          rfqName: rfq.name,
          partNumberName: item.partNumberName,
          description: item.description || '—',
          qty: item.qty,
          condition: item.condition || '—',
          customerName: rfq.customerName,
          customerCode: rfq.customerCode,
          status: rfq.status || 'Open',
          assignedUsers,
          leadTime: rfq.leadTime,
          createdAt: rfq.createdAt,
        })
      })
    })
    allItems.value = flat
  } catch {}
  finally { loading.value = false }
})
</script>
