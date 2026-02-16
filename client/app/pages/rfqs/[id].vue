<template>
  <div class="rfq-single-view">
    <!-- Header Bar -->
    <div class="d-flex align-center mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" to="/rfqs" class="mr-2" />
      <div>
        <h1 class="text-h5 font-weight-bold d-flex align-center gap-2">
          RFQ #{{ route.params.id }}
          <v-chip :color="statusColor" size="small" class="ml-2">{{ rfq.status || 'Open' }}</v-chip>
        </h1>
        <p class="text-caption text-medium-emphasis mt-1" v-if="rfq.name">{{ rfq.name }}</p>
      </div>
    </div>

    <!-- RFQ Info Cards -->
    <v-row class="mb-5" dense>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="primary" variant="tonal" size="40">
              <v-icon icon="mdi-domain" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Customer</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.customerName || '—' }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="info" variant="tonal" size="40">
              <v-icon icon="mdi-clock-outline" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Lead Time</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.leadTime ? new Date(rfq.leadTime).toLocaleDateString() : '—' }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="success" variant="tonal" size="40">
              <v-icon icon="mdi-account-outline" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Assigned To</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.userName || '—' }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="warning" variant="tonal" size="40">
              <v-icon icon="mdi-package-variant" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Total Items</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.items?.length || 0 }} parts</p>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Excel-Style Quoting Table -->
    <v-card class="excel-card">
      <div class="excel-toolbar d-flex align-center justify-space-between pa-3">
        <div class="d-flex align-center gap-2">
          <v-icon icon="mdi-table" size="18" color="primary" />
          <span class="text-body-2 font-weight-bold">Procurement Records</span>
          <v-chip size="x-small" color="primary" variant="tonal" class="ml-1">
            {{ totalQuotes }} records
          </v-chip>
        </div>
        <div class="d-flex gap-2">
          <v-btn
            size="small"
            variant="tonal"
            color="primary"
            prepend-icon="mdi-content-save"
            :loading="saving"
            @click="saveAll"
          >
            Save All
          </v-btn>
        </div>
      </div>

      <div class="excel-container">
        <table class="excel-grid">
          <thead>
            <tr>
              <th style="width: 44px; text-align: center;"></th>
              <th style="width: 50px; text-align: center;">#</th>
              <th style="width: 180px;">Part Number</th>
              <th style="width: 180px;">Description</th>
              <th>Alt P/N</th>
              <th style="width: 80px;">Qty</th>
              <th style="width: 100px;">Condition</th>
              <th style="width: 120px;">Procurements</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(item, idx) in editableItems" :key="item.id">
              <!-- Master Row — editable fields -->
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
                <td class="cell-pn">{{ item.description }}</td>
                <td>
                  <input
                    type="text"
                    class="item-input alt-input"
                    placeholder="—"
                    v-model="item.alt"
                  />
                </td>
                <td>
                  <input
                    type="number"
                    class="item-input text-center"
                    v-model.number="item.qty"
                    min="1"
                  />
                </td>
                <td>
                  <select class="item-input item-select" v-model="item.condition">
                    <option value="">N/A</option>
                    <option value="NE">NE</option>
                    <option value="OH">OH</option>
                    <option value="SV">SV</option>
                    <option value="AR">AR</option>
                  </select>
                </td>
                <td class="cell-status">
                  <span :class="getQuoteCount(item.id) > 0 ? 'text-success' : 'text-medium-emphasis'">
                    {{ getQuoteCount(item.id) }} supplier{{ getQuoteCount(item.id) !== 1 ? 's' : '' }}
                  </span>
                </td>
              </tr>

              <!-- Expanded Detail Row -->
              <tr v-if="expandedRows.has(item.id)" class="detail-row">
                <td :colspan="7" class="detail-cell">
                  <div class="quote-panel">
                    <div class="quote-header d-flex align-center justify-space-between mb-3">
                      <span class="text-caption text-uppercase font-weight-bold letter-spacing-wide" style="color: #60a5fa;">
                        Supplier Quotes for {{ item.partNumberName }}
                      </span>
                      <v-btn
                        size="x-small"
                        color="primary"
                        variant="flat"
                        prepend-icon="mdi-plus"
                        @click="addQuoteRow(item.id)"
                      >
                        Add Supplier
                      </v-btn>
                    </div>

                    <table class="quote-grid" v-if="getItemQuotes(item.id).length > 0">
                      <thead>
                        <tr>
                          <th>Supplier Name</th>
                          <th style="width: 90px;">Condition</th>
                          <th style="width: 80px;">Qty</th>
                          <th style="width: 120px;">Unit Price ($)</th>
                          <th style="width: 150px;">Alt P/N</th>
                          <th style="width: 44px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="(quote, qIdx) in getItemQuotes(item.id)" :key="qIdx" class="quote-row">
                          <td>
                            <input
                              type="text"
                              class="quote-input"
                              placeholder="Supplier name..."
                              v-model="quote.supplierName"
                            />
                          </td>
                          <td>
                            <select class="quote-input quote-select" v-model="quote.condition">
                              <option value="NE">NE</option>
                              <option value="OH">OH</option>
                              <option value="SV">SV</option>
                              <option value="AR">AR</option>
                            </select>
                          </td>
                          <td>
                            <input
                              type="number"
                              class="quote-input text-center"
                              v-model.number="quote.qty"
                              min="1"
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              class="quote-input price-input"
                              placeholder="0.00"
                              v-model.number="quote.price"
                              step="0.01"
                              min="0"
                            />
                          </td>
                          <td>
                            <input
                              type="text"
                              class="quote-input alt-input"
                              placeholder="Same P/N"
                              v-model="quote.alt"
                            />
                          </td>
                          <td class="text-center">
                            <v-btn
                              icon="mdi-close"
                              size="x-small"
                              variant="text"
                              color="error"
                              @click="removeQuote(item.id, qIdx)"
                            />
                          </td>
                        </tr>
                      </tbody>
                    </table>

                    <div v-else class="empty-quotes text-center pa-6">
                      <v-icon icon="mdi-package-variant" size="32" color="grey-darken-1" class="mb-2" />
                      <p class="text-caption text-medium-emphasis">No supplier quotes yet. Click "Add Supplier" to start.</p>
                    </div>
                  </div>
                </td>
              </tr>
            </template>

            <!-- Empty State -->
            <tr v-if="!editableItems.length && !loading">
              <td :colspan="7" class="text-center pa-8">
                <v-icon icon="mdi-file-document-outline" size="48" color="grey-darken-1" class="mb-3" />
                <p class="text-body-2 text-medium-emphasis">No items in this RFQ</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </v-card>

    <!-- Snackbar -->
    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()

// State
const rfq = ref<any>({})
const editableItems = ref<any[]>([])
const supplierQuotes = ref<any[]>([])
const expandedRows = ref(new Set<number>())
const loading = ref(true)
const saving = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const statusColor = computed(() => {
  const s = rfq.value.status?.toLowerCase()
  if (s === 'closed') return 'success'
  if (s === 'cancelled') return 'error'
  return 'primary'
})

const totalQuotes = computed(() => supplierQuotes.value.length)

// ──── Data Loading ────

onMounted(async () => {
  await loadData()
})

async function loadData() {
  loading.value = true
  try {
    const [rfqData, quotesData] = await Promise.all([
      api.get<any>(`/rfqs/${route.params.id}`),
      api.get<any[]>(`/rfqs/${route.params.id}/supplier-quotes`)
    ])
    rfq.value = rfqData
    // Create editable copies of items
    editableItems.value = (rfqData.items || []).map((i: any) => ({
      id: i.id,
      description: i.description, 
      partNumberName: i.partNumberName,
      partNumberId: i.partNumberId,
      alt: i.alt || '',
      qty: i.qty,
      condition: i.condition || ''
    }))
    supplierQuotes.value = quotesData.map((q: any) => ({ ...q }))
  } catch (e) {
    console.error('Failed to load RFQ:', e)
  } finally {
    loading.value = false
  }
}

// ──── Quote Management ────

function getItemQuotes(itemId: number) {
  return supplierQuotes.value.filter(q => q.rfqItemId === itemId)
}

function getQuoteCount(itemId: number) {
  return supplierQuotes.value.filter(q => q.rfqItemId === itemId).length
}

function addQuoteRow(itemId: number) {
  const item = editableItems.value.find(i => i.id === itemId)
  supplierQuotes.value.push({
    id: null,
    rfqItemId: itemId,
    supplierName: '',
    qty: item?.qty || 1,
    price: 0,
    condition: 'NE',
    alt: ''
  })
}

async function removeQuote(itemId: number, qIdx: number) {
  const itemQuotes = getItemQuotes(itemId)
  const quote = itemQuotes[qIdx]

  if (quote.id) {
    try {
      await api.del(`/rfqs/${route.params.id}/supplier-quotes/${quote.id}`)
      showSnack('Quote removed', 'success')
    } catch (e) {
      showSnack('Failed to remove quote', 'error')
      return
    }
  }

  const globalIdx = supplierQuotes.value.indexOf(quote)
  if (globalIdx > -1) supplierQuotes.value.splice(globalIdx, 1)
}

// ──── Save All (items + quotes) ────

async function saveAll() {
  saving.value = true
  try {
    // 1. Save all editable item fields
    const itemPromises = editableItems.value.map(item =>
      api.put(`/rfqs/items/${item.id}`, {
        alt: item.alt || null,
        qty: item.qty,
        condition: item.condition || null
      })
    )
    await Promise.all(itemPromises)

    // 2. Save all supplier quotes
    const quotesToSave = supplierQuotes.value
      .filter(q => q.supplierName?.trim())
      .map(q => ({
        id: q.id || null,
        rfqItemId: q.rfqItemId,
        supplierName: q.supplierName,
        qty: q.qty,
        price: q.price,
        condition: q.condition,
        alt: q.alt
      }))

    if (quotesToSave.length > 0) {
      await api.post(
        `/rfqs/${route.params.id}/supplier-quotes/bulk`,
        { quotes: quotesToSave }
      )
    }

    // Reload
    await loadData()
    showSnack('All changes saved successfully', 'success')
  } catch (e) {
    showSnack('Failed to save changes', 'error')
  } finally {
    saving.value = false
  }
}

// ──── Helpers ────

function toggleExpand(itemId: number) {
  if (expandedRows.value.has(itemId)) {
    expandedRows.value.delete(itemId)
  } else {
    expandedRows.value.add(itemId)
  }
  expandedRows.value = new Set(expandedRows.value)
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>

<style scoped>
.rfq-single-view {
  max-width: 100%;
}

/* Info Cards */
.info-card {
  background: rgba(30, 41, 59, 0.6) !important;
  border: 1px solid rgba(51, 65, 85, 0.5) !important;
  backdrop-filter: blur(8px);
  transition: border-color 0.2s;
}
.info-card:hover {
  border-color: rgba(59, 130, 246, 0.3) !important;
}

/* Excel Card */
.excel-card {
  background: rgba(22, 27, 34, 0.9) !important;
  border: 1px solid rgba(51, 65, 85, 0.6) !important;
  overflow: hidden;
}

.excel-toolbar {
  border-bottom: 1px solid rgba(51, 65, 85, 0.6);
  background: rgba(30, 41, 59, 0.4);
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

/* Editable item inputs (master row) */
.item-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: rgba(15, 23, 42, 0.3);
  color: #e2e8f0;
  padding: 4px 8px;
  font-size: 12px;
  border-radius: 4px;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.item-input:hover {
  border-color: rgba(51, 65, 85, 0.5);
}
.item-input:focus {
  background: rgba(15, 23, 42, 0.7);
  border-color: #3b82f6;
  box-shadow: 0 0 0 1px rgba(59, 130, 246, 0.3);
}
.item-input::placeholder {
  color: #475569;
}
.item-select {
  cursor: pointer;
  appearance: auto;
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
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
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
}

.quote-row {
  transition: background-color 0.15s;
}
.quote-row:hover {
  background: rgba(51, 65, 85, 0.2);
}

/* Quote Inputs — Excel-like cells */
.quote-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: rgba(15, 23, 42, 0.4);
  color: #e2e8f0;
  padding: 4px 8px;
  font-size: 12px;
  border-radius: 4px;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.quote-input:hover {
  border-color: rgba(51, 65, 85, 0.6);
}
.quote-input:focus {
  background: rgba(15, 23, 42, 0.8);
  border-color: #3b82f6;
  box-shadow: 0 0 0 1px rgba(59, 130, 246, 0.3);
}
.quote-input::placeholder {
  color: #475569;
  font-style: italic;
}

.quote-select {
  cursor: pointer;
  appearance: auto;
}

.price-input {
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  text-align: right;
}

.alt-input {
  color: #fbbf24;
}

.text-center {
  text-align: center;
}

.empty-quotes {
  border: 1px dashed rgba(51, 65, 85, 0.5);
  border-radius: 8px;
}
</style>
