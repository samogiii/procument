<template>
  <div  class="rfq-single-view">
    <!-- Header Bar -->
    <div class="d-flex align-center mb-4 flex-wrap gap-2">
      <v-btn icon="mdi-arrow-left" variant="text" to="/rfqs" class="mr-1 flex-shrink-0" size="small" />
      <div class="min-width-0">
        <h1 class="text-h6 text-sm-h5 font-weight-bold d-flex align-center gap-2">
          RFQ #{{ route.params.id }}
          <v-chip :color="statusColor" size="small" class="ml-1">{{ rfq.status || 'Open' }}</v-chip>
        </h1>
        <p class="text-caption text-medium-emphasis mt-1 text-truncate d-flex align-center gap-2" v-if="rfq.name">
          {{ rfq.name }}
          <v-btn v-if="isAdmin" icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="editNameDialog = true" />
        </p>
      </div>
    </div>

    <!-- RFQ Info Cards -->
    <v-row class="mb-5" dense>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="primary" variant="tonal" size="40">
              <v-icon icon="mdi-domain" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Customer</p>
              <p class="text-body-2 font-weight-medium mb-0">
                <template v-if="isAdmin">{{ rfq.customerName }}<span v-if="rfq.customerCode" class="text-medium-emphasis ml-1">({{ rfq.customerCode }})</span></template>
                <template v-else>{{ rfq.customerCode || '—' }}</template>
              </p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4" :style="isLeadTimeUrgent(rfq.leadTime) ? '' : ''">
          <div class="d-flex align-center gap-3">
            <v-avatar :color="isLeadTimeUrgent(rfq.leadTime) ? 'error' : 'info'" variant="tonal" size="40">
              <v-icon :icon="isLeadTimeUrgent(rfq.leadTime) ? 'mdi-alert' : 'mdi-clock-outline'" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Deadline</p>
              <p class="text-body-2 font-weight-medium mb-0 d-flex align-center gap-2" :style="isLeadTimeUrgent(rfq.leadTime) ? '' : ''">
                {{ rfq.leadTime ? new Date(rfq.leadTime).toLocaleDateString() : '—' }}
                <v-icon v-if="isLeadTimeUrgent(rfq.leadTime)" icon="mdi-alert" size="14" color="error" />
                <v-btn v-if="isAdmin" icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="editDeadlineDialog = true" />
              </p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="success" variant="tonal" size="40">
              <v-icon icon="mdi-account-outline" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Created By</p>
              <p class="text-body-2 font-weight-medium mb-0">
                {{ rfq.userName || '—' }}
              </p>
              <p class="text-caption text-medium-emphasis mb-0" v-if="rfq.createdAt">
                {{ new Date(rfq.createdAt).toLocaleDateString() }}
                &nbsp;·&nbsp;
                <span class="font-weight-medium" style="color: rgba(var(--v-theme-on-surface),0.55);">
                  {{ new Date(rfq.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
                </span>
              </p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="success" variant="tonal" size="40">
              <v-icon icon="mdi-account-group-outline" size="20" />
            </v-avatar>
            <div style="flex: 1; min-width: 0;">
              <p class="text-caption text-medium-emphasis mb-0">Assigned Users</p>
              <div class="d-flex flex-column gap-1 mt-1" v-if="assignedUsers.length">
                <div v-for="user in assignedUsers" :key="user.id" class="d-flex align-center justify-space-between gap-2">
                  <v-chip
                    size="x-small"
                    color="primary"
                    variant="tonal"
                    prepend-icon="mdi-account"
                  >
                    {{ user.name }}
                  </v-chip>
                  <span class="text-caption text-medium-emphasis" style="font-size: 10px !important;">
                    {{ user.assignedAt ? new Date(user.assignedAt).toLocaleDateString() : '' }}
                  </span>
                </div>
              </div>
              <p v-else class="text-body-2 font-weight-medium mb-0">—</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="warning" variant="tonal" size="40">
              <v-icon icon="mdi-package-variant" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Total Items</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.items?.length || 0 }} parts</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar :color="exTypeOptions.find(e => e.value === rfq.exType)?.color || 'grey'" variant="tonal" size="40">
              <v-icon icon="mdi-tag-outline" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">ExType</p>
              <v-menu>
                <template #activator="{ props: menuProps }">
                  <v-chip
                    :color="exTypeOptions.find(e => e.value === rfq.exType)?.color || 'grey'"
                    v-bind="menuProps"
                    class="cursor-pointer mt-1"
                    append-icon="mdi-chevron-down"
                    size="small"
                  >
                    {{ exTypeOptions.find(e => e.value === rfq.exType)?.label || 'Not Set' }}
                  </v-chip>
                </template>
                <v-list density="compact" style="min-width: 180px">
                  <v-list-subheader>Change ExType</v-list-subheader>
                  <v-list-item
                    v-for="opt in exTypeOptions"
                    :key="opt.value"
                    :value="opt.value"
                    :active="rfq.exType === opt.value"
                    @click="changeExType(opt.value)"
                  >
                    <template #prepend>
                      <v-icon :icon="opt.icon" :color="opt.color" size="18" />
                    </template>
                    <v-list-item-title>{{ opt.label }}</v-list-item-title>
                  </v-list-item>
                </v-list>
              </v-menu>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Priority & Notes Row -->
    <v-row class="mb-5" dense>
      <!-- <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar :color="rfq.priority === 'AOG' ? 'error' : rfq.priority === 'Urgent' ? 'warning' : 'secondary'" variant="tonal" size="40">
              <v-icon icon="mdi-flag-outline" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Priority</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.priority || 'Normal' }}</p>
            </div>
          </div>
        </v-card>
      </v-col> -->
      <v-col cols="12" md="9" v-if="rfq.notes || isAdmin">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="info" variant="tonal" size="40">
              <v-icon icon="mdi-note-text-outline" size="20" />
            </v-avatar>
            <div class="flex-grow-1">
              <div class="d-flex align-center justify-space-between w-100">
                <p class="text-caption text-medium-emphasis mb-0">Notes</p>
                <div class="d-flex align-center gap-2" v-if="isAdmin">
                  <v-btn v-if="!isEditingNotes" size="x-small" variant="text" prepend-icon="mdi-pencil" @click="editNotes">Edit Note</v-btn>
                  <v-btn v-if="isEditingNotes" size="x-small" color="primary" variant="tonal" prepend-icon="mdi-check" @click="saveNotes" :loading="isSavingNotes">Save</v-btn>
                  <v-btn v-if="isEditingNotes" size="x-small" color="error" variant="text" prepend-icon="mdi-close" @click="cancelNotesEdit">Cancel</v-btn>
                </div>
              </div>
              <p class="text-body-2 font-weight-medium mb-0" v-if="!isEditingNotes" style="white-space: pre-wrap;">{{ rfq.notes || '—' }}</p>
              <v-textarea v-else v-model="editingNotesValue" rows="3" density="compact" hide-details variant="outlined" class="mt-2" placeholder="Enter RFQ notes here..."></v-textarea>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Excel-Style Quoting Table -->
    <v-card class="excel-card">
      <div class="excel-toolbar d-flex flex-wrap align-center justify-space-between pa-3 gap-2">
        <div class="d-flex align-center gap-2">
          <v-icon icon="mdi-table" size="18" color="primary" />
          <span class="text-body-2 font-weight-bold">Procurement Records</span>
          <v-chip size="x-small" color="primary" variant="tonal" class="ml-1">
            {{ totalQuotes }} records
          </v-chip>
        </div>
        <div class="d-flex flex-wrap gap-1 gap-sm-2">
          <!-- Action Buttons -->
          <!-- <v-menu>
            <template v-slot:activator="{ props }">
              <v-btn icon="mdi-dots-vertical" variant="text" size="small" v-bind="props" />
            </template>
            <v-list density="compact">
              <v-list-item prepend-icon="mdi-shield-account" @click="showPermissions = true" v-if="isAdmin">
                <v-list-item-title>Manage Permissions</v-list-item-title>
              </v-list-item>
              <v-list-item prepend-icon="mdi-history" @click="showAudit = true">
                <v-list-item-title>Audit History</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu> -->
          <VBtn prepend-icon="mdi-history" @click="showAudit = true" size="small" variant="tonal" color="secondary" v-if="isAdmin">Audit</VBtn>
          <VBtn v-if="isAdmin" prepend-icon="mdi-shield-account" @click="showPermissions = true" size="small" variant="tonal" color="secondary">Perms</VBtn>
          <v-btn v-if="isAdmin" prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
          <v-btn
            size="small"
            variant="tonal"
            color="warning"
            prepend-icon="mdi-plus-circle-outline"
            @click="openAddItemDialog"
          >
            Add Item
          </v-btn>
          <v-tooltip v-if="rfq.status === 'Quoted'" location="bottom" text="A quote has already been created for this RFQ">
            <template #activator="{ props: tp }">
              <span v-bind="tp">
                <v-btn
                  size="small"
                  variant="tonal"
                  color="success"
                  prepend-icon="mdi-check-circle"
                  disabled
                >
                  Quoted
                </v-btn>
              </span>
            </template>
          </v-tooltip>
          <v-btn
            v-if="existingQuoteId && rfq.status === 'Quoted'"
            size="small"
            variant="tonal"
            color="primary"
            prepend-icon="mdi-eye"
            :to="`/quotes/${existingQuoteId}`"
          >
            View Quote
          </v-btn>
          <v-btn
            v-if="!['Ready To Quote', 'Sent', 'Accepted', 'No Quote'].includes(rfq.status)"
            size="small"
            variant="tonal"
            color="success"
            prepend-icon="mdi-plus"
            :to="`/rfqs/${route.params.id}/create-quote`"
          >
            Add Commission
          </v-btn>

          <!-- No Quote chip shown when already in No Quote status -->
          <v-chip v-if="rfq.status === 'No Quote'" color="deep-purple" variant="tonal" size="small" prepend-icon="mdi-cancel">
            No Quote
          </v-chip>
          <v-tooltip v-if="rfq.status === 'No Quote' && rfq.noQuoteReason" location="bottom">
            <template #activator="{ props: tp }">
              <v-icon v-bind="tp" icon="mdi-information-outline" color="deep-purple" size="18" class="ml-1 cursor-pointer" />
            </template>
            <span>{{ rfq.noQuoteReason }}</span>
          </v-tooltip>

          <!-- No Quote button: only for Open / In Progress -->
          <v-btn
            v-if=" ['Open', 'In Progress'].includes(rfq.status)"
            size="small"
            variant="tonal"
            color="deep-purple"
            prepend-icon="mdi-cancel"
            :loading="noQuoteLoading"
            @click="showNoQuoteConfirm = true"
          >
            No Quote
          </v-btn>
          
          <v-btn
            size="small"
            variant="tonal"
            color="secondary"
            prepend-icon="mdi-content-copy"
            @click="copyAllPartNumbers"
          >
            Copy All P/N
          </v-btn>
          <v-btn
            size="small"
            variant="tonal"
            :color="rfq.isUnread ? 'blue' : 'grey'"
            :prepend-icon="rfq.isUnread ? 'mdi-email-mark-as-unread' : 'mdi-email-open-outline'"
            @click="toggleUnread"
          >
            {{ rfq.isUnread ? 'Unread' : 'Mark Unread' }}
          </v-btn>
          <v-btn
            size="small"
            variant="tonal"
            color="primary"
            prepend-icon="mdi-content-save"
            :loading="saving"
            @click="saveAll" 
            
          >
            Save All (Ctrl + Shift + S)
          </v-btn>
        </div>
      </div>

      <!-- No Quote Reason Banner -->
      <v-alert
        v-if="rfq.status === 'No Quote' && rfq.noQuoteReason"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-3"
        prepend-icon="mdi-cancel"
      >
        <strong>No Quote Reason:</strong> {{ rfq.noQuoteReason }}
      </v-alert>

      <div class="excel-container">
        <table class="excel-grid">
          <thead>
            <tr>
              <th style="min-width: 60px; text-align: center;"></th>
              <th style="min-width: 50px; text-align: center;">#</th>
              <th style="min-width: 180px;">Part Number</th>
              <th style="min-width: 180px;">Description</th>
              <th style="min-width: 80px;">Qty</th>
              <th style="min-width: 80px;">Unit</th>
              <th style="min-width: 100px;">Condition</th>
              <th style="min-width: 120px;">Priority</th>
              <th style="min-width: 140px;">Remark</th>
              <th style="min-width: 200px;">Alternatives</th>
              <th style="min-width: 200px;">Note</th>
              <th style="min-width: 120px;">Procurements</th>
              <th v-if="isAdmin" style="min-width: 60px; text-align: center;"></th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(item, idx) in editableItems" :key="item.id">
              <!-- Master Row — editable fields -->
              <tr class="master-row" :data-item-id="item.id" :class="{ 'expanded': expandedRows.has(item.id), 'highlighted-row': item.isHighlighted }">
                <td class="cell-expand" style="display: flex; align-items: center; gap: 2px;">
                  <v-icon
                    :icon="expandedRows.has(item.id) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                    size="18"
                    :color="expandedRows.has(item.id) ? 'primary' : 'grey'"
                    @click="toggleExpand(item.id)"
                    class="cursor-pointer"
                  />
                  <v-icon
                    icon="mdi-pencil"
                    size="16"
                    :color="item.isHighlighted ? 'warning' : 'grey'"
                    class="cursor-pointer highlight-pen"
                    :title="item.isHighlighted ? 'Remove highlight' : 'Highlight this item'"
                    @click.stop="toggleHighlight(item)"
                  />
                </td>
                <td class="cell-number">{{ idx + 1 }}</td>
                <td class="cell-pn cell-copyable" @click.stop="copyPartNumber(item.partNumberName)" :title="'Click to copy: ' + item.partNumberName">
                  {{ item.partNumberName }}
                  <v-icon icon="mdi-content-copy" size="12" class="copy-icon ml-1" />
                </td>
                <td class="cell-pn">{{ item.description }}</td>
                <td>
                  <input
                    type="number"
                    class="item-input text-center"
                    v-model.number="item.qty"
                    min="1"
                  />
                </td>
                <td>
                  <select class="item-input item-select" v-model="item.unit">
                    <option value="">—</option>
                    <option value="EA">EA</option>
                    <option value="Meter">METER</option>
                    <option value="Kg">KG</option>
                  </select>
                </td>
                <td>
                  <select class="item-input item-select" v-model="item.condition">
                    <option value="">N/A</option>
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
                   <select class="item-input item-select" v-model="item.priority">
                    <option value="AOG">AOG</option>
                    <option value="Urgent">Urgent</option>
                    <option value="Normal">Normal</option>
                   </select>
                 
                </td>
                <td>
                  <input
                    type="text"
                    class="item-input"
                    placeholder="Remark"
                    v-model="item.remark"
                  />
                </td>
                <td class="cell-alternatives">
                  <div class="d-flex flex-wrap align-center gap-1">
                    <v-chip
                      v-for="alt in item.alternatives"
                      :key="alt.id"
                      size="x-small"
                      color="warning"
                      variant="tonal"
                      closable
                      @click:close="removeAlternative(item, alt)"
                    >
                      {{ alt.name }}
                    </v-chip>
                    <v-btn
                      icon="mdi-plus"
                      size="x-small"
                      variant="text"
                      color="primary"
                      density="compact"
                      @click.stop="openAddAlt(item)"
                    />
                  </div>
                </td>
                <td>
                  <input
                    type="text"
                    class="item-input"
                    placeholder="Note"
                    v-model="item.note"
                  />
                </td>
                <td class="cell-status">
                  <span :class="getQuoteCount(item.id) > 0 ? 'text-success' : 'text-medium-emphasis'">
                    {{ getQuoteCount(item.id) }} supplier{{ getQuoteCount(item.id) !== 1 ? 's' : '' }}
                  </span>
                </td>
                <td v-if="isAdmin" class="text-center">
                  <v-btn
                    v-if="getQuoteCount(item.id) === 0"
                    icon="mdi-delete-outline"
                    variant="text"
                    size="x-small"
                    color="error"
                    @click.stop="confirmDeleteItem(item)"
                  />
                </td>
              </tr>

              <!-- Expanded Detail Row -->
              <tr v-if="expandedRows.has(item.id)" class="detail-row">
                <td :colspan="isAdmin ? 13 : 12" class="detail-cell">
                  <div class="quote-panel">
                    <div class="quote-header d-flex align-center justify-space-between mb-3">
                      <span class="text-caption text-uppercase font-weight-bold letter-spacing-wide" style="color: #60a5fa;">
                        Supplier Quotes for {{ item.partNumberName }}
                      </span>
                      <v-btn
                        size="x-small"
                        color="primary"
                        variant="flat"
                        prepend-icon="mdi-plus"
                        @click="addQuoteRow(item.id)"
                      >
                        Add Supplier
                      </v-btn>
                    </div>

                    <!-- Supplier Suggestions -->
                    <div
                      v-if="getSuggestions(item.id).recentQuotes.length > 0 || getSuggestions(item.id).knownSuppliers.length > 0 || partAvailability[item.partNumberId]"
                      class="suggestions-bar mb-3"
                    >
                      <div v-if="getSuggestions(item.id).recentQuotes.length > 0" class="d-flex flex-wrap align-center gap-2 mb-2">
                        <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                          <v-icon icon="mdi-lightbulb-on-outline" size="14" color="amber" class="mr-1" />
                          Recent suppliers:
                        </span>
                        <v-chip
                          v-for="s in getSuggestions(item.id).recentQuotes"
                          :key="'recent-' + s.supplierId"
                          size="small"
                          color="amber"
                          variant="tonal"
                          prepend-icon="mdi-flash"
                          class="cursor-pointer"
                          @click="applySuggestion(item, s)"
                        >
                          {{ s.supplierName }}
                          <span class="text-caption ml-1 text-medium-emphasis">${{ formatPrice(s.price) }}</span>
                        </v-chip>
                        <v-btn
                          v-if="getSuggestions(item.id).recentQuotes.length > 1"
                          size="x-small"
                          variant="outlined"
                          color="amber"
                          prepend-icon="mdi-plus-box-multiple"
                          @click="applyAllSuggestions(item)"
                        >
                          Add All
                        </v-btn>
                      </div>
                      <div
                        v-if="getSuggestions(item.id).knownSuppliers.filter((k: any) => !getSuggestions(item.id).recentQuotes.some((r: any) => r.supplierId === k.supplierId)).length > 0"
                        class="d-flex flex-wrap align-center gap-2 mb-2"
                      >
                        <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                          <v-icon icon="mdi-account-group-outline" size="14" color="grey" class="mr-1" />
                          Known suppliers:
                        </span>
                        <v-chip
                          v-for="k in getSuggestions(item.id).knownSuppliers.filter((k: any) => !getSuggestions(item.id).recentQuotes.some((r: any) => r.supplierId === k.supplierId))"
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
                            @click="applyAvailability(item, rec)"
                          >{{ rec.label }}<span v-if="rec.price" class="text-caption ml-1" style="opacity:0.75">${{ formatPrice(rec.price) }}</span></v-chip>
                          <v-chip
                            v-for="rec in partAvailability[item.partNumberId].capListRecords"
                            :key="'cap-' + rec.label"
                            size="small"
                            class="avail-chip avail-chip--caplist cursor-pointer"
                            prepend-icon="mdi-format-list-checks"
                            :title="`Cap List · ${rec.label}`"
                            @click="applyAvailability(item, rec)"
                          >{{ rec.label }}</v-chip>
                          <v-chip
                            v-for="rec in partAvailability[item.partNumberId].ilsRecords"
                            :key="'ils-' + rec.condition"
                            size="small"
                            class="avail-chip avail-chip--ils cursor-pointer"
                            prepend-icon="mdi-warehouse"
                            :title="`ILS${rec.price ? ' · $' + rec.price : ''}${rec.condition ? ' · ' + rec.condition : ''}${rec.certName ? ' · ' + rec.certName : ''}`"
                            @click="applyAvailability(item, rec)"
                          >ILS<span v-if="rec.price" class="text-caption ml-1" style="opacity:0.75">${{ formatPrice(rec.price) }}</span></v-chip>
                          <v-chip
                            v-for="rec in partAvailability[item.partNumberId].fastImportRecords"
                            :key="'fast-' + rec.label"
                            size="small"
                            class="avail-chip avail-chip--fast cursor-pointer"
                            prepend-icon="mdi-flash"
                            :title="`Past record · ${rec.label}${rec.price ? ' · $' + rec.price : ''}${rec.condition ? ' · ' + rec.condition : ''}`"
                            @click="applyAvailability(item, rec)"
                          >{{ rec.label }}<span v-if="rec.price" class="text-caption ml-1" style="opacity:0.75">${{ formatPrice(rec.price) }}</span></v-chip>
                          <v-chip
                            v-for="rec in partAvailability[item.partNumberId].knownSupplierRecords"
                            :key="'sup-' + rec.label"
                            size="small"
                            class="avail-chip avail-chip--supplier cursor-pointer"
                            prepend-icon="mdi-account-outline"
                            :title="`Known supplier · ${rec.label}`"
                            @click="applyAvailability(item, rec)"
                          >{{ rec.label }}</v-chip>
                        </div>
                      </template>
                    </div>
                    <div v-else-if="getSuggestions(item.id).loading" class="mb-3">
                      <v-progress-linear indeterminate height="2" color="primary" />
                    </div>

                    <div class="quote-grid-scroll" v-if="getItemQuotes(item.id).length > 0">
                      <table class="quote-grid">
                        <thead>
                          <tr>
                            <th style="opacity:1; min-width: 160px; position: sticky; left: 0; background: #252A37; z-index: 3; border-right: 1px solid var(--card-border);">Supplier</th>
                            <th style="min-width: 80px;">Cond</th>
                            <th style="min-width: 130px;">Alt P/N</th>
                            <th style="min-width: 70px;">Qty</th>
                            <th style="min-width: 70px;">Unit</th>
                            <th style="min-width: 110px;">Cost Price ($)</th>
                            <th style="min-width: 120px;">Cert Type</th>
                            <th style="min-width: 130px;">Tag Date</th>
                            <th style="min-width: 110px;">Shipping Cost</th>
                            <th style="min-width: 120px;">Shipping Point</th>
                            
                            <th style="min-width: 80px;">LeadTime</th>
                            <th style="min-width: 160px;">Note</th>
                            <th style="min-width: 160px;">My Notes</th>
                            <th style="min-width: 60px;"></th>
                          </tr>
                        </thead>
                        <tbody>
                          <template v-for="(quote, qIdx) in getItemQuotes(item.id)" :key="qIdx">
                          <tr class="quote-row">
                            <td style="position: sticky; left: 0; background: #252A37; opacity: 1; z-index: 2; border-right: 1px solid var(--card-border);">
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
                              <input
                                type="text"
                                class="quote-input alt-input"
                                placeholder="Same P/N"
                                v-model="quote.alt"
                              />
                            </td>
                            <td>
                              <input
                                type="number"
                                class="quote-input text-center"
                                v-model.number="quote.qty"
                                min="1"
                              />
                            </td>
                            <td>
                              <select class="quote-input quote-select"  v-model="quote.unit">
                                <option value="">—</option>
                                <option value="EA">EA</option>
                                <option value="Meter">METER</option>
                                <option value="Kg">KG</option>
                              </select>
                              <!-- <input
                                type="text"
                                class="quote-input text-center"
                                placeholder="EA"
                                v-model="quote.unit"
                              /> -->
                            </td>
                            <td>
                              <input
                                v-if="focusedField === `price-${qIdx}-${item.id}`"
                                :data-focus-key="`price-${qIdx}-${item.id}`"
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
                                @click="focusField(`price-${qIdx}-${item.id}`)"
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
                              <input
                                type="date"
                                class="quote-input"
                                v-model="quote.tagDate"
                                :max="today"
                              />
                            </td>
                            <td>
                              <input
                                v-if="focusedField === `ship-${qIdx}-${item.id}`"
                                :data-focus-key="`ship-${qIdx}-${item.id}`"
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
                                @click="focusField(`ship-${qIdx}-${item.id}`)"
                              >
                                {{ quote.shippingCost ? '$' + formatPrice(quote.shippingCost) : '' }}
                              </span>
                            </td>
                            <td>
                              <input
                                type="text"
                                class="quote-input"
                                placeholder="City / Hub"
                                v-model="quote.shippingPoint"
                              />
                            </td>
                            
                            <td>
                              <input
                                type="text"
                                class="quote-input"
                                placeholder="e.g. 5 days"
                                v-model="quote.leadTime"
                              />
                            </td>
                            <td>
                              <VTextarea
                                type="text"
                                rows="2"
                                placeholder="Note..."
                                v-model="quote.note"
                              />
                            </td>
                            <td>
                              <VTextarea
                                type="text"
                                rows="2"
                                placeholder="My Notes..."
                                v-model="quote.myNotes"
                              />
                            </td>
                            <td class="text-center" style="white-space: nowrap;">
                              <v-btn
                                v-if="quote.condition === 'AR'"
                                icon="mdi-wrench"
                                size="x-small"
                                variant="text"
                                :color="isShopExpanded(quote.id || `new-${qIdx}`, item.id) ? 'warning' : 'grey'"
                                @click="toggleShop(quote.id || `new-${qIdx}`, item.id)"
                                :title="'Shops (' + (quote.shopRecords || []).length + ')'"
                              />
                              <v-btn
                                icon="mdi-close"
                                size="x-small"
                                variant="text"
                                color="error"
                                @click="removeQuote(item.id, qIdx)"
                              />
                            </td>
                          </tr>
                          <!-- Shop Records sub-table (collapsible, for AR condition) -->
                          <tr v-if="quote.condition === 'AR' && isShopExpanded(quote.id || `new-${qIdx}`, item.id)">
                            <td :colspan="14" class="pa-0">
                              <div class="shop-panel">
                                <div class="d-flex align-center justify-space-between mb-2">
                                  <span class="text-caption text-uppercase font-weight-bold" style="color: #ff9800;">
                                    <v-icon icon="mdi-wrench" size="14" class="mr-1" />
                                    Shop Records ({{ (quote.shopRecords || []).length }})
                                  </span>
                                  <div class="d-flex align-center gap-2">
                                    <v-btn
                                      v-for="otherQ in getOtherARQuotesWithShops(item.id, quote)"
                                      :key="otherQ.id"
                                      size="x-small"
                                      color="info"
                                      variant="tonal"
                                      prepend-icon="mdi-content-copy"
                                      @click="copyShopsFrom(item, quote, otherQ)"
                                      :title="`Copy all shops from ${otherQ.supplierName}`"
                                    >
                                      Copy from {{ otherQ.supplierName }}
                                    </v-btn>
                                    <v-btn
                                      size="x-small"
                                      color="warning"
                                      variant="flat"
                                      prepend-icon="mdi-plus"
                                      @click="addShopRow(item, quote)"
                                    >
                                      Add Shop
                                    </v-btn>
                                  </div>
                                </div>
                                <table class="quote-grid" v-if="(quote.shopRecords || []).length > 0" style="width: 100%; border-collapse: collapse;">
                                  <thead>
                                    <tr>
                                      <th style="opacity:1; position: sticky; left: 0; background: #252A37; z-index: 3; border-right: 1px solid var(--card-border);">Supplier</th>
                                      <th>Alt P/N</th>
                                      <th>Condition</th>
                                      <th>Qty</th>
                                      <th>Unit</th>
                                      <th>Cost Price ($)</th>
                                      <th v-if="quote.condition === 'AR'" style="color: #ff9800;">Repair Cost ($)</th>
                                      <th>Cert Type</th>
                                      <th>Tag Date</th>
                                      <th>Shipping Cost</th>
                                      <th>Shipping Point</th>
                                      <th>LeadTime</th>
                                      <th>Note</th>
                                      <th>My Notes</th>
                                      <th style="width: 70px;"></th>
                                    </tr>
                                  </thead>
                                  <tbody>
                                    <tr v-for="(shop, sIdx) in quote.shopRecords" :key="'shop-' + sIdx" class="shop-row">
                                      <td style="position: sticky; left: 0; background: #252A37; opacity: 1; z-index: 2; border-right: 1px solid var(--card-border);">
                                        <input type="text" class="quote-input" placeholder="Shop name..." v-model="shop.supplierName" @input="searchSupplier(shop.supplierName)" list="supplier-suggestions" />
                                      </td>
                                      <td><input type="text" class="quote-input" placeholder="Same P/N" v-model="shop.alt" /></td>
                                      <td>
                                        <select class="quote-input quote-select" v-model="shop.condition">
                                          <option value="">—</option>
                                          <option value="IN">IN</option>
                                          <option value="RP">RP</option>
                                          <option value="OH">OH</option>
                                        </select>
                                      </td>
                                      <td><input type="number" class="quote-input text-center" v-model.number="shop.qty" min="1" /></td>
                                      <td>
                                        <select class="quote-input quote-select" v-model="shop.unit">
                                          <option value="">—</option>
                                          <option value="EA">EA</option>
                                          <option value="Meter">METER</option>
                                          <option value="Kg">KG</option>
                                        </select>
                                      </td>
                                      <td>
                                        <input v-if="focusedField === `shop-price-${sIdx}-${quote.id}`" :data-focus-key="`shop-price-${sIdx}-${quote.id}`" type="number" class="quote-input price-input" placeholder="0.00" v-model.number="shop.price" step="0.01" min="0" @blur="focusedField = ''" />
                                        <span v-else class="quote-input price-display" @click="focusField(`shop-price-${sIdx}-${quote.id}`)">{{ shop.price ? '$' + formatPrice(shop.price) : '' }}</span>
                                      </td>
                                      <td v-if="quote.condition === 'AR'">
                                        <input v-if="focusedField === `shop-fix-${sIdx}-${quote.id}`" :data-focus-key="`shop-fix-${sIdx}-${quote.id}`" type="number" class="quote-input price-input" style="color: #ff9800;" placeholder="0.00" v-model.number="shop.fixPrice" step="0.01" min="0" @blur="focusedField = ''" />
                                        <span v-else class="quote-input price-display" style="color: #ff9800;" @click="focusField(`shop-fix-${sIdx}-${quote.id}`)">{{ shop.fixPrice ? '$' + formatPrice(shop.fixPrice) : '' }}</span>
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
                                      <td><input type="date" class="quote-input" v-model="shop.tagDate" :max="today" /></td>
                                      <td>
                                        <input v-if="focusedField === `shop-ship-${sIdx}-${quote.id}`" :data-focus-key="`shop-ship-${sIdx}-${quote.id}`" type="number" class="quote-input price-input" placeholder="0.00" v-model.number="shop.shippingCost" step="0.01" min="0" @blur="focusedField = ''" />
                                        <span v-else class="quote-input price-display" @click="focusField(`shop-ship-${sIdx}-${quote.id}`)">{{ shop.shippingCost ? '$' + formatPrice(shop.shippingCost) : '' }}</span>
                                      </td>
                                      <td><input type="text" class="quote-input" placeholder="City / Hub" v-model="shop.shippingPoint" /></td>
                                      <td><input type="text" class="quote-input" placeholder="e.g. 5 days" v-model="shop.leadTime" /></td>
                                      <td><VTextarea rows="2" placeholder="Note..." v-model="shop.note" hide-details density="compact" variant="plain" /></td>
                                      <td><VTextarea rows="2" placeholder="My Notes..." v-model="shop.myNotes" hide-details density="compact" variant="plain" /></td>
                                      <td class="text-center" style="white-space: nowrap;">
                                        <v-btn icon="mdi-close" size="x-small" variant="text" color="error" @click="removeShopQuote(item, quote, sIdx)" />
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

                    <div v-else class="empty-quotes text-center pa-6">
                      <v-icon icon="mdi-package-variant" size="32" color="grey-darken-1" class="mb-2" />
                      <p class="text-caption text-medium-emphasis">No supplier quotes yet. Click "Add Supplier" to start.</p>
                    </div>
                  </div>
                </td>
              </tr>
            </template>

            <!-- Empty State -->
            <tr v-if="!editableItems.length && !loading">
              <td :colspan="10" class="text-center pa-8">
                <v-icon icon="mdi-file-document-outline" size="48" color="grey-darken-1" class="mb-3" />
                <p class="text-body-2 text-medium-emphasis">No items in this RFQ</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </v-card>

    <!-- Shared datalist for supplier name autocomplete -->
    <datalist id="supplier-suggestions">
      <option v-for="s in supplierSuggestions" :key="s.id" :value="s.name" />
    </datalist>

    <!-- ═══════════ Add Item Dialog ═══════════ -->
    <v-dialog v-model="showAddItem" max-width="650" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-plus-circle-outline" color="warning" class="mr-2" />
          Add RFQ Item
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showAddItem = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <!-- Part Number Search -->
          <v-combobox
            v-model="addItemForm.selectedPart"
            :items="addItemPartSuggestions"
            item-title="name"
            item-value="name"
            label="Part Number *"
            prepend-inner-icon="mdi-cog-outline"
            :loading="addItemPartLoading"
            no-filter
            clearable
            return-object
            class="mb-3"
            @update:search="searchAddItemParts"
            @update:model-value="onPartSelected"
          >
            <template #item="{ item: suggestion, props: itemProps }">
              <v-list-item v-bind="itemProps">
                <template #subtitle>
                  <span v-if="suggestion.raw.description">{{ suggestion.raw.description }}</span>
                  <span v-else class="text-medium-emphasis font-italic">No description</span>
                </template>
              </v-list-item>
            </template>
            <template #no-data>
              <v-list-item>
                <v-list-item-title
                  v-if="(addItemSearchText?.length || 0) < 3"
                  class="text-medium-emphasis text-caption"
                >
                  Type 3+ chars to search...
                </v-list-item-title>
                <v-list-item-title v-else class="text-medium-emphasis text-caption">
                  "{{ addItemSearchText }}" — new part will be created
                </v-list-item-title>
              </v-list-item>
            </template>
          </v-combobox>

          <v-chip
            v-if="addItemForm.isExisting"
            size="small"
            color="success"
            variant="tonal"
            class="mb-3"
            prepend-icon="mdi-check-circle"
          >
            Existing part found in database
          </v-chip>
          <v-chip
            v-else-if="addItemForm.selectedPart && !addItemForm.isExisting"
            size="small"
            color="info"
            variant="tonal"
            class="mb-3"
            prepend-icon="mdi-information"
          >
            New part — will be created on save
          </v-chip>

          <!-- Description -->
          <v-text-field
            v-model="addItemForm.description"
            label="Description"
            prepend-inner-icon="mdi-text"
            class="mb-3"
          />

          <!-- Qty & Condition -->
          <v-row dense class="mb-3">
            <v-col cols="6">
              <v-text-field
                v-model.number="addItemForm.qty"
                label="Quantity *"
                type="number"
                min="1"
                prepend-inner-icon="mdi-counter"
              />
            </v-col>
            <v-col cols="6">
              <v-select
                v-model="addItemForm.condition"
                :items="['NE', 'OH', 'SV', 'AR','RP', 'NS','FN','IN']"
                label="Condition"
                prepend-inner-icon="mdi-tag-outline"
                clearable
              />
            </v-col>
            <v-col cols="6">
              <v-select
                v-model="addItemForm.unit"
                :items="['EA', 'Meter', 'Kg']"
                label="Unit"
                prepend-inner-icon="mdi-ruler"
                clearable
              />
            </v-col>
          </v-row>

          <!-- Alternatives -->
          <div class="mb-3">
            <p class="text-caption text-medium-emphasis mb-2">Alternatives</p>
            <div class="d-flex flex-wrap gap-1 mb-2" v-if="addItemForm.alternatives.length">
              <v-chip
                v-for="(alt, aIdx) in addItemForm.alternatives"
                :key="aIdx"
                size="small"
                color="warning"
                variant="tonal"
                closable
                @click:close="addItemForm.alternatives.splice(aIdx, 1)"
              >
                {{ alt }}
              </v-chip>
            </div>
            <div class="d-flex align-center gap-2">
              <v-text-field
                v-model="addItemAltInput"
                label="Add alternative P/N"
                density="compact"
                variant="outlined"
                hide-details
                @keydown.enter.prevent="pushAddItemAlt"
              />
              <v-btn
                icon="mdi-plus"
                size="small"
                color="warning"
                variant="tonal"
                @click="pushAddItemAlt"
              />
            </div>
          </div>

          <v-alert v-if="addItemError" type="error" density="compact" class="mt-2">
            {{ addItemError }}
          </v-alert>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showAddItem = false">Cancel</v-btn>
          <v-btn color="warning" :loading="addItemSaving" @click="submitAddItem">
            Add Item
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ═══════════ Add Alternative Dialog (inline) ═══════════ -->
    <v-dialog v-model="showAddAlt" max-width="400">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-swap-horizontal" color="warning" class="mr-2" />
          Add Alternative
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showAddAlt = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-caption text-medium-emphasis mb-3">
            Adding alternative for <strong>{{ addAltTargetItem?.partNumberName }}</strong>
          </p>
          <v-text-field
            v-model="addAltInput"
            label="Alternative Part Number"
            prepend-inner-icon="mdi-swap-horizontal"
            autofocus
            @keydown.enter.prevent="submitAddAlt"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showAddAlt = false">Cancel</v-btn>
          <v-btn color="warning" :loading="addAltSaving" @click="submitAddAlt">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Permission Dialog -->
    <v-dialog v-model="showPermissions" max-width="600" @update:model-value="(v) => !v && loadData()">
      <PermissionManager :entity-name="'RFQ'" :entity-id="route.params.id as string" />
    </v-dialog>

    <!-- Audit Dialog -->
    <v-dialog v-model="showAudit" max-width="800">
      <BusinessAuditViewer entity-name="RFQHeader" :entity-id="route.params.id as string" />
    </v-dialog>

    <RfqPdfGenerator v-model="showPdf" :rfq="rfq" />

    <!-- No Quote Confirmation -->
    <v-dialog v-model="showNoQuoteConfirm" max-width="480" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-cancel" color="deep-purple" class="mr-2" />
          Mark as No Quote?
        </v-card-title>
        <v-card-text>
          <p class="mb-3">This will set the RFQ status to <strong>No Quote</strong> and prevent any new commission from being created for it.</p>
          <v-textarea
            v-model="noQuoteReason"
            label="Reason *"
            placeholder="Why is this RFQ being marked as No Quote?"
            variant="outlined"
            density="compact"
            rows="3"
            hide-details
            auto-grow
          />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showNoQuoteConfirm = false">Cancel</v-btn>
          <v-btn color="deep-purple" variant="flat" :loading="noQuoteLoading" :disabled="!noQuoteReason?.trim()" @click="doNoQuote">Confirm</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Item Confirmation -->
    <v-dialog v-model="showDeleteItemConfirm" max-width="400">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-alert-circle-outline" color="error" class="mr-2" />
          Delete Item?
        </v-card-title>
        <v-card-text>
          Are you sure you want to delete <strong>{{ deleteItemTarget?.partNumberName }}</strong> from this RFQ? This cannot be undone.
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showDeleteItemConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" @click="doDeleteItem">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Edit Name Dialog -->
    <v-dialog v-model="editNameDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-pencil" color="primary" class="mr-2" />
          Edit RFQ Name
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="editNameDialog = false" />
        </v-card-title>
        <v-card-text class="pa-4">
          <v-text-field
            v-model="editNameForm.name"
            label="RFQ Name"
            variant="outlined"
            density="compact"
            hide-details
            autofocus
          />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="editNameDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="editNameSaving" @click="saveName">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Edit Deadline Dialog -->
    <v-dialog v-model="editDeadlineDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-calendar-edit" color="primary" class="mr-2" />
          Edit Deadline
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="editDeadlineDialog = false" />
        </v-card-title>
        <v-card-text class="pa-4">
          <v-text-field
            v-model="editDeadlineForm.leadTime"
            label="Deadline"
            type="date"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="editDeadlineDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="editDeadlineSaving" @click="saveDeadline">Save</v-btn>
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
const route = useRoute()
const api = useApi()

const today = new Date().toISOString().split('T')[0]

function isLeadTimeUrgent(dateStr: string) {
  if (!dateStr) return false
  const diff = new Date(dateStr).getTime() - Date.now()
  const daysLeft = diff / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 3
}

// State
const rfq = ref<any>({})
const existingQuoteId = ref<number | null>(null)
const editableItems = ref<any[]>([])
const supplierQuotes = ref<any[]>([])
const linkedSuppliers = ref<Record<number, { id: number; name: string }[]>>({})
const supplierSuggestions = ref<{ id: number; name: string }[]>([])
const itemSuggestions = ref<Record<number, { knownSuppliers: any[]; recentQuotes: any[]; loading: boolean }>>({})
const expandedRows = ref(new Set<number>())
const focusedField = ref('')
const loading = ref(true)

// Part availability (Inventory=green, CapList=blue, ILS=orange, FastImport=yellow, KnownSuppliers=white-gray)
const partAvailability = ref<Record<number, any>>({})
const saving = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

// Dialogs
const showPermissions = ref(false)
const showAudit = ref(false)
const showPdf = ref(false)

const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('rfq', entityId)

const exTypeOptions = [
  { value: 0, label: 'Ex Warehouse', icon: 'mdi-warehouse', color: 'success' },
  { value: 1, label: 'Ex Vendor', icon: 'mdi-truck-outline', color: 'info' },
  { value: 2, label: 'Ex Customer', icon: 'mdi-account-outline', color: 'warning' },
]

const { statusColor: getStatusColor } = useStatusColor()
const statusColor = computed(() => getStatusColor(rfq.value.status))

const assignedUsers = computed(() => {
  const views = rfq.value.views || []
  const edits = rfq.value.edits || []
  const all = [...views, ...edits]
  const unique = all.filter((u: any, i: number, arr: any[]) => arr.findIndex((x: any) => x.id === u.id) === i)
  return unique
})

const totalQuotes = computed(() => supplierQuotes.value.length)

// ──── Data Loading ────
onMounted(async () => {
  await loadData()
  await checkLock()
  await markAsRead()

  // Auto-expand item if navigated from RFQ Items page
  const targetItemId = Number(route.query.itemId)
  if (targetItemId) {
    expandedRows.value.add(targetItemId)
    nextTick(() => {
      const row = document.querySelector(`[data-item-id="${targetItemId}"]`)
      row?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    })
  }
})
function handleKeydown(e: KeyboardEvent) {
  if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 's') {
    e.preventDefault()
    saveAll()
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeydown);
});

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown);
});

async function loadData() {
  loading.value = true
  try {
    const [rfqData, quotesData] = await Promise.all([
      api.get<any>(`/rfqs/${route.params.id}`),
      api.get<any[]>(`/rfqs/${route.params.id}/supplier-quotes`)
    ])
    rfq.value = rfqData

    // Load existing quote ID if RFQ is Quoted
    if (rfqData.status === 'Quoted') {
      try {
        const quotes = await api.get<any[]>(`/quotes/by-rfq/${route.params.id}`)
        if (quotes && quotes.length > 0) {
          existingQuoteId.value = quotes[0].id
        }
      } catch {}
    }

    // Create editable copies of items
    editableItems.value = (rfqData.items || []).map((i: any) => ({
      id: i.id,
      description: i.description, 
      partNumberName: i.partNumberName,
      partNumberId: i.partNumberId,
      alt: i.alt || '',
      note: i.note,
      unit: i.unit || '',
      qty: i.qty,
      condition: i.condition || '',
      priority: i.priority || '',
      remark: i.remark || '',
      isHighlighted: i.isHighlighted || false,
      alternatives: (i.alternatives || []).map((a: any) => ({ id: a.id, name: a.name }))
    }))
    supplierQuotes.value = quotesData.map((q: any) => ({
      ...q,
      myNotes: q.myNotes || '',
      shopRecords: (q.shopRecords || []).map((s: any) => ({ ...s, _saving: false })),
    }))

    // Load linked suppliers for each unique part number
    const partIds = [...new Set(editableItems.value.map(i => i.partNumberId))]
    const supplierMap: Record<number, { id: number; name: string }[]> = {}
    await Promise.all(partIds.map(async (pid: number) => {
      try {
        const sups = await api.get<{ id: number; name: string }[]>(`/partnumbers/${pid}/suppliers`)
        supplierMap[pid] = sups || []
      } catch {
        supplierMap[pid] = []
      }
    }))
    linkedSuppliers.value = supplierMap

    // Load part availability in background (non-blocking)
    const allPartIds = editableItems.value.map(i => i.partNumberId).filter(Boolean)
    if (allPartIds.length > 0) {
      try {
        const avail = await api.post<any[]>('/availability/parts', { partNumberIds: allPartIds })
        const map: Record<number, any> = {}
        for (const a of avail) map[a.partNumberId] = a
        partAvailability.value = map
      } catch {}
    }
  } catch (e) {
    console.error('Failed to load RFQ:', e)
  } finally {
    loading.value = false
  }
}



// ──── ExType ────

async function changeExType(newType: number) {
  if (newType === rfq.value.exType) return
  try {
    await api.patch(`/rfqs/${route.params.id}/extype`, { exType: newType })
    rfq.value.exType = newType
    showSnack('ExType updated', 'success')
  } catch {
    showSnack('Failed to update ExType', 'error')
  }
}

// ──── Edit RFQ Notes ────
const isEditingNotes = ref(false)
const isSavingNotes = ref(false)
const editingNotesValue = ref('')

function editNotes() {
  editingNotesValue.value = rfq.value.notes || ''
  isEditingNotes.value = true
}

function cancelNotesEdit() {
  isEditingNotes.value = false
}

async function saveNotes() {
  isSavingNotes.value = true
  try {
    await api.patch(`/rfqs/${route.params.id}/notes`, { notes: editingNotesValue.value })
    rfq.value.notes = editingNotesValue.value
    showSnack('Notes updated successfully', 'success')
    isEditingNotes.value = false
  } catch {
    showSnack('Failed to update notes', 'error')
  } finally {
    isSavingNotes.value = false
  }
}

// ──── Highlight Toggle ────
function toggleHighlight(item: any) {
  item.isHighlighted = !item.isHighlighted
}

// ──── Unread Toggle ────
async function markAsRead() {
  if (!rfq.value.isUnread) return
  try {
    await api.patch(`/rfqs/${route.params.id}/mark-read`, {})
    rfq.value.isUnread = false
  } catch {}
}

async function toggleUnread() {
  try {
    if (rfq.value.isUnread) {
      await api.patch(`/rfqs/${route.params.id}/mark-read`, {})
      rfq.value.isUnread = false
      showSnack('Marked as read', 'success')
    } else {
      await api.patch(`/rfqs/${route.params.id}/mark-unread`, {})
      rfq.value.isUnread = true
      showSnack('Marked as unread', 'info')
    }
  } catch {
    showSnack('Failed to update read status', 'error')
  }
}

// ──── Focus Field (for formatted price inputs) ────
function focusField(key: string) {
  focusedField.value = key
  nextTick(() => {
    const input = document.querySelector(`[data-focus-key="${key}"]`) as HTMLInputElement
    input?.focus()
  })
}

// ──── Shop Records (AR condition) ────
const expandedShops = ref<Set<string>>(new Set())

function toggleShop(quoteId: any, itemId: number) {
  const key = `${quoteId}-${itemId}`
  if (expandedShops.value.has(key)) {
    expandedShops.value.delete(key)
  } else {
    expandedShops.value.add(key)
  }
  expandedShops.value = new Set(expandedShops.value)
}

function isShopExpanded(quoteId: any, itemId: number) {
  return expandedShops.value.has(`${quoteId}-${itemId}`)
}

/** Returns other AR quotes for the same RFQ item that already have shop records */
function getOtherARQuotesWithShops(itemId: number, currentQuote: any) {
  return supplierQuotes.value.filter(
    (q: any) => q.rfqItemId === itemId && q.condition === 'AR' && q.id !== currentQuote.id && (q.shopRecords || []).length > 0
  )
}

/** Copies all shop records from sourceQuote into targetQuote (IDs cleared so they save as new) */
function copyShopsFrom(item: any, targetQuote: any, sourceQuote: any) {
  if (!targetQuote.shopRecords) targetQuote.shopRecords = []
  const copies = (sourceQuote.shopRecords || []).map((s: any) => ({
    ...s,
    id: null,
    _saving: false,
    parentProcumentId: targetQuote.id,
    rfqItemId: item.id,
  }))
  targetQuote.shopRecords.push(...copies)
  const key = `${targetQuote.id || 'new'}-${item.id}`
  expandedShops.value.add(key)
  expandedShops.value = new Set(expandedShops.value)
  showSnack(`Copied ${copies.length} shop record${copies.length !== 1 ? 's' : ''} from ${sourceQuote.supplierName}`, 'info')
}

function addShopRow(item: any, parentQuote: any) {
  if (!parentQuote.shopRecords) parentQuote.shopRecords = []
  parentQuote.shopRecords.push({
    id: null,
    rfqItemId: item.id,
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
  const key = `${parentQuote.id || 'new'}-${item.id}`
  expandedShops.value.add(key)
  expandedShops.value = new Set(expandedShops.value)
}

async function removeShopQuote(item: any, parentQuote: any, sIdx: number) {
  const shop = parentQuote.shopRecords[sIdx]
  if (shop.id) {
    try {
      await api.del(`/rfqs/${route.params.id}/supplier-quotes/${shop.id}`)
      showSnack('Shop record removed', 'success')
    } catch {
      showSnack('Failed to remove shop record', 'error')
      return
    }
  }
  parentQuote.shopRecords.splice(sIdx, 1)
}

// ──── Quote Management ────

function getItemQuotes(itemId: number) {
  return supplierQuotes.value.filter(q => q.rfqItemId === itemId)
}

function getQuoteCount(itemId: number) {
  return supplierQuotes.value.filter(q => q.rfqItemId === itemId).length
}

function getLinkedSuppliers(partNumberId: number) {
  return linkedSuppliers.value[partNumberId] || []
}

async function loadItemSuggestions(item: any) {
  const key = item.id
  if (itemSuggestions.value[key] && !itemSuggestions.value[key].loading) return
  itemSuggestions.value[key] = { knownSuppliers: [], recentQuotes: [], loading: true }
  try {
    const data = await api.get<any>(`/procument-page/suggestions?partNumberId=${item.partNumberId}&rfqId=${route.params.id}`)
    itemSuggestions.value[key] = {
      knownSuppliers: data.knownSuppliers || [],
      recentQuotes: data.recentQuotes || [],
      loading: false,
    }
  } catch {
    itemSuggestions.value[key] = { knownSuppliers: [], recentQuotes: [], loading: false }
  }
}

function getSuggestions(itemId: number) {
  return itemSuggestions.value[itemId] || { knownSuppliers: [], recentQuotes: [], loading: false }
}

function applySuggestion(item: any, suggestion: any) {
  const alreadyExists = supplierQuotes.value.some(
    (q: any) => q.rfqItemId === item.id && q.supplierName?.toLowerCase() === suggestion.supplierName?.toLowerCase()
  )
  if (alreadyExists) return
  supplierQuotes.value.push({
    id: null,
    rfqItemId: item.id,
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
  })
}

// Apply an availability record (Inventory / CapList / ILS / FastImport / KnownSupplier) as a new quote row
function applyAvailability(item: any, rec: any) {
  // Expand the row so the user sees the new entry
  if (!expandedRows.value.has(item.id)) expandedRows.value.add(item.id)

  supplierQuotes.value.push({
    id: null,
    rfqItemId: item.id,
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
    shopRecords: [],
  })
}

function applyAllSuggestions(item: any) {
  const suggestions = getSuggestions(item.id)
  for (const s of suggestions.recentQuotes) {
    applySuggestion(item, s)
  }
}

function addQuoteRowWithSupplier(itemId: number, supplierName: string, qty: number) {
  supplierQuotes.value.push({
    id: null,
    rfqItemId: itemId,
    supplierName,
    qty: qty || 1,
    price: 0,
    condition: 'NE',
    alt: '',
    certName: '',
    tagDate: '',
    shippingCost: null,
    shippingPoint: '',
    unit: '-',
    leadTime: '',
    note: '',
    myNotes: '',
  })
}

function addQuoteRow(itemId: number) {
  const item = editableItems.value.find(i => i.id === itemId)
  supplierQuotes.value.push({
    id: null,
    rfqItemId: itemId,
    supplierName: '',
    qty: 0,
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
  })
}

async function removeQuote(itemId: number, qIdx: number) {
  const itemQuotes = getItemQuotes(itemId)
  const quote = itemQuotes[qIdx]

  if (quote.id) {
    try {
      await api.del(`/rfqs/${route.params.id}/supplier-quotes/${quote.id}`)
      showSnack('Quote removed', 'success')
    } catch (e) {
      showSnack('Failed to remove quote', 'error')
      return
    }
  }

  const globalIdx = supplierQuotes.value.indexOf(quote)
  if (globalIdx > -1) supplierQuotes.value.splice(globalIdx, 1)
}

// ──── No Quote ────
const showNoQuoteConfirm = ref(false)
const noQuoteLoading = ref(false)
const noQuoteReason = ref('')

async function doNoQuote() {
  noQuoteLoading.value = true
  try {
    await api.patch(`/rfqs/${route.params.id}/status`, { status: 'No Quote', noQuoteReason: noQuoteReason.value.trim() })
    rfq.value.status = 'No Quote'
    rfq.value.noQuoteReason = noQuoteReason.value.trim()
    showNoQuoteConfirm.value = false
    noQuoteReason.value = ''
    showSnack('RFQ marked as No Quote', 'success')
  } catch {
    showSnack('Failed to update status', 'error')
  } finally {
    noQuoteLoading.value = false
  }
}

// ──── Delete RFQ Item ────
const showDeleteItemConfirm = ref(false)
const deleteItemTarget = ref<any>(null)

function confirmDeleteItem(item: any) {
  deleteItemTarget.value = item
  showDeleteItemConfirm.value = true
}

async function doDeleteItem() {
  if (!deleteItemTarget.value) return
  try {
    await api.del(`/rfqs/items/${deleteItemTarget.value.id}`)
    showSnack('Item deleted', 'success')
    await loadData()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to delete item', 'error')
  } finally {
    showDeleteItemConfirm.value = false
    deleteItemTarget.value = null
  }
}

// ──── Edit Name ────

async function saveName() {
  if (!editNameForm.value.name.trim()) return
  editNameSaving.value = true
  try {
    await api.patch(`/rfqs/${route.params.id}/name`, { name: editNameForm.value.name })
    rfq.value.name = editNameForm.value.name
    showSnack('RFQ name updated', 'success')
    editNameDialog.value = false
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to update name', 'error')
  } finally {
    editNameSaving.value = false
  }
}

// ──── Edit Deadline ────

async function saveDeadline() {
  if (!editDeadlineForm.value.leadTime) return
  editDeadlineSaving.value = true
  try {
    const dateValue = typeof editDeadlineForm.value.leadTime === 'string'
      ? new Date(editDeadlineForm.value.leadTime)
      : editDeadlineForm.value.leadTime
    await api.patch(`/rfqs/${route.params.id}/leadtime`, { leadTime: dateValue.toISOString() })
    rfq.value.leadTime = dateValue.toISOString()
    showSnack('Deadline updated', 'success')
    editDeadlineDialog.value = false
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to update deadline', 'error')
  } finally {
    editDeadlineSaving.value = false
  }
}

// ──── Supplier Name Autocomplete ────
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

// ──── Save All (items + quotes) ────

async function saveAll() {
  saving.value = true
  try {
    // 1. Save all editable item fields + fleet/remark on part numbers
    const itemPromises = editableItems.value.flatMap(item => {
      const promises: Promise<any>[] = []
      promises.push(api.put(`/rfqs/items/${item.id}`, {
        alt: item.alt || null,
        priority: item.priority || "noraml",
        note: item.note,
        qty: item.qty,
        condition: item.condition || null,
        unit: item.unit || null,
        isHighlighted: item.isHighlighted || false
      }))
      // Save fleet/remark on the part number
      if ( item.remark) {
        promises.push(api.put(`/partnumbers/${item.partNumberId}`, {
          name: item.partNumberName,
          description: item.description || null,
          remark: item.remark || null,
          supplierId: null,
        }))
      }
      return promises
    })
    await Promise.all(itemPromises)

    // 2. Save all supplier quotes (parent records first)
    const quotesToSave = supplierQuotes.value
      .filter(q => q.supplierName?.trim())
      .map(q => ({
        id: q.id || null,
        rfqItemId: q.rfqItemId,
        supplierName: q.supplierName,
        qty: q.qty,
        price: q.price,
        condition: q.condition,
        alt: q.alt,
        certName: q.certName || null,
        tagDate: q.tagDate || null,
        shippingCost: q.shippingCost ?? null,
        shippingPoint: q.shippingPoint || null,
        unit: q.unit || null,
        leadTime: q.leadTime || null,
        note: q.note || null,
        myNotes: q.myNotes || null,
        type: q.type || 'Procument',
      }))

    if (quotesToSave.length > 0) {
      await api.post(
        `/rfqs/${route.params.id}/supplier-quotes/bulk`,
        { quotes: quotesToSave }
      )
    }

    // 2b. Reload to get parent IDs, then save shop records
    const refreshedQuotes = await api.get<any[]>(`/rfqs/${route.params.id}/supplier-quotes`)
    // Match shop records from local state to saved parents
    const shopQuotesToSave: any[] = []
    for (const q of supplierQuotes.value) {
      if (!q.shopRecords?.length) continue
      // Find saved parent by matching supplier + rfqItemId
      const savedParent = (refreshedQuotes || []).find((r: any) =>
        r.rfqItemId === q.rfqItemId && r.supplierName === q.supplierName && (r.type ?? 'Procument') !== 'Shop'
      )
      if (!savedParent) continue
      for (const shop of q.shopRecords) {
        if (!shop.supplierName?.trim()) continue
        shopQuotesToSave.push({
          id: shop.id || null,
          rfqItemId: q.rfqItemId,
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
          type: 'Shop',
          parentProcumentId: savedParent.id,
        })
      }
    }
    if (shopQuotesToSave.length > 0) {
      await api.post(
        `/rfqs/${route.params.id}/supplier-quotes/bulk`,
        { quotes: shopQuotesToSave }
      )
    }

    // Reload
    await loadData()
    showSnack('All changes saved successfully', 'success')
  } catch (e) {
    showSnack('Failed to save changes', 'error')
  } finally {
    saving.value = false
  }
}

// ──── Add Item Dialog ────

const showAddItem = ref(false)
const addItemSaving = ref(false)
const addItemError = ref('')
const addItemAltInput = ref('')
const addItemSearchText = ref('')
const addItemPartSuggestions = ref<any[]>([])
const addItemPartLoading = ref(false)
let addItemPartDebounce: any = null

const addItemForm = ref({
  selectedPart: null as any,
  description: '',
  qty: 1,
  condition: '' as string,
  unit: '' as string,
  alternatives: [] as string[],
  isExisting: false,
})

function openAddItemDialog() {
  addItemForm.value = {
    selectedPart: null,
    description: '',
    qty: 1,
    condition: '',
    unit: '',
    alternatives: [],
    isExisting: false,
  }
  addItemAltInput.value = ''
  addItemError.value = ''
  addItemPartSuggestions.value = []
  addItemSearchText.value = ''
  showAddItem.value = true
}

function searchAddItemParts(val: string) {
  addItemSearchText.value = val || ''
  clearTimeout(addItemPartDebounce)
  if (!val || val.length < 3) {
    addItemPartSuggestions.value = []
    return
  }
  addItemPartDebounce = setTimeout(async () => {
    addItemPartLoading.value = true
    try {
      addItemPartSuggestions.value = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(val)}`)
    } catch {}
    finally { addItemPartLoading.value = false }
  }, 300)
}

function onPartSelected(val: any) {
  if (!val) {
    addItemForm.value.isExisting = false
    addItemForm.value.description = ''
    addItemForm.value.alternatives = []
    return
  }
  if (typeof val === 'object' && val.id) {
    // Existing part selected from search results
    addItemForm.value.isExisting = true
    addItemForm.value.description = val.description || ''
    addItemForm.value.alternatives = (val.alternatives || []).map((a: any) => a.name)
  } else {
    // User typed a new part name
    addItemForm.value.isExisting = false
    addItemForm.value.description = ''
    addItemForm.value.alternatives = []
  }
}

function pushAddItemAlt() {
  const val = addItemAltInput.value.trim()
  if (!val) return
  if (!addItemForm.value.alternatives.includes(val)) {
    addItemForm.value.alternatives.push(val)
  }
  addItemAltInput.value = ''
}

async function submitAddItem() {
  addItemError.value = ''

  const partName = typeof addItemForm.value.selectedPart === 'object'
    ? addItemForm.value.selectedPart?.name
    : addItemForm.value.selectedPart

  if (!partName?.trim()) {
    addItemError.value = 'Part Number is required.'
    return
  }
  if (!addItemForm.value.qty || addItemForm.value.qty < 1) {
    addItemError.value = 'Quantity must be at least 1.'
    return
  }

  addItemSaving.value = true
  try {
    await api.post(`/rfqs/${route.params.id}/items`, {
      partNumberName: partName.trim(),
      description: addItemForm.value.description || null,
      qty: addItemForm.value.qty,
      condition: addItemForm.value.condition || null,
      unit: addItemForm.value.unit || null,
      alt: null,
      alternatives: addItemForm.value.alternatives,
    })
    showAddItem.value = false
    await loadData()
    showSnack('Item added successfully', 'success')
  } catch (e: any) {
    addItemError.value = e?.data?.message || 'Failed to add item.'
  } finally {
    addItemSaving.value = false
  }
}

// ──── Add Alternative (on existing items) ────

const showAddAlt = ref(false)
const addAltInput = ref('')
const addAltSaving = ref(false)
const addAltTargetItem = ref<any>(null)

// ──── Edit Name Dialog ────

const editNameDialog = ref(false)
const editNameSaving = ref(false)
const editNameForm = ref({ name: '' })

// ──── Edit Deadline Dialog ────

const editDeadlineDialog = ref(false)
const editDeadlineSaving = ref(false)
const editDeadlineForm = ref({ leadTime: '' as any })

// ──── Edit Name Watch ────

watch(editNameDialog, (open) => {
  if (open) {
    editNameForm.value.name = rfq.value.name || ''
  }
})

// ──── Edit Deadline Watch ────

watch(editDeadlineDialog, (open) => {
  if (open) {
    editDeadlineForm.value.leadTime = rfq.value.leadTime ? new Date(rfq.value.leadTime).toISOString().split('T')[0] : new Date().toISOString().split('T')[0]
  }
})

function openAddAlt(item: any) {
  addAltTargetItem.value = item
  addAltInput.value = ''
  showAddAlt.value = true
}

async function submitAddAlt() {
  const val = addAltInput.value.trim()
  if (!val || !addAltTargetItem.value) return

  addAltSaving.value = true
  try {
    const result = await api.post<any>(
      `/partnumbers/${addAltTargetItem.value.partNumberId}/alternatives`,
      { name: val }
    )
    // Update local state
    addAltTargetItem.value.alternatives.push({ id: result.id, name: result.name })
    showAddAlt.value = false
    showSnack('Alternative added', 'success')
  } catch (e) {
    showSnack('Failed to add alternative', 'error')
  } finally {
    addAltSaving.value = false
  }
}

async function removeAlternative(item: any, alt: any) {
  try {
    await api.del(`/partnumbers/${item.partNumberId}/alternatives/${alt.id}`)
    item.alternatives = item.alternatives.filter((a: any) => a.id !== alt.id)
    showSnack('Alternative removed', 'success')
  } catch (e) {
    showSnack('Failed to remove alternative', 'error')
  }
}

// ──── Copy Part Numbers ────

function copyToClipboard(text: string) {
  if (navigator.clipboard?.writeText) {
    navigator.clipboard.writeText(text)
  } else {
    const ta = document.createElement('textarea')
    ta.value = text
    ta.style.position = 'fixed'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.select()
    document.execCommand('copy')
    document.body.removeChild(ta)
  }
}

function copyPartNumber(pn: string) {
  copyToClipboard(pn)
  showSnack(`Copied: ${pn}`, 'success')
}

function copyAllPartNumbers() {
  const allPNs = editableItems.value.map(i => i.partNumberName).join('\n')
  copyToClipboard(allPNs)
  showSnack(`Copied ${editableItems.value.length} part numbers`, 'success')
}

// ──── Helpers ────

function toggleExpand(itemId: number) {
  if (expandedRows.value.has(itemId)) {
    expandedRows.value.delete(itemId)
  } else {
    expandedRows.value.add(itemId)
    const item = editableItems.value.find(i => i.id === itemId)
    if (item) loadItemSuggestions(item)
  }
  expandedRows.value = new Set(expandedRows.value)
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>

<style scoped>
.rfq-single-view {
  max-width: 100%;
}

/* Info Cards */
.info-card {
  background: var(--card-bg) !important;
  border: 1px solid var(--card-border) !important;
  backdrop-filter: blur(8px);
  transition: border-color 0.2s;
}
.info-card:hover {
  border-color: var(--card-hover-border) !important;
}

/* Excel Card */
.excel-card {
  background: var(--excel-bg) !important;
  border: 1px solid var(--excel-border) !important;
  overflow: hidden;
}

.excel-toolbar {
  border-bottom: 1px solid var(--toolbar-border);
  background: var(--toolbar-bg);
}

.excel-container {
  overflow-x: auto;
}

/* Excel Grid */
.excel-grid {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
  min-width: 900px;
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
  position: sticky;
  top: 0;
  z-index: 2;
  white-space: nowrap;
}

.excel-grid tbody td {
  padding: 0 12px;
  height: 42px;
  border-bottom: 1px solid var(--card-border);
  font-size: 13px;
  vertical-align: middle;
}

/* Master Row */
.master-row {
  transition: background-color 0.15s;
  cursor: default;
}
.master-row:hover {
  background: var(--row-hover);
}
.master-row.expanded {
  background: var(--toolbar-bg);
  border-bottom: none;
}
.master-row.highlighted-row {
  background: rgba(251, 191, 36, 0.12) !important;
  border-left: 3px solid #fbbf24;
}
.master-row.highlighted-row:hover {
  background: rgba(251, 191, 36, 0.18) !important;
}
.highlight-pen {
  opacity: 0.4;
  transition: opacity 0.15s, transform 0.15s;
}
.highlight-pen:hover {
  opacity: 1;
  transform: scale(1.15);
}
.highlighted-row .highlight-pen {
  opacity: 1;
}

.cell-expand {
  text-align: center;
  cursor: pointer;
  transition: background-color 0.15s;
}
.cell-expand:hover {
  background: var(--cell-hover);
}

.cell-number {
  text-align: center;
  opacity: 0.5;
  font-size: 12px;
}

.cell-pn {
  color: var(--pn-color);
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
}

.cell-copyable {
  cursor: pointer;
  user-select: none;
  transition: background 0.15s;
}
.cell-copyable:hover {
  background: var(--cell-hover);
}
.cell-copyable .copy-icon {
  opacity: 0;
  transition: opacity 0.15s;
}
.cell-copyable:hover .copy-icon {
  opacity: 0.6;
}

.cell-alternatives {
  padding: 4px 12px !important;
}

.cell-status {
  font-size: 12px;
  font-style: italic;
}

/* Editable item inputs (master row) */
.item-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 8px;
  font-size: 12px;
  border-radius: 4px;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.item-input:hover {
  border-color: var(--card-border);
}
.item-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px var(--card-hover-border);
}
.item-input::placeholder {
  opacity: 0.4;
}
.item-select {
  cursor: pointer;
  appearance: auto;
}

/* Detail Row */
.detail-row {
  animation: slideDown 0.2s ease-out;
}
.detail-cell {
  padding: 0 !important;
  background: var(--toolbar-bg);
  border-bottom: 2px solid var(--card-hover-border) !important;
}

@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Quote Panel */
.quote-panel {
  padding: 16px 20px 16px 56px;
  border-left: 3px solid #3b82f6;
  margin-left: 20px;
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

.shop-panel {
  background: rgba(255, 152, 0, 0.04);
  border-left: 3px solid #ff9800;
  margin-left: 20px;
  padding: 10px 16px;
}
.shop-row td {
  background: rgba(255, 152, 0, 0.02);
}

@media (max-width: 960px) {
  .quote-panel {
    padding: 12px 8px 12px 12px;
    margin-left: 0;
  }
}

.quote-grid-scroll {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  max-width: 100%;
  scrollbar-width: auto;
  scrollbar-color: var(--card-border) #252A37;
}

.quote-grid-scroll::-webkit-scrollbar {
  height: 10px;
}

.quote-grid-scroll::-webkit-scrollbar-track {
  background: #252A37;
  border-radius: 5px;
}

.quote-grid-scroll::-webkit-scrollbar-thumb {
  background: var(--card-border);
  border-radius: 5px;
}

.quote-grid-scroll::-webkit-scrollbar-thumb:hover {
  background: var(--row-hover);
}

.letter-spacing-wide {
  letter-spacing: 0.1em;
}

/* Quote Sub-Grid */
.quote-grid {
  width: 100%;
  border-collapse: collapse;
  min-width: 1600px;
}

.quote-grid thead th {
  opacity: 0.6;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 6px 8px;
  border-bottom: 1px solid var(--card-border);
  text-align: left;
  white-space: nowrap;
}

.quote-grid tbody td {
  padding: 3px 4px;
  border-bottom: 1px solid var(--card-border);
}

.quote-row {
  transition: background-color 0.15s;
}
.quote-row:hover {
  background: var(--row-hover);
}

/* Quote Inputs — Excel-like cells */
.quote-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: var(--row-hover);
  color: rgb(var(--v-theme-on-surface));
  padding: 4px 8px;
  font-size: 12px;
  border-radius: 4px;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.quote-input:hover {
  border-color: var(--card-border);
}
.quote-input:focus {
  background: var(--toolbar-bg);
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px var(--card-hover-border);
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

.alt-input {
  color: #fbbf24;
}

.text-center {
  text-align: center;
}

.empty-quotes {
  border: 1px dashed var(--card-border);
  border-radius: 8px;
}

/* Availability chips */
.avail-chip {
  font-size: 10px !important;
  height: 18px !important;
  border-radius: 4px !important;
  transition: transform 0.1s, opacity 0.1s;
}
.avail-chip:hover { transform: scale(1.07); opacity: 0.9; }
.avail-chip:active { transform: scale(0.96); }

.avail-chip--inventory {
  background: rgba(34, 197, 94, 0.12) !important;
  color: #22c55e !important;
  border: 1px solid rgba(34, 197, 94, 0.35) !important;
}
.avail-chip--caplist {
  background: rgba(59, 130, 246, 0.12) !important;
  color: #60a5fa !important;
  border: 1px solid rgba(59, 130, 246, 0.35) !important;
}
.avail-chip--ils {
  background: rgba(249, 115, 22, 0.12) !important;
  color: #fb923c !important;
  border: 1px solid rgba(249, 115, 22, 0.35) !important;
}
.avail-chip--fast {
  background: rgba(234, 179, 8, 0.12) !important;
  color: #facc15 !important;
  border: 1px solid rgba(234, 179, 8, 0.35) !important;
}
.avail-chip--supplier {
  background: rgba(148, 163, 184, 0.10) !important;
  color: #94a3b8 !important;
  border: 1px solid rgba(148, 163, 184, 0.25) !important;
}
</style>
