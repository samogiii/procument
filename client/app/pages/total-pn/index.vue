<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold">Total P/N</h1>
        <div class="text-caption text-medium-emphasis">
          One row per PO line — joined across PO, Procurement, Invoice, Quote, Final Invoice and Customer Payments.
        </div>
      </div>
      <v-spacer />
      <v-text-field
        v-model="search"
        prepend-inner-icon="mdi-magnify"
        placeholder="Filter rows…"
        density="compact"
        hide-details
        variant="outlined"
        style="max-width: 280px;"
      />
      <v-btn variant="tonal" color="primary" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
      <v-btn variant="tonal" color="success" prepend-icon="mdi-download" :disabled="!filteredRows.length" @click="exportCsv">Export CSV</v-btn>
    </div>

    <v-card class="glass-card">
      <div v-if="loading && !rows.length" class="d-flex justify-center pa-12">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <div v-else-if="!filteredRows.length" class="text-center pa-12">
        <v-icon icon="mdi-table-off" size="48" color="grey" class="mb-2" />
        <p class="text-body-2 text-medium-emphasis">No rows.</p>
      </div>

      <div v-else class="excel-container">
        <table class="tpn-table">
          <thead>
            <tr>
              <th>#</th>
              <th>PO#</th>
              <th>PO Ref#</th>
              <th>Quotation Expert</th>
              <th>Procurement Expert</th>
              <th>Customer</th>
              <th>Supplier</th>
              <th>P/N</th>
              <th>Description</th>
              <th>QTY</th>
              <th>CD</th>
              <th>Priority</th>
              <th>Warehouse</th>
              <th>SN#</th>
              <th>PI# to Customer</th>
              <th class="text-right">Purchasing Unit Price (USD)</th>
              <th class="text-right">Purchasing Total Price (USD)</th>
              <th class="text-right">PO Amount</th>
              <th>DP#</th>
              <th>Supplier Delivery Time</th>
              <th>Status</th>
              <th class="text-right">Selling Unit Price (USD)</th>
              <th class="text-right">Selling Total Price (USD)</th>
              <th class="text-right">Selling Unit Price (Yuan)</th>
              <th class="text-right">Selling Total Price (Yuan)</th>
              <th class="text-right">INV Amount</th>
              <th>PO Date</th>
              <th>INV Date</th>
              <th class="text-right">Received</th>
              <th>Received Date</th>
              <th>Payment Term</th>
              <th>Customer Delivery Time</th>
              <th class="text-right">Rate</th>
              <th>Track#</th>
              <th class="text-right">Shipping Cost</th>
              <th>NOTE 02</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(r, idx) in filteredRows" :key="r.id">
              <td class="text-center">{{ idx + 1 }}</td>
              <td class="font-weight-bold">
                <NuxtLink v-if="r.poNumber" :to="`/purchase-orders/${r.id}`" class="text-primary text-decoration-none hover-underline">
                  {{ r.poNumber }}
                </NuxtLink>
                <span v-else class="text-medium-emphasis">—</span>
              </td>
              <td class="text-center">{{ r.poRef ?? '—' }}</td>
              <td>{{ r.quotationExpert || '—' }}</td>
              <td>{{ r.procurementExpert || '—' }}</td>
              <td>{{ r.customer || '—' }}</td>
              <td>{{ r.supplier || '—' }}</td>
              <td class="cell-pn">{{ r.partNumber || '—' }}</td>
              <td>{{ r.description || '—' }}</td>
              <td class="text-center">{{ r.qty }}</td>
              <td>{{ r.condition || '—' }}</td>
              <td>{{ r.priority || '—' }}</td>
              <td>{{ r.warehouse || '—' }}</td>
              <td>{{ r.serialNumber || '—' }}</td>
              <td>{{ r.customerInvoiceNumber || '—' }}</td>
              <td class="text-right">${{ formatPrice(r.purchasingUnitPriceUsd) }}</td>
              <td class="text-right cell-price">${{ formatPrice(r.purchasingTotalPriceUsd) }}</td>
              <td class="text-right">{{ r.poAmount != null ? `$${formatPrice(r.poAmount)}` : '—' }}</td>
              <td>{{ r.dpNumber || '—' }}</td>
              <td>{{ r.supplierDeliveryTime || '—' }}</td>
              <td>
                <select
                  :value="r.status || 'Not Started'"
                  class="status-select"
                  :class="statusColorClass(r.status)"
                  @change="updateStatus(r, ($event.target as HTMLSelectElement).value)"
                >
                  <option v-for="s in statusOptions" :key="s" :value="s">{{ s }}</option>
                </select>
              </td>
              <td class="text-right">${{ formatPrice(r.sellingUnitPriceUsd) }}</td>
              <td class="text-right cell-price">${{ formatPrice(r.sellingTotalPriceUsd) }}</td>
              <td class="text-right">¥{{ formatPrice(r.sellingUnitPriceYuan) }}</td>
              <td class="text-right cell-price">¥{{ formatPrice(r.sellingTotalPriceYuan) }}</td>
              <td class="text-right">{{ r.invAmount != null ? `$${formatPrice(r.invAmount)}` : '—' }}</td>
              <td class="text-caption">{{ formatDate(r.poDate) }}</td>
              <td class="text-caption">{{ formatDate(r.invDate) }}</td>
              <td class="text-right">{{ r.received != null ? `$${formatPrice(r.received)}` : '—' }}</td>
              <td class="text-caption">{{ formatDate(r.receivedDate) }}</td>
              <td>
                <v-chip v-if="r.paymentTerm" size="x-small" :color="paymentTermColor(r.paymentTerm)" variant="tonal" class="font-weight-bold">
                  {{ r.paymentTerm }}
                </v-chip>
                <span v-else class="text-medium-emphasis">—</span>
              </td>
              <td class="text-caption">{{ formatDate(r.customerDeliveryTime) }}</td>
              <td class="text-center text-caption">{{ r.rate }}</td>
              <td class="text-caption">{{ r.trackNumbers || '—' }}</td>
              <td class="text-right">{{ r.shippingCost != null ? `$${formatPrice(r.shippingCost)}` : '—' }}</td>
              <td>
                <input
                  type="text"
                  :value="r.note || ''"
                  class="note-input"
                  placeholder="Add note…"
                  @blur="updateNote(r, ($event.target as HTMLInputElement).value)"
                />
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination footer -->
      <div v-if="totalCount > pageSize" class="d-flex justify-center align-center gap-2 pa-3">
        <v-btn variant="text" size="small" :disabled="page <= 1" @click="goPage(page - 1)">Prev</v-btn>
        <span class="text-caption">Page {{ page }} / {{ Math.ceil(totalCount / pageSize) }} ({{ totalCount }} rows)</span>
        <v-btn variant="text" size="small" :disabled="page * pageSize >= totalCount" @click="goPage(page + 1)">Next</v-btn>
      </div>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()

const loading = ref(true)
const rows = ref<any[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(200)
const search = ref('')
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// Match the backend default + xlsx vocabulary
const statusOptions = [
  'Not Started',
  'Sourcing',
  'Ordered',
  'In Transit',
  'Received in Warehouse',
  'Delivered to Customer',
  'Cancelled',
]

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

function formatPrice(v: number | null | undefined) {
  if (v == null || isNaN(v as number)) return '0.00'
  return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatDate(v: string | null | undefined) {
  if (!v) return '—'
  const d = new Date(v)
  return isNaN(d.getTime()) ? '—' : d.toLocaleDateString()
}

function statusColorClass(status: string | null | undefined) {
  switch (status) {
    case 'Delivered to Customer': return 'status-success'
    case 'Received in Warehouse': return 'status-info'
    case 'In Transit':
    case 'Ordered':
    case 'Sourcing': return 'status-warning'
    case 'Cancelled': return 'status-error'
    default: return 'status-grey'
  }
}

function paymentTermColor(term: string) {
  const t = term.toLowerCase()
  if (t === 'paid') return 'success'
  if (t === 'accepted' || t === 'sent') return 'info'
  if (t.startsWith('net')) return 'warning'
  if (t === 'rejected' || t === 'cancelled') return 'error'
  return 'grey'
}

async function load() {
  loading.value = true
  try {
    const res = await api.get<any>(`/po-items/total-pn?page=${page.value}&pageSize=${pageSize.value}`)
    rows.value = res.items ?? res.Items ?? []
    totalCount.value = res.totalCount ?? res.TotalCount ?? rows.value.length
  } catch {
    showSnack('Failed to load Total P/N', 'error')
  } finally {
    loading.value = false
  }
}

function goPage(p: number) {
  page.value = p
  load()
}

const filteredRows = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return rows.value
  return rows.value.filter(r => {
    const blob = [
      r.poNumber, r.partNumber, r.description, r.customer, r.supplier,
      r.customerInvoiceNumber, r.status, r.note, r.priority, r.warehouse,
      r.quotationExpert, r.procurementExpert, r.trackNumbers
    ].filter(Boolean).join(' ').toLowerCase()
    return blob.includes(q)
  })
})

async function updateStatus(row: any, newStatus: string) {
  const old = row.status
  row.status = newStatus
  try {
    await api.patch(`/po-items/total-pn/${row.id}`, { status: newStatus })
    showSnack('Status updated')
  } catch {
    row.status = old
    showSnack('Failed to update status', 'error')
  }
}

async function updateNote(row: any, newNote: string) {
  if ((row.note || '') === newNote) return
  const old = row.note
  row.note = newNote
  try {
    await api.patch(`/po-items/total-pn/${row.id}`, { note: newNote })
    showSnack('Note saved')
  } catch {
    row.note = old
    showSnack('Failed to save note', 'error')
  }
}

function exportCsv() {
  const headers = [
    '#', 'PO#', 'PO Ref#', 'Quotation Expert', 'Procurement Expert', 'Customer', 'Supplier',
    'P/N', 'Description', 'QTY', 'CD', 'Priority', 'Warehouse', 'SN#', 'PI# to Customer',
    'Purchasing Unit Price (USD)', 'Purchasing Total Price (USD)', 'PO Amount', 'DP#',
    'Supplier Delivery Time', 'Status', 'Selling Unit Price (USD)', 'Selling Total Price (USD)',
    'Selling Unit Price (Yuan)', 'Selling Total Price (Yuan)', 'INV Amount',
    'PO Date', 'INV Date', 'Received', 'Received Date', 'Payment Term', 'Customer Delivery Time',
    'Rate', 'Track#', 'Shipping Cost', 'NOTE 02'
  ]
  const esc = (v: any) => {
    if (v == null) return ''
    const s = String(v).replace(/"/g, '""')
    return /[",\n]/.test(s) ? `"${s}"` : s
  }
  const lines = [headers.join(',')]
  filteredRows.value.forEach((r, i) => {
    lines.push([
      i + 1, r.poNumber, r.poRef, r.quotationExpert, r.procurementExpert, r.customer, r.supplier,
      r.partNumber, r.description, r.qty, r.condition, r.priority, r.warehouse, r.serialNumber, r.customerInvoiceNumber,
      r.purchasingUnitPriceUsd, r.purchasingTotalPriceUsd, r.poAmount, r.dpNumber,
      r.supplierDeliveryTime, r.status, r.sellingUnitPriceUsd, r.sellingTotalPriceUsd,
      r.sellingUnitPriceYuan, r.sellingTotalPriceYuan, r.invAmount,
      r.poDate, r.invDate, r.received, r.receivedDate, r.paymentTerm, r.customerDeliveryTime,
      r.rate, r.trackNumbers, r.shippingCost, r.note,
    ].map(esc).join(','))
  })
  const blob = new Blob(["﻿" + lines.join('\n')], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `total-pn-${new Date().toISOString().slice(0, 10)}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

onMounted(load)
</script>

<style scoped>
.excel-container {
  overflow-x: auto;
  border-radius: 8px;
}

.tpn-table {
  width: max-content;
  min-width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.tpn-table thead th {
  position: sticky;
  top: 0;
  background: rgb(var(--v-theme-surface));
  border-bottom: 2px solid rgba(var(--v-theme-primary), 0.3);
  padding: 8px 10px;
  text-align: left;
  font-weight: 700;
  text-transform: uppercase;
  font-size: 10px;
  letter-spacing: 0.4px;
  white-space: nowrap;
  color: rgb(var(--v-theme-on-surface));
  z-index: 1;
}

@media (prefers-color-scheme: dark) {
  .tpn-table thead th {
    background: rgba(var(--v-theme-surface), 0.95);
    color: rgb(var(--v-theme-on-surface));
  }
}

.tpn-table tbody td {
  padding: 6px 10px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
  white-space: nowrap;
}

.tpn-table tbody tr:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}

.cell-pn {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-weight: 700;
}

.cell-price {
  font-weight: 700;
  color: rgb(var(--v-theme-success));
}

.text-right { text-align: right; }
.text-center { text-align: center; }

.status-select,
.note-input {
  border: 1px solid transparent;
  background: transparent;
  padding: 3px 6px;
  border-radius: 4px;
  font-size: 12px;
  width: 100%;
  min-width: 140px;
  color: inherit;
}

.status-select:hover,
.note-input:hover {
  border-color: rgba(var(--v-border-color), 0.25);
}

.status-select:focus,
.note-input:focus {
  outline: none;
  border-color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-surface), 1);
}

.status-success { background: rgba(76, 175, 80, 0.12); color: rgb(var(--v-theme-success)); font-weight: 700; }
.status-info    { background: rgba(33, 150, 243, 0.12); color: rgb(var(--v-theme-info));    font-weight: 700; }
.status-warning { background: rgba(255, 152, 0, 0.12);  color: rgb(var(--v-theme-warning)); font-weight: 700; }
.status-error   { background: rgba(244, 67, 54, 0.12);  color: rgb(var(--v-theme-error));   font-weight: 700; }
.status-grey    { color: rgba(var(--v-theme-on-surface), 0.6); }

.hover-underline:hover { text-decoration: underline !important; }
</style>
