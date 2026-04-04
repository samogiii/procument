<template>
  <DataListPage
    title="Quotes"
    :headers="headers"
    api-url="/quotes"
    :status-options="['All', 'Draft', 'Sent', 'Accepted', 'Rejected']"
    detail-route="/quotes"
    :custom-filter="applyFilters"
    page-key="quotes"
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
      <v-text-field
        v-model="pnSearch"
        label="Search by P/N"
        prepend-inner-icon="mdi-cog-outline"
        hide-details
        clearable
        density="compact"
        variant="outlined"
        class="mx-2"
        style="min-width: 180px; max-width: 280px;"
        :loading="pnLoading"
        @update:model-value="searchByPN"
      />
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

    <template #item.createdAt="{ item }">
      {{ item.createdAt ? new Date(item.createdAt).toLocaleDateString() : '—' }}
    </template>

    <template #item.actions="{ item }">
      <v-btn icon="mdi-eye" variant="text" size="small" :to="`/quotes/${item.id}`" />
    </template>

    <template #tfoot="{ items }">
      <tr>
        <td colspan="3" class="text-right font-weight-bold" style="padding: 10px 12px; font-size: 13px;">
          Total
        </td>
        <td class="font-weight-bold" style="padding: 10px 12px; font-size: 13px; color: #4ade80;">
          ${{ formatPrice(items.reduce((sum: number, q: any) => sum + (Number(q.totalAmount) || 0), 0)) }}
        </td>
        <td colspan="4"></td>
      </tr>
    </template>
  </DataListPage>

  <BulkPermissionManager v-model="showBulkPerms" entity-name="Quote" />
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const showBulkPerms = ref(false)

const { filters: pf } = usePageFilters('quotes-extra', {
  user: [] as string[],
  customer: [] as string[],
})
const userFilter = pf.user
const customerFilter = pf.customer
const dateFrom = ref<string | null>(null)
const dateTo = ref<string | null>(null)

// P/N Search — filters datatable by matching quote IDs
const pnSearch = ref('')
const pnMatchedQuoteIds = ref<Set<number> | null>(null)
const pnLoading = ref(false)
let pnDebounce: any = null

function searchByPN(val: string | null) {
  clearTimeout(pnDebounce)
  if (!val || val.length < 2) {
    pnMatchedQuoteIds.value = null
    return
  }
  pnLoading.value = true
  pnDebounce = setTimeout(async () => {
    try {
      const results = await api.get<any[]>(`/quotes/search-by-pn?q=${encodeURIComponent(val)}`)
      pnMatchedQuoteIds.value = new Set((results || []).map((r: any) => r.quoteId))
    } catch {
      pnMatchedQuoteIds.value = new Set()
    } finally {
      pnLoading.value = false
    }
  }, 300)
}

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
  // P/N filter
  if (pnMatchedQuoteIds.value !== null) {
    result = result.filter((item: any) => pnMatchedQuoteIds.value!.has(item.id))
  }
  return result
}

const headers = [
  { title: 'Quote #', key: 'quoteNumber' },
  { title: 'RFQ Name', key: 'rfqName' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: 'Sent At', key: 'sentAt' },
  { title: 'Created', key: 'createdAt' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]
</script>
