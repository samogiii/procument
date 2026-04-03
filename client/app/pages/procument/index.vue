<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">Procument</h1>
      <v-spacer />
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search..."
            single-line
            hide-details
            class="flex-grow-1 mx-2"
            style="min-width: 180px;"
          />
          <v-autocomplete
            v-model="partNumberFilter"
            :items="partNumberOptions"
            label="Part Number"
            variant="outlined"
            hide-details
            single-line
            multiple
            chips
            closable-chips
            clearable
            class="mx-2"
            style="min-width: 160px; max-width: 260px;"
          />
          <v-select
            v-model="statusFilter"
            :items="statusOptions"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            class="mr-2"
            style="min-width: 120px; max-width: 200px;"
          />
          <v-select
            v-model="userFilter"
            :items="userOptions"
            item-title="name"
            item-value="id"
            label="User"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            class="mx-2"
            style="min-width: 140px; max-width: 240px;"
          />
          <v-select
            v-model="customerFilter"
            :items="customerOptions"
            label="Customer"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 260px;"
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
          item-value="rfqItemId"
          v-model:expanded="expandedArray"
          show-expand
          :row-props="getRowProps"
          @click:row="(_: any, { item }: any) => toggleExpand(item)"
        >
          <template #item.rfqId="{ item }">
            <div class="d-flex align-center gap-1">
              <v-icon
                :icon="expandedArray.includes(item.rfqItemId) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                size="18"
                :color="expandedArray.includes(item.rfqItemId) ? 'primary' : 'grey'"
              />
              <NuxtLink
                :to="`/rfqs/${item.rfqId}?itemId=${item.rfqItemId}`"
                class="text-primary font-weight-medium text-decoration-none"
                @click.stop
              >
                {{ item.rfqId }}
              </NuxtLink>
            </div>
          </template>
          <template #item.status="{ item }">
            <v-chip
              size="small"
              :color="rfqStatusColor(item.rfqStatus || 'Open')"
              variant="tonal"
            >
              {{ item.rfqStatus || 'Open' }}
            </v-chip>
          </template>
          <template #item.supplierCount="{ item }">
            <v-chip size="small" :color="item.supplierQuotes.length > 0 ? 'success' : 'grey'" variant="tonal">
              {{ item.supplierQuotes.length }} supplier{{ item.supplierQuotes.length !== 1 ? 's' : '' }}
            </v-chip>
          </template>
          <template #item.assignedUsers="{ item }">
            <div class="d-flex flex-wrap gap-1">
              <v-chip
                v-for="user in item.assignedUsers"
                :key="user.id"
                size="x-small"
                color="primary"
                variant="tonal"
                prepend-icon="mdi-account"
              >
                {{ user.name }}
              </v-chip>
              <span v-if="!item.assignedUsers?.length" class="text-medium-emphasis">—</span>
            </div>
          </template>
          <template #item.leadTime="{ item }">
            <span :class="{ 'text-error font-weight-bold': isLeadTimeExpired(item.leadTime) }" :style="isLeadTimeUrgent(item.leadTime) ? 'font-weight: 600;' : ''">
              {{ new Date(item.leadTime).toLocaleDateString() }}
              <v-icon v-if="isLeadTimeUrgent(item.leadTime)" icon="mdi-alert" size="14" color="warning" class="ml-1" title="Lead time expires within 3 days" />
              <v-icon v-else-if="isLeadTimeExpired(item.leadTime)" icon="mdi-alert-circle" size="14" color="error" class="ml-1" title="Lead time has expired" />
            </span>
          </template>
          <template #item.customerName="{ item }">
            <template v-if="isAdmin">{{ item.customerName }}<span v-if="item.customerCode" class="text-medium-emphasis ml-1">({{ item.customerCode }})</span></template>
            <template v-else>{{ item.customerCode || '—' }}</template>
          </template>
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>

          <!-- Expanded row -->
          <template #expanded-row="{ item, columns }">
            <tr>
              <td :colspan="columns.length" class="pa-0">
                <div class="supplier-panel pa-4" style="background: rgba(var(--v-theme-surface-variant), 0.3);">
                  <div class="d-flex align-center justify-space-between mb-3">
                    <span class="text-caption text-uppercase font-weight-bold letter-spacing-wide" style="color: #60a5fa;">
                      Supplier Quotes for {{ item.partNumberName }}
                    </span>
                    <v-btn
                      size="x-small"
                      color="primary"
                      variant="flat"
                      prepend-icon="mdi-plus"
                      @click.stop="addQuoteRow(item)"
                    >
                      Add Supplier
                    </v-btn>
                  </div>

                  <div v-if="getEditableQuotes(item.rfqItemId).length > 0" style="overflow-x: auto;">
                    <table class="quote-grid" style="width: 100%; border-collapse: collapse; min-width: 1100px;">
                      <thead>
                        <tr>
                          <th>Supplier</th>
                          <th>Cond</th>
                          <th>Alt P/N</th>
                          <th>Qty</th>
                          <th>Unit</th>
                          <th>Cost Price ($)</th>
                          <th>Cert Type</th>
                          <th>Tag Date</th>
                          <th>Shipping Cost</th>
                          <th>Shipping Point</th>
                          <th>LeadTime</th>
                          <th>Note</th>
                          <th>My Notes</th>
                          <th style="width: 90px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="(quote, qIdx) in getEditableQuotes(item.rfqItemId)" :key="qIdx" class="quote-row">
                          <td>
                            <input
                              type="text"
                              class="quote-input"
                              placeholder="Supplier name..."
                              v-model="quote.supplierName"
                              @input="searchSupplier(quote.supplierName)"
                              list="supplier-suggestions"
                            />
                          </td>
                          <td>
                            <select class="quote-input quote-select" v-model="quote.condition">
                              <option value="">—</option>
                              <option value="NE">NE</option>
                              <option value="OH">OH</option>
                              <option value="SV">SV</option>
                              <option value="AR">AR</option>
                              <option value="RP">RP</option>
                              <option value="NS">NS</option>
                              <option value="FN">FN</option>
                              <option value="IN">IN</option>
                            </select>
                          </td>
                          <td>
                            <input type="text" class="quote-input" placeholder="Same P/N" v-model="quote.alt" />
                          </td>
                          <td>
                            <input type="number" class="quote-input text-center" v-model.number="quote.qty" min="1" />
                          </td>
                          <td>
                            <select class="quote-input quote-select" v-model="quote.unit">
                              <option value="">—</option>
                              <option value="EA">EA</option>
                              <option value="Meter">METER</option>
                              <option value="Kg">KG</option>
                            </select>
                          </td>
                          <td>
                            <input
                              v-if="focusedField === `price-${qIdx}-${item.rfqItemId}`"
                              :data-focus-key="`price-${qIdx}-${item.rfqItemId}`"
                              type="number"
                              class="quote-input price-input"
                              placeholder="0.00"
                              v-model.number="quote.price"
                              step="0.01"
                              min="0"
                              @blur="focusedField = ''"
                            />
                            <span
                              v-else
                              class="quote-input price-display"
                              @click.stop="focusField(`price-${qIdx}-${item.rfqItemId}`)"
                            >
                              {{ quote.price ? '$' + formatPrice(quote.price) : '' }}
                            </span>
                          </td>
                          <td>
                            <select class="quote-input quote-select" v-model="quote.certName">
                              <option value="">—</option>
                              <option value="FAA">FAA</option>
                              <option value="EASA">EASA</option>
                              <option value="CAAC">CAAC</option>
                              <option value="Dual">Dual</option>
                              <option value="MFG COC">MFG COC</option>
                              <option value="Vendor COC">Vendor COC</option>
                              <option value="No Cert">No Cert</option>
                            </select>
                          </td>
                          <td>
                            <input type="date" class="quote-input" v-model="quote.tagDate" :max="today" />
                          </td>
                          <td>
                            <input
                              v-if="focusedField === `ship-${qIdx}-${item.rfqItemId}`"
                              :data-focus-key="`ship-${qIdx}-${item.rfqItemId}`"
                              type="number"
                              class="quote-input price-input"
                              placeholder="0.00"
                              v-model.number="quote.shippingCost"
                              step="0.01"
                              min="0"
                              @blur="focusedField = ''"
                            />
                            <span
                              v-else
                              class="quote-input price-display"
                              @click.stop="focusField(`ship-${qIdx}-${item.rfqItemId}`)"
                            >
                              {{ quote.shippingCost ? '$' + formatPrice(quote.shippingCost) : '' }}
                            </span>
                          </td>
                          <td>
                            <input type="text" class="quote-input" placeholder="City / Hub" v-model="quote.shippingPoint" />
                          </td>
                          <td>
                            <input type="text" class="quote-input" placeholder="e.g. 5 days" v-model="quote.leadTime" />
                          </td>
                          <td>
                            <VTextarea
                              type="text"
                              rows="2"
                              placeholder="Note..."
                              v-model="quote.note"
                              hide-details
                              density="compact"
                              variant="plain"
                            />
                          </td>
                          <td>
                            <VTextarea
                              type="text"
                              rows="2"
                              placeholder="My Notes..."
                              v-model="quote.myNotes"
                              hide-details
                              density="compact"
                              variant="plain"
                            />
                          </td>
                          <td class="text-center" style="white-space: nowrap;">
                            <v-btn
                              icon="mdi-content-save"
                              size="x-small"
                              variant="text"
                              color="success"
                              :loading="quote._saving"
                              @click.stop="saveQuote(item, quote)"
                              title="Save"
                            />
                            <v-btn
                              icon="mdi-close"
                              size="x-small"
                              variant="text"
                              color="error"
                              @click.stop="removeQuote(item, qIdx)"
                              title="Delete"
                            />
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </div>

                  <div v-else class="text-center pa-4">
                    <v-icon icon="mdi-package-variant" size="32" color="grey-darken-1" class="mb-2" />
                    <p class="text-caption text-medium-emphasis">No supplier quotes yet. Click "Add Supplier" to start.</p>
                  </div>
                </div>
              </td>
            </tr>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Shared datalist for supplier name autocomplete -->
    <datalist id="supplier-suggestions">
      <option v-for="s in supplierSuggestions" :key="s.id" :value="s.name" />
    </datalist>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const { statusColor: rfqStatusColor } = useStatusColor()

const today = new Date().toISOString().split('T')[0]

const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('procument', {
  search: '',
  status: [] as string[],
  user: [] as number[],
  customer: [] as string[],
  partNumber: [] as string[],
})
const search = pf.search
const loading = ref(false)
const allItems = ref<any[]>([])
const editableQuotes = ref<Record<number, any[]>>({})
const expandedArray = ref<any[]>([])
const focusedField = ref('')
const supplierSuggestions = ref<{ id: number; name: string }[]>([])

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Filters ──
const statusFilter = pf.status
const userFilter = pf.user
const customerFilter = pf.customer
const partNumberFilter = pf.partNumber
const isAdmin = computed(() => authStore.isAdmin)
const statusOptions = ['Open', 'In Progress', 'No Quote', 'Quoted', 'Closed', 'Completed', 'Cancelled']

const headers = [
  { title: 'RFQ #', key: 'rfqId', width: '80px' },
  { title: 'RFQ Name', key: 'rfqName' },
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Description', key: 'description' },
  { title: 'Qty', key: 'qty', width: '70px' },
  { title: 'Condition', key: 'condition', width: '90px' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Status', key: 'status', width: '110px' },
  { title: 'Suppliers', key: 'supplierCount', sortable: false, width: '120px' },
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  { title: 'Lead Time', key: 'leadTime' },
  { title: 'Created', key: 'createdAt' },
]

const partNumberOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach((item: any) => { if (item.partNumberName) set.add(item.partNumberName) })
  return Array.from(set).sort()
})

const userOptions = computed(() => {
  const map = new Map<number, string>()
  allItems.value.forEach((item: any) => {
    ;(item.assignedUsers || []).forEach((u: any) => {
      if (u.id && u.name) map.set(u.id, u.name)
    })
  })
  return Array.from(map, ([id, name]) => ({ id, name }))
})

const customerOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach((item: any) => { if (item.customerName) set.add(item.customerName) })
  return Array.from(set).sort()
})

const filteredItems = computed(() => {
  let result = allItems.value
  if (statusFilter.value?.length) {
    result = result.filter((item: any) => statusFilter.value.includes(item.rfqStatus || 'Open'))
  }
  if (userFilter.value?.length) {
    result = result.filter((item: any) =>
      (item.assignedUsers || []).some((u: any) => userFilter.value.includes(u.id))
    )
  }
  if (customerFilter.value?.length) {
    result = result.filter((item: any) => customerFilter.value.includes(item.customerName))
  }
  if (partNumberFilter.value?.length) {
    result = result.filter((item: any) => partNumberFilter.value.includes(item.partNumberName))
  }
  return result
})

function isLeadTimeUrgent(dateStr: string) {
  if (!dateStr) return false
  const diff = new Date(dateStr).getTime() - Date.now()
  const daysLeft = diff / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 3
}

function isLeadTimeExpired(dateStr: string) {
  if (!dateStr) return false
  return new Date(dateStr).getTime() < Date.now()
}

function getRowProps({ item }: { item: any }) {
  const classes: string[] = []
  if (isLeadTimeUrgent(item.leadTime)) classes.push('lead-time-urgent-row')
  if (isLeadTimeExpired(item.leadTime)) classes.push('lead-time-expired-row')
  if (item.isHighlighted) classes.push('highlighted-row')
  if (expandedArray.value.includes(item.rfqItemId)) classes.push('expanded-row')
  return classes.length ? { class: classes.join(' ') } : {}
}

// ── Data Loading ──
onMounted(() => loadData())

async function loadData() {
  loading.value = true
  try {
    const data = await api.get<any[]>('/procument-page')
    allItems.value = (data || []).map((item: any) => ({
      ...item,
      altPartNumbers: [
        ...(item.alternatives || []).map((a: any) => a.name),
        ...(item.supplierQuotes || []).map((q: any) => q.alt),
      ].filter(Boolean).join(', '),
    }))

    // Initialize editable quotes from server data
    const quotesMap: Record<number, any[]> = {}
    for (const item of allItems.value) {
      quotesMap[item.rfqItemId] = (item.supplierQuotes || []).map((q: any) => ({ ...q, _saving: false }))
    }
    editableQuotes.value = quotesMap
  } catch (e) {
    console.error('Failed to load procument data:', e)
  } finally {
    loading.value = false
  }
}

// ── Expand / Collapse ──
function toggleExpand(item: any) {
  const id = item.rfqItemId
  const idx = expandedArray.value.indexOf(id)
  if (idx >= 0) {
    expandedArray.value.splice(idx, 1)
  } else {
    expandedArray.value.push(id)
  }
}

function getEditableQuotes(rfqItemId: number) {
  return editableQuotes.value[rfqItemId] || []
}

// ── Supplier Quote Management ──
function addQuoteRow(item: any) {
  const key = item.rfqItemId
  if (!editableQuotes.value[key]) {
    editableQuotes.value[key] = []
  }
  editableQuotes.value[key]!.push({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: '',
    qty: item.qty || 1,
    price: 0,
    condition: 'NE',
    alt: '',
    certName: '',
    tagDate: '',
    shippingCost: null,
    shippingPoint: '',
    unit: 'EA',
    leadTime: '',
    note: '',
    myNotes: '',
    _saving: false,
  })
  // Ensure expanded
  if (!expandedArray.value.includes(item.rfqItemId)) {
    expandedArray.value.push(item.rfqItemId)
  }
}

async function saveQuote(item: any, quote: any) {
  if (!quote.supplierName?.trim()) {
    showSnack('Supplier name is required', 'error')
    return
  }
  quote._saving = true
  try {
    const payload = {
      id: quote.id || null,
      rfqItemId: item.rfqItemId,
      supplierName: quote.supplierName,
      qty: quote.qty,
      price: quote.price,
      condition: quote.condition,
      alt: quote.alt,
      certName: quote.certName || null,
      tagDate: quote.tagDate || null,
      shippingCost: quote.shippingCost ?? null,
      shippingPoint: quote.shippingPoint || null,
      unit: quote.unit || null,
      leadTime: quote.leadTime || null,
      note: quote.note || null,
    }

    const result = await api.post(`/rfqs/${item.rfqId}/supplier-quotes`, payload)
    // Update the quote with the returned ID
    if (result && (result as any).id) {
      quote.id = (result as any).id
    }
    showSnack('Supplier quote saved', 'success')
  } catch (e) {
    showSnack('Failed to save quote', 'error')
  } finally {
    quote._saving = false
  }
}

async function removeQuote(item: any, qIdx: number) {
  const quotes = editableQuotes.value[item.rfqItemId]
  if (!quotes) return
  const quote = quotes[qIdx]

  if (quote.id) {
    try {
      await api.del(`/rfqs/${item.rfqId}/supplier-quotes/${quote.id}`)
      showSnack('Quote removed', 'success')
    } catch {
      showSnack('Failed to remove quote', 'error')
      return
    }
  }

  quotes.splice(qIdx, 1)
}

// ── Supplier Autocomplete ──
let supplierSearchDebounce: any = null
function searchSupplier(val: string) {
  clearTimeout(supplierSearchDebounce)
  if (!val || val.length < 1) {
    supplierSuggestions.value = []
    return
  }
  supplierSearchDebounce = setTimeout(async () => {
    try {
      supplierSuggestions.value = await api.get<{ id: number; name: string }[]>(`/suppliers/search?q=${encodeURIComponent(val)}`)
    } catch {
      supplierSuggestions.value = []
    }
  }, 300)
}

// ── Focus Field (for formatted price inputs) ──
function focusField(key: string) {
  focusedField.value = key
  nextTick(() => {
    const input = document.querySelector(`[data-focus-key="${key}"]`) as HTMLInputElement
    input?.focus()
  })
}
</script>

<style scoped>
:deep(.lead-time-urgent-row) {
  background-color: rgba(255, 193, 7, 0.08) !important;
}
:deep(.lead-time-expired-row) {
  background-color: rgba(239, 68, 68, 0.12) !important;
  border-left: 3px solid #ef4444;
}
:deep(.lead-time-expired-row) td {
  color: #ef4444 !important;
}
:deep(.lead-time-expired-row:hover) {
  background-color: rgba(239, 68, 68, 0.2) !important;
}
:deep(.highlighted-row) {
  background-color: rgba(251, 191, 36, 0.12) !important;
  border-left: 3px solid #fbbf24;
}
:deep(.expanded-row) {
  background: rgba(var(--v-theme-primary), 0.06) !important;
}

.supplier-panel {
  border-top: 2px solid rgba(var(--v-theme-primary), 0.3);
}

.quote-grid th {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 8px 10px;
  color: rgba(var(--v-theme-on-surface), 0.6);
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  text-align: left;
  white-space: nowrap;
}

.quote-row td {
  padding: 2px 4px;
  vertical-align: middle;
}

.quote-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  border-radius: 4px;
  padding: 0 8px;
  font-size: 13px;
  background: transparent;
  color: inherit;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.quote-input:hover {
  border-color: rgba(var(--v-theme-on-surface), 0.2);
}
.quote-input:focus {
  background: rgba(var(--v-theme-surface-variant), 0.5);
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px rgba(var(--v-theme-primary), 0.2);
}
.quote-input::placeholder {
  opacity: 0.4;
  font-style: italic;
}
.price-display {
  display: flex;
  align-items: center;
  cursor: text;
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
  min-height: 32px;
  padding: 0 8px;
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
</style>
