<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <div>
        <div class="text-subtitle-1 font-weight-bold">Wallet Withdraws</div>
        <div class="text-body-2 text-medium-emphasis">The warehouse pays them and uploads the POP; the local AI reads it and you accept.</div>
      </div>
      <v-spacer />
      <v-btn v-if="syncEnabled" variant="tonal" color="primary" prepend-icon="mdi-cellphone-arrow-down" :loading="syncing" @click="runSync">Sync warehouse</v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
    </div>

    <v-alert v-if="syncing" type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-sync">{{ syncText }}</v-alert>
    <v-alert v-else-if="syncResult" :type="syncResult.skipped.length ? 'warning' : 'success'" variant="tonal" density="compact"
      class="mb-4" closable @click:close="syncResult = null">
      Warehouse: {{ syncResult.withdrawsSent }} withdraw{{ syncResult.withdrawsSent === 1 ? '' : 's' }} waiting there ·
      {{ syncResult.withdraws }} POP{{ syncResult.withdraws === 1 ? '' : 's' }} came back to check
      <ul v-if="syncResult.skipped.length" class="mt-1 ml-4 text-body-2">
        <li v-for="(s, i) in syncResult.skipped" :key="i">{{ s }}</li>
      </ul>
    </v-alert>

    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      Open a wallet → Add Transaction → Withdraw, and turn on <strong>Pay through the warehouse</strong>. Nothing leaves the wallet
      until you accept the POP here: the withdraw (amount × rate) plus Bank Fee rows for the wallet's handling fee, any fee the POP
      shows, and our part of the wire fee.
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      density="comfortable"
      :items-per-page="25"
      hover
      class="cursor-pointer"
      @click:row="(_: unknown, row: { item: WithdrawRow }) => open(row.item)"
    >
      <template #item.walletName="{ item }">
        <span class="font-weight-medium">{{ item.walletName }}</span>
        <span class="text-caption text-medium-emphasis ml-1">{{ item.walletCurrency }}</span>
      </template>
      <template #item.amount="{ item }">{{ fmt(item.amount) }} {{ item.currency }}</template>
      <template #item.toName="{ item }">
        {{ item.toName }}<span v-if="item.prNumber" class="text-caption text-medium-emphasis ml-1">PR-{{ item.prNumber }}</span>
      </template>
      <template #item.total="{ item }">
        <span v-if="item.status === 'Completed'" class="font-weight-medium">{{ fmt(total(item)) }} {{ item.walletCurrency }}</span>
        <span v-else-if="item.proposal" class="text-medium-emphasis">≈ {{ fmt(item.proposal.total) }} {{ item.walletCurrency }}</span>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item)" :prepend-icon="statusIcon(item)">{{ statusLabel(item) }}</v-chip>
      </template>
      <template #item.createdAt="{ item }">{{ new Date(item.createdAt).toLocaleDateString() }}</template>
      <template #no-data>No withdraws through the warehouse yet.</template>
    </v-data-table>

    <v-dialog v-model="showDetail" max-width="680" scrollable>
      <v-card v-if="selected" class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-arrow-up-circle" class="mr-2" color="error" />
          Withdraw #{{ selected.id }}
          <v-spacer />
          <v-chip size="small" :color="statusColor(selected)">{{ statusLabel(selected) }}</v-chip>
        </v-card-title>
        <v-divider />
        <v-card-text style="max-height: 78vh; overflow-y: auto;">
          <v-row dense class="mb-3">
            <v-col cols="6"><div class="text-caption text-medium-emphasis">From wallet</div><div>{{ selected.walletName }} ({{ selected.walletCurrency }})</div></v-col>
            <v-col cols="6"><div class="text-caption text-medium-emphasis">To</div><div>{{ selected.toName }}<span v-if="selected.prNumber"> · PR-{{ selected.prNumber }}</span></div></v-col>
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Amount set</div><div>{{ fmt(selected.amount) }} {{ selected.currency }}</div></v-col>
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Wire fee paid by</div><div>{{ payerLabel(selected.wireFeePaidBy) }}</div></v-col>
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Created by</div><div>{{ selected.createdByName }} · {{ new Date(selected.createdAt).toLocaleString() }}</div></v-col>
            <v-col v-if="selected.exchangeRate" cols="6">
              <div class="text-caption text-medium-emphasis">Rate</div>
              <div>{{ selected.exchangeRate }} {{ selected.walletCurrency }}/{{ selected.currency }}</div>
              <div v-if="selected.bocRate" class="text-caption text-medium-emphasis">BOC {{ selected.bocRate }} + 0.2 · {{ selected.rateAt }}</div>
            </v-col>
            <v-col v-if="selected.base" cols="6"><div class="text-caption text-medium-emphasis">Bases</div><div>{{ selected.base }}</div></v-col>
            <v-col v-if="selected.notes" cols="12"><div class="text-caption text-medium-emphasis">Notes</div><div>{{ selected.notes }}</div></v-col>
          </v-row>

          <div v-if="selected.popFileName" class="d-flex align-center gap-2 pa-2 rounded file-row mb-3">
            <v-icon icon="mdi-file-check" color="success" size="20" />
            <span class="text-body-2 flex-grow-1">POP · {{ selected.popFileName }}</span>
            <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="previewPop(selected)" />
          </div>

          <!-- ── Review: the AI's reading → proposal → accept ── -->
          <template v-if="selected.status === 'Review'">
            <PopReadingCard kind="Withdraw" :ref-id="selected.id" :reading="selected.reading" :notes="selected.proposal?.notes" :check="selected.proposal?.check" class="mb-3" @queued="load" />
            <div class="text-subtitle-2 mb-2">Check and accept</div>
            <v-row dense>
              <v-col cols="12" sm="6">
                <v-text-field v-model.number="accept.amount" :label="`Amount sent (${selected.currency})`" type="number" step="0.01" variant="outlined" density="compact" />
              </v-col>
              <v-col v-if="needsRate(selected)" cols="12" sm="6">
                <v-text-field v-model.number="accept.rate" :label="`Rate: ${selected.walletCurrency} per 1 ${selected.currency}`" type="number" step="0.0001" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="accept.handlingFee" :label="`Handling fee (${selected.walletCurrency})`" type="number" step="0.01" variant="outlined" density="compact"
                  hint="The wallet's own" persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="accept.bankFee" :label="`Bank fee on POP (${selected.walletCurrency})`" type="number" step="0.01" variant="outlined" density="compact"
                  hint="Extra the POP shows" persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="accept.wireFee" :label="`Our wire fee (${selected.walletCurrency})`" type="number" step="0.01" variant="outlined" density="compact"
                  :hint="payerLabel(selected.wireFeePaidBy)" persistent-hint />
              </v-col>
            </v-row>
            <div class="pa-3 rounded calc mt-2">
              <div class="d-flex justify-space-between ga-3">
                <span class="text-medium-emphasis">{{ needsRate(selected) ? `${fmt(accept.amount)} ${selected.currency} × ${accept.rate || '?'}` : 'Amount' }}</span>
                <span>{{ fmt(acceptWalletAmount) }} {{ selected.walletCurrency }}</span>
              </div>
              <div class="d-flex justify-space-between ga-3 mt-1"><span class="text-medium-emphasis">+ fees</span><span>{{ fmt(acceptFees) }} {{ selected.walletCurrency }}</span></div>
              <v-divider class="my-2" />
              <div class="d-flex justify-space-between ga-3 font-weight-bold"><span>Comes off the wallet</span><span class="text-error">{{ fmt(acceptWalletAmount + acceptFees) }} {{ selected.walletCurrency }}</span></div>
            </div>
          </template>

          <div v-else class="pa-3 rounded calc">
            <div class="d-flex justify-space-between ga-3">
              <span class="text-medium-emphasis">Amount{{ rateUsed(selected) ? ` · ${fmt(selected.amount)} ${selected.currency} × ${rateUsed(selected)}` : '' }}</span>
              <span>{{ fmt(selected.walletAmount ?? selected.amount * (rateUsed(selected) ?? 1)) }} {{ selected.walletCurrency }}</span>
            </div>
            <template v-if="selected.status === 'Completed'">
              <div class="d-flex justify-space-between ga-3 mt-1"><span class="text-medium-emphasis">Handling fee</span><span>{{ fmt(selected.handlingFee) }} {{ selected.walletCurrency }}</span></div>
              <div v-if="selected.bankFee" class="d-flex justify-space-between ga-3 mt-1"><span class="text-medium-emphasis">Bank fee on the POP</span><span>{{ fmt(selected.bankFee) }} {{ selected.walletCurrency }}</span></div>
              <div class="d-flex justify-space-between ga-3 mt-1"><span class="text-medium-emphasis">Wire fee</span><span>{{ fmt(selected.wireFee) }} {{ selected.walletCurrency }}</span></div>
              <v-divider class="my-2" />
              <div class="d-flex justify-space-between ga-3 font-weight-bold"><span>Came off the wallet</span><span>{{ fmt(total(selected)) }} {{ selected.walletCurrency }}</span></div>
              <div class="text-caption text-medium-emphasis mt-1">
                <span v-if="selected.warehouseBy">Paid by {{ selected.warehouseBy }} · </span>{{ selected.completedAt ? new Date(selected.completedAt).toLocaleString() : '' }}
              </div>
            </template>
            <div v-else class="text-caption text-medium-emphasis mt-1">
              Waiting for the warehouse to pay it and upload the POP (the rate of that moment is saved with it).
            </div>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-btn v-if="selected.status === 'Pending'" color="error" variant="text" prepend-icon="mdi-delete-outline" :loading="deleting" @click="remove(selected)">Delete</v-btn>
          <v-spacer />
          <v-btn variant="text" @click="showDetail = false">Close</v-btn>
          <v-btn v-if="selected.status === 'Review'" color="success" variant="flat" prepend-icon="mdi-check" :loading="accepting" :disabled="!acceptValid" @click="acceptWithdraw">
            Accept &amp; record
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3500">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { WarehousePaymentSyncSummary } from '~/composables/useWarehouseSync'
import type { PopCheck, PopReading } from '~/components/PopReadingCard.vue'

type WithdrawProposal = {
  amount: number; rate: number | null; walletAmount: number; handlingFee: number; bankFee: number; wireFee: number
  wireFeePaidBy: string; notes: string[]; total: number; check?: PopCheck | null
}
type WithdrawRow = {
  id: number
  paymentBoxId: number; walletName: string; walletCurrency: string
  amount: number; currency: string; officeRate: number | null
  toType: string; toName: string | null; paymentRequestId: number | null; prNumber: number | null
  base: string | null; notes: string | null; status: 'Pending' | 'Review' | 'Completed'
  createdByName: string; createdAt: string
  popFileName: string | null; exchangeRate: number | null; handlingFee: number | null; wireFee: number | null
  walletAmount: number | null; warehouseBy: string | null; completedAt: string | null
  bankFee: number | null; wireFeePaidBy: string | null; bocRate: number | null; rateAt: string | null; popAt: string | null
  reading: PopReading | null; proposal: WithdrawProposal | null
}

const emit = defineEmits<{ completed: [] }>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const warehouseSync = useWarehouseSync()
const loading = ref(false)
const rows = ref<WithdrawRow[]>([])
const showDetail = ref(false)
const selected = ref<WithdrawRow | null>(null)
const deleting = ref(false)
const accepting = ref(false)
const syncEnabled = ref(false)
const syncing = ref(false)
const syncText = ref('')
const syncResult = ref<WarehousePaymentSyncSummary | null>(null)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const accept = reactive({ amount: 0, rate: null as number | null, handlingFee: 0, bankFee: 0, wireFee: 0 })
const acceptTouched = ref(false)
watch(accept, () => { acceptTouched.value = true }, { deep: true })

const headers = [
  { title: 'Wallet', key: 'walletName' },
  { title: 'Amount', key: 'amount' },
  { title: 'To', key: 'toName' },
  { title: 'Off the wallet', key: 'total', sortable: false },
  { title: 'Created', key: 'createdAt' },
  { title: 'Status', key: 'status' },
]

const cents = (v: number) => Math.round(v * 100) / 100
const fee = (v: number | string | null | undefined) => Number(v) || 0
const needsRate = (w: WithdrawRow) => w.currency.toUpperCase() !== w.walletCurrency.toUpperCase()
const acceptWalletAmount = computed(() => selected.value
  ? cents((Number(accept.amount) || 0) * (needsRate(selected.value) ? Number(accept.rate) || 0 : 1))
  : 0)
const acceptFees = computed(() => cents(fee(accept.handlingFee) + fee(accept.bankFee) + fee(accept.wireFee)))
const acceptValid = computed(() => !!selected.value && accept.amount > 0
  && (!needsRate(selected.value) || Number(accept.rate) > 0)
  && [accept.handlingFee, accept.bankFee, accept.wireFee].every(v => fee(v) >= 0))

function showMessage(message: string, color = 'success') {
  snackbarText.value = message
  snackbarColor.value = color
  snackbar.value = true
}
function fmt(v: number | null | undefined) {
  return Number(v ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function payerLabel(p?: string | null) {
  return p === 'Ours' ? 'All ours' : p === 'Split' ? '50 / 50' : p === 'Theirs' ? 'All theirs (receiver)' : '—'
}
function rateUsed(w: WithdrawRow) { return w.exchangeRate ?? w.officeRate }
/** Amount × rate + handling + bank + wire fee, wallet currency. */
function total(w: WithdrawRow) { return cents((w.walletAmount ?? 0) + (w.handlingFee ?? 0) + (w.bankFee ?? 0) + (w.wireFee ?? 0)) }
function statusColor(w: WithdrawRow) { return w.status === 'Completed' ? 'success' : w.status === 'Review' ? 'warning' : 'deep-purple' }
function statusIcon(w: WithdrawRow) { return w.status === 'Completed' ? 'mdi-check-circle' : w.status === 'Review' ? 'mdi-file-search-outline' : 'mdi-clock-outline' }
function statusLabel(w: WithdrawRow) {
  if (w.status === 'Completed') return 'Paid'
  if (w.status === 'Review') return w.reading?.status === 'Waiting' ? 'POP in — AI reading' : 'POP in — check & accept'
  return 'Waiting for warehouse'
}

function fillAccept(w: WithdrawRow) {
  const p = w.proposal
  Object.assign(accept, {
    amount: p?.amount ?? w.amount,
    rate: p?.rate ?? w.exchangeRate ?? w.officeRate,
    handlingFee: p?.handlingFee ?? 0,
    bankFee: p?.bankFee ?? 0,
    wireFee: p?.wireFee ?? 0,
  })
  nextTick(() => { acceptTouched.value = false })
}

async function load() {
  loading.value = true
  try {
    rows.value = await api.get<WithdrawRow[]>('/wallet-withdraws')
    if (selected.value) {
      const fresh = rows.value.find(w => w.id === selected.value!.id)
      if (fresh) {
        const readingChanged = fresh.reading?.status !== selected.value.reading?.status || fresh.reading?.readAt !== selected.value.reading?.readAt
        selected.value = fresh
        if (readingChanged && !acceptTouched.value) fillAccept(fresh)
      }
    }
  }
  catch { showMessage('Failed to load withdraws', 'error') }
  finally { loading.value = false }
}
async function runSync() {
  syncing.value = true
  syncResult.value = null
  try {
    syncResult.value = await warehouseSync.syncPayments(text => { syncText.value = text })
    await load()
  } catch (e: any) {
    showMessage(e?.data?.message || e?.message || 'Warehouse sync failed', 'error')
  } finally { syncing.value = false }
}
function open(row: WithdrawRow) {
  selected.value = row
  fillAccept(row)
  showDetail.value = true
}
async function acceptWithdraw() {
  if (!selected.value || !acceptValid.value) return
  accepting.value = true
  try {
    await api.post(`/wallet-withdraws/${selected.value.id}/accept`, {
      amount: Number(accept.amount),
      exchangeRate: needsRate(selected.value) ? Number(accept.rate) : null,
      handlingFee: cents(fee(accept.handlingFee)),
      bankFee: cents(fee(accept.bankFee)),
      wireFee: cents(fee(accept.wireFee)),
    })
    showDetail.value = false
    showMessage('Withdraw recorded in the wallet.')
    await load()
    emit('completed')
  } catch (e: any) { showMessage(e?.data?.message || 'Accept failed', 'error') }
  finally { accepting.value = false }
}
async function remove(row: WithdrawRow) {
  if (!confirm(`Delete withdraw #${row.id}? The warehouse drops it on the next sync.`)) return
  deleting.value = true
  try {
    await api.del(`/wallet-withdraws/${row.id}`)
    showDetail.value = false
    showMessage('Deleted')
    await load()
  } catch (e: any) { showMessage(e?.data?.message || 'Could not delete it', 'error') }
  finally { deleting.value = false }
}
async function previewPop(row: WithdrawRow) {
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/wallet-withdraws/${row.id}/pop-file`, {
      responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob, row.popFileName ?? 'pop')
  } catch { showMessage('Failed to open POP', 'error') }
}

defineExpose({ load })

// While the AI is still reading a POP, look again every few seconds.
let poll: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  warehouseSync.getPaymentConfig().then(c => { syncEnabled.value = c.enabled }).catch(() => {})
  poll = setInterval(() => {
    if (!loading.value && rows.value.some(w => w.status === 'Review' && w.reading?.status === 'Waiting')) load()
  }, 8000)
  load()
})
onBeforeUnmount(() => clearInterval(poll))
</script>

<style scoped>
.calc {
  background-color: rgba(var(--v-theme-on-surface), 0.04);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  font-variant-numeric: tabular-nums;
}
.file-row {
  background-color: rgba(var(--v-theme-on-surface), 0.06);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}
</style>
