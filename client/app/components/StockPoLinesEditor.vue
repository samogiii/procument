<template>
  <div>
    <div class="d-flex align-center flex-wrap gap-2 mb-2">
      <span class="text-subtitle-2 font-weight-bold">Lines</span>
      <v-chip size="x-small" variant="tonal">{{ modelValue.length }}</v-chip>
      <v-spacer />
      <v-btn size="small" variant="tonal" prepend-icon="mdi-table-arrow-down" :disabled="readonly" @click="showPaste = true">Paste from Excel</v-btn>
      <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-plus" :disabled="readonly" @click="addLine()">Add line</v-btn>
    </div>

    <v-table density="compact" class="lines-table">
      <thead>
        <tr>
          <th style="width: 40px">#</th>
          <th style="min-width: 240px">Part Number</th>
          <th style="width: 120px">Condition</th>
          <th style="width: 110px">Qty</th>
          <th style="width: 150px">Unit Price</th>
          <th class="text-end" style="width: 130px">Line Total</th>
          <th style="width: 48px" />
        </tr>
      </thead>
      <tbody>
        <tr v-for="(line, idx) in modelValue" :key="line.key">
          <td class="text-medium-emphasis">{{ idx + 1 }}</td>
          <td>
            <v-combobox
              :model-value="line.partNumber"
              :items="suggestions[line.key] || []"
              item-title="name"
              :return-object="false"
              :loading="searching === line.key"
              :readonly="readonly"
              placeholder="Type to search or enter a new P/N"
              density="compact"
              variant="outlined"
              hide-details
              hide-no-data
              @update:search="(q: string) => searchParts(line, q)"
              @update:model-value="(v: any) => setPart(line, v)"
            >
              <template #item="{ props: itemProps, item }">
                <v-list-item v-bind="itemProps" :subtitle="item.raw.description || undefined" />
              </template>
            </v-combobox>
            <div v-if="line.partNumber && !line.partNumberId" class="text-caption text-warning mt-1">New P/N — it will be added to the catalog</div>
          </td>
          <td>
            <v-combobox v-model="line.condition" :items="CONDITIONS" :readonly="readonly" density="compact" variant="outlined" hide-details />
          </td>
          <td>
            <v-text-field v-model.number="line.qty" type="number" min="1" step="1" :readonly="readonly" density="compact" variant="outlined" hide-details
              :error="!(line.qty > 0) || !Number.isInteger(Number(line.qty))" />
          </td>
          <td>
            <v-text-field v-model.number="line.unitPrice" type="number" min="0" step="0.01" prefix="$" :readonly="readonly" density="compact" variant="outlined" hide-details
              :error="!(Number(line.unitPrice) >= 0) || String(line.unitPrice) === ''" />
          </td>
          <td class="text-end font-weight-medium">${{ formatPrice((Number(line.qty) || 0) * (Number(line.unitPrice) || 0)) }}</td>
          <td>
            <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error" :disabled="readonly" @click="removeLine(idx)" />
          </td>
        </tr>
        <tr v-if="!modelValue.length">
          <td colspan="7" class="text-center text-medium-emphasis py-4">No lines yet — add one or paste from Excel.</td>
        </tr>
      </tbody>
      <tfoot v-if="modelValue.length">
        <tr>
          <td colspan="3" class="text-end font-weight-bold">Total</td>
          <td class="font-weight-bold">{{ totalQty }}</td>
          <td />
          <td class="text-end font-weight-bold">${{ formatPrice(total) }}</td>
          <td />
        </tr>
      </tfoot>
    </v-table>

    <v-dialog v-model="showPaste" max-width="640">
      <v-card>
        <v-card-title class="pa-4 pb-1">Paste lines from Excel</v-card-title>
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-3">
            Copy the cells from Excel and paste them below — one line per row, columns in this order:
            <strong>P/N · Qty · Unit Price · Condition</strong>. Price and condition are optional; a header row is skipped.
          </p>
          <v-textarea v-model="pasteText" variant="outlined" rows="8" placeholder="ABC-123&#9;2&#9;150&#9;NE" auto-grow class="mono" />
          <v-alert v-if="pastePreview.errors.length" type="warning" variant="tonal" density="compact" class="mt-2">
            <div v-for="err in pastePreview.errors.slice(0, 5)" :key="err">{{ err }}</div>
            <div v-if="pastePreview.errors.length > 5">…and {{ pastePreview.errors.length - 5 }} more</div>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <span class="text-caption text-medium-emphasis">{{ pastePreview.lines.length }} line(s) ready</span>
          <v-spacer />
          <v-btn variant="text" @click="showPaste = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :disabled="!pastePreview.lines.length" :loading="resolvingPaste" @click="applyPaste">Add lines</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import type { StockPoLine } from '~/utils/stockPo'

const props = defineProps<{ modelValue: StockPoLine[]; readonly?: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: StockPoLine[]): void }>()

const api = useApi()
const CONDITIONS = ['NE', 'NS', 'OH', 'SV', 'AR', 'RP', 'FN', 'IN']

const makeLine = makeStockPoLine

const total = computed(() => props.modelValue.reduce((s, l) => s + (Number(l.qty) || 0) * (Number(l.unitPrice) || 0), 0))
const totalQty = computed(() => props.modelValue.reduce((s, l) => s + (Number(l.qty) || 0), 0))

function addLine(partial: Partial<StockPoLine> = {}) {
  emit('update:modelValue', [...props.modelValue, makeLine(partial)])
}
function removeLine(idx: number) {
  emit('update:modelValue', props.modelValue.filter((_, i) => i !== idx))
}

// ── Part number search (catalog) ──
const suggestions = reactive<Record<string, any[]>>({})
const searching = ref<string | null>(null)
const searchTimers: Record<string, any> = {}

function searchParts(line: StockPoLine, q: string) {
  clearTimeout(searchTimers[line.key])
  if (!q || q.trim().length < 2 || q === line.partNumber) return
  searchTimers[line.key] = setTimeout(async () => {
    searching.value = line.key
    try { suggestions[line.key] = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(q.trim())}`) }
    catch { suggestions[line.key] = [] }
    finally { if (searching.value === line.key) searching.value = null }
  }, 250)
}

/** A picked suggestion carries its catalog id; free text becomes a new P/N (resolved server-side). */
function setPart(line: StockPoLine, value: any) {
  const name = (typeof value === 'string' ? value : value?.name ?? '').trim()
  line.partNumber = name
  const match = (suggestions[line.key] || []).find((p: any) => p.name.toUpperCase() === name.toUpperCase())
  line.partNumberId = match?.id ?? null
}

// ── Paste from Excel ──
const showPaste = ref(false)
const pasteText = ref('')
const resolvingPaste = ref(false)

const pastePreview = computed(() => {
  const lines: Partial<StockPoLine>[] = []
  const errors: string[] = []
  // Only trim the end: a leading tab means an empty first cell and must keep the columns aligned.
  const rows = pasteText.value.split(/\r?\n/).map(r => r.replace(/\s+$/, '')).filter(r => r.trim())
  rows.forEach((row, i) => {
    const cells = row.split('\t').map(c => c.trim())
    const [pn, qtyRaw, priceRaw, cond] = cells
    const qty = Number((qtyRaw || '').replace(/,/g, ''))
    const price = priceRaw ? Number(priceRaw.replace(/[$,\s]/g, '')) : 0
    if (i === 0 && (Number.isNaN(qty) || /p\/?n|part/i.test(pn || ''))) return // header row
    if (!pn) { errors.push(`Row ${i + 1}: missing P/N`); return }
    if (!Number.isInteger(qty) || qty <= 0) { errors.push(`Row ${i + 1} (${pn}): quantity "${qtyRaw ?? ''}" is not a whole number above 0`); return }
    if (Number.isNaN(price) || price < 0) { errors.push(`Row ${i + 1} (${pn}): price "${priceRaw}" is not valid`); return }
    lines.push({ partNumber: pn, qty, unitPrice: price, condition: (cond || 'NE').toUpperCase() })
  })
  return { lines, errors }
})

async function applyPaste() {
  resolvingPaste.value = true
  try {
    // Link pasted names to existing catalog parts where the name matches exactly.
    const resolved = await Promise.all(pastePreview.value.lines.map(async (l) => {
      try {
        const hits = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(l.partNumber!)}`)
        const exact = hits.find((p: any) => p.name.toUpperCase() === l.partNumber!.toUpperCase())
        return makeLine({ ...l, partNumberId: exact?.id ?? null, partNumber: exact?.name ?? l.partNumber })
      } catch {
        return makeLine(l)
      }
    }))
    // Replace a single empty starter line instead of keeping it above the pasted rows.
    const keep = props.modelValue.filter(l => l.partNumber)
    emit('update:modelValue', [...keep, ...resolved])
    pasteText.value = ''
    showPaste.value = false
  } finally {
    resolvingPaste.value = false
  }
}
</script>

<style scoped>
.lines-table :deep(td) {
  padding-top: 4px !important;
  padding-bottom: 4px !important;
  vertical-align: top;
}
.mono :deep(textarea) {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12px;
}
</style>
