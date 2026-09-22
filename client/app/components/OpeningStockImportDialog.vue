<template>
  <v-dialog :model-value="modelValue" max-width="1100" scrollable persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center gap-2 pa-4 pb-1">
        <v-icon icon="mdi-database-import-outline" color="primary" />
        Import opening stock
      </v-card-title>
      <v-card-subtitle class="px-4 text-wrap">
        Loads stock you already hold (no Stock PO behind it). Every row is checked first — nothing is saved unless all rows are valid.
      </v-card-subtitle>
      <v-divider class="mt-2" />

      <v-card-text class="pa-4">
        <div class="d-flex flex-wrap align-center gap-2 mb-3">
          <v-btn variant="tonal" size="small" prepend-icon="mdi-download" @click="downloadTemplate">Download template</v-btn>
          <v-file-input v-model="file" accept=".xlsx,.xls,.csv" label="Excel or CSV file" variant="outlined" density="compact" hide-details
            prepend-icon="" prepend-inner-icon="mdi-paperclip" style="max-width: 360px" @update:model-value="parseFile" />
          <v-text-field v-model="reference" label="Reference" variant="outlined" density="compact" hide-details style="max-width: 260px" />
        </div>

        <v-alert v-if="parseError" type="error" variant="tonal" density="compact" class="mb-3">{{ parseError }}</v-alert>
        <v-alert v-if="checked && !errors.length && rows.length" type="success" variant="tonal" density="compact" class="mb-3">
          All {{ rows.length }} rows are valid — {{ fmt(totalUnits) }} units{{ newParts ? `, ${newParts} new part number(s) will be added to the catalog` : '' }}.
        </v-alert>
        <v-alert v-if="errors.length" type="error" variant="tonal" density="compact" class="mb-3">
          {{ errors.length }} row(s) need fixing. Nothing has been saved.
        </v-alert>

        <v-table v-if="rows.length" density="compact" fixed-header height="360">
          <thead>
            <tr>
              <th>#</th><th>P/N</th><th>Cond.</th><th>Warehouse</th><th>Company</th><th class="text-end">Qty</th>
              <th class="text-end">Unit cost</th><th>Cert</th><th>Bin</th><th class="text-end">Min</th><th>Serials</th><th>Problem</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(r, i) in rows" :key="i" :class="{ 'row-error': errorFor(i + 1) }">
              <td class="text-medium-emphasis">{{ i + 1 }}</td>
              <td class="font-weight-medium">{{ r.partNumber }}</td>
              <td>{{ r.condition || 'NE' }}</td>
              <td>{{ r.warehouse }}</td>
              <td>{{ r.companyPreset }}</td>
              <td class="text-end">{{ r.qty }}</td>
              <td class="text-end">{{ r.unitCost }}</td>
              <td>{{ r.certName }}</td>
              <td>{{ r.binLocation }}</td>
              <td class="text-end">{{ r.minQty ?? '' }}</td>
              <td class="text-caption">{{ (r.serials || []).length || '' }}</td>
              <td class="text-error text-caption">{{ errorFor(i + 1) }}</td>
            </tr>
          </tbody>
        </v-table>
        <div v-else class="text-body-2 text-medium-emphasis">
          Columns: <strong>Part Number, Condition, Warehouse, Company, Qty, Unit Cost</strong> (required) and
          Description, Certificate, Tag Date, Bin, Min Qty, Serials (optional; serials separated by commas, one per unit).
        </div>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Close</v-btn>
        <v-btn variant="tonal" color="primary" :loading="busy === 'check'" :disabled="!rows.length || !!busy" @click="submit(true)">Check</v-btn>
        <v-btn variant="flat" color="success" :loading="busy === 'save'" :disabled="!rows.length || !!busy || !checked || !!errors.length" @click="submit(false)">
          Import {{ rows.length || '' }} row(s)
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import * as XLSX from 'xlsx'

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: boolean): void; (e: 'imported', summary: any): void }>()
const api = useApi()

const file = ref<File | File[] | null>(null)
const reference = ref(`Opening stock ${new Date().toISOString().slice(0, 10)}`)
const rows = ref<any[]>([])
const errors = ref<{ row: number; message: string }[]>([])
const checked = ref(false)
const newParts = ref(0)
const parseError = ref('')
const busy = ref<'' | 'check' | 'save'>('')

const fmt = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })
const totalUnits = computed(() => rows.value.reduce((s, r) => s + Number(r.qty || 0), 0))
const errorFor = (row: number) => errors.value.filter(e => e.row === row).map(e => e.message).join('; ')

const HEADERS = ['Part Number', 'Description', 'Condition', 'Warehouse', 'Company', 'Qty', 'Unit Cost', 'Certificate', 'Tag Date', 'Bin', 'Min Qty', 'Serials']

function downloadTemplate() {
  const ws = XLSX.utils.aoa_to_sheet([HEADERS, ['2315M20-3', 'Hydraulic filter', 'NE', 'Main Warehouse', 'Your Company', 4, 42.5, '8130-3', '2026-01-15', 'A-01', 2, '']])
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Opening stock')
  XLSX.writeFile(wb, 'opening-stock-template.xlsx')
}

/** Header names are matched loosely (case, spaces and punctuation ignored). */
const norm = (s: string) => String(s).toLowerCase().replace(/[^a-z0-9]/g, '')
const FIELD: Record<string, string> = {
  partnumber: 'partNumber', pn: 'partNumber', part: 'partNumber', description: 'description', desc: 'description',
  condition: 'condition', cond: 'condition', cd: 'condition', warehouse: 'warehouse', company: 'companyPreset', companypreset: 'companyPreset',
  qty: 'qty', quantity: 'qty', unitcost: 'unitCost', cost: 'unitCost', price: 'unitCost', certificate: 'certName', cert: 'certName',
  tagdate: 'tagDate', bin: 'binLocation', binlocation: 'binLocation', minqty: 'minQty', min: 'minQty', serials: 'serials', serial: 'serials',
}

function toDate(v: any): string | null {
  if (v == null || v === '') return null
  if (v instanceof Date) return v.toISOString().slice(0, 10)
  const d = new Date(v)
  return isNaN(d.getTime()) ? String(v) : d.toISOString().slice(0, 10)
}

async function parseFile() {
  parseError.value = ''
  errors.value = []
  checked.value = false
  rows.value = []
  const f = Array.isArray(file.value) ? file.value[0] : file.value
  if (!f) return
  try {
    const wb = XLSX.read(await f.arrayBuffer(), { cellDates: true })
    const sheet = wb.Sheets[wb.SheetNames[0]!]!
    const raw = XLSX.utils.sheet_to_json<Record<string, any>>(sheet, { defval: '' })
    rows.value = raw
      .map((r) => {
        const out: any = {}
        for (const [k, v] of Object.entries(r)) { const f = FIELD[norm(k)]; if (f) out[f] = typeof v === 'string' ? v.trim() : v }
        return out
      })
      .filter(r => r.partNumber)
      .map(r => ({
        partNumber: String(r.partNumber),
        description: r.description || null,
        condition: r.condition ? String(r.condition).toUpperCase() : null,
        warehouse: r.warehouse ? String(r.warehouse) : null,
        companyPreset: r.companyPreset ? String(r.companyPreset) : null,
        qty: Number(r.qty),
        unitCost: Number(String(r.unitCost ?? '0').replace(/[$,]/g, '')) || 0,
        certName: r.certName || null,
        tagDate: toDate(r.tagDate),
        binLocation: r.binLocation ? String(r.binLocation) : null,
        minQty: r.minQty === '' || r.minQty == null ? null : Number(r.minQty),
        serials: r.serials ? String(r.serials).split(/[,;\n]/).map(s => s.trim()).filter(Boolean) : [],
      }))
    if (!rows.value.length) parseError.value = 'No rows with a part number were found. Use the template headers.'
  } catch {
    parseError.value = 'The file could not be read. Save it as .xlsx or .csv and try again.'
  }
}

async function submit(dryRun: boolean) {
  busy.value = dryRun ? 'check' : 'save'
  try {
    const res = await api.post<any>('/our-inventory/stock/opening', { reference: reference.value, dryRun, lines: rows.value })
    errors.value = []
    checked.value = true
    newParts.value = res.newPartNumbers || 0
    if (!dryRun) {
      emit('imported', res)
      emit('update:modelValue', false)
      rows.value = []
      file.value = null
      checked.value = false
    }
  } catch (e: any) {
    const data = e?.data
    errors.value = data?.errors || []
    checked.value = true
    if (!errors.value.length) parseError.value = data?.message || 'The import failed.'
  } finally {
    busy.value = ''
  }
}

watch(() => props.modelValue, (open) => { if (open) { parseError.value = ''; errors.value = []; checked.value = false } })
</script>

<style scoped>
.row-error td {
  background: rgba(var(--v-theme-error), 0.08);
}
</style>
