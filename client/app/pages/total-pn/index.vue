<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold">Total Project</h1>
        <div class="text-caption text-medium-emphasis">
          One row per PO line — joined across PO, Procurement, Invoice, Quote, Final Invoice and Customer Payments.
        </div>
      </div>
      <v-spacer />
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
              v-if="visibleColumns.length < ALL_COLUMNS.length"
              color="error"
              :content="String(ALL_COLUMNS.length - visibleColumns.length)"
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

      <v-btn variant="tonal" color="success" prepend-icon="mdi-download" :disabled="!filteredRows.length" @click="exportCsv">Export CSV</v-btn>
    </div>

    <v-card class="glass-card">
      <div v-if="loading && !rows.length" class="d-flex justify-center pa-12">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <div v-else-if="!filteredRows.length" class="text-center pa-12">
        <v-icon icon="mdi-table-off" size="48" color="grey" class="mb-2" />
        <p class="text-body-2 text-medium-emphasis">No rows.</p>
      </div>

      <div v-else class="excel-container" :style="{ maxHeight: tableMaxHeight }">
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

              <!-- Quotation Expert -->
              <th v-if="visibleColumns.includes('quotationExpert')"><div class="tpn-th-inner"><span class="tpn-th-label">Quotation Expert</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['quotationExpert']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['quotationExpert']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('quotationExpert')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('quotationExpert', val) }" @click.stop="tpnToggleFilter('quotationExpert', val)"><v-checkbox-btn :model-value="tpnColFilters['quotationExpert']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('quotationExpert', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('quotationExpert')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('quotationExpert')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['quotationExpert'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['quotationExpert'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('quotationExpert')" /></v-card></v-menu></div></th>

              <!-- Procurement Expert -->
              <th v-if="visibleColumns.includes('procurementExpert')"><div class="tpn-th-inner"><span class="tpn-th-label">Procurement Expert</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['procurementExpert']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="200" max-width="260" class="pa-2"><v-text-field v-model="tpnFilterSearch['procurementExpert']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('procurementExpert')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('procurementExpert', val) }" @click.stop="tpnToggleFilter('procurementExpert', val)"><v-checkbox-btn :model-value="tpnColFilters['procurementExpert']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('procurementExpert', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('procurementExpert')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('procurementExpert')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['procurementExpert'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['procurementExpert'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('procurementExpert')" /></v-card></v-menu></div></th>

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
              <th v-if="visibleColumns.includes('poAmount')" class="text-right">PO Amount</th>

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
              <th v-if="visibleColumns.includes('invAmount')" class="text-right">INV Amount</th>
              <!-- PO Date — filterable -->
              <th v-if="visibleColumns.includes('poDate')"><div class="tpn-th-inner"><span class="tpn-th-label">PO Date</span><v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['poDate']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['poDate']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('poDate')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('poDate', val) }" @click.stop="tpnToggleFilter('poDate', val)"><v-checkbox-btn :model-value="tpnColFilters['poDate']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('poDate', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('poDate')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('poDate')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['poDate'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['poDate'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('poDate')" /></v-card></v-menu></div></th>

              <!-- INV Date — sortable -->
              <th v-if="visibleColumns.includes('invDate')">
                <div class="tpn-th-inner">
                  <span class="tpn-th-label sortable-th" @click="toggleSort('invDate')">INV Date <v-icon :icon="sortIcon('invDate')" size="12" /></span>
                  <v-menu :close-on-content-click="false" location="bottom start"><template #activator="{ props: mp }"><button v-bind="mp" class="tpn-filter-btn" :class="{ 'tpn-filter-active': tpnColFilters['invDate']?.size }"><v-icon icon="mdi-filter-outline" size="11" /></button></template><v-card min-width="180" max-width="240" class="pa-2"><v-text-field v-model="tpnFilterSearch['invDate']" density="compact" hide-details variant="outlined" placeholder="Search..." prepend-inner-icon="mdi-magnify" class="mb-2" /><div style="max-height:200px;overflow-y:auto"><div v-for="val in tpnDisplayVals('invDate')" :key="val" class="tpn-filter-item" :class="{ 'opacity-40': tpnIsUnavail('invDate', val) }" @click.stop="tpnToggleFilter('invDate', val)"><v-checkbox-btn :model-value="tpnColFilters['invDate']?.has(val)" density="compact" color="primary" @click.stop="tpnToggleFilter('invDate', val)" /><span class="text-caption">{{ val }}</span></div></div><v-divider class="my-1" /><v-btn size="x-small" variant="text" color="primary" @click="tpnSelectAll('invDate')">All</v-btn><v-btn size="x-small" variant="text" color="error" @click="tpnClearFilter('invDate')">Clear</v-btn><v-divider class="my-1" /><v-list-item :title="tpnShowAll['invDate'] ? 'Show available only' : 'Show all'" :prepend-icon="tpnShowAll['invDate'] ? 'mdi-filter' : 'mdi-filter-off'" density="compact" class="text-caption text-medium-emphasis" @click.stop="tpnToggleShowAll('invDate')" /></v-card></v-menu>
                </div>
              </th>

              <th v-if="visibleColumns.includes('received')" class="text-right">Received</th>
              <th v-if="visibleColumns.includes('receivedDate')">Received Date</th>

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
            <tr v-for="(r, idx) in filteredRows" :key="r.id">
              <td class="text-center">{{ (page - 1) * pageSize + idx + 1 }}</td>
              <td v-if="visibleColumns.includes('poNumber')" class="font-weight-bold">
                <NuxtLink v-if="r.poNumber && r.purchaseOrderId" :to="`/purchase-orders/${r.purchaseOrderId}`" class="text-primary text-decoration-none hover-underline">
                  {{ r.poNumber }}
                </NuxtLink>
                <span v-else class="text-medium-emphasis">-</span>
              </td>
              <td v-if="visibleColumns.includes('poRef')" class="text-center">{{ r.poRef ?? '-' }}</td>
              <td v-if="visibleColumns.includes('quotationExpert')">{{ r.quotationExpert || '-' }}</td>
              <td v-if="visibleColumns.includes('procurementExpert')">{{ r.procurementExpert || '-' }}</td>
              <td v-if="visibleColumns.includes('customer')">{{ r.customer || '-' }}</td>
              <td v-if="visibleColumns.includes('supplier')">{{ r.supplier || '-' }}</td>
              <td v-if="visibleColumns.includes('partNumber')" class="cell-pn">{{ r.partNumber || '-' }}</td>
              <td v-if="visibleColumns.includes('description')">{{ r.description || '-' }}</td>
              <td v-if="visibleColumns.includes('qty')" class="text-center">{{ r.qty }}</td>
              <td v-if="visibleColumns.includes('condition')">{{ r.condition || '-' }}</td>
              <td v-if="visibleColumns.includes('priority')">{{ r.priority || '-' }}</td>
              <td v-if="visibleColumns.includes('warehouse')">{{ r.warehouse || '-' }}</td>
              <td v-if="visibleColumns.includes('serialNumber')">
                <v-chip v-if="r.serialNumber" size="x-small" color="primary" variant="tonal" class="font-weight-bold">
                  {{ r.serialNumber }}
                </v-chip>
                <span v-else class="text-medium-emphasis">-</span>
              </td>
              <td v-if="visibleColumns.includes('customerInvoiceNumber')">{{ r.customerInvoiceNumber || '-' }}</td>
              <td v-if="visibleColumns.includes('purchasingUnitPriceUsd')" class="text-right">${{ formatPrice(r.purchasingUnitPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('purchasingTotalPriceUsd')" class="text-right cell-price">${{ formatPrice(r.purchasingTotalPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('poAmount')" class="text-right">{{ r.poAmount != null ? `$${formatPrice(r.poAmount)}` : '-' }}</td>
              <td v-if="visibleColumns.includes('dpNumber')">{{ r.dpNumber || '-' }}</td>
              <td v-if="visibleColumns.includes('supplierDeliveryTime')">{{ r.supplierDeliveryTime || '-' }}</td>
              <td v-if="visibleColumns.includes('status')">
                <select
                  :value="r.status || 'Not Started'"
                  class="status-select"
                  :class="statusColorClass(r.status)"
                  @change="updateStatus(r, ($event.target as HTMLSelectElement).value)"
                >
                  <option v-for="s in statusOptions" :key="s" :value="s">{{ s }}</option>
                </select>
              </td>
              <td v-if="visibleColumns.includes('sellingUnitPriceUsd')" class="text-right">${{ formatPrice(r.sellingUnitPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('sellingTotalPriceUsd')" class="text-right cell-price">${{ formatPrice(r.sellingTotalPriceUsd) }}</td>
              <td v-if="visibleColumns.includes('sellingUnitPriceYuan')" class="text-right">¥{{ formatPrice(r.sellingUnitPriceYuan) }}</td>
              <td v-if="visibleColumns.includes('sellingTotalPriceYuan')" class="text-right cell-price">¥{{ formatPrice(r.sellingTotalPriceYuan) }}</td>
              <td v-if="visibleColumns.includes('invAmount')" class="text-right">{{ r.invAmount != null ? `$${formatPrice(r.invAmount)}` : '-' }}</td>
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
              <td v-if="visibleColumns.includes('trackNumbers')" class="text-caption">{{ r.trackNumbers || '-' }}</td>
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

      <!-- Pagination footer -->
      <div class="d-flex flex-wrap align-center gap-3 pa-3 border-t">
        <span class="text-caption text-medium-emphasis">
          {{ totalCount }} rows
          <template v-if="totalCount > 0">
            • showing {{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, totalCount) }}
          </template>
          <template v-if="tpnActiveFilterCount > 0">
            • <strong>{{ filteredRows.length }}</strong> after filters
          </template>
        </span>
        <v-spacer />
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
        </div>
        <v-pagination
          v-if="totalPages > 1"
          v-model="page"
          :length="totalPages"
          :total-visible="7"
          density="compact"
          @update:model-value="load"
        />
      </div>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()

interface ColumnConfig {
  key: string
  label: string
}

const ALL_COLUMNS: ColumnConfig[] = [
  { key: 'poNumber', label: 'PO#' },
  { key: 'poRef', label: 'PO Ref#' },
  { key: 'quotationExpert', label: 'Quotation Expert' },
  { key: 'procurementExpert', label: 'Procurement Expert' },
  { key: 'customer', label: 'Customer' },
  { key: 'supplier', label: 'Supplier' },
  { key: 'partNumber', label: 'P/N' },
  { key: 'description', label: 'Description' },
  { key: 'qty', label: 'QTY' },
  { key: 'condition', label: 'CD' },
  { key: 'priority', label: 'Priority' },
  { key: 'warehouse', label: 'Warehouse' },
  { key: 'serialNumber', label: 'SN#' },
  { key: 'customerInvoiceNumber', label: 'PI# to Customer' },
  { key: 'purchasingUnitPriceUsd', label: 'Purchasing Unit Price (USD)' },
  { key: 'purchasingTotalPriceUsd', label: 'Purchasing Total Price (USD)' },
  { key: 'poAmount', label: 'PO Amount' },
  { key: 'dpNumber', label: 'DP#' },
  { key: 'supplierDeliveryTime', label: 'Supplier Delivery Time' },
  { key: 'status', label: 'Status' },
  { key: 'sellingUnitPriceUsd', label: 'Selling Unit Price (USD)' },
  { key: 'sellingTotalPriceUsd', label: 'Selling Total Price (USD)' },
  { key: 'sellingUnitPriceYuan', label: 'Selling Unit Price (Yuan)' },
  { key: 'sellingTotalPriceYuan', label: 'Selling Total Price (Yuan)' },
  { key: 'invAmount', label: 'INV Amount' },
  { key: 'poDate', label: 'PO Date' },
  { key: 'invDate', label: 'INV Date' },
  { key: 'received', label: 'Received' },
  { key: 'receivedDate', label: 'Received Date' },
  { key: 'paymentTerm', label: 'Payment Term' },
  { key: 'customerDeliveryTime', label: 'Customer Delivery Time' },
  { key: 'rate', label: 'Rate' },
  { key: 'trackNumbers', label: 'Track#' },
  { key: 'shippingStatus', label: 'Shipping Status' },
  { key: 'shippingCost', label: 'Shipping Cost' },
  { key: 'note', label: 'NOTE 02' }
]

const visibleColumns = ref<string[]>(ALL_COLUMNS.map(c => c.key))
const COL_VISIBILITY_STORAGE_KEY = 'total-pn-column-visibility'

if (import.meta.client) {
  try {
    const raw = localStorage.getItem(COL_VISIBILITY_STORAGE_KEY)
    if (raw) {
      const parsed = JSON.parse(raw)
      if (Array.isArray(parsed)) {
        const validKeys = ALL_COLUMNS.map(c => c.key)
        visibleColumns.value = parsed.filter(k => validKeys.includes(k))
      }
    }
  } catch {}
}

function saveColumnVisibility() {
  if (!import.meta.client) return
  try {
    localStorage.setItem(COL_VISIBILITY_STORAGE_KEY, JSON.stringify(visibleColumns.value))
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
  visibleColumns.value = ALL_COLUMNS.map(c => c.key)
  saveColumnVisibility()
}

const columnSearchQuery = ref('')

const filteredColumnList = computed(() => {
  const q = columnSearchQuery.value.trim().toLowerCase()
  if (!q) return ALL_COLUMNS
  return ALL_COLUMNS.filter(c => c.label.toLowerCase().includes(q))
})

const loading = ref(true)
const rows = ref<any[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(50)
const pageSizeOptions = [25, 50, 100, 200]
const totalPages = computed(() => pageSize.value > 0 ? Math.ceil(totalCount.value / pageSize.value) : 1)
const tableMaxHeight = computed(() => `calc(100vh - 260px)`)
const search = ref('')
const sortBy = ref('')
const sortDesc = ref(false)

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

const TPN_COLUMNS = [
  { key: 'poNumber',              field: (r: any) => r.poNumber },
  { key: 'poRef',                 field: (r: any) => r.poRef },
  { key: 'quotationExpert',       field: (r: any) => r.quotationExpert },
  { key: 'procurementExpert',     field: (r: any) => r.procurementExpert },
  { key: 'customer',              field: (r: any) => r.customer },
  { key: 'supplier',              field: (r: any) => r.supplier },
  { key: 'partNumber',            field: (r: any) => r.partNumber },
  { key: 'description',           field: (r: any) => r.description },
  { key: 'qty',                   field: (r: any) => r.qty != null ? String(r.qty) : null },
  { key: 'condition',             field: (r: any) => r.condition },
  { key: 'priority',              field: (r: any) => r.priority },
  { key: 'warehouse',             field: (r: any) => r.warehouse },
  { key: 'serialNumber',          field: (r: any) => r.serialNumber },
  { key: 'customerInvoiceNumber', field: (r: any) => r.customerInvoiceNumber },
  { key: 'dpNumber',              field: (r: any) => r.dpNumber },
  { key: 'status',                field: (r: any) => r.status },
  { key: 'poDate',                field: (r: any) => r.poDate ? new Date(r.poDate).toLocaleDateString() : null },
  { key: 'invDate',               field: (r: any) => r.invDate ? new Date(r.invDate).toLocaleDateString() : null },
  { key: 'paymentTerm',           field: (r: any) => r.paymentTerm },
  { key: 'trackNumbers',          field: (r: any) => r.trackNumbers },
  { key: 'shippingStatus',        field: (r: any) => r.shippingStatus },
]

const tpnColFilters = reactive<Record<string, Set<string>>>({})
const tpnFilterSearch = reactive<Record<string, string>>({})

/**
 * Filter options fetched from the backend — used for server-side filter columns.
 * Key = column key, value = sorted list of unique strings across ALL rows.
 */
const filterOptions = ref<Record<string, string[]>>({})
const filterOptionsLoading = ref(false)

async function loadFilterOptions() {
  filterOptionsLoading.value = true
  try {
    const opts = await api.get<any>('/po-items/total-pn/filter-options')
    filterOptions.value = {
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

/** All unique values for a column — from backend options (server-side) or current page rows (client-side). */
function tpnUniqueVals(key: string): string[] {
  const s = (tpnFilterSearch[key] || '').toLowerCase()

  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    const opts = filterOptions.value[key] ?? []
    const results = s ? opts.filter(v => v.toLowerCase().includes(s)) : opts
    return results.length ? results : ['(Blank)']
  }

  const col = TPN_COLUMNS.find(c => c.key === key)
  if (!col) return []
  const vals = new Set<string>()
  for (const r of rows.value) {
    const raw = col.field(r)
    const v = raw != null && String(raw).trim() !== '' ? String(raw) : '(Blank)'
    if (!s || v.toLowerCase().includes(s)) vals.add(v)
  }
  return [...vals].sort((a, b) => a === '(Blank)' ? 1 : b === '(Blank)' ? -1 : a.localeCompare(b))
}

/** "Available" values — subset that pass all OTHER active filters (for client-side columns). */
function tpnUniqueValsAvail(key: string): string[] {
  // For server-side columns the backend handles cross-filter availability — just return all options
  if (SERVER_SIDE_FILTER_KEYS.has(key)) return tpnUniqueVals(key)

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
      const rawV = c.field(r)
      const cv = rawV != null && String(rawV).trim() !== '' ? String(rawV) : '(Blank)'
      if (!sel.has(cv)) { ok = false; break }
    }
    if (!ok) continue
    const raw = col.field(r)
    const v = raw != null && String(raw).trim() !== '' ? String(raw) : '(Blank)'
    if (!s || v.toLowerCase().includes(s)) vals.add(v)
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
  if (SERVER_SIDE_FILTER_KEYS.has(key)) return false
  return !!(tpnShowAll[key] && !tpnUniqueValsAvail(key).includes(val))
}

function tpnToggleFilter(key: string, val: string) {
  if (!tpnColFilters[key]) tpnColFilters[key] = new Set()
  if (tpnColFilters[key].has(val)) tpnColFilters[key].delete(val)
  else tpnColFilters[key].add(val)
  tpnColFilters[key] = new Set(tpnColFilters[key]) // trigger reactivity
  saveTpnFilters()
  // Server-side filters → go back to page 1 and reload
  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    page.value = 1
    load()
  }
}

function tpnClearFilter(key: string) {
  if (tpnColFilters[key]) tpnColFilters[key] = new Set()
  saveTpnFilters()
  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    page.value = 1
    load()
  }
}

function tpnSelectAll(key: string) {
  const vals = tpnUniqueVals(key)
  tpnColFilters[key] = new Set(vals)
  saveTpnFilters()
  // selecting all = no filter → reload
  if (SERVER_SIDE_FILTER_KEYS.has(key)) {
    page.value = 1
    load()
  }
}

function tpnClearAllFilters() {
  for (const key of Object.keys(tpnColFilters)) {
    tpnColFilters[key] = new Set()
  }
  saveTpnFilters()
  page.value = 1
  load()
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

const statusOptions = [
  'Not Started',
  'Under Contract',
  'Waiting For Payment',
  'PO Sent',
  'Document Added',
  'Payment Done',
  'Waiting For Shipment',
  'Ship to Warehouse/Customer',
  'Cancelled',
]

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
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

function statusColorClass(status: string | null | undefined) {
  switch (status) {
    case 'Ship to Warehouse/Customer': return 'status-success'
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
    // Build query object — $fetch serialises arrays as repeated keys: ?partNumbers=X&partNumbers=Y
    const query: Record<string, any> = {
      page: page.value,
      pageSize: pageSize.value,
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
    totalCount.value = res.totalCount ?? res.TotalCount ?? rows.value.length
  } catch {
    showSnack('Failed to load Total Project', 'error')
  } finally {
    loading.value = false
  }
}

function onPageSizeChange() {
  page.value = 1
  load()
}

const filteredRows = computed(() => {
  let result = rows.value

  // Text search
  const q = (search.value ?? '').trim().toLowerCase()
  if (q) {
    result = result.filter(r => {
      const blob = [
        r.poNumber, r.partNumber, r.description, r.customer, r.supplier,
        r.customerInvoiceNumber, r.status, r.note, r.priority, r.warehouse,
        r.quotationExpert, r.procurementExpert, r.trackNumbers,
        r.serialNumber, r.shippingStatus
      ].filter(Boolean).join(' ').toLowerCase()
      return blob.includes(q)
    })
  }

  // Column filters
  for (const col of TPN_COLUMNS) {
    const selected = tpnColFilters[col.key]
    if (!selected || selected.size === 0) continue
    result = result.filter(r => {
      const raw = col.field(r)
      const cellVal = raw != null && String(raw).trim() !== '' ? String(raw) : '(Blank)'
      return selected.has(cellVal)
    })
  }

  return result
})

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
    { key: 'quotationExpert', label: 'Quotation Expert', value: (r) => r.quotationExpert },
    { key: 'procurementExpert', label: 'Procurement Expert', value: (r) => r.procurementExpert },
    { key: 'customer', label: 'Customer', value: (r) => r.customer },
    { key: 'supplier', label: 'Supplier', value: (r) => r.supplier },
    { key: 'partNumber', label: 'P/N', value: (r) => r.partNumber },
    { key: 'description', label: 'Description', value: (r) => r.description },
    { key: 'qty', label: 'QTY', value: (r) => r.qty },
    { key: 'condition', label: 'CD', value: (r) => r.condition },
    { key: 'priority', label: 'Priority', value: (r) => r.priority },
    { key: 'warehouse', label: 'Warehouse', value: (r) => r.warehouse },
    { key: 'serialNumber', label: 'SN#', value: (r) => r.serialNumber },
    { key: 'customerInvoiceNumber', label: 'PI# to Customer', value: (r) => r.customerInvoiceNumber },
    { key: 'purchasingUnitPriceUsd', label: 'Purchasing Unit Price (USD)', value: (r) => r.purchasingUnitPriceUsd },
    { key: 'purchasingTotalPriceUsd', label: 'Purchasing Total Price (USD)', value: (r) => r.purchasingTotalPriceUsd },
    { key: 'poAmount', label: 'PO Amount', value: (r) => r.poAmount },
    { key: 'dpNumber', label: 'DP#', value: (r) => r.dpNumber },
    { key: 'supplierDeliveryTime', label: 'Supplier Delivery Time', value: (r) => r.supplierDeliveryTime },
    { key: 'status', label: 'Status', value: (r) => r.status },
    { key: 'sellingUnitPriceUsd', label: 'Selling Unit Price (USD)', value: (r) => r.sellingUnitPriceUsd },
    { key: 'sellingTotalPriceUsd', label: 'Selling Total Price (USD)', value: (r) => r.sellingTotalPriceUsd },
    { key: 'sellingUnitPriceYuan', label: 'Selling Unit Price (Yuan)', value: (r) => r.sellingUnitPriceYuan },
    { key: 'sellingTotalPriceYuan', label: 'Selling Total Price (Yuan)', value: (r) => r.sellingTotalPriceYuan },
    { key: 'invAmount', label: 'INV Amount', value: (r) => r.invAmount },
    { key: 'poDate', label: 'PO Date', value: (r) => r.poDate },
    { key: 'invDate', label: 'INV Date', value: (r) => r.invDate },
    { key: 'received', label: 'Received', value: (r) => r.received },
    { key: 'receivedDate', label: 'Received Date', value: (r) => r.receivedDate },
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

onMounted(() => {
  load()
  loadFilterOptions()
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

.tpn-table {
  width: max-content;
  min-width: 100%;
  border-collapse: collapse;
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
  white-space: nowrap;
  color: rgb(var(--v-theme-on-surface));
  z-index: 1;
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

.tpn-table tbody tr:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}

.tpn-th-inner {
  display: flex;
  align-items: center;
  gap: 4px;
}

.tpn-th-label {
  flex: 1;
  white-space: nowrap;
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

.sortable-th {
  cursor: pointer;
  user-select: none;
}

.sortable-th:hover {
  color: rgb(var(--v-theme-primary));
}
</style>


