<template>
  <div class="create-quote-page">
    <!-- Header -->
    <div class="d-flex align-center mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" :to="`/rfqs/${route.params.id}`" class="mr-2" />
      <div>
        <h1 class="text-h5 font-weight-bold">Add Quote</h1>
        <p class="text-caption text-medium-emphasis mt-1">
          Select supplier prices to include in this customer quote
        </p>
      </div>
    </div>

    <!-- Toolbar -->
    <v-card class="toolbar-card mb-4">
      <div class="d-flex align-center justify-space-between pa-3">
        <div class="d-flex align-center gap-3">
          <v-chip color="primary" variant="tonal" size="small">
            {{ selectedCount }} item{{ selectedCount !== 1 ? 's' : '' }} selected
          </v-chip>
          <span class="text-body-2 text-medium-emphasis" v-if="selectedTotal > 0">
            Total: <strong style="color: #4ade80;">${{ selectedTotal.toFixed(2) }}</strong>
          </span>
        </div>
        <div class="d-flex align-center gap-2">
          <v-text-field
            v-model="validUntil"
            label="Valid Until"
            type="date"
            density="compact"
            hide-details
            variant="outlined"
            style="max-width: 180px;"
          />
          <v-btn
            color="success"
            prepend-icon="mdi-check"
            :disabled="selectedCount === 0"
            :loading="saving"
            @click="createQuote"
          >
            Create Quote
          </v-btn>
        </div>
      </div>
    </v-card>

    <!-- Items with Procurement Records -->
    <v-card class="excel-card">
      <div class="excel-container">
        <table class="excel-grid">
          <thead>
            <tr>
              <th style="width: 44px;"></th>
              <th style="width: 50px;">#</th>
              <th style="width: 160px;">Part Number</th>
              <th>Description</th>
              <th style="width: 70px;">RFQ Qty</th>
              <th style="width: 90px;">RFQ Cond</th>
              <th style="width: 100px;">Suppliers</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(item, idx) in rfqItems" :key="item.id">
              <!-- Master Row -->
              <tr class="master-row" :class="{ 'expanded': expandedRows.has(item.id) }">
                <td class="cell-expand" @click="toggleExpand(item.id)">
                  <v-icon
                    :icon="expandedRows.has(item.id) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                    size="18"
                    :color="expandedRows.has(item.id) ? 'primary' : 'grey'"
                  />
                </td>
                <td class="cell-number">{{ idx + 1 }}</td>
                <td class="cell-pn">{{ item.partNumberName }}</td>
                <td style="padding-left: 12px; font-size: 13px; color: #94a3b8;">{{ item.description || '—' }}</td>
                <td class="text-center" style="font-size: 13px;">{{ item.qty }}</td>
                <td style="padding-left: 12px; font-size: 13px;">{{ item.condition || 'N/A' }}</td>
                <td class="cell-status">
                  <span :class="getRecordCount(item.id) > 0 ? 'text-success' : 'text-medium-emphasis'">
                    {{ getRecordCount(item.id) }} price{{ getRecordCount(item.id) !== 1 ? 's' : '' }}
                  </span>
                </td>
              </tr>

              <!-- Expanded: Procurement Records for this item -->
              <tr v-if="expandedRows.has(item.id)" class="detail-row">
                <td :colspan="7" class="detail-cell">
                  <div class="quote-panel">
                    <div class="quote-header d-flex align-center mb-3">
                      <span class="text-caption text-uppercase font-weight-bold letter-spacing-wide" style="color: #60a5fa;">
                        Available Supplier Prices for {{ item.partNumberName }}
                      </span>
                    </div>

                    <table class="quote-grid" v-if="getItemRecords(item.id).length > 0">
                      <thead>
                        <tr>
                          <th style="width: 40px;"></th>
                          <th>Supplier</th>
                          <th style="width: 80px;">Cond</th>
                          <th style="width: 70px;">Qty</th>
                          <th style="width: 110px;">Cost Price</th>
                          <th style="width: 130px;">Sell Price ($)</th>
                          <th style="width: 130px;">Alt P/N</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr
                          v-for="record in getItemRecords(item.id)"
                          :key="record.id"
                          class="quote-row"
                          :class="{ 'selected-row': selections[record.id]?.selected }"
                        >
                          <td class="text-center">
                            <input
                              type="checkbox"
                              :checked="selections[record.id]?.selected"
                              @change="toggleSelection(record)"
                              class="record-checkbox"
                            />
                          </td>
                          <td style="padding-left: 8px; font-size: 13px;">{{ record.supplierName }}</td>
                          <td style="padding-left: 8px; font-size: 12px;">{{ record.condition || 'N/A' }}</td>
                          <td class="text-center" style="font-size: 13px;">{{ record.qty }}</td>
                          <td style="color: #94a3b8; font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                            ${{ record.price?.toFixed(2) || '0.00' }}
                          </td>
                          <td>
                            <input
                              type="number"
                              class="sell-price-input"
                              :value="selections[record.id]?.sellPrice ?? record.price"
                              step="0.01"
                              min="0"
                              :disabled="!selections[record.id]?.selected"
                              @input="updateSellPrice(record.id, $event)"
                            />
                          </td>
                          <td style="padding-left: 8px; font-size: 12px; color: #fbbf24;">
                            {{ record.alt || '—' }}
                          </td>
                        </tr>
                      </tbody>
                    </table>

                    <div v-else class="empty-records text-center pa-6">
                      <v-icon icon="mdi-package-variant" size="32" color="grey-darken-1" class="mb-2" />
                      <p class="text-caption text-medium-emphasis">
                        No procurement records for this item. Add suppliers on the RFQ page first.
                      </p>
                    </div>
                  </div>
                </td>
              </tr>
            </template>

            <tr v-if="!rfqItems.length && !loading">
              <td :colspan="7" class="text-center pa-8">
                <v-icon icon="mdi-file-document-outline" size="48" color="grey-darken-1" class="mb-3" />
                <p class="text-body-2 text-medium-emphasis">No items in this RFQ</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const router = useRouter()
const api = useApi()

// State
const loading = ref(true)
const saving = ref(false)
const rfqItems = ref<any[]>([])
const procurementRecords = ref<any[]>([])
const expandedRows = ref(new Set<number>())
const validUntil = ref('')

// selections: { [procurementRecordId]: { selected: boolean, sellPrice: number, record: any } }
const selections = ref<Record<number, { selected: boolean; sellPrice: number; record: any }>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const selectedCount = computed(() =>
  Object.values(selections.value).filter(s => s.selected).length
)

const selectedTotal = computed(() =>
  Object.values(selections.value)
    .filter(s => s.selected)
    .reduce((sum, s) => sum + (s.sellPrice * (s.record?.qty || 1)), 0)
)

onMounted(async () => {
  await loadData()
  // Auto-expand all items that have records
  rfqItems.value.forEach(item => {
    if (getRecordCount(item.id) > 0) {
      expandedRows.value.add(item.id)
    }
  })
  expandedRows.value = new Set(expandedRows.value)
})

async function loadData() {
  loading.value = true
  try {
    const [rfqData, records] = await Promise.all([
      api.get<any>(`/rfqs/${route.params.id}`),
      api.get<any[]>(`/rfqs/${route.params.id}/supplier-quotes`)
    ])

    rfqItems.value = (rfqData.items || []).map((i: any) => ({
      id: i.id,
      partNumberName: i.partNumberName,
      partNumberId: i.partNumberId,
      description: i.description,
      qty: i.qty,
      condition: i.condition || ''
    }))

    procurementRecords.value = records || []

    // Initialize selections for each record
    const sel: Record<number, any> = {}
    procurementRecords.value.forEach((r: any) => {
      sel[r.id] = { selected: false, sellPrice: r.price || 0, record: r }
    })
    selections.value = sel
  } catch (e) {
    showSnack('Failed to load data', 'error')
  } finally {
    loading.value = false
  }
}

// ──── Record helpers ────

function getItemRecords(itemId: number) {
  return procurementRecords.value.filter(r => r.rfqItemId === itemId)
}

function getRecordCount(itemId: number) {
  return procurementRecords.value.filter(r => r.rfqItemId === itemId).length
}

function toggleExpand(itemId: number) {
  if (expandedRows.value.has(itemId)) {
    expandedRows.value.delete(itemId)
  } else {
    expandedRows.value.add(itemId)
  }
  expandedRows.value = new Set(expandedRows.value)
}

// ──── Selection logic ────

function toggleSelection(record: any) {
  const current = selections.value[record.id]
  if (current) {
    current.selected = !current.selected
  } else {
    selections.value[record.id] = { selected: true, sellPrice: record.price || 0, record }
  }
  // Trigger reactivity
  selections.value = { ...selections.value }
}

function updateSellPrice(recordId: number, event: Event) {
  const val = parseFloat((event.target as HTMLInputElement).value) || 0
  if (selections.value[recordId]) {
    selections.value[recordId].sellPrice = val
    selections.value = { ...selections.value }
  }
}

// ──── Create Quote ────

async function createQuote() {
  const selectedEntries = Object.values(selections.value).filter(s => s.selected)

  if (selectedEntries.length === 0) {
    showSnack('Please select at least one supplier price', 'warning')
    return
  }

  saving.value = true
  try {
    const items = selectedEntries.map(s => ({
      rfqItemId: s.record.rfqItemId,
      qty: s.record.qty,
      unitPrice: s.sellPrice,
      condition: s.record.condition || null,
      alt: s.record.alt || null,
      leadTimeDays: null
    }))

    await api.post('/quotes', {
      rfqId: Number(route.params.id),
      validUntil: validUntil.value || null,
      items
    })

    showSnack('Quote created successfully', 'success')
    setTimeout(() => {
      router.push(`/rfqs/${route.params.id}`)
    }, 500)
  } catch (e) {
    showSnack('Failed to create quote', 'error')
  } finally {
    saving.value = false
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>

<style scoped>
.create-quote-page {
  max-width: 100%;
}

/* Toolbar */
.toolbar-card {
  background: rgba(30, 41, 59, 0.6) !important;
  border: 1px solid rgba(51, 65, 85, 0.5) !important;
}

/* Excel Card */
.excel-card {
  background: rgba(22, 27, 34, 0.9) !important;
  border: 1px solid rgba(51, 65, 85, 0.6) !important;
  overflow: hidden;
}

.excel-container {
  overflow-x: auto;
}

/* Excel Grid */
.excel-grid {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}

.excel-grid thead th {
  background: rgba(30, 41, 59, 0.8);
  color: #94a3b8;
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 12px;
  border-bottom: 2px solid rgba(51, 65, 85, 0.8);
  text-align: left;
  position: sticky;
  top: 0;
  z-index: 2;
}

.excel-grid tbody td {
  padding: 0 12px;
  height: 42px;
  border-bottom: 1px solid rgba(51, 65, 85, 0.3);
  font-size: 13px;
  vertical-align: middle;
}

/* Master Row */
.master-row {
  transition: background-color 0.15s;
  cursor: default;
}
.master-row:hover {
  background: rgba(30, 41, 59, 0.4);
}
.master-row.expanded {
  background: rgba(30, 41, 59, 0.6);
  border-bottom: none;
}

.cell-expand {
  text-align: center;
  cursor: pointer;
  transition: background-color 0.15s;
}
.cell-expand:hover {
  background: rgba(59, 130, 246, 0.1);
}

.cell-number {
  text-align: center;
  color: #64748b;
  font-size: 12px;
}

.cell-pn {
  color: #60a5fa;
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
}

.cell-status {
  font-size: 12px;
  font-style: italic;
}

/* Detail Row */
.detail-row {
  animation: slideDown 0.2s ease-out;
}
.detail-cell {
  padding: 0 !important;
  background: rgba(15, 23, 42, 0.6);
  border-bottom: 2px solid rgba(59, 130, 246, 0.3) !important;
}

@keyframes slideDown {
  from { opacity: 0; transform: translateY(-8px); }
  to { opacity: 1; transform: translateY(0); }
}

/* Quote Panel */
.quote-panel {
  padding: 16px 20px 16px 56px;
  border-left: 3px solid #3b82f6;
  margin-left: 20px;
}

.letter-spacing-wide {
  letter-spacing: 0.1em;
}

/* Quote Sub-Grid */
.quote-grid {
  width: 100%;
  border-collapse: collapse;
}

.quote-grid thead th {
  color: #64748b;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 6px 8px;
  border-bottom: 1px solid rgba(51, 65, 85, 0.5);
  text-align: left;
}

.quote-grid tbody td {
  padding: 3px 4px;
  border-bottom: 1px solid rgba(51, 65, 85, 0.2);
  height: 38px;
  vertical-align: middle;
}

.quote-row {
  transition: background-color 0.15s;
}
.quote-row:hover {
  background: rgba(51, 65, 85, 0.2);
}
.quote-row.selected-row {
  background: rgba(59, 130, 246, 0.1);
  border-left: 2px solid #3b82f6;
}

/* Checkbox */
.record-checkbox {
  width: 16px;
  height: 16px;
  accent-color: #3b82f6;
  cursor: pointer;
}

/* Sell Price Input */
.sell-price-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: rgba(15, 23, 42, 0.4);
  color: #4ade80;
  padding: 4px 8px;
  font-size: 13px;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  text-align: right;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.sell-price-input:hover:not(:disabled) {
  border-color: rgba(51, 65, 85, 0.6);
}
.sell-price-input:focus {
  background: rgba(15, 23, 42, 0.8);
  border-color: #3b82f6;
  box-shadow: 0 0 0 1px rgba(59, 130, 246, 0.3);
}
.sell-price-input:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.empty-records {
  border: 1px dashed rgba(51, 65, 85, 0.5);
  border-radius: 8px;
}

.text-center { text-align: center; }
</style>
