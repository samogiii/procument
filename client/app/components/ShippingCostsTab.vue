<template>
  <v-card-text>
    <p class="text-body-2 text-medium-emphasis mb-4">
      Costs the warehouse entered with the shipping documents. They arrive with <strong>Sync warehouse</strong> above.
      Accept one and pick its wallet to withdraw it, or reject it to send it back.
    </p>
    <v-data-table
      :headers="headers"
      :items="costs"
      :loading="loading"
      density="comfortable"
      :items-per-page="25"
      hover
      class="cursor-pointer"
      @click:row="(_: unknown, row: { item: ShippingCost }) => open(row.item)"
    >
      <template #item.trackNumber="{ item }">
        <div class="font-weight-bold">{{ item.trackNumber }}</div>
        <div class="text-caption text-medium-emphasis">{{ [item.carrier, item.warehouseName].filter(Boolean).join(' · ') }}</div>
      </template>
      <template #item.pos="{ item }">
        <template v-if="item.pos.length">
          <NuxtLink v-for="(po, i) in item.pos" :key="po.id" :to="`/purchase-orders/${po.id}`" class="text-primary text-decoration-none" @click.stop>
            {{ po.number }}{{ i < item.pos.length - 1 ? ', ' : '' }}
          </NuxtLink>
        </template>
        <span v-else>—</span>
      </template>
      <template #item.fixedCost="{ item }">¥{{ formatPrice(item.fixedCost) }}</template>
      <template #item.otherCost="{ item }">¥{{ formatPrice(item.otherCost) }}</template>
      <template #item.total="{ item }"><span class="font-weight-bold">¥{{ formatPrice(item.total) }}</span></template>
      <template #item.enteredAt="{ item }">
        <div>{{ item.enteredBy }}</div>
        <div class="text-caption text-medium-emphasis">{{ new Date(item.enteredAt).toLocaleDateString() }}</div>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" variant="tonal">{{ item.status === 'Pending' ? 'Waiting for review' : item.status }}</v-chip>
      </template>
      <template #item.wallet="{ item }">
        <template v-if="item.walletName">{{ item.walletName }} · {{ currencySymbol(item.walletCurrency) }}{{ formatPrice(item.walletAmount) }}</template>
        <span v-else>—</span>
      </template>
      <template #no-data>No shipping costs yet.</template>
    </v-data-table>

    <v-dialog v-model="showDialog" max-width="620" scrollable>
      <v-card v-if="selected">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-truck-delivery-outline" class="mr-2" />
          {{ selected.trackNumber }}
          <v-spacer />
          <v-chip size="small" :color="statusColor(selected.status)" variant="tonal">{{ selected.status === 'Pending' ? 'Waiting for review' : selected.status }}</v-chip>
        </v-card-title>
        <v-divider />
        <v-card-text style="max-height: 75vh; overflow-y: auto;">
          <v-row dense class="mb-3">
            <v-col cols="6">
              <div class="text-caption text-medium-emphasis">Entered by</div>
              <div class="text-body-2">{{ selected.enteredBy }} · {{ new Date(selected.enteredAt).toLocaleString() }}</div>
            </v-col>
            <v-col cols="6">
              <div class="text-caption text-medium-emphasis">Carrier / Warehouse</div>
              <div class="text-body-2">{{ [selected.carrier, selected.warehouseName].filter(Boolean).join(' · ') || '—' }}</div>
            </v-col>
            <v-col cols="6">
              <div class="text-caption text-medium-emphasis">PO</div>
              <div class="text-body-2">{{ selected.pos.map(p => p.number).join(', ') || '—' }}</div>
            </v-col>
            <v-col cols="6">
              <div class="text-caption text-medium-emphasis">Sales Order</div>
              <div class="text-body-2">{{ selected.salesOrders.join(', ') || '—' }}</div>
            </v-col>
            <v-col cols="12">
              <div class="text-caption text-medium-emphasis">Parts</div>
              <div class="text-body-2">{{ selected.parts.join(', ') || '—' }}</div>
            </v-col>
          </v-row>

          <div class="pa-3 rounded calc mb-4">
            <div class="d-flex justify-space-between">
              <span class="text-medium-emphasis">Fixed</span>
              <span>¥{{ formatPrice(selected.fixedCost) }}</span>
            </div>
            <div class="d-flex justify-space-between mt-1">
              <span class="text-medium-emphasis">+ Others</span>
              <span>¥{{ formatPrice(selected.otherCost) }}</span>
            </div>
            <v-divider class="my-2" />
            <div class="d-flex justify-space-between font-weight-bold">
              <span>Total</span>
              <span>¥{{ formatPrice(selected.total) }} CNY</span>
            </div>
          </div>

          <div class="text-subtitle-2 mb-2">Shipping documents</div>
          <div v-if="docs.length" class="d-flex flex-column gap-2 mb-4">
            <div v-for="d in docs" :key="d.id" class="d-flex align-center gap-2 pa-2 rounded file-row">
              <v-icon icon="mdi-file-document-outline" color="primary" size="20" />
              <span class="text-body-2 font-weight-medium flex-grow-1 text-truncate">{{ d.name }}</span>
              <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="previewDoc(d)" />
            </div>
          </div>
          <v-alert v-else type="info" variant="tonal" density="compact" class="mb-4">
            {{ loadingDocs ? 'Loading…' : 'None in the office yet. The documents come with Sync warehouse on the Shipping page.' }}
          </v-alert>

          <template v-if="selected.status === 'Pending'">
            <v-select v-model="walletId" :items="wallets" item-title="label" item-value="id"
              label="Withdrawn From Wallet *" variant="outlined" prepend-inner-icon="mdi-bank-outline" class="mb-3" hide-details />
            <v-text-field v-if="wallet && !isCny" v-model.number="rate" type="number" min="0.000001" step="0.000001"
              :label="`CNY per 1 ${wallet.currency} *`" variant="outlined" class="mb-3" hide-details />
            <p v-if="wallet" class="text-body-2 mb-0">
              Wallet debit: {{ currencySymbol(wallet.currency) }}{{ formatPrice(debit) }} {{ wallet.currency }}
              <span v-if="!isCny && debit" class="text-medium-emphasis">(¥{{ formatPrice(selected.total) }} ÷ {{ rate }})</span>
            </p>
          </template>

          <template v-else-if="selected.status === 'Accepted'">
            <div class="text-body-2">
              Withdrawn from <strong>{{ selected.walletName }}</strong>:
              {{ currencySymbol(selected.walletCurrency) }}{{ formatPrice(selected.walletAmount) }} {{ selected.walletCurrency }}
              <span v-if="selected.exchangeRate" class="text-medium-emphasis">at {{ selected.exchangeRate }} CNY per 1 {{ selected.walletCurrency }}</span>
            </div>
            <div class="text-caption text-medium-emphasis mt-1">{{ selected.reviewedBy }} · {{ selected.reviewedAt ? new Date(selected.reviewedAt).toLocaleString() : '' }}</div>
          </template>

          <v-alert v-else type="error" variant="tonal" density="compact">
            Rejected by {{ selected.reviewedBy }}: {{ selected.rejectNote }}. The warehouse sees this after the next sync and can fix it.
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-btn variant="text" @click="showDialog = false">Close</v-btn>
          <v-spacer />
          <template v-if="selected.status === 'Pending'">
            <v-btn color="error" variant="tonal" prepend-icon="mdi-close-circle" @click="showReject = true">Reject</v-btn>
            <v-btn color="success" variant="flat" prepend-icon="mdi-check-circle" :disabled="!canAccept" :loading="accepting" @click="accept">Accept &amp; Withdraw</v-btn>
          </template>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="showReject" max-width="500">
      <v-card class="glass-card">
        <v-card-title class="pa-4">Reject Shipping Cost</v-card-title>
        <v-card-text class="pa-4">
          <div class="mb-4 text-body-2">Nothing is withdrawn for <strong>{{ selected?.trackNumber }}</strong>. After the next sync the phone shows your note and the warehouse can fix the cost.</div>
          <v-textarea v-model="rejectNote" label="Why is it rejected?" rows="3" variant="outlined" density="comfortable" counter="500" hide-details="auto" />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showReject = false">Cancel</v-btn>
          <v-btn color="error" variant="tonal" :disabled="!rejectNote.trim()" :loading="rejecting" @click="reject">Confirm Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">{{ snackbarText }}</v-snackbar>

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />
  </v-card-text>
</template>

<script setup lang="ts">
type ShippingCost = {
  id: number
  trackNumber: string
  carrier: string | null
  warehouseName: string | null
  fixedCost: number
  otherCost: number
  total: number
  currency: string
  enteredBy: string
  enteredAt: string
  status: 'Pending' | 'Accepted' | 'Rejected'
  rejectNote: string | null
  reviewedAt: string | null
  reviewedBy: string | null
  walletName: string | null
  walletCurrency: string
  exchangeRate: number | null
  walletAmount: number | null
  pos: { id: number; number: string }[]
  parts: string[]
  salesOrders: string[]
}
type CostDoc = { id: number; name: string; mimeType: string | null; uploadedAt: string }

const props = defineProps<{
  wallets: { id: number; label: string; currency: string }[]
  /** Bumped after a sync, which can bring new costs. */
  refreshKey: number
}>()
const emit = defineEmits<{ pending: [count: number] }>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()

const headers = [
  { title: 'Track #', key: 'trackNumber' },
  { title: 'PO', key: 'pos', sortable: false },
  { title: 'Fixed', key: 'fixedCost' },
  { title: 'Others', key: 'otherCost' },
  { title: 'Total', key: 'total' },
  { title: 'Entered by', key: 'enteredAt' },
  { title: 'Status', key: 'status' },
  { title: 'Wallet', key: 'wallet', sortable: false },
]

const loading = ref(false)
const costs = ref<ShippingCost[]>([])
const showDialog = ref(false)
const selected = ref<ShippingCost | null>(null)
const docs = ref<CostDoc[]>([])
const loadingDocs = ref(false)
const walletId = ref<number | null>(null)
const rate = ref<number | null>(null)
const accepting = ref(false)
const showReject = ref(false)
const rejectNote = ref('')
const rejecting = ref(false)

const wallet = computed(() => props.wallets.find(w => w.id === walletId.value))
const isCny = computed(() => wallet.value?.currency === 'CNY')
const debit = computed(() => {
  if (!selected.value || !wallet.value) return 0
  if (isCny.value) return selected.value.total
  return Number(rate.value) > 0 ? selected.value.total / Number(rate.value) : 0
})
const canAccept = computed(() => !!wallet.value && debit.value > 0)

watch(walletId, () => { rate.value = null })
watch(() => props.refreshKey, load)
onMounted(load)

async function load() {
  loading.value = true
  try {
    costs.value = await api.get<ShippingCost[]>('/shipping-costs')
    emit('pending', costs.value.filter(c => c.status === 'Pending').length)
  } catch { showSnack('Failed to load shipping costs', 'error') }
  finally { loading.value = false }
}

async function open(cost: ShippingCost) {
  selected.value = cost
  walletId.value = null
  docs.value = []
  showDialog.value = true
  loadingDocs.value = true
  try { docs.value = await api.get<CostDoc[]>(`/shipping-costs/${cost.id}/documents`) }
  catch { showSnack('Failed to load the shipping documents', 'error') }
  finally { loadingDocs.value = false }
}

async function previewDoc(doc: CostDoc) {
  if (!selected.value) return
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/shipping-costs/${selected.value.id}/documents/${doc.id}/file`, {
      responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob, doc.name)
  } catch { showSnack('Failed to open file', 'error') }
}

async function accept() {
  if (!selected.value || !canAccept.value || accepting.value) return
  accepting.value = true
  try {
    const result = await api.post<any>(`/shipping-costs/${selected.value.id}/accept`, {
      walletId: walletId.value,
      exchangeRate: isCny.value ? null : rate.value,
    })
    showSnack(`Accepted: ${formatPrice(result.walletAmount)} ${result.walletCurrency} withdrawn from ${result.walletName}.`, 'success')
    showDialog.value = false
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not accept the shipping cost', 'error')
  } finally { accepting.value = false }
}

async function reject() {
  if (!selected.value || !rejectNote.value.trim()) return
  rejecting.value = true
  try {
    await api.post(`/shipping-costs/${selected.value.id}/reject`, { note: rejectNote.value.trim() })
    showSnack('Rejected. The warehouse sees your note after the next sync.', 'warning')
    showReject.value = false
    showDialog.value = false
    rejectNote.value = ''
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not reject the shipping cost', 'error')
  } finally { rejecting.value = false }
}

function statusColor(status: string) {
  return status === 'Accepted' ? 'success' : status === 'Rejected' ? 'error' : 'warning'
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
