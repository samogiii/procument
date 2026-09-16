<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <div>
        <div class="text-subtitle-1 font-weight-bold">Wallet Transfers</div>
        <div class="text-body-2 text-medium-emphasis">Upload a POP to execute an accepted transfer.</div>
      </div>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadTransfers">Refresh</v-btn>
      <v-btn color="deep-purple" variant="flat" prepend-icon="mdi-swap-horizontal" @click="openCreateTransfer">New Wallet Transfer</v-btn>
    </div>

    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      Creating a transfer accepts it immediately. Wallet balances change only after its POP is uploaded.
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="transfers"
      :loading="loading"
      density="comfortable"
      :items-per-page="25"
      hover
      class="cursor-pointer"
      @click:row="(_, row) => openTransfer(row.item)"
    >
      <template #item.fromBoxName="{ item }">
        <span class="font-weight-medium">{{ item.fromBoxName }}</span>
        <v-icon icon="mdi-arrow-right" size="16" class="mx-2" />
        <span class="font-weight-medium">{{ item.toBoxName }}</span>
      </template>
      <template #item.withdrawAmount="{ item }">
        <span class="font-weight-medium">{{ item.withdrawAmount }} {{ item.fromCurrency }}</span>
        <span class="text-caption text-medium-emphasis mx-1">→</span>
        <span class="font-weight-medium">{{ item.depositAmount }} {{ item.toCurrency }}</span>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" :prepend-icon="statusIcon(item.status)">{{ item.status }}</v-chip>
      </template>
      <template #item.createdAt="{ item }">{{ new Date(item.createdAt).toLocaleDateString() }}</template>
      <template #item.actions="{ item }">
        <v-btn
          size="small"
          variant="tonal"
          color="deep-purple"
          :prepend-icon="item.status === 'Completed' ? 'mdi-eye' : 'mdi-upload'"
          @click.stop="openTransfer(item)"
        >
          {{ item.status === 'Completed' ? 'View' : 'Upload POP' }}
        </v-btn>
      </template>
    </v-data-table>

    <v-dialog v-model="showDetail" max-width="680" scrollable>
      <v-card v-if="selected" class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-swap-horizontal" class="mr-2" color="deep-purple" />
          Wallet Transfer #{{ selected.id }}
          <v-spacer />
          <v-chip size="small" :color="statusColor(selected.status)" :prepend-icon="statusIcon(selected.status)">{{ selected.status }}</v-chip>
        </v-card-title>
        <v-divider />
        <v-card-text style="max-height: 75vh; overflow-y: auto;">
          <v-row dense class="mb-4 mt-2">
            <v-col cols="12" sm="5">
              <v-card variant="tonal" color="deep-purple" class="pa-3 text-center">
                <div class="text-caption text-medium-emphasis mb-1">From Wallet</div>
                <div class="font-weight-bold">{{ selected.fromBoxName }}</div>
                <div class="text-h6 font-weight-bold mt-1">{{ selected.withdrawAmount }} <span class="text-body-2">{{ selected.fromCurrency }}</span></div>
              </v-card>
            </v-col>
            <v-col cols="12" sm="2" class="d-flex align-center justify-center">
              <div class="text-center">
                <v-icon icon="mdi-arrow-right-bold" color="deep-purple" size="28" />
                <div v-if="selected.exchangeRate" class="text-caption text-medium-emphasis mt-1">× {{ selected.exchangeRate }}</div>
              </div>
            </v-col>
            <v-col cols="12" sm="5">
              <v-card variant="tonal" color="success" class="pa-3 text-center">
                <div class="text-caption text-medium-emphasis mb-1">To Wallet</div>
                <div class="font-weight-bold">{{ selected.toBoxName }}</div>
                <div class="text-h6 font-weight-bold mt-1">{{ selected.depositAmount }} <span class="text-body-2">{{ selected.toCurrency }}</span></div>
              </v-card>
            </v-col>
          </v-row>

          <v-row dense class="mb-4">
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Created By</div><div>{{ selected.createdByName }}</div></v-col>
            <v-col cols="6"><div class="text-caption text-medium-emphasis">Date</div><div>{{ new Date(selected.createdAt).toLocaleString() }}</div></v-col>
            <v-col v-if="selected.notes" cols="12"><div class="text-caption text-medium-emphasis">Notes</div><div>{{ selected.notes }}</div></v-col>
          </v-row>

          <v-divider class="mb-4" />
          <div class="text-subtitle-2 mb-2"><v-icon icon="mdi-cash-check" size="18" class="mr-1" color="warning" />Proof of Payment (POP)</div>
          <div v-if="selected.popFileName" class="d-flex align-center gap-2 pa-2 rounded file-row">
            <v-icon icon="mdi-file-check" color="success" size="20" />
            <span class="text-body-2 flex-grow-1">{{ selected.popFileName }}</span>
            <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadPop(selected)" />
          </div>
          <v-alert v-else type="info" variant="tonal" density="compact">No POP uploaded yet. The wallet balances have not changed.</v-alert>

          <div v-if="selected.status === 'Accepted'" class="mt-3">
            <v-btn variant="flat" color="deep-purple" prepend-icon="mdi-upload" :loading="uploading" @click="popInput?.click()">Upload POP &amp; Execute Transfer</v-btn>
            <div class="text-caption text-medium-emphasis mt-1">The transfer happens only when this POP upload succeeds.</div>
            <input ref="popInput" type="file" class="d-none" @change="onPopSelected" />
          </div>
          <v-alert v-if="selected.status === 'Completed'" type="success" variant="tonal" class="mt-3">
            Transfer completed on {{ selected.completedAt ? new Date(selected.completedAt).toLocaleString() : '—' }}. Wallet balances were updated.
          </v-alert>
        </v-card-text>
        <v-card-actions><v-spacer /><v-btn variant="text" @click="showDetail = false">Close</v-btn></v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="showCreate" max-width="560">
      <v-card class="glass-card">
        <v-card-title class="pa-4"><v-icon icon="mdi-swap-horizontal" class="mr-2" color="deep-purple" />New Wallet Transfer</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-alert type="info" variant="tonal" density="compact" class="mb-4">The transfer will be Accepted immediately, but no money moves until a POP is uploaded.</v-alert>
          <v-select v-model="form.fromBoxId" :items="wallets" item-title="label" item-value="id" label="From Wallet *" variant="outlined" class="mb-3" />
          <v-select v-model="form.toBoxId" :items="wallets.filter(wallet => wallet.id !== form.fromBoxId)" item-title="label" item-value="id" label="To Wallet *" variant="outlined" class="mb-3" />
          <v-text-field v-model.number="form.withdrawAmount" label="Withdraw Amount *" type="number" min="0" variant="outlined" class="mb-3" :suffix="fromCurrency" />
          <v-text-field v-model.number="form.exchangeRate" label="Exchange Rate (optional)" type="number" min="0" step="0.0001" variant="outlined" class="mb-3" hint="Leave blank if both wallets use the same currency" persistent-hint />
          <v-text-field v-if="form.exchangeRate" :model-value="depositAmount" label="Real Amount" variant="outlined" class="mb-3" readonly :suffix="toCurrency" hint="Exchange Rate × Withdraw Amount" persistent-hint />
          <v-textarea v-model="form.notes" label="Notes (optional)" rows="2" variant="outlined" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4"><v-spacer /><v-btn variant="text" @click="showCreate = false">Cancel</v-btn><v-btn color="deep-purple" variant="flat" :loading="creating" :disabled="!formValid" @click="createTransfer">Create Transfer</v-btn></v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3500">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
type WalletTransfer = {
  id: number
  fromBoxId: number; fromBoxName: string; fromCurrency: string
  toBoxId: number; toBoxName: string; toCurrency: string
  withdrawAmount: number; depositAmount: number; exchangeRate?: number
  notes?: string; status: 'Accepted' | 'Completed'; popFileName?: string
  createdByName: string; createdAt: string; acceptedAt?: string; completedAt?: string
}
type WalletOption = { id: number; label: string; currency: string }

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const loading = ref(false)
const creating = ref(false)
const uploading = ref(false)
const allTransfers = ref<WalletTransfer[]>([])
const wallets = ref<WalletOption[]>([])
const showDetail = ref(false)
const selected = ref<WalletTransfer | null>(null)
const popInput = ref<HTMLInputElement | null>(null)
const showCreate = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const form = reactive({ fromBoxId: null as number | null, toBoxId: null as number | null, withdrawAmount: 0, exchangeRate: null as number | null, notes: '' })

const transfers = computed(() => allTransfers.value.filter(transfer => transfer.status === 'Accepted' || transfer.status === 'Completed'))
const fromCurrency = computed(() => wallets.value.find(wallet => wallet.id === form.fromBoxId)?.currency ?? '')
const toCurrency = computed(() => wallets.value.find(wallet => wallet.id === form.toBoxId)?.currency ?? '')
const depositAmount = computed(() => Math.round(form.withdrawAmount * (form.exchangeRate || 1) * 100) / 100)
const formValid = computed(() => !!form.fromBoxId && !!form.toBoxId && form.fromBoxId !== form.toBoxId && form.withdrawAmount > 0)
const headers = [
  { title: 'From → To', key: 'fromBoxName' },
  { title: 'Amount', key: 'withdrawAmount' },
  { title: 'Created By', key: 'createdByName' },
  { title: 'Date', key: 'createdAt' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: 140 },
]

function showMessage(message: string, color = 'success') {
  snackbarText.value = message
  snackbarColor.value = color
  snackbar.value = true
}
function statusColor(status: string) { return status === 'Completed' ? 'success' : 'deep-purple' }
function statusIcon(status: string) { return status === 'Completed' ? 'mdi-check-circle' : 'mdi-clock-outline' }

async function loadTransfers() {
  loading.value = true
  try { allTransfers.value = await api.get<WalletTransfer[]>('/wallet-transfers') }
  catch { showMessage('Failed to load wallet transfers', 'error') }
  finally { loading.value = false }
}
async function loadWallets() {
  try {
    const rows = await api.get<any[]>('/supplier-payments/wallets')
    wallets.value = rows.map(wallet => ({ id: wallet.id, label: `${wallet.name} (${wallet.currency})`, currency: wallet.currency }))
  } catch { showMessage('Failed to load wallets', 'error') }
}
function openTransfer(transfer: WalletTransfer) { selected.value = transfer; showDetail.value = true }
function openCreateTransfer() {
  Object.assign(form, { fromBoxId: null, toBoxId: null, withdrawAmount: 0, exchangeRate: null, notes: '' })
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
      exchangeRate: form.exchangeRate || null,
      notes: form.notes || null,
    })
    showCreate.value = false
    showMessage('Transfer created and accepted. Upload its POP to execute it.')
    await loadTransfers()
  } catch (error: any) { showMessage(error?.data?.message || 'Failed to create wallet transfer', 'error') }
  finally { creating.value = false }
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
    showDetail.value = false
    showMessage('POP uploaded — wallet transfer executed successfully!')
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

onMounted(() => Promise.all([loadTransfers(), loadWallets()]))
</script>

<style scoped>
.file-row {
  background-color: rgba(var(--v-theme-on-surface), 0.06);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}
</style>
