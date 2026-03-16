<template>
  <DataListPage
    title="Quotes"
    :headers="headers"
    api-url="/quotes"
    :status-options="['All', 'Draft', 'Sent', 'Accepted', 'Rejected']"
    detail-route="/quotes"
    :custom-filter="applyFilters"
  >
    <template #actions>
      <v-btn
        v-if="isAdmin"
        prepend-icon="mdi-shield-account"
        variant="tonal"
        @click="showBulkPerms = true"
      >
        Manage Permissions
      </v-btn>
    </template>

    <template #filters>
      <v-select
        v-model="userFilter"
        :items="userOptions"
        label="User"
        class="mx-2"
        hide-details
        multiple
        chips
        closable-chips
        clearable
        style="min-width: 140px; max-width: 240px;"
      />
      <v-select
        v-model="customerFilter"
        :items="customerOptions"
        label="Customer"
        hide-details
        class="mx-2"
        multiple
        chips
        closable-chips
        clearable
        style="min-width: 140px; max-width: 260px;"
      />
      <!-- <v-text-field
        v-model="dateFrom"
        label="From"
        type="date"
        hide-details
        clearable
        style="min-width: 130px; max-width: 160px;"
      />
      <v-text-field
        v-model="dateTo"
        label="To"
        type="date"
        hide-details
        clearable
        style="min-width: 130px; max-width: 160px;"
      /> -->
    </template>

    <template #item.customerName="{ item }">
      <template v-if="isAdmin">{{ item.customerName }}<span v-if="item.customerCode" class="text-medium-emphasis ml-1">({{ item.customerCode }})</span></template>
      <template v-else>{{ item.customerCode || '—' }}</template>
    </template>

    <template #item.status="{ item }">
      <StatusChip :status="item.status" />
    </template>

    <template #item.totalAmount="{ item }">
      ${{ formatPrice(item.totalAmount) }}
    </template>

    <template #item.actions="{ item }">
      <v-btn icon="mdi-eye" variant="text" size="small" :to="`/quotes/${item.id}`" />
    </template>
  </DataListPage>

  <BulkPermissionManager v-model="showBulkPerms" entity-name="Quote" />
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const showBulkPerms = ref(false)

const userFilter = ref<string[]>([])
const customerFilter = ref<string[]>([])
const dateFrom = ref<string | null>(null)
const dateTo = ref<string | null>(null)

const users = ref<any[]>([])
const customers = ref<any[]>([])

onMounted(async () => {
  try {
    const res = await api.get<any>('/quotes?pageSize=9999')
    const items = Array.isArray(res) ? res : (res.items || res.Items || [])
    const userSet = new Map<string, string>()
    const custSet = new Set<string>()
    items.forEach((q: any) => {
      if (q.userName) userSet.set(q.userName, q.userName)
      if (q.customerName) custSet.add(q.customerName)
    })
    users.value = Array.from(userSet.values()).sort()
    customers.value = Array.from(custSet).sort()
  } catch {}
})

const userOptions = computed(() => users.value)
const customerOptions = computed(() => customers.value)

function applyFilters(items: any[]) {
  let result = items
  if (userFilter.value?.length) {
    result = result.filter((item: any) => userFilter.value.includes(item.userName))
  }
  if (customerFilter.value?.length) {
    result = result.filter((item: any) => customerFilter.value.includes(item.customerName))
  }
  if (dateFrom.value) {
    const from = new Date(dateFrom.value).getTime()
    result = result.filter((item: any) => new Date(item.createdAt).getTime() >= from)
  }
  if (dateTo.value) {
    const to = new Date(dateTo.value).getTime() + 86400000
    result = result.filter((item: any) => new Date(item.createdAt).getTime() < to)
  }
  return result
}

const headers = [
  { title: 'Quote #', key: 'quoteNumber' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]
</script>
