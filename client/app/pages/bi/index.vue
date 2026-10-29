<template>
  <div class="bi">
    <PageHeader title="BI">
      <template #actions>
        <template v-if="bi.isConfigured && !editing">
          <v-btn
            icon="mdi-refresh"
            variant="text"
            size="small"
            :loading="loading"
            title="Refresh"
            aria-label="Refresh"
            @click="loadSummary()"
          />
          <v-btn variant="tonal" prepend-icon="mdi-microsoft-excel" :disabled="!summary?.customers.length" :loading="exporting" @click="exportExcel">
            Export
          </v-btn>
          <v-btn variant="tonal" color="primary" prepend-icon="mdi-tune-variant" @click="editing = true">Change</v-btn>
          <v-btn variant="tonal" color="error" prepend-icon="mdi-broom" @click="clearBi">Clear</v-btn>
        </template>
      </template>
    </PageHeader>

    <!-- ═══ Selection ═══ -->
    <BiSelectionForm
      v-if="!bi.isConfigured || editing"
      :key="formKey"
      :all-customers="bi.allCustomers"
      :bases="bi.bases"
      :customers="bi.customers"
      :days="bi.days"
      :cancellable="bi.isConfigured"
      class="mb-6"
      @apply="applySelection"
      @cancel="editing = false"
    />

    <template v-if="bi.isConfigured && !editing">
      <!-- What this BI covers -->
      <div class="bi-scope mb-4">
        <v-chip v-if="bi.allCustomers" color="primary" variant="tonal" prepend-icon="mdi-account-group-outline">All customers</v-chip>
        <template v-else-if="bi.bases.length">
          <v-chip
            v-for="b in bi.bases"
            :key="b"
            color="primary"
            variant="tonal"
            prepend-icon="mdi-map-marker-outline"
            closable
            :close-label="`Remove Base ${b}`"
            @click:close="removeBase(b)"
          >
            All Base {{ b }} customers
          </v-chip>
        </template>
        <template v-else>
          <v-chip
            v-for="c in visibleScopeCustomers"
            :key="c.id"
            variant="tonal"
            size="small"
            closable
            :close-label="`Remove ${c.name}`"
            @click:close="removeCustomer(c.id)"
          >
            {{ biCustomerLabel(c) }}
          </v-chip>
          <v-chip v-if="hiddenScopeCount" size="small" variant="outlined" @click="editing = true">+{{ hiddenScopeCount }} more</v-chip>
        </template>
        <v-chip variant="tonal" color="secondary" prepend-icon="mdi-calendar-range">
          Last {{ bi.days }} day{{ bi.days === 1 ? '' : 's' }}
          <span v-if="summary" class="ml-1 text-medium-emphasis">· {{ biDate(summary.from) }} – {{ biDate(summary.to) }}</span>
        </v-chip>
      </div>

      <v-progress-linear v-if="loading && summary" indeterminate color="primary" height="2" class="mb-2" />

      <v-alert v-if="error" type="error" variant="tonal" class="mb-4">
        {{ error }}
        <template #append><v-btn variant="text" @click="loadSummary()">Retry</v-btn></template>
      </v-alert>

      <!-- ═══ KPI tiles ═══ -->
      <div class="bi-kpis mb-6" aria-live="polite">
        <template v-if="summary">
          <v-card v-for="k in kpis" :key="k.key" class="glass-card bi-kpi" :style="{ '--bi-accent': k.accent }">
            <div class="bi-kpi__head">
              <v-icon :icon="k.icon" size="18" class="bi-kpi__icon" />
              <span class="bi-kpi__label">{{ k.label }}</span>
              <v-tooltip v-if="k.hint" :text="k.hint" location="top" max-width="320">
                <template #activator="{ props: tip }">
                  <v-icon v-bind="tip" icon="mdi-information-outline" size="14" class="bi-kpi__hint" :aria-label="k.hint" />
                </template>
              </v-tooltip>
            </div>
            <div class="bi-kpi__value" :class="k.valueClass">{{ k.value }}</div>
            <div class="bi-kpi__sub">
              <v-icon v-if="k.subIcon" :icon="k.subIcon" size="14" :color="k.subColor" class="mr-1" />{{ k.sub }}
            </div>
          </v-card>
        </template>
        <template v-else>
          <v-skeleton-loader v-for="n in 8" :key="n" type="article" class="bi-kpi bi-kpi--skeleton" />
        </template>
      </div>

      <template v-if="summary">
        <!-- ═══ Conversion funnel ═══ -->
        <v-card class="glass-card pa-4 mb-6">
          <div class="bi-card-title">
            <v-icon icon="mdi-filter-variant" size="18" class="mr-2" />From RFQ to final invoice
          </div>
          <div class="bi-funnel">
            <div v-for="(s, i) in funnel" :key="s.label" class="bi-funnel__step">
              <div class="bi-funnel__bar-wrap">
                <div class="bi-funnel__bar" :style="{ width: `${s.width}%`, background: s.color }" />
              </div>
              <div class="bi-funnel__meta">
                <span class="bi-funnel__label">{{ s.label }}</span>
                <span class="bi-funnel__count">{{ biCount(s.count) }}</span>
                <span v-if="i > 0" class="bi-funnel__rate">{{ s.rate }}</span>
              </div>
            </div>
          </div>
        </v-card>

        <!-- ═══ Charts — mounted only once scrolled near, then kept ═══ -->
        <div v-intersect.once="{ handler: onChartsVisible, options: { rootMargin: '200px' } }" class="bi-charts mb-6">
          <v-card class="glass-card pa-4">
            <div class="bi-card-title">
              <v-icon icon="mdi-chart-line" size="18" class="mr-2" />Activity per {{ summary.bucket }}
            </div>
            <LazyBiTrendChart v-if="chartsVisible" :trend="summary.trend" :bucket="summary.bucket" mode="count" />
            <v-skeleton-loader v-else type="image" height="280" />
          </v-card>
          <v-card class="glass-card pa-4">
            <div class="bi-card-title">
              <v-icon icon="mdi-chart-areaspline" size="18" class="mr-2" />Quoted vs bought value per {{ summary.bucket }}
            </div>
            <LazyBiTrendChart v-if="chartsVisible" :trend="summary.trend" :bucket="summary.bucket" mode="value" />
            <v-skeleton-loader v-else type="image" height="280" />
          </v-card>
          <v-card v-if="customersWithBought > 1" class="glass-card pa-4 bi-charts__wide">
            <div class="bi-card-title">
              <v-icon icon="mdi-podium" size="18" class="mr-2" />Top customers by bought value
            </div>
            <LazyBiTopCustomersChart v-if="chartsVisible" :customers="summary.customers" />
            <v-skeleton-loader v-else type="image" height="240" />
          </v-card>
        </div>

        <!-- ═══ Breakdown ═══ -->
        <v-card class="glass-card">
          <v-tabs v-model="tab" color="primary" class="px-2">
            <v-tab value="customers" prepend-icon="mdi-account-group-outline">
              By customer <v-chip size="x-small" variant="tonal" class="ml-2">{{ summary.customers.length }}</v-chip>
            </v-tab>
            <v-tab value="proformas" prepend-icon="mdi-receipt-text-outline">
              Proforma invoices <v-chip size="x-small" variant="tonal" class="ml-2">{{ biCount(summary.totals.piCount) }}</v-chip>
            </v-tab>
          </v-tabs>
          <v-divider />

          <v-window v-model="tab">
            <v-window-item value="customers">
              <div class="d-flex align-center flex-wrap gap-2 pa-3">
                <v-text-field
                  v-model="customerSearch"
                  placeholder="Filter customers"
                  prepend-inner-icon="mdi-magnify"
                  density="compact"
                  variant="outlined"
                  hide-details
                  clearable
                  style="max-width: 320px"
                />
                <v-spacer />
                <span class="text-caption text-medium-emphasis">
                  <v-icon icon="mdi-gesture-tap" size="14" class="mr-1" />Open a row to see its proforma invoices
                </span>
              </div>

              <v-data-table
                v-model:expanded="expanded"
                v-model:sort-by="customerSort"
                :headers="customerHeaders"
                :items="filteredCustomers"
                item-value="customerId"
                :items-per-page="25"
                :items-per-page-options="[10, 25, 50, 100]"
                density="comfortable"
                class="bi-table"
                show-expand
                hover
                @click:row="toggleRow"
              >
                <template #item.customerName="{ item }">
                  <div class="bi-table__customer">
                    <span class="font-weight-medium">{{ item.customerName || `Customer #${item.customerId}` }}</span>
                    <span class="text-caption text-medium-emphasis">
                      {{ item.customerCode || '—' }}<template v-if="item.base"> · Base {{ item.base }}</template>
                    </span>
                  </div>
                </template>

                <template v-for="col in METRIC_COLUMNS" :key="col.key" #[`item.${col.key}`]="{ item }">
                  <MetricCell :cell="col.cell(item)" />
                </template>

                <template #tfoot>
                  <tfoot v-if="filteredCustomers.length > 1">
                    <tr class="bi-table__total">
                      <td />
                      <td class="font-weight-bold">{{ customerSearch ? 'Shown total' : 'All customers' }}</td>
                      <td v-for="col in METRIC_COLUMNS" :key="col.key" class="text-end">
                        <MetricCell :cell="col.cell(footerTotals)" />
                      </td>
                    </tr>
                  </tfoot>
                </template>

                <template #expanded-row="{ columns, item }">
                  <tr class="bi-table__expanded">
                    <td :colspan="columns.length" class="pa-0">
                      <div class="pa-3">
                        <LazyBiProformaTable nested :customer-ids="[item.customerId]" :days="bi.days!" />
                      </div>
                    </td>
                  </tr>
                </template>

                <template #no-data>
                  <div class="py-8 text-center text-medium-emphasis">
                    <v-icon icon="mdi-chart-box-outline" size="32" class="mb-2" />
                    <div>{{ customerSearch ? 'No customer matches the filter.' : 'No activity in this period.' }}</div>
                  </div>
                </template>
              </v-data-table>
            </v-window-item>

            <!-- Mounted on first open only -->
            <v-window-item value="proformas">
              <div class="pa-3">
                <LazyBiProformaTable v-if="proformasOpened" :customer-ids="bi.customerIds" :bases="bi.baseFilter" :days="bi.days!" />
              </div>
            </v-window-item>
          </v-window>
        </v-card>

        <p class="bi-note mt-4">
          <v-icon icon="mdi-information-outline" size="14" class="mr-1" />
          Sent quotes count by their sent date. Bought value counts only the proforma lines that are in a live PO
          (cancelled or returned POs and returned lines are left out), whether or not the customer has paid.
          Profit = bought value − supplier cost − shipping, wire fee &amp; others. For a paid PO those costs are what really
          left the wallets (supplier payments, credit, Bank Fee and Others); an unpaid PO uses the amounts written on it
          <template v-if="summary.totals.unpaidPoCount"> ({{ summary.totals.unpaidPoCount }} still unpaid, so their part can change)</template>.
          A PO shared by several proformas is split by their share of its parts.
          <template v-if="summary.totals.unconvertedFeeCount">
            {{ summary.totals.unconvertedFeeCount }} fee withdraw{{ summary.totals.unconvertedFeeCount === 1 ? '' : 's' }} left out — no USD rate.
          </template>
          PI → Final invoice is the time to the first final invoice. Amounts in USD.
        </p>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, h, type PropType } from 'vue'
import {
  biCount, biCustomerLabel, biDate, biDays, biMargin, biMoney, biPercent,
  type BiMetrics, type BiSummary,
} from '~/utils/bi'
import type { BiSelection } from '~/stores/bi'
import { BI_SERIES_COLORS } from '~/utils/biChart'
import { useBiStore } from '~/stores/bi'

useHead({ title: 'BI' })

const bi = useBiStore()
bi.load()

const api = useApi()
const { isDark } = useAppTheme()

const editing = ref(false)
const formKey = ref(0)
const loading = ref(false)
const exporting = ref(false)
const error = ref('')
const tab = ref<'customers' | 'proformas'>('customers')
const proformasOpened = ref(false)
const chartsVisible = ref(false)
/** Expanded customer ids (Vuetify types this as string[], but it holds the item-value as given). */
const expanded = ref<any[]>([])
const customerSearch = ref('')
const customerSort = ref<{ key: string; order: 'asc' | 'desc' }[]>([{ key: 'boughtValue', order: 'desc' }])

/** Kept raw — the table and charts only read it, so no deep proxies over every row. */
const summary = shallowRef<BiSummary | null>(bi.summaryKey === bi.selectionKey ? bi.summary : null)

watch(tab, (t) => { if (t === 'proformas') proformasOpened.value = true })

function onChartsVisible(isIntersecting: boolean) {
  if (isIntersecting) chartsVisible.value = true
}

// ── Loading ──
let controller: AbortController | null = null

async function loadSummary() {
  if (!bi.isConfigured) return
  controller?.abort()
  const current = new AbortController()
  controller = current
  const key = bi.selectionKey
  loading.value = true
  error.value = ''
  try {
    const res = await api.post<BiSummary>('/bi/summary', {
      customerIds: bi.customerIds, bases: bi.baseFilter, days: bi.days,
    }, { signal: current.signal })
    if (controller !== current) return
    bi.setSummary(key, res)
    summary.value = bi.summary
  } catch (e: any) {
    if (current.signal.aborted) return
    error.value = typeof e?.data === 'string' ? e.data : e?.data?.message || 'Could not load the BI. Please try again.'
  } finally {
    if (controller === current) loading.value = false
  }
}

function resetView() {
  expanded.value = []
  customerSearch.value = ''
  proformasOpened.value = tab.value === 'proformas'
}

function applySelection(selection: BiSelection & { days: number }) {
  bi.apply(selection)
  editing.value = false
  resetView()
  if (bi.summaryKey !== bi.selectionKey) summary.value = null
  loadSummary()
}

function removeCustomer(id: number) {
  const rest = bi.customers.filter(c => c.id !== id)
  if (!rest.length) return clearBi()
  applySelection({ allCustomers: false, bases: [], customers: rest, days: bi.days! })
}

function removeBase(base: number) {
  const rest = bi.bases.filter(b => b !== base)
  if (!rest.length) return clearBi()
  applySelection({ allCustomers: false, bases: rest, customers: [], days: bi.days! })
}

function clearBi() {
  controller?.abort()
  bi.clear()
  summary.value = null
  error.value = ''
  editing.value = false
  tab.value = 'customers'
  resetView()
  formKey.value++
}

onMounted(() => {
  // Stale-while-revalidate: a cached summary shows at once, then refreshes quietly.
  if (bi.isConfigured) loadSummary()
})
onBeforeUnmount(() => controller?.abort())

// ── Scope chips ──
const SCOPE_CHIP_LIMIT = 6
const visibleScopeCustomers = computed(() => bi.customers.slice(0, SCOPE_CHIP_LIMIT))
const hiddenScopeCount = computed(() => Math.max(0, bi.customers.length - SCOPE_CHIP_LIMIT))

// ── KPIs ──
const palette = computed(() => (isDark.value ? BI_SERIES_COLORS.dark : BI_SERIES_COLORS.light))

const kpis = computed(() => {
  const t = summary.value!.totals
  const p = palette.value
  const margin = biMargin(t.profit, t.boughtValue)
  const neutral = isDark.value ? '#8B949E' : '#64748b'
  const plural = (n: number, word: string) => `${biCount(n)} ${word}${n === 1 ? '' : 's'}`
  return [
    { key: 'rfq', label: 'RFQs', icon: 'mdi-file-document-outline', accent: p.rfq,
      value: biCount(t.rfqCount), sub: `${activeCustomers.value} active customer${activeCustomers.value === 1 ? '' : 's'}` },
    { key: 'quotes', label: 'Sent quotes', icon: 'mdi-send-outline', accent: p.quote,
      value: biCount(t.sentQuoteCount), sub: t.rfqCount ? `${biPercent(t.sentQuoteCount, t.rfqCount)} of RFQ count` : 'No RFQs in period' },
    { key: 'quoteSum', label: 'Quoted value', icon: 'mdi-cash-fast', accent: p.quote,
      value: biMoney(t.sentQuoteSum, true), sub: t.sentQuoteCount ? `avg ${biMoney(t.sentQuoteSum / t.sentQuoteCount, true)} per quote` : '—' },
    { key: 'pi', label: 'Proforma invoices', icon: 'mdi-receipt-text-outline', accent: p.pi,
      value: biCount(t.piCount), sub: t.sentQuoteCount ? `${biPercent(t.piCount, t.sentQuoteCount)} of sent quotes` : '—' },
    // ── What we actually bought for these proformas ──
    { key: 'bought', label: 'Bought value', icon: 'mdi-cart-check', accent: p.pi,
      value: biMoney(t.boughtValue, true),
      sub: t.boughtPiCount
        ? `${biCount(t.boughtPiCount)} of ${plural(t.piCount, 'proforma')} in a PO${t.partialPoCount ? ` · ${t.partialPoCount} partly` : ''}`
        : 'Nothing in a PO yet',
      subIcon: t.partialPoCount ? 'mdi-alert-outline' : undefined, subColor: 'warning',
      hint: 'Sell price of the proforma lines that are in a PO — counted even if the customer has not paid yet.' },
    { key: 'supplier', label: 'Supplier cost', icon: 'mdi-package-variant-closed', accent: neutral,
      value: biMoney(t.supplierCost, true),
      sub: t.boughtValue ? `${biPercent(t.supplierCost, t.boughtValue)} of bought value` : '—',
      hint: 'PO line prices of the bought parts.' },
    { key: 'fees', label: 'Shipping, wire fee & others', icon: 'mdi-truck-fast-outline', accent: neutral,
      value: biMoney(t.fees, true),
      sub: t.feesEstimated
        ? `${biMoney(t.feesEstimated, true)} estimated · ${plural(t.unpaidPoCount, 'unpaid PO')}`
        : t.fees ? 'From the wallet withdraws' : '—',
      subIcon: t.feesEstimated ? 'mdi-timer-sand' : undefined, subColor: 'warning',
      hint: 'Paid POs: what really left the wallets above the part prices (shipping, tax, processing, wire fee, Bank Fee and Others). Unpaid POs: the amounts written on the PO until they are paid.' },
    { key: 'profit', label: 'Profit', icon: t.profit < 0 ? 'mdi-trending-down' : 'mdi-trending-up', accent: t.profit < 0 ? '#e34948' : '#008300',
      value: biMoney(t.profit, true), valueClass: t.profit < 0 ? 'text-error' : undefined,
      sub: t.boughtValue ? `${margin}% margin` : 'Nothing bought yet',
      hint: 'Bought value − supplier cost − shipping, wire fee & others.' },
    { key: 'days', label: 'PI → Final invoice', icon: 'mdi-timer-outline', accent: neutral,
      value: biDays(t.avgDaysToFinal), sub: t.finalizedCount
        ? `avg · ${biDays(t.minDaysToFinal)}–${biDays(t.maxDaysToFinal)} · ${t.finalizedCount}/${t.piCount} invoiced`
        : 'No final invoice yet' },
  ]
})

const activeCustomers = computed(() =>
  summary.value?.customers.filter(c => c.rfqCount || c.sentQuoteCount || c.piCount).length ?? 0)
const customersWithBought = computed(() => summary.value?.customers.filter(c => c.boughtValue > 0).length ?? 0)

// ── Funnel ──
const funnel = computed(() => {
  const t = summary.value!.totals
  const p = palette.value
  const steps = [
    { label: 'RFQs', count: t.rfqCount, color: p.rfq, base: 0 },
    { label: 'Sent quotes', count: t.sentQuoteCount, color: p.quote, base: t.rfqCount },
    { label: 'Proforma invoices', count: t.piCount, color: p.pi, base: t.sentQuoteCount },
    { label: 'Final invoiced', count: t.finalizedCount, color: isDark.value ? '#8B949E' : '#64748b', base: t.piCount },
  ]
  const max = Math.max(1, ...steps.map(s => s.count))
  return steps.map(s => ({
    ...s,
    width: s.count ? Math.max(2, (s.count / max) * 100) : 0,
    rate: s.base ? `${biPercent(s.count, s.base)} of previous` : '—',
  }))
})

// ── Customer table ──
interface Cell { main: string; sub?: string; cls?: string; icon?: string }

const METRIC_COLUMNS: { key: keyof BiMetrics; title: string; cell: (m: BiMetrics) => Cell }[] = [
  { key: 'rfqCount', title: 'RFQs', cell: m => ({ main: biCount(m.rfqCount) }) },
  { key: 'sentQuoteCount', title: 'Sent quotes', cell: m => ({ main: biCount(m.sentQuoteCount) }) },
  { key: 'sentQuoteSum', title: 'Quoted value', cell: m => ({ main: biMoney(m.sentQuoteSum) }) },
  { key: 'piCount', title: 'Proformas', cell: m => ({
    main: biCount(m.piCount),
    sub: m.sentQuoteCount ? `${biPercent(m.piCount, m.sentQuoteCount)} of quotes` : undefined,
  }) },
  { key: 'boughtValue', title: 'Bought value', cell: m => ({
    main: m.boughtPiCount ? biMoney(m.boughtValue) : '—',
    sub: m.piCount ? `${m.boughtPiCount}/${m.piCount} in a PO${m.partialPoCount ? ` · ${m.partialPoCount} partly` : ''}` : undefined,
  }) },
  { key: 'supplierCost', title: 'Supplier cost', cell: m => ({ main: m.boughtPiCount ? biMoney(m.supplierCost) : '—' }) },
  { key: 'fees', title: 'Shipping, wire fee & others', cell: m => ({
    main: m.fees ? biMoney(m.fees) : '—',
    sub: m.feesEstimated ? `${biMoney(m.feesEstimated, true)} estimated` : undefined,
  }) },
  { key: 'profit', title: 'Profit', cell: m => m.boughtPiCount || m.fees
    ? {
        main: biMoney(m.profit),
        cls: m.profit < 0 ? 'text-error' : 'text-success',
        icon: m.profit < 0 ? 'mdi-trending-down' : 'mdi-trending-up',
        sub: m.boughtValue ? `${biMargin(m.profit, m.boughtValue)}% margin` : undefined,
      }
    : { main: '—' } },
  { key: 'avgDaysToFinal', title: 'Avg PI → Final', cell: m => ({
    main: biDays(m.avgDaysToFinal),
    sub: m.piCount ? `${m.finalizedCount}/${m.piCount} invoiced` : undefined,
  }) },
]

const customerHeaders = [
  { title: '', key: 'data-table-expand', width: 40, sortable: false },
  { title: 'Customer', key: 'customerName', sortable: true },
  ...METRIC_COLUMNS.map(c => ({ title: c.title, key: c.key, align: 'end' as const, sortable: true, nowrap: true })),
]

const filteredCustomers = computed(() => {
  const rows = summary.value?.customers ?? []
  const term = (customerSearch.value ?? '').trim().toLowerCase()
  if (!term) return rows
  return rows.filter(c => c.customerName?.toLowerCase().includes(term) || c.customerCode?.toLowerCase().includes(term))
})

/** Totals of the rows the filter leaves (= the server totals when nothing is filtered). */
const footerTotals = computed<BiMetrics>(() => {
  if (!customerSearch.value?.trim() && summary.value) return summary.value.totals
  const t: BiMetrics = {
    rfqCount: 0, sentQuoteCount: 0, sentQuoteSum: 0, piCount: 0, boughtPiCount: 0, partialPoCount: 0,
    boughtValue: 0, supplierCost: 0, fees: 0, feesEstimated: 0, profit: 0, unpaidPoCount: 0, unconvertedFeeCount: 0,
    finalizedCount: 0, avgDaysToFinal: null, minDaysToFinal: null, maxDaysToFinal: null,
  }
  let daysWeighted = 0
  for (const c of filteredCustomers.value) {
    t.rfqCount += c.rfqCount
    t.sentQuoteCount += c.sentQuoteCount
    t.sentQuoteSum += c.sentQuoteSum
    t.piCount += c.piCount
    t.boughtPiCount += c.boughtPiCount
    t.partialPoCount += c.partialPoCount
    t.boughtValue += c.boughtValue
    t.supplierCost += c.supplierCost
    t.fees += c.fees
    t.feesEstimated += c.feesEstimated
    t.profit += c.profit
    t.finalizedCount += c.finalizedCount
    if (c.avgDaysToFinal != null) {
      daysWeighted += c.avgDaysToFinal * c.finalizedCount
      t.minDaysToFinal = t.minDaysToFinal == null ? c.minDaysToFinal : Math.min(t.minDaysToFinal, c.minDaysToFinal ?? Infinity)
      t.maxDaysToFinal = t.maxDaysToFinal == null ? c.maxDaysToFinal : Math.max(t.maxDaysToFinal, c.maxDaysToFinal ?? 0)
    }
  }
  t.avgDaysToFinal = t.finalizedCount ? Math.round((daysWeighted / t.finalizedCount) * 10) / 10 : null
  return t
})

function toggleRow(_: unknown, { item }: { item: { customerId: number } }) {
  const id = item.customerId
  expanded.value = expanded.value.includes(id) ? expanded.value.filter(x => x !== id) : [...expanded.value, id]
}

// ── Export (xlsx loaded only when used) ──
async function exportExcel() {
  if (!summary.value) return
  exporting.value = true
  try {
    const { downloadExcel } = await import('~/utils/exportExcel')
    const row = (name: string, code: string, m: BiMetrics) => ({
      Customer: name,
      Code: code,
      RFQs: m.rfqCount,
      'Sent quotes': m.sentQuoteCount,
      'Quoted value': Number(m.sentQuoteSum),
      'Proforma invoices': m.piCount,
      'Proformas in a PO': m.boughtPiCount,
      'Bought value': Number(m.boughtValue),
      'Supplier cost': Number(m.supplierCost),
      'Shipping, wire fee & others': Number(m.fees),
      'Of which estimated (unpaid POs)': Number(m.feesEstimated),
      Profit: Number(m.profit),
      'Margin %': biMargin(m.profit, m.boughtValue),
      'Final invoiced': m.finalizedCount,
      'Avg days PI → FI': m.avgDaysToFinal,
      'Min days PI → FI': m.minDaysToFinal,
      'Max days PI → FI': m.maxDaysToFinal,
    })
    const rows = summary.value.customers.map(c => row(c.customerName, c.customerCode ?? '', c))
    rows.push(row('ALL CUSTOMERS', '', summary.value.totals))
    downloadExcel(rows, `BI_last_${summary.value.days}_days_${new Date().toISOString().slice(0, 10)}`, 'BI')
  } finally {
    exporting.value = false
  }
}

/** One metric cell: value, optional trend icon and a muted second line. */
const MetricCell = defineComponent({
  props: { cell: { type: Object as PropType<Cell>, required: true } },
  setup(props) {
    return () => h('div', { class: 'bi-cell' }, [
      h('span', { class: ['bi-cell__main', props.cell.cls] }, [
        props.cell.icon ? h('i', { class: ['mdi', props.cell.icon, 'bi-cell__icon'], 'aria-hidden': 'true' }) : null,
        props.cell.main,
      ]),
      props.cell.sub ? h('span', { class: 'bi-cell__sub' }, props.cell.sub) : null,
    ])
  },
})
</script>

<style scoped>
.bi-scope {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

/* ── KPI tiles ── */
.bi-kpis {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 16px;
}
.bi-kpi {
  position: relative;
  padding: 16px 18px 14px;
  overflow: hidden;
}
.bi-kpi::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: var(--bi-accent);
}
.bi-kpi--skeleton {
  border-radius: 8px;
}
.bi-kpi__head {
  display: flex;
  align-items: center;
  gap: 8px;
  color: rgba(var(--v-theme-on-surface), 0.7);
}
.bi-kpi__icon {
  color: var(--bi-accent);
}
.bi-kpi__label {
  font-size: 0.8rem;
  font-weight: 500;
  letter-spacing: 0.01em;
}
.bi-kpi__hint {
  margin-left: auto;
  opacity: 0.55;
  cursor: help;
}
.bi-kpi__value {
  margin-top: 6px;
  font-size: 1.75rem;
  line-height: 1.2;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.bi-kpi__sub {
  margin-top: 4px;
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
  display: flex;
  align-items: center;
}

.bi-card-title {
  display: flex;
  align-items: center;
  font-weight: 600;
  margin-bottom: 12px;
}

/* ── Funnel ── */
.bi-funnel {
  display: grid;
  gap: 10px;
}
.bi-funnel__step {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 300px;
  align-items: center;
  gap: 16px;
}
.bi-funnel__bar-wrap {
  height: 14px;
  border-radius: 4px;
  background: rgba(var(--v-theme-on-surface), 0.06);
  overflow: hidden;
}
.bi-funnel__bar {
  height: 100%;
  border-radius: 4px;
  transition: width 0.5s ease;
}
.bi-funnel__meta {
  display: flex;
  align-items: baseline;
  gap: 8px;
  font-size: 0.85rem;
}
.bi-funnel__label {
  min-width: 128px;
}
.bi-funnel__count {
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.bi-funnel__rate {
  color: rgba(var(--v-theme-on-surface), 0.6);
  font-size: 0.75rem;
}
@media (max-width: 700px) {
  .bi-funnel__step {
    grid-template-columns: minmax(0, 1fr);
    gap: 4px;
  }
}

/* ── Charts ── */
.bi-charts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}
.bi-charts__wide {
  grid-column: 1 / -1;
}
@media (max-width: 1100px) {
  .bi-charts {
    grid-template-columns: minmax(0, 1fr);
  }
}

/* ── Table ── */
.bi-table {
  background: transparent !important;
}
.bi-table :deep(tbody tr) {
  cursor: pointer;
}
.bi-table :deep(th) {
  white-space: nowrap;
}
.bi-table__customer {
  display: flex;
  flex-direction: column;
  line-height: 1.25;
  min-width: 180px;
}
.bi-table__expanded {
  background: rgba(var(--v-theme-on-surface), 0.03);
  cursor: default !important;
}
.bi-table__total td {
  border-top: 2px solid rgba(var(--v-theme-on-surface), 0.16);
  background: rgba(var(--v-theme-primary), 0.05);
  padding-top: 10px !important;
  padding-bottom: 10px !important;
}
.bi-table :deep(.bi-cell) {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  line-height: 1.25;
}
.bi-table :deep(.bi-cell__main) {
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
.bi-table :deep(.bi-cell__icon) {
  font-size: 14px;
  margin-right: 4px;
}
.bi-table :deep(.bi-cell__sub) {
  font-size: 0.72rem;
  color: rgba(var(--v-theme-on-surface), 0.55);
  white-space: nowrap;
}
.bi-note {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
}
</style>
