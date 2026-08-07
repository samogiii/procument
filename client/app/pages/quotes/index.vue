<template>
  <DataListPage
    title="Quotes"
    :headers="headers"
    api-url="/quotes"
    :status-options="quoteStatusOptions"
    detail-route="/quotes"
    :server-side="true"
    :extra-params="extraParams"
    page-key="quotes"
    :show-total-sum="true"
    :hide-clear-button="true"
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
        :color="showRejected ? 'error' : 'default'"
        :variant="showRejected ? 'tonal' : 'outlined'"
        size="small"
        :prepend-icon="showRejected ? 'mdi-eye' : 'mdi-eye-off-outline'"
        class="align-self-center"
        @click="showRejected = !showRejected"
      >
        Rejected
      </v-btn>
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

    <!-- Column filter header slots — server-side: toggling a checkbox re-fetches from the backend.
         :options is the cascaded list (values that still return rows under the other active
         filters); :all-options is the full list, reachable via the menu's "Show all". -->
    <template #header.quoteNumber="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="quoteNumber"
        :label="column.title"
        :options="quoteNumberColOptions"
        :all-options="quoteNumberAllOptions"
        :selected="quoteNumberSet"
        :search="colSearch.quoteNumber"
        :loading="cfLoading"
        :is-sorted="isSorted(column)"
        :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
        @toggle="(v) => toggleColFilter(quoteNumberFilter, v)"
        @select-all="(vals) => setColFilter(quoteNumberFilter, vals)"
        @clear-all="() => setColFilter(quoteNumberFilter, [])"
        @update:search="(v) => colSearch.quoteNumber = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <template #header.customerCode="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="customerCode"
        :label="column.title"
        :options="customerColOptions"
        :all-options="customerAllOptions"
        :selected="customerSet"
        :search="colSearch.customer"
        :loading="cfLoading"
        :is-sorted="isSorted(column)"
        :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
        @toggle="(v) => toggleColFilter(customerFilter, v)"
        @select-all="(vals) => setColFilter(customerFilter, vals)"
        @clear-all="() => setColFilter(customerFilter, [])"
        @update:search="(v) => colSearch.customer = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <template #header.status="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="status"
        :label="column.title"
        :options="statusColOptions"
        :all-options="statusAllOptions"
        :selected="statusSet"
        :search="colSearch.status"
        :loading="cfLoading"
        :is-sorted="isSorted(column)"
        :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
        @toggle="(v) => toggleColFilter(statusFilter, v)"
        @select-all="(vals) => setColFilter(statusFilter, vals)"
        @clear-all="() => setColFilter(statusFilter, [])"
        @update:search="(v) => colSearch.status = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <template #header.rfqName="{ column, toggleSort, isSorted, sortBy }">
      <ColFilterMenu
        col-key="rfqName"
        :label="column.title"
        :options="rfqColOptions"
        :all-options="rfqAllOptions"
        :selected="rfqSet"
        :search="colSearch.rfq"
        :loading="cfLoading"
        :is-sorted="isSorted(column)"
        :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
        @toggle="(v) => toggleColFilter(rfqFilter, v)"
        @select-all="(vals) => setColFilter(rfqFilter, vals)"
        @clear-all="() => setColFilter(rfqFilter, [])"
        @update:search="(v) => colSearch.rfq = v"
        @sort-click="toggleSort(column)"
      />
    </template>

    <template #header.assignedUsers="{ column }">
      <ColFilterMenu
        col-key="assignedUsers"
        :label="column.title"
        :options="userColOptions"
        :all-options="userAllOptions"
        :selected="userSet"
        :search="colSearch.user"
        :loading="cfLoading"
        @toggle="(v) => toggleColFilter(userFilter, v)"
        @select-all="(vals) => setColFilter(userFilter, vals)"
        @clear-all="() => setColFilter(userFilter, [])"
        @update:search="(v) => colSearch.user = v"
        @sort-click="() => {}"
      />
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

const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('quotes', {
  search: '',
  status: [] as string[],
  user: [] as string[],
  customer: [] as string[],
  rfq: [] as string[],
  pnSearch: '',
  quoteNumber: [] as string[],
})
const userFilter = pf.user
const customerFilter = pf.customer
const statusFilter = pf.status
const rfqFilter = pf.rfq
const quoteNumberFilter = pf.quoteNumber

// P/N Search — passed as server-side filter param
const pnSearch = pf.pnSearch

const quoteStatusOptions = ['Draft', 'Sent', 'Accepted', 'Rejected']

const showRejected = ref(false)

/**
 * Column options come from the backend now — one small request per filter change instead
 * of pulling every quote into the browser. `available` is cascaded (each column computed
 * with the *other* filters applied) and `all` is the unfiltered list ColFilterMenu shows
 * behind "Show all".
 */
type QuoteOptions = {
  statuses: string[]
  customers: { title: string; value: string }[]
  users: string[]
  rfqNames: string[]
  quoteNumbers: string[]
}
const EMPTY_QUOTE_OPTIONS: QuoteOptions = { statuses: [], customers: [], users: [], rfqNames: [], quoteNumbers: [] }

const cfOptions = useCascadingOptions<QuoteOptions>(
  async (cascading) => {
    const params = new URLSearchParams()
    if (cascading) {
      if (pf.search.value?.trim()) params.set('search', pf.search.value.trim())
      if (pnSearch.value?.trim()) params.set('pnSearch', pnSearch.value.trim())
      if (showRejected.value) params.set('includeRejected', 'true')
      ;(statusFilter.value || []).forEach(s => params.append('status', s))
      ;(customerFilter.value || []).forEach(c => params.append('customerNames', c))
      ;(userFilter.value || []).forEach(u => params.append('assignedUserNames', u))
      ;(rfqFilter.value || []).forEach(r => params.append('rfqNames', r))
      ;(quoteNumberFilter.value || []).forEach(n => params.append('quoteNumbers', n))
    }
    const qs = params.toString()
    const res = await api.get<any>(`/quotes/filter-options${qs ? `?${qs}` : ''}`)
    return {
      statuses: quoteStatusOptions.filter(s => (res.statuses || []).includes(s)),
      customers: (res.customers || []).map((c: any) => ({ title: c.code || '-', value: c.name })),
      users: res.users || [],
      rfqNames: res.rfqNames || [],
      quoteNumbers: res.quoteNumbers || [],
    }
  },
  EMPTY_QUOTE_OPTIONS,
)
const cfLoading = cfOptions.loading

// Available (cascaded) — what each column can still offer.
const customerColOptions = computed(() => cfOptions.available.value.customers)
const userColOptions = computed(() => cfOptions.available.value.users)
const statusColOptions = computed(() => cfOptions.available.value.statuses)
const rfqColOptions = computed(() => cfOptions.available.value.rfqNames)
const quoteNumberColOptions = computed(() => cfOptions.available.value.quoteNumbers)

// Full lists — reachable through ColFilterMenu's "Show all".
const customerAllOptions = computed(() => cfOptions.all.value.customers)
const userAllOptions = computed(() => cfOptions.all.value.users)
const statusAllOptions = computed(() => quoteStatusOptions)
const rfqAllOptions = computed(() => cfOptions.all.value.rfqNames)
const quoteNumberAllOptions = computed(() => cfOptions.all.value.quoteNumbers)

// Top-bar autocompletes keep the full lists so a value is never unreachable there.
const userOptions = computed(() => userAllOptions.value)
const customerOptions = computed(() => customerAllOptions.value)

onMounted(() => cfOptions.init())

watch(
  [statusFilter, customerFilter, userFilter, rfqFilter, quoteNumberFilter, pnSearch, showRejected, () => pf.search.value],
  () => cfOptions.refreshDebounced(),
  { deep: true },
)

const extraParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = {}
  if (pnSearch.value) p.pnSearch = pnSearch.value
  if (userFilter.value?.length) p.assignedUserNames = userFilter.value
  if (customerFilter.value?.length) p.customerNames = customerFilter.value
  if (rfqFilter.value?.length) p.rfqNames = rfqFilter.value
  if (quoteNumberFilter.value?.length) p.quoteNumbers = quoteNumberFilter.value
  if (showRejected.value) p.includeRejected = 'true'
  return p
})

// ── Excel-style column filters — server-side ──
// These refs feed directly into extraParams / DataListPage's status param,
// so toggling a checkbox triggers a real backend fetch with the filter applied.
const colSearch = reactive<Record<string, string>>({ customer: '', status: '', user: '', rfq: '', quoteNumber: '' })

/** Toggle a value in a reactive string array (add if absent, remove if present).
 *  Receives the unwrapped array — as Vue auto-unwraps refs in templates. */
function toggleColFilter(arr: string[], val: string) {
  const idx = arr.indexOf(val)
  if (idx >= 0) arr.splice(idx, 1)
  else arr.push(val)
}

/** Replace a filter array in place (used by the menu's All / None buttons). */
function setColFilter(arr: string[], vals: string[]) {
  arr.splice(0, arr.length, ...vals)
}

// ColFilterMenu takes a Set; the page keeps arrays because they go straight into the query.
const quoteNumberSet = computed(() => new Set(quoteNumberFilter.value))
const customerSet = computed(() => new Set(customerFilter.value))
const statusSet = computed(() => new Set(statusFilter.value))
const rfqSet = computed(() => new Set(rfqFilter.value))
const userSet = computed(() => new Set(userFilter.value))

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
