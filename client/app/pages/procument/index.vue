<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">Procument</h1>
      <v-spacer />
    </div>

    <!-- Rejected Supplier Banner (user-facing) -->
    <v-alert
      v-if="!isAdmin && rejectedSupplierQuotes.length > 0"
      type="error"
      variant="tonal"
      density="compact"
      class="mb-4"
      prepend-icon="mdi-account-alert-outline"
    >
      <strong>{{ rejectedSupplierQuotes.length }} supplier(s) were rejected.</strong>
      Please correct and resubmit — click the red indicator next to the supplier name.
    </v-alert>

    <!-- Pending Supplier Banner (user-facing) -->
    <v-alert
      v-if="!isAdmin && pendingSupplierQuotes.length > 0"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
      prepend-icon="mdi-clock-outline"
    >
      <strong>{{ pendingSupplierQuotes.length }} supplier(s) pending admin approval.</strong>
      They will appear in quotes once approved.
    </v-alert>

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
           <v-text-field
            v-model="pnSearch"
            label="Search by P/N"
            prepend-inner-icon="mdi-cog-outline"
            hide-details
            clearable
            density="compact"
            variant="outlined"
            class="mx-2"
            style="min-width: 160px; max-width: 260px;"
          />
          <v-autocomplete
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
            density="compact"
            variant="outlined"
            class="mx-2"
            style="min-width: 140px; max-width: 240px;"
          />
          <v-autocomplete
            v-model="customerFilter"
            :items="customerOptions"
            item-title="title"
            item-value="value"
            label="Customer"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 260px;"
          />
          <v-btn
            :variant="showPendingOnly ? 'flat' : 'tonal'"
            :color="showPendingOnly ? 'warning' : 'grey'"
            size="small"
            prepend-icon="mdi-clock-outline"
            class="align-self-center"
            @click="showPendingOnly = !showPendingOnly"
          >
            Pending Suppliers
          </v-btn>
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
            <v-tooltip v-if="item.rejectionNote || (['No Quote', 'Waiting For Admin'].includes(item.rfqStatus) && item.noQuoteReason)" location="bottom">
              <template #activator="{ props: tp }">
                <v-chip
                  v-bind="tp"
                  size="small"
                  :color="rfqStatusColor(item.rfqStatus || 'Open')"
                  variant="tonal"
                >
                  {{ item.rfqStatus }}
                  <v-icon icon="mdi-information-outline" size="14" class="ml-1" />
                </v-chip>
              </template>
              <span>{{ item.noQuoteReason }}{{ item.rejectionNote ? ` (Admin: ${item.rejectionNote})` : '' }}</span>
            </v-tooltip>
            <v-chip
              v-else
              size="small"
              :color="rfqStatusColor(item.rfqStatus || 'Open')"
              variant="tonal"
            >
              {{ item.rfqStatus || 'Open' }}
            </v-chip>
          </template>
          <template #item.supplierCount="{ item }">
            <div class="d-flex flex-wrap gap-1 align-center">
              <v-chip size="small" :color="item.supplierQuotes.length > 0 ? 'success' : 'grey'" variant="tonal">
                {{ item.supplierQuotes.length }} supplier{{ item.supplierQuotes.length !== 1 ? 's' : '' }}
              </v-chip>
              <v-chip
                v-if="item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Pending').length > 0"
                size="x-small"
                color="warning"
                variant="tonal"
                prepend-icon="mdi-clock-outline"
              >
                {{ item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Pending').length }} pending
              </v-chip>
              <v-chip
                v-if="item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Rejected').length > 0"
                size="x-small"
                color="error"
                variant="tonal"
                prepend-icon="mdi-alert-circle-outline"
              >
                {{ item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Rejected').length }} rejected
              </v-chip>
            </div>
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
            <span :class="{ 'text-error font-weight-bold': ['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeExpired(item.leadTime) }" :style="isLeadTimeUrgent(item.leadTime) ? 'font-weight: 600;' : ''">
              {{ new Date(item.leadTime).toLocaleDateString() }}
              <v-icon v-if="['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeUrgent(item.leadTime)" icon="mdi-alert" size="14" color="warning" class="ml-1" title="Lead time expires within 3 days" />
              <v-icon v-else-if="['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeExpired(item.leadTime)" icon="mdi-alert-circle" size="14" color="error" class="ml-1" title="Lead time has expired" />
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

                  <!-- Supplier Suggestions -->
                  <div
                    v-if="getSuggestions(item.rfqItemId).recentQuotes.length > 0 || getSuggestions(item.rfqItemId).knownSuppliers.length > 0 || partAvailability[item.partNumberId]"
                    class="suggestions-bar mb-3"
                  >
                    <!-- Recent quotes (auto-fill capable) -->
                    <div v-if="getSuggestions(item.rfqItemId).recentQuotes.length > 0" class="d-flex flex-wrap align-center gap-2 mb-2">
                      <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                        <v-icon icon="mdi-lightbulb-on-outline" size="14" color="amber" class="mr-1" />
                        Recent suppliers:
                      </span>
                      <v-chip
                        v-for="s in getSuggestions(item.rfqItemId).recentQuotes"
                        :key="'recent-' + s.supplierId"
                        size="small"
                        color="amber"
                        variant="tonal"
                        prepend-icon="mdi-flash"
                        class="cursor-pointer"
                        @click.stop="applySuggestion(item, s)"
                      >
                        {{ s.supplierName }}
                        <span class="text-caption ml-1 text-medium-emphasis">${{ formatPrice(s.price) }}</span>
                      </v-chip>
                      <v-btn
                        v-if="getSuggestions(item.rfqItemId).recentQuotes.length > 1"
                        size="x-small"
                        variant="outlined"
                        color="amber"
                        prepend-icon="mdi-plus-box-multiple"
                        @click.stop="applyAllSuggestions(item)"
                      >
                        Add All
                      </v-btn>
                    </div>
                    <!-- Known suppliers (name only) -->
                    <div
                      v-if="getSuggestions(item.rfqItemId).knownSuppliers.filter((k: any) => !getSuggestions(item.rfqItemId).recentQuotes.some((r: any) => r.supplierId === k.supplierId)).length > 0"
                      class="d-flex flex-wrap align-center gap-2 mb-2"
                    >
                      <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                        <v-icon icon="mdi-account-group-outline" size="14" color="grey" class="mr-1" />
                        Known suppliers:
                      </span>
                      <v-chip
                        v-for="k in getSuggestions(item.rfqItemId).knownSuppliers.filter((k: any) => !getSuggestions(item.rfqItemId).recentQuotes.some((r: any) => r.supplierId === k.supplierId))"
                        :key="'known-' + k.supplierId"
                        size="x-small"
                        color="grey"
                        variant="tonal"
                        prepend-icon="mdi-account"
                      >
                        {{ k.supplierName }}
                      </v-chip>
                    </div>
                    <!-- Availability chips (Inventory=green, CapList=blue, ILS=orange, FastImport=yellow, KnownSup=gray) -->
                    <template v-if="partAvailability[item.partNumberId]">
                      <div class="d-flex flex-wrap align-center gap-2 mt-1">
                        <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                          <v-icon icon="mdi-database-search-outline" size="14" color="primary" class="mr-1" />
                          In stock:
                        </span>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].inventoryRecords"
                          :key="'inv-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--inventory cursor-pointer"
                          prepend-icon="mdi-archive-outline"
                          :title="`Inventory · ${rec.label}${rec.price ? ' · $' + rec.price : ''}${rec.condition ? ' · ' + rec.condition : ''}`"
                          @click.stop="applyAvailability(item, rec)"
                        >{{ rec.label }}<span v-if="rec.price" class="text-caption ml-1" style="opacity:0.75">${{ formatPrice(rec.price) }}</span></v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].capListRecords"
                          :key="'cap-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--caplist cursor-pointer"
                          prepend-icon="mdi-format-list-checks"
                          :title="`Cap List · ${rec.label}`"
                          @click.stop="applyAvailability(item, rec)"
                        >{{ rec.label }}</v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].ilsRecords"
                          :key="'ils-' + rec.condition"
                          size="small"
                          class="avail-chip avail-chip--ils cursor-pointer"
                          prepend-icon="mdi-warehouse"
                          :title="`ILS${rec.price ? ' · $' + rec.price : ''}${rec.condition ? ' · ' + rec.condition : ''}${rec.certName ? ' · ' + rec.certName : ''}`"
                          @click.stop="applyAvailability(item, rec)"
                        >ILS<span v-if="rec.price" class="text-caption ml-1" style="opacity:0.75">${{ formatPrice(rec.price) }}</span></v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].fastImportRecords"
                          :key="'fast-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--fast cursor-pointer"
                          prepend-icon="mdi-flash"
                          :title="`Past record · ${rec.label}${rec.price ? ' · $' + rec.price : ''}${rec.condition ? ' · ' + rec.condition : ''}`"
                          @click.stop="applyAvailability(item, rec)"
                        >{{ rec.label }}<span v-if="rec.price" class="text-caption ml-1" style="opacity:0.75">${{ formatPrice(rec.price) }}</span></v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].knownSupplierRecords"
                          :key="'sup-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--supplier cursor-pointer"
                          prepend-icon="mdi-account-outline"
                          :title="`Known supplier · ${rec.label}`"
                          @click.stop="applyAvailability(item, rec)"
                        >{{ rec.label }}</v-chip>
                      </div>
                    </template>
                  </div>
                  <div v-else-if="getSuggestions(item.rfqItemId).loading" class="mb-3">
                    <v-progress-linear indeterminate height="2" color="primary" />
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
                          <th style="width: 40px" class="text-center">Cert</th>
                          <th style="width: 90px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <template v-for="(quote, qIdx) in getEditableQuotes(item.rfqItemId)" :key="qIdx">
                        <tr class="quote-row">
                          <td>
                            <input
                              type="text"
                              class="quote-input"
                              placeholder="Supplier name..."
                              v-model="quote.supplierName"
                              @input="searchSupplier(quote.supplierName)"
                              list="supplier-suggestions"
                            />
                            <div
                              v-if="quote.supplierStatus === 'Pending'"
                              style="font-size:10px; color:#f59e0b; padding: 1px 4px; line-height:1.4; pointer-events:none;"
                            >
                              ⏳ Pending approval
                            </div>
                            <div
                              v-else-if="quote.supplierStatus === 'Rejected'"
                              style="font-size:10px; color:#ef4444; padding: 1px 4px; line-height:1.4; cursor:pointer; text-decoration:underline;"
                              @click.stop="openResubmit(quote)"
                            >
                              ❌ Rejected — click to correct &amp; resubmit
                            </div>
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
                          <td class="text-center">
                            <v-checkbox
                              v-if="quote.supplierDependency === 'Certificated'"
                              v-model="quote.isCertificated"
                              density="compact"
                              hide-details
                              class="ma-0 pa-0 d-inline-block"
                              @change="saveQuote(item, quote)"
                            />
                          </td>
                          <td class="text-center" style="white-space: nowrap;">
                            <v-btn
                              v-if="quote.condition === 'AR'"
                              icon="mdi-wrench"
                              size="x-small"
                              variant="text"
                              :color="isShopExpanded(quote.id, item.rfqItemId) ? 'warning' : 'grey'"
                              @click.stop="toggleShop(quote.id, item.rfqItemId)"
                              :title="'Shops (' + (quote.shopRecords || []).length + ')'"
                            />
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
                              @click.stop="confirmRemoveQuote(item, qIdx)"
                              title="Delete"
                            />
                          </td>
                        </tr>
                        <!-- Shop Records sub-table (collapsible, for AR condition) -->
                        <tr v-if="quote.condition === 'AR' && isShopExpanded(quote.id, item.rfqItemId)">
                          <td :colspan="14" class="pa-0">
                            <div class="shop-panel">
                              <div class="d-flex align-center justify-space-between mb-2">
                                <span class="text-caption text-uppercase font-weight-bold" style="color: #ff9800;">
                                  <v-icon icon="mdi-wrench" size="14" class="mr-1" />
                                  Shop Records ({{ (quote.shopRecords || []).length }})
                                </span>
                                <div class="d-flex align-center gap-2">
                                  <v-btn
                                    v-for="otherQ in getOtherARQuotesWithShops(item, quote)"
                                    :key="otherQ.id"
                                    size="x-small"
                                    color="info"
                                    variant="tonal"
                                    prepend-icon="mdi-content-copy"
                                    @click.stop="copyShopsFrom(item, quote, otherQ)"
                                    :title="`Copy all shops from ${otherQ.supplierName}`"
                                  >
                                    Copy from {{ otherQ.supplierName }}
                                  </v-btn>
                                  <v-btn
                                    size="x-small"
                                    color="warning"
                                    variant="flat"
                                    prepend-icon="mdi-plus"
                                    @click.stop="addShopRow(item, quote)"
                                  >
                                    Add Shop
                                  </v-btn>
                                </div>
                              </div>
                              <table class="quote-grid" v-if="(quote.shopRecords || []).length > 0" style="width: 100%; border-collapse: collapse;">
                                <thead>
                                  <tr>
                                    <th>Supplier</th>
                                    <th>Alt P/N</th>
                                    <th>Condition</th>
                                    <th>Qty</th>
                                    <th>Unit</th>
                                    <th>Cost Price ($)</th>
                                    <th style="color: #ff9800;">Repair Cost ($)</th>
                                    <th>Cert Type</th>
                                    <th>Tag Date</th>
                                    <th>Shipping Cost</th>
                                    <th>Shipping Point</th>
                                    <th>LeadTime</th>
                                    <th>Note</th>
                                    <th>My Notes</th>
                                    <th style="width: 40px" class="text-center">Cert</th>
                                    <th style="width: 70px;"></th>
                                  </tr>
                                </thead>
                                <tbody>
                                  <tr v-for="(shop, sIdx) in quote.shopRecords" :key="'shop-' + sIdx" class="shop-row">
                                    <td>
                                      <input
                                        type="text"
                                        class="quote-input"
                                        placeholder="Shop name..."
                                        v-model="shop.supplierName"
                                        @input="searchSupplier(shop.supplierName)"
                                        list="supplier-suggestions"
                                      />
                                      <div
                                        v-if="shop.supplierStatus === 'Pending'"
                                        style="font-size:10px; color:#f59e0b; padding: 1px 4px; line-height:1.4; pointer-events:none;"
                                      >
                                        ⏳ Pending approval
                                      </div>
                                      <div
                                        v-else-if="shop.supplierStatus === 'Rejected'"
                                        style="font-size:10px; color:#ef4444; padding: 1px 4px; line-height:1.4; cursor:pointer; text-decoration:underline;"
                                        @click.stop="openResubmit(shop)"
                                      >
                                        ❌ Rejected — click to correct &amp; resubmit
                                      </div>
                                    </td>
                                    <td>
                                      <input type="text" class="quote-input" placeholder="Same P/N" v-model="shop.alt" />
                                    </td>
                                    <td>
                                      <select class="quote-input quote-select" v-model="shop.condition">
                                        <option value="">—</option>
                                        <option value="IN">IN</option>
                                        <option value="RP">RP</option>
                                        <option value="OH">OH</option>
                                      </select>
                                    </td>
                                    <td>
                                      <input type="number" class="quote-input text-center" v-model.number="shop.qty" min="1" />
                                    </td>
                                    <td>
                                      <select class="quote-input quote-select" v-model="shop.unit">
                                        <option value="">—</option>
                                        <option value="EA">EA</option>
                                        <option value="Meter">METER</option>
                                        <option value="Kg">KG</option>
                                      </select>
                                    </td>
                                    <td>
                                      <input
                                        v-if="focusedField === `shop-price-${sIdx}-${quote.id}`"
                                        :data-focus-key="`shop-price-${sIdx}-${quote.id}`"
                                        type="number"
                                        class="quote-input price-input"
                                        placeholder="0.00"
                                        v-model.number="shop.price"
                                        step="0.01"
                                        min="0"
                                        @blur="focusedField = ''"
                                      />
                                      <span
                                        v-else
                                        class="quote-input price-display"
                                        @click.stop="focusField(`shop-price-${sIdx}-${quote.id}`)"
                                      >
                                        {{ shop.price ? '$' + formatPrice(shop.price) : '' }}
                                      </span>
                                    </td>
                                    <td>
                                      <input
                                        v-if="focusedField === `shop-fix-${sIdx}-${quote.id}`"
                                        :data-focus-key="`shop-fix-${sIdx}-${quote.id}`"
                                        type="number"
                                        class="quote-input price-input"
                                        style="color: #ff9800;"
                                        placeholder="0.00"
                                        v-model.number="shop.fixPrice"
                                        step="0.01"
                                        min="0"
                                        @blur="focusedField = ''"
                                      />
                                      <span
                                        v-else
                                        class="quote-input price-display"
                                        style="color: #ff9800;"
                                        @click.stop="focusField(`shop-fix-${sIdx}-${quote.id}`)"
                                      >
                                        {{ shop.fixPrice ? '$' + formatPrice(shop.fixPrice) : '' }}
                                      </span>
                                    </td>
                                    <td>
                                      <select class="quote-input quote-select" v-model="shop.certName">
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
                                      <input type="date" class="quote-input" v-model="shop.tagDate" :max="today" />
                                    </td>
                                    <td>
                                      <input
                                        v-if="focusedField === `shop-ship-${sIdx}-${quote.id}`"
                                        :data-focus-key="`shop-ship-${sIdx}-${quote.id}`"
                                        type="number"
                                        class="quote-input price-input"
                                        placeholder="0.00"
                                        v-model.number="shop.shippingCost"
                                        step="0.01"
                                        min="0"
                                        @blur="focusedField = ''"
                                      />
                                      <span
                                        v-else
                                        class="quote-input price-display"
                                        @click.stop="focusField(`shop-ship-${sIdx}-${quote.id}`)"
                                      >
                                        {{ shop.shippingCost ? '$' + formatPrice(shop.shippingCost) : '' }}
                                      </span>
                                    </td>
                                    <td>
                                      <input type="text" class="quote-input" placeholder="City / Hub" v-model="shop.shippingPoint" />
                                    </td>
                                    <td>
                                      <input type="text" class="quote-input" placeholder="e.g. 5 days" v-model="shop.leadTime" />
                                    </td>
                                    <td>
                                      <VTextarea rows="2" placeholder="Note..." v-model="shop.note" hide-details density="compact" variant="plain" />
                                    </td>
                                    <td>
                                      <VTextarea rows="2" placeholder="My Notes..." v-model="shop.myNotes" hide-details density="compact" variant="plain" />
                                    </td>
                                    <td class="text-center" style="white-space: nowrap;">
                                      <v-btn
                                        icon="mdi-content-save"
                                        size="x-small"
                                        variant="text"
                                        color="success"
                                        :loading="shop._saving"
                                        @click.stop="saveShopQuote(item, quote, shop)"
                                        title="Save Shop"
                                      />
                                      <v-btn
                                        icon="mdi-close"
                                        size="x-small"
                                        variant="text"
                                        color="error"
                                        @click.stop="confirmRemoveShop(item, quote, sIdx)"
                                        title="Delete"
                                      />
                                    </td>
                                  </tr>
                                </tbody>
                              </table>
                              <div v-else class="text-center pa-3">
                                <p class="text-caption text-medium-emphasis">No shop records. Click "Add Shop" to start.</p>
                              </div>
                            </div>
                          </td>
                        </tr>
                        </template>
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
      <option v-for="s in supplierSuggestions" :key="s.id" :value="s.name">
        {{ s.status === 'Pending' ? '(Pending Approval)' : '' }}
      </option>
    </datalist>

    <!-- Resubmit Supplier Dialog -->
    <v-dialog v-model="resubmitDialog" max-width="440">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-account-sync-outline" color="error" class="mr-2" />
          Correct Supplier Name
        </v-card-title>
        <v-card-text>
          <v-alert type="error" variant="tonal" density="compact" class="mb-4">
            This supplier was rejected. Enter the correct supplier name and resubmit for admin approval.
          </v-alert>
          <v-text-field
            v-model="resubmitName"
            label="Correct Supplier Name"
            variant="outlined"
            density="compact"
            autofocus
          />
        </v-card-text>
        <v-card-actions class="justify-end pa-4">
          <v-btn variant="text" @click="resubmitDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="tonal" :loading="resubmitSaving" :disabled="!resubmitName.trim()" @click="doResubmit">
            Resubmit
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>

    <ConfirmDialog
      v-model="showConfirmQuote"
      title="Delete Supplier Quote?"
      message="Are you sure you want to remove this quote?"
      @confirm="doRemoveQuote"
    />

    <ConfirmDialog
      v-model="showConfirmShop"
      title="Delete Shop Record?"
      message="Are you sure you want to remove this shop record?"
      @confirm="doRemoveShop"
    />
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
  pnSearch: '',
})
const search = pf.search
const loading = ref(false)
const allItems = ref<any[]>([])
const editableQuotes = ref<Record<number, any[]>>({})
const expandedArray = ref<any[]>([])
const focusedField = ref('')
const supplierSuggestions = ref<{ id: number; name: string; status: string }[]>([])
const itemSuggestions = ref<Record<number, { knownSuppliers: any[]; recentQuotes: any[]; loading: boolean }>>({})
const partAvailability = ref<Record<number, any>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const showPendingOnly = ref(false)

// Delete Confirmations
const showConfirmQuote = ref(false)
const showConfirmShop = ref(false)
const deleteTargetQuote = ref<{ item: any; idx: number } | null>(null)
const deleteTargetShop = ref<{ item: any; parentQuote: any; idx: number } | null>(null)

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ──── Supplier Resubmit ────

const resubmitDialog = ref(false)
const resubmitSaving = ref(false)
const resubmitName = ref('')
const resubmitTarget = ref<any>(null)

const allEditableQuotesList = computed(() => {
  const all: any[] = []
  for (const quotes of Object.values(editableQuotes.value)) {
    for (const q of quotes) {
      all.push(q)
      for (const shop of (q.shopRecords || [])) all.push(shop)
    }
  }
  return all
})

const pendingSupplierQuotes = computed(() =>
  allEditableQuotesList.value.filter(q => q.supplierStatus === 'Pending')
)
const rejectedSupplierQuotes = computed(() =>
  allEditableQuotesList.value.filter(q => q.supplierStatus === 'Rejected')
)

function openResubmit(quote: any) {
  resubmitTarget.value = quote
  resubmitName.value = quote.supplierName || ''
  resubmitDialog.value = true
}

async function doResubmit() {
  if (!resubmitTarget.value?.supplierId || !resubmitName.value.trim()) return
  resubmitSaving.value = true
  try {
    await api.post(`/suppliers/${resubmitTarget.value.supplierId}/resubmit`, { name: resubmitName.value.trim() })
    showSnack('Resubmitted for approval', 'success')
    resubmitDialog.value = false
    await loadData()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to resubmit', 'error')
  } finally {
    resubmitSaving.value = false
  }
}

// ── Filters ──
const statusFilter = pf.status
const userFilter = pf.user
const customerFilter = pf.customer
const pnSearch = pf.pnSearch
const isAdmin = computed(() => authStore.isAdmin)

// Status options: cascade by customer+user (shows only statuses in the filtered set)
const statusOptions = computed(() => {
  const set = new Set<string>()
  let source = allItems.value
  if (customerFilter.value?.length)
    source = source.filter((item: any) => customerFilter.value.includes(item.customerName))
  if (userFilter.value?.length)
    source = source.filter((item: any) =>
      (item.assignedUsers || []).some((u: any) => userFilter.value.includes(u.id))
    )
  source.forEach((item: any) => { if (item.rfqStatus) set.add(item.rfqStatus) })
  return Array.from(set).sort()
})

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
  { title: 'Deadline', key: 'leadTime' },
  { title: 'Received Date', key: 'createdAt' },
]

const partNumberOptions = computed(() => {
  const set = new Set<string>()
  allItems.value.forEach((item: any) => { if (item.partNumberName) set.add(item.partNumberName) })
  return Array.from(set).sort()
})

// User options: cascade by status+customer
const userOptions = computed(() => {
  const map = new Map<number, string>()
  let source = allItems.value
  if (statusFilter.value?.length)
    source = source.filter((item: any) => statusFilter.value.includes(item.rfqStatus || 'Open'))
  if (customerFilter.value?.length)
    source = source.filter((item: any) => customerFilter.value.includes(item.customerName))
  source.forEach((item: any) => {
    ;(item.assignedUsers || []).forEach((u: any) => {
      if (u.id && u.name) map.set(u.id, u.name)
    })
  })
  return Array.from(map, ([id, name]) => ({ id, name }))
})

// Customer options: cascade by status+user
const customerOptions = computed(() => {
  const map = new Map<string, string>()
  let source = allItems.value
  if (statusFilter.value?.length)
    source = source.filter((item: any) => statusFilter.value.includes(item.rfqStatus || 'Open'))
  if (userFilter.value?.length)
    source = source.filter((item: any) =>
      (item.assignedUsers || []).some((u: any) => userFilter.value.includes(u.id))
    )
  source.forEach((item: any) => {
    if (item.customerName && !map.has(item.customerName))
      map.set(item.customerName, item.customerCode || '')
  })
  return Array.from(map.entries())
    .map(([name, code]) => ({ title: code ? `${name} (${code})` : name, value: name }))
    .sort((a, b) => a.title.localeCompare(b.title))
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
    result = result.filter((item: any) =>
      customerFilter.value.includes(item.customerName) ||
      (item.customerCode && customerFilter.value.includes(item.customerCode))
    )
  }
  if (pnSearch.value?.trim()) {
    const q = pnSearch.value.trim().toLowerCase()
    result = result.filter((item: any) =>
      (item.partNumbers || '').toLowerCase().includes(q) ||
      (item.altPartNumbers || '').toLowerCase().includes(q)
    )
  }
  if (showPendingOnly.value) {
    result = result.filter((item: any) =>
      (item.supplierQuotes || []).some((q: any) => q.supplierStatus === 'Pending' || q.supplierStatus === 'Rejected')
    )
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
  if (['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeUrgent(item.leadTime)) classes.push('lead-time-urgent-row')
  if (['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeExpired(item.leadTime)) classes.push('lead-time-expired-row')
  if (item.isHighlighted) classes.push('highlighted-row')
  if (expandedArray.value.includes(item.rfqItemId)) classes.push('expanded-row')
  return classes.length ? { class: classes.join(' ') } : {}
}

// ── Data Loading ──
onMounted(() => loadData())

async function loadData() {
  loading.value = true
  try {
    const accumulated: any[] = []
    let _page = 1
    while (true) {
      const res = await api.get<any>(`/procument-page?page=${_page}&pageSize=200`)
      const batch: any[] = Array.isArray(res) ? res : (res.items ?? res.Items ?? [])
      const total: number = (!Array.isArray(res) && res != null) ? (res.totalCount ?? res.TotalCount ?? batch.length) : batch.length
      accumulated.push(...batch)
      if (batch.length < 200 || accumulated.length >= total) break
      _page++
    }
    allItems.value = accumulated.map((item: any) => ({
      ...item,
      altPartNumbers: [
        ...(item.alternatives || []).map((a: any) => a.name),
        ...(item.supplierQuotes || []).map((q: any) => q.alt),
      ].filter(Boolean).join(', '),
    }))

    // Initialize editable quotes from server data (with nested shop records)
    const quotesMap: Record<number, any[]> = {}
    for (const item of allItems.value) {
      quotesMap[item.rfqItemId] = (item.supplierQuotes || []).map((q: any) => ({
        ...q,
        _saving: false,
        shopRecords: (q.shopRecords || []).map((s: any) => ({ ...s, _saving: false })),
      }))
    }
    editableQuotes.value = quotesMap

    // Load part availability in background
    const allPartIds = [...new Set(allItems.value.map((i: any) => i.partNumberId).filter(Boolean))]
    if (allPartIds.length > 0) {
      try {
        const avail = await api.post<any[]>('/availability/parts', { partNumberIds: allPartIds })
        const map: Record<number, any> = {}
        for (const a of avail) map[a.partNumberId] = a
        partAvailability.value = map
      } catch {}
    }
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
    loadSuggestions(item)
  }
}

async function loadSuggestions(item: any) {
  const key = item.rfqItemId
  if (itemSuggestions.value[key] && !itemSuggestions.value[key].loading) return // already loaded
  itemSuggestions.value[key] = { knownSuppliers: [], recentQuotes: [], loading: true }
  try {
    const data = await api.get<any>(`/procument-page/suggestions?partNumberId=${item.partNumberId}&rfqId=${item.rfqId}`)
    itemSuggestions.value[key] = {
      knownSuppliers: data.knownSuppliers || [],
      recentQuotes: data.recentQuotes || [],
      loading: false,
    }
  } catch {
    itemSuggestions.value[key] = { knownSuppliers: [], recentQuotes: [], loading: false }
  }
}

function getSuggestions(rfqItemId: number) {
  return itemSuggestions.value[rfqItemId] || { knownSuppliers: [], recentQuotes: [], loading: false }
}

function applySuggestion(item: any, suggestion: any) {
  const key = item.rfqItemId
  if (!editableQuotes.value[key]) {
    editableQuotes.value[key] = []
  }
  // Check if this supplier is already added
  const alreadyExists = editableQuotes.value[key].some(
    (q: any) => q.supplierName?.toLowerCase() === suggestion.supplierName?.toLowerCase()
  )
  if (alreadyExists) {
    showSnack(`${suggestion.supplierName} is already added`, 'warning')
    return
  }
  editableQuotes.value[key].push({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: suggestion.supplierName,
    qty: suggestion.qty || item.qty || 1,
    price: suggestion.price || 0,
    condition: suggestion.condition || 'NE',
    alt: suggestion.alt || '',
    certName: suggestion.certName || '',
    tagDate: suggestion.tagDate || '',
    shippingCost: suggestion.shippingCost ?? null,
    shippingPoint: suggestion.shippingPoint || '',
    unit: suggestion.unit || 'EA',
    leadTime: suggestion.leadTime || '',
    note: suggestion.note || '',
    myNotes: suggestion.myNotes || '',
    _saving: false,
  })
  if (!expandedArray.value.includes(item.rfqItemId)) {
    expandedArray.value.push(item.rfqItemId)
  }
  showSnack(`${suggestion.supplierName} added from history`, 'success')
}

function applyAllSuggestions(item: any) {
  const suggestions = getSuggestions(item.rfqItemId)
  let added = 0
  for (const s of suggestions.recentQuotes) {
    const key = item.rfqItemId
    const alreadyExists = (editableQuotes.value[key] || []).some(
      (q: any) => q.supplierName?.toLowerCase() === s.supplierName?.toLowerCase()
    )
    if (!alreadyExists) {
      applySuggestion(item, s)
      added++
    }
  }
  if (added === 0) {
    showSnack('All suggested suppliers are already added', 'info')
  }
}

function applyAvailability(item: any, rec: any) {
  const key = item.rfqItemId
  if (!editableQuotes.value[key]) editableQuotes.value[key] = []
  if (!expandedArray.value.includes(key)) expandedArray.value.push(key)
  editableQuotes.value[key].push({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: rec.label || '',
    qty: rec.qty || item.qty || 1,
    price: rec.price || 0,
    condition: rec.condition || item.condition || 'NE',
    alt: rec.altPartNumber || '',
    certName: rec.certName || '',
    tagDate: '',
    shippingCost: null,
    shippingPoint: '',
    unit: 'EA',
    leadTime: rec.leadTime || '',
    note: '',
    myNotes: '',
    _saving: false,
    shopRecords: [],
  })
  showSnack(`${rec.label} added`, 'success')
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
      myNotes: quote.myNotes || null,
      isCertificated: quote.isCertificated || false,
      type: quote.type || 'Procument',
    }

    const result = await api.post(`/rfqs/${item.rfqId}/supplier-quotes`, payload)
    // Update the quote with the returned ID
    if (result && (result as any).id) {
      quote.id = (result as any).id
    }
    // If RFQ was Rejected, backend resets it to In Progress - update local state
    if (item.rfqStatus === 'Rejected') {
      item.rfqStatus = 'In Progress'
    }
    showSnack('Supplier quote saved', 'success')
  } catch (e) {
    showSnack('Failed to save quote', 'error')
  } finally {
    quote._saving = false
  }
}

function confirmRemoveQuote(item: any, qIdx: number) {
  deleteTargetQuote.value = { item, idx: qIdx }
  showConfirmQuote.value = true
}

async function doRemoveQuote() {
  if (!deleteTargetQuote.value) return
  const { item, idx } = deleteTargetQuote.value
  await removeQuote(item, idx)
  deleteTargetQuote.value = null
  showConfirmQuote.value = false
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
      supplierSuggestions.value = await api.get<{ id: number; name: string; status: string; dependency: string }[]>(`/suppliers/search?q=${encodeURIComponent(val)}`)
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

// ── Shop Records (AR condition) ──
const expandedShops = ref<Set<string>>(new Set())

/** Returns other AR quotes for the same RFQ item that already have shop records */
function getOtherARQuotesWithShops(item: any, currentQuote: any) {
  return (editableQuotes.value[item.rfqItemId] || []).filter(
    (q: any) => q.condition === 'AR' && q.id !== currentQuote.id && (q.shopRecords || []).length > 0
  )
}

/** Copies all shop records from sourceQuote into targetQuote (IDs cleared so they'll be saved as new) */
function copyShopsFrom(item: any, targetQuote: any, sourceQuote: any) {
  if (!targetQuote.shopRecords) targetQuote.shopRecords = []
  const copies = (sourceQuote.shopRecords || []).map((s: any) => ({
    ...s,
    id: null,
    _saving: false,
    parentProcumentId: targetQuote.id,
    rfqItemId: item.rfqItemId,
  }))
  targetQuote.shopRecords.push(...copies)
  // Auto-expand so user sees the pasted rows
  const key = `${targetQuote.id}-${item.rfqItemId}`
  expandedShops.value.add(key)
  expandedShops.value = new Set(expandedShops.value)
  showSnack(`Copied ${copies.length} shop record${copies.length !== 1 ? 's' : ''} from ${sourceQuote.supplierName}`, 'info')
}

function toggleShop(quoteId: number, rfqItemId: number) {
  const key = `${quoteId}-${rfqItemId}`
  if (expandedShops.value.has(key)) {
    expandedShops.value.delete(key)
  } else {
    expandedShops.value.add(key)
  }
  expandedShops.value = new Set(expandedShops.value)
}

function isShopExpanded(quoteId: number, rfqItemId: number) {
  return expandedShops.value.has(`${quoteId}-${rfqItemId}`)
}

function addShopRow(item: any, parentQuote: any) {
  if (!parentQuote.shopRecords) parentQuote.shopRecords = []
  parentQuote.shopRecords.push({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: '',
    qty: parentQuote.qty || item.qty || 1,
    price: parentQuote.price || 0,
    fixPrice: null,
    condition: 'IN',
    alt: parentQuote.alt || '',
    certName: '',
    tagDate: '',
    shippingCost: null,
    shippingPoint: '',
    unit: 'EA',
    leadTime: '',
    note: '',
    myNotes: '',
    type: 'Shop',
    parentProcumentId: parentQuote.id,
    _saving: false,
  })
  // Auto-expand shop section
  const key = `${parentQuote.id}-${item.rfqItemId}`
  expandedShops.value.add(key)
  expandedShops.value = new Set(expandedShops.value)
}

async function saveShopQuote(item: any, parentQuote: any, shop: any) {
  if (!shop.supplierName?.trim()) {
    showSnack('Supplier name is required', 'error')
    return
  }
  if (!parentQuote.id) {
    showSnack('Save the parent procurement record first', 'error')
    return
  }

  shop._saving = true
  try {
    const payload = {
      id: shop.id || null,
      rfqItemId: item.rfqItemId,
      supplierName: shop.supplierName,
      qty: shop.qty,
      price: shop.price,
      fixPrice: shop.fixPrice,
      condition: shop.condition || 'IN',
      alt: shop.alt,
      certName: shop.certName || null,
      tagDate: shop.tagDate || null,
      shippingCost: shop.shippingCost ?? null,
      shippingPoint: shop.shippingPoint || null,
      unit: shop.unit || null,
      leadTime: shop.leadTime || null,
      note: shop.note || null,
      myNotes: shop.myNotes || null,
      isCertificated: shop.isCertificated || false,
      type: 'Shop',
      parentProcumentId: parentQuote.id,
    }
    const result = await api.post(`/rfqs/${item.rfqId}/supplier-quotes`, payload)
    if (result && (result as any).id) shop.id = (result as any).id
    showSnack('Shop record saved', 'success')
  } catch {
    showSnack('Failed to save shop record', 'error')
  } finally {
    shop._saving = false
  }
}

function confirmRemoveShop(item: any, parentQuote: any, sIdx: number) {
  deleteTargetShop.value = { item, parentQuote, idx: sIdx }
  showConfirmShop.value = true
}

async function doRemoveShop() {
  if (!deleteTargetShop.value) return
  const { item, parentQuote, idx } = deleteTargetShop.value
  await removeShopQuote(item, parentQuote, idx)
  deleteTargetShop.value = null
  showConfirmShop.value = false
}

async function removeShopQuote(item: any, parentQuote: any, sIdx: number) {
  const shop = parentQuote.shopRecords[sIdx]
  if (shop.id) {
    try {
      await api.del(`/rfqs/${item.rfqId}/supplier-quotes/${shop.id}`)
      showSnack('Shop record removed', 'success')
    } catch {
      showSnack('Failed to remove shop record', 'error')
      return
    }
  }
  parentQuote.shopRecords.splice(sIdx, 1)
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

.suggestions-bar {
  background: rgba(var(--v-theme-surface-variant), 0.25);
  border: 1px dashed rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 8px;
  padding: 10px 14px;
}
.cursor-pointer {
  cursor: pointer;
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

.shop-panel {
  background: rgba(255, 152, 0, 0.04);
  border-left: 3px solid #ff9800;
  margin-left: 20px;
  padding: 10px 16px;
}
.shop-row td {
  background: rgba(255, 152, 0, 0.02);
}
</style>
