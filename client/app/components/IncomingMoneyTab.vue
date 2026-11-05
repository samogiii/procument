<template>
  <div>
    <div class="d-flex align-center mb-3">
      <p class="text-body-2 text-medium-emphasis mb-0">
        Customer POPs on their way to a wallet. The warehouse uploads the bank POP (the rate is saved with it), the local AI reads
        what arrived, then you check and accept the money here. Fees on a customer's POP are theirs, never ours.
      </p>
      <v-spacer />
      <v-btn v-if="syncEnabled" variant="tonal" size="small" color="primary" prepend-icon="mdi-cellphone-arrow-down"
        class="ml-3 flex-shrink-0" :loading="syncing" @click="runSync">Sync warehouse</v-btn>
    </div>

    <v-alert v-if="syncing" type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-sync">{{ syncText }}</v-alert>
    <v-alert v-else-if="syncResult" :type="syncResult.skipped.length ? 'warning' : 'success'" variant="tonal" density="compact"
      class="mb-4" closable @click:close="syncResult = null">
      Warehouse: {{ syncResult.depositsSent }} deposit{{ syncResult.depositsSent === 1 ? '' : 's' }} sent ·
      {{ syncResult.deposits }} confirmed and waiting for you
      <ul v-if="syncResult.skipped.length" class="mt-1 ml-4 text-body-2">
        <li v-for="(s, i) in syncResult.skipped" :key="i">{{ s }}</li>
      </ul>
    </v-alert>

    <v-card class="glass-card">
      <v-data-table
        :headers="headers"
        :items="rows"
        :loading="loading"
        density="comfortable"
        :items-per-page="25"
        hover
        class="cursor-pointer"
        @click:row="(_: unknown, row: { item: IncomingRow }) => open(row.item)"
      >
        <template #item.invoiceNumber="{ item }">
          <NuxtLink :to="`/invoices/${item.invoiceId}`" class="text-primary text-decoration-none font-weight-bold" @click.stop>{{ item.invoiceNumber }}</NuxtLink>
          <div class="text-caption text-medium-emphasis">{{ item.customer || '—' }}</div>
        </template>
        <template #item.amount="{ item }">{{ currencySymbol(item.currency) }}{{ formatPrice(item.amount) }}</template>
        <template #item.officeAmount="{ item }">{{ currencySymbol(item.walletCurrency) }}{{ formatPrice(item.officeAmount) }}</template>
        <template #item.bankWalletAmount="{ item }">
          <template v-if="(item.proposal?.walletAmount ?? item.bankWalletAmount) != null">
            {{ currencySymbol(item.walletCurrency) }}{{ formatPrice(item.proposal?.walletAmount ?? item.bankWalletAmount) }}
            <v-icon v-if="item.reading?.status === 'Done'" icon="mdi-robot-outline" size="14" class="ml-1" title="Read by the local AI" />
          </template>
          <span v-else>—</span>
        </template>
        <template #item.difference="{ item }">
          <span v-if="item.difference == null">—</span>
          <span v-else :class="item.difference < 0 ? 'text-success' : ''">{{ differenceText(item) }}</span>
        </template>
        <template #item.walletName="{ item }">{{ item.walletName }} · {{ item.walletCurrency }}</template>
        <template #item.stage="{ item }">
          <v-chip size="small" :color="stageColor(item)" variant="tonal">{{ stageLabel(item) }}</v-chip>
        </template>
        <template #no-data>No incoming money yet.</template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="showDialog" max-width="620" scrollable>
      <v-card v-if="selected">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-cash-plus" class="mr-2" color="success" />
          {{ selected.invoiceNumber }}
          <span v-if="selected.customer" class="text-body-2 text-medium-emphasis ml-2">{{ selected.customer }}</span>
          <v-spacer />
          <v-chip size="small" :color="stageColor(selected)" variant="tonal">{{ stageLabel(selected) }}</v-chip>
        </v-card-title>
        <v-divider />
        <v-card-text style="max-height: 75vh; overflow-y: auto;">
          <v-alert v-if="selected.rejectNote" type="warning" variant="tonal" density="compact" class="mb-3">
            Sent back: {{ selected.rejectNote }}
          </v-alert>

          <!-- ── Waiting for acceptance: the AI's reading, then the office's own numbers ── -->
          <template v-if="selected.status === 'Pending' && selected.bankAt">
            <PopReadingCard kind="Deposit" :ref-id="selected.id" :reading="selected.reading" :notes="selected.proposal?.notes" :check="selected.proposal?.check" class="mb-3" @queued="load" />
            <v-row dense class="mb-1">
              <v-col cols="12" sm="6">
                <v-text-field v-model.number="confirm.amount" :label="`What arrived (${selected.currency})`" type="number" step="0.01" variant="outlined" density="compact"
                  hint="From the bank POP — the customer's fees are not ours" persistent-hint />
              </v-col>
              <v-col v-if="needsRate(selected)" cols="12" sm="6">
                <v-text-field v-model.number="confirm.rate" :label="`Rate: ${selected.walletCurrency} per 1 ${selected.currency}`" type="number" step="0.0001" variant="outlined" density="compact"
                  :hint="selected.bocRate ? `BOC ${selected.bocRate} + 0.2 · ${selected.rateAt ?? ''}` : ''" persistent-hint />
              </v-col>
            </v-row>
          </template>

          <div class="pa-3 rounded calc mb-4">
            <div class="d-flex justify-space-between ga-3">
              <span class="text-medium-emphasis">Procument: {{ formula(selected.amount, selected.currency, selected.walletRate) }}</span>
              <span>{{ currencySymbol(selected.walletCurrency) }}{{ formatPrice(selected.officeAmount) }}</span>
            </div>
            <div v-if="selected.status === 'Pending' && selected.bankAt" class="d-flex justify-space-between ga-3 mt-1">
              <span class="text-medium-emphasis">Arrived: {{ formula(Number(confirm.amount) || 0, selected.currency, needsRate(selected) ? Number(confirm.rate) || 0 : null) }}</span>
              <span>{{ currencySymbol(selected.walletCurrency) }}{{ formatPrice(confirmWalletAmount) }}</span>
            </div>
            <div v-else class="d-flex justify-space-between ga-3 mt-1">
              <span class="text-medium-emphasis">
                Warehouse:
                <template v-if="selected.bankAmount != null">{{ formula(selected.bankAmount, selected.currency, selected.bankRate) }}</template>
                <template v-else>not confirmed yet</template>
              </span>
              <span>{{ selected.bankWalletAmount != null ? `${currencySymbol(selected.walletCurrency)}${formatPrice(selected.bankWalletAmount)}` : '—' }}</span>
            </div>
            <template v-if="selected.status === 'Pending' && selected.bankAt">
              <v-divider class="my-2" />
              <div class="d-flex justify-space-between ga-3 font-weight-bold">
                <span>{{ confirmDifference < 0 ? 'Received more than expected' : 'Bank fee' }}</span>
                <span :class="confirmDifference < 0 ? 'text-success' : ''">{{ confirmDifference < 0 ? '+' : '' }}{{ currencySymbol(selected.walletCurrency) }}{{ formatPrice(Math.abs(confirmDifference)) }}</span>
              </div>
            </template>
            <template v-else-if="selected.difference != null">
              <v-divider class="my-2" />
              <div class="d-flex justify-space-between ga-3 font-weight-bold">
                <span>{{ selected.difference < 0 ? 'Received more than expected' : 'Bank fee' }}</span>
                <span>{{ differenceText(selected) }}</span>
              </div>
              <div class="d-flex justify-space-between ga-3 mt-1">
                <span class="text-medium-emphasis">{{ selected.status === 'Accepted' ? 'Went into' : 'Goes into' }} {{ selected.walletName }}</span>
                <span>{{ currencySymbol(selected.walletCurrency) }}{{ formatPrice(selected.bankWalletAmount) }} {{ selected.walletCurrency }}</span>
              </div>
            </template>
          </div>

          <div class="text-subtitle-2 mb-2">Proofs of payment</div>
          <div class="d-flex flex-column gap-2 mb-2">
            <div class="d-flex align-center gap-2 pa-2 rounded file-row">
              <v-icon icon="mdi-receipt-text-check" color="primary" size="20" />
              <div class="flex-grow-1 min-width-0">
                <div class="text-body-2 font-weight-medium text-truncate">{{ selected.popFileName }}</div>
                <div class="text-caption text-medium-emphasis">Customer POP<span v-if="selected.popNotes"> · {{ selected.popNotes }}</span></div>
              </div>
              <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="preview('customer', selected.popFileName)" />
            </div>
            <div v-if="selected.bankPopFileName" class="d-flex align-center gap-2 pa-2 rounded file-row">
              <v-icon icon="mdi-bank-check" color="success" size="20" />
              <div class="flex-grow-1 min-width-0">
                <div class="text-body-2 font-weight-medium text-truncate">{{ selected.bankPopFileName }}</div>
                <div class="text-caption text-medium-emphasis">Bank POP · {{ selected.bankBy }} · {{ selected.bankAt ? new Date(selected.bankAt).toLocaleString() : '' }}</div>
              </div>
              <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="preview('bank', selected.bankPopFileName!)" />
            </div>
          </div>

          <div v-if="selected.status === 'Accepted'" class="text-caption text-medium-emphasis mt-3">
            Accepted by {{ selected.reviewedBy }} · {{ selected.reviewedAt ? new Date(selected.reviewedAt).toLocaleString() : '' }}
          </div>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-btn variant="text" @click="showDialog = false">Close</v-btn>
          <v-spacer />
          <template v-if="selected.status === 'Pending' && selected.bankAt">
            <v-btn color="warning" variant="tonal" prepend-icon="mdi-undo" @click="showSendBack = true">Send Back</v-btn>
            <v-btn color="success" variant="flat" prepend-icon="mdi-check-circle" :loading="accepting" :disabled="!confirmValid" @click="accept">Accept Money</v-btn>
          </template>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="showSendBack" max-width="500">
      <v-card class="glass-card">
        <v-card-title class="pa-4">Send Back to the Warehouse</v-card-title>
        <v-card-text class="pa-4">
          <div class="mb-4 text-body-2">Nothing reaches the wallet. After the next sync the phone shows your note and asks for it again.</div>
          <v-textarea v-model="sendBackNote" label="What should they fix?" rows="3" variant="outlined" density="comfortable" counter="500" hide-details="auto" />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showSendBack = false">Cancel</v-btn>
          <v-btn color="warning" variant="tonal" :disabled="!sendBackNote.trim()" :loading="sendingBack" @click="sendBack">Send Back</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000" location="bottom end">{{ snackbarText }}</v-snackbar>

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />
  </div>
</template>

<script setup lang="ts">
import type { WarehousePaymentSyncSummary } from '~/composables/useWarehouseSync'
import type { PopCheck, PopReading } from '~/components/PopReadingCard.vue'

type IncomingRow = {
  id: number
  invoiceId: number
  invoiceNumber: string
  customer: string | null
  popFileName: string
  popNotes: string | null
  amount: number
  currency: string
  walletRate: number | null
  officeAmount: number
  walletName: string
  walletCurrency: string
  bankPopFileName: string | null
  bankAmount: number | null
  bankRate: number | null
  bankWalletAmount: number | null
  bankBy: string | null
  bankAt: string | null
  rejectNote: string | null
  status: 'Pending' | 'Accepted'
  reviewedAt: string | null
  reviewedBy: string | null
  createdAt: string
  stage: string
  difference: number | null
  bocRate: number | null
  rateAt: string | null
  reading: PopReading | null
  proposal: { amount: number; rate: number | null; walletAmount: number; notes: string[]; check?: PopCheck | null } | null
}

const emit = defineEmits<{ pending: [count: number]; accepted: [] }>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const warehouseSync = useWarehouseSync()

const headers = [
  { title: 'Proforma', key: 'invoiceNumber' },
  { title: 'POP Price', key: 'amount' },
  { title: 'Procument', key: 'officeAmount' },
  { title: 'Warehouse', key: 'bankWalletAmount' },
  { title: 'Bank Fee', key: 'difference' },
  { title: 'Wallet', key: 'walletName' },
  { title: 'Status', key: 'stage', sortable: false },
]

const loading = ref(false)
const rows = ref<IncomingRow[]>([])
const showDialog = ref(false)
const selected = ref<IncomingRow | null>(null)
const accepting = ref(false)
const showSendBack = ref(false)
const sendBackNote = ref('')
const sendingBack = ref(false)
const syncEnabled = ref(false)
const syncing = ref(false)
const syncText = ref('')
const syncResult = ref<WarehousePaymentSyncSummary | null>(null)

// What the office confirms arrived (the AI's reading, maybe changed). Follows a new reading while untouched.
const confirm = reactive({ amount: 0, rate: null as number | null })
const confirmTouched = ref(false)
watch(confirm, () => { confirmTouched.value = true }, { deep: true })
const needsRate = (r: IncomingRow) => r.currency.toUpperCase() !== r.walletCurrency.toUpperCase()
const confirmWalletAmount = computed(() => {
  const r = selected.value
  if (!r) return 0
  const rate = needsRate(r) ? Number(confirm.rate) || 0 : 1
  return Math.round((Number(confirm.amount) || 0) * rate * 100) / 100
})
const confirmDifference = computed(() => Math.round(((selected.value?.officeAmount ?? 0) - confirmWalletAmount.value) * 100) / 100)
const confirmValid = computed(() => !!selected.value && Number(confirm.amount) > 0 && (!needsRate(selected.value) || Number(confirm.rate) > 0))
function fillConfirm(r: IncomingRow) {
  confirm.amount = r.proposal?.amount ?? r.bankAmount ?? r.amount
  confirm.rate = r.proposal?.rate ?? r.bankRate ?? r.walletRate
  nextTick(() => { confirmTouched.value = false })
}

// While the AI is still reading a POP, look again every few seconds.
let poll: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  load()
  warehouseSync.getPaymentConfig().then(c => { syncEnabled.value = c.enabled }).catch(() => {})
  poll = setInterval(() => {
    if (!loading.value && rows.value.some(r => r.status === 'Pending' && r.reading?.status === 'Waiting')) load()
  }, 8000)
})
onBeforeUnmount(() => clearInterval(poll))

async function load() {
  loading.value = true
  try {
    rows.value = await api.get<IncomingRow[]>('/incoming-money')
    emit('pending', rows.value.filter(r => r.status === 'Pending' && r.bankAt).length)
    if (selected.value) {
      const fresh = rows.value.find(r => r.id === selected.value!.id)
      if (fresh) {
        const readingChanged = fresh.reading?.status !== selected.value.reading?.status || fresh.reading?.readAt !== selected.value.reading?.readAt
        selected.value = fresh
        if (readingChanged && !confirmTouched.value) fillConfirm(fresh)
      }
    }
  } catch { showSnack('Failed to load incoming money', 'error') }
  finally { loading.value = false }
}

async function runSync() {
  syncing.value = true
  syncResult.value = null
  try {
    syncResult.value = await warehouseSync.syncPayments(text => { syncText.value = text })
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || e?.message || 'Warehouse sync failed', 'error')
  } finally { syncing.value = false }
}

function open(row: IncomingRow) {
  selected.value = row
  fillConfirm(row)
  showDialog.value = true
}

async function preview(kind: 'customer' | 'bank', name: string) {
  if (!selected.value) return
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/incoming-money/${selected.value.id}/files/${kind}`, {
      responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob, name)
  } catch { showSnack('Failed to open file', 'error') }
}

async function accept() {
  if (!selected.value || accepting.value) return
  accepting.value = true
  try {
    const result = await api.post<any>(`/incoming-money/${selected.value.id}/accept`, {
      amount: Math.round(Number(confirm.amount) * 100) / 100,
      exchangeRate: needsRate(selected.value) ? Number(confirm.rate) : null,
    })
    const symbol = currencySymbol(result.walletCurrency)
    showSnack(`${symbol}${formatPrice(result.received)} went into ${result.walletName}` +
      (result.bankFee > 0 ? ` (bank fee ${symbol}${formatPrice(result.bankFee)}).` : '.'), 'success')
    showDialog.value = false
    emit('accepted')
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not accept this deposit', 'error')
  } finally { accepting.value = false }
}

async function sendBack() {
  if (!selected.value || !sendBackNote.value.trim()) return
  sendingBack.value = true
  try {
    await api.post(`/incoming-money/${selected.value.id}/send-back`, { note: sendBackNote.value.trim() })
    showSnack('Sent back. The warehouse sees your note after the next sync.', 'warning')
    showSendBack.value = false
    showDialog.value = false
    sendBackNote.value = ''
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not send it back', 'error')
  } finally { sendingBack.value = false }
}

function formula(amount: number, currency: string, rate: number | null) {
  const price = `${currencySymbol(currency)}${formatPrice(amount)}`
  return rate ? `${price} × ${rate}` : price
}

function differenceText(row: IncomingRow) {
  const value = row.difference ?? 0
  return `${value < 0 ? '+' : ''}${currencySymbol(row.walletCurrency)}${formatPrice(Math.abs(value))}`
}

function stageLabel(row: IncomingRow) {
  if (row.status === 'Accepted') return 'In wallet'
  if (row.bankAt) return 'Ready to accept'
  return row.rejectNote ? 'Sent back to warehouse' : 'Waiting for warehouse'
}

function stageColor(row: IncomingRow) {
  if (row.status === 'Accepted') return 'success'
  return row.bankAt ? 'primary' : 'warning'
}

function currencySymbol(currency: string | null) {
  return ({ USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' } as Record<string, string>)[currency ?? ''] ?? `${currency} `
}

function formatPrice(v: unknown) {
  if (v == null || isNaN(Number(v))) return '0.00'
  return Number(v).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(t: string, c: string) { snackbarText.value = t; snackbarColor.value = c; snackbar.value = true }
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
