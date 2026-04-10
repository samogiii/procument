<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">Inventory</h1>
      <v-spacer />
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
          <v-btn
            v-if="search || conditionFilter.length"
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

        <!-- Tabs -->
        <v-tabs v-model="activeTab" class="mb-4">
          <v-tab value="all">
            <v-icon start>mdi-format-list-bulleted</v-icon>
            Inventory
            <v-chip size="x-small" class="ml-2" color="primary" variant="tonal">{{ allItems.length }}</v-chip>
          </v-tab>
          <v-tab value="suppliers">
            <v-icon start>mdi-factory</v-icon>
            Suppliers
            <v-chip size="x-small" class="ml-2" color="info" variant="tonal">{{ uniqueSuppliers.length }}</v-chip>
          </v-tab>
        </v-tabs>

        <v-data-table
          v-if="activeTab !== 'suppliers'"
          :headers="headers"
          :items="filteredItems"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
          @click:row="onRowClick"
        >
          <template #item.price="{ item }">
            <span v-if="item.price != null" class="font-weight-medium" style="font-family: monospace; color: #4ade80;">
              ${{ formatPrice(item.price) }}
            </span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.condition="{ item }">
            <v-chip v-if="item.condition" size="small" variant="tonal" :color="conditionColor(item.condition)">
              {{ item.condition }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.qty="{ item }">
            <span class="font-weight-medium">{{ item.qty }}</span>
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click.stop="openEditDialog(item)" />
            <v-btn
              v-if="isAdmin"
              icon="mdi-delete"
              size="x-small"
              variant="text"
              color="error"
              @click.stop="confirmDelete(item)"
            />
          </template>
        </v-data-table>

        <!-- Suppliers Table -->
        <v-data-table
          v-if="activeTab === 'suppliers'"
          :headers="supplierListHeaders"
          :items="filteredSuppliers"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
          @click:row="onSupplierRowClick"
        >
          <template #item.totalItems="{ item }">
            <v-chip size="small" color="primary" variant="tonal">{{ item.totalItems }}</v-chip>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Add / Edit Dialog -->
    <v-dialog v-model="showDialog" max-width="680" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon :icon="isEditing ? 'mdi-pencil' : 'mdi-plus'" class="mr-2" size="20" />
          {{ isEditing ? 'Edit Inventory Item' : 'Add Inventory Item' }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeDialog" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <!-- Part Number -->
            <v-col cols="12" md="6">
              <v-combobox
                v-model="form.partNumber"
                v-model:search="partSearch"
                :items="partSuggestions"
                item-title="name"
                item-value="id"
                label="Part Number *"
                density="compact"
                variant="outlined"
                hide-details
                no-filter
                clearable
                return-object
                :loading="partSearchLoading"
                @update:search="onPartSearch"
                @update:model-value="onPartPicked"
              >
                <template #item="{ item: suggestion, props: itemProps }">
                  <v-list-item v-bind="itemProps">
                    <template #subtitle>
                      <span v-if="suggestion.raw.description">{{ suggestion.raw.description }}</span>
                      <span v-else class="font-italic text-medium-emphasis">No description</span>
                    </template>
                  </v-list-item>
                </template>
                <template #no-data>
                  <v-list-item>
                    <v-list-item-title v-if="partSearch.length < 3" class="text-medium-emphasis text-caption">
                      Type 3+ chars to search...
                    </v-list-item-title>
                    <v-list-item-title v-else class="text-medium-emphasis text-caption">
                      "{{ partSearch }}" — will be created
                    </v-list-item-title>
                  </v-list-item>
                </template>
              </v-combobox>
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
            <!-- Company -->
            <v-col cols="12" md="6">
              <v-combobox
                v-model="form.company"
                v-model:search="companySearch"
                :items="companySuggestions"
                item-title="name"
                item-value="id"
                label="Company *"
                density="compact"
                variant="outlined"
                hide-details
                no-filter
                clearable
                return-object
                :loading="companySearchLoading"
                @update:search="onCompanySearch"
                @update:model-value="onCompanyPicked"
              >
                <template #no-data>
                  <v-list-item>
                    <v-list-item-title v-if="companySearch.length < 2" class="text-medium-emphasis text-caption">
                      Type 2+ chars to search...
                    </v-list-item-title>
                    <v-list-item-title v-else class="text-medium-emphasis text-caption">
                      "{{ companySearch }}" — will be created
                    </v-list-item-title>
                  </v-list-item>
                </template>
              </v-combobox>
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
            <!-- Qty -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model.number="form.qty"
                label="Qty *"
                variant="outlined"
                hide-details
                type="number"
                min="0"
                step="1"
              />
            </v-col>
            <!-- Price -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model.number="form.price"
                label="Price ($)"
                variant="outlined"
                hide-details
                type="number"
                step="0.01"
                min="0"
                prefix="$"
                clearable
              />
            </v-col>
            <!-- Serial Number -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.serialNumber"
                label="Serial Number"
                variant="outlined"
                hide-details
                clearable
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
            :disabled="!form.partNumber || !form.company"
            @click="saveItem"
          >
            {{ isEditing ? 'Update' : 'Add' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Excel Import Dialog -->
    <v-dialog v-model="showExcelDialog" max-width="900" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-file-excel" class="mr-2" color="success" size="20" />
          Excel Import - Inventory
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
            <strong>Expected columns (in order):</strong> PartNumber | Description | Qty | Company | Condition | Price | SerialNumber
          </v-alert>

          <!-- File Upload -->
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

          <!-- Paste -->
          <div v-if="excelTab === 'paste'">
            <v-textarea
              v-model="pasteText"
              label="Paste Excel data here (Ctrl+V from Excel)"
              variant="outlined"
              rows="6"
              hint="Copy cells from Excel and paste here"
              persistent-hint
              placeholder="PartNumber&#9;Description&#9;Qty&#9;Company&#9;Condition&#9;Price"
              @input="handlePasteInput"
            />
          </div>

          <!-- Preview -->
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
                    <th>#</th>
                    <th>Part Number</th>
                    <th>Description</th>
                    <th>Qty</th>
                    <th>Company</th>
                    <th>Condition</th>
                    <th>Price</th>
                    <th>Serial Number</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, i) in importPreview.slice(0, 20)" :key="i">
                    <td class="text-medium-emphasis">{{ i + 1 }}</td>
                    <td class="font-weight-medium" style="font-family: monospace;">{{ row.partNumberName }}</td>
                    <td>{{ row.description || '—' }}</td>
                    <td>{{ row.qty }}</td>
                    <td>{{ row.companyName || '—' }}</td>
                    <td>
                      <v-chip v-if="row.condition" size="x-small" :color="conditionColor(row.condition)" variant="tonal">
                        {{ row.condition }}
                      </v-chip>
                      <span v-else class="text-medium-emphasis">—</span>
                    </td>
                    <td>{{ row.price != null ? '$' + row.price : '—' }}</td>
                    <td>{{ row.serialNumber || '—' }}</td>
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

    <!-- Supplier Details Dialog -->
    <v-dialog v-model="showSupplierDialog" max-width="900" scrollable>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-factory" class="mr-2" color="primary" size="20" />
          {{ selectedSupplier?.companyName }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showSupplierDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-data-table
            :headers="headers"
            :items="filteredSupplierItems"
            :loading="loading"
            :items-per-page="10"
            hover
            density="compact"
          >
            <template #item.price="{ item }">
              <span v-if="item.price != null" class="font-weight-medium" style="font-family: monospace; color: #4ade80;">
                ${{ formatPrice(item.price) }}
              </span>
              <span v-else class="text-medium-emphasis">—</span>
            </template>
            <template #item.condition="{ item }">
              <v-chip v-if="item.condition" size="x-small" variant="tonal" :color="conditionColor(item.condition)">
                {{ item.condition }}
              </v-chip>
              <span v-else class="text-medium-emphasis">—</span>
            </template>
          </v-data-table>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <v-spacer />
          <v-btn variant="text" @click="showSupplierDialog = false">Close</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirm -->
    <v-dialog v-model="showDeleteConfirm" max-width="400">
      <v-card>
        <v-card-title class="pa-4 pb-2">Delete Inventory Item?</v-card-title>
        <v-card-text>
          Remove <strong>{{ deleteTarget?.partNumberName }}</strong> from Inventory?
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showDeleteConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleteSaving" @click="doDelete">Delete</v-btn>
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

interface InventoryForm {
  partNumber: { id: number; name: string; description?: string } | string | null
  description: string
  qty: number
  company: { id: number; name: string } | string | null
  condition: string
  price: number | null
  serialNumber: string | null
}

// ── State ──
const loading = ref(false)
const saving = ref(false)
const deleteSaving = ref(false)
const allItems = ref<any[]>([])
const search = ref('')
const conditionFilter = ref<string[]>([])
const activeTab = ref('all')

// Supplier Details Dialog State
const showSupplierDialog = ref(false)
const selectedSupplier = ref<any>(null)

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

const conditionOptions = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']

function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error',
    RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

function formatPrice(val: number | null) {
  if (val == null) return '—'
  return Number(val).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function clearFilters() {
  search.value = ''
  conditionFilter.value = []
}

const filteredItems = computed(() => {
  if (activeTab.value === 'suppliers') return uniqueSuppliers.value
  let result = allItems.value
  if (conditionFilter.value.length) {
    result = result.filter(i => conditionFilter.value.includes(i.condition))
  }
  if (search.value.trim()) {
    const q = search.value.toLowerCase()
    result = result.filter(i =>
      (i.partNumberName || '').toLowerCase().includes(q) ||
      (i.companyName || '').toLowerCase().includes(q) ||
      (i.description || '').toLowerCase().includes(q)
    )
  }
  return result
})

const uniqueSuppliers = computed(() => {
  const supplierMap = new Map<number, any>()
  allItems.value.forEach(item => {
    if (item.companyId && !supplierMap.has(item.companyId)) {
      const items = allItems.value.filter(i => i.companyId === item.companyId)
      supplierMap.set(item.companyId, {
        companyId: item.companyId,
        companyName: item.companyName,
        totalItems: items.length,
      })
    }
  })
  return Array.from(supplierMap.values())
})

const filteredSuppliers = computed(() => {
  if (!search.value.trim()) return uniqueSuppliers.value
  const q = search.value.toLowerCase()
  return uniqueSuppliers.value.filter(s =>
    (s.companyName || '').toLowerCase().includes(q)
  )
})

// Supplier Details Computed
const supplierItems = computed(() => {
  if (!selectedSupplier.value?.companyId) return []
  return allItems.value.filter(i => i.companyId === selectedSupplier.value.companyId)
})

const filteredSupplierItems = computed(() => supplierItems.value)

const headers = [
  { title: 'Part Number', key: 'partNumberName', width: '150px' },
  { title: 'Description', key: 'description' },
  { title: 'Serial Number', key: 'serialNumber', width: '150px' },
  { title: 'Qty', key: 'qty', width: '80px' },
  { title: 'Company', key: 'companyName', width: '180px' },
  { title: 'Condition', key: 'condition', width: '110px' },
  { title: 'Price', key: 'price', width: '110px' },
  { title: '', key: 'actions', width: '80px', sortable: false },
]

const supplierListHeaders = [
  { title: 'Company', key: 'companyName', width: '250px' },
  { title: 'Total Items', key: 'totalItems', width: '120px' },
]

onMounted(loadItems)

async function loadItems() {
  loading.value = true
  try {
    allItems.value = await api.get<any[]>('/inventory')
  } catch {
    showSnack('Failed to load inventory', 'error')
  } finally {
    loading.value = false
  }
}

// ── Row Click Handlers ──
function onRowClick(event: any, item: any) {
  selectedSupplier.value = {
    companyId: item.item.companyId,
    companyName: item.item.companyName,
  }
  showSupplierDialog.value = true
}

function onSupplierRowClick(event: any, item: any) {
  selectedSupplier.value = {
    companyId: item.item.companyId,
    companyName: item.item.companyName,
  }
  showSupplierDialog.value = true
}

// ── Part Number Search ──
const partSearch = ref('')
const partSuggestions = ref<{ id: number; name: string; description?: string }[]>([])
const partSearchLoading = ref(false)
let partSearchDebounce: any = null

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

// ── Company Search ──
const companySearch = ref('')
const companySuggestions = ref<{ id: number; name: string }[]>([])
const companySearchLoading = ref(false)
let companySearchDebounce: any = null

function onCompanySearch(val: string) {
  clearTimeout(companySearchDebounce)
  if (!val || val.length < 2) { companySuggestions.value = []; return }
  companySearchLoading.value = true
  companySearchDebounce = setTimeout(async () => {
    try {
      companySuggestions.value = await api.get<any[]>(`/suppliers/search?q=${encodeURIComponent(val)}`)
    } catch {
      companySuggestions.value = []
    } finally {
      companySearchLoading.value = false
    }
  }, 300)
}

// ── Add / Edit Dialog ──
const showDialog = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)

const defaultForm = () => ({
  partNumber: null as any,
  description: '',
  qty: 1,
  company: null as any,
  condition: '' as string,
  price: null as number | null,
  serialNumber: null as string | null,
})

const form = ref(defaultForm())

function openAddDialog() {
  isEditing.value = false
  editingId.value = null
  form.value = defaultForm()
  partSearch.value = ''
  partSuggestions.value = []
  companySearch.value = ''
  companySuggestions.value = []
  showDialog.value = true
}

function openEditDialog(item: any) {
  isEditing.value = true
  editingId.value = item.id
  form.value = {
    partNumber: { id: item.partNumberId, name: item.partNumberName, description: item.description },
    description: item.description || '',
    qty: item.qty,
    company: { id: item.companyId, name: item.companyName },
    condition: item.condition || '',
    price: item.price != null ? Number(item.price) : null,
    serialNumber: item.serialNumber || null,
  }
  partSearch.value = item.partNumberName
  partSuggestions.value = [{ id: item.partNumberId, name: item.partNumberName, description: item.description }]
  companySearch.value = item.companyName
  companySuggestions.value = [{ id: item.companyId, name: item.companyName }]
  showDialog.value = true
}

function closeDialog() {
  showDialog.value = false
}

function onPartPicked(val: any) {
  if (val && typeof val === 'object' && val.id) {
    // Existing part selected - auto-fill description
    if (val.description && !form.value.description) {
      form.value.description = val.description
    }
  }
}

function onCompanyPicked(val: any) {
  // Just store the selection, no auto-fill needed
}

async function saveItem() {
  if (!form.value.partNumber || !form.value.company) return
  saving.value = true
  try {
    let partId: number | null = null
    let partName: string | null = null
    let companyId: number | null = null
    let companyName: string | null = null

    if (typeof form.value.partNumber === 'object' && form.value.partNumber.id) {
      partId = form.value.partNumber.id
      partName = form.value.partNumber.name || null
    } else if (typeof form.value.partNumber === 'string' && form.value.partNumber.trim()) {
      partName = form.value.partNumber.trim()
    }

    if (typeof form.value.company === 'object' && form.value.company.id) {
      companyId = form.value.company.id
      companyName = form.value.company.name || null
    } else if (typeof form.value.company === 'string' && form.value.company.trim()) {
      companyName = form.value.company.trim()
    }

    const payload = {
      id: isEditing.value ? editingId.value : null,
      partNumberId: partId,
      partNumberName: partName,
      description: form.value.description || null,
      qty: form.value.qty,
      companyId: companyId,
      companyName: companyName,
      condition: form.value.condition || null,
      price: form.value.price != null ? form.value.price : null,
      serialNumber: form.value.serialNumber || null,
    }
    const saved = await api.post<any>('/inventory', payload)
    if (isEditing.value) {
      const idx = allItems.value.findIndex(i => i.id === editingId.value)
      if (idx >= 0) allItems.value[idx] = saved
    } else {
      allItems.value.unshift(saved)
    }
    showSnack(isEditing.value ? 'Item updated' : 'Item added')
    closeDialog()
  } catch (e: any) {
    console.error('Save error:', e)
    const errorMsg = e?.data?.error || e?.data?.message || 'Failed to save item'
    showSnack(errorMsg, 'error')
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
    await api.del(`/inventory/${deleteTarget.value.id}`)
    allItems.value = allItems.value.filter(i => i.id !== deleteTarget.value.id)
    showSnack('Item deleted')
    showDeleteConfirm.value = false
  } catch {
    showSnack('Failed to delete', 'error')
  } finally {
    deleteSaving.value = false
  }
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

function parseRows(rows: any[][]) {
  let dataRows = rows
  if (rows.length > 0 && rows[0] && rows[0][0] && typeof rows[0][0] === 'string' &&
      rows[0][0].toLowerCase().replace(/\s/g, '').includes('part')) {
    dataRows = rows.slice(1)
  }
  return dataRows
    .filter(row => row && row.some(c => c != null && String(c).trim() !== ''))
    .map(row => {
      const priceRaw = String(row[5] ?? '').replace(/[$,\s]/g, '')
      const price = priceRaw ? parseFloat(priceRaw) : null
      return {
        partNumberName: String(row[0] ?? '').trim(),
        description: String(row[1] ?? '').trim() || null,
        qty: parseFloat(String(row[2] ?? '0')) || 0,
        companyName: String(row[3] ?? '').trim() || null,
        condition: String(row[4] ?? '').trim().toUpperCase() || null,
        price: isNaN(price!) ? null : price,
        serialNumber: String(row[6] ?? '').trim() || null,
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
    importPreview.value = parseRows(rows)
  }
  reader.readAsArrayBuffer(file)
}

function handlePasteInput() {
  if (!pasteText.value.trim()) { importPreview.value = []; return }
  const rows = pasteText.value.trim().split('\n').map(line => line.split('\t'))
  importPreview.value = parseRows(rows)
}

async function doExcelImport() {
  if (!importPreview.value.length) return
  importingExcel.value = true
  try {
    const result = await api.post<any>('/inventory/bulk-import', { rows: importPreview.value })
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
</script>

<style scoped>
.preview-table-wrapper { overflow-x: auto; max-height: 280px; overflow-y: auto; }
.preview-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.preview-table th { background: rgba(var(--v-theme-surface-variant), 0.5); padding: 6px 10px; text-align: left; font-weight: 600; position: sticky; top: 0; }
.preview-table td { padding: 5px 10px; border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.06); }
.preview-table tr:hover td { background: rgba(var(--v-theme-surface-variant), 0.3); }
</style>
