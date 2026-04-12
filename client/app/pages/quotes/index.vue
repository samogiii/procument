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
        v-if="isAmir"
        prepend-icon="mdi-download-multiple"
        variant="tonal"
        color="primary"
        size="small"
        @click="showBulkDownload = true"
      >
        Bulk Download
      </v-btn>
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
      <v-autocomplete
        v-model="userFilter"
        :items="userOptions"
        label="User"
        hide-details
        multiple
        chips
        closable-chips
        clearable
        density="compact"
        variant="outlined"
        class="mx-2"
        style="min-width: 140px; max-width: 240px;"
      />
      <v-autocomplete
        v-model="customerFilter"
        :items="customerOptions"
        label="Customer"
        hide-details
        multiple
        chips
        closable-chips
        clearable
        density="compact"
        variant="outlined"
        class="mx-2"
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

    <template #item.sentAt="{ item }">
      {{ item.sentAt ? new Date(item.sentAt).toLocaleString() : '—' }}
    </template>

    <template #item.assignedUsers="{ item }">
      <div class="d-flex flex-wrap gap-1 py-1">
        <v-chip
          v-for="u in item.assignedUsers"
          :key="u.id"
          size="x-small"
          color="indigo"
          variant="tonal"
        >{{ u.name }}</v-chip>
        <span v-if="!item.assignedUsers?.length" class="text-medium-emphasis">—</span>
      </div>
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
        <td colspan="5"></td>
      </tr>
    </template>
  </DataListPage>

  <BulkPermissionManager v-model="showBulkPerms" entity-name="Quote" />
  <BulkQuoteDownload v-model="showBulkDownload" />
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const isAmir = computed(() => authStore.isAmir)
const showBulkPerms = ref(false)
const showBulkDownload = ref(false)

// Use a different key to avoid conflicts with DataListPage's search/status
const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('quotes', {
  search: '',
  status: [] as string[],
  user: [] as string[],
  customer: [] as string[],
  pnSearch: '',
})
const userFilter = pf.user
const customerFilter = pf.customer
const statusFilter = pf.status
const dateFrom = ref<string | null>(null)
const dateTo = ref<string | null>(null)

// P/N Search — filters datatable by matching quote IDs
const pnSearch = pf.pnSearch
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

const allQuotes = ref<any[]>([])

onMounted(async () => {
  try {
    const res = await api.get<any>('/quotes?pageSize=9999')
    allQuotes.value = Array.isArray(res) ? res : (res.items || res.Items || [])
  } catch {}
})

const userOptions = computed(() => {
  const userSet = new Map<string, string>()
  const sourceItems = statusFilter.value?.length
    ? allQuotes.value.filter((q: any) => statusFilter.value.includes(q.status))
    : allQuotes.value

  sourceItems.forEach((q: any) => {
    if (q.userName) userSet.set(q.userName, q.userName)
    if (q.assignedUsers?.length) {
      q.assignedUsers.forEach((u: any) => {
        if (u.name) userSet.set(u.name, u.name)
      })
    }
  })
  return Array.from(userSet.values()).sort()
})

const customerOptions = computed(() => {
  const custSet = new Set<string>()
  const sourceItems = statusFilter.value?.length
    ? allQuotes.value.filter((q: any) => statusFilter.value.includes(q.status))
    : allQuotes.value

  sourceItems.forEach((q: any) => {
    if (q.customerName) custSet.add(q.customerName)
  })
  return Array.from(custSet).sort()
})

function applyFilters(items: any[]) {
  let result = items
  if (userFilter.value?.length) {
    result = result.filter((item: any) =>
      userFilter.value.includes(item.userName) ||
      item.assignedUsers?.some((u: any) => userFilter.value.includes(u.name))
    )
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
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  { title: 'Sent At', key: 'sentAt' },
  { title: 'Created', key: 'createdAt' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]
</script>
