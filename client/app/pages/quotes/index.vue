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
      <div style="min-width: 220px; max-width: 320px; position: relative;" class="mx-2">
        <v-text-field
          v-model="pnSearch"
          label="Search by P/N"
          prepend-inner-icon="mdi-magnify"
          hide-details
          clearable
          density="compact"
          variant="outlined"
          @update:model-value="searchByPN"
        />
        <v-card
          v-if="pnResults.length > 0"
          class="pn-results-dropdown"
          elevation="8"
        >
          <v-list density="compact">
            <v-list-item
              v-for="r in pnResults"
              :key="r.quoteId + '-' + r.partNumberName"
              :to="`/quotes/${r.quoteId}`"
            >
              <v-list-item-title class="text-body-2">
                <strong>{{ r.partNumberName }}</strong>
                <span class="text-medium-emphasis ml-2">→ {{ r.quoteNumber }}</span>
              </v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                {{ r.customerName }} · {{ r.status }}
              </v-list-item-subtitle>
            </v-list-item>
          </v-list>
        </v-card>
      </div>
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

// P/N Search
const pnSearch = ref('')
const pnResults = ref<any[]>([])
let pnDebounce: any = null
function searchByPN(val: string | null) {
  clearTimeout(pnDebounce)
  if (!val || val.length < 2) {
    pnResults.value = []
    return
  }
  pnDebounce = setTimeout(async () => {
    try {
      pnResults.value = await api.get<any[]>(`/quotes/search-by-pn?q=${encodeURIComponent(val)}`)
    } catch {
      pnResults.value = []
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

<style scoped>
.pn-results-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  z-index: 100;
  max-height: 300px;
  overflow-y: auto;
}
</style>
