<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Order Items</h1>
        <!-- <p class="text-caption text-medium-emphasis">All items across active procurements.</p> -->
      </div>
      <v-spacer />
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search part number..."
            single-line
            hide-details
            clearable
            class="flex-grow-1"
            style="min-width: 180px;"
          />
          <v-select
            v-model="statusFilter"
            :items="statusOptions"
            label="Item Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
          />
          <v-select
            v-model="procStatusFilter"
            :items="procStatusOptions"
            label="Proc Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
          />
          <v-autocomplete
            v-model="customerFilter"
            :items="customerOptions"
            label="Customer"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 240px;"
          />
          <v-autocomplete
            v-if="isAdmin"
            v-model="userFilter"
            :items="userOptions"
            item-title="name"
            item-value="id"
            label="Assigned User"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 240px;"
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
          :item-value="item => (item.id ?? item.Id)"
          v-model:expanded="expanded"
          show-expand
          @click:row="(_: any, { item }: any) => toggleExpand(item)"
        >
          <!-- # -->
          <template #item.index="{ index }">
            <span class="text-caption text-medium-emphasis">{{ index + 1 }}</span>
          </template>

          <!-- Part Number -->
          <template #item.partNumberName="{ item }">
            <span class="font-weight-bold cell-pn">{{ item.partNumberName || '—' }}</span>
            <div v-if="item.partNumberDescription" class="text-caption text-medium-emphasis text-truncate" style="max-width: 200px;">
              {{ item.partNumberDescription }}
            </div>
          </template>

          <!-- Item Status -->
          <template #item.itemStatus="{ item }">
            <v-menu location="bottom" offset="5">
              <template #activator="{ props }">
                <v-chip
                  v-bind="props"
                  :color="itemStatusColor(item.itemStatus)"
                  size="x-small"
                  variant="flat"
                  class="font-weight-bold text-uppercase cursor-pointer"
                  :loading="updatingStatusItemId === item.id"
                  @click.stop
                >
                  {{ item.itemStatus }}
                  <v-icon icon="mdi-chevron-down" size="14" class="ml-1" />
                </v-chip>
              </template>
              <v-list density="compact" min-width="120">
                <v-list-item
                  v-for="st in ['Open', 'Sourcing', 'Ready', 'Cancelled']"
                  :key="st"
                  :value="st"
                  :active="item.itemStatus === st"
                  @click="updateItemStatus(item, st)"
                >
                  <v-list-item-title class="text-caption font-weight-bold text-uppercase">
                    <v-icon
                      icon="mdi-circle"
                      :color="itemStatusColor(st)"
                      size="10"
                      class="mr-2"
                    />
                    {{ st }}
                  </v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
          </template>

          <!-- Proc Status -->
          <template #item.procurementStatus="{ item }">
            <v-chip :color="procStatusColor(item.procurementStatus)" size="x-small" variant="tonal" class="font-weight-bold text-uppercase">
              {{ item.procurementStatus }}
            </v-chip>
          </template>

          <!-- Current Supplier -->
          <template #item.currentSupplierName="{ item }">
            <span :class="item.currentSupplierName ? '' : 'text-medium-emphasis'">
              {{ item.currentSupplierName || '—' }}
            </span>
          </template>

          <!-- Assigned Users -->
          <template #item.assignedUsers="{ item }">
            <div class="d-flex flex-wrap gap-1 align-center">
              <v-chip
                v-for="u in item.assignedUsers.slice(0, 3)"
                :key="u.id"
                size="x-small"
                variant="tonal"
                color="primary"
                class="px-1"
              >
                {{ u.userName }}
              </v-chip>
              <v-chip v-if="item.assignedUsers.length > 3" size="x-small" variant="text">
                +{{ item.assignedUsers.length - 3 }}
              </v-chip>
              <span v-if="!item.assignedUsers.length" class="text-caption text-medium-emphasis">—</span>
            </div>
          </template>

          <!-- Created -->
          <template #item.createdAt="{ item }">
            <span class="text-caption">{{ new Date(item.createdAt).toLocaleDateString() }}</span>
          </template>

          <!-- Actions -->
          <template #item.actions="{ item }">
            <div class="d-flex align-center gap-1">
              <v-btn
                v-if="isAdmin"
                icon="mdi-account-plus-outline"
                variant="text"
                size="x-small"
                color="primary"
                title="Assign user to this item"
                @click.stop="openAssign(item)"
              />
              <!-- <v-btn
                icon="mdi-open-in-new"
                variant="text"
                size="x-small"
                color="grey"
                :to="`/procurements/${(item.procurementId || item.ProcurementId)}`"
                title="Open procurement"
                @click.stop
              /> -->
            </div>
          </template>

          <!-- Expanded Row -->
          <template #expanded-row="{ columns, item }">
            <tr>
              <td :colspan="columns.length" class="pa-0 border-0">
                <div v-if="loadingDetails[(item.procurementId || item.ProcurementId)]" class="d-flex justify-center py-6">
                  <v-progress-circular indeterminate size="24" color="primary" />
                </div>
                <div v-else-if="procurementDetails[(item.procurementId || item.ProcurementId)]" class="pa-4 expansion-panel">
                  <div class="d-flex align-center justify-space-between mb-3">
                    <div class="d-flex align-center gap-2 flex-wrap">
                      <span class="text-caption text-uppercase font-weight-bold letter-spacing-wide text-primary">
                        <v-icon icon="mdi-truck-outline" size="16" class="mr-1" />
                        Supplier Quotes
                      </span>
                      <v-chip
                        v-if="selectedQuoteCount(item) >= 2"
                        size="x-small"
                        color="warning"
                        variant="tonal"
                        prepend-icon="mdi-call-split"
                        class="font-weight-bold"
                      >
                        Split: {{ selectedQuoteCount(item) }} suppliers · {{ selectedQuoteQty(item) }} of {{ item.qty }} qty
                      </v-chip>
                    </div>
                    <v-btn
                      v-if="!isFinalizedOrCancelled(item)"
                      size="x-small"
                      color="primary"
                      variant="flat"
                      prepend-icon="mdi-plus"
                      @click.stop="addSupplierQuote(item)"
                    >
                      Add Quote
                    </v-btn>
                  </div>

                  <div class="border rounded bg-surface overflow-hidden mb-2">
                    <table class="proc-grid">
                      <thead>
                        <tr>
                          <th style="width: 40px;" class="text-center">Sel</th>
                          <th style="min-width: 150px;">Supplier</th>
                          <th style="width: 100px;">Alt P/N</th>
                          <th style="width: 60px;">Cond</th>
                          <th style="width: 60px;" class="text-center">Qty</th>
                          <th style="width: 100px;" class="text-right">Price ($)</th>
                          <th style="width: 100px;" class="text-right">Total</th>
                          <th style="width: 120px;">Lead Time</th>
                          <th>Note</th>
                          <th style="width: 50px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="sq in getItemSupplierQuotes(item)" :key="sq.id || `new-${sq.tempId}`" :class="{ 'bg-success-light': sq.isSelected }">
                          <td class="text-center">
                            <v-checkbox
                              :model-value="sq.isSelected"
                              :readonly="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              color="success"
                              density="compact"
                              hide-details
                              @click.stop="selectSupplierQuote(item, sq)"
                            />
                          </td>
                          <td>
                            <input
                              type="text"
                              v-model="sq.supplierName"
                              class="quote-input"
                              placeholder="Name..."
                              :readonly="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              list="procurement-supplier-suggestions"
                              @input="onSupplierNameInput(sq, ($event.target as HTMLInputElement).value)"
                              @blur="saveSupplierQuote(item, sq)"
                              @click.stop
                            />
                          </td>
                          <td>
                            <input
                              type="text"
                              v-model="sq.alt"
                              class="quote-input"
                              placeholder="Alt P/N..."
                              :readonly="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              @blur="saveSupplierQuote(item, sq)"
                              @click.stop
                            />
                          </td>
                          <td>
                            <select
                              v-model="sq.condition"
                              class="quote-input"
                              :disabled="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              @change="saveSupplierQuote(item, sq)"
                              @click.stop
                            >
                              <option v-for="c in ['NE','OH','SV','AR','RP']" :key="c" :value="c">{{ c }}</option>
                            </select>
                          </td>
                          <td>
                            <input
                              type="number"
                              v-model.number="sq.qty"
                              class="quote-input text-center"
                              :readonly="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              @blur="saveSupplierQuote(item, sq)"
                              @click.stop
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              v-model.number="sq.price"
                              class="quote-input text-right"
                              step="0.01"
                              :readonly="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              @blur="saveSupplierQuote(item, sq)"
                              @click.stop
                            />
                          </td>
                          <td class="text-right text-caption font-weight-bold px-2">
                            ${{ formatPrice((sq.qty || 0) * (sq.price || 0)) }}
                          </td>
                          <td>
                            <input
                              type="text"
                              v-model="sq.leadTime"
                              class="quote-input"
                              placeholder="Lead time..."
                              :readonly="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              @blur="saveSupplierQuote(item, sq)"
                              @click.stop
                            />
                          </td>
                          <td>
                            <input
                              type="text"
                              v-model="sq.note"
                              class="quote-input"
                              placeholder="Note..."
                              :readonly="isFinalizedOrCancelled(item) || sq.hasActivePOItem"
                              @blur="saveSupplierQuote(item, sq)"
                              @click.stop
                            />
                          </td>
                          <td class="text-center px-1">
                            <template v-if="isAdmin && sq.isSelected && !isFinalizedOrCancelled(item)">
                              <v-chip
                                v-if="sq.hasActivePOItem"
                                size="x-small"
                                color="success"
                                prepend-icon="mdi-check-circle"
                                variant="tonal"
                              >Approved</v-chip>
                              <v-btn
                                v-else
                                size="x-small"
                                color="success"
                                variant="flat"
                                prepend-icon="mdi-check-bold"
                                :loading="approvingQuoteId === sq.id"
                                :disabled="approvingQuoteId !== null && approvingQuoteId !== sq.id"
                                @click.stop="approveSupplierQuote(item, sq)"
                              >Approve</v-btn>
                            </template>
                            <v-btn
                              v-else-if="!sq.isSelected && !isFinalizedOrCancelled(item)"
                              icon="mdi-delete"
                              variant="text"
                              size="x-small"
                              color="error"
                              @click.stop="deleteSupplierQuote(item, sq)"
                            />
                            <v-icon v-else-if="sq.hasActivePOItem" icon="mdi-check-decagram" color="success" size="18" title="Approved" />
                          </td>
                        </tr>
                        <tr v-if="!getItemSupplierQuotes(item).length">
                          <td colspan="10" class="pa-4 text-center text-caption text-medium-emphasis">No quotes yet.</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
              </td>
            </tr>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Supplier Autocomplete Suggestions -->
    <datalist id="procurement-supplier-suggestions">
      <option
        v-for="s in supplierSuggestions.filter(x => x.status === 'Approved')"
        :key="s.id"
        :value="s.name"
      >{{ s.status }}</option>
    </datalist>

    <!-- Assign User Dialog -->
    <v-dialog v-model="showAssignDialog" max-width="400">
      <v-card>
        <v-card-title class="pa-4">
          <v-icon icon="mdi-account-plus-outline" color="primary" class="mr-2" />
          Assign User to Item
        </v-card-title>
        <v-card-text>
          <div class="text-caption text-medium-emphasis mb-3">
            Part: <strong>{{ assignTarget?.partNumberName }}</strong>
          </div>
          <v-autocomplete
            v-model="assignUserId"
            :items="users"
            item-title="name"
            item-value="id"
            label="Select User"
            variant="outlined"
            density="compact"
            autofocus
          />
        </v-card-text>
        <v-card-actions class="justify-end pa-4">
          <v-btn variant="text" @click="showAssignDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :disabled="!assignUserId" :loading="assigning" @click="doAssign">
            Assign
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const loading = ref(false)
const allItems = ref<any[]>([])
const users = ref<any[]>([])

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const showAssignDialog = ref(false)
const assignTarget = ref<any>(null)
const assignUserId = ref<number | null>(null)
const assigning = ref(false)

// ── Row Expansion ──
const expanded = ref<any[]>([])
const procurementDetails = ref<Record<number, any>>({})
const loadingDetails = ref<Record<number, boolean>>({})

async function toggleExpand(item: any) {
  const id = item.id ?? item.Id
  if (id === undefined) {
    console.error('[Procurement] Item has no id:', item)
    return
  }
  
  const idx = expanded.value.indexOf(id)
  if (idx >= 0) {
    expanded.value = expanded.value.filter(x => x !== id)
  } else {
    expanded.value = [...expanded.value, id]
  }
  console.log('[Procurement] Toggled expansion for:', id, 'Current expanded:', expanded.value)
}

// Watch expanded array to fetch details for newly expanded items
watch(expanded, (newVal, oldVal) => {
  const added = newVal.filter(id => !oldVal.includes(id))
  added.forEach(id => {
    // Find item by either number or string ID, checking both casing
    const item = allItems.value.find(i => String(i.id ?? i.Id) === String(id))
    if (item) {
      const pid = (item.procurementId || item.ProcurementId) || item.ProcurementId
      if (pid && !procurementDetails.value[pid] && !loadingDetails.value[pid]) {
        fetchProcurementDetail(pid)
      }
    }
  })
}, { deep: true })

async function fetchProcurementDetail(procurementId: number) {
  if (!procurementId) return
  loadingDetails.value[procurementId] = true
  try {
    const data = await api.get<any>(`/procurements/${procurementId}`)
    console.log(`[Procurement] Loaded detail for ${procurementId}:`, data)
    procurementDetails.value[procurementId] = data
  } catch (e) {
    console.error(`[Procurement] Failed to load detail for ${procurementId}:`, e)
    showSnack('Failed to load quotes', 'error')
  } finally {
    loadingDetails.value[procurementId] = false
  }
}

function getItemSupplierQuotes(item: any) {
  const pid = (item.procurementId || item.ProcurementId) || item.ProcurementId
  const detail = procurementDetails.value[pid]
  if (!detail) return []
  
  // Handle both camelCase and PascalCase
  const items = detail.items || detail.Items || []
  const fullItem = items.find((i: any) => String(i.id) === String(item.id))
  
  if (!fullItem) {
    console.warn(`[Procurement] Item ${item.id} not found in procurement ${pid} detail`, items)
    return []
  }
  
  return fullItem.supplierQuotes || fullItem.SupplierQuotes || []
}

function isFinalizedOrCancelled(item: any) {
  const detail = procurementDetails.value[(item.procurementId || item.ProcurementId)]
  if (!detail) return false
  const status = detail.status || detail.Status
  return status === 'Finalized' || status === 'Cancelled'
}

// ── Item Status Editing ──
const updatingStatusItemId = ref<number | null>(null)

async function updateItemStatus(item: any, newStatus: string) {
  if (item.itemStatus === newStatus || updatingStatusItemId.value) return
  updatingStatusItemId.value = item.id
  try {
    const pid = (item.procurementId || item.ProcurementId)
    await api.patch(`/procurements/${pid}/items/${item.id}`, { itemStatus: newStatus })
    item.itemStatus = newStatus
    showSnack(`Status updated to ${newStatus}`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Update failed', 'error')
  } finally {
    updatingStatusItemId.value = null
  }
}

// ── Multi-select supplier quote helpers ──
function selectedQuoteCount(item: any): number {
  return getItemSupplierQuotes(item).filter((q: any) => q.isSelected).length
}
function selectedQuoteQty(item: any): number {
  return getItemSupplierQuotes(item).reduce((sum: number, q: any) => sum + (q.isSelected ? Number(q.qty) || 0 : 0), 0)
}

// ── Supplier Quote CRUD ──
function addSupplierQuote(item: any) {
  const detail = procurementDetails.value[(item.procurementId || item.ProcurementId)]
  if (!detail) return
  const fullItem = detail.items?.find((i: any) => i.id === item.id)
  if (!fullItem) return

  if (!fullItem.supplierQuotes) fullItem.supplierQuotes = []
  fullItem.supplierQuotes.push({
    tempId: Date.now(),
    supplierName: '',
    price: 0,
    qty: item.qty,
    condition: item.condition || 'NE',
    leadTime: '',
    note: '',
    isSelected: false
  })
}

async function saveSupplierQuote(item: any, sq: any) {
  if (isFinalizedOrCancelled(item) || !sq.supplierName.trim()) return
  try {
    const typed = (sq.supplierName || '').trim().toLowerCase()
    const match = supplierSuggestions.value.find(s => s.name.toLowerCase() === typed)
    const resolvedSupplierId = sq.supplierId || (match ? match.id : null)
    const payload = {
      id: sq.id || null,
      supplierId: resolvedSupplierId,
      supplierName: sq.supplierName,
      price: sq.price,
      qty: sq.qty,
      condition: sq.condition,
      leadTime: sq.leadTime,
      note: sq.note
    }
    const res = await api.post<any>(`/procurements/${(item.procurementId || item.ProcurementId)}/items/${item.id}/supplier-quotes`, payload)
    Object.assign(sq, res)
    showSnack('Quote saved')
  } catch {
    showSnack('Save failed', 'error')
  }
}

async function selectSupplierQuote(item: any, sq: any) {
  if (isFinalizedOrCancelled(item) || !sq.id) return
  const wasSelected = sq.isSelected
  sq.isSelected = !wasSelected
  try {
    await api.post(`/procurements/${(item.procurementId || item.ProcurementId)}/items/${item.id}/supplier-quotes/${sq.id}/select`, {})
    // Refresh to get updated header/item state (UnitPrice etc)
    await fetchProcurementDetail((item.procurementId || item.ProcurementId))
    showSnack(wasSelected ? 'Supplier deselected' : 'Supplier selected')
  } catch {
    sq.isSelected = wasSelected
    showSnack('Selection failed', 'error')
  }
}

async function deleteSupplierQuote(item: any, sq: any) {
  if (isFinalizedOrCancelled(item)) return
  if (!sq.id) {
    const detail = procurementDetails.value[(item.procurementId || item.ProcurementId)]
    const fullItem = detail.items?.find((i: any) => i.id === item.id)
    if (fullItem) {
      fullItem.supplierQuotes = fullItem.supplierQuotes.filter((q: any) => q !== sq)
    }
    return
  }
  try {
    await api.del(`/procurements/${(item.procurementId || item.ProcurementId)}/items/${item.id}/supplier-quotes/${sq.id}`)
    const detail = procurementDetails.value[(item.procurementId || item.ProcurementId)]
    const fullItem = detail.items?.find((i: any) => i.id === item.id)
    if (fullItem) {
      fullItem.supplierQuotes = fullItem.supplierQuotes.filter((q: any) => q.id !== sq.id)
    }
    showSnack('Quote removed')
  } catch {
    showSnack('Delete failed', 'error')
  }
}

// ── Quote Approval ──
const approvingQuoteId = ref<number | null>(null)
const router = useRouter()

async function approveSupplierQuote(item: any, sq: any) {
  if (approvingQuoteId.value) return
  approvingQuoteId.value = sq.id
  try {
    const res = await api.post<any>(
      `/procurements/${(item.procurementId || item.ProcurementId)}/items/${item.id}/supplier-quotes/${sq.id}/approve`,
      {}
    )
    if (res.procurementFullyFinalized) {
      showSnack('All supplier rows approved — procurement finalized! Redirecting...', 'success')
      setTimeout(() => router.push('/purchase-orders'), 1500)
    } else if ((res.createdPOItemIds?.length ?? 0) === 0) {
      showSnack('This supplier row is already approved.', 'info')
    } else {
      showSnack('Supplier row approved — PO item created.', 'success')
    }
    await fetchProcurementDetail((item.procurementId || item.ProcurementId))
    await loadData() // Refresh list for statuses
  } catch (e: any) {
    showSnack(e?.data?.message || 'Approval failed', 'error')
  } finally {
    approvingQuoteId.value = null
  }
}

// ── Supplier Autocomplete ──
const supplierSuggestions = ref<{ id: number; name: string; status: string }[]>([])
let supplierSearchDebounce: any = null

function searchSupplier(val: string) {
  clearTimeout(supplierSearchDebounce)
  if (!val || val.length < 1) {
    supplierSuggestions.value = []
    return
  }
  supplierSearchDebounce = setTimeout(async () => {
    try {
      supplierSuggestions.value = await api.get<{ id: number; name: string; status: string }[]>(`/suppliers/search?q=${encodeURIComponent(val)}`)
    } catch {
      supplierSuggestions.value = []
    }
  }, 300)
}

function onSupplierNameInput(sq: any, val: string) {
  sq.supplierName = val
  const typed = (val || '').trim().toLowerCase()
  const match = supplierSuggestions.value.find(s => s.name.toLowerCase() === typed)
  sq.supplierId = match ? match.id : null
  searchSupplier(val)
}

// ── Filters ──
const search = ref('')
const statusFilter = ref<string[]>([])
const procStatusFilter = ref<string[]>([])
const customerFilter = ref<string[]>([])
const userFilter = ref<number[]>([])

const hasActiveFilters = computed(() =>
  search.value.trim() !== '' ||
  statusFilter.value.length > 0 ||
  procStatusFilter.value.length > 0 ||
  customerFilter.value.length > 0 ||
  userFilter.value.length > 0
)

function clearFilters() {
  search.value = ''
  statusFilter.value = []
  procStatusFilter.value = []
  customerFilter.value = []
  userFilter.value = []
}

const statusOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach(i => { if (i.itemStatus) set.add(i.itemStatus) })
  return Array.from(set).sort()
})

const procStatusOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach(i => { if (i.procurementStatus) set.add(i.procurementStatus) })
  return Array.from(set).sort()
})

const customerOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach(i => { if (i.customerName) set.add(i.customerName) })
  return Array.from(set).sort()
})

const userOptions = computed(() => {
  const map = new Map<number, string>()
  allItems.value.forEach(i =>
    (i.assignedUsers || []).forEach((u: any) => {
      if (u.userId && u.userName) map.set(u.userId, u.userName)
    })
  )
  return Array.from(map, ([id, name]) => ({ id, name }))
})

const filteredItems = computed(() => {
  let result = allItems.value
  if (statusFilter.value.length)
    result = result.filter(i => statusFilter.value.includes(i.itemStatus))
  if (procStatusFilter.value.length)
    result = result.filter(i => procStatusFilter.value.includes(i.procurementStatus))
  if (customerFilter.value.length)
    result = result.filter(i => customerFilter.value.includes(i.customerName))
  if (userFilter.value.length)
    result = result.filter(i =>
      (i.assignedUsers || []).some((u: any) => userFilter.value.includes(u.userId))
    )
  if (search.value.trim()) {
    const q = search.value.trim().toLowerCase()
    result = result.filter(i =>
      (i.partNumberName || '').toLowerCase().includes(q) ||
      (i.partNumberDescription || '').toLowerCase().includes(q) ||
      (i.currentSupplierName || '').toLowerCase().includes(q)
    )
  }
  return result
})

const headers = computed(() => {
  const h: any[] = [
    { title: '#', key: 'index', sortable: false, width: '50px' },
    { title: 'Part Number', key: 'partNumberName' },
    { title: 'Qty', key: 'qty', width: '60px', align: 'center' },
    { title: 'Cond', key: 'condition', width: '70px' },
    { title: 'Item Status', key: 'itemStatus', width: '110px' },
    { title: 'Proc Status', key: 'procurementStatus', width: '110px' },
    { title: 'Customer', key: 'customerName', width: '100px' },
    { title: 'Supplier', key: 'currentSupplierName' },
  ]
  if (isAdmin.value) {
    h.push({ title: 'Assigned Users', key: 'assignedUsers', sortable: false })
  }
  h.push({ title: 'Created', key: 'createdAt', width: '100px' })
  h.push({ title: '', key: 'actions', sortable: false, align: 'end', width: '80px' })
  return h
})

function itemStatusColor(status: string) {
  const map: Record<string, string> = { Open: 'grey', Sourcing: 'info', Ready: 'success', Cancelled: 'error', Returned: 'warning' }
  return map[status] || 'grey'
}

function procStatusColor(status: string) {
  const map: Record<string, string> = { Open: 'grey', Sourcing: 'info', InProgress: 'warning', Finalized: 'success', Cancelled: 'error' }
  return map[status] || 'grey'
}

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Data Loading ──
async function loadData() {
  loading.value = true
  try {
    const data = await api.get<any[]>('/procurements/items')
    allItems.value = Array.isArray(data) ? data : []
  } catch (e) {
    console.error('[ProcurementItems] Load failed', e)
  } finally {
    loading.value = false
  }
}

async function loadUsers() {
  if (users.value.length) return
  try {
    const allUsers = await api.get<any[]>('/users')
    const allowed = ['GHS', 'SNP', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']
    users.value = allUsers.filter((u: any) => allowed.includes(u.name) || allowed.includes(u.username))
  } catch {
    users.value = []
  }
}

// ── Assign Dialog ──
function openAssign(item: any) {
  assignTarget.value = item
  assignUserId.value = null
  showAssignDialog.value = true
  loadUsers()
}

async function doAssign() {
  if (!assignUserId.value || !assignTarget.value) return
  assigning.value = true
  try {
    await api.post('/permissions/assign', {
      userId: assignUserId.value,
      entityName: 'Procurement',
      entityId: String(assignTarget.value.id),
      permission: 'Edit',
    })
    showSnack('User assigned')
    showAssignDialog.value = false
    // Update local state to reflect the new assignment
    const user = users.value.find(u => u.id === assignUserId.value)
    if (user) {
      const item = allItems.value.find(i => i.id === assignTarget.value.id)
      if (item) {
        item.assignedUsers = item.assignedUsers || []
        const alreadyAssigned = item.assignedUsers.some((u: any) => u.userId === user.id)
        if (!alreadyAssigned) {
          item.assignedUsers.push({ userId: user.id, userName: user.name })
        }
      }
    }
  } catch {
    showSnack('Assignment failed', 'error')
  } finally {
    assigning.value = false
  }
}

onMounted(loadData)
</script>

<style scoped>
.cell-pn {
  font-family: 'Roboto Mono', monospace;
  color: rgb(var(--v-theme-primary));
}

.expansion-panel {
  background-color: rgba(var(--v-theme-on-surface), 0.04);
}

.transparent-table {
  background: transparent !important;
}

.selected-sq-row {
  background-color: rgba(var(--v-theme-success), 0.1) !important;
}

.letter-spacing-wide {
  letter-spacing: 0.05em;
}

.italic {
  font-style: italic;
}

.proc-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.proc-grid th {
  background: rgba(var(--v-theme-on-surface), 0.05);
  text-align: left;
  padding: 8px;
  font-weight: bold;
  text-transform: uppercase;
  font-size: 10px;
  letter-spacing: 0.05em;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}

.proc-grid td {
  padding: 4px 8px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.05);
  vertical-align: middle;
}

.quote-input {
  width: 100%;
  border: 1px solid transparent;
  background: transparent;
  padding: 4px;
  font-size: 12px;
  outline: none;
  border-radius: 4px;
  color: inherit;
}

.quote-input:focus {
  background: rgba(var(--v-theme-primary), 0.05);
  border-color: rgba(var(--v-theme-primary), 0.3);
}

.quote-input:read-only {
  cursor: default;
}

.bg-success-light {
  background-color: rgba(74, 222, 128, 0.08) !important;
}
</style>
