<template>
  <v-dialog :model-value="modelValue" max-width="1100" scrollable persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center pa-4 pb-2 gap-2">
        <v-icon icon="mdi-truck-check-outline" color="success" />
        Receive into stock — {{ summary?.poNumber || '…' }}
      </v-card-title>
      <v-card-subtitle class="px-4 text-wrap">
        For goods that arrived without a track number. Stock that arrives on a track is booked automatically when the track is accepted.
      </v-card-subtitle>
      <v-divider class="mt-2" />

      <v-card-text class="pa-4">
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
        <v-alert v-else-if="done" type="success" variant="tonal" icon="mdi-check-circle">
          <div class="font-weight-medium">{{ fmtQty(done.qty) }} unit(s) received into stock.</div>
          <div class="text-body-2">{{ done.summary.fullyReceived ? 'Every line of this PO is now received.' : 'The rest of the PO is still open.' }}</div>
          <v-btn class="mt-3" color="primary" variant="flat" size="small" prepend-icon="mdi-file-pdf-box"
            :loading="stockPdf.loading.value === 'grn'" @click="stockPdf.openGrn(poId!, done.movementIds, done.summary.poNumber)">
            Print GRN for this delivery
          </v-btn>
        </v-alert>
        <v-alert v-else-if="summary && !openLines.length" type="success" variant="tonal" density="compact">
          Every line of this PO has been received.
        </v-alert>

        <template v-if="openLines.length && !done">
          <v-row dense class="mb-2">
            <v-col cols="12" md="5">
              <v-select v-model="warehouseId" :items="warehouses" :item-title="(w: any) => w.displayName || w.name" item-value="id"
                label="Received at warehouse" variant="outlined" density="compact" prepend-inner-icon="mdi-warehouse" hide-details />
            </v-col>
            <v-col cols="12" md="7">
              <v-text-field v-model="note" label="Note (optional)" variant="outlined" density="compact" hide-details />
            </v-col>
          </v-row>

          <v-table density="compact" class="receive-table">
            <thead>
              <tr>
                <th style="width: 40px" />
                <th>P/N</th>
                <th class="text-end">Remaining</th>
                <th style="width: 110px">Receive</th>
                <th style="width: 140px">Certificate</th>
                <th style="width: 150px">Tag date</th>
                <th style="width: 110px">Bin</th>
                <th style="min-width: 180px">Serial numbers</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.poItemId">
                <td><v-checkbox-btn v-model="row.selected" density="compact" /></td>
                <td>
                  <div class="font-weight-medium">{{ row.partNumber }}</div>
                  <div class="text-caption text-medium-emphasis">Line {{ row.poRef }} · {{ row.condition }}</div>
                </td>
                <td class="text-end">{{ fmtQty(row.qtyRemaining) }} / {{ row.qtyOrdered }}</td>
                <td>
                  <v-text-field v-model.number="row.qty" type="number" min="0" :max="row.qtyRemaining" step="1" density="compact" variant="outlined"
                    hide-details :disabled="!row.selected" :error="row.selected && !qtyValid(row)" />
                </td>
                <td><v-text-field v-model="row.certName" density="compact" variant="outlined" hide-details :disabled="!row.selected" placeholder="8130-3…" /></td>
                <td><v-text-field v-model="row.tagDate" type="date" density="compact" variant="outlined" hide-details :disabled="!row.selected" /></td>
                <td><v-text-field v-model="row.binLocation" density="compact" variant="outlined" hide-details :disabled="!row.selected" /></td>
                <td>
                  <v-textarea v-model="row.serialsText" rows="1" auto-grow density="compact" variant="outlined" :disabled="!row.selected"
                    placeholder="One per line (optional)" :hint="serialHint(row)" :persistent-hint="!!serialHint(row)"
                    :error="row.selected && !serialsValid(row)" />
                </td>
              </tr>
            </tbody>
          </v-table>
        </template>

        <v-alert v-if="error || stockPdf.error.value" type="error" variant="tonal" density="compact" class="mt-3">{{ error || stockPdf.error.value }}</v-alert>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-4">
        <span v-if="!done" class="text-caption text-medium-emphasis">{{ selectedRows.length }} line(s), {{ fmtQty(selectedQty) }} unit(s)</span>
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Close</v-btn>
        <v-btn v-if="!done" color="success" variant="flat" prepend-icon="mdi-check" :loading="saving" :disabled="!canSubmit" @click="submit">
          Receive
        </v-btn>
      </v-card-actions>
    </v-card>

    <DocPreviewModal
      :open="stockPdf.preview.open.value"
      :blob-url="stockPdf.preview.blobUrl.value"
      :file-name="stockPdf.preview.fileName.value"
      :mime-type="stockPdf.preview.mimeType.value"
      @close="stockPdf.preview.close()"
    />
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{ modelValue: boolean; poId: number | string | null }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: boolean): void; (e: 'received', summary: any): void }>()

const api = useApi()
const stockPdf = useStockPdf()

type Row = {
  poItemId: number; poRef: number; partNumber: string; condition: string; qtyOrdered: number; qtyRemaining: number
  selected: boolean; qty: number; certName: string; tagDate: string; binLocation: string; serialsText: string
}

const summary = ref<any>(null)
const rows = ref<Row[]>([])
const warehouses = ref<any[]>([])
const warehouseId = ref<number | null>(null)
const note = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref('')
/** Set after a successful receipt: the movements it created, so the GRN can cover just this delivery. */
const done = ref<{ summary: any; movementIds: number[]; qty: number } | null>(null)

const openLines = computed(() => (summary.value?.lines || []).filter((l: any) => l.qtyRemaining > 0))
const selectedRows = computed(() => rows.value.filter(r => r.selected && Number(r.qty) > 0))
const selectedQty = computed(() => selectedRows.value.reduce((s, r) => s + Number(r.qty), 0))
const fmtQty = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })

const serialsOf = (r: Row) => r.serialsText.split(/\r?\n|,/).map(s => s.trim()).filter(Boolean)
const qtyValid = (r: Row) => Number(r.qty) > 0 && Number(r.qty) <= r.qtyRemaining && Math.round(Number(r.qty) * 100) === Number(r.qty) * 100
// The API requires one serial per unit when serials are given at all.
const serialsValid = (r: Row) => { const n = serialsOf(r).length; return n === 0 || n === Number(r.qty) }
const serialHint = (r: Row) => { const n = serialsOf(r).length; return n ? `${n} of ${Number(r.qty) || 0}` : '' }
const canSubmit = computed(() => !!warehouseId.value && selectedRows.value.length > 0
  && rows.value.filter(r => r.selected).every(r => qtyValid(r) && serialsValid(r)))

async function load() {
  if (!props.poId) return
  loading.value = true
  error.value = ''
  done.value = null
  try {
    const [s, wh] = await Promise.all([
      api.get<any>(`/our-inventory/purchase-orders/${props.poId}/receipts`),
      warehouses.value.length ? Promise.resolve(warehouses.value) : api.get<any[]>('/warehouses').catch(() => []),
    ])
    summary.value = s
    warehouses.value = wh.filter((w: any) => w.isActive !== false)
    warehouseId.value = s.destinationWarehouseId ?? null
    note.value = ''
    rows.value = s.lines.filter((l: any) => l.qtyRemaining > 0).map((l: any) => ({
      poItemId: l.poItemId, poRef: l.poRef, partNumber: l.partNumber, condition: l.condition,
      qtyOrdered: l.qtyOrdered, qtyRemaining: l.qtyRemaining,
      selected: true, qty: l.qtyRemaining, certName: '', tagDate: '', binLocation: '', serialsText: '',
    }))
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not load the PO lines.'
  } finally {
    loading.value = false
  }
}

const movementIdsOf = (s: any): number[] => (s?.lines || []).flatMap((l: any) => (l.movements || []).map((m: any) => m.id))

async function submit() {
  saving.value = true
  error.value = ''
  const before = new Set(movementIdsOf(summary.value))
  const qty = selectedQty.value
  try {
    const result = await api.post<any>(`/our-inventory/purchase-orders/${props.poId}/receive`, {
      warehouseId: warehouseId.value,
      note: note.value || null,
      lines: selectedRows.value.map(r => ({
        poItemId: r.poItemId,
        qty: Number(r.qty),
        certName: r.certName || null,
        tagDate: r.tagDate || null,
        binLocation: r.binLocation || null,
        serials: serialsOf(r),
      })),
    })
    emit('received', result)
    done.value = { summary: result, movementIds: movementIdsOf(result).filter(id => !before.has(id)), qty }
    summary.value = result
  } catch (e: any) {
    error.value = e?.data?.message || 'Receiving failed.'
  } finally {
    saving.value = false
  }
}

watch(() => [props.modelValue, props.poId], ([open]) => { if (open) load() }, { immediate: true })
</script>

<style scoped>
.receive-table :deep(td) {
  padding-top: 4px !important;
  padding-bottom: 4px !important;
  vertical-align: top;
}
</style>
