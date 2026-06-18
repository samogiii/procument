<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="navigateTo(`/ils/quotes/${id}/select-parts`)" />
      <div>
        <h1 class="text-h5 font-weight-bold">{{ quote?.quoteNumber || 'ILS Quote' }}</h1>
        <p class="text-caption text-medium-emphasis mb-0">Step 3 of 3 — Pick serials & set coefficient</p>
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
        :disabled="!selectedLines.length"
        @click="save"
      >
        Save Quote
      </v-btn>
    </div>

    <v-stepper :model-value="3" :items="['Details', 'Select Parts', 'Serials & Pricing']" hide-actions class="mb-4 glass-card" flat />

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-card v-for="part in parts" :key="part.id" class="glass-card mb-4">
      <v-card-title class="d-flex align-center pa-4 pb-2">
        <span class="text-subtitle-1 font-weight-bold" style="font-family: monospace;">{{ part.partNumberName }}</span>
        <v-chip v-if="part.altPartNumber" size="x-small" class="ml-2" color="amber" variant="tonal">{{ part.altPartNumber }}</v-chip>
        <span class="text-caption text-medium-emphasis ml-3">{{ part.description }}</span>
        <v-spacer />
        <span class="text-caption text-medium-emphasis">{{ (serialsByItem[part.id] || []).length }} serial(s)</span>
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-0">
        <table class="serial-table">
          <thead>
            <tr>
              <th style="width: 44px;"></th>
              <th>Serial #</th>
              <th style="width: 110px;">Condition</th>
              <th style="width: 120px;">Base $</th>
              <th style="width: 90px;">Coef</th>
              <th style="width: 130px;">Sell $</th>
              <th style="width: 70px;">Qty</th>
              <th style="width: 130px;">Total</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in lines.filter(l => l.ilsItemId === part.id)" :key="row.serialId" :class="{ 'row-on': row.include }">
              <td class="text-center">
                <v-checkbox-btn v-model="row.include" color="primary" @update:model-value="recalc(row)" />
              </td>
              <td style="font-family: monospace; padding-left: 6px;">{{ row.serialNumber }}</td>
              <td>
                <v-chip v-if="row.condition" size="x-small" variant="tonal" :color="conditionColor(row.condition)">{{ row.condition }}</v-chip>
                <span v-else class="text-medium-emphasis">—</span>
              </td>
              <td><v-text-field v-model.number="row.basePrice" type="number" min="0" step="0.01" density="compact" variant="plain" hide-details prefix="$" :disabled="!row.include" @input="recalcSell(row)" /></td>
              <td><v-text-field v-model.number="row.coef" type="number" min="0" step="0.01" density="compact" variant="plain" hide-details :disabled="!row.include" @input="recalcSell(row)" /></td>
              <td><v-text-field v-model.number="row.sellPrice" type="number" min="0" step="0.01" density="compact" variant="plain" hide-details prefix="$" :disabled="!row.include" @input="recalcTotal(row)" /></td>
              <td><v-text-field v-model.number="row.qty" type="number" min="1" density="compact" variant="plain" hide-details :disabled="!row.include" @input="recalcTotal(row)" /></td>
              <td class="text-right" style="font-family: monospace; padding-right: 10px; color: #4ade80; font-weight: 600;">
                {{ row.include ? '$' + formatPrice(row.totalPrice) : '—' }}
              </td>
            </tr>
            <tr v-if="!(serialsByItem[part.id] || []).length">
              <td colspan="8" class="text-center text-caption text-medium-emphasis py-4">
                No serials recorded for this part. Add serials on its
                <a href="javascript:void(0)" @click="navigateTo(`/ils/items/${part.id}`)">serials page</a>.
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
const route = useRoute()
const api = useApi()
const id = route.params.id as string

const loading = ref(false)
const saving = ref(false)
const quote = ref<any>(null)
const parts = ref<any[]>([])
const serialsByItem = reactive<Record<number, any[]>>({})

interface SerialLine {
  ilsItemId: number
  serialId: number
  serialNumber: string
  condition: string | null
  basePrice: number
  coef: number
  qty: number
  sellPrice: number
  totalPrice: number
  include: boolean
}
const lines = ref<SerialLine[]>([])

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

const selectedLines = computed(() => lines.value.filter(l => l.include))
const grandTotal = computed(() => selectedLines.value.reduce((s, l) => s + (l.totalPrice || 0), 0))

function recalcSell(row: SerialLine) {
  row.sellPrice = Math.round((Number(row.basePrice) || 0) * (Number(row.coef) || 0) * 100) / 100
  recalcTotal(row)
}
function recalcTotal(row: SerialLine) {
  row.totalPrice = Math.round((Number(row.qty) || 0) * (Number(row.sellPrice) || 0) * 100) / 100
}
function recalc(row: SerialLine) {
  if (row.include && !row.sellPrice) recalcSell(row)
}

async function load() {
  loading.value = true
  try {
    const itemIds = String(route.query.items || '')
      .split(',').map(s => Number(s)).filter(n => n > 0)

    const [allItems, q] = await Promise.all([
      api.get<any[]>('/ils'),
      api.get<any>(`/ils-quotes/${id}`),
    ])
    quote.value = q
    parts.value = allItems.filter(i => itemIds.includes(i.id))

    // Existing quote lines keyed by serial id (for Back prefill)
    const existing = new Map<number, any>()
    for (const it of (q.items || [])) {
      if (it.ilsItemSerialId) existing.set(it.ilsItemSerialId, it)
    }

    const built: SerialLine[] = []
    await Promise.all(parts.value.map(async (part) => {
      const serials = await api.get<any[]>(`/ils/${part.id}/serials`)
      serialsByItem[part.id] = serials
      for (const s of serials) {
        const prev = existing.get(s.id)
        const basePrice = prev?.basePrice != null ? Number(prev.basePrice) : (Number(s.price) || 0)
        const coef = prev?.coef != null ? Number(prev.coef) : 1
        const sellPrice = prev ? Number(prev.sellPrice) : Math.round(basePrice * coef * 100) / 100
        const qty = prev ? Number(prev.qty) : 1
        built.push({
          ilsItemId: part.id,
          serialId: s.id,
          serialNumber: s.serialNumber,
          condition: s.condition || part.condition || null,
          basePrice,
          coef,
          qty,
          sellPrice,
          totalPrice: Math.round(qty * sellPrice * 100) / 100,
          include: !!prev,
        })
      }
    }))
    lines.value = built
  } catch {
    showSnack('Failed to load serials', 'error')
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!selectedLines.value.length) return
  saving.value = true
  try {
    const partById = new Map(parts.value.map(p => [p.id, p]))
    const items = selectedLines.value.map(l => {
      const part = partById.get(l.ilsItemId)
      return {
        partNumberId: part.partNumberId,
        partNumberName: part.partNumberName,
        altPartNumber: part.altPartNumber || null,
        condition: l.condition || null,
        certName: part.certName || null,
        qty: Number(l.qty) || 1,
        sellPrice: Number(l.sellPrice) || 0,
        totalPrice: Number(l.totalPrice) || 0,
        leadTime: part.leadTime || null,
        ilsItemId: l.ilsItemId,
        ilsItemSerialId: l.serialId,
        serialNumber: l.serialNumber,
        basePrice: l.basePrice ?? null,
        coef: l.coef ?? null,
      }
    })
    await api.put(`/ils-quotes/${id}`, {
      ilsCustomerId: quote.value.ilsCustomerId,
      rfqReference: quote.value.rfqReference || null,
      notes: quote.value.notes || null,
      billTo: quote.value.billTo || null,
      shipTo: quote.value.shipTo || null,
      items,
    })
    navigateTo(`/ils/quotes/${id}`)
  } catch {
    showSnack('Failed to save quote', 'error')
    saving.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.serial-table {
  width: 100%;
  border-collapse: collapse;
}
.serial-table th {
  background: rgba(var(--v-theme-surface-variant), 0.5);
  padding: 8px 8px;
  text-align: left;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}
.serial-table td {
  padding: 2px 4px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.06);
  vertical-align: middle;
}
.serial-table tr.row-on td {
  background: rgba(var(--v-theme-primary), 0.06);
}
</style>
