<template>
  <div class="create-invoice-page">
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" :to="`/quotes/${route.params.id}`" class="mr-1 flex-shrink-0" size="small" />
      <div class="min-width-0">
        <h1 class="text-h6 text-sm-h5 font-weight-bold">Create Sales Order</h1>
        <p class="text-caption text-medium-emphasis mt-1">
          Select items from {{ isMultiQuote ? 'multiple quotes' : 'Quote #' + quote?.quoteNumber }} to create a Sales Order.
        </p>
      </div>
    </div>

    <!-- Toolbar -->
    <v-card class="toolbar-card mb-4">
      <div class="d-flex flex-wrap align-center justify-space-between pa-3 gap-2">
        <div class="d-flex align-center gap-2">
          <v-chip color="primary" variant="tonal" size="small">
            {{ selectedCount }} item{{ selectedCount !== 1 ? 's' : '' }} selected
          </v-chip>
          <span class="text-body-2 text-medium-emphasis" v-if="selectedTotal > 0">
            Total: <strong style="color: #4ade80;">${{ formatPrice(selectedTotal) }}</strong>
          </span>
          <v-chip v-if="isMultiQuote" size="small" variant="outlined" color="info" class="ml-2">
            Merging {{ quoteCount }} quotes
          </v-chip>
        </div>
        <div class="d-flex flex-wrap align-center gap-2">
          <v-text-field
            v-model="subject"
            label="Subject"
            placeholder="e.g. Engine Parts"
            density="compact"
            hide-details
            variant="outlined"
            style="min-width: 200px;"
          />
          <v-text-field
            v-model="poNumber"
            label="Customer PO #"
            density="compact"
            hide-details
            variant="outlined"
            style="min-width: 150px; max-width: 180px;"
          />
          <v-text-field
            v-model="poDate"
            label="Customer PO Date"
            type="date"
            density="compact"
            hide-details
            variant="outlined"
            style="min-width: 150px; max-width: 180px;"
          />
          <v-text-field
            v-model="dueDate"
            label="Due Date"
            type="date"
            :min="today"
            density="compact"
            hide-details
            variant="outlined"
            style="min-width: 150px; max-width: 180px;"
          />
          <v-select
            v-model="paymentStatus"
            :items="['Net30', 'CAD', 'Prepayment']"
            label="Payment Terms"
            density="compact"
            hide-details
            variant="outlined"
            style="min-width: 140px; max-width: 160px;"
          />
          <div v-if="paymentStatus === 'Prepayment'" class="d-flex flex-column">
            <v-text-field
              v-model.number="prepaymentPercent"
              label="Prepayment % *"
              type="number"
              :min="1"
              :max="100"
              density="compact"
              :hide-details="!prepaymentError"
              variant="outlined"
              :error="!!prepaymentError"
              :error-messages="prepaymentError"
              style="min-width: 130px; max-width: 150px;"
            />
          </div>
          <v-btn
            color="success"
            prepend-icon="mdi-check"
            :disabled="selectedCount === 0 || !paymentStatus || (paymentStatus === 'Prepayment' && (!prepaymentPercent || prepaymentPercent <= 0))"
            :loading="saving"
            @click="createInvoice"
          >
            Create
          </v-btn>
        </div>
      </div>
    </v-card>

    <!-- Wallet Picker Dialog -->
    <v-dialog v-model="showWalletPicker" max-width="520" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-wallet-outline" class="mr-2" color="primary" />
          Select Deposit Wallet
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            Choose which wallet customer POP payments for this Sales Order should automatically deposit into.
          </p>
          <v-select
            v-model="selectedWalletId"
            :items="walletOptions"
            item-title="label"
            item-value="id"
            label="Deposit Wallet *"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-bank-outline"
          />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-btn variant="text" @click="skipWalletAndNavigate">Skip</v-btn>
          <v-spacer />
          <v-btn
            color="primary"
            variant="flat"
            :disabled="!selectedWalletId"
            :loading="savingWallet"
            @click="saveWalletAndNavigate"
          >
            <v-icon start>mdi-check</v-icon>
            Confirm
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Quote Items Grid -->
    <v-card class="excel-card">
      <div class="excel-container">
        <table class="excel-grid">
          <thead>
            <tr>
              <th style="width: 40px;">
                <input type="checkbox" :checked="isAllSelected" @change="toggleSelectAll" class="record-checkbox" />
              </th>
              <th v-if="isMultiQuote" style="width: 100px;">Quote #</th>
              <th>Part Number</th>
              <th>Description</th>
              <th style="width: 80px;">Cond</th>
              <th style="width: 100px;">Quote Qty</th>
              <th style="width: 100px;">Inv Qty</th>
              <th style="width: 140px;">Lead Time</th>
              <th style="width: 120px;">Unit Price</th>
              <th style="width: 120px;">Total</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in quoteItems"
              :key="item.id"
              class="quote-row"
              :class="{ 'selected-row': selections[item.id]?.selected }"
            >
              <td class="text-center">
                <input
                  type="checkbox"
                  :checked="selections[item.id]?.selected"
                  @change="toggleSelection(item.id)"
                  class="record-checkbox"
                />
              </td>
              <td v-if="isMultiQuote" style="font-size: 11px; font-weight: bold; color: rgba(var(--v-theme-primary), 0.7);">
                {{ item.quoteNumber }}
              </td>
              <td class="cell-pn">
                <span>{{ item.alt || item.partNumberName }}</span>
                <div v-if="item.alt" class="text-caption text-medium-emphasis" style="font-size:10px; font-family: inherit; font-weight: normal;">
                  orig: {{ item.partNumberName }}
                </div>
              </td>
              <td class="text-medium-emphasis" style="font-size: 13px;">{{ item.description || '—' }}</td>
              <td style="font-size: 12px;">{{ item.condition || 'N/A' }}</td>
              <td class="text-center" style="font-size: 13px;">{{ item.qty }}</td>
              <td>
                <input
                  type="number"
                  class="inv-qty-input"
                  v-model.number="selections[item.id].qty"
                  min="1"
                  :max="item.qty"
                  :disabled="!selections[item.id]?.selected"
                />
              </td>
              <td>
                <input
                  type="date"
                  class="inv-date-input"
                  v-model="selections[item.id].expectedDeliveryDate"
                  :min="today"
                  :disabled="!selections[item.id]?.selected"
                />
              </td>
              <td style="color: #4ade80; font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                ${{ formatPrice(item.unitPrice) }}
              </td>
              <td style="color: #4ade80; font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                ${{ formatPrice(selections[item.id]?.qty * item.unitPrice || 0) }}
              </td>
            </tr>
            <tr v-if="quoteItems.length === 0 && !loading">
              <td :colspan="isMultiQuote ? 10 : 9" class="text-center pa-8">
                <p class="text-body-2 text-medium-emphasis">No items found in the selected quotes.</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const router = useRouter()
const api = useApi()

const today = new Date().toISOString().split('T')[0]

// State
const loading = ref(true)
const saving = ref(false)
const quote = ref<any>(null)
const quoteItems = ref<any[]>([])
const dueDate = ref('')
const subject = ref('')
const poNumber = ref('')
const poDate = ref('')
const paymentStatus = ref<string>('Prepayment')
const prepaymentPercent = ref<number | null>(null)
const prepaymentError = ref('')

// Wallet picker
const showWalletPicker = ref(false)
const walletOptions = ref<{ id: number; label: string }[]>([])
const selectedWalletId = ref<number | null>(null)
const savingWallet = ref(false)
const createdInvoiceId = ref<number | null>(null)

// selections: { [quoteItemId]: { selected: boolean, qty: number, expectedDeliveryDate: string } }
const selections = ref<Record<number, { selected: boolean; qty: number; expectedDeliveryDate: string }>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// Computeds
const quoteIds = computed(() => {
  const primaryId = route.params.id
  const additional = route.query.additionalIds ? String(route.query.additionalIds).split(',') : []
  return [primaryId, ...additional].map(Number)
})

const isMultiQuote = computed(() => quoteIds.value.length > 1)
const quoteCount = computed(() => quoteIds.value.length)

const selectedCount = computed(() =>
  Object.values(selections.value).filter(s => s.selected).length
)

const selectedTotal = computed(() =>
  quoteItems.value.reduce((sum, item) => {
    const sel = selections.value[item.id]
    if (sel && sel.selected) {
      return sum + (sel.qty * item.unitPrice)
    }
    return sum
  }, 0)
)

const isAllSelected = computed(() =>
  quoteItems.value.length > 0 && quoteItems.value.every(i => selections.value[i.id]?.selected)
)

onMounted(async () => {
  await loadData()
})

async function loadData() {
  loading.value = true
  try {
    const ids = quoteIds.value
    const quotes = await Promise.all(ids.map(id => api.get<any>(`/quotes/${id}`)))
    
    quote.value = quotes[0]
    
    // Merge all items, adding quoteNumber to each
    quoteItems.value = quotes.flatMap(q => 
      (q.items || []).map((item: any) => ({
        ...item,
        quoteNumber: q.quoteNumber
      }))
    )

    // Initialize selections
    const sel: Record<number, any> = {}
    quoteItems.value.forEach((i: any) => {
      sel[i.id] = { selected: true, qty: i.qty, expectedDeliveryDate: '' } // Default select all
    })
    selections.value = sel

    // Default due date: today + 30 days
    const d = new Date()
    d.setDate(d.getDate() + 30)
    dueDate.value = d.toISOString().split('T')[0] as string
  } catch (e) {
    showSnack('Failed to load quote data', 'error')
  } finally {
    loading.value = false
  }
}

function toggleSelection(itemId: number) {
  if (selections.value[itemId]) {
    selections.value[itemId].selected = !selections.value[itemId].selected
  }
}

function toggleSelectAll() {
  const allSelected = isAllSelected.value
  quoteItems.value.forEach(item => {
    if (selections.value[item.id]) {
      selections.value[item.id].selected = !allSelected
    }
  })
}

async function createInvoice() {
  const selectedEntries = Object.entries(selections.value)
    .filter(([_, s]) => s.selected)
    .map(([id, s]) => ({
      quoteItemId: Number(id),
      qty: s.qty,
      unitPrice: quoteItems.value.find(i => i.id === Number(id))?.unitPrice || 0,
      expectedDeliveryDate: s.expectedDeliveryDate || null
    }))

  if (selectedEntries.length === 0) {
    showSnack('Please select at least one item', 'warning')
    return
  }

  if (paymentStatus.value === 'Prepayment') {
    if (!prepaymentPercent.value || prepaymentPercent.value <= 0 || prepaymentPercent.value > 100) {
      prepaymentError.value = 'Prepayment % is required and must be between 1 and 100.'
      showSnack('Please enter a valid Prepayment %', 'error')
      return
    }
  }
  prepaymentError.value = ''

  saving.value = true
  try {
    const payload = {
      quoteId: Number(route.params.id),
      dueDate: dueDate.value || null,
      subject: subject.value || null,
      customerPONumber: poNumber.value || null,
      customerPODate: poDate.value || null,
      paymentStatus: paymentStatus.value || null,
      prepaymentPercent: paymentStatus.value === 'Prepayment' ? prepaymentPercent.value : null,
      items: selectedEntries
    }

    const res = await api.post<any>('/invoices', payload)
    showSnack('Sales Order created successfully', 'success')

    // Check wallets for this customer
    const customerId = quote.value?.customerId
    if (customerId) {
      try {
        const wallets = await api.get<any[]>(`/payment-boxes/for-customer/${customerId}`)
        if (wallets && wallets.length > 1) {
          createdInvoiceId.value = res.id
          walletOptions.value = wallets.map((w: any) => ({
            id: w.id,
            label: `${w.name || w.companyPresetName} (${w.currency})`,
          }))
          selectedWalletId.value = wallets[0].id
          showWalletPicker.value = true
          return // don't navigate yet — wait for wallet pick
        }
      } catch { /* silent — wallets not critical */ }
    }

    setTimeout(() => { router.push(`/invoices/${res.id}`) }, 500)
  } catch (e) {
    showSnack('Failed to create Sales Order', 'error')
  } finally {
    saving.value = false
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

async function saveWalletAndNavigate() {
  if (!createdInvoiceId.value || !selectedWalletId.value) {
    skipWalletAndNavigate()
    return
  }
  savingWallet.value = true
  try {
    await api.patch(`/invoices/${createdInvoiceId.value}/default-wallet`, { walletId: selectedWalletId.value })
  } catch { /* non-critical */ }
  finally { savingWallet.value = false }
  router.push(`/invoices/${createdInvoiceId.value}`)
}

function skipWalletAndNavigate() {
  showWalletPicker.value = false
  router.push(`/invoices/${createdInvoiceId.value}`)
}
</script>

<style scoped>
.create-invoice-page {
  max-width: 100%;
}

.toolbar-card {
  background: var(--card-bg) !important;
  border: 1px solid var(--card-border) !important;
}

.excel-card {
  background: var(--excel-bg) !important;
  border: 1px solid var(--excel-border) !important;
  overflow: hidden;
}

.excel-container {
  overflow-x: auto;
}

.excel-grid {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
  min-width: 700px;
}

.excel-grid thead th {
  background: var(--toolbar-bg);
  color: rgb(var(--v-theme-on-surface), 0.6);
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 12px;
  border-bottom: 2px solid var(--excel-border);
  text-align: left;
  white-space: nowrap;
}

.excel-grid tbody td {
  padding: 0 12px;
  height: 46px;
  border-bottom: 1px solid var(--card-border);
  font-size: 13px;
  vertical-align: middle;
}

.quote-row {
  transition: background-color 0.15s;
}
.quote-row:hover {
  background: var(--row-hover);
}
.quote-row.selected-row {
  background: var(--cell-hover);
}

.cell-pn {
  color: var(--pn-color);
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
}

.record-checkbox {
  width: 16px;
  height: 16px;
  accent-color: #3b82f6;
  cursor: pointer;
}

.inv-qty-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 8px;
  font-size: 13px;
  text-align: center;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.inv-qty-input:hover:not(:disabled) {
  border-color: var(--card-border);
}
.inv-qty-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
}
.inv-qty-input:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.inv-date-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 8px;
  font-size: 11px;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.inv-date-input:hover:not(:disabled) {
  border-color: var(--card-border);
}
.inv-date-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
}
.inv-date-input:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}
.inv-date-input::-webkit-calendar-picker-indicator {
  filter: var(--date-picker-invert);
  opacity: 0.6;
  cursor: pointer;
}

.text-center { text-align: center; }
</style>
