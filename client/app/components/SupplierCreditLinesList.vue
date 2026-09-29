<template>
  <DataListPage
    title="Supplier Credits"
    :headers="headers"
    api-url="/supplier-credits"
    :server-side="true"
    :extra-params="extraParams"
    page-key="supplier-credit-lines"
    :hide-clear-button="true"
    search-placeholder="Search supplier or company"
  >
    <template #actions>
      <v-chip v-if="owedTotal > 0" color="error" variant="tonal" size="small" prepend-icon="mdi-alert-circle-outline" class="mr-2">
        To pay back: ${{ formatPrice(owedTotal) }}
      </v-chip>
      <v-btn v-if="canAdjust" color="indigo" variant="flat" size="small" prepend-icon="mdi-credit-card-plus-outline"
        @click="$emit('create-line')">Set up credit line</v-btn>
    </template>

    <template #filters>
      <v-autocomplete v-model="supplierFilter" :items="options.suppliers" item-title="name" item-value="id" label="Supplier"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2"
        style="min-width: 160px; max-width: 280px;" :loading="optionsLoading" />
      <v-autocomplete v-model="companyFilter" :items="options.companies" item-title="name" item-value="id" label="Company"
        multiple chips closable-chips clearable hide-details density="compact" variant="outlined" class="mx-2"
        style="min-width: 150px; max-width: 260px;" :loading="optionsLoading" />
      <v-switch v-model="onlyOwing" label="Only with a debt" color="error" hide-details density="compact"
        inset class="mx-2 flex-grow-0" />
      <v-btn v-if="hasActiveFilters" variant="tonal" color="error" size="small" prepend-icon="mdi-filter-off"
        class="align-self-center" @click="clearFilters">Clear</v-btn>
    </template>

    <template #item.supplierName="{ item }">
      <button type="button" class="text-primary font-weight-medium text-left ledger-link" @click="$emit('open-ledger', item.id)">
        {{ item.supplierName }}
      </button>
    </template>
    <template #item.companyPresetName="{ item }">
      <v-chip size="small" variant="tonal" prepend-icon="mdi-domain">{{ item.companyPresetName }}</v-chip>
    </template>
    <template #item.creditLimit="{ item }">${{ formatPrice(item.creditLimit ?? 0) }}</template>
    <!-- The reminder: what we still have to pay back -->
    <template #item.owed="{ item }">
      <span v-if="item.owed > 0" class="font-weight-bold text-error text-no-wrap">
        <v-icon icon="mdi-alert-circle-outline" size="16" class="mr-1" />${{ formatPrice(item.owed) }}
      </span>
      <span v-else class="text-success text-no-wrap"><v-icon icon="mdi-check-circle-outline" size="16" class="mr-1" />Nothing owed</span>
    </template>
    <template #item.balance="{ item }">
      <span class="text-no-wrap">${{ formatPrice(item.balance) }}</span>
      <v-progress-linear :model-value="usedPct(item)" :color="usedPct(item) >= 100 ? 'error' : 'warning'" height="4" rounded class="mt-1" />
    </template>
    <template #item.oldestUnpaidAt="{ item }">
      <template v-if="item.owed > 0 && item.oldestUnpaidAt">
        {{ new Date(item.oldestUnpaidAt).toLocaleDateString() }}
        <div class="text-caption text-medium-emphasis">{{ daysAgo(item.oldestUnpaidAt) }}</div>
      </template>
      <span v-else class="text-medium-emphasis">—</span>
    </template>
    <template #item.openRepaymentId="{ item }">
      <v-chip v-if="item.openRepaymentId" size="x-small" color="warning" variant="tonal" prepend-icon="mdi-progress-clock">In progress</v-chip>
    </template>
    <template #item.actions="{ item }">
      <div class="d-flex justify-end">
        <v-btn v-if="canPay && (item.owed > 0 || item.openRepaymentId)" size="small" variant="tonal" color="teal"
          prepend-icon="mdi-cash-refund" class="mr-1" @click.stop="$emit('pay-back', item)">Pay back</v-btn>
        <v-btn v-if="canAdjust" icon="mdi-pencil-outline" size="small" variant="text" title="Change limit"
          :aria-label="`Change the credit limit of ${item.supplierName} / ${item.companyPresetName}`" @click.stop="$emit('change-limit', item)" />
        <v-btn icon="mdi-book-open-variant" variant="text" size="small" title="Open ledger"
          :aria-label="`Open ledger of ${item.supplierName} / ${item.companyPresetName}`" @click.stop="$emit('open-ledger', item.id)" />
      </div>
    </template>
  </DataListPage>
</template>

<script setup lang="ts">
import type { SupplierCreditAccountRow, SupplierCreditOption } from '~/utils/supplierCredits'

const props = defineProps<{
  /** Bump after any write so the list and its filter options reload. */
  refreshKey?: number
  /** Σ owed across all credit lines (from /summary) for the header reminder. */
  owedTotal: number
}>()
defineEmits<{
  'open-ledger': [accountId: number]
  'pay-back': [account: SupplierCreditAccountRow]
  'change-limit': [account: SupplierCreditAccountRow]
  'create-line': []
}>()

const api = useApi()
const authStore = useAuthStore()
const role = computed(() => authStore.user?.role ?? '')
// Mirrors the API: repayments follow the supplier-payment roles; limits are admin-only.
const canPay = computed(() => ['SuperAdmin', 'Admin', 'Payment', 'AHM'].includes(role.value))
const canAdjust = computed(() => ['SuperAdmin', 'Admin'].includes(role.value))

const headers = [
  { title: 'Supplier', key: 'supplierName', sortable: true },
  { title: 'Company', key: 'companyPresetName', sortable: true },
  { title: 'Limit', key: 'creditLimit', sortable: true, align: 'end' as const },
  { title: 'Owed', key: 'owed', sortable: true, align: 'end' as const },
  { title: 'Available', key: 'balance', sortable: true, align: 'end' as const },
  { title: 'Unpaid since', key: 'oldestUnpaidAt', sortable: false },
  { title: 'Repayment', key: 'openRepaymentId', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]

// Same key as DataListPage's page-key, so one Clear resets search and these filters together.
const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('supplier-credit-lines', {
  search: '',
  supplier: [] as number[],
  company: [] as number[],
  onlyOwing: false,
})
const supplierFilter = pf.supplier
const companyFilter = pf.company
const onlyOwing = pf.onlyOwing

const filterParams = computed<Record<string, string | string[]>>(() => {
  const p: Record<string, string | string[]> = { kind: 'CreditLine' }
  if (supplierFilter.value?.length) p.supplierIds = supplierFilter.value.map(String)
  if (companyFilter.value?.length) p.companyPresetIds = companyFilter.value.map(String)
  if (onlyOwing.value) p.onlyOwing = 'true'
  return p
})
const extraParams = computed<Record<string, string | string[]>>(() => {
  const p = { ...filterParams.value }
  if (props.refreshKey) p._r = String(props.refreshKey)
  return p
})

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
watch([filterParams, () => props.refreshKey], () => {
  if (optionsTimer) clearTimeout(optionsTimer)
  optionsTimer = setTimeout(loadOptions, 250)
}, { deep: true })
onMounted(loadOptions)

function usedPct(item: SupplierCreditAccountRow) {
  const limit = item.creditLimit ?? 0
  return limit > 0 ? Math.min(100, (item.owed / limit) * 100) : 0
}
function daysAgo(iso: string) {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)
  return days <= 0 ? 'today' : days === 1 ? '1 day ago' : `${days} days ago`
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
