<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">ILS</h1>
      <v-spacer />
      <template v-if="mainTab === 'inventory'">
        <v-btn
          v-if="isAdmin"
          variant="tonal"
          color="warning"
          prepend-icon="mdi-wrench"
          size="small"
          @click="openARShopDialog"
        >
          Import AR Shops
        </v-btn>
        <v-btn
          variant="tonal"
          color="success"
          prepend-icon="mdi-file-excel"
          size="small"
          @click="openExcelImport"
        >
          Excel Import
        </v-btn>
        <v-btn
          color="primary"
          prepend-icon="mdi-plus"
          size="small"
          @click="openAddDialog"
        >
          Add Item
        </v-btn>
      </template>
      <template v-if="mainTab === 'quotes'">
        <v-btn
          color="primary"
          prepend-icon="mdi-plus"
          size="small"
          @click="openCreateQuoteDialog"
        >
          New ILS Quote
        </v-btn>
      </template>
      <template v-if="mainTab === 'customers'">
        <v-btn
          color="primary"
          prepend-icon="mdi-plus"
          size="small"
          @click="openCustomerDialog()"
        >
          Add Customer
        </v-btn>
      </template>
    </div>

    <v-tabs v-model="mainTab" class="mb-4">
      <v-tab value="inventory">
        <v-icon start size="18">mdi-warehouse</v-icon>Inventory
      </v-tab>
      <v-tab value="quotes">
        <v-icon start size="18">mdi-file-document-outline</v-icon>Quotes
      </v-tab>
      <v-tab value="customers">
        <v-icon start size="18">mdi-account-group</v-icon>ILS Customers
      </v-tab>
    </v-tabs>

    <v-tabs-window v-model="mainTab">
    <v-tabs-window-item value="inventory">
    <v-card class="glass-card">
      <v-card-text>
        <!-- Filters -->
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search..."
            single-line
            hide-details
            class="flex-grow-1"
            style="min-width: 200px;"
          />
          <v-select
            v-model="conditionFilter"
            :items="conditionOptions"
            label="Condition"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
          />
          <v-select
            v-model="certFilter"
            :items="certOptions"
            label="Cert Type"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 140px; max-width: 220px;"
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
        >
          <template #item.price="{ item }">
            <span class="font-weight-medium" style="font-family: monospace; color: #4ade80;">
              ${{ formatPrice(item.price) }}
            </span>
          </template>
          <template #item.tagDate="{ item }">
            {{ item.tagDate ? new Date(item.tagDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.condition="{ item }">
            <v-chip v-if="item.condition" size="small" variant="tonal" :color="conditionColor(item.condition)">
              {{ item.condition }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.certName="{ item }">
            <v-chip v-if="item.certName" size="small" variant="tonal" color="info">{{ item.certName }}</v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.procumentRecordId="{ item }">
            <v-chip v-if="item.procumentRecordId" size="x-small" color="warning" variant="tonal" prepend-icon="mdi-wrench">
              Shop #{{ item.procumentRecordId }}
            </v-chip>
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openEditDialog(item)" />
            <v-btn
              v-if="isAdmin"
              icon="mdi-delete"
              size="x-small"
              variant="text"
              color="error"
              @click="confirmDelete(item)"
            />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
    </v-tabs-window-item>

    <!-- ══════════════════════════════ QUOTES TAB ══════════════════════════════ -->
    <v-tabs-window-item value="quotes">
      <v-card class="glass-card">
        <v-card-text>
          <v-text-field
            v-model="quoteSearch"
            prepend-inner-icon="mdi-magnify"
            label="Search quotes..."
            single-line
            hide-details
            class="mb-4"
            style="max-width: 320px;"
          />
          <v-data-table
            :headers="quoteHeaders"
            :items="filteredQuotes"
            :search="quoteSearch"
            :loading="quotesLoading"
            :items-per-page="25"
            hover
            density="comfortable"
            @click:row="(_: any, { item }: { item: any }) => navigateTo(`/ils/quotes/${item.id}`)"
          >
            <template #item.status="{ item }">
              <v-chip size="small" variant="tonal" :color="ilsQuoteStatusColor(item.status)">
                {{ item.status }}
              </v-chip>
            </template>
            <template #item.totalAmount="{ item }">
              <span class="font-weight-medium" style="font-family: monospace; color: #4ade80;">
                ${{ formatPrice(item.totalAmount) }}
              </span>
            </template>
            <template #item.createdAt="{ item }">
              {{ new Date(item.createdAt).toLocaleDateString() }}
            </template>
            <template #item.actions="{ item }">
              <v-btn icon="mdi-open-in-new" size="x-small" variant="text" color="primary" @click.stop="navigateTo(`/ils/quotes/${item.id}`)" />
              <v-btn
                v-if="isAdmin"
                icon="mdi-delete"
                size="x-small"
                variant="text"
                color="error"
                @click.stop="confirmDeleteQuote(item)"
              />
            </template>
          </v-data-table>
        </v-card-text>
      </v-card>
    </v-tabs-window-item>

    <!-- ══════════════════════════════ CUSTOMERS TAB ══════════════════════════════ -->
    <v-tabs-window-item value="customers">
      <v-card class="glass-card">
        <v-card-text>
          <v-data-table
            :headers="customerHeaders"
            :items="ilsCustomers"
            :loading="customersLoading"
            :items-per-page="25"
            hover
            density="comfortable"
          >
            <template #item.isActive="{ item }">
              <v-chip size="x-small" :color="item.isActive ? 'success' : 'grey'" variant="tonal">
                {{ item.isActive ? 'Active' : 'Inactive' }}
              </v-chip>
            </template>
            <template #item.actions="{ item }">
              <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openCustomerDialog(item)" />
              <v-btn
                v-if="isAdmin"
                icon="mdi-delete"
                size="x-small"
                variant="text"
                color="error"
                @click="confirmDeleteCustomer(item)"
              />
            </template>
          </v-data-table>
        </v-card-text>
      </v-card>
    </v-tabs-window-item>

    </v-tabs-window>

    <!-- Add / Edit Dialog -->
    <v-dialog v-model="showDialog" max-width="720" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon :icon="isEditing ? 'mdi-pencil' : 'mdi-plus'" class="mr-2" size="20" />
          {{ isEditing ? 'Edit ILS Item' : 'Add ILS Item' }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeDialog" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <!-- Part Number -->
            <v-col cols="12" md="6">
              <v-combobox
                v-model="form.partNumber"
                v-model:search="partSearch"
                :items="partSuggestions"
                item-title="name"
                item-value="id"
                label="Part Number *"
                density="compact"
                variant="outlined"
                hide-details
                no-filter
                clearable
                return-object
                :loading="partSearchLoading"
                @update:search="onPartSearch"
                @update:model-value="onPartPicked"
              >
                <template #item="{ item: suggestion, props: itemProps }">
                  <v-list-item v-bind="itemProps">
                    <template #subtitle>
                      <span v-if="suggestion.raw.description">{{ suggestion.raw.description }}</span>
                      <span v-else class="font-italic text-medium-emphasis">No description</span>
                    </template>
                  </v-list-item>
                </template>
                <template #no-data>
                  <v-list-item>
                    <v-list-item-title v-if="partSearch.length < 3" class="text-medium-emphasis text-caption">
                      Type 3+ chars to search...
                    </v-list-item-title>
                    <v-list-item-title v-else class="text-medium-emphasis text-caption">
                      "{{ partSearch }}" — will be created
                    </v-list-item-title>
                  </v-list-item>
                </template>
              </v-combobox>
            </v-col>
            <!-- Description -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.description"
                label="Description"
                variant="outlined"
                hide-details
              />
            </v-col>
            <!-- Alt Part Number -->
            <v-col cols="12" md="6">
              <v-combobox
                v-model="form.altPartNumber"
                :items="altSuggestions"
                label="Alt Part Number"
                variant="outlined"
                hide-details
                clearable
                hint="Type or select from known alternatives"
              />
            </v-col>
            <!-- Condition -->
            <v-col cols="12" md="6">
              <v-select
                v-model="form.condition"
                :items="conditionOptions"
                label="Condition"
                variant="outlined"
                hide-details
                clearable
              />
            </v-col>
            <!-- Price -->
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="form.price"
                label="Price ($)"
                variant="outlined"
                hide-details
                type="number"
                step="0.01"
                min="0"
                prefix="$"
              />
            </v-col>
            <!-- Qty -->
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="form.qty"
                label="Qty"
                variant="outlined"
                hide-details
                type="number"
                min="0"
              />
            </v-col>
            <!-- Cert Type -->
            <v-col cols="12" md="4">
              <v-select
                v-model="form.certName"
                :items="certOptions"
                label="Cert Type"
                variant="outlined"
                hide-details
                clearable
              />
            </v-col>
            <!-- Tag Date -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.tagDate"
                label="Tag Date"
                variant="outlined"
                hide-details
                type="date"
                :max="today"
              />
            </v-col>
            <!-- Lead Time -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.leadTime"
                label="Lead Time"
                variant="outlined"
                hide-details
                placeholder="e.g. 5 days"
              />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <v-spacer />
          <v-btn variant="text" @click="closeDialog">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="saving"
            :disabled="!form.partNumber"
            @click="saveItem"
          >
            {{ isEditing ? 'Update' : 'Add' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- AR Shop Import Dialog -->
    <v-dialog v-model="showARDialog" max-width="900" scrollable>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-wrench" class="mr-2" color="warning" size="20" />
          Import AR Shops to ILS
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showARDialog = false" />
        </v-card-title>
        <v-card-subtitle class="px-4 pb-2">
          Select shop records (AR parts being fixed) to add to the ILS inventory.
        </v-card-subtitle>
        <v-divider />
        <v-card-text class="pa-4" style="max-height: 500px;">
          <div v-if="arShopsLoading" class="text-center pa-6">
            <v-progress-circular indeterminate color="warning" />
          </div>
          <div v-else-if="arShops.length === 0" class="text-center pa-6">
            <v-icon icon="mdi-wrench-outline" size="48" color="grey-darken-1" class="mb-3" />
            <p class="text-body-2 text-medium-emphasis">No AR shop records found.</p>
            <p class="text-caption text-medium-emphasis">Add Shop records to AR-condition supplier quotes in the Procument or RFQ pages.</p>
          </div>
          <template v-else>
            <v-text-field
              v-model="arShopSearch"
              prepend-inner-icon="mdi-magnify"
              label="Search shops..."
              density="compact"
              hide-details
              class="mb-3"
            />
            <div class="d-flex align-center gap-2 mb-3">
              <v-btn size="x-small" variant="tonal" color="primary" @click="selectAllShops">Select All</v-btn>
              <v-btn size="x-small" variant="tonal" @click="selectedARShops.clear(); selectedARShops = new Set(selectedARShops)">Clear</v-btn>
              <span class="text-caption text-medium-emphasis">{{ selectedARShops.size }} selected</span>
            </div>
            <div class="ar-shops-list">
              <div
                v-for="shop in filteredARShops"
                :key="shop.procumentRecordId"
                class="ar-shop-card"
                :class="{ 'ar-shop-card--selected': selectedARShops.has(shop.procumentRecordId) }"
                @click="toggleARShop(shop.procumentRecordId)"
              >
                <div class="d-flex align-center gap-3">
                  <v-checkbox
                    :model-value="selectedARShops.has(shop.procumentRecordId)"
                    hide-details
                    density="compact"
                    color="warning"
                    @click.stop
                    @update:model-value="toggleARShop(shop.procumentRecordId)"
                  />
                  <div class="flex-grow-1 min-width-0">
                    <div class="d-flex align-center gap-2 flex-wrap mb-1">
                      <span class="font-weight-bold text-body-2" style="font-family: monospace;">{{ shop.partNumberName }}</span>
                      <span v-if="shop.altPartNumber" class="text-caption" style="color: #fbbf24;">{{ shop.altPartNumber }}</span>
                      <v-chip size="x-small" color="warning" variant="tonal">AR</v-chip>
                      <span class="text-caption text-medium-emphasis">RFQ #{{ shop.rfqId }} · {{ shop.rfqName }}</span>
                    </div>
                    <div class="d-flex flex-wrap gap-3 text-caption">
                      <span><strong>Shop:</strong> {{ shop.supplierName }}</span>
                      <span class="text-green"><strong>Cost:</strong> ${{ formatPrice(shop.price) }}</span>
                      <span v-if="shop.fixPrice" style="color: #ff9800;"><strong>Fix:</strong> ${{ formatPrice(shop.fixPrice) }}</span>
                      <span><strong>Qty:</strong> {{ shop.qty }}</span>
                      <span v-if="shop.certName"><strong>Cert:</strong> {{ shop.certName }}</span>
                      <span v-if="shop.leadTime"><strong>LT:</strong> {{ shop.leadTime }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <span class="text-caption text-medium-emphasis">{{ selectedARShops.size }} shop(s) will be added to ILS</span>
          <v-spacer />
          <v-btn variant="text" @click="showARDialog = false">Cancel</v-btn>
          <v-btn
            color="warning"
            variant="flat"
            prepend-icon="mdi-import"
            :loading="importingShops"
            :disabled="selectedARShops.size === 0"
            @click="importARShops"
          >
            Import Selected
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirm -->
    <v-dialog v-model="showDeleteConfirm" max-width="400">
      <v-card>
        <v-card-title class="pa-4 pb-2">Delete ILS Item?</v-card-title>
        <v-card-text>
          Remove <strong>{{ deleteTarget?.partNumberName }}</strong> from ILS? This cannot be undone.
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showDeleteConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleteSaving" @click="doDelete">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Excel Import Dialog -->
    <v-dialog v-model="showExcelDialog" max-width="900" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-file-excel" class="mr-2" color="success" size="20" />
          Excel Import - ILS Inventory
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeExcelDialog" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-tabs v-model="excelTab" class="mb-4">
            <v-tab value="file">
              <v-icon start>mdi-upload</v-icon>Upload File
            </v-tab>
            <v-tab value="paste">
              <v-icon start>mdi-content-paste</v-icon>Paste from Excel
            </v-tab>
          </v-tabs>

          <v-alert type="info" variant="tonal" class="mb-4" density="compact">
            <strong>Expected columns (in order):</strong> PartNumber | Description | AltPartNumber | Price | QTY | Condition | TagDate | CertType | LeadTime
          </v-alert>

          <div v-if="excelTab === 'file'">
            <v-file-input
              v-model="excelFile"
              label="Select Excel file (.xlsx, .csv)"
              accept=".xlsx,.xls,.csv"
              variant="outlined"
              prepend-icon="mdi-file-excel"
              @change="handleFileUpload"
            />
          </div>

          <div v-if="excelTab === 'paste'">
            <v-textarea
              v-model="pasteText"
              label="Paste Excel data here (Ctrl+V from Excel)"
              variant="outlined"
              rows="6"
              hint="Copy cells from Excel and paste here"
              persistent-hint
              placeholder="PartNumber&#9;Description&#9;AltPartNumber&#9;Price&#9;QTY&#9;Condition&#9;TagDate&#9;CertType&#9;LeadTime"
              @input="handlePasteInput"
            />
          </div>

          <div v-if="importPreview.length > 0" class="mt-4">
            <div class="d-flex align-center gap-2 mb-2">
              <span class="text-subtitle-2">Preview ({{ importPreview.length }} rows)</span>
              <v-spacer />
              <v-btn size="x-small" variant="text" color="error" @click="importPreview = []">Clear Preview</v-btn>
            </div>
            <div class="preview-table-wrapper">
              <table class="preview-table">
                <thead>
                  <tr>
                    <th>#</th><th>Part Number</th><th>Description</th><th>Alt P/N</th>
                    <th>Price</th><th>QTY</th><th>Condition</th><th>Tag Date</th><th>Cert</th><th>Lead Time</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, i) in importPreview.slice(0, 20)" :key="i">
                    <td class="text-medium-emphasis">{{ i + 1 }}</td>
                    <td class="font-weight-medium" style="font-family: monospace;">{{ row.partNumberName }}</td>
                    <td>{{ row.description || '—' }}</td>
                    <td>{{ row.altPartNumber || '—' }}</td>
                    <td>{{ row.price ? '$' + row.price : '—' }}</td>
                    <td>{{ row.qty }}</td>
                    <td>{{ row.condition || '—' }}</td>
                    <td>{{ row.tagDate || '—' }}</td>
                    <td>{{ row.certName || '—' }}</td>
                    <td>{{ row.leadTime || '—' }}</td>
                  </tr>
                </tbody>
              </table>
              <p v-if="importPreview.length > 20" class="text-caption text-medium-emphasis mt-1">
                + {{ importPreview.length - 20 }} more rows not shown
              </p>
            </div>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <span class="text-caption text-medium-emphasis">{{ importPreview.length }} rows ready to import</span>
          <v-spacer />
          <v-btn variant="text" @click="closeExcelDialog">Cancel</v-btn>
          <v-btn
            color="success"
            variant="flat"
            prepend-icon="mdi-import"
            :loading="importingExcel"
            :disabled="importPreview.length === 0"
            @click="doExcelImport"
          >
            Import {{ importPreview.length }} Rows
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Create ILS Quote Dialog -->
    <v-dialog v-model="showQuoteDialog" max-width="860" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon icon="mdi-file-document-plus" class="mr-2" size="20" color="primary" />
          New ILS Quote
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showQuoteDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense class="mb-2">
            <v-col cols="12" md="6">
              <v-select
                v-model="quoteForm.ilsCustomerId"
                :items="ilsCustomers"
                item-title="name"
                item-value="id"
                label="ILS Customer *"
                variant="outlined"
                density="compact"
                hide-details
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="quoteForm.rfqReference"
                label="RFQ Reference Number"
                variant="outlined"
                density="compact"
                hide-details
                placeholder="e.g. RFQ-2024-001"
              />
            </v-col>
            <v-col cols="12">
              <v-textarea
                v-model="quoteForm.notes"
                label="Notes"
                variant="outlined"
                density="compact"
                hide-details
                rows="2"
              />
            </v-col>
          </v-row>

          <div class="d-flex align-center mb-2">
            <span class="text-subtitle-2">Quote Items</span>
            <v-spacer />
            <v-btn size="x-small" color="primary" variant="tonal" prepend-icon="mdi-plus" @click="addQuoteItemRow">
              Add Item
            </v-btn>
          </div>

          <div class="quote-items-table-wrap">
            <table class="quote-items-table">
              <thead>
                <tr>
                  <th style="min-width: 200px;">ILS Item *</th>
                  <th style="width: 90px;">Condition</th>
                  <th style="width: 70px;">Cert</th>
                  <th style="width: 80px;">Avail. Qty</th>
                  <th style="width: 70px;">Qty *</th>
                  <th style="width: 110px;">Sell Price *</th>
                  <th style="width: 120px;">Total Price</th>
                  <th style="width: 36px;"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, idx) in quoteForm.items" :key="idx">
                  <td>
                    <v-autocomplete
                      v-model="row.ilsItem"
                      :items="availableILSItems"
                      :item-title="(i: any) => i.partNumberName + (i.altPartNumber ? ' / ' + i.altPartNumber : '')"
                      item-value="id"
                      density="compact"
                      variant="plain"
                      hide-details
                      return-object
                      placeholder="Select from inventory..."
                      no-data-text="No ILS items found"
                      @update:model-value="val => onILSItemSelected(val, row)"
                    >
                      <template #item="{ item: suggestion, props: sp }">
                        <v-list-item v-bind="sp" :subtitle="`${suggestion.raw.condition || '—'} · Qty: ${suggestion.raw.qty} · $${formatPrice(suggestion.raw.price)}`" />
                      </template>
                    </v-autocomplete>
                  </td>
                  <td class="text-center">
                    <v-chip v-if="row.ilsItem?.condition" size="x-small" variant="tonal" :color="conditionColor(row.ilsItem.condition)">
                      {{ row.ilsItem.condition }}
                    </v-chip>
                    <span v-else class="text-medium-emphasis text-caption">—</span>
                  </td>
                  <td style="padding-left: 6px; font-size: 12px; color: rgba(var(--v-theme-on-surface), 0.6);">
                    {{ row.ilsItem?.certName || '—' }}
                  </td>
                  <td class="text-center" style="font-size: 12px; color: rgba(var(--v-theme-on-surface), 0.6);">
                    {{ row.ilsItem?.qty ?? '—' }}
                  </td>
                  <td>
                    <v-text-field v-model.number="row.qty" type="number" min="1" density="compact" variant="plain" hide-details @input="recalcRow(row)" />
                  </td>
                  <td>
                    <v-text-field v-model.number="row.sellPrice" type="number" min="0" step="0.01" density="compact" variant="plain" hide-details prefix="$" @input="recalcRow(row)" />
                  </td>
                  <td class="text-right" style="font-family: monospace; padding-right: 8px; color: #4ade80; font-weight: 600;">
                    ${{ formatPrice(row.totalPrice) }}
                  </td>
                  <td>
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="removeQuoteItemRow(idx)" />
                  </td>
                </tr>
                <tr v-if="!quoteForm.items.length">
                  <td colspan="8" class="text-center text-caption text-medium-emphasis py-4">
                    No items yet — click "Add Item"
                  </td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <td colspan="4"></td>
                  <td class="text-right text-caption font-weight-bold" style="padding-right: 4px;">TOTAL:</td>
                  <td class="text-right font-weight-bold" style="font-family: monospace; padding-right: 8px; color: #4ade80;">
                    ${{ formatPrice(quoteTotalAmount) }}
                  </td>
                  <td colspan="2"></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <v-spacer />
          <v-btn variant="text" @click="showQuoteDialog = false">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="quoteSaving"
            :disabled="!quoteForm.ilsCustomerId || !quoteForm.items.some(r => r.ilsItem)"
            @click="saveQuote"
          >
            Create Quote
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Quote Confirm -->
    <v-dialog v-model="showDeleteQuoteConfirm" max-width="400">
      <v-card>
        <v-card-title class="pa-4 pb-2">Delete ILS Quote?</v-card-title>
        <v-card-text>
          Remove quote <strong>{{ deleteQuoteTarget?.quoteNumber }}</strong>? This cannot be undone.
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showDeleteQuoteConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleteQuoteSaving" @click="doDeleteQuote">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ILS Customer Add/Edit Dialog -->
    <v-dialog v-model="showCustomerDialog" max-width="600" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon :icon="editingCustomerId ? 'mdi-pencil' : 'mdi-plus'" class="mr-2" size="20" />
          {{ editingCustomerId ? 'Edit ILS Customer' : 'Add ILS Customer' }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showCustomerDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="12" md="8">
              <v-text-field v-model="customerForm.name" label="Name *" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model="customerForm.customerCode" label="Customer Code" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="customerForm.email" label="Email" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="customerForm.phone" label="Phone" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="customerForm.contactPerson" label="Contact Person" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="customerForm.address" label="Address" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="customerForm.description" label="Description" variant="outlined" density="compact" hide-details rows="2" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <v-spacer />
          <v-btn variant="text" @click="showCustomerDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="customerSaving" :disabled="!customerForm.name" @click="saveCustomer">
            {{ editingCustomerId ? 'Update' : 'Add' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Customer Confirm -->
    <v-dialog v-model="showDeleteCustomerConfirm" max-width="400">
      <v-card>
        <v-card-title class="pa-4 pb-2">Delete ILS Customer?</v-card-title>
        <v-card-text>Remove <strong>{{ deleteCustomerTarget?.name }}</strong>? This cannot be undone.</v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showDeleteCustomerConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleteCustomerSaving" @click="doDeleteCustomer">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
import * as XLSX from 'xlsx'

const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const today = new Date().toISOString().split('T')[0]

interface ILSForm {
  partNumber: { id: number; name: string; description?: string } | string | null
  description: string
  altPartNumber: string
  price: number
  qty: number
  condition: string
  certName: string
  tagDate: string
  leadTime: string
  procumentRecordId: number | null
}

// ── Main Tab ──
const route = useRoute()
const mainTab = ref((route.query.tab as string) || 'inventory')

// ── State ──
const loading = ref(false)
const saving = ref(false)
const deleteSaving = ref(false)
const allItems = ref<any[]>([])
const search = ref('')
const conditionFilter = ref<string[]>([])
const certFilter = ref<string[]>([])

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Options ──
const conditionOptions = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']
const certOptions = ['FAA', 'EASA', 'CAAC', 'Dual', 'MFG COC', 'Vendor COC', 'No Cert']

// ── Filters ──
const hasActiveFilters = computed(() => conditionFilter.value.length > 0 || certFilter.value.length > 0)

function clearFilters() {
  conditionFilter.value = []
  certFilter.value = []
  search.value = ''
}

const filteredItems = computed(() => {
  let result = allItems.value
  if (conditionFilter.value.length) {
    result = result.filter(i => conditionFilter.value.includes(i.condition))
  }
  if (certFilter.value.length) {
    result = result.filter(i => certFilter.value.includes(i.certName))
  }
  return result
})

// ── Table Headers ──
const headers = [
  { title: 'Part Number', key: 'partNumberName', width: '140px' },
  { title: 'Description', key: 'description' },
  { title: 'Alt P/N', key: 'altPartNumber', width: '130px' },
  { title: 'Price', key: 'price', width: '100px' },
  { title: 'Qty', key: 'qty', width: '70px' },
  { title: 'Condition', key: 'condition', width: '100px' },
  { title: 'Cert', key: 'certName', width: '110px' },
  { title: 'Tag Date', key: 'tagDate', width: '110px' },
  { title: 'Lead Time', key: 'leadTime', width: '110px' },
  { title: 'Source', key: 'procumentRecordId', width: '110px', sortable: false },
  { title: '', key: 'actions', width: '80px', sortable: false },
]

// ── Condition color helper ──
function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error',
    RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

// ── Data Loading ──
onMounted(loadItems)

async function loadItems() {
  loading.value = true
  try {
    allItems.value = await api.get<any[]>('/ils')
  } catch {
    showSnack('Failed to load ILS items', 'error')
  } finally {
    loading.value = false
  }
}

// ── Part Number Search ──
const partSearch = ref('')
const partSuggestions = ref<{ id: number; name: string; description?: string }[]>([])
const partSearchLoading = ref(false)
let partSearchDebounce: any = null
const altSuggestions = ref<string[]>([])

function onPartSearch(val: string) {
  clearTimeout(partSearchDebounce)
  if (!val || val.length < 2) { partSuggestions.value = []; return }
  partSearchLoading.value = true
  partSearchDebounce = setTimeout(async () => {
    try {
      partSuggestions.value = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(val)}`)
    } catch {
      partSuggestions.value = []
    } finally {
      partSearchLoading.value = false
    }
  }, 300)
}

async function loadAltSuggestions(partNumberId: number) {
  try {
    const alts = await api.get<any[]>(`/partnumbers/${partNumberId}/alternatives`)
    altSuggestions.value = (alts || []).map((a: any) => a.name)
  } catch {
    altSuggestions.value = []
  }
}

// ── Add / Edit Dialog ──
const showDialog = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)

const defaultForm = () => ({
  partNumber: null as any,
  description: '',
  altPartNumber: '',
  price: 0,
  qty: 1,
  condition: '' as string,
  certName: '' as string,
  tagDate: '',
  leadTime: '',
  procumentRecordId: null as number | null,
})

const form = ref(defaultForm())

function openAddDialog() {
  isEditing.value = false
  editingId.value = null
  form.value = defaultForm()
  partSearch.value = ''
  partSuggestions.value = []
  altSuggestions.value = []
  showDialog.value = true
}

function openEditDialog(item: any) {
  isEditing.value = true
  editingId.value = item.id
  form.value = {
    partNumber: { id: item.partNumberId, name: item.partNumberName, description: item.description || undefined },
    description: item.description || '',
    altPartNumber: item.altPartNumber || '',
    price: Number(item.price),
    qty: Number(item.qty),
    condition: item.condition || '',
    certName: item.certName || '',
    tagDate: item.tagDate ? new Date(item.tagDate).toISOString().split('T')[0] : '',
    leadTime: item.leadTime || '',
    procumentRecordId: item.procumentRecordId || null,
  }
  // Pre-fill the part search with current value
  partSearch.value = item.partNumberName
  partSuggestions.value = [{ id: item.partNumberId, name: item.partNumberName, description: item.description || undefined }]
  loadAltSuggestions(item.partNumberId)
  showDialog.value = true
}

// Watch selected part to load alt suggestions
watch(() => form.value.partNumber, (pn) => {
  if (pn && typeof pn === 'object' && pn.id) {
    loadAltSuggestions(pn.id)
  }
})

function closeDialog() {
  showDialog.value = false
}

function onPartPicked(val: any) {
  if (val && typeof val === 'object' && val.id) {
    // Existing part selected - auto-fill description
    if (val.description && !form.value.description) {
      form.value.description = val.description
    }
    loadAltSuggestions(val.id)
  }
}

async function saveItem() {
  if (!form.value.partNumber) return
  saving.value = true
  try {
    let partId: number | null = null
    let partName: string | null = null

    if (typeof form.value.partNumber === 'object' && form.value.partNumber.id) {
      partId = form.value.partNumber.id
      partName = form.value.partNumber.name || null
    } else if (typeof form.value.partNumber === 'string' && form.value.partNumber.trim()) {
      partName = form.value.partNumber.trim()
    }

    const tagDate = form.value.tagDate
      ? form.value.tagDate  // ISO date string (YYYY-MM-DD) - backend accepts DateOnly
      : null

    const payload = {
      id: isEditing.value ? editingId.value : null,
      partNumberId: partId,
      partNumberName: partName,
      description: form.value.description || null,
      altPartNumber: form.value.altPartNumber || null,
      price: form.value.price,
      qty: form.value.qty,
      condition: form.value.condition || null,
      tagDate,
      certName: form.value.certName || null,
      leadTime: form.value.leadTime || null,
      procumentRecordId: form.value.procumentRecordId || null,
    }

    const saved = await api.post<any>('/ils', payload)

    if (isEditing.value) {
      const idx = allItems.value.findIndex(i => i.id === editingId.value)
      if (idx >= 0) allItems.value[idx] = saved
    } else {
      allItems.value.unshift(saved)
    }

    showSnack(isEditing.value ? 'Item updated' : 'Item added', 'success')
    closeDialog()
  } catch (e: any) {
    console.error('Save error:', e)
    const errorMsg = e?.data?.error || e?.data?.message || 'Failed to save item'
    showSnack(errorMsg, 'error')
  } finally {
    saving.value = false
  }
}

// ── Delete ──
const showDeleteConfirm = ref(false)
const deleteTarget = ref<any>(null)

function confirmDelete(item: any) {
  deleteTarget.value = item
  showDeleteConfirm.value = true
}

async function doDelete() {
  if (!deleteTarget.value) return
  deleteSaving.value = true
  try {
    await api.del(`/ils/${deleteTarget.value.id}`)
    allItems.value = allItems.value.filter(i => i.id !== deleteTarget.value.id)
    showSnack('Item deleted', 'success')
    showDeleteConfirm.value = false
    deleteTarget.value = null
  } catch {
    showSnack('Failed to delete item', 'error')
  } finally {
    deleteSaving.value = false
  }
}

// ── AR Shop Import ──
const showARDialog = ref(false)
const arShopsLoading = ref(false)
const arShops = ref<any[]>([])
const arShopSearch = ref('')
const selectedARShops = ref<Set<number>>(new Set())
const importingShops = ref(false)

const filteredARShops = computed(() => {
  if (!arShopSearch.value.trim()) return arShops.value
  const q = arShopSearch.value.trim().toLowerCase()
  return arShops.value.filter(s =>
    s.partNumberName.toLowerCase().includes(q) ||
    s.supplierName.toLowerCase().includes(q) ||
    (s.altPartNumber || '').toLowerCase().includes(q) ||
    s.rfqName.toLowerCase().includes(q)
  )
})

async function openARShopDialog() {
  showARDialog.value = true
  arShopSearch.value = ''
  selectedARShops.value = new Set()
  arShopsLoading.value = true
  try {
    arShops.value = await api.get<any[]>('/ils/ar-shop-suggestions')
  } catch {
    showSnack('Failed to load AR shop suggestions', 'error')
  } finally {
    arShopsLoading.value = false
  }
}

function toggleARShop(id: number) {
  if (selectedARShops.value.has(id)) {
    selectedARShops.value.delete(id)
  } else {
    selectedARShops.value.add(id)
  }
  selectedARShops.value = new Set(selectedARShops.value)
}

function selectAllShops() {
  selectedARShops.value = new Set(filteredARShops.value.map((s: any) => s.procumentRecordId))
}

// ── Excel Import ──
const showExcelDialog = ref(false)
const excelTab = ref<'file' | 'paste'>('file')
const excelFile = ref<File[]>([])
const pasteText = ref('')
const importPreview = ref<any[]>([])
const importingExcel = ref(false)

function openExcelImport() {
  showExcelDialog.value = true
  excelTab.value = 'file'
  excelFile.value = []
  pasteText.value = ''
  importPreview.value = []
}

function closeExcelDialog() {
  showExcelDialog.value = false
  importPreview.value = []
  pasteText.value = ''
  excelFile.value = []
}

function parseILSRows(rows: any[][]) {
  let dataRows = rows
  if (rows.length > 0 && rows[0] && rows[0][0] && typeof rows[0][0] === 'string' &&
      rows[0][0].toLowerCase().replace(/\s/g, '').includes('part')) {
    dataRows = rows.slice(1)
  }
  return dataRows
    .filter(row => row && row.some(c => c != null && String(c).trim() !== ''))
    .map(row => {
      const priceRaw = String(row[4] ?? '').replace(/[$,\s]/g, '')
      const price = priceRaw ? parseFloat(priceRaw) : 0
      return {
        partNumberName: String(row[0] ?? '').trim(),
        description: String(row[1] ?? '').trim() || null,
        altPartNumber: String(row[2] ?? '').trim() || null,
        price: isNaN(price!) ? 0 : price,
        qty: parseFloat(String(row[5] ?? '0')) || 1,
        condition: String(row[6] ?? '').trim().toUpperCase() || null,
        tagDate: String(row[7] ?? '').trim() || null,
        certName: String(row[8] ?? '').trim() || null,
        leadTime: String(row[9] ?? '').trim() || null,
      }
    })
    .filter(row => row.partNumberName)
}

function handleFileUpload(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = (e) => {
    const data = new Uint8Array(e.target!.result as ArrayBuffer)
    const workbook = XLSX.read(data, { type: 'array' })
    const worksheet = workbook.Sheets[workbook.SheetNames[0]]
    const rows = XLSX.utils.sheet_to_json<any[]>(worksheet, { header: 1 })
    importPreview.value = parseILSRows(rows)
  }
  reader.readAsArrayBuffer(file)
}

function handlePasteInput() {
  if (!pasteText.value.trim()) { importPreview.value = []; return }
  const rows = pasteText.value.trim().split('\n').map(line => line.split('\t'))
  importPreview.value = parseILSRows(rows)
}

async function doExcelImport() {
  if (!importPreview.value.length) return
  importingExcel.value = true
  try {
    const result = await api.post<any>('/ils/bulk-import', { rows: importPreview.value })
    showSnack(`Imported ${result.created} rows${result.skipped > 0 ? `, skipped ${result.skipped}` : ''}`)
    if (result.errors?.length) console.warn('Import errors:', result.errors)
    await loadItems()
    closeExcelDialog()
  } catch {
    showSnack('Import failed', 'error')
  } finally {
    importingExcel.value = false
  }
}

async function importARShops() {
  const toImport = arShops.value.filter((s: any) => selectedARShops.value.has(s.procumentRecordId))
  if (!toImport.length) return

  importingShops.value = true
  try {
    for (const shop of toImport) {
      const payload = {
        id: null,
        partNumberId: shop.partNumberId,
        description: null,
        altPartNumber: shop.altPartNumber || null,
        price: shop.fixPrice ?? shop.price,
        qty: shop.qty,
        condition: 'AR',
        tagDate: shop.tagDate || null,
        certName: shop.certName || null,
        leadTime: shop.leadTime || null,
        procumentRecordId: shop.procumentRecordId,
      }
      const saved = await api.post<any>('/ils', payload)
      allItems.value.unshift(saved)
    }
    showSnack(`${toImport.length} shop(s) imported to ILS`, 'success')
    showARDialog.value = false
  } catch {
    showSnack('Failed to import shops', 'error')
  } finally {
    importingShops.value = false
  }
}

// ════════════════════════════════════════════════════════════
// ILS CUSTOMERS
// ════════════════════════════════════════════════════════════

const ilsCustomers = ref<any[]>([])
const customersLoading = ref(false)
const showCustomerDialog = ref(false)
const customerSaving = ref(false)
const editingCustomerId = ref<number | null>(null)
const showDeleteCustomerConfirm = ref(false)
const deleteCustomerTarget = ref<any>(null)
const deleteCustomerSaving = ref(false)

const customerHeaders = [
  { title: 'Name', key: 'name' },
  { title: 'Code', key: 'customerCode', width: '100px' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone', width: '120px' },
  { title: 'Contact', key: 'contactPerson', width: '150px' },
  { title: 'Status', key: 'isActive', width: '90px' },
  { title: '', key: 'actions', width: '80px', sortable: false },
]

const defaultCustomerForm = () => ({
  name: '',
  customerCode: '',
  email: '',
  phone: '',
  contactPerson: '',
  address: '',
  description: '',
})
const customerForm = ref(defaultCustomerForm())

async function loadILSCustomers() {
  customersLoading.value = true
  try {
    ilsCustomers.value = await api.get<any[]>('/ils-customers')
  } catch {
    showSnack('Failed to load ILS customers', 'error')
  } finally {
    customersLoading.value = false
  }
}

function openCustomerDialog(item?: any) {
  if (item) {
    editingCustomerId.value = item.id
    customerForm.value = {
      name: item.name,
      customerCode: item.customerCode || '',
      email: item.email || '',
      phone: item.phone || '',
      contactPerson: item.contactPerson || '',
      address: item.address || '',
      description: item.description || '',
    }
  } else {
    editingCustomerId.value = null
    customerForm.value = defaultCustomerForm()
  }
  showCustomerDialog.value = true
}

async function saveCustomer() {
  customerSaving.value = true
  try {
    if (editingCustomerId.value) {
      const updated = await api.put<any>(`/ils-customers/${editingCustomerId.value}`, customerForm.value)
      const idx = ilsCustomers.value.findIndex(c => c.id === editingCustomerId.value)
      if (idx >= 0) ilsCustomers.value[idx] = updated
    } else {
      const created = await api.post<any>('/ils-customers', customerForm.value)
      ilsCustomers.value.unshift(created)
    }
    showSnack(editingCustomerId.value ? 'Customer updated' : 'Customer added', 'success')
    showCustomerDialog.value = false
  } catch {
    showSnack('Failed to save customer', 'error')
  } finally {
    customerSaving.value = false
  }
}

function confirmDeleteCustomer(item: any) {
  deleteCustomerTarget.value = item
  showDeleteCustomerConfirm.value = true
}

async function doDeleteCustomer() {
  if (!deleteCustomerTarget.value) return
  deleteCustomerSaving.value = true
  try {
    await api.del(`/ils-customers/${deleteCustomerTarget.value.id}`)
    ilsCustomers.value = ilsCustomers.value.filter(c => c.id !== deleteCustomerTarget.value.id)
    showSnack('Customer deleted')
    showDeleteCustomerConfirm.value = false
  } catch {
    showSnack('Failed to delete customer', 'error')
  } finally {
    deleteCustomerSaving.value = false
  }
}

// ════════════════════════════════════════════════════════════
// ILS QUOTES
// ════════════════════════════════════════════════════════════

const ilsQuotes = ref<any[]>([])
const quotesLoading = ref(false)
const quoteSearch = ref('')
const showQuoteDialog = ref(false)
const quoteSaving = ref(false)
const showDeleteQuoteConfirm = ref(false)
const deleteQuoteTarget = ref<any>(null)
const deleteQuoteSaving = ref(false)

const quoteHeaders = [
  { title: 'Quote #', key: 'quoteNumber', width: '120px' },
  { title: 'Customer', key: 'ilsCustomerName' },
  { title: 'RFQ Ref', key: 'rfqReference', width: '130px' },
  { title: 'Items', key: 'items.length', width: '70px' },
  { title: 'Total', key: 'totalAmount', width: '120px' },
  { title: 'Status', key: 'status', width: '110px' },
  { title: 'Created', key: 'createdAt', width: '110px' },
  { title: '', key: 'actions', width: '80px', sortable: false },
]

const filteredQuotes = computed(() => {
  if (!quoteSearch.value) return ilsQuotes.value
  const q = quoteSearch.value.toLowerCase()
  return ilsQuotes.value.filter(qr =>
    qr.quoteNumber?.toLowerCase().includes(q) ||
    qr.ilsCustomerName?.toLowerCase().includes(q) ||
    qr.rfqReference?.toLowerCase().includes(q)
  )
})

function ilsQuoteStatusColor(status: string) {
  const map: Record<string, string> = {
    Draft: 'grey', Sent: 'info', Accepted: 'success', Rejected: 'error'
  }
  return map[status] || 'grey'
}

async function loadILSQuotes() {
  quotesLoading.value = true
  try {
    ilsQuotes.value = await api.get<any[]>('/ils-quotes')
  } catch {
    showSnack('Failed to load ILS quotes', 'error')
  } finally {
    quotesLoading.value = false
  }
}

// ── ILS Inventory items for quote selection (already loaded in allItems) ──
const availableILSItems = computed(() => allItems.value)

// ── Quote Form ──
interface QuoteItemRow {
  ilsItem: any | null
  qty: number
  sellPrice: number
  totalPrice: number
}

const defaultQuoteForm = () => ({
  ilsCustomerId: null as number | null,
  rfqReference: '',
  notes: '',
  items: [] as QuoteItemRow[],
})
const quoteForm = ref(defaultQuoteForm())

const quoteTotalAmount = computed(() =>
  quoteForm.value.items.reduce((sum, r) => sum + (r.totalPrice || 0), 0)
)

function newQuoteItemRow(): QuoteItemRow {
  return {
    ilsItem: null,
    qty: 1,
    sellPrice: 0,
    totalPrice: 0,
  }
}

function addQuoteItemRow() {
  quoteForm.value.items.push(newQuoteItemRow())
}

function removeQuoteItemRow(idx: number) {
  quoteForm.value.items.splice(idx, 1)
}

function recalcRow(row: QuoteItemRow) {
  row.totalPrice = (Number(row.qty) || 0) * (Number(row.sellPrice) || 0)
}

function onILSItemSelected(item: any, row: QuoteItemRow) {
  row.ilsItem = item || null
  recalcRow(row)
}

function openCreateQuoteDialog() {
  quoteForm.value = defaultQuoteForm()
  showQuoteDialog.value = true
}

async function saveQuote() {
  if (!quoteForm.value.ilsCustomerId || !quoteForm.value.items.length) return
  quoteSaving.value = true
  try {
    const payload = {
      ilsCustomerId: quoteForm.value.ilsCustomerId,
      rfqReference: quoteForm.value.rfqReference || null,
      notes: quoteForm.value.notes || null,
      items: quoteForm.value.items.filter(row => row.ilsItem).map(row => ({
        partNumberId: row.ilsItem.partNumberId,
        partNumberName: row.ilsItem.partNumberName,
        altPartNumber: row.ilsItem.altPartNumber || null,
        condition: row.ilsItem.condition || null,
        certName: row.ilsItem.certName || null,
        qty: Number(row.qty) || 1,
        sellPrice: Number(row.sellPrice) || 0,
        totalPrice: Number(row.totalPrice) || 0,
        leadTime: row.ilsItem.leadTime || null,
        ilsItemId: row.ilsItem.id,
      })),
    }
    const created = await api.post<any>('/ils-quotes', payload)
    ilsQuotes.value.unshift(created)
    showSnack(`Quote ${created.quoteNumber} created`, 'success')
    showQuoteDialog.value = false
    navigateTo(`/ils/quotes/${created.id}`)
  } catch {
    showSnack('Failed to create quote', 'error')
  } finally {
    quoteSaving.value = false
  }
}

function confirmDeleteQuote(item: any) {
  deleteQuoteTarget.value = item
  showDeleteQuoteConfirm.value = true
}

async function doDeleteQuote() {
  if (!deleteQuoteTarget.value) return
  deleteQuoteSaving.value = true
  try {
    await api.del(`/ils-quotes/${deleteQuoteTarget.value.id}`)
    ilsQuotes.value = ilsQuotes.value.filter(q => q.id !== deleteQuoteTarget.value.id)
    showSnack('Quote deleted')
    showDeleteQuoteConfirm.value = false
  } catch {
    showSnack('Failed to delete quote', 'error')
  } finally {
    deleteQuoteSaving.value = false
  }
}

// Load customers and quotes on mount
onMounted(() => {
  loadILSCustomers()
  loadILSQuotes()
})
</script>

<style scoped>
.preview-table-wrapper { overflow-x: auto; max-height: 260px; overflow-y: auto; }
.preview-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.preview-table th { background: rgba(var(--v-theme-surface-variant), 0.5); padding: 6px 10px; text-align: left; font-weight: 600; position: sticky; top: 0; }
.preview-table td { padding: 5px 10px; border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.06); white-space: nowrap; }
.preview-table tr:hover td { background: rgba(var(--v-theme-surface-variant), 0.3); }

.ar-shops-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.ar-shop-card {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  border-radius: 8px;
  padding: 10px 12px;
  cursor: pointer;
  transition: all 0.15s;
}
.ar-shop-card:hover {
  background: rgba(var(--v-theme-surface-variant), 0.4);
  border-color: rgba(255, 152, 0, 0.4);
}
.ar-shop-card--selected {
  background: rgba(255, 152, 0, 0.08) !important;
  border-color: #ff9800 !important;
}

.quote-items-table-wrap {
  overflow-x: auto;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  border-radius: 8px;
}
.quote-items-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 700px;
}
.quote-items-table th {
  background: rgba(var(--v-theme-surface-variant), 0.5);
  padding: 6px 8px;
  text-align: left;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}
.quote-items-table td {
  padding: 2px 4px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.06);
  vertical-align: middle;
}
.quote-items-table tfoot td {
  padding: 6px 4px;
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-bottom: none;
}
.quote-items-table tr:hover td {
  background: rgba(var(--v-theme-surface-variant), 0.3);
}
</style>
