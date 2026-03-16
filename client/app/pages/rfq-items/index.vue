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
              :color="item.status === 'Closed' ? 'success' : item.status === 'In Progress' ? 'warning' : 'info'"
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
            <span :style="isLeadTimeUrgent(item.leadTime) ? ' font-weight: 600;' : ''">
              {{ new Date(item.leadTime).toLocaleDateString() }}
              <v-icon v-if="isLeadTimeUrgent(item.leadTime)" icon="mdi-alert" size="14" color="error" class="ml-1" />
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

// Guard: admin only
// if (!authStore.isAdmin) {
//   navigateTo('/dashboard')
// }

const search = ref('')
const loading = ref(false)
const allItems = ref<any[]>([])
const statusFilter = ref<string[]>([])
const userFilter = ref<number[]>([])
const customerFilter = ref<string[]>([])
const partNumberFilter = ref<string[]>([])

const statusOptions = ['Open', 'In Progress', 'Closed']

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
  return diff > 0 && diff < 3 * 24 * 60 * 60 * 1000
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
