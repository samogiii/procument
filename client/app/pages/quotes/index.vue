<template>
  <DataListPage
    title="Quotes"
    :headers="headers"
    api-url="/quotes"
    :status-options="quoteStatusOptions"
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
        item-title="title"
        item-value="value"
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

    <template #item.quoteNumber="{ item }">
      <nuxt-link :to="`/quotes/${item.id}`" class="text-primary font-weight-bold text-decoration-none" @click.stop>
        {{ item.quoteNumber || `#${item.id}` }}
      </nuxt-link>
    </template>

    <template #item.rfqName="{ item }">
      <nuxt-link v-if="item.rfqId" :to="`/rfqs/${item.rfqId}`" class="text-primary text-decoration-none" @click.stop>
        {{ item.rfqName || `RFQ #${item.rfqId}` }}
      </nuxt-link>
      <span v-else class="text-medium-emphasis">—</span>
    </template>

    <template #item.customerName="{ item }">
      <span
        v-if="item.customerName"
        class="text-primary text-decoration-none"
        style="cursor:pointer;"
        @click.stop="router.push(`/catalog/customers?search=${encodeURIComponent(item.customerName)}`)"
      >
        <template v-if="isAdmin">{{ item.customerName }}<span v-if="item.customerCode" class="text-medium-emphasis ml-1">({{ item.customerCode }})</span></template>
        <template v-else>{{ item.customerCode || '—' }}</template>
      </span>
      <span v-else class="text-medium-emphasis">—</span>
    </template>

    <template #item.status="{ item }">
      <v-menu v-if="isAdmin" :close-on-content-click="true">
        <template #activator="{ props: mp }">
          <v-chip
            :color="statusColor(item.status)"
            v-bind="mp"
            size="small"
            class="cursor-pointer"
            append-icon="mdi-chevron-down"
            @click.stop
          >
            {{ item.status }}
          </v-chip>
        </template>
        <v-list density="compact" style="min-width: 160px">
          <v-list-subheader>Change Status</v-list-subheader>
          <v-list-item
            v-for="s in quoteStatuses"
            :key="s.value"
            :active="item.status === s.value"
            @click="onStatusClick(item, s.value)"
          >
            <template #prepend>
              <v-icon :icon="s.icon" :color="s.color" size="18" />
            </template>
            <v-list-item-title>{{ s.label }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
      <StatusChip v-else :status="item.status" />
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

  <!-- Rejection dialog -->
  <v-dialog v-model="showRejectDialog" max-width="450">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-close-circle" color="error" class="mr-2" />
        Reject Quote
      </v-card-title>
      <v-card-text class="pa-4">
        <v-textarea
          v-model="rejectionNote"
          label="Rejection reason"
          rows="3"
          auto-grow
          variant="outlined"
        />
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
        <v-btn color="error" variant="tonal" :loading="statusSaving" @click="confirmReject">Reject</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Under $1000 Warning Dialog -->
  <v-dialog v-model="showUnder1000Warning" max-width="480" persistent>
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-alert-circle-outline" color="warning" class="mr-2" />
        Low Price Warning
      </v-card-title>
      <v-card-text class="pa-4">
        <div class="text-body-1 mb-3">The following items have a Total Price under <strong>$1,000</strong>:</div>
        <v-list density="compact" class="mb-3" bg-color="transparent">
          <v-list-item
            v-for="qi in under1000Items"
            :key="qi.id ?? qi.rfqItemId"
            :title="qi.partNumberName || 'Unknown part'"
            :subtitle="'Total: $' + formatPrice(qi.totalPrice)"
            prepend-icon="mdi-alert"
            color="warning"
          />
        </v-list>
        <div class="text-body-2 text-medium-emphasis">Are you sure you want to Accept this quote?</div>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="cancelUnder1000">No, Cancel</v-btn>
        <v-btn color="warning" variant="flat" :loading="statusSaving" @click="confirmUnder1000Accept">Yes, Accept Anyway</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
    {{ snackbarText }}
  </v-snackbar>
</template>

<script setup lang="ts">
const router = useRouter()
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const isAmir = computed(() => authStore.isAmir)
const { statusColor } = useStatusColor()

const quoteStatuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Sent', label: 'Sent', icon: 'mdi-send', color: 'info' },
  { value: 'Accepted', label: 'Accepted', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Rejected', label: 'Rejected', icon: 'mdi-close-circle', color: 'error' },
]

const showRejectDialog = ref(false)
const rejectingItem = ref<any>(null)
const rejectionNote = ref('')
const statusSaving = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const showUnder1000Warning = ref(false)
const under1000Items = ref<any[]>([])
const under1000PendingItem = ref<any>(null)

function onStatusClick(item: any, status: string) {
  if (status === item.status) return
  if (status === 'Rejected') {
    rejectingItem.value = item
    rejectionNote.value = ''
    showRejectDialog.value = true
    return
  }
  if (status === 'Accepted') {
    checkUnder1000AndAccept(item)
    return
  }
  doChangeStatus(item, status)
}

async function checkUnder1000AndAccept(item: any) {
  try {
    const quote = await api.get<any>(`/quotes/${item.id}`)
    const cheapItems = (quote.items || []).filter((qi: any) =>
      qi.totalPrice != null && Number(qi.totalPrice) < 1000
    )
    if (cheapItems.length > 0) {
      under1000Items.value = cheapItems
      under1000PendingItem.value = item
      showUnder1000Warning.value = true
      return
    }
  } catch {}
  doChangeStatus(item, 'Accepted')
}

async function confirmUnder1000Accept() {
  showUnder1000Warning.value = false
  if (under1000PendingItem.value) {
    await doChangeStatus(under1000PendingItem.value, 'Accepted')
    under1000PendingItem.value = null
  }
}

function cancelUnder1000() {
  showUnder1000Warning.value = false
  under1000PendingItem.value = null
}

async function doChangeStatus(item: any, status: string, note?: string) {
  statusSaving.value = true
  try {
    await api.patch(`/quotes/${item.id}/status`, { status, rejectionNote: note || null })
    item.status = status
    item.rejectionNote = note || null
    snackbarText.value = `Status changed to ${status}`
    snackbarColor.value = 'success'
    snackbar.value = true
  } catch {
    snackbarText.value = 'Failed to change status'
    snackbarColor.value = 'error'
    snackbar.value = true
  } finally {
    statusSaving.value = false
  }
}

async function confirmReject() {
  if (rejectingItem.value) {
    await doChangeStatus(rejectingItem.value, 'Rejected', rejectionNote.value)
  }
  showRejectDialog.value = false
  rejectingItem.value = null
}
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

// Status options: unique statuses from loaded quotes (not filtered)
const quoteStatusOptions = computed(() => {
  const set = new Set<string>(['All'])
  allQuotes.value.forEach((q: any) => { if (q.status) set.add(q.status) })
  return Array.from(set).sort()
})

onMounted(async () => {
  try {
    const res = await api.get<any>('/quotes?pageSize=9999')
    allQuotes.value = Array.isArray(res) ? res : (res.items || res.Items || [])
  } catch {}
})

// Base set filtered by status (shared starting point for cascading options)
const statusFilteredQuotes = computed(() =>
  statusFilter.value?.length
    ? allQuotes.value.filter((q: any) => statusFilter.value.includes(q.status))
    : allQuotes.value
)

// User options: only ASSIGNED users (not creator), cascades by customerFilter
const userOptions = computed(() => {
  const userSet = new Map<string, string>()
  let source = statusFilteredQuotes.value
  // cascade: if customer filter active, only users from those customers
  if (customerFilter.value?.length) {
    source = source.filter((q: any) =>
      customerFilter.value.includes(q.customerName) ||
      (q.customerCode && customerFilter.value.includes(q.customerCode))
    )
  }
  source.forEach((q: any) => {
    ;(q.assignedUsers || []).forEach((u: any) => {
      if (u.name) userSet.set(u.name, u.name)
    })
  })
  return Array.from(userSet.values()).sort()
})

// Customer options: cascades by userFilter (assigned users only)
const customerOptions = computed(() => {
  const custMap = new Map<string, string>()
  let source = statusFilteredQuotes.value
  // cascade: if user filter active, only customers where those users are assigned
  if (userFilter.value?.length) {
    source = source.filter((q: any) =>
      (q.assignedUsers || []).some((u: any) => userFilter.value.includes(u.name))
    )
  }
  source.forEach((q: any) => {
    if (q.customerName && !custMap.has(q.customerName))
      custMap.set(q.customerName, q.customerCode || '')
  })
  return Array.from(custMap.entries())
    .map(([name, code]) => ({
      title: code || '—',
      value: name,
    }))
    .sort((a, b) => a.title.localeCompare(b.title))
})

function applyFilters(items: any[]) {
  let result = items
  // Filter by ASSIGNED users only — not the quote creator
  if (userFilter.value?.length) {
    result = result.filter((item: any) =>
      item.assignedUsers?.some((u: any) => userFilter.value.includes(u.name))
    )
  }
  if (customerFilter.value?.length) {
    result = result.filter((item: any) =>
      customerFilter.value.includes(item.customerName) ||
      (item.customerCode && customerFilter.value.includes(item.customerCode))
    )
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
  { title: 'Customer', key: 'customerCode' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  { title: 'Sent At', key: 'sentAt' },
  { title: 'Created', key: 'createdAt' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]
</script>
