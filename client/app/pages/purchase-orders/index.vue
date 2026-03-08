<template>
  <div>
    <PageHeader title="Purchase Orders" />

    <!-- Loading -->
    <v-card v-if="loading" class="glass-card">
      <div class="text-center pa-12">
        <v-progress-circular indeterminate color="primary" class="mb-4" />
        <p class="text-body-2 text-medium-emphasis">Loading PO items...</p>
      </div>
    </v-card>

    <!-- Tabs -->
    <v-card v-else class="glass-card">
      <v-tabs v-model="activeTab" bg-color="transparent" color="primary">
        <v-tab value="orders">
          <v-icon start size="18">mdi-clipboard-list</v-icon>
          Purchase Orders
          <v-chip v-if="purchaseOrders.length" size="x-small" color="primary" variant="tonal" class="ml-2">{{ purchaseOrders.length }}</v-chip>
        </v-tab>
        <v-tab value="warehouse">
          <v-icon start size="18">mdi-warehouse</v-icon>
          Warehouse
          <v-chip v-if="warehouseItemCount" size="x-small" color="success" variant="tonal" class="ml-2">{{ warehouseItemCount }}</v-chip>
        </v-tab>
        <v-tab value="vendor">
          <v-icon start size="18">mdi-truck-outline</v-icon>
          Vendor
          <v-chip v-if="vendorItemCount" size="x-small" color="info" variant="tonal" class="ml-2">{{ vendorItemCount }}</v-chip>
        </v-tab>
        <v-tab value="customer">
          <v-icon start size="18">mdi-account-group</v-icon>
          Customer
          <v-chip v-if="customerItemCount" size="x-small" color="error" variant="tonal" class="ml-2">{{ customerItemCount }}</v-chip>
        </v-tab>
        <v-tab value="edit">
          <v-icon start size="18">mdi-pencil</v-icon>
          Edit
          <v-chip v-if="allItems.length" size="x-small" color="primary" variant="tonal" class="ml-2">{{ allItems.length }}</v-chip>
        </v-tab>
      </v-tabs>

      <v-divider />

      <v-tabs-window v-model="activeTab">
        <!-- ═══════════ PURCHASE ORDERS TAB ═══════════ -->
        <v-tabs-window-item value="orders">
          <div class="pa-4">
            <p class="text-body-2 text-medium-emphasis mb-4">
              All created <strong>Purchase Orders</strong>.
            </p>

            <div v-if="purchaseOrders.length === 0" class="text-center pa-8">
              <v-icon icon="mdi-clipboard-list" size="48" color="grey" class="mb-2" />
              <p class="text-body-2 text-medium-emphasis">No purchase orders yet.</p>
            </div>

            <div class="excel-container" v-else>
              <table class="po-table">
                <thead>
                  <tr>
                    <th style="width: 60px;">#</th>
                    <th>PO Number</th>
                    <th>Supplier</th>
                    <th style="width: 120px;">Total Amount</th>
                    <th style="width: 180px;">Status</th>
                    <th style="width: 100px;">Items</th>
                    <th style="width: 140px;">Created</th>
                    <th style="width: 80px;"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(po, idx) in purchaseOrders" :key="po.id" class="cursor-pointer" @click="$router.push(`/purchase-orders/${po.id}`)">
                    <td class="text-center" style="color: #64748b;">{{ idx + 1 }}</td>
                    <td class="cell-pn">{{ po.poNumber }}</td>
                    <td>{{ po.supplierName || '—' }}</td>
                    <td class="text-right cell-price">${{ po.totalAmount?.toFixed(2) || '0.00' }}</td>
                    <td @click.stop>
                      <v-menu :disabled="po._locked">
                        <template #activator="{ props: menuProps }">
                          <v-chip :color="poStatusColor(po.status)" size="x-small" variant="tonal" v-bind="menuProps" class="cursor-pointer" :append-icon="po._locked ? 'mdi-lock' : 'mdi-chevron-down'">
                            {{ po.status }}
                          </v-chip>
                        </template>
                        <v-list density="compact" style="min-width: 200px">
                          <v-list-subheader>Change Status</v-list-subheader>
                          <v-list-item
                            v-for="s in poStatusOptions"
                            :key="s.value"
                            :value="s.value"
                            :active="po.status === s.value"
                            @click="changePOStatus(po, s.value)"
                          >
                            <template #prepend>
                              <v-icon :icon="s.icon" :color="s.color" size="18" />
                            </template>
                            <v-list-item-title>{{ s.label }}</v-list-item-title>
                          </v-list-item>
                        </v-list>
                      </v-menu>
                    </td>
                    <td class="text-center">{{ po.items?.length || 0 }}</td>
                    <td style="color: #94a3b8; font-size: 12px;">{{ po.createdAt ? new Date(po.createdAt).toLocaleDateString() : '—' }}</td>
                    <td class="text-center">
                      <v-btn icon="mdi-arrow-right" variant="text" size="x-small" density="compact" :to="`/purchase-orders/${po.id}`" />
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </v-tabs-window-item>

        <!-- ═══════════ WAREHOUSE TAB ═══════════ -->
        <v-tabs-window-item value="warehouse">
          <div class="pa-4">
            <p class="text-body-2 text-medium-emphasis mb-4">
              All <strong>Ex Warehouse</strong> items grouped by supplier. Select items and create a Purchase Order.
            </p>

            <div v-if="Object.keys(warehouseGroups).length === 0" class="text-center pa-8">
              <v-icon icon="mdi-warehouse" size="48" color="grey" class="mb-2" />
              <p class="text-body-2 text-medium-emphasis">No warehouse items found.</p>
            </div>

            <div v-for="(group, supplierName) in warehouseGroups" :key="supplierName" class="mb-6">
              <div class="d-flex flex-wrap align-center gap-2 mb-2">
                <v-icon icon="mdi-truck-delivery" size="20" color="primary" />
                <h3 class="text-subtitle-1 font-weight-bold">{{ supplierName }}</h3>
                <v-chip size="x-small" color="primary" variant="tonal">{{ group.length }} item(s)</v-chip>
                <v-spacer />
                <v-btn
                  size="small"
                  color="success"
                  variant="tonal"
                  prepend-icon="mdi-plus"
                  :disabled="getSelectedFromGroup(group).length === 0"
                  @click="createPOFromGroup(supplierName as string, group)"
                >
                  Create PO ({{ getSelectedFromGroup(group).length }})
                </v-btn>
              </div>

              <div class="excel-container">
                <table class="po-table">
                  <thead>
                    <tr>
                      <th style="width: 40px;">
                        <input type="checkbox" class="po-checkbox" :checked="isGroupAllSelected(group)" @change="toggleGroupAll(group)" />
                      </th>
                      <th>Part Number</th>
                      <th>Alt P/N</th>
                      <th>Condition</th>
                      <th style="width: 80px;">Qty</th>
                      <th style="width: 110px;">Unit Price</th>
                      <th style="width: 110px;">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="item in group" :key="item.id" :class="{ 'selected-row': selections[item.id] }">
                      <td class="text-center">
                        <input type="checkbox" class="po-checkbox" :checked="selections[item.id]" @change="toggleSelect(item.id)" />
                      </td>
                      <td class="cell-pn">{{ item.partNumberName }}</td>
                      <td style="color: #fbbf24;">{{ item.alt || '—' }}</td>
                      <td>{{ item.condition || '—' }}</td>
                      <td class="text-center">{{ item.qty }}</td>
                      <td class="text-right cell-price">${{ item.unitPrice?.toFixed(2) || '0.00' }}</td>
                      <td class="text-right cell-price">${{ item.totalPrice?.toFixed(2) || '0.00' }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </v-tabs-window-item>

        <!-- ═══════════ VENDOR TAB ═══════════ -->
        <v-tabs-window-item value="vendor">
          <div class="pa-4">
            <p class="text-body-2 text-medium-emphasis mb-4">
              All <strong>Ex Vendor</strong> items grouped by customer, then by supplier.
            </p>

            <div v-if="Object.keys(vendorGroups).length === 0" class="text-center pa-8">
              <v-icon icon="mdi-truck-outline" size="48" color="grey" class="mb-2" />
              <p class="text-body-2 text-medium-emphasis">No vendor items found.</p>
            </div>

            <div v-for="(supplierMap, customerName) in vendorGroups" :key="customerName" class="mb-6">
              <div class="d-flex align-center gap-2 mb-3">
                <v-icon icon="mdi-account" size="20" color="info" />
                <h3 class="text-subtitle-1 font-weight-bold">{{ customerName }}</h3>
              </div>

              <div v-for="(items, supplierName) in supplierMap" :key="supplierName" class="ml-4 mb-4">
                <div class="d-flex flex-wrap align-center gap-2 mb-2">
                  <v-icon icon="mdi-truck-delivery" size="18" color="warning" />
                  <span class="text-body-2 font-weight-medium">{{ supplierName }}</span>
                  <v-chip size="x-small" color="info" variant="tonal">{{ items.length }} item(s)</v-chip>
                  <v-spacer />
                  <v-btn
                    size="small"
                    color="success"
                    variant="tonal"
                    prepend-icon="mdi-plus"
                    :disabled="getSelectedFromGroup(items).length === 0"
                    @click="createPOFromGroup(supplierName as string, items)"
                  >
                    Create PO ({{ getSelectedFromGroup(items).length }})
                  </v-btn>
                </div>

                <div class="excel-container">
                  <table class="po-table">
                    <thead>
                      <tr>
                        <th style="width: 40px;">
                          <input type="checkbox" class="po-checkbox" :checked="isGroupAllSelected(items)" @change="toggleGroupAll(items)" />
                        </th>
                        <th>Part Number</th>
                        <th>Alt P/N</th>
                        <th>Condition</th>
                        <th style="width: 80px;">Qty</th>
                        <th style="width: 110px;">Unit Price</th>
                        <th style="width: 110px;">Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="item in items" :key="item.id" :class="{ 'selected-row': selections[item.id] }">
                        <td class="text-center">
                          <input type="checkbox" class="po-checkbox" :checked="selections[item.id]" @change="toggleSelect(item.id)" />
                        </td>
                        <td class="cell-pn">{{ item.partNumberName }}</td>
                        <td style="color: #fbbf24;">{{ item.alt || '—' }}</td>
                        <td>{{ item.condition || '—' }}</td>
                        <td class="text-center">{{ item.qty }}</td>
                        <td class="text-right cell-price">${{ item.unitPrice?.toFixed(2) || '0.00' }}</td>
                        <td class="text-right cell-price">${{ item.totalPrice?.toFixed(2) || '0.00' }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
        </v-tabs-window-item>

        <!-- ═══════════ CUSTOMER TAB ═══════════ -->
        <v-tabs-window-item value="customer">
          <div class="pa-4">
            <p class="text-body-2 text-medium-emphasis mb-4">
              All <strong>Ex Customer</strong> items grouped by customer, then by supplier.
            </p>

            <div v-if="Object.keys(customerGroups).length === 0" class="text-center pa-8">
              <v-icon icon="mdi-account-group" size="48" color="grey" class="mb-2" />
              <p class="text-body-2 text-medium-emphasis">No customer items found.</p>
            </div>

            <div v-for="(supplierMap, customerName) in customerGroups" :key="customerName" class="mb-6">
              <div class="d-flex align-center gap-2 mb-3">
                <v-icon icon="mdi-account" size="20" color="info" />
                <h3 class="text-subtitle-1 font-weight-bold">{{ customerName }}</h3>
              </div>

              <div v-for="(items, supplierName) in supplierMap" :key="supplierName" class="ml-4 mb-4">
                <div class="d-flex flex-wrap align-center gap-2 mb-2">
                  <v-icon icon="mdi-truck-delivery" size="18" color="warning" />
                  <span class="text-body-2 font-weight-medium">{{ supplierName }}</span>
                  <v-chip size="x-small" color="warning" variant="tonal">{{ items.length }} item(s)</v-chip>
                  <v-spacer />
                  <v-btn
                    size="small"
                    color="success"
                    variant="tonal"
                    prepend-icon="mdi-plus"
                    :disabled="getSelectedFromGroup(items).length === 0"
                    @click="createPOFromGroup(supplierName as string, items)"
                  >
                    Create PO ({{ getSelectedFromGroup(items).length }})
                  </v-btn>
                </div>

                <div class="excel-container">
                  <table class="po-table">
                    <thead>
                      <tr>
                        <th style="width: 40px;">
                          <input type="checkbox" class="po-checkbox" :checked="isGroupAllSelected(items)" @change="toggleGroupAll(items)" />
                        </th>
                        <th>Part Number</th>
                        <th>Alt P/N</th>
                        <th>Condition</th>
                        <th style="width: 80px;">Qty</th>
                        <th style="width: 110px;">Unit Price</th>
                        <th style="width: 110px;">Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="item in items" :key="item.id" :class="{ 'selected-row': selections[item.id] }">
                        <td class="text-center">
                          <input type="checkbox" class="po-checkbox" :checked="selections[item.id]" @change="toggleSelect(item.id)" />
                        </td>
                        <td class="cell-pn">{{ item.partNumberName }}</td>
                        <td style="color: #fbbf24;">{{ item.alt || '—' }}</td>
                        <td>{{ item.condition || '—' }}</td>
                        <td class="text-center">{{ item.qty }}</td>
                        <td class="text-right cell-price">${{ item.unitPrice?.toFixed(2) || '0.00' }}</td>
                        <td class="text-right cell-price">${{ item.totalPrice?.toFixed(2) || '0.00' }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
        </v-tabs-window-item>

        <!-- ═══════════ EDIT TAB ═══════════ -->
        <v-tabs-window-item value="edit">
          <div class="pa-4">
            <div class="d-flex flex-wrap align-center gap-2 mb-4">
              <p class="text-body-2 text-medium-emphasis flex-grow-1">
                All unassigned PO items. Edit supplier, qty, and unit price then save.
              </p>
              <v-btn
                color="success"
                variant="tonal"
                prepend-icon="mdi-content-save"
                :loading="saving"
                @click="saveEdits"
              >
                Save Changes
              </v-btn>
            </div>

            <div v-if="editableItems.length === 0" class="text-center pa-8">
              <v-icon icon="mdi-pencil-off" size="48" color="grey" class="mb-2" />
              <p class="text-body-2 text-medium-emphasis">No items to edit.</p>
            </div>

            <div class="excel-container" v-else>
              <table class="po-table edit-table">
                <thead>
                  <tr>
                    <th style="width: 40px;">#</th>
                    <th>Part Number</th>
                    <th>Alt P/N</th>
                    <th>Condition</th>
                    <th style="width: 200px;">Supplier</th>
                    <th style="width: 100px;">Qty</th>
                    <th style="width: 130px;">Unit Price</th>
                    <th style="width: 120px;">Total</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(item, idx) in editableItems" :key="item.id">
                    <td class="text-center" style="color: #64748b;">{{ idx + 1 }}</td>
                    <td class="cell-pn">{{ item.partNumberName }}</td>
                    <td style="color: #fbbf24;">{{ item.alt || '—' }}</td>
                    <td>{{ item.condition || '—' }}</td>
                    <td>
                      <input type="text" class="edit-input" v-model="item.editSupplierName" placeholder="Supplier" />
                    </td>
                    <td>
                      <input type="number" class="edit-input text-center" v-model.number="item.editQty" min="1" />
                    </td>
                    <td>
                      <input type="number" class="edit-input text-right" v-model.number="item.editUnitPrice" step="0.01" min="0" />
                    </td>
                    <td class="text-right cell-price">
                      ${{ ((item.editQty || 0) * (item.editUnitPrice || 0)).toFixed(2) }}
                    </td>
                  </tr>
                </tbody>
                <tfoot>
                  <tr>
                    <td colspan="7" class="text-right" style="font-weight: 600; padding: 10px 12px; color: #94a3b8;">
                      Grand Total
                    </td>
                    <td class="text-right" style="font-weight: 700; color: #4ade80; padding: 10px 12px; font-size: 15px;">
                      ${{ editTotal.toFixed(2) }}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        </v-tabs-window-item>
      </v-tabs-window>
    </v-card>

    <!-- Snackbar -->
    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()

const loading = ref(true)
const allItems = ref<any[]>([])
const editableItems = ref<any[]>([])
const selections = ref<Record<number, boolean>>({})
const activeTab = ref('orders')
const purchaseOrders = ref<any[]>([])
const saving = ref(false)

const poStatusColorMap: Record<string, string> = {
  'Sent': 'info',
  'Accept': 'success',
  'Waiting For Payment': 'warning',
  'Payment Done': 'teal',
  'Ship To Warehouse 1': 'indigo',
  'Ship To Warehouse 2': 'deep-purple',
  'Ship To Warehouse 3': 'blue-grey',
  'Ship To Customer': 'orange',
  'Completed': 'green',
  'Cancelled': 'grey',
}
function poStatusColor(status: string) {
  return poStatusColorMap[status] || 'grey'
}
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const poStatusOptions = [
  { value: 'Sent', label: 'Sent', icon: 'mdi-send', color: 'info' },
  { value: 'Accept', label: 'Accept', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Waiting For Payment', label: 'Waiting For Payment', icon: 'mdi-clock-outline', color: 'warning' },
  { value: 'Payment Done', label: 'Payment Done', icon: 'mdi-cash-check', color: 'teal' },
  { value: 'Ship To Warehouse 1', label: 'Ship To Warehouse 1', icon: 'mdi-warehouse', color: 'indigo' },
  { value: 'Ship To Warehouse 2', label: 'Ship To Warehouse 2', icon: 'mdi-warehouse', color: 'deep-purple' },
  { value: 'Ship To Warehouse 3', label: 'Ship To Warehouse 3', icon: 'mdi-warehouse', color: 'blue-grey' },
  { value: 'Ship To Customer', label: 'Ship To Customer', icon: 'mdi-account-arrow-right', color: 'orange' },
  { value: 'Completed', label: 'Completed', icon: 'mdi-check-all', color: 'green' },
  { value: 'Cancelled', label: 'Cancelled', icon: 'mdi-cancel', color: 'grey' },
]

async function changePOStatus(po: any, newStatus: string) {
  if (newStatus === po.status) return
  try {
    await api.patch(`/purchase-orders/${po.id}/status`, { status: newStatus })
    po.status = newStatus
    showSnack(`Status changed to ${newStatus}`, 'success')
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to change status', 'error')
  }
}

// ── Warehouse tab: ExType=0, grouped by supplier ──
const warehouseItems = computed(() => allItems.value.filter(i => i.exType === 0))
const warehouseItemCount = computed(() => warehouseItems.value.length)

const warehouseGroups = computed(() => {
  const groups: Record<string, any[]> = {}
  for (const item of warehouseItems.value) {
    const supplier = item.supplierName || 'Unknown Supplier'
    if (!groups[supplier]) groups[supplier] = []
    groups[supplier].push(item)
  }
  return groups
})

// ── Vendor tab: ExType=1, grouped by customer → supplier ──
const vendorItems = computed(() => allItems.value.filter(i => i.exType === 1))
const vendorItemCount = computed(() => vendorItems.value.length)

const vendorGroups = computed(() => {
  const groups: Record<string, Record<string, any[]>> = {}
  for (const item of vendorItems.value) {
    const customer = item.customerName || 'Unknown Customer'
    const supplier = item.supplierName || 'Unknown Supplier'
    if (!groups[customer]) groups[customer] = {}
    if (!groups[customer][supplier]) groups[customer][supplier] = []
    groups[customer][supplier].push(item)
  }
  return groups
})

// ── Customer tab: ExType=2, grouped by customer → supplier ──
const customerItems = computed(() => allItems.value.filter(i => i.exType === 2))
const customerItemCount = computed(() => customerItems.value.length)

const customerGroups = computed(() => {
  const groups: Record<string, Record<string, any[]>> = {}
  for (const item of customerItems.value) {
    const customer = item.customerName || 'Unknown Customer'
    const supplier = item.supplierName || 'Unknown Supplier'
    if (!groups[customer]) groups[customer] = {}
    if (!groups[customer][supplier]) groups[customer][supplier] = []
    groups[customer][supplier].push(item)
  }
  return groups
})

// ── Edit tab total ──
const editTotal = computed(() =>
  editableItems.value.reduce((sum, i) => sum + (i.editQty || 0) * (i.editUnitPrice || 0), 0)
)

// ── Selection helpers (use item.id as key) ──
function toggleSelect(id: number) {
  selections.value[id] = !selections.value[id]
}
function getSelectedFromGroup(group: any[]) {
  return group.filter(i => selections.value[i.id])
}
function isGroupAllSelected(group: any[]) {
  return group.length > 0 && group.every(i => selections.value[i.id])
}
function toggleGroupAll(group: any[]) {
  const allSel = isGroupAllSelected(group)
  group.forEach(i => { selections.value[i.id] = !allSel })
}

// ── Load data ──
onMounted(() => {
  loadItems()
  loadPurchaseOrders()
})

async function loadItems() {
  loading.value = true
  try {
    const items = await api.get<any[]>('/purchase-orders/unassigned-items')
    allItems.value = items || []

    // Init selections
    const sel: Record<number, boolean> = {}
    allItems.value.forEach(i => { sel[i.id] = false })
    selections.value = sel

    // Init editable items
    editableItems.value = allItems.value.map(i => ({
      ...i,
      editSupplierName: i.supplierName || '',
      editQty: i.qty,
      editUnitPrice: i.unitPrice,
    }))
  } catch {
    showSnack('Failed to load data', 'error')
  } finally {
    loading.value = false
  }
}

async function loadPurchaseOrders() {
  try {
    const pos = await api.get<any[]>('/purchase-orders') || []
    purchaseOrders.value = pos
    // Check lock status for each PO in parallel
    await Promise.all(pos.map(async (po: any) => {
      try {
        const res = await api.get<any>(`/final-invoices/is-locked?entityType=po&entityId=${po.id}`)
        po._locked = res?.locked === true
      } catch {
        po._locked = false
      }
    }))
  } catch {
    // silent
  }
}

// ── Create PO from selected items in a supplier group ──
async function createPOFromGroup(supplierName: string, group: any[]) {
  const selected = getSelectedFromGroup(group)
  if (selected.length === 0) return

  try {
    const payload = {
      supplierId: selected[0].supplierId || 0,
      invoiceId: selected[0].invoiceId || null,
      poItemIds: selected.map((item: any) => item.id),
    }

    const result = await api.post<any>('/purchase-orders', payload)
    showSnack(`PO ${result.poNumber} created for ${supplierName}!`, 'success')

    // Reload items (created items are now assigned to a PO and won't appear)
    await loadItems()
    await loadPurchaseOrders()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to create PO', 'error')
  }
}

// ── Save edits ──
async function saveEdits() {
  saving.value = true
  try {
    for (const item of editableItems.value) {
      await api.put(`/purchase-orders/items/${item.id}`, {
        supplierName: item.editSupplierName,
        qty: item.editQty,
        unitPrice: item.editUnitPrice,
      })
    }
    showSnack('Changes saved successfully', 'success')
    // Reload to reflect updated supplier names
    await loadItems()
  } catch {
    showSnack('Failed to save changes', 'error')
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
.excel-container {
  overflow-x: auto;
  border-radius: 8px;
  border: 1px solid rgba(51, 65, 85, 0.5);
}

.po-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 700px;
}

.po-table thead th {
  background: rgba(30, 41, 59, 0.8);
  color: #94a3b8;
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 12px;
  border-bottom: 2px solid rgba(51, 65, 85, 0.8);
  text-align: left;
  white-space: nowrap;
}

.po-table tbody td {
  padding: 8px 12px;
  border-bottom: 1px solid rgba(51, 65, 85, 0.3);
  font-size: 13px;
  vertical-align: middle;
}

.po-table tbody tr {
  transition: background-color 0.15s;
}
.po-table tbody tr:hover {
  background: rgba(30, 41, 59, 0.4);
}
.po-table tbody tr.selected-row {
  background: rgba(59, 130, 246, 0.1);
}

.po-table tfoot td {
  border-top: 2px solid rgba(51, 65, 85, 0.6);
}

.cell-pn {
  color: #60a5fa;
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
}

.cell-price {
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
  padding-right: 12px !important;
}

.po-checkbox {
  width: 16px;
  height: 16px;
  accent-color: #3b82f6;
  cursor: pointer;
}

.edit-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: rgba(15, 23, 42, 0.4);
  color: white;
  padding: 4px 8px;
  font-size: 13px;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.edit-input:hover {
  border-color: rgba(51, 65, 85, 0.6);
}
.edit-input:focus {
  background: rgba(15, 23, 42, 0.8);
  border-color: #3b82f6;
}

.text-center { text-align: center; }
.text-right { text-align: right; }
</style>
