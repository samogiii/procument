<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="navigateTo('/ils?tab=quotes')" />
      <div>
        <h1 class="text-h5 font-weight-bold">{{ quote?.quoteNumber || 'ILS Quote' }}</h1>
        <p class="text-caption text-medium-emphasis mb-0">ILS Quote Detail</p>
      </div>
      <v-spacer />
      <v-chip
        v-if="quote"
        :color="statusColor(quote.status)"
        variant="tonal"
        size="small"
        class="mr-2"
      >
        {{ quote.status }}
      </v-chip>
      <v-btn
        v-if="isAdmin && quote"
        variant="tonal"
        color="warning"
        size="small"
        prepend-icon="mdi-pencil"
        @click="openEditDialog"
      >
        Edit
      </v-btn>
      <v-btn
        variant="tonal"
        color="primary"
        size="small"
        prepend-icon="mdi-file-pdf-box"
        @click="showPdf = true"
      >
        PDF
      </v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-row v-if="quote" class="mb-4">
      <!-- Quote Info Card -->
      <v-col cols="12" md="6">
        <v-card class="glass-card h-100">
          <v-card-text>
            <div class="text-caption text-medium-emphasis mb-3 font-weight-bold" style="text-transform: uppercase; letter-spacing: 0.08em;">Quote Info</div>
            <div class="info-grid">
              <span class="info-label">Quote Number</span>
              <span class="info-value font-weight-bold" style="font-family: monospace;">{{ quote.quoteNumber }}</span>

              <span class="info-label">Status</span>
              <v-chip :color="statusColor(quote.status)" variant="tonal" size="x-small">{{ quote.status }}</v-chip>

              <span class="info-label">RFQ Reference</span>
              <span class="info-value">{{ quote.rfqReference || '—' }}</span>

              <span class="info-label">Total Amount</span>
              <span class="info-value font-weight-bold" style="font-family: monospace; color: #4ade80;">
                ${{ formatPrice(quote.totalAmount) }}
              </span>

              <span class="info-label">Created At</span>
              <span class="info-value">{{ new Date(quote.createdAt).toLocaleDateString() }}</span>

              <span v-if="quote.notes" class="info-label">Notes</span>
              <span v-if="quote.notes" class="info-value text-medium-emphasis" style="white-space: pre-wrap;">{{ quote.notes }}</span>
            </div>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Customer Card -->
      <v-col cols="12" md="6">
        <v-card class="glass-card h-100">
          <v-card-text>
            <div class="text-caption text-medium-emphasis mb-3 font-weight-bold" style="text-transform: uppercase; letter-spacing: 0.08em;">Customer</div>
            <div class="info-grid">
              <span class="info-label">Name</span>
              <span class="info-value font-weight-bold">{{ quote.ilsCustomerName }}</span>

              <span v-if="quote.ilsCustomerCode" class="info-label">Code</span>
              <span v-if="quote.ilsCustomerCode" class="info-value">{{ quote.ilsCustomerCode }}</span>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Items Table -->
    <v-card v-if="quote" class="glass-card">
      <v-card-text>
        <div class="text-caption text-medium-emphasis mb-3 font-weight-bold" style="text-transform: uppercase; letter-spacing: 0.08em;">
          Items ({{ quote.items?.length || 0 }})
        </div>
        <div class="detail-table-wrap">
          <table class="detail-table">
            <thead>
              <tr>
                <th style="width: 40px;">#</th>
                <th style="min-width: 140px;">Part Number</th>
                <th style="width: 120px;">Alt P/N</th>
                <th style="width: 90px;">Condition</th>
                <th style="width: 80px;">Cert</th>
                <th style="width: 70px;">Qty</th>
                <th style="width: 110px;">Sell Price</th>
                <th style="width: 120px;">Total Price</th>
                <th style="width: 110px;">Lead Time</th>
                <th style="width: 180px;">Notes</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, i) in quote.items" :key="item.id">
                <td class="text-center text-medium-emphasis text-caption">{{ Number(i) + 1 }}</td>
                <td class="font-weight-bold" style="font-family: monospace; padding-left: 8px;">{{ item.partNumberName }}</td>
                <td style="padding-left: 8px; color: #fbbf24; font-size: 12px;">{{ item.altPartNumber || '—' }}</td>
                <td style="padding-left: 8px;">
                  <v-chip v-if="item.condition" size="x-small" variant="tonal" :color="conditionColor(item.condition)">
                    {{ item.condition }}
                  </v-chip>
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td style="padding-left: 8px; font-size: 12px;">{{ item.certName || '—' }}</td>
                <td class="text-center" style="font-size: 13px;">{{ item.qty }}</td>
                <td class="text-right" style="font-family: monospace; padding-right: 12px; font-size: 13px;">
                  ${{ formatPrice(item.sellPrice) }}
                </td>
                <td class="text-right font-weight-bold" style="font-family: monospace; padding-right: 12px; font-size: 13px; color: #4ade80;">
                  ${{ formatPrice(item.totalPrice) }}
                </td>
                <td style="padding-left: 8px; font-size: 12px;">{{ item.leadTime || '—' }}</td>
                <td style="padding-left: 8px; font-size: 12px; max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" :title="item.notes">
                  {{ item.notes || '—' }}
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr>
                <td colspan="6"></td>
                <td class="text-right text-caption font-weight-bold" style="padding-right: 4px; border-top: 1px solid rgba(255,255,255,0.1);">TOTAL:</td>
                <td class="text-right font-weight-bold" style="font-family: monospace; padding-right: 12px; font-size: 14px; color: #4ade80; border-top: 1px solid rgba(255,255,255,0.1);">
                  ${{ formatPrice(quote.totalAmount) }}
                </td>
                <td colspan="2" style="border-top: 1px solid rgba(255,255,255,0.1);"></td>
              </tr>
            </tfoot>
          </table>
        </div>
      </v-card-text>
    </v-card>

    <!-- Status Actions (Admin) -->
    <div v-if="quote && isAdmin" class="d-flex gap-2 mt-4 justify-end">
      <v-btn
        v-if="quote.status === 'Draft'"
        color="info"
        variant="tonal"
        size="small"
        prepend-icon="mdi-send"
        :loading="statusUpdating"
        @click="updateStatus('Sent')"
      >
        Mark as Sent
      </v-btn>
      <v-btn
        v-if="quote.status === 'Sent'"
        color="success"
        variant="tonal"
        size="small"
        prepend-icon="mdi-check"
        :loading="statusUpdating"
        @click="updateStatus('Accepted')"
      >
        Accept
      </v-btn>
      <v-btn
        v-if="quote.status === 'Sent'"
        color="error"
        variant="tonal"
        size="small"
        prepend-icon="mdi-close"
        :loading="statusUpdating"
        @click="updateStatus('Rejected')"
      >
        Reject
      </v-btn>
    </div>

    <!-- Edit Dialog -->
    <v-dialog v-model="showEditDialog" max-width="860" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-pencil" class="mr-2" size="20" color="primary" />
          Edit ILS Quote
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showEditDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense class="mb-2">
            <v-col cols="12" md="6">
              <v-select
                v-model="editForm.ilsCustomerId"
                :items="ilsCustomers"
                item-title="name"
                item-value="id"
                label="ILS Customer *"
                variant="outlined"
                density="compact"
                hide-details
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="editForm.rfqReference"
                label="RFQ Reference Number"
                variant="outlined"
                density="compact"
                hide-details
                placeholder="e.g. RFQ-2024-001"
              />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="editForm.notes" label="Notes" variant="outlined" density="compact" hide-details rows="2" />
            </v-col>
          </v-row>

          <div class="d-flex align-center mb-2">
            <span class="text-subtitle-2">Quote Items</span>
            <v-spacer />
            <v-btn size="x-small" color="primary" variant="tonal" prepend-icon="mdi-plus" @click="addEditItemRow">
              Add Item
            </v-btn>
          </div>

          <div class="quote-items-table-wrap">
            <table class="quote-items-table">
              <thead>
                <tr>
                  <th style="min-width: 200px;">ILS Item *</th>
                  <th style="width: 90px;">Condition</th>
                  <th style="width: 70px;">Cert</th>
                  <th style="width: 80px;">Avail. Qty</th>
                  <th style="width: 70px;">Qty *</th>
                  <th style="width: 110px;">Sell Price *</th>
                  <th style="width: 120px;">Total Price</th>
                  <th style="width: 36px;"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, idx) in editForm.items" :key="idx">
                  <td>
                    <v-autocomplete
                      v-model="row.ilsItem"
                      :items="allILSItems"
                      :item-title="(i: any) => i.partNumberName + (i.altPartNumber ? ' / ' + i.altPartNumber : '')"
                      item-value="id"
                      density="compact"
                      variant="plain"
                      hide-details
                      return-object
                      placeholder="Select from inventory..."
                      no-data-text="No ILS items found"
                      @update:model-value="val => recalcEditRow(row)"
                    >
                      <template #item="{ item: suggestion, props: sp }">
                        <v-list-item v-bind="sp" :subtitle="`${suggestion.raw.condition || '—'} · Qty: ${suggestion.raw.qty} · $${formatPrice(suggestion.raw.price)}`" />
                      </template>
                    </v-autocomplete>
                  </td>
                  <td class="text-center">
                    <v-chip v-if="row.ilsItem?.condition" size="x-small" variant="tonal" :color="conditionColor(row.ilsItem.condition)">
                      {{ row.ilsItem.condition }}
                    </v-chip>
                    <span v-else class="text-medium-emphasis text-caption">—</span>
                  </td>
                  <td style="padding-left: 6px; font-size: 12px; color: rgba(var(--v-theme-on-surface), 0.6);">
                    {{ row.ilsItem?.certName || '—' }}
                  </td>
                  <td class="text-center" style="font-size: 12px; color: rgba(var(--v-theme-on-surface), 0.6);">
                    {{ row.ilsItem?.qty ?? '—' }}
                  </td>
                  <td><v-text-field v-model.number="row.qty" type="number" min="1" density="compact" variant="plain" hide-details @input="recalcEditRow(row)" /></td>
                  <td><v-text-field v-model.number="row.sellPrice" type="number" min="0" step="0.01" density="compact" variant="plain" hide-details prefix="$" @input="recalcEditRow(row)" /></td>
                  <td class="text-right" style="font-family: monospace; padding-right: 8px; color: #4ade80; font-weight: 600;">${{ formatPrice(row.totalPrice) }}</td>
                  <td><v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="editForm.items.splice(idx, 1)" /></td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <td colspan="4"></td>
                  <td class="text-right text-caption font-weight-bold" style="padding-right: 4px;">TOTAL:</td>
                  <td class="text-right font-weight-bold" style="font-family: monospace; padding-right: 8px; color: #4ade80;">
                    ${{ formatPrice(editTotalAmount) }}
                  </td>
                  <td colspan="2"></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <v-spacer />
          <v-btn variant="text" @click="showEditDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="editSaving" :disabled="!editForm.ilsCustomerId || !editForm.items.some(r => r.ilsItem)" @click="saveEdit">
            Save Changes
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- PDF Dialog -->
    <v-dialog v-model="showPdf" max-width="900" scrollable>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-file-pdf-box" class="mr-2" color="error" size="22" />
          ILS Quote PDF Preview
          <v-spacer />
          <v-btn icon="mdi-printer" variant="tonal" color="primary" size="small" class="mr-2" @click="printPdf" />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showPdf = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-0">
          <div class="pdf-preview" ref="pdfRef">
            <div class="pdf-header">
              <div class="pdf-company">
                <div class="pdf-title">ILS QUOTE</div>
                <div class="pdf-subtitle">ILS Aviation Parts</div>
              </div>
              <div class="pdf-meta">
                <table class="pdf-meta-table">
                  <tbody>
                    <tr>
                      <td>Quote #</td>
                      <td><strong>{{ quote?.quoteNumber }}</strong></td>
                    </tr>
                    <tr>
                      <td>Date</td>
                      <td>{{ quote ? new Date(quote.createdAt).toLocaleDateString() : '' }}</td>
                    </tr>
                    <tr v-if="quote?.rfqReference">
                      <td>RFQ Ref</td>
                      <td>{{ quote.rfqReference }}</td>
                    </tr>
                    <tr>
                      <td>Status</td>
                      <td>{{ quote?.status }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <div class="pdf-section-title">Customer</div>
            <div class="pdf-customer">
              <strong>{{ quote?.ilsCustomerName }}</strong>
              <span v-if="quote?.ilsCustomerCode"> ({{ quote.ilsCustomerCode }})</span>
            </div>

            <div class="pdf-section-title">Items</div>
            <table class="pdf-items-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Part Number</th>
                  <th>Alt P/N</th>
                  <th>Condition</th>
                  <th>Cert</th>
                  <th>Qty</th>
                  <th>Sell Price</th>
                  <th>Total Price</th>
                  <th>Lead Time</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(item, i) in quote?.items" :key="item.id">
                  <td>{{ Number(i) + 1 }}</td>
                  <td><strong>{{ item.partNumberName }}</strong></td>
                  <td>{{ item.altPartNumber || '—' }}</td>
                  <td>{{ item.condition || '—' }}</td>
                  <td>{{ item.certName || '—' }}</td>
                  <td>{{ item.qty }}</td>
                  <td>${{ formatPrice(item.sellPrice) }}</td>
                  <td><strong>${{ formatPrice(item.totalPrice) }}</strong></td>
                  <td>{{ item.leadTime || '—' }}</td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <td colspan="7" style="text-align: right; font-weight: bold; border-top: 2px solid #1a2744;">TOTAL:</td>
                  <td style="font-weight: bold; border-top: 2px solid #1a2744;">${{ formatPrice(quote?.totalAmount) }}</td>
                  <td style="border-top: 2px solid #1a2744;"></td>
                </tr>
              </tfoot>
            </table>

            <div v-if="quote?.notes" class="pdf-notes">
              <div class="pdf-section-title">Notes</div>
              <p>{{ quote.notes }}</p>
            </div>
          </div>
        </v-card-text>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const id = computed(() => route.params.id as string)

const quote = ref<any>(null)
const loading = ref(true)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const showPdf = ref(false)
const pdfRef = ref<HTMLElement | null>(null)
const statusUpdating = ref(false)
const ilsCustomers = ref<any[]>([])

const conditionOptions = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']
const certOptions = ['FAA', 'EASA', 'CAAC', 'Dual', 'MFG COC', 'Vendor COC', 'No Cert']

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

function formatPrice(val: any) {
  const n = Number(val) || 0
  return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function statusColor(status: string) {
  const map: Record<string, string> = {
    Draft: 'grey', Sent: 'info', Accepted: 'success', Rejected: 'error'
  }
  return map[status] || 'grey'
}

function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error',
    RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

async function loadQuote() {
  loading.value = true
  try {
    quote.value = await api.get<any>(`/ils-quotes/${id.value}`)
  } catch {
    showSnack('Failed to load quote', 'error')
  } finally {
    loading.value = false
  }
}

async function loadCustomers() {
  try {
    ilsCustomers.value = await api.get<any[]>('/ils-customers')
  } catch {}
}

async function updateStatus(status: string) {
  statusUpdating.value = true
  try {
    const updated = await api.patch<any>(`/ils-quotes/${id.value}/status`, { status })
    quote.value = updated
    showSnack(`Quote marked as ${status}`)
  } catch {
    showSnack('Failed to update status', 'error')
  } finally {
    statusUpdating.value = false
  }
}

function printPdf() {
  window.print()
}

const allILSItems = ref<any[]>([])

async function loadAllILSItems() {
  try {
    allILSItems.value = await api.get<any[]>('/ils')
  } catch {}
}

onMounted(() => {
  loadQuote()
  loadCustomers()
  loadAllILSItems()
})

// ── Edit Dialog ──
const showEditDialog = ref(false)
const editSaving = ref(false)

interface EditItemRow {
  ilsItem: any | null
  qty: number
  sellPrice: number
  totalPrice: number
}

const editForm = ref({
  ilsCustomerId: null as number | null,
  rfqReference: '',
  notes: '',
  items: [] as EditItemRow[],
})

const editTotalAmount = computed(() =>
  editForm.value.items.reduce((sum, r) => sum + (r.totalPrice || 0), 0)
)

function openEditDialog() {
  if (!quote.value) return
  editForm.value = {
    ilsCustomerId: quote.value.ilsCustomerId,
    rfqReference: quote.value.rfqReference || '',
    notes: quote.value.notes || '',
    items: (quote.value.items || []).map((item: any) => {
      const matchedILSItem = allILSItems.value.find((i: any) => i.id === item.ilsItemId)
      return {
        ilsItem: matchedILSItem || {
          id: item.ilsItemId,
          partNumberId: item.partNumberId,
          partNumberName: item.partNumberName,
          altPartNumber: item.altPartNumber,
          condition: item.condition,
          certName: item.certName,
          leadTime: item.leadTime,
          qty: null,
          price: null,
        },
        qty: item.qty,
        sellPrice: Number(item.sellPrice),
        totalPrice: Number(item.totalPrice),
      }
    }),
  }
  showEditDialog.value = true
}

function addEditItemRow() {
  editForm.value.items.push({ ilsItem: null, qty: 1, sellPrice: 0, totalPrice: 0 })
}

function recalcEditRow(row: EditItemRow) {
  row.totalPrice = (Number(row.qty) || 0) * (Number(row.sellPrice) || 0)
}

async function saveEdit() {
  if (!editForm.value.ilsCustomerId) return
  const validItems = editForm.value.items.filter(r => r.ilsItem)
  if (!validItems.length) return
  editSaving.value = true
  try {
    const payload = {
      ilsCustomerId: editForm.value.ilsCustomerId,
      rfqReference: editForm.value.rfqReference || null,
      notes: editForm.value.notes || null,
      items: validItems.map(row => ({
        partNumberId: row.ilsItem.partNumberId,
        partNumberName: row.ilsItem.partNumberName,
        altPartNumber: row.ilsItem.altPartNumber || null,
        condition: row.ilsItem.condition || null,
        certName: row.ilsItem.certName || null,
        qty: Number(row.qty) || 1,
        sellPrice: Number(row.sellPrice) || 0,
        totalPrice: Number(row.totalPrice) || 0,
        leadTime: row.ilsItem.leadTime || null,
        ilsItemId: row.ilsItem.id,
      })),
    }
    const updated = await api.put<any>(`/ils-quotes/${id.value}`, payload)
    quote.value = updated
    showSnack('Quote updated', 'success')
    showEditDialog.value = false
  } catch {
    showSnack('Failed to save changes', 'error')
  } finally {
    editSaving.value = false
  }
}
</script>

<style scoped>
.info-grid {
  display: grid;
  grid-template-columns: 120px 1fr;
  gap: 8px 12px;
  align-items: center;
}
.info-label {
  font-size: 12px;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-weight: 500;
}
.info-value {
  font-size: 13px;
}

.detail-table-wrap {
  overflow-x: auto;
  scrollbar-width: auto;
  scrollbar-color: var(--card-border) #252A37;
}
.detail-table-wrap::-webkit-scrollbar { height: 10px; }
.detail-table-wrap::-webkit-scrollbar-track { background: #252A37; border-radius: 5px; }
.detail-table-wrap::-webkit-scrollbar-thumb { background: var(--card-border); border-radius: 5px; }

.detail-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 1000px;
}
.detail-table thead th {
  opacity: 0.55;
  font-size: 10px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 6px 8px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  text-align: left;
}
.detail-table tbody td {
  padding: 8px 6px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.05);
  font-size: 13px;
}
.detail-table tbody tr:hover td { background: rgba(var(--v-theme-surface-variant), 0.3); }
.detail-table tfoot td { padding: 10px 6px; }

/* Quote items table in dialogs */
.quote-items-table-wrap {
  overflow-x: auto;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  border-radius: 8px;
}
.quote-items-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 700px;
}
.quote-items-table th {
  background: rgba(var(--v-theme-surface-variant), 0.5);
  padding: 6px 8px;
  text-align: left;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}
.quote-items-table td {
  padding: 2px 4px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.06);
  vertical-align: middle;
}
.quote-items-table tfoot td {
  padding: 6px 4px;
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-bottom: none;
}

/* PDF Preview */
.pdf-preview {
  padding: 32px 40px;
  background: #fff;
  color: #111;
  min-height: 600px;
  font-family: Arial, Helvetica, sans-serif;
  font-size: 13px;
}
.pdf-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 28px;
  border-bottom: 3px solid #1a2744;
  padding-bottom: 20px;
}
.pdf-title {
  font-size: 26px;
  font-weight: 900;
  color: #1a2744;
  letter-spacing: 0.05em;
}
.pdf-subtitle {
  font-size: 15px;
  font-weight: 700;
  color: #1a2744;
  margin-top: 4px;
}
.pdf-detail { font-size: 12px; color: #555; margin-top: 2px; }
.pdf-meta-table td { padding: 3px 8px; font-size: 12px; }
.pdf-meta-table td:first-child { color: #666; font-weight: 600; text-align: right; }
.pdf-section-title {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #1a2744;
  border-bottom: 1px solid #ddd;
  padding-bottom: 4px;
  margin: 16px 0 8px;
}
.pdf-customer { font-size: 13px; margin-bottom: 8px; }
.pdf-items-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.pdf-items-table th {
  background: #1a2744;
  color: #fff;
  padding: 6px 8px;
  text-align: left;
  font-weight: 600;
}
.pdf-items-table td {
  padding: 6px 8px;
  border-bottom: 1px solid #eee;
}
.pdf-items-table tr:nth-child(even) td { background: #f8f9fa; }
.pdf-items-table tfoot td { padding: 8px; background: #f0f2f5; font-size: 13px; }
.pdf-notes { margin-top: 20px; font-size: 12px; color: #444; }

@media print {
  .pdf-preview { padding: 20px; }
  .v-card { box-shadow: none !important; }
}
</style>
