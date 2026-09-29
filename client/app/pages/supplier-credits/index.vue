<template>
  <div>
    <!-- Prepaid credit = the supplier holds money for us. Credit lines = we bought on credit and must pay back. -->
    <v-tabs v-model="tab" color="primary" class="mb-3">
      <v-tab value="prepaid" prepend-icon="mdi-cash-refund">Our Amount</v-tab>
      <v-tab value="lines" prepend-icon="mdi-credit-card-clock-outline">
        Our Credit
        <v-badge v-if="summary.creditLinesOwingCount" :content="summary.creditLinesOwingCount" color="error" inline
          :title="`${summary.creditLinesOwingCount} credit line(s) to pay back`" />
      </v-tab>
    </v-tabs>

    <DataListPage
      v-if="tab === 'prepaid'"
      title="Supplier Credits"
      :headers="headers"
      api-url="/supplier-credits"
      :server-side="true"
      :extra-params="extraParams"
      page-key="supplier-credits"
      :show-total-sum="true"
      :hide-clear-button="true"
      search-placeholder="Search supplier or company"
    >
      <template #actions>
        <v-btn v-if="canAdd" color="success" variant="flat" size="small" prepend-icon="mdi-file-document-plus-outline" class="mr-2"
          @click="openDialog('creditNote')">Add Credit Note</v-btn>
        <v-btn v-if="canAdd" color="teal" variant="tonal" size="small" prepend-icon="mdi-bank-transfer-out" class="mr-2"
          @click="openDialog('walletTopUp')">Prepay from Wallet</v-btn>
        <v-btn v-if="canAdjust" color="warning" variant="tonal" size="small" prepend-icon="mdi-scale-balance"
          @click="openDialog('adjustment')">Adjustment</v-btn>
      </template>

      <template #filters>
        <v-autocomplete v-model="supplierFilter" :items="options.suppliers" item-title="name" item-value="id" label="Supplier"
          multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2"
          style="min-width: 160px; max-width: 280px;" :loading="optionsLoading" />
        <v-autocomplete v-model="companyFilter" :items="options.companies" item-title="name" item-value="id" label="Company"
          multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2"
          style="min-width: 150px; max-width: 260px;" :loading="optionsLoading" />
        <v-switch v-model="onlyWithBalance" label="Only with balance" color="primary" hide-details density="compact"
          inset class="mx-2 flex-grow-0" />
        <v-btn v-if="hasActiveFilters" variant="tonal" color="error" size="small" prepend-icon="mdi-filter-off"
          class="align-self-center" @click="clearFilters">Clear</v-btn>
      </template>

      <template #item.supplierName="{ item }">
        <button type="button" class="text-primary font-weight-medium text-left ledger-link" @click="openLedger(item.id)">
          {{ item.supplierName }}
        </button>
      </template>
      <template #item.companyPresetName="{ item }">
        <v-chip size="small" variant="tonal" prepend-icon="mdi-domain">{{ item.companyPresetName }}</v-chip>
      </template>
      <template #item.balance="{ item }">
        <span class="font-weight-bold" :class="item.balance > 0 ? 'text-success' : 'text-medium-emphasis'">
          ${{ formatPrice(item.balance) }}
        </span>
      </template>
      <template #item.totalIn="{ item }">${{ formatPrice(item.totalIn) }}</template>
      <template #item.totalOut="{ item }">${{ formatPrice(item.totalOut) }}</template>
      <template #item.lastEntryAt="{ item }">
        {{ item.lastEntryAt ? new Date(item.lastEntryAt).toLocaleDateString() : '—' }}
      </template>
      <template #item.actions="{ item }">
        <v-btn icon="mdi-book-open-variant" variant="text" size="small" title="Open ledger"
          :aria-label="`Open ledger of ${item.supplierName} / ${item.companyPresetName}`" @click.stop="openLedger(item.id)" />
      </template>

    </DataListPage>

    <SupplierCreditLinesList
      v-else
      :refresh-key="refreshKey"
      :owed-total="summary.creditLinesOwedTotal"
      @open-ledger="openLedger"
      @pay-back="openPayBack"
      @change-limit="openLimit"
      @create-line="openLimit(null)"
    />

    <!-- Shared by both tabs -->
    <SupplierCreditDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :supplier="dialogSupplier"
      :fixed-company-id="dialogCompanyId"
      :current-balance="dialogBalance"
      @saved="onSaved"
    />
    <SupplierCreditLineDialog v-model="limitOpen" :account="limitAccount" @saved="onLimitSaved" />
    <SupplierCreditRepaymentDialog v-model="payBackOpen" :account="payBackAccount" @changed="onRepaymentChanged" />
    <SupplierCreditLedgerDrawer
      v-model="ledgerOpen"
      :account-id="ledgerAccountId"
      :refresh-key="refreshKey"
      @add="openDialogFor"
      @pay-back="openPayBack"
      @change-limit="openLimit"
      @changed="refresh"
    />
    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="5000">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type {
  SupplierCreditAccountRow, SupplierCreditDialogMode, SupplierCreditOption, SupplierCreditSummary, SupplierCreditWriteResult,
} from '~/utils/supplierCredits'

const api = useApi()
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

// ── Tabs (deep link: ?tab=lines) + the badge numbers ──
const tab = ref<'prepaid' | 'lines'>(route.query.tab === 'lines' ? 'lines' : 'prepaid')
watch(tab, (t) => router.replace({ query: { ...route.query, tab: t === 'lines' ? 'lines' : undefined } }))
const summary = ref<SupplierCreditSummary>({ prepaidTotal: 0, creditLinesOwingCount: 0, creditLinesOwedTotal: 0 })
async function loadSummary() {
  try { summary.value = await api.get<SupplierCreditSummary>('/supplier-credits/summary') } catch { /* badges stay as they are */ }
}
onMounted(loadSummary)

// Mirrors the API roles (plan §3.3): Payment may add credit, only admins adjust or reverse.
const role = computed(() => authStore.user?.role ?? '')
const canAdd = computed(() => ['SuperAdmin', 'Admin', 'Payment'].includes(role.value))
const canAdjust = computed(() => ['SuperAdmin', 'Admin'].includes(role.value))

const headers = [
  { title: 'Supplier', key: 'supplierName', sortable: true },
  { title: 'Company', key: 'companyPresetName', sortable: true },
  { title: 'Balance', key: 'balance', sortable: true, align: 'end' as const },
  { title: 'Total In', key: 'totalIn', sortable: true, align: 'end' as const },
  { title: 'Used / Out', key: 'totalOut', sortable: true, align: 'end' as const },
  { title: 'Last Movement', key: 'lastEntryAt', sortable: true },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]

// Same key as DataListPage's page-key, so one Clear resets search and these filters together.
const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('supplier-credits', {
  search: '',
  supplier: [] as number[],
  company: [] as number[],
  onlyWithBalance: true,
})
const supplierFilter = pf.supplier
const companyFilter = pf.company
const onlyWithBalance = pf.onlyWithBalance

/** Bumped after every write so DataListPage reloads (it reloads whenever extraParams change). */
const refreshKey = ref(0)
const filterParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = {}
  if (supplierFilter.value?.length) p.supplierIds = supplierFilter.value.map(String)
  if (companyFilter.value?.length) p.companyPresetIds = companyFilter.value.map(String)
  if (!onlyWithBalance.value) p.onlyWithBalance = 'false'
  return p
})
const extraParams = computed<Record<string, string | string[]>>(() => {
  const p = { ...filterParams.value }
  if (refreshKey.value) p._r = String(refreshKey.value)
  return p
})
function refresh() {
  refreshKey.value++
  loadSummary()
}

// Dropdowns only offer values that still return rows under the other filters (exclude-self on the API).
const options = shallowRef<{ suppliers: SupplierCreditOption[]; companies: SupplierCreditOption[] }>({ suppliers: [], companies: [] })
const optionsLoading = ref(false)
let optionsTimer: ReturnType<typeof setTimeout> | null = null
async function loadOptions() {
  optionsLoading.value = true
  try {
    const params = new URLSearchParams()
    for (const [k, v] of Object.entries(filterParams.value)) (Array.isArray(v) ? v : [v]).forEach(x => params.append(k, x))
    options.value = await api.get<any>(`/supplier-credits/filter-options?${params}`)
  } catch { /* keep the previous options */ } finally { optionsLoading.value = false }
}
watch([filterParams, refreshKey], () => {
  if (optionsTimer) clearTimeout(optionsTimer)
  optionsTimer = setTimeout(loadOptions, 250)
}, { deep: true })
onMounted(loadOptions)

// ── Ledger drawer ──
const ledgerOpen = ref(false)
const ledgerAccountId = ref<number | null>(null)
function openLedger(accountId: number) {
  ledgerAccountId.value = accountId
  ledgerOpen.value = true
}
// Deep link: /supplier-credits?account=12 opens that ledger (used from PO pages).
onMounted(() => {
  const id = Number(route.query.account)
  if (id > 0) openLedger(id)
})

// ── Add / adjust dialog ──
const dialogOpen = ref(false)
const dialogMode = ref<SupplierCreditDialogMode>('creditNote')
const dialogSupplier = ref<SupplierCreditOption | null>(null)
const dialogCompanyId = ref<number | null>(null)
const dialogBalance = ref<number | null>(null)

function openDialog(mode: SupplierCreditDialogMode) {
  dialogMode.value = mode
  dialogSupplier.value = null
  dialogCompanyId.value = null
  dialogBalance.value = null
  dialogOpen.value = true
}

/** From the ledger: same supplier and company, locked. */
function openDialogFor(mode: SupplierCreditDialogMode, account: SupplierCreditAccountRow) {
  dialogMode.value = mode
  dialogSupplier.value = { id: account.supplierId, name: account.supplierName }
  dialogCompanyId.value = account.companyPresetId
  dialogBalance.value = account.balance
  dialogOpen.value = true
}

// ── Credit lines: limit + pay back ──
const limitOpen = ref(false)
const limitAccount = ref<SupplierCreditAccountRow | null>(null)
const payBackOpen = ref(false)
const payBackAccount = ref<SupplierCreditAccountRow | null>(null)

function openLimit(account: SupplierCreditAccountRow | null) {
  limitAccount.value = account
  limitOpen.value = true
}
function openPayBack(account: SupplierCreditAccountRow) {
  payBackAccount.value = account
  payBackOpen.value = true
}
function onLimitSaved(result: SupplierCreditWriteResult) {
  showSnack(`Credit limit saved — $${formatPrice(result.balance)} available.`)
  refresh()
}
/** A repayment step was saved: refresh the lists and give the dialog the new owed amount. */
async function onRepaymentChanged(message: string) {
  showSnack(message)
  refresh()
  if (!payBackAccount.value) return
  try { payBackAccount.value = await api.get<SupplierCreditAccountRow>(`/supplier-credits/${payBackAccount.value.id}`) } catch { /* keep the old row */ }
}

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
function onSaved(result: SupplierCreditWriteResult) {
  showSnack(`Saved. ${result.entry.direction === 'In' ? 'Credit added' : 'Credit removed'} — new balance $${formatPrice(result.balance)}.`)
  refresh()
}
</script>

<style scoped>
.ledger-link {
  background: none;
  border: 0;
  padding: 0;
  cursor: pointer;
}
.ledger-link:hover { text-decoration: underline; }
</style>
