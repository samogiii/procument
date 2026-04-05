<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">ILS Inventory</h1>
      <v-spacer />
      <v-btn
        v-if="isAdmin"
        variant="tonal"
        color="warning"
        prepend-icon="mdi-wrench"
        size="small"
        @click="openARShopDialog"
      >
        Import AR Shops
      </v-btn>
      <v-btn
        variant="tonal"
        color="success"
        prepend-icon="mdi-file-excel"
        size="small"
        @click="openExcelImport"
      >
        Excel Import
      </v-btn>
      <v-btn
        color="primary"
        prepend-icon="mdi-plus"
        size="small"
        @click="openAddDialog"
      >
        Add Item
      </v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <!-- Filters -->
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search..."
            single-line
            hide-details
            class="flex-grow-1"
            style="min-width: 200px;"
          />
          <v-select
            v-model="conditionFilter"
            :items="conditionOptions"
            label="Condition"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
          />
          <v-select
            v-model="certFilter"
            :items="certOptions"
            label="Cert Type"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
          />
          <v-btn
            v-if="hasActiveFilters"
            variant="tonal"
            color="error"
            size="small"
            prepend-icon="mdi-filter-off"
            class="align-self-center"
            @click="clearFilters"
          >
            Clear
          </v-btn>
        </div>

        <v-data-table
          :headers="headers"
          :items="filteredItems"
          :search="search"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
        >
          <template #item.price="{ item }">
            <span class="font-weight-medium" style="font-family: monospace; color: #4ade80;">
              ${{ formatPrice(item.price) }}
            </span>
          </template>
          <template #item.tagDate="{ item }">
            {{ item.tagDate ? new Date(item.tagDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.condition="{ item }">
            <v-chip v-if="item.condition" size="small" variant="tonal" :color="conditionColor(item.condition)">
              {{ item.condition }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.certName="{ item }">
            <v-chip v-if="item.certName" size="small" variant="tonal" color="info">{{ item.certName }}</v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.procumentRecordId="{ item }">
            <v-chip v-if="item.procumentRecordId" size="x-small" color="warning" variant="tonal" prepend-icon="mdi-wrench">
              Shop #{{ item.procumentRecordId }}
            </v-chip>
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openEditDialog(item)" />
            <v-btn
              v-if="isAdmin"
              icon="mdi-delete"
              size="x-small"
              variant="text"
              color="error"
              @click="confirmDelete(item)"
            />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Add / Edit Dialog -->
    <v-dialog v-model="showDialog" max-width="720" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon :icon="isEditing ? 'mdi-pencil' : 'mdi-plus'" class="mr-2" size="20" />
          {{ isEditing ? 'Edit ILS Item' : 'Add ILS Item' }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeDialog" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <!-- Part Number -->
            <v-col cols="12" md="6">
              <v-autocomplete
                v-model="form.selectedPartId"
                v-model:search="partSearch"
                :items="partSuggestions"
                item-title="name"
                item-value="id"
                label="Part Number *"
                variant="outlined"
                hide-details="auto"
                :loading="partSearchLoading"
                no-data-text="Type to search part numbers..."
                clearable
                @update:search="onPartSearch"
              />
            </v-col>
            <!-- Description -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.description"
                label="Description"
                variant="outlined"
                hide-details
              />
            </v-col>
            <!-- Alt Part Number -->
            <v-col cols="12" md="6">
              <v-combobox
                v-model="form.altPartNumber"
                :items="altSuggestions"
                label="Alt Part Number"
                variant="outlined"
                hide-details
                clearable
                hint="Type or select from known alternatives"
              />
            </v-col>
            <!-- Condition -->
            <v-col cols="12" md="6">
              <v-select
                v-model="form.condition"
                :items="conditionOptions"
                label="Condition"
                variant="outlined"
                hide-details
                clearable
              />
            </v-col>
            <!-- Price -->
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="form.price"
                label="Price ($)"
                variant="outlined"
                hide-details
                type="number"
                step="0.01"
                min="0"
                prefix="$"
              />
            </v-col>
            <!-- Qty -->
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="form.qty"
                label="Qty"
                variant="outlined"
                hide-details
                type="number"
                min="0"
              />
            </v-col>
            <!-- Cert Type -->
            <v-col cols="12" md="4">
              <v-select
                v-model="form.certName"
                :items="certOptions"
                label="Cert Type"
                variant="outlined"
                hide-details
                clearable
              />
            </v-col>
            <!-- Tag Date -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.tagDate"
                label="Tag Date"
                variant="outlined"
                hide-details
                type="date"
                :max="today"
              />
            </v-col>
            <!-- Lead Time -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.leadTime"
                label="Lead Time"
                variant="outlined"
                hide-details
                placeholder="e.g. 5 days"
              />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <v-spacer />
          <v-btn variant="text" @click="closeDialog">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="saving"
            :disabled="!form.selectedPartId"
            @click="saveItem"
          >
            {{ isEditing ? 'Update' : 'Add' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- AR Shop Import Dialog -->
    <v-dialog v-model="showARDialog" max-width="900" scrollable>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-wrench" class="mr-2" color="warning" size="20" />
          Import AR Shops to ILS
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showARDialog = false" />
        </v-card-title>
        <v-card-subtitle class="px-4 pb-2">
          Select shop records (AR parts being fixed) to add to the ILS inventory.
        </v-card-subtitle>
        <v-divider />
        <v-card-text class="pa-4" style="max-height: 500px;">
          <div v-if="arShopsLoading" class="text-center pa-6">
            <v-progress-circular indeterminate color="warning" />
          </div>
          <div v-else-if="arShops.length === 0" class="text-center pa-6">
            <v-icon icon="mdi-wrench-outline" size="48" color="grey-darken-1" class="mb-3" />
            <p class="text-body-2 text-medium-emphasis">No AR shop records found.</p>
            <p class="text-caption text-medium-emphasis">Add Shop records to AR-condition supplier quotes in the Procument or RFQ pages.</p>
          </div>
          <template v-else>
            <v-text-field
              v-model="arShopSearch"
              prepend-inner-icon="mdi-magnify"
              label="Search shops..."
              density="compact"
              hide-details
              class="mb-3"
            />
            <div class="d-flex align-center gap-2 mb-3">
              <v-btn size="x-small" variant="tonal" color="primary" @click="selectAllShops">Select All</v-btn>
              <v-btn size="x-small" variant="tonal" @click="selectedARShops.clear(); selectedARShops = new Set(selectedARShops)">Clear</v-btn>
              <span class="text-caption text-medium-emphasis">{{ selectedARShops.size }} selected</span>
            </div>
            <div class="ar-shops-list">
              <div
                v-for="shop in filteredARShops"
                :key="shop.procumentRecordId"
                class="ar-shop-card"
                :class="{ 'ar-shop-card--selected': selectedARShops.has(shop.procumentRecordId) }"
                @click="toggleARShop(shop.procumentRecordId)"
              >
                <div class="d-flex align-center gap-3">
                  <v-checkbox
                    :model-value="selectedARShops.has(shop.procumentRecordId)"
                    hide-details
                    density="compact"
                    color="warning"
                    @click.stop
                    @update:model-value="toggleARShop(shop.procumentRecordId)"
                  />
                  <div class="flex-grow-1 min-width-0">
                    <div class="d-flex align-center gap-2 flex-wrap mb-1">
                      <span class="font-weight-bold text-body-2" style="font-family: monospace;">{{ shop.partNumberName }}</span>
                      <span v-if="shop.altPartNumber" class="text-caption" style="color: #fbbf24;">{{ shop.altPartNumber }}</span>
                      <v-chip size="x-small" color="warning" variant="tonal">AR</v-chip>
                      <span class="text-caption text-medium-emphasis">RFQ #{{ shop.rfqId }} · {{ shop.rfqName }}</span>
                    </div>
                    <div class="d-flex flex-wrap gap-3 text-caption">
                      <span><strong>Shop:</strong> {{ shop.supplierName }}</span>
                      <span class="text-green"><strong>Cost:</strong> ${{ formatPrice(shop.price) }}</span>
                      <span v-if="shop.fixPrice" style="color: #ff9800;"><strong>Fix:</strong> ${{ formatPrice(shop.fixPrice) }}</span>
                      <span><strong>Qty:</strong> {{ shop.qty }}</span>
                      <span v-if="shop.certName"><strong>Cert:</strong> {{ shop.certName }}</span>
                      <span v-if="shop.leadTime"><strong>LT:</strong> {{ shop.leadTime }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <span class="text-caption text-medium-emphasis">{{ selectedARShops.size }} shop(s) will be added to ILS</span>
          <v-spacer />
          <v-btn variant="text" @click="showARDialog = false">Cancel</v-btn>
          <v-btn
            color="warning"
            variant="flat"
            prepend-icon="mdi-import"
            :loading="importingShops"
            :disabled="selectedARShops.size === 0"
            @click="importARShops"
          >
            Import Selected
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirm -->
    <v-dialog v-model="showDeleteConfirm" max-width="400">
      <v-card>
        <v-card-title class="pa-4 pb-2">Delete ILS Item?</v-card-title>
        <v-card-text>
          Remove <strong>{{ deleteTarget?.partNumberName }}</strong> from ILS? This cannot be undone.
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showDeleteConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleteSaving" @click="doDelete">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Excel Import Dialog -->
    <v-dialog v-model="showExcelDialog" max-width="900" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-file-excel" class="mr-2" color="success" size="20" />
          Excel Import - ILS Inventory
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeExcelDialog" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-tabs v-model="excelTab" class="mb-4">
            <v-tab value="file">
              <v-icon start>mdi-upload</v-icon>Upload File
            </v-tab>
            <v-tab value="paste">
              <v-icon start>mdi-content-paste</v-icon>Paste from Excel
            </v-tab>
          </v-tabs>

          <v-alert type="info" variant="tonal" class="mb-4" density="compact">
            <strong>Expected columns (in order):</strong> PartNumber | Description | AltPartNumber | Price | QTY | Condition | TagDate | CertType | LeadTime
          </v-alert>

          <div v-if="excelTab === 'file'">
            <v-file-input
              v-model="excelFile"
              label="Select Excel file (.xlsx, .csv)"
              accept=".xlsx,.xls,.csv"
              variant="outlined"
              prepend-icon="mdi-file-excel"
              @change="handleFileUpload"
            />
          </div>

          <div v-if="excelTab === 'paste'">
            <v-textarea
              v-model="pasteText"
              label="Paste Excel data here (Ctrl+V from Excel)"
              variant="outlined"
              rows="6"
              hint="Copy cells from Excel and paste here"
              persistent-hint
              placeholder="PartNumber&#9;Description&#9;AltPartNumber&#9;Price&#9;QTY&#9;Condition&#9;TagDate&#9;CertType&#9;LeadTime"
              @input="handlePasteInput"
            />
          </div>

          <div v-if="importPreview.length > 0" class="mt-4">
            <div class="d-flex align-center gap-2 mb-2">
              <span class="text-subtitle-2">Preview ({{ importPreview.length }} rows)</span>
              <v-spacer />
              <v-btn size="x-small" variant="text" color="error" @click="importPreview = []">Clear Preview</v-btn>
            </div>
            <div class="preview-table-wrapper">
              <table class="preview-table">
                <thead>
                  <tr>
                    <th>#</th><th>Part Number</th><th>Description</th><th>Alt P/N</th>
                    <th>Price</th><th>QTY</th><th>Condition</th><th>Tag Date</th><th>Cert</th><th>Lead Time</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, i) in importPreview.slice(0, 20)" :key="i">
                    <td class="text-medium-emphasis">{{ i + 1 }}</td>
                    <td class="font-weight-medium" style="font-family: monospace;">{{ row.partNumberName }}</td>
                    <td>{{ row.description || '—' }}</td>
                    <td>{{ row.altPartNumber || '—' }}</td>
                    <td>{{ row.price ? '$' + row.price : '—' }}</td>
                    <td>{{ row.qty }}</td>
                    <td>{{ row.condition || '—' }}</td>
                    <td>{{ row.tagDate || '—' }}</td>
                    <td>{{ row.certName || '—' }}</td>
                    <td>{{ row.leadTime || '—' }}</td>
                  </tr>
                </tbody>
              </table>
              <p v-if="importPreview.length > 20" class="text-caption text-medium-emphasis mt-1">
                + {{ importPreview.length - 20 }} more rows not shown
              </p>
            </div>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <span class="text-caption text-medium-emphasis">{{ importPreview.length }} rows ready to import</span>
          <v-spacer />
          <v-btn variant="text" @click="closeExcelDialog">Cancel</v-btn>
          <v-btn
            color="success"
            variant="flat"
            prepend-icon="mdi-import"
            :loading="importingExcel"
            :disabled="importPreview.length === 0"
            @click="doExcelImport"
          >
            Import {{ importPreview.length }} Rows
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
import * as XLSX from 'xlsx'

const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const today = new Date().toISOString().split('T')[0]

// ── State ──
const loading = ref(false)
const saving = ref(false)
const deleteSaving = ref(false)
const allItems = ref<any[]>([])
const search = ref('')
const conditionFilter = ref<string[]>([])
const certFilter = ref<string[]>([])

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Options ──
const conditionOptions = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']
const certOptions = ['FAA', 'EASA', 'CAAC', 'Dual', 'MFG COC', 'Vendor COC', 'No Cert']

// ── Filters ──
const hasActiveFilters = computed(() => conditionFilter.value.length > 0 || certFilter.value.length > 0)

function clearFilters() {
  conditionFilter.value = []
  certFilter.value = []
  search.value = ''
}

const filteredItems = computed(() => {
  let result = allItems.value
  if (conditionFilter.value.length) {
    result = result.filter(i => conditionFilter.value.includes(i.condition))
  }
  if (certFilter.value.length) {
    result = result.filter(i => certFilter.value.includes(i.certName))
  }
  return result
})

// ── Table Headers ──
const headers = [
  { title: 'Part Number', key: 'partNumberName', width: '140px' },
  { title: 'Description', key: 'description' },
  { title: 'Alt P/N', key: 'altPartNumber', width: '130px' },
  { title: 'Price', key: 'price', width: '100px' },
  { title: 'Qty', key: 'qty', width: '70px' },
  { title: 'Condition', key: 'condition', width: '100px' },
  { title: 'Cert', key: 'certName', width: '110px' },
  { title: 'Tag Date', key: 'tagDate', width: '110px' },
  { title: 'Lead Time', key: 'leadTime', width: '110px' },
  { title: 'Source', key: 'procumentRecordId', width: '110px', sortable: false },
  { title: '', key: 'actions', width: '80px', sortable: false },
]

// ── Condition color helper ──
function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error',
    RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

// ── Data Loading ──
onMounted(loadItems)

async function loadItems() {
  loading.value = true
  try {
    allItems.value = await api.get<any[]>('/ils')
  } catch {
    showSnack('Failed to load ILS items', 'error')
  } finally {
    loading.value = false
  }
}

// ── Part Number Search ──
const partSearch = ref('')
const partSuggestions = ref<{ id: number; name: string }[]>([])
const partSearchLoading = ref(false)
let partSearchDebounce: any = null
const altSuggestions = ref<string[]>([])

function onPartSearch(val: string) {
  clearTimeout(partSearchDebounce)
  if (!val || val.length < 2) { partSuggestions.value = []; return }
  partSearchLoading.value = true
  partSearchDebounce = setTimeout(async () => {
    try {
      partSuggestions.value = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(val)}`)
    } catch {
      partSuggestions.value = []
    } finally {
      partSearchLoading.value = false
    }
  }, 300)
}

async function loadAltSuggestions(partNumberId: number) {
  try {
    const alts = await api.get<any[]>(`/partnumbers/${partNumberId}/alternatives`)
    altSuggestions.value = (alts || []).map((a: any) => a.name)
  } catch {
    altSuggestions.value = []
  }
}

// ── Add / Edit Dialog ──
const showDialog = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)

const defaultForm = () => ({
  selectedPartId: null as number | null,
  description: '',
  altPartNumber: '',
  price: 0,
  qty: 1,
  condition: '' as string,
  certName: '' as string,
  tagDate: '',
  leadTime: '',
  procumentRecordId: null as number | null,
})

const form = ref(defaultForm())

function openAddDialog() {
  isEditing.value = false
  editingId.value = null
  form.value = defaultForm()
  partSearch.value = ''
  partSuggestions.value = []
  altSuggestions.value = []
  showDialog.value = true
}

function openEditDialog(item: any) {
  isEditing.value = true
  editingId.value = item.id
  form.value = {
    selectedPartId: item.partNumberId,
    description: item.description || '',
    altPartNumber: item.altPartNumber || '',
    price: Number(item.price),
    qty: Number(item.qty),
    condition: item.condition || '',
    certName: item.certName || '',
    tagDate: item.tagDate ? new Date(item.tagDate).toISOString().split('T')[0] : '',
    leadTime: item.leadTime || '',
    procumentRecordId: item.procumentRecordId || null,
  }
  // Pre-fill the part search with current value
  partSearch.value = item.partNumberName
  partSuggestions.value = [{ id: item.partNumberId, name: item.partNumberName }]
  loadAltSuggestions(item.partNumberId)
  showDialog.value = true
}

// Watch selected part to load alt suggestions
watch(() => form.value.selectedPartId, (id) => {
  if (id) loadAltSuggestions(id)
})

function closeDialog() {
  showDialog.value = false
}

async function saveItem() {
  if (!form.value.selectedPartId) return
  saving.value = true
  try {
    const tagDate = form.value.tagDate
      ? form.value.tagDate  // ISO date string (YYYY-MM-DD) - backend accepts DateOnly
      : null

    const payload = {
      id: isEditing.value ? editingId.value : null,
      partNumberId: form.value.selectedPartId,
      description: form.value.description || null,
      altPartNumber: form.value.altPartNumber || null,
      price: form.value.price,
      qty: form.value.qty,
      condition: form.value.condition || null,
      tagDate,
      certName: form.value.certName || null,
      leadTime: form.value.leadTime || null,
      procumentRecordId: form.value.procumentRecordId || null,
    }

    const saved = await api.post<any>('/ils', payload)

    if (isEditing.value) {
      const idx = allItems.value.findIndex(i => i.id === editingId.value)
      if (idx >= 0) allItems.value[idx] = saved
    } else {
      allItems.value.unshift(saved)
    }

    showSnack(isEditing.value ? 'Item updated' : 'Item added', 'success')
    closeDialog()
  } catch {
    showSnack('Failed to save item', 'error')
  } finally {
    saving.value = false
  }
}

// ── Delete ──
const showDeleteConfirm = ref(false)
const deleteTarget = ref<any>(null)

function confirmDelete(item: any) {
  deleteTarget.value = item
  showDeleteConfirm.value = true
}

async function doDelete() {
  if (!deleteTarget.value) return
  deleteSaving.value = true
  try {
    await api.del(`/ils/${deleteTarget.value.id}`)
    allItems.value = allItems.value.filter(i => i.id !== deleteTarget.value.id)
    showSnack('Item deleted', 'success')
    showDeleteConfirm.value = false
    deleteTarget.value = null
  } catch {
    showSnack('Failed to delete item', 'error')
  } finally {
    deleteSaving.value = false
  }
}

// ── AR Shop Import ──
const showARDialog = ref(false)
const arShopsLoading = ref(false)
const arShops = ref<any[]>([])
const arShopSearch = ref('')
const selectedARShops = ref<Set<number>>(new Set())
const importingShops = ref(false)

const filteredARShops = computed(() => {
  if (!arShopSearch.value.trim()) return arShops.value
  const q = arShopSearch.value.trim().toLowerCase()
  return arShops.value.filter(s =>
    s.partNumberName.toLowerCase().includes(q) ||
    s.supplierName.toLowerCase().includes(q) ||
    (s.altPartNumber || '').toLowerCase().includes(q) ||
    s.rfqName.toLowerCase().includes(q)
  )
})

async function openARShopDialog() {
  showARDialog.value = true
  arShopSearch.value = ''
  selectedARShops.value = new Set()
  arShopsLoading.value = true
  try {
    arShops.value = await api.get<any[]>('/ils/ar-shop-suggestions')
  } catch {
    showSnack('Failed to load AR shop suggestions', 'error')
  } finally {
    arShopsLoading.value = false
  }
}

function toggleARShop(id: number) {
  if (selectedARShops.value.has(id)) {
    selectedARShops.value.delete(id)
  } else {
    selectedARShops.value.add(id)
  }
  selectedARShops.value = new Set(selectedARShops.value)
}

function selectAllShops() {
  selectedARShops.value = new Set(filteredARShops.value.map((s: any) => s.procumentRecordId))
}

// ── Excel Import ──
const showExcelDialog = ref(false)
const excelTab = ref<'file' | 'paste'>('file')
const excelFile = ref<File[]>([])
const pasteText = ref('')
const importPreview = ref<any[]>([])
const importingExcel = ref(false)

function openExcelImport() {
  showExcelDialog.value = true
  excelTab.value = 'file'
  excelFile.value = []
  pasteText.value = ''
  importPreview.value = []
}

function closeExcelDialog() {
  showExcelDialog.value = false
  importPreview.value = []
  pasteText.value = ''
  excelFile.value = []
}

function parseILSRows(rows: any[][]) {
  let dataRows = rows
  if (rows.length > 0 && typeof rows[0][0] === 'string' &&
      rows[0][0].toLowerCase().replace(/\s/g, '').includes('part')) {
    dataRows = rows.slice(1)
  }
  return dataRows
    .filter(row => row.some(c => c != null && String(c).trim() !== ''))
    .map(row => {
      const priceRaw = String(row[3] ?? '').replace(/[$,\s]/g, '')
      return {
        partNumberName: String(row[0] ?? '').trim(),
        description: String(row[1] ?? '').trim() || null,
        altPartNumber: String(row[2] ?? '').trim() || null,
        price: parseFloat(priceRaw) || 0,
        qty: parseFloat(String(row[4] ?? '0')) || 0,
        condition: String(row[5] ?? '').trim().toUpperCase() || null,
        tagDate: String(row[6] ?? '').trim() || null,
        certName: String(row[7] ?? '').trim() || null,
        leadTime: String(row[8] ?? '').trim() || null,
      }
    })
    .filter(row => row.partNumberName)
}

function handleFileUpload(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = (e) => {
    const data = new Uint8Array(e.target!.result as ArrayBuffer)
    const workbook = XLSX.read(data, { type: 'array' })
    const worksheet = workbook.Sheets[workbook.SheetNames[0]]
    const rows = XLSX.utils.sheet_to_json<any[]>(worksheet, { header: 1 })
    importPreview.value = parseILSRows(rows)
  }
  reader.readAsArrayBuffer(file)
}

function handlePasteInput() {
  if (!pasteText.value.trim()) { importPreview.value = []; return }
  const rows = pasteText.value.trim().split('\n').map(line => line.split('\t'))
  importPreview.value = parseILSRows(rows)
}

async function doExcelImport() {
  if (!importPreview.value.length) return
  importingExcel.value = true
  try {
    const result = await api.post<any>('/ils/bulk-import', { rows: importPreview.value })
    showSnack(`Imported ${result.created} rows${result.skipped > 0 ? `, skipped ${result.skipped}` : ''}`)
    if (result.errors?.length) console.warn('Import errors:', result.errors)
    await loadItems()
    closeExcelDialog()
  } catch {
    showSnack('Import failed', 'error')
  } finally {
    importingExcel.value = false
  }
}

async function importARShops() {
  const toImport = arShops.value.filter((s: any) => selectedARShops.value.has(s.procumentRecordId))
  if (!toImport.length) return

  importingShops.value = true
  try {
    for (const shop of toImport) {
      const payload = {
        id: null,
        partNumberId: shop.partNumberId,
        description: null,
        altPartNumber: shop.altPartNumber || null,
        price: shop.fixPrice ?? shop.price,
        qty: shop.qty,
        condition: 'AR',
        tagDate: shop.tagDate || null,
        certName: shop.certName || null,
        leadTime: shop.leadTime || null,
        procumentRecordId: shop.procumentRecordId,
      }
      const saved = await api.post<any>('/ils', payload)
      allItems.value.unshift(saved)
    }
    showSnack(`${toImport.length} shop(s) imported to ILS`, 'success')
    showARDialog.value = false
  } catch {
    showSnack('Failed to import shops', 'error')
  } finally {
    importingShops.value = false
  }
}
</script>

<style scoped>
.preview-table-wrapper { overflow-x: auto; max-height: 260px; overflow-y: auto; }
.preview-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.preview-table th { background: rgba(var(--v-theme-surface-variant), 0.5); padding: 6px 10px; text-align: left; font-weight: 600; position: sticky; top: 0; }
.preview-table td { padding: 5px 10px; border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.06); white-space: nowrap; }
.preview-table tr:hover td { background: rgba(var(--v-theme-surface-variant), 0.3); }

.ar-shops-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.ar-shop-card {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  border-radius: 8px;
  padding: 10px 12px;
  cursor: pointer;
  transition: all 0.15s;
}
.ar-shop-card:hover {
  background: rgba(var(--v-theme-surface-variant), 0.4);
  border-color: rgba(255, 152, 0, 0.4);
}
.ar-shop-card--selected {
  background: rgba(255, 152, 0, 0.08) !important;
  border-color: #ff9800 !important;
}
</style>
