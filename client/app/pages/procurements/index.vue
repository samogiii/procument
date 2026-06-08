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

        <v-data-table-server
          :headers="headers"
          :items="allItems"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
          :item-value="item => (item.id ?? item.Id)"
          v-model:expanded="expanded"
          show-expand
          @update:options="loadServerPage"
          @click:row="(_: any, { item }: any) => toggleExpand(item)"
        >
          <!-- Column filter: Part Number -->
          <template #header.partNumberName="{ column, toggleSort }">
            <ColFilterMenu
              col-key="partNumberName"
              :label="column.title"
              :options="cfPartOptions"
              :all-options="allPartOptions"
              :selected="colFilter.selected['partNumberName'] || new Set()"
              :search="colFilter.search['partNumberName'] || ''"
              @toggle="(v) => { colFilter.toggle('partNumberName', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('partNumberName', allPartOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('partNumberName'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['partNumberName'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Item Status -->
          <template #header.itemStatus="{ column, toggleSort }">
            <ColFilterMenu
              col-key="itemStatus"
              :label="column.title"
              :options="cfItemStatusOptionsPage"
              :all-options="cfItemStatusOptions"
              :selected="colFilter.selected['itemStatus'] || new Set()"
              :search="colFilter.search['itemStatus'] || ''"
              @toggle="(v) => { colFilter.toggle('itemStatus', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('itemStatus', cfItemStatusOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('itemStatus'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['itemStatus'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Customer -->
          <template #header.customerName="{ column, toggleSort }">
            <ColFilterMenu
              col-key="customerName"
              :label="column.title"
              :options="cfCustomerOptionsPage"
              :all-options="cfCustomerOptions"
              :selected="colFilter.selected['customerName'] || new Set()"
              :search="colFilter.search['customerName'] || ''"
              @toggle="(v) => { colFilter.toggle('customerName', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('customerName', cfCustomerOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('customerName'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['customerName'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Condition -->
          <template #header.condition="{ column, toggleSort }">
            <ColFilterMenu
              col-key="condition"
              :label="column.title"
              :options="cfCondOptionsPage"
              :all-options="cfCondOptions"
              :selected="colFilter.selected['condition'] || new Set()"
              :search="colFilter.search['condition'] || ''"
              @toggle="(v) => { colFilter.toggle('condition', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('condition', cfCondOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('condition'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['condition'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Proc Status -->
          <template #header.procurementStatus="{ column, toggleSort }">
            <ColFilterMenu
              col-key="procurementStatus"
              :label="column.title"
              :options="cfProcStatusOptionsPage"
              :all-options="cfProcStatusOptions"
              :selected="colFilter.selected['procurementStatus'] || new Set()"
              :search="colFilter.search['procurementStatus'] || ''"
              @toggle="(v) => { colFilter.toggle('procurementStatus', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('procurementStatus', cfProcStatusOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('procurementStatus'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['procurementStatus'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Supplier -->
          <template #header.currentSupplierName="{ column, toggleSort }">
            <ColFilterMenu
              col-key="currentSupplierName"
              :label="column.title"
              :options="cfSupplierOptionsPage"
              :all-options="cfSupplierOptions"
              :selected="colFilter.selected['currentSupplierName'] || new Set()"
              :search="colFilter.search['currentSupplierName'] || ''"
              @toggle="(v) => { colFilter.toggle('currentSupplierName', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('currentSupplierName', cfSupplierOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('currentSupplierName'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['currentSupplierName'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Assigned Users -->
          <template #header.assignedUsers="{ column }">
            <ColFilterMenu
              col-key="assignedUsers"
              :label="column.title"
              :options="cfUserOptionsPage"
              :all-options="cfUserOptions"
              :selected="colFilter.selected['assignedUsers'] || new Set()"
              :search="colFilter.search['assignedUsers'] || ''"
              @toggle="(v) => { colFilter.toggle('assignedUsers', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('assignedUsers', cfUserOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('assignedUsers'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['assignedUsers'] = v"
              @sort-click="() => {}"
            />
          </template>

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
                    <div class="d-flex gap-2">
                      <!-- Reopen button — shown only for Finalized procurement rows (admin only) -->
                      <v-btn
                        v-if="isAdmin && item.procurementStatus === 'Finalized'"
                        size="x-small"
                        color="warning"
                        variant="tonal"
                        prepend-icon="mdi-lock-open-variant-outline"
                        :loading="reopeningId === (item.procurementId || item.ProcurementId)"
                        @click.stop="reopenProcurement(item)"
                      >
                        Reopen
                      </v-btn>
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
                          <th style="width: 100px;" class="text-right">Shipping</th>
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
                          <td>
                            <input
                              type="number"
                              v-model.number="sq.shippingCost"
                              class="quote-input text-right"
                              step="0.01"
                              placeholder="0.00"
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
        </v-data-table-server>
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
const totalItems = ref(0)
const users = ref<any[]>([])

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// ── Column filters ──
const colFilter = useColFilterPersisted('procurements')
// "Available" options — from current page results (shrinks when col filter is active)
const cfPartOptions = ref<string[]>([])
// "All" options — from full DB (never shrinks)
const allPartOptions = ref<string[]>([])
const cfItemStatusOptions = ref<string[]>([])   // full DB (from loadAllProcurementFilterOptions)
const cfCustomerOptions = ref<string[]>([])     // full DB (from loadAllProcurementFilterOptions)

// "Available" (current page) for itemStatus + customerName
const cfItemStatusOptionsPage = computed(() =>
  [...new Set(allItems.value.map((i: any) => i.itemStatus).filter(Boolean))].sort() as string[]
)
const cfCustomerOptionsPage = computed(() =>
  [...new Set(allItems.value.map((i: any) => i.customerName).filter(Boolean))].sort() as string[]
)

const cfCondOptions = computed(() => ['AR', 'FN', 'IN', 'NE', 'NS', 'OH', 'RP', 'SV'])
const cfCondOptionsPage = computed(() => [...new Set(allItems.value.map((i: any) => i.condition).filter(Boolean))].sort() as string[])

const cfProcStatusOptions = computed(() => ['Open', 'Sourcing', 'InProgress', 'Reopened', 'Finalized', 'Cancelled'])
const cfProcStatusOptionsPage = computed(() => [...new Set(allItems.value.map((i: any) => i.procurementStatus).filter(Boolean))].sort() as string[])

const cfSupplierOptions = computed(() => [...new Set(allItems.value.map((i: any) => i.currentSupplierName).filter(Boolean))].sort() as string[])
const cfSupplierOptionsPage = computed(() => cfSupplierOptions.value)

const cfUserOptions = computed(() => userOptions.value.map((u: any) => u.name).sort() as string[])
const cfUserOptionsPage = computed(() => {
  const names = new Set<string>()
  for (const item of allItems.value) {
    for (const u of (item.assignedUsers || [])) {
      if (u.userName) names.add(u.userName)
    }
  }
  return [...names].sort() as string[]
})

function collectCfOptions(loadedItems: any[]) {
  // Part options from current page (the "available" subset)
  cfPartOptions.value = [...new Set(loadedItems.map((i: any) => i.partNumberName).filter(Boolean))].sort()
  // Accumulate all-time part options (grows, never shrinks when filter active)
  const newParts = loadedItems.map((i: any) => i.partNumberName).filter(Boolean) as string[]
  if (newParts.length) {
    allPartOptions.value = [...new Set([...allPartOptions.value, ...newParts])].sort()
  }
  // itemStatus and customerName full-DB options come from loadAllProcurementFilterOptions()
}

async function loadAllProcurementFilterOptions() {
  try {
    const res = await api.get<any>('/procurements/items/filter-options')
    cfItemStatusOptions.value = (res.itemStatuses || []).sort()
    cfCustomerOptions.value = (res.customerNames || []).filter(Boolean).sort()
  } catch {}
}

let cfDebounce: any = null
function debouncedCfLoad() {
  clearTimeout(cfDebounce)
  cfDebounce = setTimeout(() => loadServerPage({ ...lastProcOpts.value, page: 1 }), 200)
}

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
  const added = newVal.filter((id: any) => !oldVal.includes(id))
  added.forEach((id: any) => {
    const item = allItems.value.find((i: any) => String(i.id ?? i.Id) === String(id))
    if (item) {
      const pid = (item.procurementId || item.ProcurementId)
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
    shippingCost: 0,
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
      shippingCost: sq.shippingCost,
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

// ── Reopen finalized procurement ──
const reopeningId = ref<number | null>(null)

async function reopenProcurement(item: any) {
  const pid = item.procurementId || item.ProcurementId
  if (!pid || reopeningId.value) return
  reopeningId.value = pid
  try {
    await api.post(`/procurements/${pid}/reopen`, {})
    showSnack('Procurement reopened — you can now add supplier quotes to cover remaining qty.', 'success')
    await fetchProcurementDetail(pid)
    await loadData()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to reopen', 'error')
  } finally {
    reopeningId.value = null
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

const statusOptions = ['Open', 'Sourcing', 'Ready', 'Cancelled', 'Returned']
const procStatusOptions = ['Open', 'Sourcing', 'InProgress', 'Reopened', 'Finalized', 'Cancelled']
const customerOptions = ref<string[]>([])
const userOptions = ref<{ id: number; name: string }[]>([])

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
  const map: Record<string, string> = { Open: 'grey', Sourcing: 'info', InProgress: 'warning', Reopened: 'orange', Finalized: 'success', Cancelled: 'error' }
  return map[status] || 'grey'
}

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Data Loading ──
const lastProcOpts = ref<any>({ page: 1, itemsPerPage: 50 })
const sort = useServerSort()

async function loadServerPage(opts?: any) {
  if (opts) { lastProcOpts.value = opts; sort.capture(opts) }
  const { page, itemsPerPage } = lastProcOpts.value
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page), pageSize: String(itemsPerPage) })
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (statusFilter.value.length) statusFilter.value.forEach((s: string) => params.append('status', s))
    // When no proc-status filter is active, hide Finalized/Cancelled items but always show Reopened.
    // Admin can still see Finalized/Cancelled by explicitly selecting them in the filter.
    const effectiveProcStatuses = procStatusFilter.value.length
      ? procStatusFilter.value
      : procStatusOptions.filter((s: string) => s !== 'Finalized' && s !== 'Cancelled')
    effectiveProcStatuses.forEach((s: string) => params.append('procStatus', s))
    if (customerFilter.value.length) customerFilter.value.forEach((c: string) => params.append('customerName', c))
    if (userFilter.value.length) userFilter.value.forEach((id: number) => params.append('userIds', String(id)))
    // Column header filters
    if (colFilter.isActive('partNumberName')) colFilter.getSelected('partNumberName').forEach(v => params.append('partNames', v))
    if (colFilter.isActive('itemStatus')) colFilter.getSelected('itemStatus').forEach(v => params.append('status', v))
    if (colFilter.isActive('customerName')) colFilter.getSelected('customerName').forEach(v => params.append('customerName', v))
    if (colFilter.isActive('condition')) colFilter.getSelected('condition').forEach(v => params.append('conditions', v))
    if (colFilter.isActive('procurementStatus')) colFilter.getSelected('procurementStatus').forEach(v => params.append('procStatus', v))
    if (colFilter.isActive('currentSupplierName')) colFilter.getSelected('currentSupplierName').forEach(v => params.append('supplierNames', v))
    if (colFilter.isActive('assignedUsers')) {
      const nameToId = new Map(userOptions.value.map(u => [u.name, u.id]))
      colFilter.getSelected('assignedUsers').forEach(name => {
        const id = nameToId.get(name)
        if (id) params.append('userIds', String(id))
      })
    }
    sort.appendTo(params)
    const res = await api.get<any>(`/procurements/items?${params.toString()}`)
    allItems.value = res.items ?? res.Items ?? (Array.isArray(res) ? res : [])
    totalItems.value = res.totalCount ?? res.TotalCount ?? allItems.value.length
    collectCfOptions(allItems.value)
  } catch (e) {
    console.error('[ProcurementItems] Load failed', e)
  } finally {
    loading.value = false
  }
}

async function loadData() {
  await loadServerPage()
}

async function loadUsers() {
  if (users.value.length) return
  try {
    const allUsers = await api.get<any[]>('/users')
    const allowed = ['GHS', 'MOR', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']
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

let procDebounce: any = null
function debouncedProcLoad() {
  clearTimeout(procDebounce)
  procDebounce = setTimeout(() => loadServerPage({ ...lastProcOpts.value, page: 1 }), 350)
}
watch(search, debouncedProcLoad)
watch(statusFilter, () => loadServerPage({ ...lastProcOpts.value, page: 1 }), { deep: true })
watch(procStatusFilter, () => loadServerPage({ ...lastProcOpts.value, page: 1 }), { deep: true })
watch(customerFilter, () => loadServerPage({ ...lastProcOpts.value, page: 1 }), { deep: true })
watch(userFilter, () => loadServerPage({ ...lastProcOpts.value, page: 1 }), { deep: true })

onMounted(async () => {
  loadData()
  loadAllProcurementFilterOptions()
  try {
    const all = await api.get<any[]>('/users')
    userOptions.value = (all || []).filter((u: any) => u.isActive !== false).map((u: any) => ({ id: u.id, name: u.name || u.username }))
  } catch {}
  try {
    const custs = await api.get<any>('/customers?page=1&pageSize=500')
    const items = custs?.items ?? custs?.Items ?? (Array.isArray(custs) ? custs : [])
    customerOptions.value = [...new Set(items
      .map((c: any) => c.customerCode || '-'))]
      .sort()
  } catch {}
})
</script>

<style scoped>
.cf-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.cf-filter-btn { opacity: 0.5; flex-shrink: 0; }
.cf-filter-btn:hover, .cf-filter-btn.v-btn--active { opacity: 1; }

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
