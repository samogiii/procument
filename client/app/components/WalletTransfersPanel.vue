<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <div>
        <div class="text-subtitle-1 font-weight-bold">Wallet Transfers</div>
        <div class="text-body-2 text-medium-emphasis">
          {{ syncEnabled ? 'The warehouse sends the money and uploads the POP; the local AI reads it and you accept.' : 'Upload a POP; the local AI reads it and you accept.' }}
        </div>
      </div>
      <v-spacer />
      <v-btn v-if="syncEnabled" variant="tonal" color="primary" prepend-icon="mdi-cellphone-arrow-down" :loading="syncing" @click="runSync">Sync warehouse</v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadTransfers">Refresh</v-btn>
      <v-btn color="deep-purple" variant="flat" prepend-icon="mdi-swap-horizontal" @click="openCreateTransfer">New Wallet Transfer</v-btn>
    </div>

    <v-alert v-if="syncing" type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-sync">{{ syncText }}</v-alert>
    <v-alert v-else-if="syncResult" :type="syncResult.skipped.length ? 'warning' : 'success'" variant="tonal" density="compact"
      class="mb-4" closable @click:close="syncResult = null">
      Warehouse: {{ syncResult.transfersSent }} transfer{{ syncResult.transfersSent === 1 ? '' : 's' }} waiting there ·
      {{ syncResult.transfers }} POP{{ syncResult.transfers === 1 ? '' : 's' }} came back to check
      <ul v-if="syncResult.skipped.length" class="mt-1 ml-4 text-body-2">
        <li v-for="(s, i) in syncResult.skipped" :key="i">{{ s }}</li>
      </ul>
    </v-alert>

    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      Nothing moves until you accept. The handling fee is the source wallet's own; a fee the POP shows (or a bigger amount than
      set) is a bank fee; the wire fee is split by who pays it (source wallet's setting). Transfers from a yuan wallet have no wire fee.
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="transfers"
      :loading="loading"
      density="comfortable"
      :items-per-page="25"
      hover
      class="cursor-pointer"
      @click:row="(_: unknown, row: { item: WalletTransfer }) => openTransfer(row.item)"
    >
      <template #item.fromBoxName="{ item }">
        <span class="font-weight-medium">{{ item.fromBoxName }}</span>
        <v-icon icon="mdi-arrow-right" size="16" class="mx-2" />
        <span class="font-weight-medium">{{ item.toBoxName }}</span>
      </template>
      <template #item.withdrawAmount="{ item }">
        <span class="font-weight-medium">{{ fmt(item.withdrawAmount) }} {{ item.fromCurrency }}</span>
        <span class="text-caption text-medium-emphasis mx-1">→</span>
        <span class="font-weight-medium">{{ fmt(arrives(item)) }} {{ item.toCurrency }}</span>
      </template>
      <template #item.fees="{ item }">
        <span v-if="!sourceFees(item) && !item.wireFee" class="text-medium-emphasis">—</span>
        <span v-else class="text-body-2">
          <span v-if="sourceFees(item)">{{ fmt(sourceFees(item)) }} {{ item.fromCurrency }}</span>
          <span v-if="sourceFees(item) && item.wireFee"> + </span>
          <span v-if="item.wireFee">{{ fmt(item.wireFee) }} {{ item.toCurrency }}</span>
        </span>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" :prepend-icon="statusIcon(item.status)">{{ statusLabel(item) }}</v-chip>
      </template>
      <template #item.createdAt="{ item }">{{ new Date(item.createdAt).toLocaleDateString() }}</template>
      <template #item.actions="{ item }">
        <v-btn size="small" :variant="item.status === 'Review' ? 'flat' : 'tonal'" :color="item.status === 'Review' ? 'warning' : 'deep-purple'"
          :prepend-icon="item.status === 'Review' ? 'mdi-check-decagram' : 'mdi-eye'" @click.stop="openTransfer(item)">
          {{ item.status === 'Review' ? 'Check' : 'View' }}
        </v-btn>
      </template>
    </v-data-table>

    <v-dialog v-model="showDetail" max-width="760" scrollable>
      <v-card v-if="selected" class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-swap-horizontal" class="mr-2" color="deep-purple" />
          Wallet Transfer #{{ selected.id }}
          <v-spacer />
          <v-chip size="small" :color="statusColor(selected.status)" :prepend-icon="statusIcon(selected.status)">{{ statusLabel(selected) }}</v-chip>
        </v-card-title>
        <v-divider />
        <v-card-text style="max-height: 78vh; overflow-y: auto;">
          <v-row dense class="mb-4 mt-2">
            <v-col cols="12" sm="5">
              <v-card variant="tonal" color="deep-purple" class="pa-3 text-center">
                <div class="text-caption text-medium-emphasis mb-1">From Wallet</div>
                <div class="font-weight-bold">{{ selected.fromBoxName }}</div>
                <div class="text-h6 font-weight-bold mt-1">{{ fmt(selected.withdrawAmount) }} <span class="text-body-2">{{ selected.fromCurrency }}</span></div>
                <template v-if="selected.status === 'Completed' && sourceFees(selected)">
                  <div class="text-caption">+ fees {{ fmt(sourceFees(selected)) }}</div>
                  <div class="text-body-2 font-weight-bold">Out: {{ fmt(selected.withdrawAmount + sourceFees(selected)) }} {{ selected.fromCurrency }}</div>
                </template>
              </v-card>
            </v-col>
            <v-col cols="12" sm="2" class="d-flex align-center justify-center">
              <div class="text-center">
                <v-icon icon="mdi-arrow-right-bold" color="deep-purple" size="28" />
                <div v-if="selected.rateText" class="text-caption text-medium-emphasis mt-1">{{ selected.rateText }}</div>
                <div v-if="selected.bocRate" class="text-caption text-medium-emphasis">BOC {{ selected.bocRate }} + 0.2<br>{{ selected.rateAt }}</div>
              </div>
            </v-col>
            <v-col cols="12" sm="5">
              <v-card variant="tonal" color="success" class="pa-3 text-center">
                <div class="text-caption text-medium-emphasis mb-1">To Wallet</div>
                <div class="font-weight-bold">{{ selected.toBoxName }}</div>
                <div class="text-h6 font-weight-bold mt-1">{{ fmt(selected.depositAmount) }} <span class="text-body-2">{{ selected.toCurrency }}</span></div>
                <template v-if="selected.status === 'Completed' && selected.wireFee">
                  <div class="text-caption">− wire fee {{ fmt(selected.wireFee) }}</div>
                  <div class="text-body-2 font-weight-bold">In: {{ fmt(arrives(selected)) }} {{ selected.toCurrency }}</div>
                </template>
              </v-card>
            </v-col>
          </v-row>

          <v-row dense class="mb-4">
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Created By</div><div>{{ selected.createdByName }}</div></v-col>
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Date</div><div>{{ new Date(selected.createdAt).toLocaleString() }}</div></v-col>
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Wire fee paid by</div><div>{{ payerLabel(selected.wireFeePaidBy) }}</div></v-col>
            <v-col v-if="selected.warehouseBy" cols="6"><div class="text-caption text-medium-emphasis">Sent By (warehouse)</div><div>{{ selected.warehouseBy }}</div></v-col>
            <v-col v-if="selected.notes" cols="12"><div class="text-caption text-medium-emphasis">Notes</div><div>{{ selected.notes }}</div></v-col>
          </v-row>

          <v-divider class="mb-4" />
          <div class="text-subtitle-2 mb-2"><v-icon icon="mdi-cash-check" size="18" class="mr-1" color="warning" />Proof of Payment (POP)</div>
          <div v-if="selected.popFileName" class="d-flex align-center gap-2 pa-2 rounded file-row mb-3">
            <v-icon icon="mdi-file-check" color="success" size="20" />
            <span class="text-body-2 flex-grow-1">{{ selected.popFileName }}</span>
            <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadPop(selected)" />
          </div>
          <v-alert v-else type="info" variant="tonal" density="compact" class="mb-3">
            {{ syncEnabled ? 'Waiting for the warehouse. The wallet balances have not changed.' : 'No POP uploaded yet. The wallet balances have not changed.' }}
          </v-alert>

          <!-- ── Review: the AI's reading → proposal → accept ── -->
          <template v-if="selected.status === 'Review'">
            <PopReadingCard kind="Transfer" :ref-id="selected.id" :reading="selected.reading" :notes="selected.proposal?.notes" :check="selected.proposal?.check" class="mb-3" @queued="loadTransfers" />
            <div class="text-subtitle-2 mb-2">Check and accept</div>
            <v-row dense>
              <v-col cols="12" sm="6">
                <v-text-field v-model.number="accept.amount" :label="`Amount sent (${selected.fromCurrency})`" type="number" step="0.01" variant="outlined" density="compact" />
              </v-col>
              <v-col v-if="selected.rateBase" cols="12" sm="6">
                <v-text-field v-model.number="accept.rate" :label="`Rate: 1 ${selected.rateBase} = ? ${selected.rateQuote}`" type="number" step="0.0001" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="accept.handlingFee" :label="`Handling fee (${selected.fromCurrency})`" type="number" step="0.01" variant="outlined" density="compact"
                  hint="Source wallet's own" persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="accept.bankFee" :label="`Bank fee on POP (${selected.fromCurrency})`" type="number" step="0.01" variant="outlined" density="compact"
                  hint="Extra the POP shows" persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="accept.sourceWireFee" :label="`Our wire fee (${selected.fromCurrency})`" type="number" step="0.01" variant="outlined" density="compact"
                  :hint="payerLabel(selected.wireFeePaidBy)" persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="accept.destWireFee" :label="`Their wire fee (${selected.toCurrency})`" type="number" step="0.01" variant="outlined" density="compact"
                  hint="Off what arrives" persistent-hint />
              </v-col>
            </v-row>
            <v-card variant="outlined" class="pa-3 mt-2 calc">
              <div class="d-flex justify-space-between"><span class="text-medium-emphasis">{{ selected.fromBoxName }} loses</span>
                <strong class="text-error">{{ fmt(acceptDebit) }} {{ selected.fromCurrency }}</strong></div>
              <div class="d-flex justify-space-between"><span class="text-medium-emphasis">{{ selected.toBoxName }} gets</span>
                <strong class="text-success">{{ fmt(acceptCredit) }} {{ selected.toCurrency }}</strong></div>
            </v-card>
          </template>

          <div v-if="selected.status === 'Accepted' || selected.status === 'Review'" class="mt-3">
            <v-btn variant="tonal" color="deep-purple" prepend-icon="mdi-upload" :loading="uploading" @click="popInput?.click()">
              {{ selected.status === 'Review' ? 'Replace POP' : syncEnabled ? 'Upload POP here instead' : 'Upload POP' }}
            </v-btn>
            <div class="text-caption text-medium-emphasis mt-1">
              The AI reads it, then you accept here{{ syncEnabled && selected.status === 'Accepted' ? '; the warehouse can no longer send it' : '' }}.
            </div>
            <input ref="popInput" type="file" class="d-none" @change="onPopSelected" />
          </div>
          <v-alert v-if="selected.status === 'Completed'" type="success" variant="tonal" class="mt-3">
            Transfer completed on {{ selected.completedAt ? new Date(selected.completedAt).toLocaleString() : '—' }}. Wallet balances were updated.
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDetail = false">Close</v-btn>
          <v-btn v-if="selected.status === 'Review'" color="success" variant="flat" prepend-icon="mdi-check" :loading="accepting" :disabled="!acceptValid" @click="acceptTransfer">
            Accept &amp; move the money
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="showCreate" max-width="560">
      <v-card class="glass-card">
        <v-card-title class="pa-4"><v-icon icon="mdi-swap-horizontal" class="mr-2" color="deep-purple" />New Wallet Transfer</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-alert type="info" variant="tonal" density="compact" class="mb-4">
            {{ syncEnabled
              ? 'It goes to the warehouse on the next sync. No money moves until their POP comes back and you accept it.'
              : 'No money moves until a POP is uploaded and you accept it.' }}
          </v-alert>
          <v-select v-model="form.fromBoxId" :items="wallets" item-title="label" item-value="id" label="From Wallet *" variant="outlined" class="mb-3" />
          <v-select v-model="form.toBoxId" :items="wallets.filter(wallet => wallet.id !== form.fromBoxId)" item-title="label" item-value="id" label="To Wallet *" variant="outlined" class="mb-3" />
          <v-text-field v-model.number="form.withdrawAmount" label="Amount to send *" type="number" min="0" variant="outlined" class="mb-3" :suffix="fromCurrency" />
          <template v-if="needsRate">
            <v-text-field
              v-model.number="form.exchangeRate"
              :label="`Rate: 1 ${rateBase} = ? ${rateOther}`"
              type="number" min="0" step="0.0001" variant="outlined" class="mb-3"
              :hint="syncEnabled ? 'Optional — the warehouse saves the rate of the moment it uploads the POP.' : 'Leave blank if you will fill in the real amount later'"
              persistent-hint
            />
            <v-text-field v-if="form.exchangeRate" :model-value="depositAmount" label="Arrives (before the wire fee)" variant="outlined" class="mb-3" readonly :suffix="toCurrency" />
          </template>
          <v-select v-model="form.wireFeePaidBy" :items="PAYER_OPTIONS" item-title="title" item-value="value" label="Wire fee paid by" variant="outlined" class="mb-3"
            :hint="fromCurrency.toUpperCase() === 'CNY' ? 'Transfers from a yuan wallet have no wire fee.' : ''" persistent-hint />
          <v-textarea v-model="form.notes" label="Notes (optional)" rows="2" variant="outlined" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4"><v-spacer /><v-btn variant="text" @click="showCreate = false">Cancel</v-btn><v-btn color="deep-purple" variant="flat" :loading="creating" :disabled="!formValid" @click="createTransfer">Create Transfer</v-btn></v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3500">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { WarehousePaymentSyncSummary } from '~/composables/useWarehouseSync'
import type { PopCheck, PopReading } from '~/components/PopReadingCard.vue'

type TransferProposal = {
  amount: number; rate: number | null; received: number; handlingFee: number; bankFee: number
  sourceWireFee: number; destWireFee: number; wireFeePaidBy: string; notes: string[]; debit: number; credit: number; check?: PopCheck | null
}
type WalletTransfer = {
  id: number
  fromBoxId: number; fromBoxName: string; fromCurrency: string
  toBoxId: number; toBoxName: string; toCurrency: string
  withdrawAmount: number; depositAmount: number; exchangeRate?: number; rateText?: string
  handlingFee?: number; wireFee?: number; sourceWireFee?: number; bankFee?: number; wireFeePaidBy?: string
  bocRate?: number; rateAt?: string; popAt?: string; rateBase?: string; rateQuote?: string
  notes?: string; status: 'Accepted' | 'Review' | 'Completed'; popFileName?: string
  createdByName: string; createdAt: string; acceptedAt?: string; completedAt?: string; warehouseBy?: string
  reading?: PopReading | null; proposal?: TransferProposal | null
}
type WalletOption = { id: number; label: string; currency: string }

const PAYER_OPTIONS = [
  { title: "Wallet's setting", value: null },
  { title: 'All ours (source wallet)', value: 'Ours' },
  { title: '50 / 50', value: 'Split' },
  { title: 'All theirs (destination wallet)', value: 'Theirs' },
]

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const warehouseSync = useWarehouseSync()
const loading = ref(false)
const creating = ref(false)
const uploading = ref(false)
const accepting = ref(false)
const allTransfers = ref<WalletTransfer[]>([])
const wallets = ref<WalletOption[]>([])
const showDetail = ref(false)
const selected = ref<WalletTransfer | null>(null)
const popInput = ref<HTMLInputElement | null>(null)
const showCreate = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const syncEnabled = ref(false)
const syncing = ref(false)
const syncText = ref('')
const syncResult = ref<WarehousePaymentSyncSummary | null>(null)
const form = reactive({
  fromBoxId: null as number | null, toBoxId: null as number | null, withdrawAmount: 0, exchangeRate: null as number | null,
  notes: '', wireFeePaidBy: null as string | null,
})
const accept = reactive({ amount: 0, rate: null as number | null, handlingFee: 0, bankFee: 0, sourceWireFee: 0, destWireFee: 0 })

const transfers = computed(() => allTransfers.value.filter(t => ['Accepted', 'Review', 'Completed'].includes(t.status)))
const fromCurrency = computed(() => wallets.value.find(wallet => wallet.id === form.fromBoxId)?.currency ?? '')
const toCurrency = computed(() => wallets.value.find(wallet => wallet.id === form.toBoxId)?.currency ?? '')
const needsRate = computed(() => !!fromCurrency.value && !!toCurrency.value && fromCurrency.value.toUpperCase() !== toCurrency.value.toUpperCase())
// Same rule as WalletTransferPending.RateBase: "1 USD = 6.91 CNY" whichever way the money goes, else 1 source = rate destination.
const rateBase = computed(() => toCurrency.value.toUpperCase() === 'USD' && fromCurrency.value.toUpperCase() !== 'USD' ? toCurrency.value : fromCurrency.value)
const rateOther = computed(() => (rateBase.value === fromCurrency.value ? toCurrency.value : fromCurrency.value))
const depositAmount = computed(() => convert(form.withdrawAmount, fromCurrency.value, toCurrency.value, rateBase.value, needsRate.value ? form.exchangeRate : null))
const formValid = computed(() => !!form.fromBoxId && !!form.toBoxId && form.fromBoxId !== form.toBoxId && form.withdrawAmount > 0)
const headers = [
  { title: 'From → To', key: 'fromBoxName' },
  { title: 'Amount', key: 'withdrawAmount' },
  { title: 'Fees', key: 'fees', sortable: false },
  { title: 'Created By', key: 'createdByName' },
  { title: 'Date', key: 'createdAt' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: 140 },
]

const cents = (v: number) => Math.round(v * 100) / 100
/** Same as WalletTransferPending.Convert: × rate out of the base currency, ÷ rate into it. */
function convert(amount: number, from: string, to: string, base: string, rate: number | null | undefined) {
  if (!from || from.toUpperCase() === to.toUpperCase()) return cents(amount)
  if (!rate || rate <= 0) return 0
  return cents(base.toUpperCase() === from.toUpperCase() ? amount * rate : amount / rate)
}
const acceptReceived = computed(() => selected.value
  ? convert(Number(accept.amount) || 0, selected.value.fromCurrency, selected.value.toCurrency, selected.value.rateBase ?? selected.value.fromCurrency, accept.rate)
  : 0)
const acceptDebit = computed(() => cents((Number(accept.amount) || 0) + fee(accept.handlingFee) + fee(accept.bankFee) + fee(accept.sourceWireFee)))
const acceptCredit = computed(() => cents(acceptReceived.value - fee(accept.destWireFee)))
const acceptValid = computed(() => {
  if (!selected.value || !(accept.amount > 0)) return false
  if (selected.value.rateBase && !(Number(accept.rate) > 0)) return false
  if ([accept.handlingFee, accept.bankFee, accept.sourceWireFee, accept.destWireFee].some(v => fee(v) < 0)) return false
  return acceptCredit.value > 0
})

function fee(v: number | string | null | undefined) { return Number(v) || 0 }
function showMessage(message: string, color = 'success') {
  snackbarText.value = message
  snackbarColor.value = color
  snackbar.value = true
}
function fmt(v: number | null | undefined) {
  return Number(v ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function payerLabel(p?: string | null) {
  return p === 'Ours' ? 'All ours (source wallet)' : p === 'Split' ? '50 / 50' : p === 'Theirs' ? 'All theirs (destination wallet)' : '—'
}
/** Everything the source paid on top of the amount. */
function sourceFees(t: WalletTransfer) { return cents((t.handlingFee ?? 0) + (t.bankFee ?? 0) + (t.sourceWireFee ?? 0)) }
/** What reaches the destination wallet: converted amount − its wire fee. */
function arrives(t: WalletTransfer) { return cents(t.depositAmount - (t.wireFee ?? 0)) }
function statusColor(status: string) { return status === 'Completed' ? 'success' : status === 'Review' ? 'warning' : 'deep-purple' }
function statusIcon(status: string) { return status === 'Completed' ? 'mdi-check-circle' : status === 'Review' ? 'mdi-file-search-outline' : 'mdi-clock-outline' }
function statusLabel(t: WalletTransfer) {
  if (t.status === 'Completed') return t.warehouseBy ? 'Completed (warehouse)' : 'Completed'
  if (t.status === 'Review') return t.reading?.status === 'Waiting' ? 'POP in — AI reading' : 'POP in — check & accept'
  return syncEnabled.value ? 'Waiting for warehouse' : 'Waiting for POP'
}

async function loadTransfers() {
  loading.value = true
  try {
    allTransfers.value = await api.get<WalletTransfer[]>('/wallet-transfers')
    // Keep the open dialog on fresh data; the accept form follows a new reading only while it is untouched.
    if (selected.value) {
      const fresh = allTransfers.value.find(t => t.id === selected.value!.id)
      if (fresh) {
        const readingChanged = fresh.reading?.status !== selected.value.reading?.status || fresh.reading?.readAt !== selected.value.reading?.readAt
        selected.value = fresh
        if (readingChanged && !acceptTouched.value) fillAccept(fresh)
      }
    }
  } catch { showMessage('Failed to load wallet transfers', 'error') }
  finally { loading.value = false }
}
async function loadWallets() {
  try {
    const rows = await api.get<any[]>('/supplier-payments/wallets')
    wallets.value = rows.map(wallet => ({ id: wallet.id, label: `${wallet.name} (${wallet.currency})`, currency: wallet.currency }))
  } catch { showMessage('Failed to load wallets', 'error') }
}
async function runSync() {
  syncing.value = true
  syncResult.value = null
  try {
    syncResult.value = await warehouseSync.syncPayments(text => { syncText.value = text })
    await loadTransfers()
  } catch (e: any) {
    showMessage(e?.data?.message || e?.message || 'Warehouse sync failed', 'error')
  } finally { syncing.value = false }
}

const acceptTouched = ref(false)
watch(accept, () => { acceptTouched.value = true }, { deep: true })
function fillAccept(t: WalletTransfer) {
  const p = t.proposal
  Object.assign(accept, {
    amount: p?.amount ?? t.withdrawAmount,
    rate: p?.rate ?? t.exchangeRate ?? null,
    handlingFee: p?.handlingFee ?? 0,
    bankFee: p?.bankFee ?? 0,
    sourceWireFee: p?.sourceWireFee ?? 0,
    destWireFee: p?.destWireFee ?? 0,
  })
  nextTick(() => { acceptTouched.value = false })
}
function openTransfer(transfer: WalletTransfer) {
  selected.value = transfer
  fillAccept(transfer)
  showDetail.value = true
}
function openCreateTransfer() {
  Object.assign(form, { fromBoxId: null, toBoxId: null, withdrawAmount: 0, exchangeRate: null, notes: '', wireFeePaidBy: null })
  showCreate.value = true
}
async function createTransfer() {
  if (!formValid.value) return
  creating.value = true
  try {
    await api.post('/wallet-transfers', {
      fromBoxId: form.fromBoxId,
      toBoxId: form.toBoxId,
      withdrawAmount: form.withdrawAmount,
      depositAmount: depositAmount.value,
      exchangeRate: needsRate.value ? form.exchangeRate || null : null,
      notes: form.notes || null,
      wireFeePaidBy: form.wireFeePaidBy,
    })
    showCreate.value = false
    showMessage(syncEnabled.value ? 'Transfer created. Sync the warehouse to send it to them.' : 'Transfer created. Upload its POP when the money is sent.')
    await loadTransfers()
  } catch (error: any) { showMessage(error?.data?.message || 'Failed to create wallet transfer', 'error') }
  finally { creating.value = false }
}
async function acceptTransfer() {
  if (!selected.value || !acceptValid.value) return
  accepting.value = true
  try {
    await api.post(`/wallet-transfers/${selected.value.id}/accept`, {
      amount: Number(accept.amount),
      exchangeRate: selected.value.rateBase ? Number(accept.rate) : null,
      handlingFee: cents(fee(accept.handlingFee)),
      bankFee: cents(fee(accept.bankFee)),
      sourceWireFee: cents(fee(accept.sourceWireFee)),
      destWireFee: cents(fee(accept.destWireFee)),
    })
    showDetail.value = false
    showMessage('Transfer accepted — wallet balances updated.')
    await loadTransfers()
  } catch (error: any) { showMessage(error?.data?.message || 'Accept failed', 'error') }
  finally { accepting.value = false }
}
async function onPopSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !selected.value) return
  uploading.value = true
  try {
    const body = new FormData()
    body.append('file', file)
    await $fetch(`${api.baseURL}/wallet-transfers/${selected.value.id}/upload-pop`, {
      method: 'POST', body, headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showMessage('POP uploaded — the local AI is reading it.')
    acceptTouched.value = false
    await loadTransfers()
  } catch (error: any) { showMessage(error?.data?.message || 'POP upload failed', 'error') }
  finally { uploading.value = false; input.value = '' }
}
async function downloadPop(transfer: WalletTransfer) {
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/wallet-transfers/${transfer.id}/pop-file`, {
      method: 'GET', responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob as Blob, transfer.popFileName ?? 'pop')
  } catch { showMessage('Failed to open POP', 'error') }
}

// While the AI is still reading a POP, look again every few seconds.
let poll: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  warehouseSync.getPaymentConfig().then(c => { syncEnabled.value = c.enabled }).catch(() => {})
  poll = setInterval(() => {
    if (!loading.value && allTransfers.value.some(t => t.status === 'Review' && t.reading?.status === 'Waiting')) loadTransfers()
  }, 8000)
  return Promise.all([loadTransfers(), loadWallets()])
})
onBeforeUnmount(() => clearInterval(poll))
</script>

<style scoped>
.file-row {
  background-color: rgba(var(--v-theme-on-surface), 0.06);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}
.calc { font-variant-numeric: tabular-nums; }
</style>
