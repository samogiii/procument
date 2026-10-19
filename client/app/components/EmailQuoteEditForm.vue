<template>
  <!--
    Editable fields of one email quote line. Changes save themselves (debounced, flushed on
    blur/unmount) and cert PDFs upload as soon as they are picked — no Save / Upload buttons.
    Used by EmailQuoteEditDialog, the side panel of QuoteEmailViewerDialog and Procurement Plus
    (apiBase="/procurement-plus/quotes").
  -->
  <div v-if="form" class="eqf" :class="{ 'eqf--compact': compact }">
    <div class="eqf-status">
      <template v-if="status === 'saving'">
        <v-progress-circular indeterminate size="12" width="2" color="primary" class="mr-2" />Saving…
      </template>
      <template v-else-if="status === 'error'">
        <v-icon icon="mdi-alert-circle" size="14" color="error" class="mr-1" />
        <span class="text-error">{{ statusText }}</span>
        <v-btn v-if="canRetry" size="x-small" variant="text" color="error" class="ml-1" @click="flush">Retry</v-btn>
      </template>
      <template v-else-if="status === 'saved'">
        <v-icon icon="mdi-cloud-check-outline" size="14" color="success" class="mr-1" />
        <span class="text-success">All changes saved</span>
      </template>
      <template v-else>
        <v-icon icon="mdi-content-save-cog-outline" size="14" class="mr-1" />Changes save automatically
      </template>
    </div>

    <v-alert v-if="accepted" type="info" variant="tonal" density="compact" class="mb-3">
      Already accepted — changes apply only to new RFQs that pick this quote up. RFQs it was already added to keep their values.
    </v-alert>

    <v-autocomplete
      v-if="!hideSupplier"
      v-model="form.supplierId"
      :items="supplierOptions"
      item-title="name"
      item-value="id"
      label="Supplier"
      density="compact"
      variant="outlined"
      class="mb-3"
      :loading="supplierSearchLoading"
      no-filter
      clearable
      hide-details="auto"
      @update:search="searchSuppliers"
    />
    <div class="eqf-row">
      <v-text-field
        v-model="form.partNumber"
        label="Part Number"
        density="compact"
        variant="outlined"
        :error-messages="form.partNumber?.trim() ? '' : 'Required'"
        hide-details="auto"
        @blur="flush"
      />
      <v-text-field v-model="form.alt" label="Alt P/N (offered instead)" density="compact" variant="outlined" clearable hide-details @blur="flush" />
    </div>
    <div class="eqf-row">
      <v-text-field v-model.number="form.price" type="number" label="Price ($)" density="compact" variant="outlined" hide-details @blur="flush" />
      <v-text-field v-model.number="form.qty" type="number" label="Qty" density="compact" variant="outlined" hide-details @blur="flush" />
    </div>
    <div class="eqf-row eqf-row--3">
      <v-combobox v-model="form.condition" :items="CONDITIONS" label="Condition" density="compact" variant="outlined" hide-details />
      <v-combobox v-model="form.certName" :items="CERT_TYPES" label="Cert" density="compact" variant="outlined" hide-details />
      <v-text-field
        v-model="form.tagDate"
        type="date"
        label="Tag Date"
        density="compact"
        variant="outlined"
        :hint="form.tagDateRaw && !form.tagDate ? `In the email: ${form.tagDateRaw}` : ''"
        :persistent-hint="!!(form.tagDateRaw && !form.tagDate)"
        :hide-details="!(form.tagDateRaw && !form.tagDate)"
        @blur="flush"
      />
    </div>
    <div class="eqf-row">
      <v-text-field v-model="form.leadTime" label="Lead Time (buy)" placeholder="e.g. 1-2 weeks, Stock" density="compact" variant="outlined" hide-details @blur="flush" />
      <v-text-field
        v-model="form.sellLeadTime"
        label="Sell Lead Time"
        :placeholder="sellLeadTimePreview ? `auto: ${sellLeadTimePreview}` : 'auto'"
        density="compact"
        variant="outlined"
        clearable
        hide-details
        title="Leave blank to derive it per RFQ customer (Base 1 differs)"
        @blur="flush"
      />
    </div>
    <div class="eqf-row">
      <v-text-field v-model.number="form.shippingCost" type="number" label="Shipping Cost ($)" density="compact" variant="outlined" hide-details @blur="flush" />
      <v-text-field v-model="form.shippingPoint" label="Shipping Point" placeholder="City / Hub" density="compact" variant="outlined" hide-details @blur="flush" />
    </div>
    <div class="eqf-label">QUOTE DETAILS</div>
    <div class="eqf-row eqf-row--3">
      <template v-for="f in QUOTE_DETAIL_FIELDS" :key="f.key">
        <v-combobox
          v-if="f.suggestions.length"
          v-model="form[f.key]"
          :items="f.suggestions"
          :label="f.label"
          :placeholder="f.placeholder"
          density="compact"
          variant="outlined"
          clearable
          hide-details
          @blur="flush"
        />
        <v-text-field
          v-else
          v-model="form[f.key]"
          :label="f.label"
          :placeholder="f.placeholder"
          density="compact"
          variant="outlined"
          clearable
          hide-details
          @blur="flush"
        />
      </template>
    </div>
    <v-textarea v-model="form.remark" label="Note" rows="2" auto-grow density="compact" variant="outlined" class="mb-3" hide-details @blur="flush" />
    <v-textarea v-model="form.myNotes" label="My Notes" rows="2" auto-grow density="compact" variant="outlined" class="mb-3" hide-details @blur="flush" />
    <v-combobox
      v-model="form.customCertReferences"
      label="Cert Reference"
      placeholder="Type a reference and press Enter"
      multiple
      chips
      closable-chips
      density="compact"
      variant="outlined"
      class="mb-3"
      hide-details
    />

    <!-- Cert PDFs: uploaded the moment they are picked; copied to each RFQ supplier quote on accept -->
    <div class="eqf-label">CERT PDFs</div>
    <div class="d-flex flex-wrap gap-1 mb-2">
      <v-chip
        v-for="c in form.certificates"
        :key="c.id"
        size="small"
        variant="outlined"
        color="red"
        prepend-icon="mdi-file-pdf-box"
        closable
        @click="emit('open-certificate', c)"
        @click:close="deleteCertificate(c)"
      >
        {{ c.fileName }}
      </v-chip>
      <span v-if="!form.certificates?.length" class="text-caption text-medium-emphasis">No cert PDFs yet.</span>
    </div>
    <v-file-input
      :model-value="certFiles"
      accept=".pdf,application/pdf"
      :label="uploadingCerts ? 'Uploading…' : 'Add cert PDFs — they upload automatically'"
      multiple
      density="compact"
      variant="outlined"
      prepend-icon=""
      prepend-inner-icon="mdi-paperclip"
      hide-details
      :loading="uploadingCerts"
      :disabled="uploadingCerts"
      @update:model-value="onCertFilesPicked"
    />
  </div>
</template>

<script setup lang="ts">
type Certificate = { id: number; fileName: string; sizeBytes: number }
type SupplierOption = { id: number; name: string }

const props = defineProps<{
  /** The pending/accepted line (PendingSupplierQuoteResponse shape). */
  row: any
  /** Accepted line: edits only reach new RFQs. */
  accepted?: boolean
  /** Narrow single-column layout (side panel). */
  compact?: boolean
  /** Line endpoints: PUT {apiBase}/{id}, {apiBase}/{id}/certificates. Default: email quotes. */
  apiBase?: string
  /** Supplier picker search; default {apiBase}/suppliers. */
  suppliersUrl?: string
  /** The supplier is fixed by the caller (Procurement Plus works per supplier). */
  hideSupplier?: boolean
}>()
const base = computed(() => props.apiBase || '/pending-supplier-quotes')
const emit = defineEmits<{
  /** The server's refreshed line after a save / cert change — the parent patches its list row. */
  (e: 'updated', row: any): void
  (e: 'open-certificate', c: Certificate): void
}>()

const CONDITIONS = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']
const CERT_TYPES = ['FAA', 'EASA', 'CAAC', 'Dual', 'MFG COC', 'Vendor COC', 'No Cert']
const SAVE_DELAY_MS = 800

const api = useApi()
const form = ref<any>(null)
const status = ref<'idle' | 'saving' | 'saved' | 'error'>('idle')
const statusText = ref('')
const canRetry = ref(false)
const certFiles = ref<File[]>([])
const uploadingCerts = ref(false)

const sellLeadTimePreview = computed(() => toSellLeadTime(form.value?.leadTime))

// ── Supplier lookup ──
const supplierOptions = ref<SupplierOption[]>([])
const supplierSearchLoading = ref(false)
let supplierSearchTimer: ReturnType<typeof setTimeout> | null = null
function searchSuppliers(q: string) {
  if (supplierSearchTimer) clearTimeout(supplierSearchTimer)
  supplierSearchTimer = setTimeout(async () => {
    supplierSearchLoading.value = true
    try {
      const url = props.suppliersUrl || `${base.value}/suppliers`
      supplierOptions.value = await api.get<SupplierOption[]>(`${url}?q=${encodeURIComponent(q || '')}`)
    } catch {
      supplierOptions.value = []
    } finally {
      supplierSearchLoading.value = false
    }
  }, 250)
}

// ── Autosave ──
// Each line gets its own form object; a save always goes to the object it was scheduled for,
// so switching lines mid-edit still saves the previous line.
const payloadOf = (f: any) => JSON.stringify(toEmailQuoteUpdatePayload(f, f.supplierId ?? null))
const lastSaved = new WeakMap<object, string>()
let timer: ReturnType<typeof setTimeout> | null = null
let scheduledFor: any = null
let inFlight: Promise<void> | null = null

function schedule() {
  if (timer) clearTimeout(timer)
  scheduledFor = form.value
  timer = setTimeout(flush, SAVE_DELAY_MS)
}

async function flush() {
  if (timer) { clearTimeout(timer); timer = null }
  const f = scheduledFor ?? form.value
  scheduledFor = null
  if (!f) return
  // Serialise saves: wait for the running one, then save whatever is newest.
  while (inFlight) await inFlight
  const payload = payloadOf(f)
  if (payload === lastSaved.get(f)) return
  if (!f.partNumber?.trim()) {
    if (f === form.value) { status.value = 'error'; statusText.value = 'Part number is required'; canRetry.value = false }
    return
  }
  inFlight = save(f, payload)
  try { await inFlight } finally { inFlight = null }
}

async function save(f: any, payload: string) {
  if (f === form.value) status.value = 'saving'
  try {
    const updated = await api.put<any>(`${base.value}/${f.id}`, JSON.parse(payload))
    lastSaved.set(f, payload)
    if (updated) emit('updated', updated)
    if (f === form.value) status.value = payloadOf(f) === payload ? 'saved' : 'saving'
  } catch (e: any) {
    if (f === form.value) {
      status.value = 'error'
      statusText.value = e?.data?.message || 'Could not save'
      canRetry.value = true
    }
  }
}

// Fresh copy per line (by id) — later prop updates for the same line (our own `updated` patches)
// don't reset what the user is typing.
watch(() => props.row?.id, () => {
  if (timer && scheduledFor) flush() // finish the previous line first
  const r = props.row
  if (!r) { form.value = null; return }
  const f = {
    ...r,
    customCertReferences: [...(r.customCertReferences ?? [])],
    certificates: [...(r.certificates ?? [])],
  }
  lastSaved.set(f, payloadOf(f))
  form.value = f
  status.value = 'idle'
  certFiles.value = []
  supplierOptions.value = r.supplierId ? [{ id: r.supplierId, name: r.supplierName || '' }] : []
}, { immediate: true })

watch(() => (form.value ? payloadOf(form.value) : ''), (now) => {
  if (form.value && now !== lastSaved.get(form.value)) schedule()
})

onBeforeUnmount(() => { if (timer) flush() })

// ── Cert PDFs (auto-upload) ──

async function onCertFilesPicked(files: File | File[] | null) {
  const list = (Array.isArray(files) ? files : files ? [files] : []).filter(Boolean)
  const f = form.value
  if (!f || !list.length) return
  certFiles.value = list
  const data = new FormData()
  list.forEach(file => data.append('files', file))
  uploadingCerts.value = true
  try {
    const uploaded = await api.post<Certificate[]>(`${base.value}/${f.id}/certificates`, data)
    f.certificates = [...(f.certificates ?? []), ...uploaded]
    emit('updated', { ...props.row, certificates: [...f.certificates] })
  } catch (e: any) {
    status.value = 'error'
    statusText.value = e?.data?.message || 'Upload failed'
    canRetry.value = false
  } finally {
    certFiles.value = []
    uploadingCerts.value = false
  }
}

async function deleteCertificate(c: Certificate) {
  const f = form.value
  if (!f) return
  try {
    await api.del(`${base.value}/${f.id}/certificates/${c.id}`)
    f.certificates = (f.certificates ?? []).filter((x: Certificate) => x.id !== c.id)
    emit('updated', { ...props.row, certificates: [...f.certificates] })
  } catch {
    status.value = 'error'
    statusText.value = 'Could not delete the PDF'
    canRetry.value = false
  }
}

defineExpose({ flush })
</script>

<style scoped>
.eqf-status {
  display: flex;
  align-items: center;
  min-height: 22px;
  margin-bottom: 10px;
  font-size: 12px;
  opacity: 0.85;
}
.eqf-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-bottom: 12px;
}
.eqf-row--3 {
  grid-template-columns: 1fr 1fr 1fr;
}
.eqf--compact .eqf-row--3 {
  grid-template-columns: 1fr 1fr;
}
.eqf--compact .eqf-row--3 > :last-child {
  grid-column: 1 / -1;
}
.eqf-label {
  font-size: 12px;
  font-weight: 700;
  opacity: 0.7;
  margin-bottom: 4px;
}
</style>
