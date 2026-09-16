<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold">Payment Deposit</h1>
        <div class="text-caption text-medium-emphasis">
          Every Customer POP uploaded against a Sales Order — grouped by customer.
        </div>
      </div>
      <v-spacer />
      <v-text-field
        v-model="search"
        prepend-inner-icon="mdi-magnify"
        placeholder="Filter customer / invoice / file…"
        density="compact"
        hide-details
        variant="outlined"
        style="max-width: 320px;"
      />
      <v-btn variant="tonal" color="primary" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
    </div>

    <div class="d-flex flex-wrap align-center gap-3 mb-3">
      <v-tabs v-model="activeTab" color="primary" density="comfortable">
        <!-- <v-tab value="payments" prepend-icon="mdi-cash-plus">Customer POPs</v-tab> -->
        <v-tab value="remaining" prepend-icon="mdi-cash-clock">Remaining Payments</v-tab>
        <v-tab value="finished" prepend-icon="mdi-check-circle-outline">Finished Payments</v-tab>
      </v-tabs>
    </div>

    <!-- Filter-aware summary for the active tab -->
    <v-row class="mb-4" dense>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Customers</div>
          <div class="text-h5 font-weight-bold">{{ activeSummary.customers }}</div>
          <div class="text-caption text-medium-emphasis">Filtered results</div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Total Pays</div>
          <div class="text-h5 font-weight-bold">{{ activeSummary.pays }}</div>
          <div class="text-caption text-medium-emphasis">Filtered results</div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Grand Total</div>
          <div class="text-h5 font-weight-bold" :class="activeSummary.totalClass">${{ formatPrice(activeSummary.total) }}</div>
          <div class="text-caption text-medium-emphasis">{{ activeSummary.totalCaption }}</div>
        </v-card>
      </v-col>
    </v-row>

    <v-card v-if="activeTab === 'remaining' || activeTab === 'finished'" class="glass-card overflow-hidden">
      <v-card-text class="pa-0">
        <v-table class="payments-table fixed-header-table" fixed-header height="calc(100vh - 360px)">
          <thead><tr>
            <th><ColFilterMenu col-key="balanceCustomer" label="Customer" :options="balanceOptions.customer" :selected="balanceFilters.selected.balanceCustomer || new Set()" :search="balanceFilters.search.balanceCustomer || ''" :sortable="false" @toggle="balanceFilters.toggle('balanceCustomer', $event)" @select-all="balanceFilters.selectAll('balanceCustomer', $event)" @clear-all="balanceFilters.clearAll('balanceCustomer')" @update:search="balanceFilters.search.balanceCustomer = $event" /></th>
            <th><ColFilterMenu col-key="balanceBase" label="Base" :options="balanceOptions.base" :selected="balanceFilters.selected.balanceBase || new Set()" :search="balanceFilters.search.balanceBase || ''" :sortable="false" @toggle="balanceFilters.toggle('balanceBase', $event)" @select-all="balanceFilters.selectAll('balanceBase', $event)" @clear-all="balanceFilters.clearAll('balanceBase')" @update:search="balanceFilters.search.balanceBase = $event" /></th>
            <th><ColFilterMenu col-key="balanceInvoice" label="Proforma Invoice" :options="balanceOptions.invoice" :selected="balanceFilters.selected.balanceInvoice || new Set()" :search="balanceFilters.search.balanceInvoice || ''" :sortable="false" @toggle="balanceFilters.toggle('balanceInvoice', $event)" @select-all="balanceFilters.selectAll('balanceInvoice', $event)" @clear-all="balanceFilters.clearAll('balanceInvoice')" @update:search="balanceFilters.search.balanceInvoice = $event" /></th>
            <th><ColFilterMenu col-key="balanceTerms" label="Payment Terms" :options="balanceOptions.terms" :selected="balanceFilters.selected.balanceTerms || new Set()" :search="balanceFilters.search.balanceTerms || ''" :sortable="false" @toggle="balanceFilters.toggle('balanceTerms', $event)" @select-all="balanceFilters.selectAll('balanceTerms', $event)" @clear-all="balanceFilters.clearAll('balanceTerms')" @update:search="balanceFilters.search.balanceTerms = $event" /></th>
            <th class="text-right"><ColRangeFilterMenu col-key="balanceInvoiceTotal" label="Invoice Total" prefix="$" :min="balanceRanges.invoiceTotal.min" :max="balanceRanges.invoiceTotal.max" :bounds="balanceBounds.invoiceTotal" @update:min="balanceRanges.invoiceTotal.min = $event" @update:max="balanceRanges.invoiceTotal.max = $event" @select-all="setRangeToBounds(balanceRanges.invoiceTotal, balanceBounds.invoiceTotal)" @clear-all="clearRange(balanceRanges.invoiceTotal)" /></th>
            <th class="text-right"><ColRangeFilterMenu col-key="balanceDueNow" label="Due Now" prefix="$" :min="balanceRanges.dueNow.min" :max="balanceRanges.dueNow.max" :bounds="balanceBounds.dueNow" @update:min="balanceRanges.dueNow.min = $event" @update:max="balanceRanges.dueNow.max = $event" @select-all="setRangeToBounds(balanceRanges.dueNow, balanceBounds.dueNow)" @clear-all="clearRange(balanceRanges.dueNow)" /></th>
            <th class="text-right"><ColRangeFilterMenu col-key="balancePaid" label="Paid" prefix="$" :min="balanceRanges.paid.min" :max="balanceRanges.paid.max" :bounds="balanceBounds.paid" @update:min="balanceRanges.paid.min = $event" @update:max="balanceRanges.paid.max = $event" @select-all="setRangeToBounds(balanceRanges.paid, balanceBounds.paid)" @clear-all="clearRange(balanceRanges.paid)" /></th>
            <th class="text-right"><ColRangeFilterMenu col-key="balanceRemaining" label="Remaining" prefix="$" :min="balanceRanges.remaining.min" :max="balanceRanges.remaining.max" :bounds="balanceBounds.remaining" @update:min="balanceRanges.remaining.min = $event" @update:max="balanceRanges.remaining.max = $event" @select-all="setRangeToBounds(balanceRanges.remaining, balanceBounds.remaining)" @clear-all="clearRange(balanceRanges.remaining)" /></th>
            <th>Status</th>
          </tr></thead>
          <tbody>
            <tr v-for="invoice in filteredInvoices" :key="invoice.invoiceId">
              <td>{{ invoice.customerName || '—' }}</td>
              <td><v-chip size="x-small" color="primary" variant="tonal">{{ baseLabel(invoice.customerBase) }}</v-chip></td>
              <td><NuxtLink v-if="isAdmin" :to="`/invoices/${invoice.invoiceId}`" class="text-primary text-decoration-none hover-underline font-weight-bold">{{ invoice.invoiceNumber }}</NuxtLink><span v-else class="font-weight-bold">{{ invoice.invoiceNumber }}</span></td>
              <td><v-chip size="x-small" :color="invoice.isPrepayment ? 'warning' : 'info'" variant="tonal">{{ paymentTermLabel(invoice) }}</v-chip></td>
              <td class="text-right">${{ formatPrice(invoice.invoiceTotal) }}</td>
              <td class="text-right font-weight-bold">${{ formatPrice(invoice.requiredAmount) }}</td>
              <td class="text-right text-success">${{ formatPrice(invoice.totalPaid) }}</td>
              <td class="text-right" :class="invoice.remainingAmount > 0 ? 'text-error font-weight-bold' : 'text-success font-weight-bold'">${{ formatPrice(invoice.remainingAmount) }}</td>
              <td><v-chip size="x-small" :color="activeTab === 'remaining' ? 'warning' : 'success'" variant="tonal">{{ activeTab === 'remaining' ? 'Remaining' : 'Finished' }}</v-chip></td>
            </tr>
            <tr v-if="!filteredInvoices.length"><td colspan="9" class="text-center text-medium-emphasis pa-6">No matching payment balances.</td></tr>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <!-- Empty / loading -->
    <v-card v-if="activeTab === 'payments' && loading && !customers.length" class="glass-card d-flex justify-center pa-12">
      <v-progress-circular indeterminate color="primary" />
    </v-card>
    <v-card v-else-if="activeTab === 'payments' && !filteredCustomers.length" class="glass-card text-center pa-12">
      <v-icon icon="mdi-cash-remove" size="48" color="grey" class="mb-2" />
      <p class="text-body-2 text-medium-emphasis">No customer payments yet.</p>
    </v-card>

    <!-- Customer cards -->
    <div v-if="activeTab === 'payments'" v-for="c in filteredCustomers" :key="c.customerId ?? c.customerName" class="mb-4">
      <v-card class="glass-card overflow-hidden">
        <!-- Customer header -->
        <div
          class="pa-3 d-flex align-center gap-3 cursor-pointer hover-bg"
          @click="toggle(c.customerId ?? -1)"
        >
          <v-avatar color="success" size="40" variant="tonal">
            <v-icon icon="mdi-domain" size="22" />
          </v-avatar>
          <div style="flex: 1; min-width: 0;">
            <div class="d-flex align-center gap-1">
              <span class="font-weight-bold text-body-1 text-truncate">{{ c.customerName }}</span>
              <ColFilterMenu col-key="paymentCustomer" label="" :options="paymentOptions.customer" :selected="paymentFilters.selected.paymentCustomer || new Set()" :search="paymentFilters.search.paymentCustomer || ''" :sortable="false" @toggle="paymentFilters.toggle('paymentCustomer', $event)" @select-all="paymentFilters.selectAll('paymentCustomer', $event)" @clear-all="paymentFilters.clearAll('paymentCustomer')" @update:search="paymentFilters.search.paymentCustomer = $event" />
            </div>
            <div class="text-caption text-medium-emphasis">
              {{ c.paymentCount }} payment(s) across {{ c.invoiceCount }} invoice(s)
            </div>
          </div>
          <div class="text-right">
            <ColRangeFilterMenu col-key="paymentCustomerTotal" label="Total Paid" prefix="$" :min="paymentRanges.customerTotal.min" :max="paymentRanges.customerTotal.max" :bounds="paymentBounds.customerTotal" @update:min="paymentRanges.customerTotal.min = $event" @update:max="paymentRanges.customerTotal.max = $event" @select-all="setRangeToBounds(paymentRanges.customerTotal, paymentBounds.customerTotal)" @clear-all="clearRange(paymentRanges.customerTotal)" />
            <div class="text-h6 font-weight-bold text-success">${{ formatPrice(c.totalPaid) }}</div>
          </div>
          <v-btn
            icon
            variant="text"
            size="small"
            :class="{ 'rotate-180': expanded.has(c.customerId ?? -1) }"
          >
            <v-icon icon="mdi-chevron-down" />
          </v-btn>
        </div>

        <!-- Payments list (expandable) -->
        <v-expand-transition>
          <div v-show="expanded.has(c.customerId ?? -1)">
            <v-divider />
            <div class="pa-3">
              <table class="payments-table">
                <thead>
                  <tr>
                    <th><ColFilterMenu col-key="paymentFile" label="File" :options="paymentOptions.file" :selected="paymentFilters.selected.paymentFile || new Set()" :search="paymentFilters.search.paymentFile || ''" :sortable="false" @toggle="paymentFilters.toggle('paymentFile', $event)" @select-all="paymentFilters.selectAll('paymentFile', $event)" @clear-all="paymentFilters.clearAll('paymentFile')" @update:search="paymentFilters.search.paymentFile = $event" /></th>
                    <th><ColFilterMenu col-key="paymentInvoice" label="Invoice" :options="paymentOptions.invoice" :selected="paymentFilters.selected.paymentInvoice || new Set()" :search="paymentFilters.search.paymentInvoice || ''" :sortable="false" @toggle="paymentFilters.toggle('paymentInvoice', $event)" @select-all="paymentFilters.selectAll('paymentInvoice', $event)" @clear-all="paymentFilters.clearAll('paymentInvoice')" @update:search="paymentFilters.search.paymentInvoice = $event" /></th>
                    <th class="text-right"><ColRangeFilterMenu col-key="paymentAmount" label="Amount" prefix="$" :min="paymentRanges.amount.min" :max="paymentRanges.amount.max" :bounds="paymentBounds.amount" @update:min="paymentRanges.amount.min = $event" @update:max="paymentRanges.amount.max = $event" @select-all="setRangeToBounds(paymentRanges.amount, paymentBounds.amount)" @clear-all="clearRange(paymentRanges.amount)" /></th>
                    <th class="text-right"><ColRangeFilterMenu col-key="paymentInvoiceTotal" label="Invoice Total" prefix="$" :min="paymentRanges.invoiceTotal.min" :max="paymentRanges.invoiceTotal.max" :bounds="paymentBounds.invoiceTotal" @update:min="paymentRanges.invoiceTotal.min = $event" @update:max="paymentRanges.invoiceTotal.max = $event" @select-all="setRangeToBounds(paymentRanges.invoiceTotal, paymentBounds.invoiceTotal)" @clear-all="clearRange(paymentRanges.invoiceTotal)" /></th>
                    <th><ColFilterMenu col-key="paymentStatus" label="Invoice Status" :options="paymentOptions.status" :selected="paymentFilters.selected.paymentStatus || new Set()" :search="paymentFilters.search.paymentStatus || ''" :sortable="false" @toggle="paymentFilters.toggle('paymentStatus', $event)" @select-all="paymentFilters.selectAll('paymentStatus', $event)" @clear-all="paymentFilters.clearAll('paymentStatus')" @update:search="paymentFilters.search.paymentStatus = $event" /></th>
                    <th><ColFilterMenu col-key="paymentNotes" label="Notes" :options="paymentOptions.notes" :selected="paymentFilters.selected.paymentNotes || new Set()" :search="paymentFilters.search.paymentNotes || ''" :sortable="false" @toggle="paymentFilters.toggle('paymentNotes', $event)" @select-all="paymentFilters.selectAll('paymentNotes', $event)" @clear-all="paymentFilters.clearAll('paymentNotes')" @update:search="paymentFilters.search.paymentNotes = $event" /></th>
                    <th><ColFilterMenu col-key="paymentUploaded" label="Uploaded" :options="paymentOptions.uploaded" :selected="paymentFilters.selected.paymentUploaded || new Set()" :search="paymentFilters.search.paymentUploaded || ''" :sortable="false" @toggle="paymentFilters.toggle('paymentUploaded', $event)" @select-all="paymentFilters.selectAll('paymentUploaded', $event)" @clear-all="paymentFilters.clearAll('paymentUploaded')" @update:search="paymentFilters.search.paymentUploaded = $event" /></th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="p in c.payments" :key="p.id">
                    <td>
                      <div class="d-flex align-center gap-2">
                        <v-icon icon="mdi-file-document-check-outline" color="success" size="18" />
                        <span class="text-body-2 font-weight-medium">{{ p.fileName }}</span>
                      </div>
                    </td>
                    <td>
                      <NuxtLink
                        v-if="isAdmin"
                        :to="`/invoices/${p.invoiceId}`"
                        class="text-primary text-decoration-none hover-underline font-weight-bold"
                      >{{ p.invoiceNumber }}</NuxtLink>
                      <span v-else class="font-weight-bold">{{ p.invoiceNumber }}</span>
                    </td>
                    <td class="text-right cell-price">
                      <div>${{ formatPrice(p.amount) }} USD</div>
                      <div v-if="p.currency !== 'USD'" class="text-caption text-medium-emphasis font-weight-regular">
                        Received {{ currencySymbol(p.currency) }}{{ formatPrice(p.receivedAmount) }} {{ p.currency }}
                        <span v-if="p.exchangeRate"> · 1 {{ p.currency }} = ${{ formatRate(p.exchangeRate) }}</span>
                      </div>
                    </td>
                    <td class="text-right">${{ formatPrice(p.invoiceTotal) }}</td>
                    <td>
                      <v-chip
                        size="x-small"
                        :color="invoiceStatusColor(p.invoiceStatus)"
                        variant="tonal"
                        class="font-weight-bold"
                      >{{ p.invoiceStatus }}</v-chip>
                    </td>
                    <td class="text-caption">{{ p.notes || '—' }}</td>
                    <td class="text-caption">{{ uploadedLabel(p.createdAt) }}</td>
                    <td class="text-right">
                      <v-btn
                        size="small"
                        variant="tonal"
                        color="primary"
                        prepend-icon="mdi-eye-outline"
                        @click="download(p)"
                      >Preview</v-btn>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </v-expand-transition>
      </v-card>
    </div>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      :is-pdf="docPreview.isPdf(docPreview.fileName.value, docPreview.mimeType.value)"
      @close="docPreview.close()"
      @download="docPreview.download()"
    />
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const config = useRuntimeConfig()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const isAdmin = computed(() => authStore.isAdmin)

type Payment = {
  id: number
  fileName: string
  amount: number
  receivedAmount: number
  currency: string
  exchangeRate?: number | null
  notes?: string | null
  createdAt: string
  invoiceId: number
  invoiceNumber: string
  invoiceTotal: number
  invoiceStatus: string
}
type CustomerGroup = {
  customerId: number | null
  customerName: string
  customerBase?: number | null
  totalPaid: number
  paymentCount: number
  invoiceCount: number
  payments: Payment[]
}
type InvoiceBalance = {
  invoiceId: number; invoiceNumber: string; customerName?: string | null; customerBase?: number | null; invoiceTotal: number
  paymentTerm?: string | null; prepaymentPercent?: number | null; requiredAmount: number; totalPaid: number; remainingAmount: number; isPrepayment: boolean
}

const loading = ref(true)
const customers = ref<CustomerGroup[]>([])
const search = ref('')
const expanded = ref(new Set<number>())
const activeTab = ref<'payments' | 'remaining' | 'finished'>('remaining')
const invoices = ref<InvoiceBalance[]>([])

const paymentFilters = useColFilter()
const balanceFilters = useColFilter()
type Range = { min: number | null; max: number | null }
const range = (): Range => ({ min: null, max: null })
const paymentRanges = reactive({ customerTotal: range(), amount: range(), invoiceTotal: range() })
const balanceRanges = reactive({ invoiceTotal: range(), dueNow: range(), paid: range(), remaining: range() })

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text; snackbarColor.value = color; snackbar.value = true
}

async function load() {
  loading.value = true
  try {
    const data = await api.get<any>('/documents/customer-payments/all')
    // Only catalog customers with a real C-prefixed customer code belong on
    // this page. The API enforces the same rule; retaining it here keeps the
    // UI correct during a rolling API update and for cached responses.
    customers.value = (data.customers ?? []).filter((customer: CustomerGroup) => hasCustomerCode(customer.customerName))
    invoices.value = (data.invoices ?? []).filter((invoice: InvoiceBalance) => hasCustomerCode(invoice.customerName))
    // Auto-expand the first customer for instant visibility on landing
    if (customers.value.length && !expanded.value.size) {
      expanded.value.add(customers.value[0].customerId ?? -1)
    }
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to load customer payments', 'error')
  } finally {
    loading.value = false
  }
}

function toggle(id: number) {
  if (expanded.value.has(id)) expanded.value.delete(id)
  else expanded.value.add(id)
}

function hasCustomerCode(customerCode: string | null | undefined) {
  return !!customerCode?.trim() && customerCode.trim().toUpperCase().startsWith('C')
}

function uploadedLabel(createdAt: string) {
  return new Date(createdAt).toLocaleString()
}

function baseLabel(base: number | null | undefined) {
  return base == null ? '—' : String(base)
}

function currencySymbol(currency: string) {
  return ({ USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' } as Record<string, string>)[currency] ?? `${currency} `
}

function formatRate(value: number) {
  return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 8 })
}

function optionList(values: Array<string | null | undefined>) {
  return [...new Set(values.map(value => value || '—'))].sort((a, b) => a.localeCompare(b))
}

function bounds(values: number[]) {
  const finite = values.filter(Number.isFinite)
  return finite.length ? { lo: Math.min(...finite), hi: Math.max(...finite) } : null
}

function matchesSelected(filter: ReturnType<typeof useColFilter>, key: string, value: string) {
  const selected = filter.selected[key]
  return !selected?.size || selected.has(value)
}

function matchesRange(value: number, current: Range) {
  return (current.min == null || value >= current.min) && (current.max == null || value <= current.max)
}

function setRangeToBounds(current: Range, currentBounds: { lo: number; hi: number } | null) {
  if (!currentBounds) return
  current.min = currentBounds.lo
  current.max = currentBounds.hi
}

function clearRange(current: Range) {
  current.min = null
  current.max = null
}

const allPayments = computed(() => customers.value.flatMap(customer => customer.payments))
const paymentOptions = computed(() => ({
  customer: optionList(customers.value.map(c => c.customerName)),
  file: optionList(allPayments.value.map(p => p.fileName)),
  invoice: optionList(allPayments.value.map(p => p.invoiceNumber)),
  status: optionList(allPayments.value.map(p => p.invoiceStatus)),
  notes: optionList(allPayments.value.map(p => p.notes)),
  uploaded: optionList(allPayments.value.map(p => uploadedLabel(p.createdAt))),
}))
const paymentBounds = computed(() => ({
  customerTotal: bounds(customers.value.map(c => c.totalPaid)),
  amount: bounds(allPayments.value.map(p => p.amount)),
  invoiceTotal: bounds(allPayments.value.map(p => p.invoiceTotal)),
}))

const balanceTabInvoices = computed(() => invoices.value.filter(invoice =>
  activeTab.value === 'finished' ? invoice.remainingAmount <= 0 : invoice.remainingAmount > 0
))
const balanceOptions = computed(() => ({
  customer: optionList(balanceTabInvoices.value.map(i => i.customerName)),
  base: optionList(balanceTabInvoices.value.map(i => baseLabel(i.customerBase))),
  invoice: optionList(balanceTabInvoices.value.map(i => i.invoiceNumber)),
  terms: optionList(balanceTabInvoices.value.map(paymentTermLabel)),
}))
const balanceBounds = computed(() => ({
  invoiceTotal: bounds(balanceTabInvoices.value.map(i => i.invoiceTotal)),
  dueNow: bounds(balanceTabInvoices.value.map(i => i.requiredAmount)),
  paid: bounds(balanceTabInvoices.value.map(i => i.totalPaid)),
  remaining: bounds(balanceTabInvoices.value.map(i => i.remainingAmount)),
}))

function matchesPaymentFilters(payment: Payment) {
  return matchesSelected(paymentFilters, 'paymentFile', payment.fileName)
    && matchesSelected(paymentFilters, 'paymentInvoice', payment.invoiceNumber)
    && matchesSelected(paymentFilters, 'paymentStatus', payment.invoiceStatus)
    && matchesSelected(paymentFilters, 'paymentNotes', payment.notes || '—')
    && matchesSelected(paymentFilters, 'paymentUploaded', uploadedLabel(payment.createdAt))
    && matchesRange(payment.amount, paymentRanges.amount)
    && matchesRange(payment.invoiceTotal, paymentRanges.invoiceTotal)
}

const filteredCustomers = computed(() => {
  const q = search.value.trim().toLowerCase()
  return customers.value
    .map(c => {
      if (!matchesSelected(paymentFilters, 'paymentCustomer', c.customerName) || !matchesRange(c.totalPaid, paymentRanges.customerTotal)) return null
      const customerHit = !q || c.customerName.toLowerCase().includes(q)
      const matchedPayments = c.payments
        .filter(p => customerHit || p.fileName.toLowerCase().includes(q) || p.invoiceNumber.toLowerCase().includes(q) || (p.notes || '').toLowerCase().includes(q))
        .filter(matchesPaymentFilters)
      if (matchedPayments.length === 0) return null
      return {
        ...c,
        payments: matchedPayments,
        paymentCount: matchedPayments.length,
        invoiceCount: new Set(matchedPayments.map(p => p.invoiceId)).size,
        totalPaid: matchedPayments.reduce((sum, p) => sum + p.amount, 0),
      }
    })
    .filter((x): x is CustomerGroup => x !== null)
})

const filteredInvoices = computed(() => {
  const q = search.value.trim().toLowerCase()
  return balanceTabInvoices.value.filter(invoice => {
    const matchesSearch = !q || [invoice.customerName, baseLabel(invoice.customerBase), invoice.invoiceNumber, invoice.paymentTerm]
      .some(value => value?.toLowerCase().includes(q))
    return matchesSearch
      && matchesSelected(balanceFilters, 'balanceCustomer', invoice.customerName || '—')
      && matchesSelected(balanceFilters, 'balanceBase', baseLabel(invoice.customerBase))
      && matchesSelected(balanceFilters, 'balanceInvoice', invoice.invoiceNumber)
      && matchesSelected(balanceFilters, 'balanceTerms', paymentTermLabel(invoice))
      && matchesRange(invoice.invoiceTotal, balanceRanges.invoiceTotal)
      && matchesRange(invoice.requiredAmount, balanceRanges.dueNow)
      && matchesRange(invoice.totalPaid, balanceRanges.paid)
      && matchesRange(invoice.remainingAmount, balanceRanges.remaining)
  })
})

const activeSummary = computed(() => {
  if (activeTab.value === 'payments') {
    const visibleInvoiceIds = new Set(
      filteredCustomers.value.flatMap(customer => customer.payments.map(payment => payment.invoiceId)),
    )
    return {
      customers: filteredCustomers.value.length,
      pays: visibleInvoiceIds.size,
      total: filteredCustomers.value.reduce((sum, customer) => sum + customer.totalPaid, 0),
      totalCaption: 'Filtered customer deposits',
      totalClass: 'text-success',
    }
  }

  const visibleInvoiceIds = new Set(filteredInvoices.value.map(invoice => invoice.invoiceId))
  const customerNames = new Set(filteredInvoices.value.map(invoice => invoice.customerName || '—'))
  const isRemaining = activeTab.value === 'remaining'
  return {
    customers: customerNames.size,
    pays: visibleInvoiceIds.size,
    total: filteredInvoices.value.reduce((sum, invoice) =>
      sum + (isRemaining ? invoice.remainingAmount : invoice.totalPaid), 0),
    totalCaption: isRemaining ? 'Filtered remaining balance' : 'Filtered finished payments',
    totalClass: isRemaining ? 'text-warning' : 'text-success',
  }
})

function paymentTermLabel(invoice: InvoiceBalance) {
  return invoice.isPrepayment
    ? `Prepayment ${invoice.prepaymentPercent ?? 100}%`
    : invoice.paymentTerm || 'Full payment'
}

function invoiceStatusColor(status: string): string {
  const s = (status || '').toLowerCase()
  if (s === 'paid') return 'success'
  if (s === 'accepted' || s === 'sent') return 'info'
  if (s.startsWith('net')) return 'warning'
  if (s === 'rejected' || s === 'cancelled' || s === 'draft') return 'grey'
  return 'grey'
}

async function download(p: Payment) {
  try {
    const blob = await $fetch<Blob>(
      `${api.baseURL}/documents/proforma-invoice/${p.invoiceId}/file`,
      {
        method: 'GET',
        query: { name: p.fileName, category: 'customer_pop' },
        responseType: 'blob',
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      }
    )
    docPreview.previewBlob(blob as Blob, p.fileName)
  } catch {
    showSnack('Preview failed', 'error')
  }
}

onMounted(load)
</script>

<style scoped>
.fixed-header-table :deep(.v-table__wrapper) {
  max-height: none !important;
  overflow-y: auto !important;
}

.fixed-header-table :deep(.v-table__wrapper > table) {
  border-collapse: separate !important;
  border-spacing: 0;
  overflow: visible !important;
}

.fixed-header-table :deep(.v-table__wrapper > table > thead) {
  position: static !important;
}

.fixed-header-table :deep(thead th) {
  position: sticky !important;
  top: 0;
  z-index: 11 !important;
  background: rgb(var(--v-theme-surface)) !important;
  box-shadow: 0 1px 0 rgba(var(--v-border-color), 0.7);
}

.payments-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}
.payments-table thead th {
  text-align: left;
  font-size: 10px;
  letter-spacing: 0.4px;
  font-weight: 700;
  text-transform: uppercase;
  color: rgba(var(--v-theme-on-surface), 0.7);
  padding: 8px 10px;
  border-bottom: 2px solid rgba(var(--v-theme-primary), 0.25);
  white-space: nowrap;
}
.payments-table tbody td {
  padding: 8px 10px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
  vertical-align: middle;
}
.payments-table tbody tr:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}
.text-right { text-align: right; }
.cell-price { font-weight: 700; color: rgb(var(--v-theme-success)); }

.hover-bg:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}
.cursor-pointer { cursor: pointer; }
.rotate-180 { transform: rotate(180deg); transition: transform 0.2s; }

.hover-underline:hover { text-decoration: underline !important; }
</style>
