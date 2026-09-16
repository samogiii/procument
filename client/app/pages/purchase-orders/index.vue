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
        <v-tab value="vendor-customer">
          <v-icon start size="18">mdi-truck-delivery-outline</v-icon>
          Vendor/Customer
          <v-chip v-if="vendorCustomerItemCount" size="x-small" color="info" variant="tonal" class="ml-2">{{ vendorCustomerItemCount }}</v-chip>
        </v-tab>
        <!-- <v-tab v-if="isAdmin" value="edit">
          <v-icon start size="18">mdi-pencil</v-icon>
          Edit
          <v-chip v-if="allItems.length" size="x-small" color="primary" variant="tonal" class="ml-2">{{ allItems.length }}</v-chip>
        </v-tab> -->
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

            <!-- Filter bar -->
            <div class="d-flex align-center gap-2 mb-3 flex-wrap">
              <v-text-field
                v-model="poSearch"
                label="Search PO# / Supplier / PI"
                variant="outlined"
                density="compact"
                hide-details
                clearable
                prepend-inner-icon="mdi-magnify"
                style="min-width:200px;max-width:280px;"
              />
              <v-text-field
                v-model="poPnSearch"
                label="Search by P/N"
                variant="outlined"
                density="compact"
                hide-details
                clearable
                prepend-inner-icon="mdi-cog-outline"
                style="min-width:160px;max-width:240px;"
              />
              <v-autocomplete
                v-model="poSupplierFilter"
                :items="poSupplierOptions"
                label="Supplier"
                variant="outlined"
                density="compact"
                hide-details
                clearable
                multiple
                chips
                closable-chips
                style="min-width:160px;max-width:260px;"
              />
              <v-text-field
                v-model="poDateFrom"
                label="Created From"
                type="date"
                variant="outlined"
                density="compact"
                hide-details
                clearable
                style="min-width:155px;max-width:195px;"
              />
              <v-text-field
                v-model="poDateTo"
                label="Created To"
                type="date"
                variant="outlined"
                density="compact"
                hide-details
                clearable
                style="min-width:155px;max-width:195px;"
              />
              <v-chip
                v-if="poActiveFilterCount > 0"
                size="small"
                color="primary"
                variant="tonal"
                closable
                prepend-icon="mdi-filter"
                @click:close="clearAllPoFilters"
              >
                {{ poActiveFilterCount }} filter{{ poActiveFilterCount !== 1 ? 's' : '' }} active
              </v-chip>
              <span v-if="filteredPOs.length !== purchaseOrders.length" class="text-caption text-medium-emphasis">
                {{ filteredPOs.length }} of {{ purchaseOrders.length }} shown
              </span>
            </div>

            <div class="excel-container" v-if="purchaseOrders.length > 0">
              <table class="po-table" v-if="pageLoading">
                <thead>
                  <tr>
                    <th style="width: 60px;">#</th>
                    <th>
                      <ColFilterMenu
                          col-key="poNumber"
                          label="Supplier PO#"
                          :sortable="false"
                          :options="poAvailableValues('poNumber')"
                          :all-options="poAllValues('poNumber')"
                          :selected="poSelected('poNumber')"
                          :search="poFilterSearch['poNumber'] || ''"
                          @toggle="(v) => togglePoFilter('poNumber', v)"
                          @select-all="(vals) => setPoFilter('poNumber', vals)"
                          @clear-all="() => setPoFilter('poNumber', [])"
                          @update:search="(v) => poFilterSearch['poNumber'] = v"
                        />
                    </th>
                    <th v-if="isAdmin" style="width: 120px;">
                      <ColFilterMenu
                          col-key="invoiceNumber"
                          label="PI"
                          :sortable="false"
                          :options="poAvailableValues('invoiceNumber')"
                          :all-options="poAllValues('invoiceNumber')"
                          :selected="poSelected('invoiceNumber')"
                          :search="poFilterSearch['invoiceNumber'] || ''"
                          @toggle="(v) => togglePoFilter('invoiceNumber', v)"
                          @select-all="(vals) => setPoFilter('invoiceNumber', vals)"
                          @clear-all="() => setPoFilter('invoiceNumber', [])"
                          @update:search="(v) => poFilterSearch['invoiceNumber'] = v"
                        />
                    </th>
                    <th>
                      <ColFilterMenu
                          col-key="supplierName"
                          label="Supplier"
                          :sortable="false"
                          :options="poAvailableValues('supplierName')"
                          :all-options="poAllValues('supplierName')"
                          :selected="poSelected('supplierName')"
                          :search="poFilterSearch['supplierName'] || ''"
                          @toggle="(v) => togglePoFilter('supplierName', v)"
                          @select-all="(vals) => setPoFilter('supplierName', vals)"
                          @clear-all="() => setPoFilter('supplierName', [])"
                          @update:search="(v) => poFilterSearch['supplierName'] = v"
                        />
                    </th>
                    <th style="width: 160px;">
                      <ColFilterMenu
                          col-key="subject"
                          label="Subject"
                          :sortable="false"
                          :options="poAvailableValues('subject')"
                          :all-options="poAllValues('subject')"
                          :selected="poSelected('subject')"
                          :search="poFilterSearch['subject'] || ''"
                          @toggle="(v) => togglePoFilter('subject', v)"
                          @select-all="(vals) => setPoFilter('subject', vals)"
                          @clear-all="() => setPoFilter('subject', [])"
                          @update:search="(v) => poFilterSearch['subject'] = v"
                        />
                    </th>
                    <th style="width: 120px;">Total Amount</th>
                    <th style="width: 180px;">
                      <ColFilterMenu
                          col-key="status"
                          label="Status"
                          :sortable="false"
                          :options="poAvailableValues('status')"
                          :all-options="poAllValues('status')"
                          :selected="poSelected('status')"
                          :search="poFilterSearch['status'] || ''"
                          @toggle="(v) => togglePoFilter('status', v)"
                          @select-all="(vals) => setPoFilter('status', vals)"
                          @clear-all="() => setPoFilter('status', [])"
                          @update:search="(v) => poFilterSearch['status'] = v"
                        />
                    </th>
                    <th style="width: 100px;">Items</th>
                    <th style="width: 110px;">Track Status</th>
                    <th v-if="isAdmin" style="width: 200px;">
                      <ColFilterMenu
                          col-key="assignedUsers"
                          label="Assigned Users"
                          :sortable="false"
                          :options="poAvailableAssignedUsers"
                          :all-options="poAllAssignedUsers"
                          :selected="poSelected('assignedUsers')"
                          :search="poFilterSearch['assignedUsers'] || ''"
                          @toggle="(v) => togglePoFilter('assignedUsers', v)"
                          @select-all="(vals) => setPoFilter('assignedUsers', vals)"
                          @clear-all="() => setPoFilter('assignedUsers', [])"
                          @update:search="(v) => poFilterSearch['assignedUsers'] = v"
                        />
                    </th>
                    <th style="width: 140px;">Created</th>
                    <th style="width: 80px;"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(po, idx) in filteredPOs" :key="po.id" class="cursor-pointer" @click="$router.push(`/purchase-orders/${po.id}`)">
                    <td class="text-center text-medium-emphasis">{{ idx + 1 }}</td>
                    <td class="cell-pn">{{ po.poNumber }}</td>
                    <td v-if="isAdmin" class="text-medium-emphasis">{{ po.invoiceNumber || '—' }}</td>
                    <td>{{ po.supplierName || '—' }}</td>
                    <td class="text-medium-emphasis" :title="po.subject || ''" style="max-width:160px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">{{ po.subject || '—' }}</td>
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
                    <td class="text-center" @click.stop>
                      <template v-if="po.totalTrackItems > 0">
                        <v-chip
                          size="x-small"
                          :color="po.acceptedTrackItems === po.totalTrackItems ? 'success' : po.acceptedTrackItems > 0 ? 'warning' : 'default'"
                          variant="tonal"
                          :title="`${po.acceptedTrackItems} accepted / ${po.totalTrackItems} total track items`"
                        >
                          {{ po.acceptedTrackItems }}/{{ po.totalTrackItems }}
                        </v-chip>
                      </template>
                      <span v-else class="text-medium-emphasis" style="font-size:11px;">—</span>
                    </td>
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
                    <tr v-for="item in group" :key="item.id" :class="{ 'selected-row': selections[item.id], 'total-pn-focus-row': highlightedItemId === item.id }">
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

        <!-- ═══════════ VENDOR/CUSTOMER TAB ═══════════ -->
        <v-tabs-window-item value="vendor-customer">
          <div class="pa-4">
            <p class="text-body-2 text-medium-emphasis mb-4">
              All <strong>Vendor/Customer</strong> items grouped by customer, then by supplier.
            </p>

            <div v-if="Object.keys(vendorCustomerGroups).length === 0" class="text-center pa-8">
              <v-icon icon="mdi-truck-delivery-outline" size="48" color="grey" class="mb-2" />
              <p class="text-body-2 text-medium-emphasis">No vendor/customer items found.</p>
            </div>

            <div v-for="(supplierMap, customerName) in vendorCustomerGroups" :key="customerName" class="mb-6">
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
                        <th style="width: 100px;">Type</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="item in items" :key="item.id" :class="{ 'selected-row': selections[item.id], 'total-pn-focus-row': highlightedItemId === item.id }">
                        <td class="text-center">
                          <input type="checkbox" class="po-checkbox" :checked="selections[item.id]" @change="toggleSelect(item.id)" />
                        </td>
                        <td class="cell-pn">{{ item.partNumberName }}</td>
                        <td style="color: #fbbf24;">{{ item.alt || '—' }}</td>
                        <td>{{ item.condition || '—' }}</td>
                        <td class="text-center">{{ item.qty }}</td>
                        <td class="text-right cell-price">${{ formatPrice(item.unitPrice) }}</td>
                        <td class="text-right cell-price">${{ formatPrice(item.totalPrice) }}</td>
                        <td class="text-center">
                          <v-chip size="x-small" :color="item.exType === 1 ? 'info' : 'warning'" variant="tonal">
                            {{ item.exType === 1 ? 'Vendor' : 'Customer' }}
                          </v-chip>
                        </td>
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

    <!-- ══ Company preset picker — asked once, before the PO is created ══
         The preset identifies the buying company for PO and payment request documents. ══ -->
    <v-dialog v-model="showPresetPickerDialog" max-width="560" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4 gap-2">
          <v-icon icon="mdi-domain" color="primary" />
          Create Purchase Order
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-1">
            Supplier: <strong>{{ pendingCreate.supplierName }}</strong>
          </p>
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ pendingSelectedCount }} item(s) selected. Which company is this PO placed from?
            Optional — you can create the PO without one.
          </p>

          <v-select
            v-model="pendingCreate.presetId"
            :items="presetOptions"
            item-title="name"
            item-value="id"
            label="Company Preset (optional)"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-domain"
            :loading="presetsLoading"
            clearable
            hide-details
            class="mb-3"
          />

          <!-- /companypresets is Admin/SuperAdmin/Expert only — other roles keep the old flow -->
          <v-alert
            v-if="!presetsLoading && presetOptions.length === 0"
            type="info"
            variant="tonal"
            density="compact"
            class="text-caption"
          >
            No company presets available for your role — the PO will be created without one.
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-btn variant="text" @click="cancelCreate">Cancel</v-btn>
          <v-spacer />
          <v-btn
            color="success"
            variant="flat"
            prepend-icon="mdi-plus"
            :loading="creatingPo"
            @click="confirmCreate"
          >
            Create PO
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>



    <!-- Snackbar -->
    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const route = useRoute()
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
const highlightedItemId = ref<number | null>(null)
const purchaseOrders = ref<any[]>([])
const saving = ref(false)

const requestedPoItemId = computed(() => {
  const value = Number(route.query.poItemId)
  return Number.isSafeInteger(value) && value > 0 ? value : null
})

// ── Company preset / wallet picker (before creating PO) ──────────────────────
const showPresetPickerDialog = ref(false)
const creatingPo = ref(false)
const pendingCreate = reactive<{ supplierName: string; group: any[]; presetId: number | null }>({
  supplierName: '', group: [], presetId: null,
})

// ── Company presets — the "which of our companies is buying" choice ──
const apiPresets = ref<any[]>([])
const presetsLoading = ref(false)

async function loadPresets() {
  presetsLoading.value = true
  try { apiPresets.value = await api.get<any[]>('/companypresets') }
  catch { apiPresets.value = [] }
  finally { presetsLoading.value = false }
}

// Same rule as the PO PDF generator: admins pick any preset, everyone else is on Base 105.
const presetOptions = computed(() => {
  const list = apiPresets.value.filter((p: any) => p.isActive !== false)
  return isAdmin.value ? list : list.filter((p: any) => p.sortOrder === 105)
})

const selectedPresetName = computed(
  () => apiPresets.value.find((p: any) => p.id === pendingCreate.presetId)?.name || ''
)

const pendingSelectedCount = computed(() => getSelectedFromGroup(pendingCreate.group).length)

const poStatusColorMap: Record<string, string> = {
  'Not Started': 'grey',
  'Sourcing': 'purple',
  'EndUser': 'cyan',
  'In Shop': 'deep-orange',
  'Received in Warehouse': 'orange',
  'Waiting For Supplier Documents': 'blue',
  'Waiting For PR': 'indigo',
  'Waiting For Payment': 'orange',
  'PR Rejected': 'error',
  'Payment Done': 'success',
  'Waiting For Shipment': 'amber',
  'Ship to Warehouse': 'indigo',
  'Waiting for Expert Approval Shipment': 'deep-purple',
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
  { value: 'Not Started', label: 'Not Started', icon: 'mdi-circle-outline', color: 'grey' },
  { value: 'Sourcing', label: 'Sourcing', icon: 'mdi-source-branch', color: 'purple' },
  { value: 'EndUser', label: 'EndUser', icon: 'mdi-account-check-outline', color: 'cyan' },
  { value: 'In Shop', label: 'In Shop', icon: 'mdi-store-clock-outline', color: 'deep-orange' },
  { value: 'Received in Warehouse', label: 'Received in Warehouse', icon: 'mdi-warehouse-check', color: 'orange' },
  { value: 'Waiting For Supplier Documents', label: 'Waiting For Supplier Documents', icon: 'mdi-file-clock', color: 'blue' },
  { value: 'Waiting For PR', label: 'Waiting For PR', icon: 'mdi-file-export-outline', color: 'indigo' },
  { value: 'Waiting For Payment', label: 'Waiting For Payment', icon: 'mdi-clock-outline', color: 'orange' },
  { value: 'PR Rejected', label: 'PR Rejected', icon: 'mdi-file-remove-outline', color: 'error' },
  { value: 'Payment Done', label: 'Payment Done', icon: 'mdi-cash-check', color: 'success' },
  { value: 'Waiting For Shipment', label: 'Waiting For Shipment', icon: 'mdi-truck-fast-outline', color: 'amber' },
  { value: 'Ship to Warehouse', label: 'Ship to Warehouse', icon: 'mdi-warehouse', color: 'indigo' },
  { value: 'Waiting for Expert Approval Shipment', label: 'Waiting for Expert Approval Shipment', icon: 'mdi-account-clock-outline', color: 'deep-purple' },
  { value: 'Completed', label: 'Completed', icon: 'mdi-check-all', color: 'teal' },
  { value: 'Cancelled', label: 'Cancelled', icon: 'mdi-cancel', color: 'grey' },
  { value: 'Returned', label: 'Returned', icon: 'mdi-keyboard-return', color: 'error' },
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

// ── Vendor/Customer tab: ExType=1 or 2, grouped by customer → supplier ──
const vendorCustomerItems = computed(() => allItems.value.filter(i => i.exType === 1 || i.exType === 2))
const vendorCustomerItemCount = computed(() => vendorCustomerItems.value.length)

const vendorCustomerGroups = computed(() => {
  const groups: Record<string, Record<string, any[]>> = {}
  for (const item of vendorCustomerItems.value) {
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
function focusRequestedItem() {
  const id = requestedPoItemId.value
  if (id == null) return

  const item = allItems.value.find(item => item.id === id)
  if (!item) {
    showSnack('This item is no longer available for a new Purchase Order.', 'warning')
    return
  }

  selections.value[id] = true
  highlightedItemId.value = id
  window.setTimeout(() => {
    if (highlightedItemId.value === id) highlightedItemId.value = null
  }, 10_000)
}

onMounted(async () => {
  const tab = route.query.tab
  if (tab === 'warehouse' || tab === 'vendor-customer') activeTab.value = tab

  await Promise.all([
    loadPurchaseOrders(),
    loadItems(),
    loadPresets(),
  ])
  focusRequestedItem()
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

    const res = await api.get<any>('/purchase-orders?page=1&pageSize=1000')
    const pos: any[] = Array.isArray(res) ? res : (res.items ?? res.Items ?? [])
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

// ── Create PO from selected items — ask which company preset to buy from first ──
function createPOFromGroup(supplierName: string, group: any[]) {
  const selected = getSelectedFromGroup(group)
  if (selected.length === 0) return
  pendingCreate.supplierName = supplierName
  pendingCreate.group = group
  // Default to Base 105, falling back to the only preset the user is allowed to use.
  const options = presetOptions.value
  pendingCreate.presetId = (options.find((p: any) => p.sortOrder === 105) || options[0])?.id ?? null
  showPresetPickerDialog.value = true
}

function cancelCreate() {
  showPresetPickerDialog.value = false
}

async function confirmCreate() {
  const selected = getSelectedFromGroup(pendingCreate.group)
  if (selected.length === 0) return
  creatingPo.value = true
  try {
    const payload = {
      supplierId: selected[0].supplierId || 0,
      invoiceId: selected[0].invoiceId || null,
      poItemIds: selected.map((item: any) => item.id),
      // Store the buying company independently from payment wallets.
      companyPresetId: pendingCreate.presetId || null,
    }
    const result = await api.post<any>('/purchase-orders', payload)
    const company = result.companyPresetName || selectedPresetName.value
    showSnack(`PO ${result.poNumber} created for ${pendingCreate.supplierName}${company ? ` (${company})` : ''}!`, 'success')
    showPresetPickerDialog.value = false
    await loadItems()
    await loadPurchaseOrders()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to create PO', 'error')
  } finally {
    creatingPo.value = false
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

// ── Excel-style column filters for Purchase Orders tab ──
const poSearch = ref('')
const poPnSearch = ref('')
const poSupplierFilter = ref<string[]>([])
const poDateFrom = ref('')
const poDateTo = ref('')
const poColFilters = reactive<Record<string, Set<string>>>({})
const poFilterSearch = reactive<Record<string, string>>({})

const PO_FILTER_COLS = [
  { key: 'poNumber',      field: (r: any) => r.poNumber },
  { key: 'invoiceNumber', field: (r: any) => r.invoiceNumber || '—' },
  { key: 'supplierName',  field: (r: any) => r.supplierName || '—' },
  { key: 'subject',       field: (r: any) => r.subject || '—' },
  { key: 'status',        field: (r: any) => r.status },
]

/** Every value in the column, regardless of what else is filtered — the "Show all" list. */
function poAllValues(key: string) {
  const col = PO_FILTER_COLS.find(c => c.key === key)!
  return ([...new Set(purchaseOrders.value.map(col.field).filter(Boolean))] as string[]).sort()
}

/**
 * Values still selectable in this column: computed from the rows that survive every
 * *other* filter. Excluding the column's own filter is what keeps its remaining values
 * reachable instead of collapsing the menu to what is already ticked.
 */
function poAvailableValues(key: string) {
  const col = PO_FILTER_COLS.find(c => c.key === key)!
  return ([...new Set(applyPoFilters(purchaseOrders.value, key).map(col.field).filter(Boolean))] as string[]).sort()
}

const poAllAssignedUsers = computed(() => {
  const names = new Set<string>()
  for (const po of purchaseOrders.value) {
    for (const p of po._assignedUsers || []) {
      if (p.user?.name) names.add(p.user.name)
    }
  }
  return [...names].sort()
})

const poAvailableAssignedUsers = computed(() => {
  const names = new Set<string>()
  for (const po of applyPoFilters(purchaseOrders.value, 'assignedUsers')) {
    for (const p of po._assignedUsers || []) {
      if (p.user?.name) names.add(p.user.name)
    }
  }
  return [...names].sort()
})

function togglePoFilter(key: string, val: string) {
  if (!poColFilters[key]) poColFilters[key] = new Set()
  const next = new Set(poColFilters[key])
  if (next.has(val)) next.delete(val)
  else next.add(val)
  poColFilters[key] = next
}

function setPoFilter(key: string, vals: string[]) {
  poColFilters[key] = new Set(vals)
}

function poSelected(key: string): Set<string> {
  return poColFilters[key] || new Set()
}

const poSupplierOptions = computed(() =>
  [...new Set(purchaseOrders.value.map((po: any) => po.supplierName).filter(Boolean))].sort()
)

const poActiveFilterCount = computed(() => {
  let count = Object.values(poColFilters).filter(s => s && s.size > 0).length
  if (poSearch.value.trim()) count++
  if (poPnSearch.value.trim()) count++
  if (poSupplierFilter.value.length) count++
  if (poDateFrom.value) count++
  if (poDateTo.value) count++
  return count
})

function clearAllPoFilters() {
  for (const key of Object.keys(poColFilters)) poColFilters[key] = new Set()
  poSearch.value = ''
  poPnSearch.value = ''
  poSupplierFilter.value = []
  poDateFrom.value = ''
  poDateTo.value = ''
}

/**
 * Applies the PO filter bar + column filters. Pass `exclude` to leave one column's own
 * filter out — that is how each filter menu works out which values are still available.
 */
function applyPoFilters(source: any[], exclude?: string) {
  let rows = source
  const q = poSearch.value.trim().toLowerCase()
  if (q) {
    rows = rows.filter(r =>
      (r.poNumber || '').toLowerCase().includes(q) ||
      (r.supplierName || '').toLowerCase().includes(q) ||
      (r.invoiceNumber || '').toLowerCase().includes(q)
    )
  }
  // P/N search — matches any item in the PO
  const pn = poPnSearch.value.trim().toLowerCase()
  if (pn) {
    rows = rows.filter(r =>
      (r.items || []).some((item: any) =>
        (item.partNumberName || '').toLowerCase().includes(pn)
      )
    )
  }
  // Supplier multi-select
  if (poSupplierFilter.value.length) {
    rows = rows.filter(r => poSupplierFilter.value.includes(r.supplierName))
  }
  // Date range on createdAt
  if (poDateFrom.value) {
    const from = new Date(poDateFrom.value)
    rows = rows.filter(r => r.createdAt && new Date(r.createdAt) >= from)
  }
  if (poDateTo.value) {
    const to = new Date(poDateTo.value)
    to.setHours(23, 59, 59, 999)
    rows = rows.filter(r => r.createdAt && new Date(r.createdAt) <= to)
  }
  // Apply column filters (status, supplier, etc.)
  for (const col of PO_FILTER_COLS) {
    if (col.key === exclude) continue
    const active = poColFilters[col.key]
    if (active && active.size > 0) {
      rows = rows.filter(r => active.has(col.field(r)))
    }
  }
  // Hide Cancelled POs by default unless the user has explicitly filtered for Cancelled
  const statusFilter = poColFilters['status']
  if (exclude !== 'status' && (!statusFilter || statusFilter.size === 0)) {
    rows = rows.filter(r => r.status !== 'Cancelled')
  }
  // Assigned users is multi-value: keep row if it has ANY selected user
  const activeUsers = poColFilters['assignedUsers']
  if (exclude !== 'assignedUsers' && activeUsers && activeUsers.size > 0) {
    rows = rows.filter(r =>
      (r._assignedUsers || []).some((p: any) => activeUsers.has(p.user?.name))
    )
  }
  return rows
}

const filteredPOs = computed(() => applyPoFilters(purchaseOrders.value))
</script>

<style scoped>
.excel-container {
  overflow-x: auto;
  overflow-y: auto;
  max-height: calc(100vh - 280px);
  border-radius: 8px;
  border: 1px solid var(--card-border);
}

.po-table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  min-width: 700px;
  overflow: visible !important;
}

.po-table thead th {
  position: sticky;
  top: 0;
  z-index: 4;
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
  box-shadow: 0 1px 0 var(--excel-border);
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
.po-table tbody tr.total-pn-focus-row {
  background: rgba(255, 193, 7, 0.34);
  animation: total-pn-focus-pulse 1s ease-in-out infinite;
}

@keyframes total-pn-focus-pulse {
  50% { background: rgba(255, 152, 0, 0.58); }
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
