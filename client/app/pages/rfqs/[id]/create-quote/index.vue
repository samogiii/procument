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
        Editing Commission #{{ editQuoteId }}
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
            Total: <strong style="color: #4ade80;">${{ formatPrice(selectedTotal) }}</strong>
          </span>
        </div>
        <div class="d-flex flex-wrap align-center gap-2">
          <!-- Global Coefs -->
          <v-text-field
            v-model.number="globalCoef1"
            label="Coef 1"
            type="number"
            density="compact"
            hide-details
            variant="outlined"
            step="0.01"
            min="0"
            style="min-width: 90px; max-width: 110px;"
          />
          <v-text-field
            v-model.number="globalCoef2"
            label="Coef 2"
            type="number"
            density="compact"
            hide-details
            variant="outlined"
            step="0.01"
            min="0"
            style="min-width: 90px; max-width: 110px;"
          />
          <v-text-field
            v-model.number="globalCoef3"
            label="Coef 3"
            type="number"
            density="compact"
            hide-details
            variant="outlined"
            step="0.01"
            min="0"
            style="min-width: 90px; max-width: 110px;"
          />
          <v-divider vertical class="mx-1" style="height: 32px;" />
          <v-text-field
            v-model="validUntil"
            label="Valid Until"
            type="date"
            :min="today"
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
                <td class="text-medium-emphasis" style="padding-left: 12px; font-size: 13px;">{{ item.description || '—' }}</td>
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
                      <v-spacer />
                      <v-btn
                        v-if="hasShops(item.id)"
                        size="x-small"
                        :color="showShopsForItem[item.id] ? 'warning' : 'grey'"
                        variant="tonal"
                        prepend-icon="mdi-wrench"
                        @click="toggleShopsForItem(item.id)"
                      >
                        {{ showShopsForItem[item.id] ? 'Hide Shops' : 'Show Shops' }}
                      </v-btn>
                    </div>

                    <div class="quote-grid-scroll" v-if="getItemRecords(item.id).length > 0">
                    <table class="quote-grid">
                      <thead>
                        <tr>
                          <th style="opacity:1; width: 40px; position: sticky; left: 0; background: var(--toolbar-bg); z-index: 3; border-right: 1px solid var(--card-border);"></th>
                          <th style="opacity:1; min-width: 80px; position: sticky; left: 40px; background: var(--toolbar-bg); z-index: 3; border-right: 1px solid var(--card-border);">Supplier</th>
                          <th style="width: 130px;">Alt P/N</th>
                          <th style="width: 80px;">Condition</th>
                          <th style="width: 70px;">Qty</th>
                          <th style="width: 100px;">Cert</th>
                          <th style="width: 100px;">Tag Date</th>
                          <th style="width: 120px;">Shipping Point</th>
                          <th style="width: 110px;">Shipping Cost</th>
                          <th style="width: 110px;">Lead Time</th>
                          <th style="width: 250px;">Note</th>
                          <th v-if="hasArForItem(item.id)" style="width: 100px; color: #ff9800;">Repair Cost</th>
                          <th style="width: 110px;">Buy Price</th>
                          <th style="width: 120px;">Total Buy Price</th>
                          <th style="width: 75px;">Coef 1</th>
                          <th style="width: 75px;">Coef 2</th>
                          <th style="width: 75px;">Coef 3</th>
                          <th style="width: 110px;">Unit Price</th>
                          <th style="width: 120px;">Total Price</th>
                          <th style="width: 140px; color: #a78bfa;">My Notes</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr
                          v-for="record in getItemRecords(item.id)"
                          :key="record.id"
                          class="quote-row"
                          :class="{ 
                            'selected-row': selections[record.id], 
                            'shop-record-row': record.isShop,
                            'disabled-row': isLineDisabled(record)
                          }"
                        >
                          <td class="text-center" style="position: sticky; left: 0; background: var(--toolbar-bg); opacity: 1; z-index: 2; border-right: 1px solid var(--card-border);">
                            <v-tooltip v-if="isLineDisabled(record)" location="top" :text="getDisabledReason(record)">
                              <template #activator="{ props: tp }">
                                <div v-bind="tp" class="d-inline-block">
                                  <input
                                    type="checkbox"
                                    :disabled="!isAdmin"
                                    :checked="selections[record.id]"
                                    @click.prevent="toggleSelection(record)"
                                    class="record-checkbox"
                                    :class="{ 'opacity-30': !isAdmin }"
                                  />
                                </div>
                              </template>
                            </v-tooltip>
                            <input
                              v-else
                              type="checkbox"
                              :checked="selections[record.id]"
                              @change="toggleSelection(record)"
                              class="record-checkbox"
                            />
                          </td>
                          <td style="padding-left: 8px; font-size: 13px; position: sticky; left: 40px; background: var(--toolbar-bg); opacity: 1; z-index: 2; border-right: 1px solid var(--card-border);">
                            {{ record.supplierName }}
                            <v-chip v-if="record.isShop" size="x-small" color="warning" variant="tonal" class="ml-1">Shop</v-chip>
                          </td>
                          <td style="padding-left: 8px; font-size: 12px; color: #fbbf24;">
                            {{ record.alt || '—' }}
                          </td>
                          <td style="padding-left: 8px; font-size: 12px;">
                            {{ record.isShop ? (record.condition || '—') : (record.condition || 'N/A') }}
                          </td>
                          <td class="text-center" style="font-size: 13px;">{{ record.qty }}</td>
                          <td style="padding-left: 8px; font-size: 12px;">
                            {{ record.certName || '—' }}
                          </td>
                          <td style="padding-left: 8px; font-size: 12px;">
                            {{ record.tagDate ? new Date(record.tagDate).toLocaleDateString() : '—' }}
                          </td>
                          <td>
                            <input
                              type="text"
                              class="coef-input"
                              placeholder="Shipping point..."
                              v-model="record.shippingPoint"
                              style="min-width: 100px;"
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input"
                              placeholder="0.00"
                              v-model.number="record.shippingCost"
                              step="0.01"
                              min="0"
                              style="text-align: right;"
                            />
                          </td>
                          <td>
                            <input
                              type="text"
                              class="coef-input"
                              placeholder="e.g. 7-10 days"
                              v-model="record.leadTime"
                              style="min-width: 90px;"
                            />
                          </td>
                          <td>
                            <v-textarea
                              type="text"
                              placeholder="Note..."
                              v-model="record.note"
                              rows="1"
                              auto-grow
                              hide-details
                              density="compact"
                              variant="plain"
                            />
                          </td>
                          <td v-if="hasArForItem(item.id)">
                            <span v-if="record.isShop && record.fixPrice" class="computed-cell" style="color: #ff9800; font-weight: 600;">
                              ${{ formatPrice(record.fixPrice) }}
                            </span>
                            <span v-else class="text-medium-emphasis" style="padding-left: 8px; font-size: 12px;">—</span>
                          </td>
                          <td class="text-medium-emphasis" style="font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                            ${{ formatPrice(record.price) }}
                          </td>
                          <td class="computed-cell" style="font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                            ${{ formatPrice((Number(record.price) || 0) * (Number(record.qty) || 1)) }}
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input"
                              placeholder="1"
                              v-model.number="record.coef_1"
                              step="0.01"
                              @input="record.customUnitPrice = null"
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input"
                              placeholder="1"
                              v-model.number="record.coef_2"
                              step="0.01"
                              @input="record.customUnitPrice = null"
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input"
                              placeholder="1"
                              v-model.number="record.coef_3"
                              step="0.01"
                              @input="record.customUnitPrice = null"
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              class="coef-input unit-price-input"
                              :value="getUnitPrice(record)"
                              step="0.01"
                              min="0"
                              @input="onUnitPriceInput(record, $event)"
                            />
                          </td>
                          <td class="computed-cell total-cell">
                            ${{ formatPrice(calcTotalPrice(record)) }}
                          </td>
                          <td>
                            <v-textarea
                              type="text"
                              placeholder="My notes..."
                              v-model="record.myNotes"
                              style="color: #a78bfa;"
                              rows="1"
                              auto-grow
                              hide-details
                              density="compact"
                              variant="plain"
                            />
                          </td>
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

    <!-- Condition Warning Dialog -->
    <v-dialog v-model="showConditionWarning" max-width="450">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-alert-circle" color="warning" class="mr-2" />
          Supplier Condition Warning
        </v-card-title>
        <v-card-text class="pa-4">
          <div class="text-body-1 mb-2">{{ warningMessage }}</div>
          <div class="text-body-2 text-medium-emphasis">As an Admin, you can choose to bypass this condition. Do you want to proceed?</div>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showConditionWarning = false">Cancel</v-btn>
          <v-btn color="warning" variant="tonal" @click="confirmBypass">Select Anyway</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { formatPrice } from '@/utils/formatPrice'

const route = useRoute()
const router = useRouter()
const api = useApi()
const authStore = useAuthStore()

const isAdmin = computed(() => authStore.isAdmin)
const today = new Date().toISOString().split('T')[0]

// State
const loading = ref(true)
const saving = ref(false)
const rfq = ref<any>({})
const rfqItems = ref<any[]>([])
const procurementRecords = ref<any[]>([])
const expandedRows = ref(new Set<number>())
const validUntil = ref('')
const finalPriceOverride = ref<number | null>(null)

// Global coefs
const globalCoef1 = ref<number | null>(null)
const globalCoef2 = ref<number | null>(null)
const globalCoef3 = ref<number | null>(null)

// selections
const selections = ref<Record<number, boolean>>({})
const showShopsForItem = ref<Record<number, boolean>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// Warning Logic
const showConditionWarning = ref(false)
const warningMessage = ref('')
const pendingSelectionRecord = ref<any>(null)

// Computed
const selectedRecords = computed(() =>
  procurementRecords.value.filter(r => selections.value[r.id])
)
const selectedCount = computed(() => selectedRecords.value.length)
const selectedTotal = computed(() =>
  selectedRecords.value.reduce((sum, r) => sum + calcTotalPrice(r), 0)
)

const editQuoteId = computed(() => (route.query.editQuoteId as string) || null)
const isEditMode = computed(() => !!editQuoteId.value)
const existingQuote = ref<any>(null)

const backUrl = computed(() =>
  isEditMode.value && editQuoteId.value
    ? `/quotes/${editQuoteId.value}`
    : `/rfqs/${route.params.id}`
)

// Watch global coefs
watch(globalCoef1, (val) => { if (val != null) procurementRecords.value.forEach(r => { r.coef_1 = val; r.customUnitPrice = null }) })
watch(globalCoef2, (val) => { if (val != null) procurementRecords.value.forEach(r => { r.coef_2 = val; r.customUnitPrice = null }) })
watch(globalCoef3, (val) => { if (val != null) procurementRecords.value.forEach(r => { r.coef_3 = val; r.customUnitPrice = null }) })

onMounted(async () => {
  await loadData()
  if (!isEditMode.value) {
    try {
      const existingQuotes = await api.get<any[]>(`/quotes/by-rfq/${route.params.id}`)
      const activeQuote = (existingQuotes || []).find((q: any) => q.status !== 'Rejected')
      if (activeQuote) {
        showSnack('A quote already exists for this RFQ. Redirecting...', 'warning')
        setTimeout(() => router.push(`/quotes/${activeQuote.id}`), 800)
        return
      }
    } catch {}
  }
  if (editQuoteId.value) await loadExistingQuote()

  rfqItems.value.forEach(item => {
    if (getRecordCount(item.id) > 0) {
      expandedRows.value.add(item.id)
      const hasAR = procurementRecords.value.some(r => r.rfqItemId === item.id && r.condition === 'AR' && !r.isShop)
      if (hasAR && hasShops(item.id)) showShopsForItem.value[item.id] = true
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
    rfq.value = rfqData
    rfqItems.value = (rfqData.items || []).map((i: any) => ({
      id: i.id,
      partNumberName: i.partNumberName,
      partNumberId: i.partNumberId,
      description: i.description,
      qty: i.qty,
      condition: i.condition || ''
    }))

    const flatRecords: any[] = []
    for (const r of records || []) {
      flatRecords.push({ ...r, coef_1: r.coef_1 ?? 1, coef_2: r.coef_2 ?? 1, coef_3: r.coef_3 ?? 1, isShop: false })
      for (const shop of r.shopRecords || []) {
        flatRecords.push({ ...shop, coef_1: shop.coef_1 ?? 1, coef_2: shop.coef_2 ?? 1, coef_3: shop.coef_3 ?? 1, isShop: true, parentProcurementId: r.id })
      }
    }
    procurementRecords.value = flatRecords
    const sel: Record<number, boolean> = {}
    procurementRecords.value.forEach((r: any) => { sel[r.id] = false })
    selections.value = sel
  } catch (e) {
    showSnack('Failed to load data', 'error')
  } finally {
    loading.value = false
  }
}

function isLineDisabled(record: any) {
  const dep = record.supplierDependency || 'Normal'
  if (dep === 'NoQuote' && !isAdmin.value) return true
  if (dep === 'EndUser' && rfq.value.customerBase == 5) return true
  if (dep === 'Certificated' && !record.isCertificated) return true
  return false
}

function getDisabledReason(record: any) {
  const dep = record.supplierDependency || 'Normal'
  if (dep === 'NoQuote' && !isAdmin.value) return 'Only Admin can use NoQuote suppliers'
  if (dep === 'EndUser' && rfq.value.customerBase == 5) return 'EndUser supplier not allowed for Customer Base 5'
  if (dep === 'Certificated' && !record.isCertificated) return 'Must be marked as Certificated in Procurements'
  return ''
}

function toggleSelection(record: any) {
  if (isLineDisabled(record)) {
    if (isAdmin.value) {
      warningMessage.value = getDisabledReason(record)
      pendingSelectionRecord.value = record
      showConditionWarning.value = true
      return
    }
    return
  }
  selections.value[record.id] = !selections.value[record.id]
  selections.value = { ...selections.value }
  if (record.condition === 'AR' && !record.isShop && selections.value[record.id]) {
    showShopsForItem.value[record.rfqItemId] = true
    showShopsForItem.value = { ...showShopsForItem.value }
  }
}

function confirmBypass() {
  if (pendingSelectionRecord.value) {
    const r = pendingSelectionRecord.value
    selections.value[r.id] = !selections.value[r.id]
    selections.value = { ...selections.value }
    if (r.condition === 'AR' && !r.isShop && selections.value[r.id]) {
      showShopsForItem.value[r.rfqItemId] = true
      showShopsForItem.value = { ...showShopsForItem.value }
    }
  }
  showConditionWarning.value = false
  pendingSelectionRecord.value = null
}

function calcUnitPrice(q: any): number {
  const price = Number(q.price) || 0
  const shipping = Number(q.shippingCost) || 0
  const qty = Number(q.qty) || 1
  const c1 = Number(q.coef_1) || 1
  const c2 = Number(q.coef_2) || 1
  const c3 = Number(q.coef_3) || 1
  if (q.isShop) {
    const repairCost = Number(q.fixPrice) || 0
    return (price + repairCost + (shipping / qty)) * c1 * c2 * c3
  }
  return (price + (shipping / qty)) * c1 * c2 * c3
}

function getUnitPrice(q: any): number {
  if (q.customUnitPrice != null && Number(q.customUnitPrice) > 0) return Number(q.customUnitPrice)
  return calcUnitPrice(q)
}

function calcTotalPrice(q: any): number {
  return getUnitPrice(q) * (Number(q.qty) || 1)
}

function onUnitPriceInput(record: any, event: Event) {
  const val = parseFloat((event.target as HTMLInputElement).value)
  if (!isNaN(val) && val > 0) record.customUnitPrice = val
  else record.customUnitPrice = null
}

function getItemRecords(itemId: number) {
  const records = procurementRecords.value.filter(r => r.rfqItemId === itemId)
  const showShops = showShopsForItem.value[itemId] || false
  return records.filter(r => !r.isShop || showShops)
}

function hasShops(itemId: number): boolean {
  return procurementRecords.value.some(r => r.rfqItemId === itemId && r.isShop)
}

function toggleShopsForItem(itemId: number) {
  showShopsForItem.value[itemId] = !showShopsForItem.value[itemId]
  showShopsForItem.value = { ...showShopsForItem.value }
}

function getRecordCount(itemId: number) {
  return procurementRecords.value.filter(r => r.rfqItemId === itemId).length
}

function hasArForItem(itemId: number): boolean {
  return procurementRecords.value.some(r => r.rfqItemId === itemId && (r.condition || '').toUpperCase() === 'AR')
}

function toggleExpand(itemId: number) {
  if (expandedRows.value.has(itemId)) expandedRows.value.delete(itemId)
  else {
    expandedRows.value.add(itemId)
    const hasAR = procurementRecords.value.some(r => r.rfqItemId === itemId && r.condition === 'AR' && !r.isShop)
    if (hasAR && hasShops(itemId)) showShopsForItem.value[itemId] = true
  }
  expandedRows.value = new Set(expandedRows.value)
}

async function loadExistingQuote() {
  try {
    existingQuote.value = await api.get<any>(`/quotes/${editQuoteId.value}`)
    const eq = existingQuote.value
    if (eq.validUntil) validUntil.value = new Date(eq.validUntil).toISOString().split('T')[0]
    if (eq.finalPrice != null) finalPriceOverride.value = eq.finalPrice
    if (eq.items && Array.isArray(eq.items)) {
      for (const qi of eq.items) {
        if (qi.procumentRecordId) {
          const exactMatch = procurementRecords.value.find((r: any) => r.id === qi.procumentRecordId)
          if (exactMatch) {
            selections.value[exactMatch.id] = true
            const calc = calcUnitPrice(exactMatch)
            if (qi.unitPrice && Math.abs(Number(qi.unitPrice) - calc) > 0.001) exactMatch.customUnitPrice = Number(qi.unitPrice)
            continue
          }
        }
        const matchingRecords = procurementRecords.value.filter((r: any) => r.rfqItemId === qi.rfqItemId && !selections.value[r.id])
        if (matchingRecords.length > 0) selections.value[matchingRecords[0].id] = true
      }
      selections.value = { ...selections.value }
    }
  } catch {
    showSnack('Failed to load existing quote for editing', 'error')
  }
}

async function saveQuote() {
  const selected = selectedRecords.value
  if (selected.length === 0) {
    showSnack('Please select at least one supplier price', 'warning')
    return
  }
  saving.value = true
  try {
    const quotesToUpdate = selected.map(r => {
      const effectiveUnit = getUnitPrice(r)
      const effectiveTotal = effectiveUnit * (Number(r.qty) || 1)
      return {
        id: r.id,
        rfqItemId: r.rfqItemId,
        supplierName: r.supplierName,
        qty: r.qty,
        price: r.price,
        condition: r.condition,
        alt: r.alt,
        unit: r.unit || null,
        leadTime: r.leadTime || null,
        note: r.note || null,
        myNotes: r.myNotes || null,
        certName: r.certName || null,
        tagDate: r.tagDate || null,
        shippingCost: r.shippingCost ?? null,
        shippingPoint: r.shippingPoint || null,
        coef_1: r.coef_1 ?? 1,
        coef_2: r.coef_2 ?? 1,
        coef_3: r.coef_3 ?? 1,
        unitPrice: effectiveUnit,
        totalPrice: effectiveTotal,
        type: r.type || (r.isShop ? 'Shop' : 'Procument'),
        fixPrice: r.fixPrice ?? null,
        parentProcumentId: r.parentProcurementId ?? null,
      }
    })
    if (quotesToUpdate.length > 0) {
      await api.post(`/rfqs/${route.params.id}/supplier-quotes/bulk`, { quotes: quotesToUpdate })
    }
    const items = selected.map(r => ({
      rfqItemId: r.rfqItemId,
      procumentRecordId: r.id,
      qty: r.qty,
      unitPrice: getUnitPrice(r),
      condition: r.condition || null,
      alt: r.alt || null,
      leadTimeDays: null
    }))
    const payload = { rfqId: Number(route.params.id), validUntil: validUntil.value || null, finalPrice: finalPriceOverride.value || null, items }
    if (isEditMode.value) {
      await api.put(`/quotes/${editQuoteId.value}`, payload)
      showSnack('Quote updated successfully', 'success')
      setTimeout(() => router.push(`/quotes/${editQuoteId.value}`), 500)
    } else {
      const created = await api.post<any>('/quotes', payload)
      showSnack('Quote created successfully', 'success')
      const newQuoteId = created?.id || created?.Id
      setTimeout(() => { if (newQuoteId) router.push(`/quotes/${newQuoteId}`); else router.push(`/rfqs/${route.params.id}`) }, 500)
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
  background: var(--card-bg) !important;
  border: 1px solid var(--card-border) !important;
}

/* Excel Card */
.excel-card {
  background: var(--excel-bg) !important;
  border: 1px solid var(--excel-border) !important;
  overflow: hidden;
}

.excel-container {
  overflow-x: auto;
  scrollbar-width: auto;
  scrollbar-color: var(--card-border) var(--toolbar-bg);
}

.excel-container::-webkit-scrollbar {
  height: 10px;
}

.excel-container::-webkit-scrollbar-track {
  background: var(--toolbar-bg);
  border-radius: 5px;
}

.excel-container::-webkit-scrollbar-thumb {
  background: var(--card-border);
  border-radius: 5px;
}

.excel-container::-webkit-scrollbar-thumb:hover {
  background: var(--row-hover);
}

/* Excel Grid */
.excel-grid {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
  min-width: 900px;
}

.excel-grid thead th {
  background: var(--toolbar-bg);
  color: rgb(var(--v-theme-on-surface), 0.6);
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 12px;
  border-bottom: 2px solid var(--excel-border);
  text-align: left;
  position: sticky;
  top: 0;
  z-index: 2;
  white-space: nowrap;
}

.excel-grid tbody td {
  padding: 0 12px;
  height: 42px;
  border-bottom: 1px solid var(--card-border);
  font-size: 13px;
  vertical-align: middle;
}

/* Master Row */
.master-row {
  transition: background-color 0.15s;
  cursor: default;
}
.master-row:hover {
  background: var(--row-hover);
}
.master-row.expanded {
  background: var(--toolbar-bg);
  border-bottom: none;
}

.cell-expand {
  text-align: center;
  cursor: pointer;
  transition: background-color 0.15s;
}
.cell-expand:hover {
  background: var(--cell-hover);
}

.cell-number {
  text-align: center;
  opacity: 0.5;
  font-size: 12px;
}

.cell-pn {
  color: var(--pn-color);
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
  background: var(--toolbar-bg);
  border-bottom: 2px solid var(--card-hover-border) !important;
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
  max-width: 100%;
  scrollbar-width: auto;
  scrollbar-color: var(--card-border) var(--toolbar-bg);
}

.quote-grid-scroll::-webkit-scrollbar {
  height: 10px;
}

.quote-grid-scroll::-webkit-scrollbar-track {
  background: var(--toolbar-bg);
  border-radius: 5px;
}

.quote-grid-scroll::-webkit-scrollbar-thumb {
  background: var(--card-border);
  border-radius: 5px;
}

.quote-grid-scroll::-webkit-scrollbar-thumb:hover {
  background: var(--row-hover);
}

.computed-cell {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
  opacity: 0.7;
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
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 6px;
  font-size: 12px;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  text-align: center;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.coef-input:hover {
  border-color: var(--card-border);
}
.coef-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px var(--card-hover-border);
}

/* Editable Unit Price input */
.unit-price-input {
  color: #60a5fa !important;
  font-weight: 600;
  text-align: right;
}

.letter-spacing-wide {
  letter-spacing: 0.1em;
}

/* Quote Sub-Grid */
.quote-grid {
  width: 100%;
  border-collapse: collapse;
  min-width: 2000px;
}

.quote-grid thead th {
  opacity: 0.6;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 6px 8px;
  border-bottom: 1px solid var(--card-border);
  text-align: left;
}

.quote-grid tbody td {
  padding: 3px 4px;
  border-bottom: 1px solid var(--card-border);
  height: 38px;
  vertical-align: middle;
}

.shop-record-row {
  background: rgba(255, 152, 0, 0.04) !important;
  border-left: 2px solid #ff9800;
}

.quote-row {
  transition: background-color 0.15s;
}
.quote-row:hover {
  background: var(--row-hover);
}
.quote-row.selected-row {
  background: rgba(74, 222, 128, 0.1);
  border-left: 3px solid #4ade80;
}

.quote-row.selected-row td[style*="position: sticky"] {
  background: rgba(74, 222, 128, 0.15) !important;
}

.disabled-row {
  opacity: 0.6;
  background: rgba(var(--v-theme-on-surface), 0.05);
}
.opacity-30 {
  opacity: 0.3;
}

/* Checkbox */
.record-checkbox {
  width: 16px;
  height: 16px;
  accent-color: #3b82f6;
  cursor: pointer;
}

.empty-records {
  border: 1px dashed var(--card-border);
  border-radius: 8px;
}

.text-center { text-align: center; }
</style>