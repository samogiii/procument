<template>
  <v-container fluid class="pa-4">
    <!-- Header -->
    <div class="d-flex align-center gap-3 mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" @click="navigateTo('/payment-control')" />
      <div class="flex-1-1">
        <div class="d-flex align-center gap-2">
          <span class="text-h5 font-weight-bold">{{ detail?.companyPresetName }}</span>
          <v-chip v-if="detail" size="small" color="primary" variant="tonal">{{ detail.currency }}</v-chip>
        </div>
        <div class="text-caption text-medium-emphasis">Transaction ledger</div>
      </div>

      <!-- Stat chips -->
      <div v-if="detail" class="d-flex gap-2 flex-wrap">
        <v-chip color="success" variant="tonal" prepend-icon="mdi-arrow-down-circle">
          +{{ formatPrice(detail.totalDeposit) }}
        </v-chip>
        <v-chip color="error" variant="tonal" prepend-icon="mdi-arrow-up-circle">
          -{{ formatPrice(detail.totalWithdraw) }}
        </v-chip>
        <v-chip
          :color="detail.balance >= 0 ? 'success' : 'error'"
          variant="flat"
          prepend-icon="mdi-scale-balance"
        >
          {{ formatPrice(detail.balance) }}
        </v-chip>
      </div>

      <v-btn
        color="success"
        variant="tonal"
        prepend-icon="mdi-microsoft-excel"
        @click="exportDialog = true"
      >
        Export
      </v-btn>
      <v-btn
        v-if="authStore.isSuperAdmin"
        color="primary"
        prepend-icon="mdi-plus"
        @click="openAddTx"
      >
        Add Transaction
      </v-btn>
    </div>

    <!-- Table -->
    <v-card class="glass-card" rounded="lg">
      <v-data-table
        :headers="headers"
        :items="detail?.transactions ?? []"
        :loading="loading"
        density="comfortable"
        :items-per-page="50"
      >
        <!-- Deposit -->
        <template #item.deposit="{ item }">
          <span v-if="item.deposit != null" class="text-success font-weight-medium text-no-wrap">
            +{{ currencySymbol(item.txCurrency || detail?.currency || '') }}{{ formatPrice(item.deposit) }}
          </span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- Withdraw -->
        <template #item.withdraw="{ item }">
          <span v-if="item.withdraw != null" class="text-error font-weight-medium text-no-wrap">
            -{{ currencySymbol(item.txCurrency || detail?.currency || '') }}{{ formatPrice(item.withdraw) }}
          </span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- From (only shown for Deposit rows) -->
        <template #item.fromName="{ item }">
          <template v-if="item.type === 'Deposit'">
            <div class="d-flex align-center gap-1">
              <v-icon
                :icon="item.fromType === 'Wallet' ? 'mdi-bank-transfer' : item.fromType === 'MotherWallet' ? 'mdi-bank-outline' : 'mdi-account-outline'"
                size="14"
                class="text-medium-emphasis"
              />
              <span>{{ item.fromName ?? 'Mother Wallet' }}</span>
            </div>
          </template>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- To (only shown for Withdraw rows) -->
        <template #item.toName="{ item }">
          <template v-if="item.type === 'Withdraw'">
            <div class="d-flex align-center gap-1">
              <v-icon
                :icon="item.toType === 'Wallet' ? 'mdi-bank-transfer' : item.toType === 'MotherWallet' ? 'mdi-bank-outline' : 'mdi-truck-outline'"
                size="14"
                class="text-medium-emphasis"
              />
              <span>{{ item.toName ?? 'Mother Wallet' }}</span>
            </div>
          </template>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- PI# -->
        <template #item.piNumber="{ item }">
          <v-chip
            v-if="item.piNumber"
            size="x-small"
            color="primary"
            variant="tonal"
            class="cursor-pointer"
            @click="navigateTo('/invoices/' + item.piId)"
          >
            {{ item.piNumber }}
          </v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- PR# -->
        <template #item.prNumber="{ item }">
          <v-chip
            v-if="item.prNumber"
            size="x-small"
            color="secondary"
            variant="tonal"
            class="cursor-pointer"
            @click="navigateTo('/purchase-orders/' + item.poId)"
          >
            {{ item.prNumber }}
          </v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- Exchange Rate -->
        <template #item.exchangeRate="{ item }">
          <div v-if="item.exchangeRate != null" class="text-caption">
            <v-chip size="x-small" color="surface-variant" variant="tonal" class="mr-1">
              {{ item.txCurrency ?? detail?.currency }}
            </v-chip>
            <span>×{{ item.exchangeRate }}</span>
          </div>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- Balance -->
        <template #item.balance="{ item }">
          <span
            class="font-weight-medium"
            :class="item.balance >= 0 ? 'text-success' : 'text-error'"
          >
            {{ formatPrice(item.balance) }}
          </span>
        </template>

        <!-- Source -->
        <template #item.isAuto="{ item }">
          <v-chip
            :color="item.isAuto ? 'teal' : 'default'"
            :prepend-icon="item.isAuto ? 'mdi-robot-outline' : 'mdi-account-outline'"
            size="x-small"
            variant="tonal"
          >
            {{ item.isAuto ? 'Auto' : 'Manual' }}
          </v-chip>
        </template>

        <!-- Date -->
        <template #item.createdAt="{ item }">
          <span class="text-caption text-medium-emphasis">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </span>
        </template>

        <!-- Actions -->
        <template #item.actions="{ item }">
          <div v-if="authStore.isSuperAdmin" class="d-flex align-center">
            <v-btn
              icon="mdi-pencil-outline"
              size="x-small"
              variant="text"
              color="primary"
              @click="openEditTx(item)"
            />
            <v-btn
              icon="mdi-delete-outline"
              size="x-small"
              variant="text"
              color="error"
              @click="confirmDeleteTx(item)"
            />
          </div>
        </template>
      </v-data-table>
    </v-card>

    <!-- Export Dialog -->
    <v-dialog v-model="exportDialog" max-width="460">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Export Transactions</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <div class="text-caption text-medium-emphasis mb-2">Transaction Type</div>
          <v-btn-toggle
            v-model="exportForm.type"
            mandatory
            color="primary"
            variant="outlined"
            divided
            class="mb-4"
          >
            <v-btn value="All">All</v-btn>
            <v-btn value="Deposit">Deposits</v-btn>
            <v-btn value="Withdraw">Withdrawals</v-btn>
          </v-btn-toggle>
          <v-row dense>
            <v-col cols="6">
              <v-text-field
                v-model="exportForm.fromDate"
                label="From Date"
                type="date"
                variant="outlined"
                density="comfortable"
                clearable
              />
            </v-col>
            <v-col cols="6">
              <v-text-field
                v-model="exportForm.toDate"
                label="To Date"
                type="date"
                variant="outlined"
                density="comfortable"
                clearable
              />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="exportDialog = false">Cancel</v-btn>
          <v-btn color="success" prepend-icon="mdi-microsoft-excel" @click="doExport">Export</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Add/Edit Transaction Dialog -->
    <v-dialog v-model="txDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">
          {{ txForm.id ? 'Edit Transaction' : 'Add Transaction' }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <!-- Type -->
          <div class="mb-4">
            <div class="text-caption text-medium-emphasis mb-2">Transaction Type</div>
            <v-btn-toggle v-model="txForm.type" mandatory color="primary" variant="outlined" divided :disabled="!!txForm.id">
              <v-btn value="Deposit" prepend-icon="mdi-arrow-down-circle">Deposit</v-btn>
              <v-btn value="Withdraw" prepend-icon="mdi-arrow-up-circle">Withdraw</v-btn>
              <v-btn v-if="!txForm.id" value="Transfer" prepend-icon="mdi-bank-transfer">Transfer</v-btn>
            </v-btn-toggle>
          </div>

          <!-- Transfer UI -->
          <template v-if="txForm.type === 'Transfer'">
            <v-alert type="info" variant="tonal" density="compact" icon="mdi-shield-check-outline" class="mb-3">
              Transfer requests require acceptance and POP upload before execution. They will appear in <strong>Payment Withdraw</strong>.
            </v-alert>
            <v-autocomplete
              v-model="txForm.toBoxId"
              :items="allBoxes"
              :item-title="(b) => `${b.companyPresetName} (${b.currency})`"
              item-value="id"
              label="Transfer To Wallet"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              clearable
              @update:model-value="txForm.depositAmount = 0; txForm.transferExchangeRate = null"
            />
            <v-row dense class="mb-3">
              <v-col cols="6">
                <v-text-field
                  v-model.number="txForm.amount"
                  label="Withdraw Amount"
                  type="number"
                  variant="outlined"
                  density="comfortable"
                  :prefix="currencySymbol(detail?.currency ?? '')"
                />
              </v-col>
              <v-col cols="6">
                <v-text-field
                  v-model.number="txForm.depositAmount"
                  label="Deposit Amount"
                  type="number"
                  variant="outlined"
                  density="comfortable"
                  :prefix="currencySymbol(toBoxCurrency)"
                />
              </v-col>
            </v-row>
            <v-text-field
              v-if="showTransferRate"
              v-model.number="txForm.transferExchangeRate"
              :label="`Exchange Rate (1 ${detail?.currency} = ? ${toBoxCurrency})`"
              type="number"
              variant="outlined"
              density="comfortable"
              class="mb-3"
            />
            <v-text-field
              v-model="txForm.notes"
              label="Notes (optional)"
              variant="outlined"
              density="comfortable"
              class="mb-3"
            />
          </template>

          <!-- Deposit / Withdraw UI -->
          <template v-else>
          <!-- Amount + Currency -->
          <v-row dense class="mb-3">
            <v-col cols="7">
              <v-text-field
                v-model.number="txForm.amount"
                label="Amount"
                type="number"
                variant="outlined"
                density="comfortable"
                :prefix="currencySymbol(txForm.currency || detail?.currency || '')"
              />
            </v-col>
            <v-col cols="5">
              <v-select
                v-model="txForm.currency"
                :items="currencies"
                label="Currency"
                variant="outlined"
                density="comfortable"
                @update:model-value="txForm.exchangeRate = null"
              />
            </v-col>
          </v-row>

          <!-- Exchange Rate (only when currency differs from box currency) -->
          <v-text-field
            v-if="showExchangeRate"
            v-model.number="txForm.exchangeRate"
            :label="`Exchange Rate (1 ${txForm.currency} = ? ${detail?.currency})`"
            type="number"
            variant="outlined"
            density="comfortable"
            class="mb-3"
            hint="Used to compute balance in wallet's base currency"
            persistent-hint
          />

          <!-- From (Deposit) -->
          <template v-if="txForm.type === 'Deposit'">
            <v-select
              v-model="txForm.fromType"
              :items="[{ title: 'Mother Wallet', value: 'MotherWallet' }, { title: 'Customer', value: 'Customer' }]"
              label="From"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              @update:model-value="txForm.fromCustomerId = null; txForm.invoiceId = null"
            />
            <template v-if="txForm.fromType === 'Customer'">
              <v-autocomplete
                v-model="txForm.fromCustomerId"
                :items="customers"
                :item-title="(c) => c.customerCode ? `${c.customerCode} — ${c.name}` : c.name"
                item-value="id"
                label="Customer"
                variant="outlined"
                density="comfortable"
                class="mb-3"
                clearable
                @update:model-value="txForm.invoiceId = null"
              />
              <v-autocomplete
                v-if="txForm.fromCustomerId"
                v-model="txForm.invoiceId"
                :items="filteredInvoices"
                item-title="invoiceNumber"
                item-value="id"
                label="PI# (optional)"
                variant="outlined"
                density="comfortable"
                clearable
                no-data-text="No invoices for this customer"
                class="mb-3"
              />
            </template>
          </template>

          <!-- To (Withdraw) -->
          <template v-if="txForm.type === 'Withdraw'">
            <v-select
              v-model="txForm.toType"
              :items="[{ title: 'Mother Wallet', value: 'MotherWallet' }, { title: 'Supplier', value: 'Supplier' }]"
              label="To"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              @update:model-value="txForm.toSupplierId = null; txForm.paymentRequestId = null"
            />
            <template v-if="txForm.toType === 'Supplier'">
              <v-autocomplete
                v-model="txForm.toSupplierId"
                :items="suppliers"
                item-title="name"
                item-value="id"
                label="Supplier"
                variant="outlined"
                density="comfortable"
                class="mb-3"
                clearable
                @update:model-value="txForm.paymentRequestId = null"
              />
              <v-autocomplete
                v-if="txForm.toSupplierId"
                v-model="txForm.paymentRequestId"
                :items="filteredPaymentRequests"
                :item-title="(pr) => `PR-${pr.prNumber}`"
                item-value="id"
                label="PR# (optional)"
                variant="outlined"
                density="comfortable"
                clearable
                no-data-text="No payment requests for this supplier"
                class="mb-3"
              />
            </template>
          </template>

          <v-text-field
            v-if="txForm.id"
            v-model="txForm.createdAt"
            label="Transaction Date/Time"
            type="datetime-local"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />

          <!-- Notes -->
          <v-textarea
            v-model="txForm.notes"
            label="Notes (optional)"
            variant="outlined"
            density="comfortable"
            rows="2"
            auto-grow
          />
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="txDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="txSaving" @click="saveTx">
        {{ txForm.id ? 'Save Changes' : txForm.type === 'Transfer' ? 'Send for Approval' : 'Add' }}
      </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Negative Balance Warning -->
    <v-dialog v-model="negativeWarnDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6 d-flex align-center gap-2">
          <v-icon icon="mdi-alert-outline" color="warning" />
          Balance Will Go Negative
        </v-card-title>
        <v-card-text class="pa-4">
          This transaction will make the wallet balance negative
          (<span class="font-weight-bold text-error">{{ currencySymbol(detail?.currency ?? '') }}{{ formatPrice(projectedBalance) }}</span>).
          Do you want to proceed?
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="negativeWarnDialog = false">Cancel</v-btn>
          <v-btn color="error" @click="proceedDespiteNegative">Proceed Anyway</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>

    <!-- Delete Transaction Confirm -->
    <v-dialog v-model="deleteTxDialog" max-width="400">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Delete Transaction?</v-card-title>
        <v-card-text class="pa-4">This action cannot be undone.</v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="deleteTxDialog = false">Cancel</v-btn>
          <v-btn color="error" :loading="txDeleting" @click="deleteTx">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { formatPrice } from '~/utils/formatPrice'
import { downloadExcel } from '~/utils/exportExcel'

interface TransactionRow {
  id: number
  type: string
  deposit: number | null
  withdraw: number | null
  fromType: string
  fromName: string | null
  fromCustomerId: number | null
  toType: string
  toName: string | null
  toSupplierId: number | null
  piNumber: string | null
  piId: number | null
  prNumber: string | null
  prId: number | null
  poId: number | null
  notes: string | null
  isAuto: boolean
  createdAt: string
  balance: number
  txCurrency: string | null
  exchangeRate: number | null
}

interface BoxDetail {
  id: number
  companyPresetName: string
  currency: string
  totalDeposit: number
  totalWithdraw: number
  balance: number
  transactions: TransactionRow[]
}

interface Customer { id: number; name: string; customerCode: string | null }
interface Supplier { id: number; name: string }
interface Invoice { id: number; invoiceNumber: string; customerId: number }
interface PR { id: number; prNumber: number | null; supplierId: number | null; supplierName: string | null }

const route = useRoute()
const authStore = useAuthStore()
const api = useApi()

const id = computed(() => Number(route.params.id))
const detail = ref<BoxDetail | null>(null)
const loading = ref(true)

const exportDialog = ref(false)
const exportForm = ref({
  type: 'All' as 'All' | 'Deposit' | 'Withdraw',
  fromDate: '',
  toDate: '',
})

const negativeWarnDialog = ref(false)
const projectedBalance = ref(0)
let pendingSubmit: (() => Promise<void>) | null = null

const txDialog = ref(false)
const txSaving = ref(false)
const deleteTxDialog = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const txDeleting = ref(false)
const deleteTxTarget = ref<TransactionRow | null>(null)

const customers = ref<Customer[]>([])
const suppliers = ref<Supplier[]>([])
const invoices = ref<Invoice[]>([])
const paymentRequests = ref<PR[]>([])
const allBoxes = ref<{ id: number; companyPresetName: string; currency: string }[]>([])

const txForm = ref({
  id: null as number | null,
  type: 'Deposit' as 'Deposit' | 'Withdraw' | 'Transfer',
  amount: 0,
  currency: '' as string,           // set on open to box currency
  exchangeRate: null as number | null,
  fromType: 'MotherWallet',
  fromCustomerId: null as number | null,
  toType: 'MotherWallet',
  toSupplierId: null as number | null,
  invoiceId: null as number | null,
  paymentRequestId: null as number | null,
  // Wallet-to-wallet
  toBoxId: null as number | null,
  depositAmount: 0,
  transferExchangeRate: null as number | null,
  notes: '',
  createdAt: '',
})

const filteredInvoices = computed(() =>
  txForm.value.fromCustomerId
    ? invoices.value.filter(i => i.customerId === txForm.value.fromCustomerId)
    : []
)

const filteredPaymentRequests = computed(() =>
  txForm.value.toSupplierId
    ? paymentRequests.value.filter(pr => pr.supplierId === txForm.value.toSupplierId)
    : []
)

const currencies = ['USD', 'EUR', 'CNY', 'GBP', 'AED', 'RUB']

function currencySymbol(c: string) {
  return ({ USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' } as Record<string, string>)[c] ?? c
}

const headers = [
  { title: 'Deposit', key: 'deposit', sortable: false, width: '130px' },
  { title: 'Withdraw', key: 'withdraw', sortable: false, width: '130px' },
  { title: 'From', key: 'fromName', sortable: false },
  { title: 'To', key: 'toName', sortable: false },
  { title: 'PI#', key: 'piNumber', sortable: false, width: '100px' },
  { title: 'PR#', key: 'prNumber', sortable: false, width: '100px' },
  { title: 'Rate', key: 'exchangeRate', sortable: false, width: '90px' },
  { title: 'Notes', key: 'notes', sortable: false },
  { title: 'Source', key: 'isAuto', sortable: false, width: '90px' },
  { title: 'Date', key: 'createdAt', sortable: true, width: '100px' },
  { title: 'Balance', key: 'balance', sortable: false, width: '120px' },
  { title: '', key: 'actions', sortable: false, width: '70px' },
]

async function loadDetail() {
  loading.value = true
  try {
    detail.value = await api.get<BoxDetail>(`/payment-boxes/${id.value}`)
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

async function loadLookups() {
  const [c, s, inv, pr, boxes] = await Promise.allSettled([
    api.get<{ items: Customer[] }>('/customers?pageSize=500'),
    api.get<{ items: Supplier[] }>('/suppliers?pageSize=500'),
    api.get<{ items: Invoice[] }>('/invoices?pageSize=500'),
    api.get<PR[]>('/paymentrequests'),
    api.get<any[]>('/payment-boxes'),
  ])
  if (c.status === 'fulfilled') customers.value = (c.value as any).items ?? c.value
  if (s.status === 'fulfilled') suppliers.value = (s.value as any).items ?? s.value
  if (inv.status === 'fulfilled') invoices.value = (inv.value as any).items ?? inv.value
  if (pr.status === 'fulfilled') paymentRequests.value = pr.value
  if (boxes.status === 'fulfilled') allBoxes.value = (boxes.value as any[]).filter(b => b.id !== id.value)
}

function openAddTx() {
  resetForm()
  txDialog.value = true
}

function openEditTx(tx: TransactionRow) {
  txForm.value = {
    id: tx.id,
    type: tx.type as any,
    amount: tx.deposit || tx.withdraw || 0,
    currency: tx.txCurrency || detail.value?.currency || 'USD',
    exchangeRate: tx.exchangeRate,
    fromType: tx.fromType || 'MotherWallet',
    fromCustomerId: tx.fromCustomerId,
    toType: tx.toType || 'MotherWallet',
    toSupplierId: tx.toSupplierId,
    invoiceId: tx.piId,
    paymentRequestId: tx.prId,
    toBoxId: null,
    depositAmount: 0,
    transferExchangeRate: null,
    notes: tx.notes || '',
    createdAt: tx.createdAt ? new Date(tx.createdAt).toISOString().slice(0, 16) : '',
  }
  txDialog.value = true
}

function buildSubmitFn(): (() => Promise<void>) | null {
  const form = txForm.value
  if (form.type === 'Transfer' && !form.id) {
    if (!form.toBoxId || !form.amount || form.amount <= 0) return null
    const payload = {
      fromBoxId: id.value,
      toBoxId: form.toBoxId,
      withdrawAmount: form.amount,
      depositAmount: form.depositAmount > 0 ? form.depositAmount : form.amount,
      exchangeRate: form.transferExchangeRate || null,
      notes: form.notes || null,
    }
    return () => api.post('/wallet-transfers', payload)
  } else {
    if (!form.amount || form.amount <= 0) return null
    const boxCurrency = detail.value?.currency ?? 'USD'
    const isSameCurrency = !form.currency || form.currency === boxCurrency
    const body = {
      type: form.type,
      amount: form.amount,
      fromType: form.type === 'Deposit' ? form.fromType : 'MotherWallet',
      fromCustomerId: form.type === 'Deposit' && form.fromType === 'Customer' ? form.fromCustomerId : null,
      toType: form.type === 'Withdraw' ? form.toType : 'MotherWallet',
      toSupplierId: form.type === 'Withdraw' && form.toType === 'Supplier' ? form.toSupplierId : null,
      invoiceId: form.type === 'Deposit' ? form.invoiceId : null,
      paymentRequestId: form.type === 'Withdraw' ? form.paymentRequestId : null,
      notes: form.notes || null,
      currency: isSameCurrency ? null : form.currency,
      exchangeRate: isSameCurrency ? null : form.exchangeRate,
      toPaymentBoxId: null,
      createdAt: form.id ? new Date(form.createdAt).toISOString() : new Date().toISOString(),
    }
    if (form.id) {
      return () => api.patch(`/payment-boxes/${id.value}/transactions/${form.id}`, body)
    } else {
      return () => api.post(`/payment-boxes/${id.value}/transactions`, body)
    }
  }
}

async function saveTx() {
  const submitFn = buildSubmitFn()
  if (!submitFn) return

  // Transfers go to pending approval — skip negative balance check and immediate deduction
  if (txForm.value.type === 'Transfer') {
    await executeSubmit(submitFn)
    return
  }

  // Check if withdraw would make balance negative
  const form = txForm.value
  if (form.type === 'Withdraw' && detail.value) {
    const deductAmount = form.amount * (form.exchangeRate ?? 1)
    const newBalance = detail.value.balance - deductAmount
    if (newBalance < 0) {
      projectedBalance.value = newBalance
      pendingSubmit = submitFn
      negativeWarnDialog.value = true
      return
    }
  }

  await executeSubmit(submitFn)
}

async function proceedDespiteNegative() {
  negativeWarnDialog.value = false
  if (pendingSubmit) {
    await executeSubmit(pendingSubmit)
    pendingSubmit = null
  }
}

async function executeSubmit(submitFn: () => Promise<void>) {
  txSaving.value = true
  const isTransfer = txForm.value.type === 'Transfer'
  try {
    await submitFn()
    txDialog.value = false
    resetForm()
    if (isTransfer) {
      snackbarText.value = 'Transfer request submitted — pending acceptance in Payment Withdraw'
      snackbarColor.value = 'deep-purple'
      snackbar.value = true
    } else {
      await loadDetail()
    }
  } catch (e) {
    console.error(e)
    snackbarText.value = 'Failed to save transaction'
    snackbarColor.value = 'error'
    snackbar.value = true
  } finally {
    txSaving.value = false
  }
}

function resetForm() {
  txForm.value = {
    id: null,
    type: 'Deposit',
    amount: 0,
    currency: detail.value?.currency ?? 'USD',
    exchangeRate: null,
    fromType: 'MotherWallet',
    fromCustomerId: null,
    toType: 'MotherWallet',
    toSupplierId: null,
    invoiceId: null,
    paymentRequestId: null,
    toBoxId: null,
    depositAmount: 0,
    transferExchangeRate: null,
    notes: '',
    createdAt: '',
  }
}

const toBoxCurrency = computed(() =>
  allBoxes.value.find(b => b.id === txForm.value.toBoxId)?.currency ?? ''
)

const showExchangeRate = computed(() =>
  txForm.value.currency && txForm.value.currency !== (detail.value?.currency ?? 'USD')
)

const showTransferRate = computed(() =>
  txForm.value.toBoxId &&
  toBoxCurrency.value &&
  toBoxCurrency.value !== (detail.value?.currency ?? 'USD')
)

// Auto-calculate deposit amount when exchange rate or withdraw amount changes
watch([() => txForm.value.transferExchangeRate, () => txForm.value.amount], ([rate, amount]) => {
  if (txForm.value.type === 'Transfer' && rate && amount > 0) {
    txForm.value.depositAmount = Math.round(amount * rate * 100) / 100
  }
})

function confirmDeleteTx(tx: TransactionRow) {
  deleteTxTarget.value = tx
  deleteTxDialog.value = true
}

async function deleteTx() {
  if (!deleteTxTarget.value) return
  txDeleting.value = true
  try {
    await api.del(`/payment-boxes/${id.value}/transactions/${deleteTxTarget.value.id}`)
    await loadDetail()
    deleteTxDialog.value = false
  } catch (e) {
    console.error(e)
  } finally {
    txDeleting.value = false
  }
}

function doExport() {
  const { type, fromDate, toDate } = exportForm.value
  const from = fromDate ? new Date(fromDate) : null
  const to = toDate ? new Date(toDate + 'T23:59:59') : null
  const walletName = detail.value?.companyPresetName ?? 'wallet'

  const rows = (detail.value?.transactions ?? []).filter(t => {
    if (type === 'Deposit' && t.deposit == null) return false
    if (type === 'Withdraw' && t.withdraw == null) return false
    const d = new Date(t.createdAt)
    if (from && d < from) return false
    if (to && d > to) return false
    return true
  }).map(t => ({
    Deposit: t.deposit ?? '',
    Withdraw: t.withdraw ?? '',
    From: t.fromName ?? '',
    To: t.toName ?? '',
    'PI#': t.piNumber ?? '',
    'PR#': t.prNumber ?? '',
    Currency: t.txCurrency ?? detail.value?.currency ?? '',
    'Exchange Rate': t.exchangeRate ?? '',
    Notes: t.notes ?? '',
    Source: t.isAuto ? 'Auto' : 'Manual',
    Date: new Date(t.createdAt).toLocaleDateString(),
    Balance: t.balance,
  }))

  downloadExcel(rows, `${walletName}-transactions`)
  exportDialog.value = false
}

watch(txDialog, (open) => {
  if (open && authStore.isSuperAdmin) {
    txForm.value.currency = detail.value?.currency ?? 'USD'
    if (customers.value.length === 0) loadLookups()
  }
})

onMounted(async () => {
  await loadDetail()
  const editId = Number(route.query.edit)
  if (editId && detail.value) {
    const tx = detail.value.transactions.find(t => t.id === editId)
    if (tx) {
      await loadLookups()
      openEditTx(tx)
    }
  }
})
</script>
