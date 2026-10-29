<template>
  <!--
    Editable fields of one pending Email RFQs line (side panel of QuoteEmailViewerDialog). Changes save
    themselves (debounced, flushed on blur/line switch/unmount) — same behaviour as EmailQuoteEditForm.
  -->
  <div v-if="form" class="erf">
    <div class="erf-status">
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

    <v-text-field
      v-model="form.partNumber"
      label="Part Number"
      density="compact"
      variant="outlined"
      class="mb-3"
      :error-messages="form.partNumber?.trim() ? '' : 'Required'"
      hide-details="auto"
      @blur="flush"
    />
    <v-combobox
      v-model="form.alternatives"
      label="Alternatives"
      multiple
      chips
      closable-chips
      density="compact"
      variant="outlined"
      class="mb-3"
      hide-details
    />
    <v-textarea v-model="form.description" label="Description" rows="2" auto-grow density="compact" variant="outlined" class="mb-3" hide-details @blur="flush" />
    <div class="erf-row erf-row--3">
      <v-text-field v-model.number="form.qty" type="number" min="0" label="Qty" density="compact" variant="outlined" hide-details @blur="flush" />
      <v-combobox v-model="form.condition" :items="CONDITIONS" label="Condition" density="compact" variant="outlined" hide-details @blur="flush" />
      <v-text-field v-model="form.priority" label="Priority" density="compact" variant="outlined" hide-details @blur="flush" />
    </div>
    <v-textarea v-model="form.remark" label="Remark" rows="2" auto-grow density="compact" variant="outlined" class="mb-3" hide-details @blur="flush" />
    <div class="erf-row">
      <v-text-field
        v-model="form.rfqName"
        label="RFQ name"
        density="compact"
        variant="outlined"
        :error-messages="form.rfqName?.trim() ? '' : 'Required'"
        hide-details="auto"
        @blur="flush"
      />
      <v-text-field
        v-model="form.deadline"
        type="date"
        label="Deadline"
        density="compact"
        variant="outlined"
        hide-details
        :title="row.deadlineRaw ? `In the email: ${row.deadlineRaw}` : 'Empty = 3 days after the email arrived'"
        @blur="flush"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  /** The pending line (PendingRfqItemResponse shape). */
  row: any
}>()
const emit = defineEmits<{
  /** The server's refreshed line after a save — the parent patches its list row. */
  (e: 'updated', row: any): void
}>()

const CONDITIONS = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']
const SAVE_DELAY_MS = 800

const api = useApi()
const form = ref<any>(null)
const status = ref<'idle' | 'saving' | 'saved' | 'error'>('idle')
const statusText = ref('')
const canRetry = ref(false)

// ── Autosave ── (see EmailQuoteEditForm: one form object per line, saves serialised)
const payloadOf = (f: any) => JSON.stringify({
  partNumber: f.partNumber ?? '',
  alternatives: cleanAltList(f.alternatives),
  description: f.description || null,
  qty: f.qty === '' || f.qty == null ? null : Number(f.qty),
  condition: f.condition || null,
  priority: f.priority || null,
  remark: f.remark || null,
  rfqName: f.rfqName ?? '',
  deadline: f.deadline || null,
})
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
  while (inFlight) await inFlight
  const payload = payloadOf(f)
  if (payload === lastSaved.get(f)) return
  if (!f.partNumber?.trim() || !f.rfqName?.trim()) {
    if (f === form.value) {
      status.value = 'error'
      statusText.value = f.partNumber?.trim() ? 'RFQ name is required' : 'Part number is required'
      canRetry.value = false
    }
    return
  }
  inFlight = save(f, payload)
  try { await inFlight } finally { inFlight = null }
}

async function save(f: any, payload: string) {
  if (f === form.value) status.value = 'saving'
  try {
    const updated = await api.put<any>(`/pending-rfqs/${f.id}`, JSON.parse(payload))
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

// Fresh copy per line (by id) — our own `updated` patches for the same line don't reset the typing.
watch(() => props.row?.id, () => {
  if (timer && scheduledFor) flush() // finish the previous line first
  const r = props.row
  if (!r) { form.value = null; return }
  const f = {
    id: r.id,
    partNumber: r.partNumber,
    alternatives: [...(r.alternatives ?? [])],
    description: r.description || '',
    qty: r.qty,
    condition: r.condition || '',
    priority: r.priority || '',
    remark: r.remark || '',
    rfqName: r.rfqName,
    deadline: r.deadline ? String(r.deadline).slice(0, 10) : '',
  }
  lastSaved.set(f, payloadOf(f))
  form.value = f
  status.value = 'idle'
}, { immediate: true })

watch(() => (form.value ? payloadOf(form.value) : ''), (now) => {
  if (form.value && now !== lastSaved.get(form.value)) schedule()
})

onBeforeUnmount(() => { if (timer) flush() })

defineExpose({ flush })
</script>

<style scoped>
.erf-status {
  display: flex;
  align-items: center;
  min-height: 22px;
  margin-bottom: 10px;
  font-size: 12px;
  opacity: 0.85;
}
.erf-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-bottom: 12px;
}
.erf-row--3 {
  grid-template-columns: 1fr 1fr;
}
.erf-row--3 > :last-child {
  grid-column: 1 / -1;
}
</style>
