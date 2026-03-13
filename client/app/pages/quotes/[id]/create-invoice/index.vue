<template>
  <div class="create-invoice-page">
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" :to="`/quotes/${route.params.id}`" class="mr-1 flex-shrink-0" size="small" />
      <div class="min-width-0">
        <h1 class="text-h6 text-sm-h5 font-weight-bold">Create Proforma Invoice</h1>
        <p class="text-caption text-medium-emphasis mt-1">
          Select items from Quote #{{ quote?.quoteNumber }} to create a proforma invoice.
        </p>
      </div>
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
            v-model="dueDate"
            label="Due Date"
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
            @click="createInvoice"
          >
            Create
          </v-btn>
        </div>
      </div>
    </v-card>

    <!-- Quote Items Grid -->
    <v-card class="excel-card">
      <div class="excel-container">
        <table class="excel-grid">
          <thead>
            <tr>
              <th style="width: 40px;">
                <input type="checkbox" :checked="isAllSelected" @change="toggleSelectAll" class="record-checkbox" />
              </th>
              <th>Part Number</th>
              <th>Description</th>
              <th style="width: 80px;">Cond</th>
              <th style="width: 100px;">Quote Qty</th>
              <th style="width: 100px;">Inv Qty</th>
              <th style="width: 140px;">Lead Time</th>
              <th style="width: 120px;">Unit Price</th>
              <th style="width: 120px;">Total</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in quoteItems"
              :key="item.id"
              class="quote-row"
              :class="{ 'selected-row': selections[item.id]?.selected }"
            >
              <td class="text-center">
                <input
                  type="checkbox"
                  :checked="selections[item.id]?.selected"
                  @change="toggleSelection(item.id)"
                  class="record-checkbox"
                />
              </td>
              <td class="cell-pn">{{ item.partNumberName }}</td>
              <td class="text-medium-emphasis" style="font-size: 13px;">{{ item.description || '—' }}</td>
              <td style="font-size: 12px;">{{ item.condition || 'N/A' }}</td>
              <td class="text-center" style="font-size: 13px;">{{ item.qty }}</td>
              <td>
                <input
                  type="number"
                  class="inv-qty-input"
                  v-model.number="selections[item.id].qty"
                  min="1"
                  :max="item.qty"
                  :disabled="!selections[item.id]?.selected"
                />
              </td>
              <td>
                <input
                  type="date"
                  class="inv-date-input"
                  v-model="selections[item.id].expectedDeliveryDate"
                  :min="today"
                  :disabled="!selections[item.id]?.selected"
                />
              </td>
              <td style="color: #4ade80; font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                ${{ item.unitPrice.toFixed(2) }}
              </td>
              <td style="color: #4ade80; font-family: monospace; text-align: right; padding-right: 12px; font-size: 13px;">
                ${{ (selections[item.id]?.qty * item.unitPrice || 0).toFixed(2) }}
              </td>
            </tr>
            <tr v-if="quoteItems.length === 0 && !loading">
              <td colspan="8" class="text-center pa-8">
                <p class="text-body-2 text-medium-emphasis">No items found in this quote.</p>
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

const today = new Date().toISOString().split('T')[0]

// State
const loading = ref(true)
const saving = ref(false)
const quote = ref<any>(null)
const quoteItems = ref<any[]>([])
const dueDate = ref('')

// selections: { [quoteItemId]: { selected: boolean, qty: number, expectedDeliveryDate: string } }
const selections = ref<Record<number, { selected: boolean; qty: number; expectedDeliveryDate: string }>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// Computeds
const selectedCount = computed(() =>
  Object.values(selections.value).filter(s => s.selected).length
)

const selectedTotal = computed(() =>
  quoteItems.value.reduce((sum, item) => {
    const sel = selections.value[item.id]
    if (sel && sel.selected) {
      return sum + (sel.qty * item.unitPrice)
    }
    return sum
  }, 0)
)

const isAllSelected = computed(() =>
  quoteItems.value.length > 0 && quoteItems.value.every(i => selections.value[i.id]?.selected)
)

onMounted(async () => {
  await loadData()
})

async function loadData() {
  loading.value = true
  try {
    const q = await api.get<any>(`/quotes/${route.params.id}`)
    quote.value = q
    quoteItems.value = q.items || []

    // Initialize selections
    const sel: Record<number, any> = {}
    quoteItems.value.forEach((i: any) => {
      sel[i.id] = { selected: true, qty: i.qty, expectedDeliveryDate: '' } // Default select all
    })
    selections.value = sel

    // Default due date: today + 30 days
    const d = new Date()
    d.setDate(d.getDate() + 30)
    dueDate.value = d.toISOString().split('T')[0] as string
  } catch (e) {
    showSnack('Failed to load quote data', 'error')
  } finally {
    loading.value = false
  }
}

function toggleSelection(itemId: number) {
  if (selections.value[itemId]) {
    selections.value[itemId].selected = !selections.value[itemId].selected
  }
}

function toggleSelectAll() {
  const allSelected = isAllSelected.value
  quoteItems.value.forEach(item => {
    if (selections.value[item.id]) {
      selections.value[item.id].selected = !allSelected
    }
  })
}

async function createInvoice() {
  const selectedEntries = Object.entries(selections.value)
    .filter(([_, s]) => s.selected)
    .map(([id, s]) => ({
      quoteItemId: Number(id),
      qty: s.qty,
      unitPrice: quoteItems.value.find(i => i.id === Number(id))?.unitPrice || 0,
      expectedDeliveryDate: s.expectedDeliveryDate || null
    }))

  if (selectedEntries.length === 0) {
    showSnack('Please select at least one item', 'warning')
    return
  }

  saving.value = true
  try {
    const payload = {
      quoteId: Number(route.params.id),
      dueDate: dueDate.value || null,
      items: selectedEntries
    }

    const res = await api.post<any>('/invoices', payload)
    showSnack('Proforma Invoice created successfully', 'success')
    setTimeout(() => {
      router.push(`/invoices/${res.id}`)
    }, 500)
  } catch (e) {
    showSnack('Failed to create proforma invoice', 'error')
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
.create-invoice-page {
  max-width: 100%;
}

.toolbar-card {
  background: var(--card-bg) !important;
  border: 1px solid var(--card-border) !important;
}

.excel-card {
  background: var(--excel-bg) !important;
  border: 1px solid var(--excel-border) !important;
  overflow: hidden;
}

.excel-container {
  overflow-x: auto;
}

.excel-grid {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
  min-width: 700px;
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
  white-space: nowrap;
}

.excel-grid tbody td {
  padding: 0 12px;
  height: 46px;
  border-bottom: 1px solid var(--card-border);
  font-size: 13px;
  vertical-align: middle;
}

.quote-row {
  transition: background-color 0.15s;
}
.quote-row:hover {
  background: var(--row-hover);
}
.quote-row.selected-row {
  background: var(--cell-hover);
}

.cell-pn {
  color: var(--pn-color);
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
}

.record-checkbox {
  width: 16px;
  height: 16px;
  accent-color: #3b82f6;
  cursor: pointer;
}

.inv-qty-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 8px;
  font-size: 13px;
  text-align: center;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.inv-qty-input:hover:not(:disabled) {
  border-color: var(--card-border);
}
.inv-qty-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
}
.inv-qty-input:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.inv-date-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 8px;
  font-size: 11px;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.inv-date-input:hover:not(:disabled) {
  border-color: var(--card-border);
}
.inv-date-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
}
.inv-date-input:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}
.inv-date-input::-webkit-calendar-picker-indicator {
  filter: var(--date-picker-invert);
  opacity: 0.6;
  cursor: pointer;
}

.text-center { text-align: center; }
</style>
