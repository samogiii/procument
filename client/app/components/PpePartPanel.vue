<template>
  <!--
    New Procurement Plus — one part, expanded: the RFQs asking for it (deadline at most 14 days ago) and its
    supplier rows. Quote rows stand for the same supplier quote on every RFQ (chips say which); editing one
    updates it on all of them. Warning rows = suppliers added here without a price yet (their accepted quote
    mail fills them); blue = accepted Email Quotes — editing one edits the email line and every RFQ it is on.
  -->
  <div class="supplier-panel pa-4">
    <div class="d-flex align-center flex-wrap gap-2 mb-3">
      <span class="text-caption text-uppercase font-weight-bold" style="color: #60a5fa;">
        Supplier Quotes for {{ part.partNumber }}
      </span>
      <v-spacer />
      <v-btn size="x-small" color="blue-grey" variant="tonal" prepend-icon="mdi-history" title="Every supplier / price recorded for this part on any RFQ" @click.stop="historyOpen = true">
        History
      </v-btn>
      <v-btn size="x-small" color="deep-purple" variant="tonal" prepend-icon="mdi-store-search-outline" title="Suppliers listing this part in your ILS files — click one to add it" @click.stop="availableOpen = true">
        Available suppliers
      </v-btn>
      <v-btn
        v-if="selectedRows.length"
        size="x-small"
        color="success"
        variant="flat"
        prepend-icon="mdi-percent-circle-outline"
        title="Set the coef for the ticked suppliers, per customer — saving takes the part off your list"
        @click.stop="commissionOpen = true"
      >
        Add Commission ({{ selectedRows.length }})
      </v-btn>
      <v-btn size="x-small" color="primary" variant="flat" prepend-icon="mdi-plus" @click.stop="openNew()">Add Supplier</v-btn>
    </div>

    <!-- RFQs this part is on -->
    <div class="d-flex flex-wrap align-center gap-1 mb-3">
      <span class="text-caption text-medium-emphasis mr-1">On {{ detail?.rfqs.length ?? 0 }} RFQ{{ detail?.rfqs.length === 1 ? '' : 's' }}:</span>
      <v-chip
        v-for="r in detail?.rfqs ?? []"
        :key="r.rfqItemId"
        size="x-small"
        variant="tonal"
        :color="deadlineColor(r.deadline)"
        :to="`/rfqs/${r.rfqId}`"
        :title="`${r.partNumber} · Qty ${r.qty}${r.condition ? ' · ' + r.condition : ''} · deadline ${fmtDate(r.deadline)}`"
      >
        {{ r.rfqName }}<span v-if="r.customerCode" class="ml-1 text-medium-emphasis">· {{ r.customerCode }}</span>
        <span class="ml-1">· {{ deadlineLabel(r.deadline) }}</span>
      </v-chip>
      <span v-if="detail && !detail.rfqs.length" class="text-caption text-warning">
        No RFQ with a deadline in the last {{ PPE_DEADLINE_DAYS }} days — prices are kept and auto-filled onto new RFQs for a week.
      </span>
    </div>

    <div v-if="loading && !detail" class="d-flex justify-center py-6"><v-progress-circular indeterminate color="primary" size="28" /></div>
    <v-alert v-else-if="error" type="error" variant="tonal" density="compact">{{ error }}</v-alert>
    <div v-else-if="detail?.rows.length" style="overflow-x: auto;">
      <table class="quote-grid">
        <thead>
          <tr>
            <th style="width: 32px;" title="Tick priced suppliers to add the commission" />
            <th style="min-width: 200px;">Supplier</th>
            <th style="min-width: 180px;">RFQs</th>
            <th>Cond</th>
            <th>Alt P/N</th>
            <th>Qty</th>
            <th style="min-width: 110px;">Cost Price ($)</th>
            <th>Cert Type</th>
            <th>Tagged By</th>
            <th>Buy LeadTime</th>
            <th style="min-width: 140px;">Note</th>
            <th style="width: 90px;" />
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in detail.rows"
            :key="row.key"
            class="quote-row"
            :class="{ 'row--draft': row.kind === 'Draft', 'row--email': row.kind === 'Email' }"
            :title="row.readOnly ? 'View only' : row.kind === 'Email' ? 'Accepted Email Quote — double-click to edit (Email Quotes and every RFQ it is on get the edit)' : 'Double-click to edit'"
            @dblclick.stop="!row.readOnly && openEdit(row)"
          >
            <td>
              <!-- Priced and on the RFQs: can get a commission -->
              <v-checkbox-btn
                v-if="canCommission(row)"
                :model-value="selected.has(row.key)"
                density="compact"
                color="success"
                @click.stop
                @dblclick.stop
                @update:model-value="toggleSelected(row.key)"
              />
            </td>
            <td>
              <div class="quote-cell quote-cell--supplier">
                <span class="font-weight-medium">{{ row.supplierName || '—' }}</span>
                <v-chip v-if="row.kind === 'Email'" size="x-small" color="info" variant="tonal" class="ml-1" prepend-icon="mdi-email-check-outline">Email · accepted</v-chip>
                <v-chip
                  v-else-if="row.kind === 'Draft'"
                  size="x-small"
                  color="warning"
                  variant="tonal"
                  class="ml-1"
                  title="Type the price, or accept this supplier's quote mail in Email Quotes — it fills price, condition and alt"
                >
                  {{ row.reason || 'No price' }}
                </v-chip>
                <v-chip v-if="row.supplierId == null && row.kind !== 'Email'" size="x-small" color="error" variant="text" class="ml-1" title="Pick the supplier from the catalog (edit the row) to put the price on the RFQs">not in catalog</v-chip>
                <v-icon v-if="row.supplierTrusted" icon="mdi-shield-check-outline" size="14" color="success" class="ml-1" title="Trusted supplier" />
                <v-chip
                  v-if="commissionText(row)"
                  size="x-small"
                  color="deep-purple"
                  variant="tonal"
                  prepend-icon="mdi-lock-outline"
                  class="ml-1"
                  title="Commission saved — Coef 1 is locked on the Add Quote page"
                >
                  {{ commissionText(row) }}
                </v-chip>
              </div>
              <div v-if="row.kind === 'Draft' && row.fileName" class="text-caption text-medium-emphasis px-2">from {{ row.fileName }}</div>
            </td>
            <td>
              <div class="d-flex flex-wrap gap-1 py-1">
                <v-chip
                  v-for="r in row.rfqs.slice(0, 4)"
                  :key="r.rfqItemId"
                  size="x-small"
                  variant="outlined"
                  :color="deadlineColor(r.deadline)"
                  :to="`/rfqs/${r.rfqId}`"
                  :title="`${r.rfqName} · asks for ${r.partNumber} · deadline ${fmtDate(r.deadline)}`"
                  @click.stop
                >
                  {{ r.rfqName }}
                </v-chip>
                <v-chip v-if="row.rfqs.length > 4" size="x-small" variant="text" :title="row.rfqs.slice(4).map(r => r.rfqName).join(', ')">+{{ row.rfqs.length - 4 }}</v-chip>
                <span v-if="!row.rfqs.length" class="quote-cell text-medium-emphasis">{{ row.kind === 'Draft' ? 'not on RFQs yet' : '—' }}</span>
              </div>
            </td>
            <td><span class="quote-cell">{{ cellText(row.condition) }}</span></td>
            <!-- A supplier added from the files has no condition / alt until it quotes -->
            <td><span class="quote-cell">{{ row.alt || (row.kind === 'Draft' && !row.condition ? '—' : 'Same P/N') }}</span></td>
            <td><span class="quote-cell">{{ cellText(row.qty) }}</span></td>
            <td>
              <!-- A draft: type the price here (Enter) — it goes onto every RFQ for the part -->
              <div v-if="row.kind === 'Draft'" class="d-flex align-center">
                <input
                  class="price-box price-text"
                  type="number"
                  min="0"
                  step="0.01"
                  :placeholder="row.filePrice ? `file: ${row.filePrice}` : 'Price'"
                  :value="row.price ?? ''"
                  :disabled="savingKey === row.key"
                  title="Type the price and press Enter"
                  @dblclick.stop
                  @keydown.enter.prevent="(e: KeyboardEvent) => savePrice(row, (e.target as HTMLInputElement).value)"
                  @change="(e: Event) => savePrice(row, (e.target as HTMLInputElement).value)"
                >
                <v-progress-circular v-if="savingKey === row.key" indeterminate size="14" width="2" class="ml-1" />
              </div>
              <span v-else class="quote-cell price-text">{{ row.price ? '$' + formatPrice(row.price) : '—' }}</span>
            </td>
            <td><span class="quote-cell">{{ cellText(row.certName) }}</span></td>
            <td><span class="quote-cell">{{ cellText(row.taggedBy) }}</span></td>
            <td><span class="quote-cell">{{ cellText(row.leadTime) }}</span></td>
            <td><div class="quote-cell quote-cell--text">{{ cellText(row.note) }}</div></td>
            <td class="text-right" style="white-space: nowrap;">
              <v-btn v-if="!row.readOnly" icon="mdi-pencil" size="x-small" variant="text" color="primary" title="Edit" @click.stop="openEdit(row)" />
              <v-btn
                v-if="row.kind === 'Draft' && row.lineId"
                icon="mdi-close"
                size="x-small"
                variant="text"
                color="error"
                title="Remove this supplier"
                :loading="savingKey === `rm-${row.key}`"
                @click.stop="removeDraft(row)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else-if="detail" class="text-caption text-medium-emphasis text-center py-4 mb-0">
      No suppliers yet — use Available suppliers, the ILS tab, or Add Supplier.
    </p>

    <SupplierQuoteEditDialog
      v-model="editor.open"
      :quote="editor.quote"
      :part-number="part.partNumber"
      :customer-base="editor.customerBase"
      :rfq-id="editor.rfqId"
      :cert-quotes="editorCertQuotes"
      :save="saveFromEditor"
    />

    <ProcurementPlusPartHistoryDialog
      v-model="historyOpen"
      :part-id="part.id"
      :part-number="part.partNumber"
      :url="api.url(`/parts/${part.id}/history`)"
      @add-supplier="addFromHistory"
    />

    <PpeAvailableDialog v-model="availableOpen" :part="part" @added="onAdded" />

    <PpeCommissionDialog v-model="commissionOpen" :part="part" :rows="selectedRows" @saved="onCommissionSaved" />

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="5000">{{ snackText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { PpePartDetail, PpePartRow, PpeQuoteRow, PpeSaveResult } from '~/utils/ppe'
import type { PpRfqQuote } from '~/utils/procurementPlus'

const props = defineProps<{ part: PpePartRow }>()
/** The part's row changed (counts, average) — the table patches it in place; removed = commission saved, it left the list. */
const emit = defineEmits<{ changed: [row: PpePartRow]; removed: [] }>()

const api = usePpeApi()
const detail = ref<PpePartDetail | null>(null)
const loading = ref(false)
const error = ref('')
const savingKey = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    detail.value = await api.get<PpePartDetail>(`/parts/${props.part.id}`)
    const keys = new Set(detail.value.rows.filter(canCommission).map(r => r.key))
    selected.value = new Set([...selected.value].filter(k => keys.has(k)))
    emit('changed', detail.value.part)
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not load the part'
  } finally {
    loading.value = false
  }
}
onMounted(load)

const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

// ── Editing (SupplierQuoteEditDialog) ──
const editor = ref<{ open: boolean; quote: any; row: PpeQuoteRow | null; rfqId: number | null; customerBase: number | null }>({
  open: false, quote: null, row: null, rfqId: null, customerBase: null,
})
const firstRfq = () => detail.value?.rfqs[0]

function blankQuote(extra: Record<string, any> = {}) {
  return {
    id: null,
    supplierName: '',
    supplierId: null,
    supplierEmail: null,
    qty: props.part.qty || 1,
    price: 0,
    condition: props.part.condition || 'NE',
    alt: '',
    certName: '',
    tagDate: '',
    shippingCost: null,
    shippingPoint: '',
    unit: 'EA',
    leadTime: '',
    sellLeadTime: '',
    note: '',
    myNotes: '',
    companyPresetId: null,
    customCertReferences: [],
    ...emptyQuoteDetails(),
    ...extra,
  }
}

function openNew(extra: Record<string, any> = {}) {
  editor.value = { open: true, quote: blankQuote(extra), row: null, rfqId: null, customerBase: firstRfq()?.customerBase ?? null }
}

function openEdit(row: PpeQuoteRow) {
  const quoteId = row.kind === 'Draft' ? null : row.quoteIds[0] ?? null
  // rfqs are sorted by deadline, quoteIds newest first: take the RFQ of the quote being edited.
  const rfq = row.rfqs.find(r => r.quoteId === quoteId) ?? row.rfqs[0]
  editor.value = {
    open: true,
    // Quote rows edit their newest RFQ quote (cert PDFs attach to it); drafts are not on an RFQ yet.
    quote: {
      ...row,
      id: quoteId,
      qty: row.qty ?? props.part.qty ?? 1,
      price: row.price ?? 0,
      tagDate: row.tagDate ?? '',
    },
    row,
    rfqId: row.kind === 'Draft' ? null : rfq?.rfqId ?? null,
    customerBase: (rfq ?? firstRfq())?.customerBase ?? null,
  }
}

/** The row's quotes on its other RFQs — their cert PDFs show in the dialog too (same supplier offer). */
const editorCertQuotes = computed(() =>
  (editor.value.row?.rfqs ?? [])
    .filter(r => r.quoteId)
    .map(r => ({ rfqId: r.rfqId, quoteId: r.quoteId! })),
)

function payloadFor(form: any, row: PpeQuoteRow | null) {
  return {
    // Qty / price are required numbers in the payload: 0 = not given (a draft keeps them empty).
    ...toSupplierQuotePayload({ ...form, id: null, rfqItemId: 0, qty: form.qty ?? 0, price: form.price ?? 0 }),
    lineId: row?.lineId ?? null,
    // A quote / email row is the same supplier quote on every RFQ: the edit reaches all of them (an email line behind
    // some of them takes the edit itself and writes it again).
    quoteIds: row && row.kind !== 'Draft' ? row.quoteIds : [],
    emailLineId: row?.emailLineId ?? null,
  }
}

function report(res: PpeSaveResult) {
  if (res.written && !res.quotesWritten && res.message) toast(`Saved — ${res.message}`, 'info')
  else if (res.written) toast(`On the RFQs — ${res.quotesWritten} supplier quote${res.quotesWritten === 1 ? '' : 's'} written`)
  else if (res.message) toast(res.message, 'warning')
  else toast('Saved — fill the price to put it on the RFQs', 'info')
}

/** Dialog save: returns the quote it can attach cert PDFs to (the line's newest RFQ quote). Throws keep it open. */
async function saveFromEditor(form: any) {
  const res = await api.post<PpeSaveResult>(`/parts/${props.part.id}/quotes`, payloadFor(form, editor.value.row))
  if (res.rfqId) editor.value.rfqId = res.rfqId
  report(res)
  await load()
  editor.value.row = detail.value?.rows.find(r => r.lineId === res.lineId || r.emailLineId === res.lineId) ?? editor.value.row
  return { ...form, id: res.quoteId ?? null }
}

async function savePrice(row: PpeQuoteRow, value: string) {
  const price = Number(value)
  if (!(price > 0) || price === row.price || savingKey.value) return
  savingKey.value = row.key
  try {
    const res = await api.post<PpeSaveResult>(`/parts/${props.part.id}/quotes`, payloadFor({ ...row, price, tagDate: row.tagDate ?? '' }, row))
    report(res)
    await load()
  } catch (e: any) {
    toast(e?.data?.message || 'Could not save the price', 'error')
  } finally {
    savingKey.value = ''
  }
}

async function removeDraft(row: PpeQuoteRow) {
  savingKey.value = `rm-${row.key}`
  try {
    await api.del(`/lines/${row.lineId}`)
    await load()
  } catch (e: any) {
    toast(e?.data?.message || 'Could not remove it', 'error')
  } finally {
    savingKey.value = ''
  }
}

// ── Add Commission: tick priced suppliers on the RFQs ──
const selected = ref(new Set<string>())
const commissionOpen = ref(false)
const canCommission = (row: PpeQuoteRow) => row.kind !== 'Draft' && (row.price ?? 0) > 0 && row.rfqs.some(r => r.quoteId)
const selectedRows = computed(() => (detail.value?.rows ?? []).filter(r => selected.value.has(r.key)))
function toggleSelected(key: string) {
  const next = new Set(selected.value)
  if (next.has(key)) next.delete(key)
  else next.add(key)
  selected.value = next
}
/** "C501 1.2 · C502 1.15" — the commission coefs saved on this row's RFQ quotes. */
function commissionText(row: PpeQuoteRow) {
  const seen = new Map<string, number>()
  // Only active commissions (7 days) — older ones are a plain Coef 1 again.
  for (const r of row.rfqs) if (commissionActive(r.commissionAt) && r.coef1 != null) seen.set(r.customerCode || r.customerName || '?', r.coef1)
  return [...seen].map(([c, coef]) => `${c} ${coef}`).join(' · ')
}
function onCommissionSaved() {
  selected.value = new Set()
  emit('removed')
}

// ── History / Available suppliers ──
const historyOpen = ref(false)
const availableOpen = ref(false)

function addFromHistory(q: PpRfqQuote) {
  historyOpen.value = false
  openNew({ supplierName: q.supplierName, condition: q.condition || props.part.condition || 'NE', alt: q.alt || '' })
}

function onAdded(count: number) {
  if (count) toast(`${count} supplier${count === 1 ? '' : 's'} added — fill the price${count === 1 ? '' : 's'} to put them on the RFQs`)
  load()
}
</script>

<style scoped>
.supplier-panel {
  border-top: 2px solid rgba(var(--v-theme-primary), 0.3);
  background: rgba(var(--v-theme-surface-variant), 0.3);
}
.quote-grid {
  width: 100%;
  border-collapse: collapse;
  min-width: 1100px;
}
.quote-grid th {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 8px 10px;
  color: rgba(var(--v-theme-on-surface), 0.6);
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  text-align: left;
  white-space: nowrap;
}
.quote-row td {
  padding: 2px 4px;
  vertical-align: middle;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.05);
}
.row--draft td {
  background: rgba(var(--v-theme-warning), 0.12);
}
.row--email td {
  background: rgba(var(--v-theme-info), 0.1);
}
.quote-cell {
  display: block;
  min-height: 28px;
  line-height: 28px;
  padding: 0 6px;
  font-size: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 220px;
}
.quote-cell--text {
  line-height: 1.4;
  padding: 4px 6px;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  max-height: 4.4em;
  overflow-y: auto;
  min-width: 140px;
}
.quote-cell--supplier {
  display: flex;
  align-items: center;
  max-width: none;
}
.price-text {
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
}
.price-box {
  width: 110px;
  height: 28px;
  border: 1px solid rgba(var(--v-theme-warning), 0.6);
  border-radius: 4px;
  padding: 0 6px;
  font-size: 12px;
  background: rgba(var(--v-theme-surface), 0.6);
  outline: none;
}
.price-box:focus {
  border-color: rgb(var(--v-theme-primary));
}
</style>
