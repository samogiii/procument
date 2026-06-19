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
                      <div class="po-th-inner">
                        <span>Supplier PO#</span>
                        <v-menu :close-on-content-click="false" max-width="260">
                          <template #activator="{ props: mp }">
                            <v-btn v-bind="mp" :icon="poColFilters['poNumber']?.size ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="poColFilters['poNumber']?.size ? 'primary' : undefined" class="po-filter-btn" @click.stop />
                          </template>
                          <v-card class="pa-2" min-width="220">
                            <v-text-field v-model="poFilterSearch['poNumber']" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                            <div style="max-height:220px;overflow-y:auto;">
                              <v-checkbox v-for="val in poUniqueValues('poNumber')" :key="val" :label="val" :model-value="poColFilters['poNumber']?.has(val)" density="compact" hide-details @update:model-value="togglePoFilter('poNumber', val)" />
                            </div>
                            <v-btn v-if="poColFilters['poNumber']?.size" size="x-small" variant="text" color="error" class="mt-1" @click="poColFilters['poNumber'] = new Set()">Clear</v-btn>
                          </v-card>
                        </v-menu>
                      </div>
                    </th>
                    <th v-if="isAdmin" style="width: 120px;">
                      <div class="po-th-inner">
                        <span>PI</span>
                        <v-menu :close-on-content-click="false" max-width="260">
                          <template #activator="{ props: mp }">
                            <v-btn v-bind="mp" :icon="poColFilters['invoiceNumber']?.size ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="poColFilters['invoiceNumber']?.size ? 'primary' : undefined" class="po-filter-btn" @click.stop />
                          </template>
                          <v-card class="pa-2" min-width="220">
                            <v-text-field v-model="poFilterSearch['invoiceNumber']" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                            <div style="max-height:220px;overflow-y:auto;">
                              <v-checkbox v-for="val in poUniqueValues('invoiceNumber')" :key="val" :label="val" :model-value="poColFilters['invoiceNumber']?.has(val)" density="compact" hide-details @update:model-value="togglePoFilter('invoiceNumber', val)" />
                            </div>
                            <v-btn v-if="poColFilters['invoiceNumber']?.size" size="x-small" variant="text" color="error" class="mt-1" @click="poColFilters['invoiceNumber'] = new Set()">Clear</v-btn>
                          </v-card>
                        </v-menu>
                      </div>
                    </th>
                    <th>
                      <div class="po-th-inner">
                        <span>Supplier</span>
                        <v-menu :close-on-content-click="false" max-width="260">
                          <template #activator="{ props: mp }">
                            <v-btn v-bind="mp" :icon="poColFilters['supplierName']?.size ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="poColFilters['supplierName']?.size ? 'primary' : undefined" class="po-filter-btn" @click.stop />
                          </template>
                          <v-card class="pa-2" min-width="220">
                            <v-text-field v-model="poFilterSearch['supplierName']" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                            <div style="max-height:220px;overflow-y:auto;">
                              <v-checkbox v-for="val in poUniqueValues('supplierName')" :key="val" :label="val" :model-value="poColFilters['supplierName']?.has(val)" density="compact" hide-details @update:model-value="togglePoFilter('supplierName', val)" />
                            </div>
                            <v-btn v-if="poColFilters['supplierName']?.size" size="x-small" variant="text" color="error" class="mt-1" @click="poColFilters['supplierName'] = new Set()">Clear</v-btn>
                          </v-card>
                        </v-menu>
                      </div>
                    </th>
                    <th style="width: 160px;">
                      <div class="po-th-inner">
                        <span>Subject</span>
                        <v-menu :close-on-content-click="false" max-width="260">
                          <template #activator="{ props: mp }">
                            <v-btn v-bind="mp" :icon="poColFilters['subject']?.size ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="poColFilters['subject']?.size ? 'primary' : undefined" class="po-filter-btn" @click.stop />
                          </template>
                          <v-card class="pa-2" min-width="220">
                            <v-text-field v-model="poFilterSearch['subject']" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                            <div style="max-height:220px;overflow-y:auto;">
                              <v-checkbox v-for="val in poUniqueValues('subject')" :key="val" :label="val" :model-value="poColFilters['subject']?.has(val)" density="compact" hide-details @update:model-value="togglePoFilter('subject', val)" />
                            </div>
                            <v-btn v-if="poColFilters['subject']?.size" size="x-small" variant="text" color="error" class="mt-1" @click="poColFilters['subject'] = new Set()">Clear</v-btn>
                          </v-card>
                        </v-menu>
                      </div>
                    </th>
                    <th style="width: 120px;">Total Amount</th>
                    <th style="width: 180px;">
                      <div class="po-th-inner">
                        <span>Status</span>
                        <v-menu :close-on-content-click="false" max-width="260">
                          <template #activator="{ props: mp }">
                            <v-btn v-bind="mp" :icon="poColFilters['status']?.size ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="poColFilters['status']?.size ? 'primary' : undefined" class="po-filter-btn" @click.stop />
                          </template>
                          <v-card class="pa-2" min-width="220">
                            <v-text-field v-model="poFilterSearch['status']" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                            <div style="max-height:220px;overflow-y:auto;">
                              <v-checkbox v-for="val in poUniqueValues('status')" :key="val" :label="val" :model-value="poColFilters['status']?.has(val)" density="compact" hide-details @update:model-value="togglePoFilter('status', val)" />
                            </div>
                            <v-btn v-if="poColFilters['status']?.size" size="x-small" variant="text" color="error" class="mt-1" @click="poColFilters['status'] = new Set()">Clear</v-btn>
                          </v-card>
                        </v-menu>
                      </div>
                    </th>
                    <th style="width: 100px;">Items</th>
                    <th style="width: 110px;">Track Status</th>
                    <th v-if="isAdmin" style="width: 200px;">
                      <div class="po-th-inner">
                        <span>Assigned Users</span>
                        <v-menu :close-on-content-click="false" max-width="260">
                          <template #activator="{ props: mp }">
                            <v-btn v-bind="mp" :icon="poColFilters['assignedUsers']?.size ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="poColFilters['assignedUsers']?.size ? 'primary' : undefined" class="po-filter-btn" @click.stop />
                          </template>
                          <v-card class="pa-2" min-width="220">
                            <v-text-field v-model="poFilterSearch['assignedUsers']" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                            <div style="max-height:220px;overflow-y:auto;">
                              <v-checkbox v-for="val in poUniqueAssignedUsers" :key="val" :label="val" :model-value="poColFilters['assignedUsers']?.has(val)" density="compact" hide-details @update:model-value="togglePoFilter('assignedUsers', val)" />
                            </div>
                            <v-btn v-if="poColFilters['assignedUsers']?.size" size="x-small" variant="text" color="error" class="mt-1" @click="poColFilters['assignedUsers'] = new Set()">Clear</v-btn>
                          </v-card>
                        </v-menu>
                      </div>
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

    <!-- ══ Wallet picker dialog — HIDDEN: bank details now come from company presets ══ -->
    <v-dialog v-if="false" v-model="showWalletPickerDialog" max-width="500" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4 gap-2">
          <v-icon icon="mdi-wallet-outline" color="primary" />
          Select Payment Wallet
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-1">
            Supplier: <strong>{{ pendingCreate?.supplierName }}</strong>
          </p>
          <p class="text-body-2 text-medium-emphasis mb-4">
            Which wallet should be used to pay this supplier?
          </p>
          <v-select
            v-model="pendingCreate.walletId"
            :items="walletOptions"
            item-title="label"
            item-value="id"
            label="Pay with Wallet *"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-bank-outline"
            clearable
          >
            <template #item="{ item, props: itemProps }">
              <v-list-item v-bind="itemProps">
                <template #subtitle>
                  <span class="text-caption text-medium-emphasis">{{ item.raw.company }}</span>
                </template>
              </v-list-item>
            </template>
          </v-select>
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

// ── Wallet picker (before creating PO) ───────────────────────────────────────
const walletOptions = ref<{ id: number; label: string; company: string; currency: string }[]>([])
const showWalletPickerDialog = ref(false)
const creatingPo = ref(false)
const pendingCreate = reactive<{ supplierName: string; group: any[]; walletId: number | null }>({
  supplierName: '', group: [], walletId: null,
})

async function loadWallets() {
  try {
    const list = await api.get<any[]>('/payment-boxes/simple-list')
    walletOptions.value = list.map((w: any) => ({
      id: w.id,
      label: `${w.companyName} — ${w.name} (${w.currency})`,
      company: w.companyName,
      currency: w.currency,
    }))
  } catch { /* silent */ }
}

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
onMounted(() => {
  loadPurchaseOrders()
  loadItems()
  loadWallets()
  
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

// ── Create PO from selected items — wallet picker hidden; creates directly ──
function createPOFromGroup(supplierName: string, group: any[]) {
  const selected = getSelectedFromGroup(group)
  if (selected.length === 0) return
  pendingCreate.supplierName = supplierName
  pendingCreate.group = group
  pendingCreate.walletId = null
  // Skip wallet picker dialog — proceed directly to create
  confirmCreate()
}

function cancelCreate() {
  showWalletPickerDialog.value = false
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
      preferredWalletId: pendingCreate.walletId || null,
    }
    const result = await api.post<any>('/purchase-orders', payload)
    showSnack(`PO ${result.poNumber} created for ${pendingCreate.supplierName}!`, 'success')
    showWalletPickerDialog.value = false
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

function poUniqueValues(key: string) {
  const col = PO_FILTER_COLS.find(c => c.key === key)!
  const search = (poFilterSearch[key] || '').toLowerCase()
  const vals = [...new Set(purchaseOrders.value.map(col.field).filter(Boolean))] as string[]
  return vals.filter(v => v.toLowerCase().includes(search)).sort()
}

const poUniqueAssignedUsers = computed(() => {
  const search = (poFilterSearch['assignedUsers'] || '').toLowerCase()
  const names = new Set<string>()
  for (const po of purchaseOrders.value) {
    for (const p of po._assignedUsers || []) {
      if (p.user?.name) names.add(p.user.name)
    }
  }
  return [...names].filter(n => n.toLowerCase().includes(search)).sort()
})

function togglePoFilter(key: string, val: string) {
  if (!poColFilters[key]) poColFilters[key] = new Set()
  if (poColFilters[key].has(val)) poColFilters[key].delete(val)
  else poColFilters[key].add(val)
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

const filteredPOs = computed(() => {
  let rows = purchaseOrders.value
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
    const active = poColFilters[col.key]
    if (active && active.size > 0) {
      rows = rows.filter(r => active.has(col.field(r)))
    }
  }
  // Hide Cancelled POs by default unless the user has explicitly filtered for Cancelled
  const statusFilter = poColFilters['status']
  if (!statusFilter || statusFilter.size === 0) {
    rows = rows.filter(r => r.status !== 'Cancelled')
  }
  // Assigned users is multi-value: keep row if it has ANY selected user
  const activeUsers = poColFilters['assignedUsers']
  if (activeUsers && activeUsers.size > 0) {
    rows = rows.filter(r =>
      (r._assignedUsers || []).some((p: any) => activeUsers.has(p.user?.name))
    )
  }
  return rows
})
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

.po-th-inner {
  display: flex;
  align-items: center;
  gap: 2px;
  white-space: nowrap;
}
.po-filter-btn {
  opacity: 0.5;
  flex-shrink: 0;
}
.po-filter-btn:hover,
.po-filter-btn.v-btn--active {
  opacity: 1;
}
</style>
