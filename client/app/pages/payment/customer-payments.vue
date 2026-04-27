<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold">Customer Payments</h1>
        <div class="text-caption text-medium-emphasis">
          Every Customer POP uploaded against a proforma invoice — grouped by customer.
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

    <!-- Stat strip -->
    <v-row class="mb-4" dense>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Customers</div>
          <div class="text-h5 font-weight-bold">{{ totalCustomers }}</div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Total POPs</div>
          <div class="text-h5 font-weight-bold">{{ totalPayments }}</div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Grand Total</div>
          <div class="text-h5 font-weight-bold text-success">${{ formatPrice(grandTotal) }}</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Empty / loading -->
    <v-card v-if="loading && !customers.length" class="glass-card d-flex justify-center pa-12">
      <v-progress-circular indeterminate color="primary" />
    </v-card>
    <v-card v-else-if="!filteredCustomers.length" class="glass-card text-center pa-12">
      <v-icon icon="mdi-cash-remove" size="48" color="grey" class="mb-2" />
      <p class="text-body-2 text-medium-emphasis">No customer payments yet.</p>
    </v-card>

    <!-- Customer cards -->
    <div v-for="c in filteredCustomers" :key="c.customerId ?? c.customerName" class="mb-4">
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
            <div class="font-weight-bold text-body-1 text-truncate">{{ c.customerName }}</div>
            <div class="text-caption text-medium-emphasis">
              {{ c.paymentCount }} payment(s) across {{ c.invoiceCount }} invoice(s)
            </div>
          </div>
          <div class="text-right">
            <div class="text-caption text-medium-emphasis font-weight-bold uppercase">Total Paid</div>
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
                    <th>File</th>
                    <th>Invoice</th>
                    <th class="text-right">Amount</th>
                    <th class="text-right">Invoice Total</th>
                    <th>Invoice Status</th>
                    <th>Notes</th>
                    <th>Uploaded</th>
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
                    <td class="text-right cell-price">${{ formatPrice(p.amount) }}</td>
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
                    <td class="text-caption">{{ new Date(p.createdAt).toLocaleString() }}</td>
                    <td class="text-right">
                      <v-btn
                        size="small"
                        variant="tonal"
                        color="success"
                        prepend-icon="mdi-download"
                        @click="download(p)"
                      >Download</v-btn>
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
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const config = useRuntimeConfig()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

type Payment = {
  id: number
  fileName: string
  amount: number
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
  totalPaid: number
  paymentCount: number
  invoiceCount: number
  payments: Payment[]
}

const loading = ref(true)
const customers = ref<CustomerGroup[]>([])
const totalCustomers = ref(0)
const totalPayments = ref(0)
const grandTotal = ref(0)
const search = ref('')
const expanded = ref(new Set<number>())

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
    customers.value = data.customers ?? []
    totalCustomers.value = data.totalCustomers ?? 0
    totalPayments.value = data.totalPayments ?? 0
    grandTotal.value = data.grandTotal ?? 0
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

const filteredCustomers = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return customers.value
  // Filter customers whose name/invoice/filename matches; keep only matching payments per customer
  return customers.value
    .map(c => {
      const customerHit = c.customerName.toLowerCase().includes(q)
      const matchedPayments = c.payments.filter(p =>
        customerHit ||
        p.fileName.toLowerCase().includes(q) ||
        p.invoiceNumber.toLowerCase().includes(q) ||
        (p.notes || '').toLowerCase().includes(q)
      )
      if (matchedPayments.length === 0) return null
      return { ...c, payments: matchedPayments }
    })
    .filter((x): x is CustomerGroup => x !== null)
})

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
      `${config.public.apiBase}/documents/proforma-invoice/${p.invoiceId}/file`,
      {
        method: 'GET',
        query: { name: p.fileName, category: 'customer_pop' },
        responseType: 'blob',
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      }
    )
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', p.fileName)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch {
    showSnack('Download failed', 'error')
  }
}

onMounted(load)
</script>

<style scoped>
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
