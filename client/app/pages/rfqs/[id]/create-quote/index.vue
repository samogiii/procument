<template>
  <div class="create-quote-page">
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" :to="backUrl" class="mr-1 flex-shrink-0" size="small" />
      <div class="min-width-0">
        <h1 class="text-h6 text-sm-h5 font-weight-bold">{{ isEditMode ? 'Edit Quote' : 'Add Quote' }}</h1>
        <p class="text-caption text-medium-emphasis mt-1">
          {{ isEditMode ? 'Modify selected items and pricing for this quote' : 'Select supplier prices to include in this customer quote' }}
        </p>
      </div>
      <v-spacer />
      <v-chip v-if="isEditMode" color="warning" variant="tonal" size="small" prepend-icon="mdi-pencil">
        Editing Quote #{{ editQuoteId }}
      </v-chip>
    </div>

    <!-- Toolbar -->
    <v-card class="toolbar-card mb-4">
      <div class="d-flex flex-wrap align-center justify-space-between pa-3 gap-2">
        <div class="d-flex align-center gap-2">
          <v-chip color="primary" variant="tonal" size="small">
            {{ selectedCount }} item{{ selectedCount !== 1 ? 's' : '' }} selected
          </v-chip>
          <span class="text-body-2 text-medium-emphasis" v-if="selectedTotal > 0">
            Total: <strong style="color: #4ade80;">${{ selectedTotal.toFixed(2) }}</strong>
          </span>
        </div>
        <div class="d-flex flex-wrap align-center gap-2">
          <v-text-field
            v-model="validUntil"
            label="Valid Until"
            type="date"
            density="compact"
            hide-details
            variant="outlined"
            style="min-width: 150px; max-width: 180px;"
          />
          <v-btn
            color="success"
            prepend-icon="mdi-check"
            :disabled="selectedCount === 0"
            :loading="saving"
            @click="saveQuote"
          >
            {{ isEditMode ? 'Update' : 'Create' }}
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

                    <div class="quote-grid-scroll" v-if="getItemRecords(item.id).length > 0">
                    <table class="quote-grid">
                      <thead>
                        <tr>
                          <th style="width: 40px;"></th>
                          <th style="min-width: 80px;">Supplier</th>
                          <th style="width: 130px;">Alt P/N</th>
                          <th style="width: 80px;">Cond</th>
                          <th style="width: 70px;">Qty</th>
                          <th style="width: 110px;">Buyer Price</th>
                          <th style="width: 110px;">Shipping Cost</th>
                          <th style="width: 75px;">Coef 1</th>
                          <th style="width: 75px;">Coef 2</th>
                          <th style="width: 75px;">Coef 3</th>
                          <th style="width: 110px;">Unit Price</th>
                          <th style="width: 110px;">Total Price</th>
                          <!-- <th style="width: 130px;">Sell Price ($)</th> -->
                          
                        </tr>
                      </thead>
                      <tbody>
                        <tr
                          v-for="record in getItemRecords(item.id)"
                          :key="record.id"
                          class="quote-row"
                          :class="{ 'selected-row': selections[record.id] }"
                        >
                          <td class="text-center">
                            <input
                              type="checkbox"
                              :checked="selections[record.id]"
                              @change="toggleSelection(record)"
                              class="record-checkbox"
                            />
                          </td>
                          <td style="padding-left: 8px; font-size: 13px;">{{ record.supplierName }}</td>
                          <td style="padding-left: 8px; font-size: 12px; color: #fbbf24;">
                            {{ record.alt || '—' }}
                          </td>
                          <td style="padding-left: 8px; font-size: 12px;">{{ record.condition || 'N/A' }}</td>
                          <td class="text-center" style="font-size: 13px;">{{ record.qty }}</td>
                          <td style="color: #94a3b8; font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                            ${{ record.price?.toFixed(2) || '0.00' }}
                          </td>
                          <td style="color: #94a3b8; font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                            ${{ (record.shippingCost ?? 0).toFixed(2) }}
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input"
                              placeholder="1"
                              v-model.number="record.coef_1"
                              step="0.01"
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input"
                              placeholder="1"
                              v-model.number="record.coef_2"
                              step="0.01"
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input"
                              placeholder="1"
                              v-model.number="record.coef_3"
                              step="0.01"
                            />
                          </td>
                          <td class="computed-cell">
                            ${{ calcUnitPrice(record).toFixed(2) }}
                          </td>
                          <td class="computed-cell total-cell">
                            ${{ calcTotalPrice(record).toFixed(2) }}
                          </td>
                          <!-- <td>
                            <input
                              type="number"
                              class="sell-price-input"
                              :value="selections[record.id]?.sellPrice ?? calcUnitPrice(record)"
                              step="0.01"
                              min="0"
                              :disabled="!selections[record.id]?.selected"
                              @input="updateSellPrice(record.id, $event)"
                            />
                          </td> -->
                          
                        </tr>
                      </tbody>
                    </table>
                    </div>

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

// selections: simple map of recordId → selected boolean
const selections = ref<Record<number, boolean>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// Get all selected records from the live procurementRecords array
const selectedRecords = computed(() =>
  procurementRecords.value.filter(r => selections.value[r.id])
)

const selectedCount = computed(() => selectedRecords.value.length)

const selectedTotal = computed(() =>
  selectedRecords.value.reduce((sum, r) => sum + calcTotalPrice(r), 0)
)

// Edit mode
const editQuoteId = computed(() => (route.query.editQuoteId as string) || null)
const isEditMode = computed(() => !!editQuoteId.value)
const existingQuote = ref<any>(null)

const backUrl = computed(() =>
  isEditMode.value && editQuoteId.value
    ? `/quotes/${editQuoteId.value}`
    : `/rfqs/${route.params.id}`
)

onMounted(async () => {
  await loadData()

  // If editing, load existing quote and pre-select items
  if (editQuoteId.value) {
    await loadExistingQuote()
  }

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

    procurementRecords.value = (records || []).map((r: any) => ({
      ...r,
      coef_1: r.coef_1 ?? 1,
      coef_2: r.coef_2 ?? 1,
      coef_3: r.coef_3 ?? 1,
    }))

    // Initialize all selections to false
    const sel: Record<number, boolean> = {}
    procurementRecords.value.forEach((r: any) => {
      sel[r.id] = false
    })
    selections.value = sel
  } catch (e) {
    showSnack('Failed to load data', 'error')
  } finally {
    loading.value = false
  }
}

// ──── Calculation helpers ────

function calcUnitPrice(q: any): number {
  const price = Number(q.price) || 0
  const shipping = Number(q.shippingCost) || 0
  const qty = Number(q.qty) || 1
  const c1 = Number(q.coef_1) || 1
  const c2 = Number(q.coef_2) || 1
  const c3 = Number(q.coef_3) || 1
  return (price + (shipping / qty)) * c1 * c2 * c3
}

function calcTotalPrice(q: any): number {
  const qty = Number(q.qty) || 1
  return calcUnitPrice(q) * qty
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
  selections.value[record.id] = !selections.value[record.id]
  selections.value = { ...selections.value }
}

// ──── Load existing quote for edit ────

async function loadExistingQuote() {
  try {
    existingQuote.value = await api.get<any>(`/quotes/${editQuoteId.value}`)
    const eq = existingQuote.value

    // Pre-fill validUntil
    if (eq.validUntil) {
      validUntil.value = new Date(eq.validUntil).toISOString().split('T')[0] as string
    }

    // Pre-select procurement records that match quote items
    if (eq.items && Array.isArray(eq.items)) {
      for (const qi of eq.items) {
        // First try to match by procumentRecordId (exact match)
        if (qi.procumentRecordId) {
          const exactMatch = procurementRecords.value.find(
            (r: any) => r.id === qi.procumentRecordId
          )
          if (exactMatch) {
            selections.value[exactMatch.id] = true
            continue
          }
        }

        // Fallback: match by rfqItemId (pick first unselected record)
        const matchingRecords = procurementRecords.value.filter(
          (r: any) => r.rfqItemId === qi.rfqItemId && !selections.value[r.id]
        )
        if (matchingRecords.length > 0) {
          selections.value[matchingRecords[0].id] = true
        }
      }
      selections.value = { ...selections.value }
    }
  } catch {
    showSnack('Failed to load existing quote for editing', 'error')
  }
}

// ──── Save Quote (Create or Update) ────

async function saveQuote() {
  const selected = selectedRecords.value

  if (selected.length === 0) {
    showSnack('Please select at least one supplier price', 'warning')
    return
  }

  saving.value = true
  try {
    // 1. Save coefs/unitPrice/totalPrice back to procurement records
    const quotesToUpdate = selected.map(r => ({
      id: r.id,
      rfqItemId: r.rfqItemId,
      supplierName: r.supplierName,
      qty: r.qty,
      price: r.price,
      condition: r.condition,
      alt: r.alt,
      certName: r.certName || null,
      tagDate: r.tagDate || null,
      shippingCost: r.shippingCost ?? null,
      shippingPoint: r.shippingPoint || null,
      coef_1: r.coef_1 ?? 1,
      coef_2: r.coef_2 ?? 1,
      coef_3: r.coef_3 ?? 1,
      unitPrice: calcUnitPrice(r),
      totalPrice: calcTotalPrice(r),
    }))

    if (quotesToUpdate.length > 0) {
      await api.post(
        `/rfqs/${route.params.id}/supplier-quotes/bulk`,
        { quotes: quotesToUpdate }
      )
    }

    // 2. Create/update the sales quote
    const items = selected.map(r => ({
      rfqItemId: r.rfqItemId,
      procumentRecordId: r.id,
      qty: r.qty,
      unitPrice: calcUnitPrice(r),
      condition: r.condition || null,
      alt: r.alt || null,
      leadTimeDays: null
    }))

    const payload = {
      rfqId: Number(route.params.id),
      validUntil: validUntil.value || null,
      items
    }

    if (isEditMode.value) {
      await api.put(`/quotes/${editQuoteId.value}`, payload)
      showSnack('Quote updated successfully', 'success')
      setTimeout(() => {
        router.push(`/quotes/${editQuoteId.value}`)
      }, 500)
    } else {
      await api.post('/quotes', payload)
      showSnack('Quote created successfully', 'success')
      setTimeout(() => {
        router.push(`/rfqs/${route.params.id}`)
      }, 500)
    }
  } catch {
    showSnack(isEditMode.value ? 'Failed to update quote' : 'Failed to create quote', 'error')
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
  min-width: 900px;
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
  white-space: nowrap;
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

.quote-grid-scroll {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.computed-cell {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
  color: #94a3b8;
  text-align: right;
  padding-right: 12px !important;
  white-space: nowrap;
}
.total-cell {
  color: #4ade80;
  font-weight: 600;
}

.coef-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: rgba(15, 23, 42, 0.4);
  color: #e2e8f0;
  padding: 4px 6px;
  font-size: 12px;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  text-align: center;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.coef-input:hover {
  border-color: rgba(51, 65, 85, 0.6);
}
.coef-input:focus {
  background: rgba(15, 23, 42, 0.8);
  border-color: #3b82f6;
  box-shadow: 0 0 0 1px rgba(59, 130, 246, 0.3);
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
