<template >
  <div >
    <PageHeader title="Purchase Orders" />

    <!-- Loading -->
    <v-card v-if="loading" class="glass-card">
      <div class="text-center pa-12">
        <v-progress-circular indeterminate color="primary" class="mb-4" />
        <p class="text-body-2 text-medium-emphasis">Loading PO items...</p>
      </div>
    </v-card>

    <!-- Assign toolbar (admins only) -->
    <div v-if="!loading && isAdmin" class="d-flex align-center mb-3">
      <v-spacer />
      <v-btn
        color="primary"
        variant="tonal"
        size="small"
        prepend-icon="mdi-shield-account-outline"
        @click="showAssignDialog = true"
      >
        Assign Users
      </v-btn>
    </div>

    <BulkPermissionManager v-if="isAdmin" v-model="showAssignDialog" entity-name="PO" />

    <!-- Tabs -->
    <v-card v-if="!loading" class="glass-card">
      <v-tabs v-model="activeTab" bg-color="transparent" color="primary">
        <v-tab value="orders">
          <v-icon start size="18">mdi-clipboard-list</v-icon>
          Purchase Orders
          <v-chip v-if="purchaseOrders.length" size="x-small" color="primary" variant="tonal" class="ml-2">{{ purchaseOrders.length }}</v-chip>
        </v-tab>
        <!-- ExWork tabs: visible to anyone allowed on this page (Admin, SuperAdmin, Expert, Payment).
             The backend already filters /unassigned-items per user via EntityPermission("Procurement"),
             so non-admins just see an empty list if they have no assignments. -->
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
        <v-tab v-if="isAdmin" value="edit">
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
              <table class="po-table" v-if="pageLoading">
                <thead>
                  <tr>
                    <th style="width: 60px;">#</th>
                    <th>Supplier PO#</th>
                    <th v-if="isAdmin" style="width: 120px;">PI</th>
                    <th>Supplier</th>
                    <th style="width: 120px;">Total Amount</th>
                    <th style="width: 180px;">Status</th>
                    <th style="width: 100px;">Items</th>
                    <th v-if="isAdmin" style="width: 200px;">Assigned Users</th>
                    <th style="width: 140px;">Created</th>
                    <th style="width: 80px;"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(po, idx) in purchaseOrders" :key="po.id" class="cursor-pointer" @click="$router.push(`/purchase-orders/${po.id}`)">
                    <td class="text-center text-medium-emphasis">{{ idx + 1 }}</td>
                    <td class="cell-pn">{{ po.poNumber }}</td>
                    <td v-if="isAdmin" class="text-medium-emphasis">{{ po.invoiceNumber || '—' }}</td>
                    <td>{{ po.supplierName || '—' }}</td>
                    <td class="text-right cell-price">${{ formatPrice(po.totalAmount) }}</td>
                    <td @click.stop>
                      <div class="d-flex flex-column gap-1">
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
                        <v-chip v-if="po.paymentApproval === 'Rejected'" color="error" size="x-small" variant="flat" prepend-icon="mdi-alert-circle">
                          Payment Rejected
                        </v-chip>
                      </div>
                    </td>
                    <td class="text-center">{{ po.items?.length || 0 }}</td>
                    <td v-if="isAdmin" @click.stop>
                      <div v-if="po._assignedUsers && po._assignedUsers.length" class="d-flex flex-wrap gap-1">
                        <v-chip
                          v-for="p in po._assignedUsers"
                          :key="p.id"
                          size="x-small"
                          :color="p.permission === 'Edit' ? 'success' : 'info'"
                          variant="tonal"
                          :title="`${p.permission} · Assigned ${new Date(p.createdAt).toLocaleDateString()}`"
                        >
                          {{ p.user.name }}
                        </v-chip>
                      </div>
                      <span v-else class="text-medium-emphasis" style="font-size: 12px;">—</span>
                    </td>
                    <td class="text-medium-emphasis" style="font-size: 12px;">{{ po.createdAt ? new Date(po.createdAt).toLocaleDateString() : '—' }}</td>
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
                      <td class="text-right cell-price">${{ formatPrice(item.unitPrice) }}</td>
                      <td class="text-right cell-price">${{ formatPrice(item.totalPrice) }}</td>
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
                        <td class="text-right cell-price">${{ formatPrice(item.unitPrice) }}</td>
                        <td class="text-right cell-price">${{ formatPrice(item.totalPrice) }}</td>
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
                        <td class="text-right cell-price">${{ formatPrice(item.unitPrice) }}</td>
                        <td class="text-right cell-price">${{ formatPrice(item.totalPrice) }}</td>
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
                    <td class="text-center text-medium-emphasis">{{ idx + 1 }}</td>
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
                      ${{ formatPrice((item.editQty || 0) * (item.editUnitPrice || 0)) }}
                    </td>
                  </tr>
                </tbody>
                <tfoot>
                  <tr>
                    <td colspan="7" class="text-right text-medium-emphasis" style="font-weight: 600; padding: 10px 12px;">
                      Grand Total
                    </td>
                    <td class="text-right" style="font-weight: 700; color: #4ade80; padding: 10px 12px; font-size: 15px;">
                      ${{ formatPrice(editTotal) }}
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
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)
const showAssignDialog = ref(false)
const pageLoading = ref(false)
const pageLoading1 = ref(false)
const loading = ref(true)
const allItems = ref<any[]>([])
const editableItems = ref<any[]>([])
const selections = ref<Record<number, boolean>>({})
const activeTab = ref('orders')
const purchaseOrders = ref<any[]>([])
const saving = ref(false)

const poStatusColorMap: Record<string, string> = {
  'Waiting For Admin Approval': 'warning',
  'Waiting For Payment': 'orange',
  'Payment Done': 'success',
  'Ship To Warehouse 1': 'indigo',
  'Ship To Warehouse 2': 'deep-purple',
  'Ship To Warehouse 3': 'blue-grey',
  'Ship To Customer': 'info',
  'Completed': 'teal',
  'Cancelled': 'grey',
  'Returned': 'error',
}
function poStatusColor(status: string) {
  return poStatusColorMap[status] || 'grey'
}
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const poStatusOptions = [
  { value: 'Waiting For Admin Approval', label: 'Waiting For Admin Approval', icon: 'mdi-shield-clock', color: 'warning' },
  { value: 'Waiting For Payment', label: 'Waiting For Payment', icon: 'mdi-clock-outline', color: 'orange' },
  { value: 'Payment Done', label: 'Payment Done', icon: 'mdi-cash-check', color: 'success' },
  { value: 'Ship To Warehouse 1', label: 'Ship To Warehouse 1', icon: 'mdi-warehouse', color: 'indigo' },
  { value: 'Ship To Warehouse 2', label: 'Ship To Warehouse 2', icon: 'mdi-warehouse', color: 'deep-purple' },
  { value: 'Ship To Warehouse 3', label: 'Ship To Warehouse 3', icon: 'mdi-warehouse', color: 'blue-grey' },
  { value: 'Ship To Customer', label: 'Ship To Customer', icon: 'mdi-account-arrow-right', color: 'info' },
  { value: 'Completed', label: 'Completed', icon: 'mdi-check-all', color: 'teal' },
  { value: 'Cancelled', label: 'Cancelled', icon: 'mdi-cancel', color: 'grey' },
  { value: 'Returned', label: 'Returned', icon: 'mdi-keyboard-return', color: 'error' },
]

async function changePOStatus(po: any, newStatus: string) {
  if (newStatus === po.status) return
  
  // 1. If waiting for admin approval, manual change is blocked
  if (po.adminApproval !== 'Approved' && po.status === 'Waiting For Admin Approval') {
    showSnack('Cannot manually change status until SuperAdmin approves', 'warning')
    return
  }

  // 2. If waiting for payment, manual change is blocked
  if (po.adminApproval === 'Approved' && po.paymentStatus !== 'Submitted') {
    showSnack('Cannot manually change status while Awaiting Payment', 'warning')
    return
  }

  // Once payment status is 'Submitted' (Payment Done), admin can change freely.

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
  loadPurchaseOrders()
  loadItems()
  
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
  // try {
  
    const accumulated: any[] = []
    let page = 1
    while (true) {
      const res = await api.get<any>(`/purchase-orders?page=${page}&pageSize=200`)
      const batch: any[] = Array.isArray(res) ? res : (res.items ?? res.Items ?? [])
      const total: number = (!Array.isArray(res) && res != null) ? (res.totalCount ?? res.TotalCount ?? batch.length) : batch.length
      accumulated.push(...batch)
      if (batch.length < 200 || accumulated.length >= total) break
      page++
    }
    const pos = accumulated
    purchaseOrders.value = pos

    await Promise.all(pos.map(async (po: any) => {
      try {
        const res = await api.get<any>(`/final-invoices/is-locked?entityType=po&entityId=${po.id}`)
        po._locked = res?.locked === true
        
      } catch {
        po._locked = false
      }
    })).finally(x => {
      if(!isAdmin.value){
          pageLoading.value = true
        }
    })
    if (isAdmin.value) {
      await Promise.all(pos.map( async(po: any) => {
        // try {
          po._assignedUsers = await api.get<any[]>(`/permissions/PO/${po.id}`)
          
          // console.log(po._a)
        // } catch {
          // po._assignedUsers = []
        // }
      })).finally( x => {
        pageLoading.value = true
      })

    }
    // Check lock status for each PO in parallel
    
     
    // Load assigned users per PO (admin only)
   
  // } catch {
  //   // silent
  // }
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
  border: 1px solid var(--card-border);
}

.po-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 700px;
}

.po-table thead th {
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

.po-table tbody td {
  padding: 8px 12px;
  border-bottom: 1px solid var(--card-border);
  font-size: 13px;
  vertical-align: middle;
}

.po-table tbody tr {
  transition: background-color 0.15s;
}
.po-table tbody tr:hover {
  background: var(--row-hover);
}
.po-table tbody tr.selected-row {
  background: var(--cell-hover);
}

.po-table tfoot td {
  border-top: 2px solid var(--excel-border);
}

.cell-pn {
  color: var(--pn-color);
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
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 8px;
  font-size: 13px;
  border-radius: 4px;
  outline: none;
  transition: all 0.15s;
}
.edit-input:hover {
  border-color: var(--card-border);
}
.edit-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
}

.text-center { text-align: center; }
.text-right { text-align: right; }
</style>
