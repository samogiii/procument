<template>
  <div>
    <PageHeader :title="editId ? `Edit ${form.poNumber || 'Stock PO'}` : 'New Stock PO'" back-to="/our-inventory/purchase-orders">
      <template #actions>
        <v-btn variant="text" @click="$router.back()">Cancel</v-btn>
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-content-save-outline" :loading="saving === 'draft'" :disabled="!!saving" @click="save(false)">
          Save draft
        </v-btn>
        <v-btn variant="flat" color="success" prepend-icon="mdi-send" :loading="saving === 'submit'" :disabled="!!saving" @click="save(true)">
          Save &amp; submit for approval
        </v-btn>
      </template>
    </PageHeader>

    <v-alert v-if="loadError" type="error" variant="tonal" class="mb-4">{{ loadError }}</v-alert>

    <v-card class="glass-card mb-4">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-cart-arrow-down" class="mr-2" size="20" color="primary" />
        Order details
      </v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="4">
            <v-autocomplete
              v-model="form.supplierId"
              v-model:search="supplierSearch"
              :items="supplierItems"
              item-title="name"
              item-value="id"
              label="Supplier *"
              placeholder="Type to search approved suppliers"
              prepend-inner-icon="mdi-truck-outline"
              variant="outlined"
              density="compact"
              :loading="supplierLoading"
              no-filter
              hide-no-data
              :error-messages="errors.supplierId"
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-select
              v-model="form.companyPresetId"
              :items="presets"
              item-title="name"
              item-value="id"
              label="Buying company *"
              prepend-inner-icon="mdi-domain"
              variant="outlined"
              density="compact"
              :loading="presetsLoading"
              :error-messages="errors.companyPresetId"
              hint="Decides the paying wallet and the PO / PR branding"
              persistent-hint
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-select
              v-model="form.destinationWarehouseId"
              :items="warehouses"
              :item-title="(w: any) => w.displayName || w.name"
              item-value="id"
              label="Destination warehouse *"
              prepend-inner-icon="mdi-warehouse"
              variant="outlined"
              density="compact"
              :loading="warehousesLoading"
              :disabled="!form.companyPresetId"
              :error-messages="errors.destinationWarehouseId"
              :hint="warehousesLinked ? 'Warehouses linked to this company' : 'All active warehouses'"
              persistent-hint
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="form.subject" label="Subject" variant="outlined" density="compact" prepend-inner-icon="mdi-text-box-outline" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="form.supplierPIRef" label="Supplier PI # (optional)" variant="outlined" density="compact" prepend-inner-icon="mdi-pound" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="form.expectedDeliveryDate" type="date" label="Expected delivery (optional)" variant="outlined" density="compact" />
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-card class="glass-card mb-4">
      <v-card-text>
        <div class="d-flex justify-end mb-2">
          <v-btn size="small" variant="text" color="warning" prepend-icon="mdi-arrow-down-bold-circle-outline"
            :loading="suggesting" :disabled="!form.companyPresetId" @click="fillFromLowStock">
            Fill from low stock
          </v-btn>
        </div>
        <StockPoLinesEditor v-model="lines" />
        <div v-if="errors.lines" class="text-error text-caption mt-2">{{ errors.lines }}</div>
      </v-card-text>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000" location="bottom end">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { StockPoLine } from '~/utils/stockPo'

const api = useApi()
const route = useRoute()
const router = useRouter()

const editId = computed(() => (route.query.id ? Number(route.query.id) : null))

const form = reactive({
  poNumber: '',
  supplierId: null as number | null,
  companyPresetId: null as number | null,
  destinationWarehouseId: null as number | null,
  subject: '',
  supplierPIRef: '',
  expectedDeliveryDate: '',
})
const lines = ref<StockPoLine[]>([makeStockPoLine()])
const errors = reactive<Record<string, string>>({})
const saving = ref<'' | 'draft' | 'submit'>('')
const loadError = ref('')

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function notify(text: string, color = 'success') { snackbarText.value = text; snackbarColor.value = color; snackbar.value = true }

// ── Supplier search (approved catalog suppliers only — the API rejects others) ──
const supplierSearch = ref('')
const supplierResults = ref<any[]>([])
const selectedSupplier = ref<any | null>(null)
const supplierLoading = ref(false)
let supplierTimer: any = null
const supplierItems = computed(() => {
  const list = supplierResults.value.filter((s: any) => s.status === 'Approved')
  return selectedSupplier.value && !list.some((s: any) => s.id === selectedSupplier.value.id) ? [selectedSupplier.value, ...list] : list
})
watch(supplierSearch, (q) => {
  clearTimeout(supplierTimer)
  if (!q || q.length < 1 || q === selectedSupplier.value?.name) return
  supplierTimer = setTimeout(async () => {
    supplierLoading.value = true
    try { supplierResults.value = await api.get<any[]>(`/suppliers/search?q=${encodeURIComponent(q)}`) }
    catch { supplierResults.value = [] }
    finally { supplierLoading.value = false }
  }, 250)
})
watch(() => form.supplierId, (id) => {
  const hit = supplierResults.value.find((s: any) => s.id === id)
  if (hit) selectedSupplier.value = hit
})

// ── Company presets & warehouses ──
const presets = ref<any[]>([])
const presetsLoading = ref(false)
const warehouses = ref<any[]>([])
const warehousesLoading = ref(false)
const warehousesLinked = ref(false)

async function loadPresets() {
  presetsLoading.value = true
  try { presets.value = (await api.get<any[]>('/companypresets')).filter((p: any) => p.isActive !== false) }
  catch { presets.value = [] }
  finally { presetsLoading.value = false }
}

/** A company with linked warehouses may only ship to those (the API enforces the same rule). */
async function loadWarehouses(presetId: number | null) {
  warehouses.value = []
  if (!presetId) return
  warehousesLoading.value = true
  try {
    const linked = await api.get<any[]>(`/companypresets/${presetId}/warehouses`).catch(() => [])
    warehousesLinked.value = linked.length > 0
    warehouses.value = (linked.length ? linked : await api.get<any[]>('/warehouses')).filter((w: any) => w.isActive !== false)
    if (form.destinationWarehouseId && !warehouses.value.some((w: any) => w.id === form.destinationWarehouseId))
      form.destinationWarehouseId = null
    if (!form.destinationWarehouseId && warehouses.value.length === 1) form.destinationWarehouseId = warehouses.value[0].id
  } finally {
    warehousesLoading.value = false
  }
}
watch(() => form.companyPresetId, loadWarehouses)

// ── Low-stock suggestions ──
const suggesting = ref(false)
async function fillFromLowStock() {
  suggesting.value = true
  try {
    const params = new URLSearchParams({ companyPresetId: String(form.companyPresetId) })
    if (form.destinationWarehouseId) params.set('warehouseId', String(form.destinationWarehouseId))
    const rows = await api.get<any[]>(`/our-inventory/purchase-orders/suggest?${params}`)
    if (!rows.length) { notify('Nothing is below its minimum quantity for this company.', 'info'); return }
    const existing = new Set(lines.value.map(l => `${l.partNumberId}|${l.condition}`))
    const added = rows
      .filter(r => !existing.has(`${r.partNumberId}|${r.condition}`))
      .map(r => makeStockPoLine({ partNumberId: r.partNumberId, partNumber: r.partNumber, qty: r.qty, unitPrice: r.suggestedUnitPrice || 0, condition: r.condition }))
    if (!added.length) { notify('Every low-stock part is already on this PO.', 'info'); return }
    lines.value = [...lines.value.filter(l => l.partNumber), ...added]
    notify(`${added.length} low-stock line(s) added`)
  } catch (e: any) {
    notify(e?.data?.message || 'Could not load suggestions', 'error')
  } finally {
    suggesting.value = false
  }
}

// ── Save ──
function validate(): boolean {
  Object.keys(errors).forEach(k => delete errors[k])
  if (!form.supplierId) errors.supplierId = 'Choose a supplier'
  if (!form.companyPresetId) errors.companyPresetId = 'Choose the buying company'
  if (!form.destinationWarehouseId) errors.destinationWarehouseId = 'Choose where the stock should arrive'
  const filled = lines.value.filter(l => l.partNumber?.trim())
  if (!filled.length) errors.lines = 'Add at least one line'
  else if (filled.some(l => !(Number(l.qty) > 0) || !Number.isInteger(Number(l.qty)) || !(Number(l.unitPrice) >= 0)))
    errors.lines = 'Every line needs a whole quantity above 0 and a price of 0 or more'
  return Object.keys(errors).length === 0
}

async function save(submit: boolean) {
  if (!validate()) return
  saving.value = submit ? 'submit' : 'draft'
  const payload = {
    supplierId: form.supplierId,
    companyPresetId: form.companyPresetId,
    destinationWarehouseId: form.destinationWarehouseId,
    subject: form.subject || null,
    supplierPIRef: form.supplierPIRef || null,
    expectedDeliveryDate: form.expectedDeliveryDate || null,
    items: lines.value.filter(l => l.partNumber?.trim()).map(l => ({
      partNumberId: l.partNumberId,
      partNumber: l.partNumberId ? null : l.partNumber.trim(),
      qty: Number(l.qty),
      unitPrice: Number(l.unitPrice),
      condition: l.condition || null,
    })),
  }
  try {
    const saved = editId.value
      ? await api.put<any>(`/our-inventory/purchase-orders/${editId.value}`, payload)
      : await api.post<any>('/our-inventory/purchase-orders', payload)
    if (submit) await api.post(`/our-inventory/purchase-orders/${saved.id}/submit`, {})
    if (!saved.preferredWalletId)
      notify('Saved. This company has no wallet yet, so choose one when paying.', 'warning')
    await router.push(`/purchase-orders/${saved.id}`)
  } catch (e: any) {
    notify(e?.data?.message || 'Could not save the Stock PO', 'error')
  } finally {
    saving.value = ''
  }
}

// ── Load for edit ──
async function loadExisting(id: number) {
  try {
    const po = await api.get<any>(`/our-inventory/purchase-orders/${id}`)
    if (po.status !== 'Draft') {
      loadError.value = `${po.poNumber} is ${po.status}; only drafts can be edited.`
      return
    }
    form.poNumber = po.poNumber
    selectedSupplier.value = { id: po.supplierId, name: po.supplierName, status: 'Approved' }
    form.supplierId = po.supplierId
    form.companyPresetId = po.companyPresetId
    form.destinationWarehouseId = po.destinationWarehouseId
    form.subject = po.subject || ''
    form.supplierPIRef = po.supplierPIRef || ''
    form.expectedDeliveryDate = po.expectedDeliveryDate ? String(po.expectedDeliveryDate).slice(0, 10) : ''
    lines.value = (po.items || []).map((i: any) => makeStockPoLine({
      partNumberId: i.partNumberId, partNumber: i.partNumber, qty: i.qty, unitPrice: i.unitPrice, condition: i.condition,
    }))
  } catch {
    loadError.value = 'Stock PO not found.'
  }
}

onMounted(async () => {
  await loadPresets()
  if (editId.value) await loadExisting(editId.value)
})
</script>
