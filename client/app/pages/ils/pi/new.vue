<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="navigateTo('/ils?tab=pi')" />
      <div>
        <h1 class="text-h5 font-weight-bold">New Proforma Invoice</h1>
        <p class="text-caption text-medium-emphasis mb-0">Select accepted quotes from one customer, then choose the parts to invoice.</p>
      </div>
      <v-spacer />
      <div class="text-right mr-3">
        <div class="text-caption text-medium-emphasis">Total</div>
        <div class="font-weight-bold" style="font-family: monospace; color: #4ade80;">${{ formatPrice(grandTotal) }}</div>
      </div>
      <v-btn
        color="primary"
        variant="flat"
        prepend-icon="mdi-content-save"
        :loading="saving"
        :disabled="!form.ilsCustomerId || !selectedLines.length"
        @click="createPI"
      >
        Create PI
      </v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <!-- Header -->
    <v-card class="glass-card mb-4">
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="4">
            <v-select
              v-model="form.ilsCustomerId"
              :items="customersWithAccepted"
              item-title="name"
              item-value="id"
              label="Customer (with accepted quotes) *"
              variant="outlined"
              density="compact"
              hide-details
              no-data-text="No customers with accepted quotes"
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="form.subject" label="Subject" variant="outlined" density="compact" hide-details placeholder="e.g. Engine Parts" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="form.customerPONumber" label="Customer PO #" variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" md="6">
            <v-textarea v-model="form.billTo" label="Bill To" variant="outlined" density="compact" hide-details rows="2" />
          </v-col>
          <v-col cols="12" md="6">
            <v-textarea v-model="form.shipTo" label="Ship To" variant="outlined" density="compact" hide-details rows="2" />
          </v-col>
          <v-col cols="12">
            <v-textarea v-model="form.notes" label="Notes" variant="outlined" density="compact" hide-details rows="2" />
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <!-- Accepted quotes for this customer -->
    <v-card v-if="form.ilsCustomerId" class="glass-card mb-4">
      <v-card-title class="text-subtitle-2 pa-4 pb-2">Accepted Quotes</v-card-title>
      <v-divider />
      <v-card-text>
        <div v-if="!customerAcceptedQuotes.length" class="text-caption text-medium-emphasis py-2">
          No accepted quotes for this customer.
        </div>
        <div v-else class="d-flex flex-wrap gap-2">
          <v-checkbox
            v-for="q in customerAcceptedQuotes"
            :key="q.id"
            v-model="selectedQuoteIds"
            :value="q.id"
            :label="`${q.quoteNumber} · $${formatPrice(q.totalAmount)}`"
            density="compact"
            hide-details
            color="primary"
            @update:model-value="onQuoteSelectionChange"
          />
        </div>
      </v-card-text>
    </v-card>

    <!-- Merged items to invoice -->
    <v-card v-if="lines.length" class="glass-card">
      <v-card-title class="text-subtitle-2 pa-4 pb-2">Items to Invoice</v-card-title>
      <v-divider />
      <v-card-text class="pa-0">
        <table class="pi-table">
          <thead>
            <tr>
              <th style="width: 44px;"></th>
              <th>Quote</th>
              <th>Part Number</th>
              <th>Serial #</th>
              <th style="width: 90px;">Condition</th>
              <th style="width: 60px;">Qty</th>
              <th style="width: 120px;">Sell $</th>
              <th style="width: 130px;">Total</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, idx) in lines" :key="idx" :class="{ 'row-on': row.include }">
              <td class="text-center"><v-checkbox-btn v-model="row.include" color="primary" /></td>
              <td style="font-size: 12px; color: rgba(var(--v-theme-on-surface), 0.6); padding-left: 6px;">{{ row.quoteNumber }}</td>
              <td style="font-family: monospace; padding-left: 6px;">
                {{ row.partNumberName }}<span v-if="row.altPartNumber" style="color:#fbbf24;"> / {{ row.altPartNumber }}</span>
              </td>
              <td style="font-family: monospace; font-size: 12px; padding-left: 6px;">{{ row.serialNumber || '—' }}</td>
              <td>
                <v-chip v-if="row.condition" size="x-small" variant="tonal" :color="conditionColor(row.condition)">{{ row.condition }}</v-chip>
                <span v-else class="text-medium-emphasis">—</span>
              </td>
              <td class="text-center">{{ row.qty }}</td>
              <td class="text-right" style="font-family: monospace; padding-right: 10px;">${{ formatPrice(row.sellPrice) }}</td>
              <td class="text-right" style="font-family: monospace; padding-right: 10px; color: #4ade80; font-weight: 600;">
                {{ row.include ? '$' + formatPrice(row.totalPrice) : '—' }}
              </td>
            </tr>
          </tbody>
        </table>
      </v-card-text>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()

const loading = ref(false)
const saving = ref(false)
const customers = ref<any[]>([])
const quotes = ref<any[]>([])
const selectedQuoteIds = ref<number[]>([])
const quoteDetailCache = reactive<Record<number, any>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text; snackbarColor.value = color; snackbar.value = true
}

function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error', RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

interface PILine {
  include: boolean
  quoteNumber: string
  sourceQuoteId: number
  sourceQuoteItemId: number
  partNumberId: number
  partNumberName: string
  altPartNumber: string | null
  condition: string | null
  certName: string | null
  qty: number
  sellPrice: number
  totalPrice: number
  leadTime: string | null
  serialNumber: string | null
  ilsItemSerialId: number | null
  ilsItemId: number | null
}
const lines = ref<PILine[]>([])

const form = ref({
  ilsCustomerId: null as number | null,
  subject: '',
  customerPONumber: '',
  billTo: '',
  shipTo: '',
  notes: '',
})

const acceptedQuotes = computed(() => quotes.value.filter(q => q.status === 'Accepted'))

const customersWithAccepted = computed(() => {
  const ids = new Set(acceptedQuotes.value.map(q => q.ilsCustomerId))
  return customers.value.filter(c => ids.has(c.id))
})

const customerAcceptedQuotes = computed(() =>
  acceptedQuotes.value.filter(q => q.ilsCustomerId === form.value.ilsCustomerId)
)

const selectedLines = computed(() => lines.value.filter(l => l.include))
const grandTotal = computed(() => selectedLines.value.reduce((s, l) => s + (l.totalPrice || 0), 0))

// Prefill Bill/Ship from the chosen customer; reset quote selection
watch(() => form.value.ilsCustomerId, (id) => {
  selectedQuoteIds.value = []
  lines.value = []
  const c = customers.value.find(x => x.id === id)
  if (c) {
    if (!form.value.billTo) form.value.billTo = c.billTo || ''
    if (!form.value.shipTo) form.value.shipTo = c.shipTo || ''
  }
})

async function onQuoteSelectionChange() {
  // Load details for any newly selected quote
  for (const qid of selectedQuoteIds.value) {
    if (!quoteDetailCache[qid]) {
      try {
        quoteDetailCache[qid] = await api.get<any>(`/ils-quotes/${qid}`)
      } catch {
        quoteDetailCache[qid] = { items: [] }
      }
    }
  }
  rebuildLines()
}

function rebuildLines() {
  const existing = new Map(lines.value.map(l => [`${l.sourceQuoteId}-${l.sourceQuoteItemId}`, l.include]))
  const next: PILine[] = []
  for (const qid of selectedQuoteIds.value) {
    const q = quoteDetailCache[qid]
    if (!q) continue
    for (const it of (q.items || [])) {
      const key = `${qid}-${it.id}`
      next.push({
        include: existing.has(key) ? existing.get(key)! : true,
        quoteNumber: q.quoteNumber,
        sourceQuoteId: qid,
        sourceQuoteItemId: it.id,
        partNumberId: it.partNumberId,
        partNumberName: it.partNumberName,
        altPartNumber: it.altPartNumber || null,
        condition: it.condition || null,
        certName: it.certName || null,
        qty: it.qty,
        sellPrice: Number(it.sellPrice) || 0,
        totalPrice: Number(it.totalPrice) || 0,
        leadTime: it.leadTime || null,
        serialNumber: it.serialNumber || null,
        ilsItemSerialId: it.ilsItemSerialId ?? null,
        ilsItemId: it.ilsItemId ?? null,
      })
    }
  }
  lines.value = next
}

async function load() {
  loading.value = true
  try {
    const [cs, qs] = await Promise.all([
      api.get<any[]>('/ils-customers'),
      api.get<any[]>('/ils-quotes'),
    ])
    customers.value = cs
    quotes.value = qs
  } catch {
    showSnack('Failed to load data', 'error')
  } finally {
    loading.value = false
  }
}

async function createPI() {
  if (!form.value.ilsCustomerId || !selectedLines.value.length) return
  saving.value = true
  try {
    const created = await api.post<any>('/ils-proforma', {
      ilsCustomerId: form.value.ilsCustomerId,
      billTo: form.value.billTo || null,
      shipTo: form.value.shipTo || null,
      subject: form.value.subject || null,
      customerPONumber: form.value.customerPONumber || null,
      notes: form.value.notes || null,
      sourceQuoteIds: selectedQuoteIds.value,
      items: selectedLines.value.map(l => ({
        partNumberId: l.partNumberId,
        partNumberName: l.partNumberName,
        altPartNumber: l.altPartNumber,
        condition: l.condition,
        certName: l.certName,
        qty: Number(l.qty) || 1,
        sellPrice: Number(l.sellPrice) || 0,
        totalPrice: Number(l.totalPrice) || 0,
        leadTime: l.leadTime,
        serialNumber: l.serialNumber,
        ilsItemSerialId: l.ilsItemSerialId,
        ilsItemId: l.ilsItemId,
        sourceQuoteId: l.sourceQuoteId,
        sourceQuoteItemId: l.sourceQuoteItemId,
      })),
    })
    navigateTo(`/ils/pi/${created.id}`)
  } catch {
    showSnack('Failed to create PI', 'error')
    saving.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.pi-table { width: 100%; border-collapse: collapse; }
.pi-table th {
  background: rgba(var(--v-theme-surface-variant), 0.5);
  padding: 8px; text-align: left; font-size: 11px; font-weight: 600;
  text-transform: uppercase; letter-spacing: 0.04em;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}
.pi-table td { padding: 4px 4px; border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.06); vertical-align: middle; }
.pi-table tr.row-on td { background: rgba(var(--v-theme-primary), 0.06); }
</style>
