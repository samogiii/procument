<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold">Total Project</h1>
        <div class="text-caption text-medium-emphasis">
          One row per PO line — joined across PO, Procurement, Invoice, Quote, Final Invoice and Customer Payments.
          <template v-if="authStore.ourInventoryMenu && effectiveOrigin !== 'customer'">Our Stock PO lines follow the customer rows.</template>
        </div>
      </div>
      <v-spacer />
      <!-- Our Inventory: Stock PO lines are listed after customer rows (only for users with Our Inventory access) -->
      <v-btn-toggle v-if="authStore.ourInventoryMenu" v-model="origin" mandatory density="compact" variant="outlined" divided color="primary">
        <v-btn value="all" size="small">All</v-btn>
        <v-btn value="customer" size="small">Customer orders</v-btn>
        <v-btn value="stock" size="small" prepend-icon="mdi-warehouse">Our Stock POs</v-btn>
      </v-btn-toggle>
      <div class="d-flex flex-wrap align-center gap-2 total-price-summary">
        <v-chip color="primary" variant="tonal" class="font-weight-bold">
          Total Purchase: ${{ formatPrice(visibleTotals.purchase) }}
        </v-chip>
        <v-chip color="success" variant="tonal" class="font-weight-bold">
          Total Sell: ${{ formatPrice(visibleTotals.sell) }}
        </v-chip>
      </div>
      <v-chip v-if="tpnActiveFilterCount > 0" color="primary" size="small" closable @click:close="tpnClearAllFilters">
        {{ tpnActiveFilterCount }} filter{{ tpnActiveFilterCount > 1 ? 's' : '' }} active
      </v-chip>
      <v-text-field
        v-model="search"
        prepend-inner-icon="mdi-magnify"
        placeholder="Filter rows..."
        density="compact"
        hide-details
        clearable
        variant="outlined"
        style="max-width: 280px;"
      />
      <v-btn variant="tonal" color="primary" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>

      <!-- Column Visibility Toggle Dropdown -->
      <v-menu :close-on-content-click="false" location="bottom end">
        <template #activator="{ props }">
          <v-btn
            v-bind="props"
            variant="tonal"
            color="secondary"
            prepend-icon="mdi-view-column"
          >
            Columns
            <v-badge
              v-if="visibleColumns.length < availableColumns.length"
              color="error"
              :content="String(availableColumns.length - visibleColumns.length)"
              inline
              class="ml-1"
            />
          </v-btn>
        </template>
        <v-card min-width="300" max-width="360" class="pa-3">
          <div class="d-flex align-center justify-space-between mb-2">
            <span class="text-subtitle-2 font-weight-bold">Table Columns</span>
            <v-btn
              size="x-small"
              variant="text"
              color="primary"
              @click="resetColumns"
            >
              Reset (Show All)
            </v-btn>
          </div>
          <v-divider class="mb-2" />
          
          <!-- Column list search -->
          <v-text-field
            v-model="columnSearchQuery"
            density="compact"
            hide-details
            variant="outlined"
            placeholder="Search columns..."
            prepend-inner-icon="mdi-magnify"
            class="mb-2"
            clearable
          />

          <!-- Scrollable columns checklist -->
          <div style="max-height: 280px; overflow-y: auto;">
            <div
              v-for="col in filteredColumnList"
              :key="col.key"
              class="d-flex align-center py-1 px-2 rounded hover-row"
              @click="toggleColumn(col.key)"
              style="cursor: pointer;"
            >
              <v-checkbox-btn
                :model-value="visibleColumns.includes(col.key)"
                density="compact"
                color="primary"
                class="mr-2"
                @click.stop="toggleColumn(col.key)"
              />
              <span class="text-body-2">{{ col.label }}</span>
            </div>
          </div>
        </v-card>
      </v-menu>

      <v-menu>
        <template #activator="{ props }">
          <v-btn v-bind="props" variant="tonal" color="success" prepend-icon="mdi-download" :disabled="!filteredRows.length">Export</v-btn>
        </template>
        <v-list density="compact">
          <v-list-item prepend-icon="mdi-file-delimited-outline" title="Export CSV" @click="exportCsv" />
          <v-list-item prepend-icon="mdi-microsoft-excel" title="Export Excel" @click="exportExcel" />
        </v-list>
      </v-menu>
    </div>

    <v-card class="glass-card">
      <div v-if="loading && !rows.length" class="d-flex justify-center pa-12">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <div v-else-if="!filteredRows.length" class="text-center pa-12">
        <v-icon icon="mdi-table-off" size="48" color="grey" class="mb-2" />
        <p class="text-body-2 text-medium-emphasis">No rows.</p>
      </div>

      <div v-else>
      <div ref="topScroll" class="excel-scroll-top" @scroll="syncScrollFromTop">
        <div :style="{ width: `${tableScrollWidth}px` }" />
      </div>
      <div ref="tableScroll" class="excel-container" :style="{ maxHeight: tableMaxHeight }" @scroll="syncScrollFromTable">
        <table class="tpn-table">
          <thead>
            <tr>
              <th>#</th>

              <!-- PO# — sortable + filter -->
              <th v-if="visibleColumns.includes('poNumber')">
                <div class="tpn-th-inner">
                  <span class="tpn-th-label sortable-th" @click="toggleSort('poNumber')">PO# <v-icon :icon="sortIcon('poNumber')" size="12" /></span>
                  <v-menu :close-on-content-click="false" location="bottom start">
                    <template #activator="{ props: mp }">
                      <button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['poNumber']?.size }">
                        <v-icon icon="mdi-filter-outline" size="11" />
                      </button>
                    </template>
                    <v-card min-width="200" max-width="260" class="pa-2">
                      <v-text-field v-model="tpnFilterSearch['poNumber']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" />
                      <div style="max-height:200px;overflow-y:auto">
                        <div v-for="val in tpnDisplayVals('poNumber')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('poNumber', val) }" @click.stop="tpnToggleFilter('poNumber', val)">
                          <v-checkbox-btn :model-value="tpnColFilters['poNumber']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('poNumber', val)" />
                          <span class="text-caption">{{ val }}</span>
                        </div>
                      </div>
                      <v-divider class="my-1" />
                      <v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('poNumber')">All</v-btn>
                      <v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('poNumber')">Clear</v-btn>
                      <v-divider class="my-1" />
                      <v-list-item :title="tpnShowAll['poNumber'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['poNumber'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('poNumber')" />
                    </v-card>
                  </v-menu>
                </div>
              </th>

              <!-- PO Ref# -->
              <th v-if="visibleColumns.includes('poRef')"><div class="tpn-th-inner"><span class="tpn-th-label">PO Ref#</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['poRef']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['poRef']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('poRef')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('poRef', val) }" @click.stop="tpnToggleFilter('poRef', val)"><v-checkbox-btn :model-value="tpnColFilters['poRef']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('poRef', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('poRef')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('poRef')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['poRef'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['poRef'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('poRef')" /></v-card></v-menu></div></th>

              <th v-if="visibleColumns.includes('experts')"><div class="tpn-th-inner"><span class="tpn-th-label">Expert</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['experts']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['experts']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('experts')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('experts', val) }" @click.stop="tpnToggleFilter('experts', val)"><v-checkbox-btn :model-value="tpnColFilters['experts']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('experts', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('experts')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('experts')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['experts'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['experts'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('experts')" /></v-card></v-menu></div></th>

              <!-- Customer — sortable -->
              <th v-if="visibleColumns.includes('customer')">
                <div class="tpn-th-inner">
                  <span class="tpn-th-label sortable-th" @click="toggleSort('customer')">Customer <v-icon :icon="sortIcon('customer')" size="12" /></span>
                  <v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['customer']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['customer']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('customer')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('customer', val) }" @click.stop="tpnToggleFilter('customer', val)"><v-checkbox-btn :model-value="tpnColFilters['customer']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('customer', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('customer')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('customer')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['customer'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['customer'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('customer')" /></v-card></v-menu>
                </div>
              </th>

              <!-- Supplier -->
              <th v-if="visibleColumns.includes('supplier')"><div class="tpn-th-inner"><span class="tpn-th-label">Supplier</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['supplier']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['supplier']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('supplier')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('supplier', val) }" @click.stop="tpnToggleFilter('supplier', val)"><v-checkbox-btn :model-value="tpnColFilters['supplier']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('supplier', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('supplier')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('supplier')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['supplier'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['supplier'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('supplier')" /></v-card></v-menu></div></th>

              <!-- P/N — sortable -->
              <th v-if="visibleColumns.includes('partNumber')">
                <div class="tpn-th-inner">
                  <span class="tpn-th-label sortable-th" @click="toggleSort('partNumber')">P/N <v-icon :icon="sortIcon('partNumber')" size="12" /></span>
                  <v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['partNumber']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['partNumber']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('partNumber')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('partNumber', val) }" @click.stop="tpnToggleFilter('partNumber', val)"><v-checkbox-btn :model-value="tpnColFilters['partNumber']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('partNumber', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('partNumber')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('partNumber')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['partNumber'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['partNumber'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('partNumber')" /></v-card></v-menu>
                </div>
              </th>

              <!-- Description -->
              <th v-if="visibleColumns.includes('description')"><div class="tpn-th-inner"><span class="tpn-th-label">Description</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['description']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="220" max-width="300" class="pa-2"><v-text-field v-model="tpnFilterSearch['description']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('description')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('description', val) }" @click.stop="tpnToggleFilter('description', val)"><v-checkbox-btn :model-value="tpnColFilters['description']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('description', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('description')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('description')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['description'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['description'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('description')" /></v-card></v-menu></div></th>

              <!-- QTY — sortable -->
              <th v-if="visibleColumns.includes('qty')">
                <div class="tpn-th-inner">
                  <span class="tpn-th-label sortable-th" @click="toggleSort('qty')">QTY <v-icon :icon="sortIcon('qty')" size="12" /></span>
                  <v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['qty']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="160" max-width="220" class="pa-2"><v-text-field v-model="tpnFilterSearch['qty']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('qty')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('qty', val) }" @click.stop="tpnToggleFilter('qty', val)"><v-checkbox-btn :model-value="tpnColFilters['qty']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('qty', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('qty')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('qty')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['qty'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['qty'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('qty')" /></v-card></v-menu>
                </div>
              </th>

              <th v-if="visibleColumns.includes('inTransitQty')"><div class="tpn-th-inner"><span class="tpn-th-label">In Transit</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['inTransitQty']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="160" max-width="220" class="pa-2"><v-text-field v-model="tpnFilterSearch['inTransitQty']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('inTransitQty')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('inTransitQty', val) }" @click.stop="tpnToggleFilter('inTransitQty', val)"><v-checkbox-btn :model-value="tpnColFilters['inTransitQty']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('inTransitQty', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('inTransitQty')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('inTransitQty')">Clear</v-btn></v-card></v-menu></div></th>
              <th v-if="visibleColumns.includes('receivedQty')"><div class="tpn-th-inner"><span class="tpn-th-label sortable-th" @click="toggleSort('receivedQty')">Received QTY <v-icon :icon="sortIcon('receivedQty')" size="12" /></span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['receivedQty']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="160" max-width="220" class="pa-2"><v-text-field v-model="tpnFilterSearch['receivedQty']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('receivedQty')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('receivedQty', val) }" @click.stop="tpnToggleFilter('receivedQty', val)"><v-checkbox-btn :model-value="tpnColFilters['receivedQty']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('receivedQty', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('receivedQty')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('receivedQty')">Clear</v-btn></v-card></v-menu></div></th>
              <th v-if="visibleColumns.includes('inWarehouseQty')"><div class="tpn-th-inner"><span class="tpn-th-label">In Warehouse</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['inWarehouseQty']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="160" max-width="220" class="pa-2"><v-text-field v-model="tpnFilterSearch['inWarehouseQty']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('inWarehouseQty')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('inWarehouseQty', val) }" @click.stop="tpnToggleFilter('inWarehouseQty', val)"><v-checkbox-btn :model-value="tpnColFilters['inWarehouseQty']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('inWarehouseQty', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('inWarehouseQty')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('inWarehouseQty')">Clear</v-btn></v-card></v-menu></div></th>
              <th v-if="visibleColumns.includes('remainingQty')"><div class="tpn-th-inner"><span class="tpn-th-label sortable-th" @click="toggleSort('remainingQty')">Remaining QTY <v-icon :icon="sortIcon('remainingQty')" size="12" /></span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['remainingQty']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="160" max-width="220" class="pa-2"><v-text-field v-model="tpnFilterSearch['remainingQty']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('remainingQty')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('remainingQty', val) }" @click.stop="tpnToggleFilter('remainingQty', val)"><v-checkbox-btn :model-value="tpnColFilters['remainingQty']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('remainingQty', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('remainingQty')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('remainingQty')">Clear</v-btn></v-card></v-menu></div></th>

              <!-- CD -->
              <th v-if="visibleColumns.includes('condition')"><div class="tpn-th-inner"><span class="tpn-th-label">CD</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['condition']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="160" max-width="220" class="pa-2"><v-text-field v-model="tpnFilterSearch['condition']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('condition')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('condition', val) }" @click.stop="tpnToggleFilter('condition', val)"><v-checkbox-btn :model-value="tpnColFilters['condition']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('condition', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('condition')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('condition')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['condition'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['condition'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('condition')" /></v-card></v-menu></div></th>

              <!-- Priority -->
              <th v-if="visibleColumns.includes('priority')"><div class="tpn-th-inner"><span class="tpn-th-label">Priority</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['priority']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="160" max-width="220" class="pa-2"><v-text-field v-model="tpnFilterSearch['priority']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('priority')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('priority', val) }" @click.stop="tpnToggleFilter('priority', val)"><v-checkbox-btn :model-value="tpnColFilters['priority']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('priority', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('priority')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('priority')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['priority'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['priority'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('priority')" /></v-card></v-menu></div></th>

              <!-- Warehouse -->
              <th v-if="visibleColumns.includes('warehouse')"><div class="tpn-th-inner"><span class="tpn-th-label">Warehouse</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['warehouse']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['warehouse']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('warehouse')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('warehouse', val) }" @click.stop="tpnToggleFilter('warehouse', val)"><v-checkbox-btn :model-value="tpnColFilters['warehouse']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('warehouse', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('warehouse')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('warehouse')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['warehouse'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['warehouse'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('warehouse')" /></v-card></v-menu></div></th>

              <!-- SN# -->
              <th v-if="visibleColumns.includes('serialNumber')"><div class="tpn-th-inner"><span class="tpn-th-label">SN#</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['serialNumber']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['serialNumber']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('serialNumber')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('serialNumber', val) }" @click.stop="tpnToggleFilter('serialNumber', val)"><v-checkbox-btn :model-value="tpnColFilters['serialNumber']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('serialNumber', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('serialNumber')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('serialNumber')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['serialNumber'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['serialNumber'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('serialNumber')" /></v-card></v-menu></div></th>

              <!-- PI# to Customer -->
              <th v-if="visibleColumns.includes('customerInvoiceNumber')"><div class="tpn-th-inner"><span class="tpn-th-label">PI# to Customer</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['customerInvoiceNumber']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['customerInvoiceNumber']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('customerInvoiceNumber')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('customerInvoiceNumber', val) }" @click.stop="tpnToggleFilter('customerInvoiceNumber', val)"><v-checkbox-btn :model-value="tpnColFilters['customerInvoiceNumber']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('customerInvoiceNumber', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('customerInvoiceNumber')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('customerInvoiceNumber')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['customerInvoiceNumber'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['customerInvoiceNumber'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('customerInvoiceNumber')" /></v-card></v-menu></div></th>

              <th v-if="visibleColumns.includes('purchasingUnitPriceUsd')" class="text-right">Purchasing Unit Price (USD)</th>
              <th v-if="visibleColumns.includes('purchasingTotalPriceUsd')" class="text-right">Purchasing Total Price (USD)</th>
              <!-- DP# -->
              <th v-if="visibleColumns.includes('dpNumber')"><div class="tpn-th-inner"><span class="tpn-th-label">DP#</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['dpNumber']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['dpNumber']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('dpNumber')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('dpNumber', val) }" @click.stop="tpnToggleFilter('dpNumber', val)"><v-checkbox-btn :model-value="tpnColFilters['dpNumber']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('dpNumber', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('dpNumber')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('dpNumber')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['dpNumber'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['dpNumber'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('dpNumber')" /></v-card></v-menu></div></th>

              <th v-if="visibleColumns.includes('supplierDeliveryTime')">Supplier Delivery Time</th>

              <!-- Status — sortable -->
              <th v-if="visibleColumns.includes('status')">
                <div class="tpn-th-inner">
                  <span class="tpn-th-label sortable-th" @click="toggleSort('status')">Status <v-icon :icon="sortIcon('status')" size="12" /></span>
                  <v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['status']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="280" class="pa-2"><v-text-field v-model="tpnFilterSearch['status']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('status')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('status', val) }" @click.stop="tpnToggleFilter('status', val)"><v-checkbox-btn :model-value="tpnColFilters['status']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('status', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('status')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('status')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['status'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['status'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('status')" /></v-card></v-menu>
                </div>
              </th>

              <th v-if="visibleColumns.includes('sellingUnitPriceUsd')" class="text-right">Selling Unit Price (USD)</th>
              <th v-if="visibleColumns.includes('sellingTotalPriceUsd')" class="text-right">Selling Total Price (USD)</th>
              <th v-if="visibleColumns.includes('sellingUnitPriceYuan')" class="text-right">Selling Unit Price (Yuan)</th>
              <th v-if="visibleColumns.includes('sellingTotalPriceYuan')" class="text-right">Selling Total Price (Yuan)</th>
              <!-- PO Date — filterable -->
              <th v-if="visibleColumns.includes('poDate')"><div class="tpn-th-inner"><span class="tpn-th-label">PO Date</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['poDate']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['poDate']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('poDate')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('poDate', val) }" @click.stop="tpnToggleFilter('poDate', val)"><v-checkbox-btn :model-value="tpnColFilters['poDate']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('poDate', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('poDate')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('poDate')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['poDate'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['poDate'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('poDate')" /></v-card></v-menu></div></th>

              <!-- INV Date — sortable -->
              <th v-if="visibleColumns.includes('invDate')">
                <div class="tpn-th-inner">
                  <span class="tpn-th-label sortable-th" @click="toggleSort('invDate')">INV Date <v-icon :icon="sortIcon('invDate')" size="12" /></span>
                  <v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['invDate']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['invDate']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('invDate')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('invDate', val) }" @click.stop="tpnToggleFilter('invDate', val)"><v-checkbox-btn :model-value="tpnColFilters['invDate']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('invDate', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('invDate')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('invDate')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['invDate'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['invDate'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('invDate')" /></v-card></v-menu>
                </div>
              </th>

              <th v-if="visibleColumns.includes('received')" class="text-right">Received (Payment)</th>
              <th v-if="visibleColumns.includes('receivedDate')">Received Date (Payment)</th>

              <!-- Payment Term -->
              <th v-if="visibleColumns.includes('paymentTerm')"><div class="tpn-th-inner"><span class="tpn-th-label">Payment Term</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['paymentTerm']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['paymentTerm']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('paymentTerm')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('paymentTerm', val) }" @click.stop="tpnToggleFilter('paymentTerm', val)"><v-checkbox-btn :model-value="tpnColFilters['paymentTerm']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('paymentTerm', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('paymentTerm')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('paymentTerm')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['paymentTerm'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['paymentTerm'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('paymentTerm')" /></v-card></v-menu></div></th>

              <th v-if="visibleColumns.includes('customerDeliveryTime')">Customer Delivery Time</th>
              <th v-if="visibleColumns.includes('rate')" class="text-right">Rate</th>

              <!-- Track# -->
              <th v-if="visibleColumns.includes('trackNumbers')"><div class="tpn-th-inner"><span class="tpn-th-label">Track#</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['trackNumbers']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['trackNumbers']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('trackNumbers')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('trackNumbers', val) }" @click.stop="tpnToggleFilter('trackNumbers', val)"><v-checkbox-btn :model-value="tpnColFilters['trackNumbers']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('trackNumbers', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('trackNumbers')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('trackNumbers')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['trackNumbers'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['trackNumbers'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('trackNumbers')" /></v-card></v-menu></div></th>

              <!-- Shipping Status -->
              <th v-if="visibleColumns.includes('shippingStatus')"><div class="tpn-th-inner"><span class="tpn-th-label">Shipping Status</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['shippingStatus']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="280" class="pa-2"><v-text-field v-model="tpnFilterSearch['shippingStatus']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('shippingStatus')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('shippingStatus', val) }" @click.stop="tpnToggleFilter('shippingStatus', val)"><v-checkbox-btn :model-value="tpnColFilters['shippingStatus']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('shippingStatus', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('shippingStatus')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('shippingStatus')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['shippingStatus'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['shippingStatus'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('shippingStatus')" /></v-card></v-menu></div></th>

              <th v-if="visibleColumns.includes('shippingCost')" class="text-right">Shipping Cost</th>
              <th v-if="visibleColumns.includes('note')">NOTE 02</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="(r, idx) in filteredRows"
              :key="r.id"
              :class="[rowStatusClass(r.status), { 'missing-supplier-row': r.supplier === 'No Supplier' }]"
            >
              <td class="text-center">{{ (page - 1) * pageSize + idx + 1 }}</td>
              <td v-if="visibleColumns.includes('poNumber')" class="font-weight-bold">
                <NuxtLink v-if="r.poNumber && r.purchaseOrderId" :to="`/purchase-orders/${r.purchaseOrderId}`" class="text-primary text-decoration-none hover-underline">
                  {{ r.poNumber }}
                </NuxtLink>
                <button
                  v-else-if="r.id > 0"
                  type="button"
                  class="missing-po-link"
                  title="Open this item in Purchase Orders"
                  @click="openMissingPo(r)"
                >
                  Create PO
                </button>
                <span v-else class="text-medium-emphasis">-</span>
              </td>
              <td v-if="visibleColumns.includes('poRef')" class="text-center">{{ r.poRef ?? '-' }}</td>
              <td v-if="visibleColumns.includes('experts')">
                <div v-if="r.experts?.length" class="d-flex flex-wrap gap-1">
                  <v-chip v-for="expert in r.experts" :key="expert" size="x-small" color="primary" variant="tonal">
                    {{ expert }}
                  </v-chip>
                </div>
                <span v-else class="text-medium-emphasis">-</span>
              </td>
              <td v-if="visibleColumns.includes('customer')" class="cell-wrap">
                <v-chip v-if="isStockRow(r)" size="x-small" color="purple" variant="tonal" prepend-icon="mdi-warehouse">{{ r.customer }}</v-chip>
                <template v-else>{{ r.customer || '-' }}</template>
              </td>
              <td v-if="visibleColumns.includes('supplier')" class="cell-wrap">
                <button
                  v-if="canOpenSupplierEditor(r)"
                  type="button"
                  class="supplier-link"
                  @click="openSupplierEditor(r)"
                >
                  {{ r.supplier || 'Select supplier' }}
                </button>
                <span v-else class="cell-wrap">{{ r.supplier || '-' }}</span>
              </td>
              <td v-if="visibleColumns.includes('partNumber')" class="cell-pn cell-wrap">{{ r.partNumber || '-' }}</td>
              <td v-if="visibleColumns.includes('description')" class="cell-wrap">{{ r.description || '-' }}</td>
              <td v-if="visibleColumns.includes('qty')" class="text-center">
                <v-tooltip v-if="r.receivedQty != null" location="top">
                  <template #activator="{ props }">
                    <div v-bind="props" class="qty-progress-cell">
                      <span>{{ r.qty }}</span>
                      <span class="qty-progress-line" :class="qtyProgressClass(r)">↓{{ r.receivedQty }} · {{ r.remainingQty }} left</span>
                    </div>
                  </template>
                  Ordered: {{ qtyValue(r.qty) }} · In transit: {{ qtyValue(r.inTransitQty) }} · Received: {{ qtyValue(r.receivedQty) }} · In warehouse: {{ qtyValue(r.inWarehouseQty) }} · Remaining: {{ qtyValue(r.remainingQty) }}
                </v-tooltip>
                <span v-else>{{ r.qty }}</span>
              </td>
              <td v-if="visibleColumns.includes('inTransitQty')" class="text-center">{{ qtyValue(r.inTransitQty) }}</td>
              <td v-if="visibleColumns.includes('receivedQty')" class="text-center">{{ qtyValue(r.receivedQty) }}</td>
              <td v-if="visibleColumns.includes('inWarehouseQty')" class="text-center">{{ qtyValue(r.inWarehouseQty) }}</td>
              <td v-if="visibleColumns.includes('remainingQty')" class="text-center">{{ qtyValue(r.remainingQty) }}</td>
              <td v-if="visibleColumns.includes('condition')">{{ r.condition || '-' }}</td>
              <td v-if="visibleColumns.includes('priority')" class="cell-wrap">{{ r.priority || '-' }}</td>
              <td v-if="visibleColumns.includes('warehouse')" class="cell-wrap">{{ r.warehouse || '-' }}</td>
              <td v-if="visibleColumns.includes('serialNumber')">
                <v-chip v-if="r.serialNumber" size="x-small" color="primary" variant="tonal" class="font-weight-bold">
                  {{ r.serialNumber }}
                </v-chip>
                <span v-else class="text-medium-emphasis">-</span>
              </td>
              <td v-if="visibleColumns.includes('customerInvoiceNumber')">
                <NuxtLink
                  v-if="r.invoiceId && r.customerInvoiceNumber"
                  :to="`/invoices/${r.invoiceId}`"
                  class="text-primary text-decoration-none hover-underline font-weight-medium"
                >
                  {{ r.customerInvoiceNumber }}
                </NuxtLink>
                <span v-else>{{ r.customerInvoiceNumber || '-' }}</span>
              </td>
              <td v-if="visibleColumns.includes('purchasingUnitPriceUsd')" class="text-right">${{ formatPrice(r.purchasingUnitPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('purchasingTotalPriceUsd')" class="text-right cell-price">${{ formatPrice(r.purchasingTotalPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('supplierDeliveryTime')" class="cell-wrap">{{ r.supplierDeliveryTime || '-' }}</td>
              <td v-if="visibleColumns.includes('status')">
                <select
                  :value="r.status || 'Not Started'"
                  class="status-select"
                  :class="statusColorClass(r.status)"
                  @change="updateStatus(r, ($event.target as HTMLSelectElement).value)"
                >
                  <option v-if="isInShopCountdown(r.status)" :value="r.status">{{ r.status }}</option>
                  <option v-for="s in statusOptions" :key="s" :value="s">{{ s }}</option>
                </select>
              </td>
              <td v-if="visibleColumns.includes('sellingUnitPriceUsd')" class="text-right">${{ formatPrice(r.sellingUnitPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('sellingTotalPriceUsd')" class="text-right cell-price">${{ formatPrice(r.sellingTotalPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('sellingUnitPriceYuan')" class="text-right">¥{{ formatPrice(r.sellingUnitPriceYuan) }}</td>
              <td v-if="visibleColumns.includes('sellingTotalPriceYuan')" class="text-right cell-price">¥{{ formatPrice(r.sellingTotalPriceYuan) }}</td>
              <td v-if="visibleColumns.includes('poDate')" class="text-caption">{{ formatDate(r.poDate) }}</td>
              <td v-if="visibleColumns.includes('invDate')" class="text-caption">{{ formatDate(r.invDate) }}</td>
              <td v-if="visibleColumns.includes('received')" class="text-right">{{ r.received != null ? `$${formatPrice(r.received)}` : '-' }}</td>
              <td v-if="visibleColumns.includes('receivedDate')" class="text-caption">{{ formatDate(r.receivedDate) }}</td>
              <td v-if="visibleColumns.includes('paymentTerm')">
                <v-chip v-if="r.paymentTerm" size="x-small" :color="paymentTermColor(r.paymentTerm)" variant="tonal" class="font-weight-bold">
                  {{ r.paymentTerm }}
                </v-chip>
                <span v-else class="text-medium-emphasis">-</span>
              </td>
              <td v-if="visibleColumns.includes('customerDeliveryTime')" class="text-caption">{{ formatDate(r.customerDeliveryTime) }}</td>
              <td v-if="visibleColumns.includes('rate')" class="text-center text-caption">{{ r.rate }}</td>
              <td v-if="visibleColumns.includes('trackNumbers')" class="text-caption cell-wrap">{{ r.trackNumbers || '-' }}</td>
              <td v-if="visibleColumns.includes('shippingStatus')">
                <v-chip v-if="r.shippingStatus" size="x-small" :color="shippingStatusColor(r.shippingStatus)" variant="tonal" class="font-weight-bold">
                  {{ r.shippingStatus }}
                </v-chip>
                <span v-else class="text-medium-emphasis">-</span>
              </td>
              <td v-if="visibleColumns.includes('shippingCost')" class="text-right">{{ r.shippingCost != null ? `$${formatPrice(r.shippingCost)}` : '-' }}</td>
              <td v-if="visibleColumns.includes('note')">
                <input
                  type="text"
                  :value="r.note || ''"
                  class="note-input"
                  placeholder="Add note..."
                  @blur="updateNote(r, ($event.target as HTMLInputElement).value)"
                />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      </div>

      <!-- Pagination footer -->
      <div class="d-flex flex-wrap align-center justify-end gap-3 pa-3 border-t">
        <div class="d-flex align-center gap-2">
          <span class="text-caption text-medium-emphasis">Rows per page:</span>
          <v-select
            v-model="pageSize"
            :items="pageSizeOptions"
            density="compact"
            variant="outlined"
            hide-details
            style="width: 90px;"
            @update:model-value="onPageSizeChange"
          />
          <span class="text-caption text-medium-emphasis text-no-wrap">
            {{ rangeStart }}–{{ rangeEnd }} of {{ totalCount }} rows
          </span>
        </div>
        <v-pagination
          v-if="totalPages > 1"
          v-model="page"
          :length="totalPages"
          :total-visible="7"
          density="compact"
          @update:model-value="onPageChange"
        />
      </div>
    </v-card>

    <v-dialog v-model="supplierDialog" max-width="1100" @update:model-value="onSupplierDialogChange">
      <v-card v-if="supplierEditor.row">
        <v-card-title class="d-flex align-center gap-2 flex-wrap">
          <v-icon icon="mdi-truck-cog-outline" color="primary" />
          Check purchase items — {{ supplierEditor.row.partNumber }}
          <v-spacer />
          <v-chip size="small" variant="tonal">Qty {{ supplierEditor.item?.qty ?? supplierEditor.row.qty }}</v-chip>
          <v-btn icon="mdi-close" variant="text" size="small" @click="supplierDialog = false" />
        </v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">
            Select one or more suppliers. Multiple selected rows create separate PO items using each row's quantity and price.
          </p>
          <div class="supplier-editor-table-wrap">
            <table class="supplier-editor-table">
              <thead><tr><th>Select</th><th>Supplier</th><th>Alt P/N</th><th>Qty</th><th>Price (USD)</th><th>Condition</th><th>Lead time</th><th>Approve</th></tr></thead>
              <tbody>
                <tr v-for="quote in supplierEditor.quotes" :key="quote.id || quote.tempId" :class="{ 'selected-supplier-row': quote.isSelected }">
                  <td><v-checkbox-btn v-model="quote.isSelected" color="success" :disabled="savingSupplierChanges" /></td>
                  <td><input v-model="quote.supplierName" class="supplier-editor-input" placeholder="Supplier name" :disabled="savingSupplierChanges" /></td>
                  <td><input v-model="quote.alt" class="supplier-editor-input" placeholder="Alternative P/N" :disabled="savingSupplierChanges" /></td>
                  <td><input v-model.number="quote.qty" class="supplier-editor-input number-input" type="number" min="1" :disabled="savingSupplierChanges" /></td>
                  <td><input v-model.number="quote.price" class="supplier-editor-input number-input" type="number" min="0" step="0.01" :disabled="savingSupplierChanges" /></td>
                  <td><input v-model="quote.condition" class="supplier-editor-input" :disabled="savingSupplierChanges" /></td>
                  <td><input v-model="quote.leadTime" class="supplier-editor-input" :disabled="savingSupplierChanges" /></td>
                  <td>
                    <v-btn v-if="isAdmin && quote.isSelected && !quote.hasActivePOItem" size="x-small" color="success" variant="tonal" :loading="approvingQuoteId === quote.id" :disabled="!quote.id || savingSupplierChanges" @click="approveSupplierQuote(quote)">Approve</v-btn>
                    <v-chip v-else-if="quote.hasActivePOItem" size="x-small" color="success" variant="tonal">Approved</v-chip>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <v-btn class="mt-3" size="small" color="primary" variant="tonal" prepend-icon="mdi-plus" :disabled="savingSupplierChanges" @click="addSupplierQuote">Add supplier</v-btn>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" :disabled="savingSupplierChanges" @click="supplierDialog = false">Cancel</v-btn>
          <v-spacer />
          <v-btn color="primary" variant="flat" :loading="savingSupplierChanges" @click="saveSupplierChanges">Save changes</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="showPoCancellationWarning" max-width="560" persistent>
      <v-card>
        <v-card-title class="text-warning">Cancel existing PO?</v-card-title>
        <v-card-text>
          PO <strong>{{ supplierEditor.row?.poNumber }}</strong> is still open. Saving supplier, quantity, or price changes will cancel it permanently and reopen this item for approval. You will need to create a new PO afterwards.
        </v-card-text>
        <v-card-actions><v-btn variant="text" @click="showPoCancellationWarning = false">Keep PO</v-btn><v-spacer /><v-btn color="error" variant="flat" :loading="savingSupplierChanges" @click="confirmPoCancellationAndSave">Cancel PO and continue</v-btn></v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
import { downloadExcel } from '~/utils/exportExcel'

const api = useApi()
const router = useRouter()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

interface ColumnConfig {
  key: string
  label: string
}

const ALL_COLUMNS: ColumnConfig[] = [
  { key: 'poNumber', label: 'PO#' },
  { key: 'poRef', label: 'PO Ref#' },
  { key: 'experts', label: 'Expert' },
  { key: 'customer', label: 'Customer' },
  { key: 'supplier', label: 'Supplier' },
  { key: 'partNumber', label: 'P/N' },
  { key: 'description', label: 'Description' },
  { key: 'qty', label: 'QTY' },
  { key: 'inTransitQty', label: 'In Transit' },
  { key: 'receivedQty', label: 'Received QTY' },
  { key: 'inWarehouseQty', label: 'In Warehouse' },
  { key: 'remainingQty', label: 'Remaining QTY' },
  { key: 'condition', label: 'CD' },
  { key: 'priority', label: 'Priority' },
  { key: 'warehouse', label: 'Warehouse' },
  { key: 'serialNumber', label: 'SN#' },
  { key: 'customerInvoiceNumber', label: 'PI# to Customer' },
  { key: 'purchasingUnitPriceUsd', label: 'Purchasing Unit Price (USD)' },
  { key: 'purchasingTotalPriceUsd', label: 'Purchasing Total Price (USD)' },
  { key: 'supplierDeliveryTime', label: 'Supplier Delivery Time' },
  { key: 'status', label: 'Status' },
  { key: 'sellingUnitPriceUsd', label: 'Selling Unit Price (USD)' },
  { key: 'sellingTotalPriceUsd', label: 'Selling Total Price (USD)' },
  { key: 'sellingUnitPriceYuan', label: 'Selling Unit Price (Yuan)' },
  { key: 'sellingTotalPriceYuan', label: 'Selling Total Price (Yuan)' },
  { key: 'poDate', label: 'PO Date' },
  { key: 'invDate', label: 'INV Date' },
  { key: 'received', label: 'Received (Payment)' },
  { key: 'receivedDate', label: 'Received Date (Payment)' },
  { key: 'paymentTerm', label: 'Payment Term' },
  { key: 'customerDeliveryTime', label: 'Customer Delivery Time' },
  { key: 'rate', label: 'Rate' },
  { key: 'trackNumbers', label: 'Track#' },
  { key: 'shippingStatus', label: 'Shipping Status' },
  { key: 'shippingCost', label: 'Shipping Cost' },
  { key: 'note', label: 'NOTE 02' }
]

const serverAllowedColumnKeys = ref<string[]>([])
const availableColumns = computed(() => ALL_COLUMNS.filter(column => serverAllowedColumnKeys.value.includes(column.key)))
const visibleColumns = ref<string[]>([])
const COL_VISIBILITY_STORAGE_KEY = computed(() => `total-pn-column-visibility-v2:${authStore.user?.id ?? 'anonymous'}`)
const DEFAULT_HIDDEN_COLUMNS = new Set(['inTransitQty', 'inWarehouseQty'])

async function loadColumnAccess() {
  const allowed = await api.get<string[]>('/po-items/total-pn/columns')
  const validAllowed = allowed.filter(key => ALL_COLUMNS.some(column => column.key === key))
  serverAllowedColumnKeys.value = validAllowed

  let saved: string[] = []
  if (import.meta.client) {
    try {
      const raw = localStorage.getItem(COL_VISIBILITY_STORAGE_KEY.value)
      const parsed = raw ? JSON.parse(raw) : []
      if (Array.isArray(parsed)) saved = parsed
    } catch {}
  }
  visibleColumns.value = saved.length
    ? saved.filter(key => validAllowed.includes(key))
    : validAllowed.filter(key => !DEFAULT_HIDDEN_COLUMNS.has(key))
}

function saveColumnVisibility() {
  if (!import.meta.client) return
  try {
    localStorage.setItem(COL_VISIBILITY_STORAGE_KEY.value, JSON.stringify(visibleColumns.value))
  } catch {}
}

function toggleColumn(key: string) {
  if (visibleColumns.value.includes(key)) {
    if (visibleColumns.value.length > 1) {
      visibleColumns.value = visibleColumns.value.filter(k => k !== key)
    }
  } else {
    visibleColumns.value = [...visibleColumns.value, key]
  }
  saveColumnVisibility()
}

function resetColumns() {
  visibleColumns.value = availableColumns.value.map(c => c.key)
  saveColumnVisibility()
}

const columnSearchQuery = ref('')

const filteredColumnList = computed(() => {
  const q = columnSearchQuery.value.trim().toLowerCase()
  if (!q) return availableColumns.value
  return availableColumns.value.filter(c => c.label.toLowerCase().includes(q))
})

const loading = ref(true)
const rows = ref<any[]>([])
const topScroll = ref<HTMLElement | null>(null)
const tableScroll = ref<HTMLElement | null>(null)
const tableScrollWidth = ref(0)
const serverTotalCount = ref(0)
const page = ref(1)
const PAGE_SIZE_STORAGE_KEY = 'total-pn-page-size'
const pageSizeOptions = [25, 50, 100, 200, 500, 1000, 2000]
const storedPageSize = import.meta.client ? Number(localStorage.getItem(PAGE_SIZE_STORAGE_KEY)) : 50
const pageSize = ref(pageSizeOptions.includes(storedPageSize) ? storedPageSize : 50)
const loadedAllRows = ref(false)
const totalCount = computed(() => loadedAllRows.value ? locallyFilteredRows.value.length : serverTotalCount.value)
const totalPages = computed(() => pageSize.value > 0 ? Math.ceil(totalCount.value / pageSize.value) : 1)
const rangeStart = computed(() => totalCount.value > 0 ? (page.value - 1) * pageSize.value + 1 : 0)
const rangeEnd = computed(() => totalCount.value > 0 ? Math.min(rangeStart.value + filteredRows.value.length - 1, totalCount.value) : 0)
const tableMaxHeight = computed(() => `calc(100vh - 260px)`)
const search = ref('')
const sortBy = ref('')
const sortDesc = ref(false)

function updateTableScrollWidth() {
  tableScrollWidth.value = tableScroll.value?.scrollWidth ?? 0
}

function syncScrollFromTop() {
  if (topScroll.value && tableScroll.value) {
    tableScroll.value.scrollLeft = topScroll.value.scrollLeft
  }
}

function syncScrollFromTable() {
  if (topScroll.value && tableScroll.value) {
    topScroll.value.scrollLeft = tableScroll.value.scrollLeft
  }
}

watch([rows, visibleColumns], () => {
  nextTick(updateTableScrollWidth)
}, { deep: true })

// ── Column filter state ──────────────────────────────────────────────────────

/**
 * Columns whose filter values are fetched from the backend (server-side filtering).
 * Everything else stays client-side (filters within the current page after the
 * server has already narrowed results by the server-side columns).
 */
const SERVER_SIDE_FILTER_KEYS = new Set([
  'customer', 'customerInvoiceNumber', 'partNumber', 'condition',
  'poNumber', 'supplier', 'paymentTerm', 'status', 'shippingStatus',
])

const hasClientSideFilters = computed(() => TPN_COLUMNS.some(column =>
  !SERVER_SIDE_FILTER_KEYS.has(column.key) && tpnColFilters[column.key]?.size > 0
))
const requiresLocalPaging = computed(() =>
  hasClientSideFilters.value || !!search.value?.trim() || pageSize.value > 500
)

const TPN_COLUMNS = [
  { key: 'poNumber',              field: (r: any) => r.poNumber },
  { key: 'poRef',                 field: (r: any) => r.poRef },
  { key: 'experts',               field: (r: any) => r.experts },
  { key: 'customer',              field: (r: any) => r.customer },
  { key: 'supplier',              field: (r: any) => r.supplier },
  { key: 'partNumber',            field: (r: any) => r.partNumber },
  { key: 'description',           field: (r: any) => r.description },
  { key: 'qty',                   field: (r: any) => r.qty != null ? String(r.qty) : null },
  { key: 'inTransitQty',          field: (r: any) => r.inTransitQty != null ? String(r.inTransitQty) : null },
  { key: 'receivedQty',           field: (r: any) => r.receivedQty != null ? String(r.receivedQty) : null },
  { key: 'inWarehouseQty',        field: (r: any) => r.inWarehouseQty != null ? String(r.inWarehouseQty) : null },
  { key: 'remainingQty',          field: (r: any) => r.remainingQty != null ? String(r.remainingQty) : null },
  { key: 'condition',             field: (r: any) => r.condition },
  { key: 'priority',              field: (r: any) => r.priority },
  { key: 'warehouse',             field: (r: any) => r.warehouse },
  { key: 'serialNumber',          field: (r: any) => r.serialNumber },
  { key: 'customerInvoiceNumber', field: (r: any) => r.customerInvoiceNumber },
  { key: 'status',                field: (r: any) => r.status },
  { key: 'poDate',                field: (r: any) => r.poDate ? new Date(r.poDate).toLocaleDateString() : null },
  { key: 'invDate',               field: (r: any) => r.invDate ? new Date(r.invDate).toLocaleDateString() : null },
  { key: 'paymentTerm',           field: (r: any) => r.paymentTerm },
  { key: 'trackNumbers',          field: (r: any) => r.trackNumbers },
  { key: 'shippingStatus',        field: (r: any) => r.shippingStatus },
]

const tpnColFilters = reactive<Record<string, Set<string>>>({})
const tpnFilterSearch = reactive<Record<string, string>>({})

/** Available options respect every active server-side filter except their own column. */
const filterOptions = ref<Record<string, string[]>>({})
/** Full options are kept separately for the explicit "Show all" mode. */
const allFilterOptions = ref<Record<string, string[]>>({})
const filterOptionsLoading = ref(false)

function mapFilterOptions(opts: any): Record<string, string[]> {
  return {
    customer:              opts.customers        ?? [],
    customerInvoiceNumber: opts.invoiceNumbers   ?? [],
    partNumber:            opts.partNumbers      ?? [],
    condition:             opts.conditions       ?? [],
    poNumber:              opts.poNumbers        ?? [],
    supplier:              opts.suppliers        ?? [],
    paymentTerm:           opts.paymentTerms     ?? [],
    status:                opts.statuses         ?? [],
    shippingStatus:        opts.shippingStatuses ?? [],
  }
}

function activeServerFilterQuery(): Record<string, string[]> {
  const query: Record<string, string[]> = {}
  const mappings: Record<string, string> = {
    customer: 'customers',
    customerInvoiceNumber: 'invoiceNumbers',
    partNumber: 'partNumbers',
    condition: 'conditions',
    poNumber: 'poNumbers',
    supplier: 'suppliers',
    paymentTerm: 'paymentTerms',
    status: 'poStatuses',
    shippingStatus: 'shippingStatuses',
  }
  for (const [columnKey, queryKey] of Object.entries(mappings)) {
    const selected = tpnColFilters[columnKey]
    if (selected?.size) query[queryKey] = [...selected]
  }
  return query
}

// ── Our Inventory rows ──
// "all" lists customer rows first, then Stock PO lines. The API returns customer rows only for
// users without Our Inventory access, whatever is sent here.
const ORIGIN_STORAGE_KEY = 'total-pn-origin'
const origin = ref<'all' | 'customer' | 'stock'>('all')
if (import.meta.client) {
  try {
    const saved = localStorage.getItem(ORIGIN_STORAGE_KEY)
    if (saved === 'all' || saved === 'customer' || saved === 'stock') origin.value = saved
  } catch {}
}
const effectiveOrigin = computed(() => (authStore.ourInventoryMenu ? origin.value : 'customer'))
const isStockRow = (r: any) => !r.invoiceId && r.customer === 'OUR STOCK'
watch(origin, (value) => {
  if (import.meta.client) { try { localStorage.setItem(ORIGIN_STORAGE_KEY, value) } catch {} }
  page.value = 1
  Promise.all([load(), loadFilterOptions(), loadFilterOptions(false)])
})

async function loadFilterOptions(includeActiveFilters = true) {
  filterOptionsLoading.value = true
  try {
    const opts = await api.get<any>('/po-items/total-pn/filter-options', {
      query: { ...(includeActiveFilters ? activeServerFilterQuery() : {}), origin: effectiveOrigin.value },
    })
    const mapped = mapFilterOptions(opts)
    if (includeActiveFilters) filterOptions.value = mapped
    else allFilterOptions.value = mapped
  } catch { /* non-critical */ }
  finally { filterOptionsLoading.value = false }
}

// ── Total PN localStorage persistence ──
const TPN_STORAGE_KEY = 'col-filter-total-pn'

if (import.meta.client) {
  try {
    const raw = localStorage.getItem(TPN_STORAGE_KEY)
    if (raw) {
      const parsed: Record<string, string[]> = JSON.parse(raw)
      for (const [key, vals] of Object.entries(parsed)) {
        if (Array.isArray(vals) && vals.length) tpnColFilters[key] = new Set(vals)
      }
    }
  } catch {}
}

function saveTpnFilters() {
  if (!import.meta.client) return
  const toStore: Record<string, string[]> = {}
  for (const [key, set] of Object.entries(tpnColFilters)) {
    if (set && set.size > 0) toStore[key] = [...set]
  }
  try { localStorage.setItem(TPN_STORAGE_KEY, JSON.stringify(toStore)) } catch {}
}

const tpnActiveFilterCount = computed(() =>
  Object.values(tpnColFilters).filter(s => s && s.size > 0).length
)

function tpnCellValues(raw: unknown): string[] {
  const values = Array.isArray(raw) ? raw : [raw]
  const normalized = values
    .map(value => value != null && String(value).trim() !== '' ? String(value) : '(Blank)')
  return normalized.length ? normalized : ['(Blank)']
}

function tpnRowMatchesFilter(row: any, column: typeof TPN_COLUMNS[number], selected: Set<string>) {
  return tpnCellValues(column.field(row)).some(value => selected.has(value))
}

/** All unique values for a column — from backend options (server-side) or current page rows (client-side). */
function tpnUniqueVals(key: string): string[] {
  const s = (tpnFilterSearch[key] || '').toLowerCase()

  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    const opts = allFilterOptions.value[key] ?? []
    const results = s ? opts.filter(v => v.toLowerCase().includes(s)) : opts
    return results.length ? results : ['(Blank)']
  }

  const col = TPN_COLUMNS.find(c => c.key === key)
  if (!col) return []
  const vals = new Set<string>()
  for (const r of rows.value) {
    for (const value of tpnCellValues(col.field(r))) {
      if (!s || value.toLowerCase().includes(s)) vals.add(value)
    }
  }
  return [...vals].sort((a, b) => a === '(Blank)' ? 1 : b === '(Blank)' ? -1 : a.localeCompare(b))
}

/** "Available" values — subset that pass all OTHER active filters (for client-side columns). */
function tpnUniqueValsAvail(key: string): string[] {
  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    const search = (tpnFilterSearch[key] || '').toLowerCase()
    const opts = filterOptions.value[key] ?? []
    const available = opts
    const clientFilters = TPN_COLUMNS.filter(column =>
      !SERVER_SIDE_FILTER_KEYS.has(column.key) && column.key !== key && tpnColFilters[column.key]?.size)
    if (clientFilters.length === 0) return search ? available.filter(v => v.toLowerCase().includes(search)) : available

    const allowed = new Set<string>()
    for (const row of rows.value) {
      if (!clientFilters.every(column => tpnRowMatchesFilter(row, column, tpnColFilters[column.key]))) continue
      const column = TPN_COLUMNS.find(candidate => candidate.key === key)
      if (column) tpnCellValues(column.field(row)).forEach(value => allowed.add(value))
    }
    return available.filter(value => allowed.has(value) && (!search || value.toLowerCase().includes(search)))
  }

  const col = TPN_COLUMNS.find(c => c.key === key)
  if (!col) return []
  const s = (tpnFilterSearch[key] || '').toLowerCase()
  const vals = new Set<string>()
  for (const r of rows.value) {
    let ok = true
    for (const c of TPN_COLUMNS) {
      if (c.key === key || SERVER_SIDE_FILTER_KEYS.has(c.key)) continue
      const sel = tpnColFilters[c.key]
      if (!sel || sel.size === 0) continue
      if (!tpnRowMatchesFilter(r, c, sel)) { ok = false; break }
    }
    if (!ok) continue
    for (const value of tpnCellValues(col.field(r))) {
      if (!s || value.toLowerCase().includes(s)) vals.add(value)
    }
  }
  return [...vals].sort((a, b) => a === '(Blank)' ? 1 : b === '(Blank)' ? -1 : a.localeCompare(b))
}

// Per-column show-all toggle state
const tpnShowAll = reactive<Record<string, boolean>>({})
function tpnToggleShowAll(key: string) { tpnShowAll[key] = !tpnShowAll[key] }

function tpnDisplayVals(key: string): string[] {
  return tpnShowAll[key] ? tpnUniqueVals(key) : tpnUniqueValsAvail(key)
}

function tpnIsUnavail(key: string, val: string): boolean {
  return !!(tpnShowAll[key] && !tpnUniqueValsAvail(key).includes(val))
}

function resetOtherShowAll(exceptKey?: string) {
  for (const key of Object.keys(tpnShowAll)) {
    if (key !== exceptKey) tpnShowAll[key] = false
  }
}

async function reloadRowsAndAvailableOptions() {
  await Promise.all([load(), loadFilterOptions()])
}

function tpnToggleFilter(key: string, val: string) {
  if (!tpnColFilters[key]) tpnColFilters[key] = new Set()
  if (tpnColFilters[key].has(val)) tpnColFilters[key].delete(val)
  else tpnColFilters[key].add(val)
  tpnColFilters[key] = new Set(tpnColFilters[key]) // trigger reactivity
  saveTpnFilters()
  resetOtherShowAll(key)
  page.value = 1
  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    reloadRowsAndAvailableOptions()
  } else {
    load()
  }
}

function tpnClearFilter(key: string) {
  if (tpnColFilters[key]) tpnColFilters[key] = new Set()
  saveTpnFilters()
  resetOtherShowAll(key)
  page.value = 1
  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    reloadRowsAndAvailableOptions()
  } else {
    load()
  }
}

function tpnSelectAll(key: string) {
  // Select only what the user can currently see; hidden invalid values remain
  // opt-in through the explicit "Show all" mode.
  const vals = tpnDisplayVals(key)
  tpnColFilters[key] = new Set(vals)
  saveTpnFilters()
  resetOtherShowAll(key)
  page.value = 1
  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    reloadRowsAndAvailableOptions()
  } else {
    load()
  }
}

function tpnClearAllFilters() {
  for (const key of Object.keys(tpnColFilters)) {
    tpnColFilters[key] = new Set()
  }
  saveTpnFilters()
  resetOtherShowAll()
  page.value = 1
  reloadRowsAndAvailableOptions()
}

function toggleSort(key: string) {
  if (sortBy.value === key) {
    sortDesc.value = !sortDesc.value
  } else {
    sortBy.value = key
    sortDesc.value = false
  }
  page.value = 1
  load()
}

function sortIcon(key: string) {
  if (sortBy.value !== key) return 'mdi-unfold-more-horizontal'
  return sortDesc.value ? 'mdi-arrow-down' : 'mdi-arrow-up'
}
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const supplierDialog = ref(false)
const showPoCancellationWarning = ref(false)
const savingSupplierChanges = ref(false)
const approvingQuoteId = ref<number | null>(null)
const supplierEditor = reactive<{ row: any; item: any; quotes: any[]; originalSelection: Record<number, boolean>; originalState: string }>({
  row: null,
  item: null,
  quotes: [],
  originalSelection: {},
  originalState: '',
})

const statusOptions = [
  'Not Started',
  'Sourcing',
  'EndUser',
  'In Shop',
  'Received in Warehouse',
  'Waiting For Supplier Documents',
  'Waiting For PR',
  'Waiting For Payment',
  'PR Rejected',
  'Payment Done',
  'Waiting For Shipment',
  'Ship to Warehouse',
  'Waiting for Expert Approval Shipment',
  'Completed',
  'Returned',
  'Cancelled',
]

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

function openMissingPo(row: any) {
  const tab = row.warehouse === 'Warehouse' ? 'warehouse' : 'vendor-customer'
  router.push({
    path: '/purchase-orders',
    query: {
      source: 'total-pn',
      poItemId: String(row.id),
      tab,
    },
  })
}

async function openSupplierEditor(row: any) {
  if (!canOpenSupplierEditor(row)) {
    showSnack('Purchase items can be edited only while the Proforma Invoice is Waiting For Prepayment or Running.', 'warning')
    return
  }
  try {
    let editorRow = row
    let procurement: any
    if (row.procurementId && row.procurementItemId) {
      procurement = await api.get<any>(`/procurements/${row.procurementId}`)
    } else {
      procurement = await api.post<any>(`/procurements/from-invoice/${row.invoiceId}`, {})
      const invoiceItemId = row.invoiceItemId ?? (row.id < 0 ? -row.id : null)
      const createdItem = procurement.items?.find((candidate: any) => candidate.sourceInvoiceItemId === invoiceItemId)
      if (!createdItem) throw new Error('Purchase item not found')
      editorRow = { ...row, procurementId: procurement.id, procurementItemId: createdItem.id }
    }
    const item = procurement.items?.find((candidate: any) => candidate.id === editorRow.procurementItemId)
    if (!item) throw new Error('Purchase item not found')

    // Older unassigned rows were created before RFQ quote choices were copied
    // to the remainder.  Reuse the choices from their matching item so users
    // can assign the outstanding quantity without having to enter suppliers again.
    const needsRfqChoices = item.supplierName === 'No Supplier' && !(item.supplierQuotes?.length)
    const matchingItem = needsRfqChoices
      ? procurement.items?.find((candidate: any) =>
          candidate.id !== item.id &&
          candidate.sourceInvoiceItemId === item.sourceInvoiceItemId &&
          candidate.supplierQuotes?.length)
      : null
    const quoteSource = matchingItem?.supplierQuotes ?? item.supplierQuotes ?? []
    const supplierQuotes = quoteSource.map((quote: any) => matchingItem
      ? {
          ...quote,
          id: null,
          tempId: `rfq-${quote.id}`,
          qty: item.qty,
          isSelected: false,
          hasActivePOItem: false,
        }
      : { ...quote, tempId: quote.id })

    supplierEditor.row = editorRow
    supplierEditor.item = item
    supplierEditor.quotes = supplierQuotes
    supplierEditor.originalSelection = Object.fromEntries(supplierEditor.quotes
      .filter((quote: any) => quote.id)
      .map((quote: any) => [quote.id, !!quote.isSelected]))
    supplierEditor.originalState = supplierQuoteState()
    supplierDialog.value = true
  } catch {
    showSnack('Purchase-item suppliers are not available right now.', 'warning')
  }
}

function addSupplierQuote() {
  if (!canEditSupplier(supplierEditor.row)) return
  supplierEditor.quotes.push({
    tempId: `new-${Date.now()}`,
    supplierName: '',
    qty: supplierEditor.item?.qty ?? supplierEditor.row?.qty ?? 1,
    price: 0,
    condition: supplierEditor.item?.condition ?? supplierEditor.row?.condition ?? 'NE',
    alt: supplierEditor.item?.alt ?? '',
    leadTime: '',
    isSelected: false,
    hasActivePOItem: false,
  })
}

function hasOpenPurchaseOrder() {
  const status = supplierEditor.row?.purchaseOrderStatus
  return !!supplierEditor.row?.purchaseOrderId && !['Completed', 'Cancelled', 'Returned'].includes(status)
}

function supplierQuoteState() {
  return JSON.stringify(supplierEditor.quotes.map((quote: any) => ({
    id: quote.id ?? null,
    supplierName: quote.supplierName?.trim() ?? '',
    qty: Number(quote.qty) || 0,
    price: Number(quote.price) || 0,
    condition: quote.condition ?? '',
    alt: quote.alt ?? '',
    leadTime: quote.leadTime ?? '',
    isSelected: !!quote.isSelected,
  })))
}

async function saveSupplierChanges() {
  if (!canEditSupplier(supplierEditor.row)) {
    showSnack('Purchase items can be edited only while the Proforma Invoice is Waiting For Prepayment or Running.', 'warning')
    return
  }
  if (supplierQuoteState() === supplierEditor.originalState) {
    supplierDialog.value = false
    return
  }
  if (hasOpenPurchaseOrder()) {
    showPoCancellationWarning.value = true
    return
  }
  await persistSupplierChanges()
}

async function confirmPoCancellationAndSave() {
  showPoCancellationWarning.value = false
  savingSupplierChanges.value = true
  try {
    await api.patch(`/purchase-orders/${supplierEditor.row.purchaseOrderId}/status`, { status: 'Cancelled' })
    supplierEditor.quotes.forEach((quote: any) => { quote.hasActivePOItem = false })
    await persistSupplierChanges(true)
  } catch (error: any) {
    showSnack('The existing Purchase Order was kept.', 'warning')
  } finally {
    savingSupplierChanges.value = false
  }
}

async function resetApprovalWhenNoPoExists() {
  if (hasOpenPurchaseOrder() || !supplierEditor.item?.hasActivePOItem) return
  await api.post(`/procurements/${supplierEditor.row.procurementId}/items/${supplierEditor.row.procurementItemId}/reset-approval`, {})
  supplierEditor.item.hasActivePOItem = false
  supplierEditor.quotes.forEach((quote: any) => { quote.hasActivePOItem = false })
}

async function persistSupplierChanges(alreadySaving = false) {
  const procurementId = supplierEditor.row.procurementId
  const itemId = supplierEditor.row.procurementItemId
  if (!alreadySaving) savingSupplierChanges.value = true
  try {
    await resetApprovalWhenNoPoExists()
    for (const quote of supplierEditor.quotes) {
      if (!quote.supplierName?.trim()) continue
      const saved = await api.post<any>(`/procurements/${procurementId}/items/${itemId}/supplier-quotes`, {
        id: quote.id || null,
        supplierId: quote.supplierId || null,
        supplierName: quote.supplierName,
        qty: Number(quote.qty) || 0,
        price: Number(quote.price) || 0,
        condition: quote.condition,
        alt: quote.alt,
        leadTime: quote.leadTime,
      })
      const shouldBeSelected = !!quote.isSelected
      Object.assign(quote, saved)
      quote.isSelected = shouldBeSelected
    }

    for (const quote of supplierEditor.quotes) {
      if (!quote.id) continue
      const wasSelected = supplierEditor.originalSelection[quote.id]
      const isSelected = !!quote.isSelected
      if (isSelected && wasSelected) {
        // Re-select after editing so the procurement item's displayed supplier/price is
        // recalculated from this revised quote without changing the final selection state.
        await api.post(`/procurements/${procurementId}/items/${itemId}/supplier-quotes/${quote.id}/select`, {})
        await api.post(`/procurements/${procurementId}/items/${itemId}/supplier-quotes/${quote.id}/select`, {})
      } else if (wasSelected !== isSelected) {
        await api.post(`/procurements/${procurementId}/items/${itemId}/supplier-quotes/${quote.id}/select`, {})
      }
      supplierEditor.originalSelection[quote.id] = isSelected
    }

    const coveredQty = supplierEditor.quotes
      .filter((quote: any) => quote.isSelected && quote.supplierName?.trim() !== 'No Supplier')
      .reduce((total: number, quote: any) => total + (Number(quote.qty) || 0), 0)
    const itemQty = Number(supplierEditor.item?.qty) || 0
    if (supplierEditor.row?.supplier !== 'No Supplier' && coveredQty > 0 && coveredQty < itemQty) {
      await api.post(`/procurements/${procurementId}/items/${itemId}/create-unassigned-remainder`, { coveredQty })
      showSnack(`${itemQty - coveredQty} item(s) have no supplier yet. They are highlighted in red in Total P/N.`)
    }

    supplierEditor.originalState = supplierQuoteState()
    showSnack('Supplier changes saved. You can continue editing or close this window to refresh Total P/N.')
  } catch (error: any) {
    showSnack('Changes were not saved. Review the supplier rows and try again.', 'warning')
  } finally {
    if (!alreadySaving) savingSupplierChanges.value = false
  }
}

async function approveSupplierQuote(quote: any) {
  if (!canEditSupplier(supplierEditor.row)) return
  if (!quote.id || approvingQuoteId.value) return
  approvingQuoteId.value = quote.id
  try {
    await api.post(`/procurements/${supplierEditor.row.procurementId}/items/${supplierEditor.row.procurementItemId}/supplier-quotes/${quote.id}/approve`, {})
    quote.hasActivePOItem = true
    showSnack('Supplier approved. You can continue editing or close this window to refresh Total P/N.')
  } catch (error: any) {
    // The supplier may have been changed since the dialog opened. Keep the modal open so
    // the user can save the current selection again instead of exposing a backend error.
    showSnack('Save the supplier selection, then approve it.', 'warning')
  } finally {
    approvingQuoteId.value = null
  }
}

function canEditSupplier(row: any) {
  return canOpenSupplierEditor(row) && !!row?.procurementId && !!row?.procurementItemId
}

function canOpenSupplierEditor(row: any) {
  const invoiceStatus = row?.proformaInvoiceStatus ?? row?.paymentTerm
  return !!row?.invoiceId &&
    ['Waiting For Prepayment', 'Running'].includes(invoiceStatus)
}

async function onSupplierDialogChange(isOpen: boolean) {
  if (isOpen) return
  await load()
}

function formatPrice(v: number | null | undefined) {
  if (v == null || isNaN(v as number)) return '0.00'
  return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatDate(v: string | null | undefined) {
  if (!v) return '-'
  const d = new Date(v)
  return isNaN(d.getTime()) ? '-' : d.toLocaleDateString()
}

function qtyValue(value: number | null | undefined) {
  return value == null ? '—' : String(value)
}

function qtyProgressClass(row: any) {
  if (Number(row.remainingQty) === 0) return 'qty-progress-complete'
  if (Number(row.receivedQty) > 0) return 'qty-progress-partial'
  return 'qty-progress-none'
}

function statusColorClass(status: string | null | undefined) {
  if (isInShopCountdown(status) || status === 'In Shop') return 'status-warning'
  switch (status) {
    case 'Ship to Warehouse':
    case 'Completed': return 'status-success'
    case 'Payment Done': return 'status-info'
    case 'Waiting For Shipment':
    case 'Document Added':
    case 'PO Sent':
    case 'Waiting For Payment':
    case 'Under Contract':
    case 'Sourcing': return 'status-warning'
    case 'Cancelled': return 'status-error'
    default: return 'status-grey'
  }
}

function rowStatusClass(status: string | null | undefined) {
  switch ((status || '').trim().toLowerCase()) {
    case 'enduser': return 'tpn-row-end-user'
    case 'payment done': return 'tpn-row-payment-done'
    case 'waiting for expert approval shipment': return 'tpn-row-expert-shipment'
    default: return ''
  }
}

function isInShopCountdown(status: string | null | undefined) {
  return /^in shop \(\d+ days?\)$/i.test(status || '')
}

function shippingStatusColor(status: string) {
  if (!status) return 'default'
  const s = status.toLowerCase()
  if (s.includes('delivered') || s.includes('received in office')) return 'success'
  if (s.includes('clearing customs') || s.includes('ship to usa')) return 'deep-purple'
  if (s.includes('waiting for packing') || s.includes('packing')) return 'blue'
  if (s.includes('received in warehouse')) return 'orange'
  if (s.includes('rejected')) return 'error'
  return 'blue-grey'
}

function paymentTermColor(term: string) {
  const t = term.toLowerCase()
  if (t === 'paid') return 'success'
  if (t === 'accepted' || t === 'sent') return 'info'
  if (t.startsWith('net')) return 'warning'
  if (t === 'rejected' || t === 'cancelled') return 'error'
  return 'grey'
}

async function load() {
  loading.value = true
  try {
    const loadAll = requiresLocalPaging.value
    // Build query object — $fetch serialises arrays as repeated keys: ?partNumbers=X&partNumbers=Y
    const query: Record<string, any> = {
      page: loadAll ? 1 : page.value,
      pageSize: loadAll ? -1 : pageSize.value,
      origin: effectiveOrigin.value,
    }
    if (sortBy.value) { query.sortBy = sortBy.value; query.sortDesc = sortDesc.value }

    // Attach active server-side filters (only when something is selected)
    const f = tpnColFilters
    const sel = (key: string) => f[key] && f[key].size > 0 ? [...f[key]] : undefined
    if (sel('customer'))              query.customers       = sel('customer')
    if (sel('customerInvoiceNumber')) query.invoiceNumbers  = sel('customerInvoiceNumber')
    if (sel('partNumber'))            query.partNumbers     = sel('partNumber')
    if (sel('condition'))             query.conditions      = sel('condition')
    if (sel('poNumber'))              query.poNumbers       = sel('poNumber')
    if (sel('supplier'))              query.suppliers       = sel('supplier')
    if (sel('paymentTerm'))           query.paymentTerms    = sel('paymentTerm')
    if (sel('status'))                query.poStatuses      = sel('status')
    if (sel('shippingStatus'))        query.shippingStatuses = sel('shippingStatus')

    const res = await api.get<any>('/po-items/total-pn', { query })
    rows.value = res.items ?? res.Items ?? []
    serverTotalCount.value = res.totalCount ?? res.TotalCount ?? rows.value.length
    loadedAllRows.value = loadAll
  } catch {
    showSnack('Failed to load Total Project', 'error')
  } finally {
    loading.value = false
  }
}

function onPageSizeChange() {
  if (import.meta.client) localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(pageSize.value))
  page.value = 1
  load()
}

function onPageChange() {
  if (!loadedAllRows.value) load()
}

const locallyFilteredRows = computed(() => {
  let result = rows.value

  // Text search
  const q = (search.value ?? '').trim().toLowerCase()
  if (q) {
    result = result.filter(r => {
      const blob = [
        r.poNumber, r.partNumber, r.description, r.customer, r.supplier,
        r.customerInvoiceNumber, r.status, r.note, r.priority, r.warehouse,
        ...(r.experts ?? []), r.trackNumbers,
        r.serialNumber, r.shippingStatus
      ].filter(Boolean).join(' ').toLowerCase()
      return blob.includes(q)
    })
  }

  // Column filters
  for (const col of TPN_COLUMNS) {
    // These filters have already been applied before backend pagination.
    if (SERVER_SIDE_FILTER_KEYS.has(col.key)) continue
    const selected = tpnColFilters[col.key]
    if (!selected || selected.size === 0) continue
    result = result.filter(r => {
      return tpnRowMatchesFilter(r, col, selected)
    })
  }

  return result
})

const filteredRows = computed(() => {
  if (!loadedAllRows.value) return locallyFilteredRows.value
  const start = (page.value - 1) * pageSize.value
  return locallyFilteredRows.value.slice(start, start + pageSize.value)
})

let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    page.value = 1
    load()
  }, 300)
})

const visibleTotals = computed(() => filteredRows.value.reduce((totals, row) => {
  totals.purchase += Number(row.purchasingTotalPriceUsd) || 0
  totals.sell += Number(row.sellingTotalPriceUsd) || 0
  return totals
}, { purchase: 0, sell: 0 }))

async function updateStatus(row: any, newStatus: string) {
  const old = row.status
  row.status = newStatus
  try {
    await api.patch(`/po-items/total-pn/${row.id}`, { status: newStatus })
    showSnack('Status updated')
  } catch {
    row.status = old
    showSnack('Failed to update status', 'error')
  }
}

async function updateNote(row: any, newNote: string) {
  if ((row.note || '') === newNote) return
  const old = row.note
  row.note = newNote
  try {
    await api.patch(`/po-items/total-pn/${row.id}`, { note: newNote })
    showSnack('Note saved')
  } catch {
    row.note = old
    showSnack('Failed to save note', 'error')
  }
}

function exportCsv() {
  const headers = ['#']
  
  const columnsMap: { key: string; label: string; value: (r: any) => any }[] = [
    { key: 'poNumber', label: 'PO#', value: (r) => r.poNumber },
    { key: 'poRef', label: 'PO Ref#', value: (r) => r.poRef },
    { key: 'experts', label: 'Expert', value: (r) => (r.experts ?? []).join(', ') },
    { key: 'customer', label: 'Customer', value: (r) => r.customer },
    { key: 'supplier', label: 'Supplier', value: (r) => r.supplier },
    { key: 'partNumber', label: 'P/N', value: (r) => r.partNumber },
    { key: 'description', label: 'Description', value: (r) => r.description },
    { key: 'qty', label: 'QTY', value: (r) => r.qty },
    { key: 'inTransitQty', label: 'In Transit', value: (r) => r.inTransitQty },
    { key: 'receivedQty', label: 'Received QTY', value: (r) => r.receivedQty },
    { key: 'inWarehouseQty', label: 'In Warehouse', value: (r) => r.inWarehouseQty },
    { key: 'remainingQty', label: 'Remaining QTY', value: (r) => r.remainingQty },
    { key: 'condition', label: 'CD', value: (r) => r.condition },
    { key: 'priority', label: 'Priority', value: (r) => r.priority },
    { key: 'warehouse', label: 'Warehouse', value: (r) => r.warehouse },
    { key: 'serialNumber', label: 'SN#', value: (r) => r.serialNumber },
    { key: 'customerInvoiceNumber', label: 'PI# to Customer', value: (r) => r.customerInvoiceNumber },
    { key: 'purchasingUnitPriceUsd', label: 'Purchasing Unit Price (USD)', value: (r) => r.purchasingUnitPriceUsd },
    { key: 'purchasingTotalPriceUsd', label: 'Purchasing Total Price (USD)', value: (r) => r.purchasingTotalPriceUsd },
    { key: 'supplierDeliveryTime', label: 'Supplier Delivery Time', value: (r) => r.supplierDeliveryTime },
    { key: 'status', label: 'Status', value: (r) => r.status },
    { key: 'sellingUnitPriceUsd', label: 'Selling Unit Price (USD)', value: (r) => r.sellingUnitPriceUsd },
    { key: 'sellingTotalPriceUsd', label: 'Selling Total Price (USD)', value: (r) => r.sellingTotalPriceUsd },
    { key: 'sellingUnitPriceYuan', label: 'Selling Unit Price (Yuan)', value: (r) => r.sellingUnitPriceYuan },
    { key: 'sellingTotalPriceYuan', label: 'Selling Total Price (Yuan)', value: (r) => r.sellingTotalPriceYuan },
    { key: 'poDate', label: 'PO Date', value: (r) => r.poDate },
    { key: 'invDate', label: 'INV Date', value: (r) => r.invDate },
    { key: 'received', label: 'Received (Payment)', value: (r) => r.received },
    { key: 'receivedDate', label: 'Received Date (Payment)', value: (r) => r.receivedDate },
    { key: 'paymentTerm', label: 'Payment Term', value: (r) => r.paymentTerm },
    { key: 'customerDeliveryTime', label: 'Customer Delivery Time', value: (r) => r.customerDeliveryTime },
    { key: 'rate', label: 'Rate', value: (r) => r.rate },
    { key: 'trackNumbers', label: 'Track#', value: (r) => r.trackNumbers },
    { key: 'shippingStatus', label: 'Shipping Status', value: (r) => r.shippingStatus },
    { key: 'shippingCost', label: 'Shipping Cost', value: (r) => r.shippingCost },
    { key: 'note', label: 'NOTE 02', value: (r) => r.note }
  ]

  const activeCols = columnsMap.filter(c => visibleColumns.value.includes(c.key))
  activeCols.forEach(c => headers.push(c.label))

  const esc = (v: any) => {
    if (v == null) return ''
    const s = String(v).replace(/"/g, '""')
    return /[",\n]/.test(s) ? `"${s}"` : s
  }
  const lines = [headers.map(esc).join(',')]
  filteredRows.value.forEach((r, i) => {
    const rowData = [i + 1]
    activeCols.forEach(c => {
      rowData.push(c.value(r))
    })
    lines.push(rowData.map(esc).join(','))
  })
  const blob = new Blob(["\ufeff" + lines.join('\n')], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `total-pn-${new Date().toISOString().slice(0, 10)}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

function exportExcel() {
  const activeColumns = ALL_COLUMNS.filter(column => visibleColumns.value.includes(column.key))
  const exportRows = filteredRows.value.map((row, index) => {
    const result: Record<string, unknown> = { '#': index + 1 }
    for (const column of activeColumns) {
      const value = column.key === 'experts' ? (row.experts ?? []).join(', ') : row[column.key]
      result[column.label] = value ?? ''
    }
    return result
  })
  downloadExcel(exportRows, `total-pn-${new Date().toISOString().slice(0, 10)}`, 'Total Project')
}

onMounted(async () => {
  await loadColumnAccess()
  await Promise.all([
    load(),
    loadFilterOptions(),
    loadFilterOptions(false),
  ])
})
</script>

<style scoped>
.hover-row:hover {
  background-color: rgba(var(--v-theme-primary), 0.08);
}

.excel-container {
  overflow-x: auto;
  overflow-y: auto;
  border-radius: 8px;
}

.excel-scroll-top {
  overflow-x: auto;
  overflow-y: hidden;
  height: 16px;
  margin-bottom: 4px;
}

.excel-scroll-top > div {
  min-height: 1px;
}

.tpn-table {
  width: max-content;
  min-width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  overflow: visible !important;
  font-size: 12px;
}

.tpn-table thead th {
  position: sticky;
  top: 0;
  background: rgb(var(--v-theme-surface));
  border-bottom: 2px solid rgba(var(--v-theme-primary), 0.3);
  padding: 6px 8px;
  text-align: left;
  font-weight: 700;
  text-transform: uppercase;
  font-size: 10px;
  letter-spacing: 0.4px;
  white-space: normal;
  max-width: 190px;
  color: rgb(var(--v-theme-on-surface));
  z-index: 4;
}

/* A selected Excel-style filter makes its whole header easy to spot. */
.tpn-table thead th:has(.tpn-filter-active) {
  background-color: rgb(var(--v-theme-primary) / 0.14) !important;
  color: rgb(var(--v-theme-primary));
  box-shadow: inset 0 -2px 0 rgb(var(--v-theme-primary));
}

@media (prefers-color-scheme: dark) {
  .tpn-table thead th {
    background: rgba(var(--v-theme-surface), 0.95);
    color: rgb(var(--v-theme-on-surface));
  }
}

.tpn-table tbody td {
  padding: 6px 10px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
  white-space: nowrap;
}

.qty-progress-cell {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  line-height: 1.15;
}

.qty-progress-line {
  margin-top: 2px;
  font-size: 0.68rem;
  font-weight: 600;
}

.qty-progress-complete { color: rgb(var(--v-theme-success)); }
.qty-progress-partial { color: rgb(var(--v-theme-warning)); }
.qty-progress-none { color: rgb(var(--v-theme-on-surface) / 0.48); }

.tpn-table tbody td.cell-wrap {
  width: 180px;
  min-width: 120px;
  max-width: 220px;
  white-space: normal;
  line-height: 1.35;
  overflow-wrap: anywhere;
}

.tpn-table tbody tr:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}

.tpn-table tbody tr.missing-supplier-row {
  background: rgba(244, 67, 54, 0.14);
}

.tpn-table tbody tr.missing-supplier-row:hover {
  background: rgba(244, 67, 54, 0.22);
}

/* Workflow highlights take priority over the generic missing-supplier color. */
.tpn-table tbody tr.tpn-row-end-user {
  background: rgba(255, 152, 0, 0.18);
}

.tpn-table tbody tr.tpn-row-end-user:hover {
  background: rgba(255, 152, 0, 0.28);
}

.tpn-table tbody tr.tpn-row-payment-done {
  background: rgba(76, 175, 80, 0.17);
}

.tpn-table tbody tr.tpn-row-payment-done:hover {
  background: rgba(76, 175, 80, 0.27);
}

.tpn-table tbody tr.tpn-row-expert-shipment {
  background: rgba(255, 213, 0, 0.22);
}

.tpn-table tbody tr.tpn-row-expert-shipment:hover {
  background: rgba(255, 213, 0, 0.32);
}

.tpn-th-inner {
  display: flex;
  align-items: center;
  gap: 4px;
}

.tpn-th-label {
  flex: 1;
  white-space: normal;
}

.tpn-filter-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border: none;
  border-radius: 3px;
  background: transparent;
  cursor: pointer;
  opacity: 0.4;
  transition: opacity 0.15s, background 0.15s;
  color: inherit;
  flex-shrink: 0;
  padding: 0;
}

.tpn-filter-btn:hover {
  opacity: 1;
  background: rgba(var(--v-theme-primary), 0.12);
}

.tpn-filter-btn.tpn-filter-active {
  opacity: 1;
  color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.12);
}

.tpn-filter-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 2px 4px;
  border-radius: 4px;
  cursor: pointer;
}

.tpn-filter-item:hover {
  background: rgba(var(--v-theme-primary), 0.06);
}
/* â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

.cell-pn {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-weight: 700;
}

.cell-price {
  font-weight: 700;
  color: rgb(var(--v-theme-success));
}

.text-right { text-align: right; }
.text-center { text-align: center; }

.status-select,
.note-input {
  border: 1px solid transparent;
  background: transparent;
  padding: 3px 6px;
  border-radius: 4px;
  font-size: 12px;
  width: 100%;
  min-width: 140px;
  color: inherit;
}

.status-select:hover,
.note-input:hover {
  border-color: rgba(var(--v-border-color), 0.25);
}

.status-select:focus,
.note-input:focus {
  outline: none;
  border-color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-surface), 1);
}

.status-success { background: rgba(76, 175, 80, 0.12); color: rgb(var(--v-theme-success)); font-weight: 700; }
.status-info    { background: rgba(33, 150, 243, 0.12); color: rgb(var(--v-theme-info));    font-weight: 700; }
.status-warning { background: rgba(255, 152, 0, 0.12);  color: rgb(var(--v-theme-warning)); font-weight: 700; }
.status-error   { background: rgba(244, 67, 54, 0.12);  color: rgb(var(--v-theme-error));   font-weight: 700; }
.status-grey    { color: rgba(var(--v-theme-on-surface), 0.6); }

.hover-underline:hover { text-decoration: underline !important; }

.missing-po-link {
  color: rgb(var(--v-theme-primary));
  font: inherit;
  font-weight: 700;
  cursor: pointer;
  padding: 0;
  border: 0;
  background: transparent;
  text-decoration: underline;
}

.supplier-link {
  color: rgb(var(--v-theme-primary));
  font: inherit;
  cursor: pointer;
  padding: 0;
  border: 0;
  background: transparent;
  text-decoration: underline;
  white-space: normal;
  text-align: left;
}

.supplier-editor-table-wrap { overflow-x: auto; border: 1px solid rgba(var(--v-border-color), 0.5); border-radius: 6px; }
.supplier-editor-table { width: 100%; min-width: 760px; border-collapse: collapse; }
.supplier-editor-table th, .supplier-editor-table td { padding: 8px; border-bottom: 1px solid rgba(var(--v-border-color), 0.35); text-align: left; }
.supplier-editor-table th { font-size: 11px; text-transform: uppercase; color: rgba(var(--v-theme-on-surface), 0.65); }
.selected-supplier-row { background: rgba(76, 175, 80, 0.10); }
.supplier-editor-input { width: 100%; min-width: 80px; padding: 5px 7px; border: 1px solid rgba(var(--v-border-color), 0.55); border-radius: 4px; background: transparent; color: inherit; }
.number-input { text-align: right; }

.sortable-th {
  cursor: pointer;
  user-select: none;
}

.sortable-th:hover {
  color: rgb(var(--v-theme-primary));
}
</style>
