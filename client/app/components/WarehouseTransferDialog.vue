<template>
  <v-dialog v-model="model" max-width="1100" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center gap-2 pa-4">
        <v-icon icon="mdi-transfer" color="primary" />
        <span class="text-h6">Transfer Items Between Warehouses</span>
        <v-spacer />
        <v-btn icon="mdi-close" size="small" variant="text" @click="close" />
      </v-card-title>
      <v-divider />

      <v-card-text class="pa-4">
        <!-- Route + track number -->
        <v-row dense>
          <v-col cols="12" md="3">
            <v-autocomplete
              v-model="fromWarehouseId"
              :items="warehouses"
              item-title="name"
              item-value="id"
              label="From Warehouse *"
              variant="outlined"
              density="compact"
              hide-details
              prepend-inner-icon="mdi-warehouse"
              @update:model-value="onFromChange"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-autocomplete
              v-model="toWarehouseId"
              :items="destinationOptions"
              item-title="name"
              item-value="id"
              label="To Warehouse *"
              variant="outlined"
              density="compact"
              hide-details
              prepend-inner-icon="mdi-warehouse-plus"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model="trackNumber"
              label="Track Number *"
              variant="outlined"
              density="compact"
              hide-details
              prepend-inner-icon="mdi-barcode-scan"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model="carrier"
              label="Carrier"
              variant="outlined"
              density="compact"
              hide-details
              prepend-inner-icon="mdi-truck-outline"
            />
          </v-col>
        </v-row>

        <v-textarea
          v-model="notes"
          label="Notes"
          variant="outlined"
          density="compact"
          rows="2"
          hide-details
          class="mt-3"
        />

        <v-divider class="my-4" />

        <!-- Stock picker -->
        <div class="d-flex align-center gap-2 mb-2">
          <span class="text-subtitle-2 font-weight-bold">Available Stock</span>
          <v-chip v-if="selectedCount" size="x-small" color="primary" variant="tonal">
            {{ selectedCount }} selected · {{ selectedQty }} unit{{ selectedQty !== 1 ? 's' : '' }}
          </v-chip>
          <v-spacer />
          <v-text-field
            v-model="stockSearch"
            placeholder="Search part / PO#"
            variant="outlined"
            density="compact"
            hide-details
            clearable
            prepend-inner-icon="mdi-magnify"
            style="max-width:240px;"
          />
        </div>

        <v-progress-linear v-if="loadingStock" indeterminate color="primary" class="mb-2" />

        <v-alert v-if="!fromWarehouseId" type="info" variant="tonal" density="compact">
          Pick a source warehouse to see what can be moved.
        </v-alert>

        <v-alert
          v-else-if="!loadingStock && stock.length === 0"
          type="warning"
          variant="tonal"
          density="compact"
        >
          No transferable stock at this warehouse. Items must be received and accepted, and not already
          assigned to a shipment note.
        </v-alert>

        <v-table v-else-if="filteredStock.length" density="compact" class="border rounded">
          <thead>
            <tr>
              <th style="width:44px;"></th>
              <th>Part Number</th>
              <th>PO#</th>
              <th>Track#</th>
              <th>Customer</th>
              <th class="text-right">Available</th>
              <th class="text-right" style="width:120px;">Qty to Move</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in filteredStock" :key="row.trackNumberItemId">
              <td>
                <v-checkbox-btn
                  :model-value="selected.has(row.trackNumberItemId)"
                  density="compact"
                  @update:model-value="toggle(row)"
                />
              </td>
              <td class="text-body-2 font-weight-medium">
                {{ row.partNumberName || '—' }}
                <div v-if="row.condition" class="text-caption text-medium-emphasis">{{ row.condition }}</div>
              </td>
              <td class="text-caption">{{ row.poNumber || '—' }}</td>
              <td class="text-caption">{{ row.trackNumber }}</td>
              <td class="text-caption">{{ row.customerCode || row.customerName || '—' }}</td>
              <td class="text-right">
                <v-chip size="x-small" color="blue-grey" variant="tonal">{{ row.availableQty }}</v-chip>
              </td>
              <td>
                <v-text-field
                  v-model.number="qtyById[row.trackNumberItemId]"
                  type="number"
                  min="1"
                  :max="row.availableQty"
                  variant="outlined"
                  density="compact"
                  hide-details
                  :disabled="!selected.has(row.trackNumberItemId)"
                  :error="isQtyInvalid(row)"
                />
              </td>
            </tr>
          </tbody>
        </v-table>

        <div v-else-if="stock.length" class="text-center text-medium-emphasis pa-4 text-body-2">
          No stock matches "{{ stockSearch }}".
        </div>

        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">
          {{ error }}
        </v-alert>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-4">
        <span class="text-caption text-medium-emphasis">
          The destination warehouse confirms quantities before the stock becomes available there.
        </span>
        <v-spacer />
        <v-btn variant="text" @click="close">Cancel</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          prepend-icon="mdi-transfer"
          :loading="saving"
          :disabled="!canSubmit"
          @click="submit"
        >
          Create Transfer
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue: boolean
  warehouses: any[]
  /** Preselect a source warehouse when opened from a warehouse-scoped view. */
  defaultFromWarehouseId?: number | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  created: [transfer: any]
}>()

const api = useApi()

const model = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
})

const fromWarehouseId = ref<number | null>(null)
const toWarehouseId = ref<number | null>(null)
const trackNumber = ref('')
const carrier = ref('')
const notes = ref('')

const stock = ref<any[]>([])
const stockSearch = ref('')
const loadingStock = ref(false)
const selected = ref<Set<number>>(new Set())
const qtyById = reactive<Record<number, number>>({})
const saving = ref(false)
const error = ref('')

const destinationOptions = computed(() =>
  props.warehouses.filter(w => w.id !== fromWarehouseId.value))

const filteredStock = computed(() => {
  const q = (stockSearch.value || '').trim().toLowerCase()
  if (!q) return stock.value
  return stock.value.filter(r =>
    (r.partNumberName || '').toLowerCase().includes(q)
    || (r.poNumber || '').toLowerCase().includes(q)
    || (r.trackNumber || '').toLowerCase().includes(q))
})

const selectedRows = computed(() => stock.value.filter(r => selected.value.has(r.trackNumberItemId)))
const selectedCount = computed(() => selectedRows.value.length)
const selectedQty = computed(() =>
  selectedRows.value.reduce((sum, r) => sum + (qtyById[r.trackNumberItemId] || 0), 0))

function isQtyInvalid(row: any) {
  if (!selected.value.has(row.trackNumberItemId)) return false
  const qty = qtyById[row.trackNumberItemId]
  return !qty || qty < 1 || qty > row.availableQty
}

const canSubmit = computed(() =>
  !!fromWarehouseId.value
  && !!toWarehouseId.value
  && fromWarehouseId.value !== toWarehouseId.value
  && !!trackNumber.value.trim()
  && selectedCount.value > 0
  && !selectedRows.value.some(isQtyInvalid))

function toggle(row: any) {
  const next = new Set(selected.value)
  if (next.has(row.trackNumberItemId)) {
    next.delete(row.trackNumberItemId)
  } else {
    next.add(row.trackNumberItemId)
    // Default to moving everything that is on hand.
    if (!qtyById[row.trackNumberItemId]) qtyById[row.trackNumberItemId] = row.availableQty
  }
  selected.value = next
}

function close() {
  model.value = false
}

function onFromChange() {
  if (toWarehouseId.value === fromWarehouseId.value) toWarehouseId.value = null
  selected.value = new Set()
  loadStock()
}

async function loadStock() {
  if (!fromWarehouseId.value) {
    stock.value = []
    return
  }
  loadingStock.value = true
  error.value = ''
  try {
    stock.value = await api.get(`/warehouse-transfers/available?warehouseId=${fromWarehouseId.value}`)
  } catch (e: any) {
    error.value = e?.data?.message || 'Failed to load available stock.'
    stock.value = []
  } finally {
    loadingStock.value = false
  }
}

async function submit() {
  saving.value = true
  error.value = ''
  try {
    const payload = {
      fromWarehouseId: fromWarehouseId.value,
      toWarehouseId: toWarehouseId.value,
      trackNumber: trackNumber.value.trim(),
      carrier: carrier.value.trim() || null,
      notes: notes.value.trim() || null,
      items: selectedRows.value.map(r => ({
        sourceTrackNumberItemId: r.trackNumberItemId,
        qty: qtyById[r.trackNumberItemId],
      })),
    }
    const created = await api.post('/warehouse-transfers', payload)
    emit('created', created)
    close()
  } catch (e: any) {
    error.value = e?.data?.message || 'Failed to create the transfer.'
  } finally {
    saving.value = false
  }
}

function reset() {
  fromWarehouseId.value = props.defaultFromWarehouseId ?? null
  toWarehouseId.value = null
  trackNumber.value = ''
  carrier.value = ''
  notes.value = ''
  stockSearch.value = ''
  selected.value = new Set()
  for (const k of Object.keys(qtyById)) delete qtyById[Number(k)]
  stock.value = []
  error.value = ''
}

watch(model, (open) => {
  if (open) {
    reset()
    if (fromWarehouseId.value) loadStock()
  }
})
</script>
