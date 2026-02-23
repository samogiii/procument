<template>
  <div class="rfq-single-view">
    <!-- Header Bar -->
    <div class="d-flex align-center mb-4 flex-wrap gap-2">
      <v-btn icon="mdi-arrow-left" variant="text" to="/rfqs" class="mr-1 flex-shrink-0" size="small" />
      <div class="min-width-0">
        <h1 class="text-h6 text-sm-h5 font-weight-bold d-flex align-center gap-2">
          RFQ #{{ route.params.id }}
          <v-chip :color="statusColor" size="small" class="ml-1">{{ rfq.status || 'Open' }}</v-chip>
        </h1>
        <p class="text-caption text-medium-emphasis mt-1 text-truncate" v-if="rfq.name">{{ rfq.name }}</p>
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
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.customerName || '—' }}</p>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="info" variant="tonal" size="40">
              <v-icon icon="mdi-clock-outline" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Lead Time</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.leadTime ? new Date(rfq.leadTime).toLocaleDateString() : '—' }}</p>
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
              <p class="text-caption text-medium-emphasis mb-0">Assigned To</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.userName || '—' }}</p>
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
      <v-col cols="12" md="9" v-if="rfq.notes">
        <v-card class="info-card pa-4">
          <div class="d-flex align-center gap-3">
            <v-avatar color="info" variant="tonal" size="40">
              <v-icon icon="mdi-note-text-outline" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Notes</p>
              <p class="text-body-2 font-weight-medium mb-0">{{ rfq.notes }}</p>
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
          <v-btn prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
          <v-btn
            size="small"
            variant="tonal"
            color="warning"
            prepend-icon="mdi-plus-circle-outline"
            @click="openAddItemDialog"
          >
            Add Item
          </v-btn>
          <v-btn
            size="small"
            variant="tonal"
            color="success"
            prepend-icon="mdi-plus"
            :to="`/rfqs/${route.params.id}/create-quote`"
          >
            Add Quote
          </v-btn>
          <v-btn
            size="small"
            variant="tonal"
            color="primary"
            prepend-icon="mdi-content-save"
            :loading="saving"
            @click="saveAll"
          >
            Save All
          </v-btn>
        </div>
      </div>

      <div class="excel-container">
        <table class="excel-grid">
          <thead>
            <tr>
              <th style="min-width: 60px; text-align: center;"></th>
              <th style="min-width: 50px; text-align: center;">#</th>
              <th style="min-width: 180px;">Part Number</th>
              <th style="min-width: 180px;">Description</th>
              <th style="min-width: 80px;">Qty</th>
              <th style="min-width: 100px;">Condition</th>
              <th style="min-width: 120px;">Priority</th>
              <th style="min-width: 140px;">Remark</th>
              <th style="min-width: 200px;">Alternatives</th>
              <th style="min-width: 120px;">Procurements</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(item, idx) in editableItems" :key="item.id">
              <!-- Master Row — editable fields -->
              <tr class="master-row" :class="{ 'expanded': expandedRows.has(item.id) }">
                <td class="cell-expand" @click="toggleExpand(item.id)">
                  <v-icon
                    :icon="expandedRows.has(item.id) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                    size="18"
                    :color="expandedRows.has(item.id) ? 'primary' : 'grey'"
                  />
                </td>
                <td class="cell-number">{{ idx + 1 }}</td>
                <td class="cell-pn">{{ item.partNumberName }}</td>
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
                <td class="cell-status">
                  <span :class="getQuoteCount(item.id) > 0 ? 'text-success' : 'text-medium-emphasis'">
                    {{ getQuoteCount(item.id) }} supplier{{ getQuoteCount(item.id) !== 1 ? 's' : '' }}
                  </span>
                </td>
              </tr>

              <!-- Expanded Detail Row -->
              <tr v-if="expandedRows.has(item.id)" class="detail-row">
                <td :colspan="10" class="detail-cell">
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

                    <!-- Linked Suppliers (from junction table) -->
                    <!-- <div v-if="getLinkedSuppliers(item.partNumberId).length > 0" class="mb-3 d-flex flex-wrap align-center gap-1">
                      <span class="text-caption text-medium-emphasis mr-1">Known suppliers:</span>
                      <v-chip
                        v-for="sup in getLinkedSuppliers(item.partNumberId)"
                        :key="sup.id"
                        size="small"
                        color="teal"
                        variant="tonal"
                        prepend-icon="mdi-truck-outline"
                        class="cursor-pointer"
                        @click="addQuoteRowWithSupplier(item.id, sup.name, item.qty)"
                      >
                        {{ sup.name }}
                      </v-chip>
                    </div> -->

                    <div class="quote-grid-scroll" v-if="getItemQuotes(item.id).length > 0">
                      <table class="quote-grid">
                        <thead>
                          <tr>
                            <th style="min-width: 160px;">Supplier</th>
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

                            <th style="min-width: 60px;"></th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr v-for="(quote, qIdx) in getItemQuotes(item.id)" :key="qIdx" class="quote-row">
                            <td style="position: relative;">
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
                                type="number"
                                class="quote-input price-input"
                                placeholder="0.00"
                                v-model.number="quote.price"
                                step="0.01"
                                min="0"
                              />
                            </td>
                            <td>
                              <select class="quote-input quote-select" v-model="quote.certName">
                                <option value="">—</option>
                                <option value="FAA">FAA</option>
                                <option value="EASA">EASA</option>
                                <option value="CAAC">CAAC</option>
                                <option value="Dual">Dual</option>
                                <option value="No Cert">No Cert</option>
                              </select>
                            </td>
                            <td>
                              <input
                                type="date"
                                class="quote-input"
                                v-model="quote.tagDate"
                              />
                            </td>
                            <td>
                              <input
                                type="number"
                                class="quote-input price-input"
                                placeholder="0.00"
                                v-model.number="quote.shippingCost"
                                step="0.01"
                                min="0"
                              />
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
                            <td class="text-center">
                              <v-btn
                                icon="mdi-close"
                                size="x-small"
                                variant="text"
                                color="error"
                                @click="removeQuote(item.id, qIdx)"
                              />
                            </td>
                          </tr>
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
    <v-dialog v-model="showPermissions" max-width="600">
      <PermissionManager :entity-name="'RFQ'" :entity-id="route.params.id as string" />
    </v-dialog>

    <!-- Audit Dialog -->
    <v-dialog v-model="showAudit" max-width="700">
      <AuditLogViewer :entity-name="'RFQ'" :entity-id="route.params.id as string" />
    </v-dialog>

    <RfqPdfGenerator v-model="showPdf" :rfq="rfq" />

    <!-- Snackbar -->
    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()

// State
const rfq = ref<any>({})
const editableItems = ref<any[]>([])
const supplierQuotes = ref<any[]>([])
const linkedSuppliers = ref<Record<number, { id: number; name: string }[]>>({})
const supplierSuggestions = ref<{ id: number; name: string }[]>([])
const expandedRows = ref(new Set<number>())
const loading = ref(true)
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

const statusColor = computed(() => {
  const s = rfq.value.status?.toLowerCase()
  if (s === 'closed') return 'success'
  if (s === 'cancelled') return 'error'
  return 'primary'
})

const totalQuotes = computed(() => supplierQuotes.value.length)

// ──── Data Loading ────

onMounted(async () => {
  await loadData()
})

async function loadData() {
  loading.value = true
  try {
    const [rfqData, quotesData] = await Promise.all([
      api.get<any>(`/rfqs/${route.params.id}`),
      api.get<any[]>(`/rfqs/${route.params.id}/supplier-quotes`)
    ])
    rfq.value = rfqData
    // Create editable copies of items
    editableItems.value = (rfqData.items || []).map((i: any) => ({
      id: i.id,
      description: i.description, 
      partNumberName: i.partNumberName,
      partNumberId: i.partNumberId,
      alt: i.alt || '',
      qty: i.qty,
      condition: i.condition || '',
      priority: i.priority || '',
      remark: i.remark || '',
      alternatives: (i.alternatives || []).map((a: any) => ({ id: a.id, name: a.name }))
    }))
    supplierQuotes.value = quotesData.map((q: any) => ({ ...q }))

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
  } catch (e) {
    console.error('Failed to load RFQ:', e)
  } finally {
    loading.value = false
  }
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
        qty: item.qty,
        condition: item.condition || null
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

    // 2. Save all supplier quotes
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
      }))

    if (quotesToSave.length > 0) {
      await api.post(
        `/rfqs/${route.params.id}/supplier-quotes/bulk`,
        { quotes: quotesToSave }
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
  alternatives: [] as string[],
  isExisting: false,
})

function openAddItemDialog() {
  addItemForm.value = {
    selectedPart: null,
    description: '',
    qty: 1,
    condition: '',
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

// ──── Helpers ────

function toggleExpand(itemId: number) {
  if (expandedRows.value.has(itemId)) {
    expandedRows.value.delete(itemId)
  } else {
    expandedRows.value.add(itemId)
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
  background: rgba(30, 41, 59, 0.6) !important;
  border: 1px solid rgba(51, 65, 85, 0.5) !important;
  backdrop-filter: blur(8px);
  transition: border-color 0.2s;
}
.info-card:hover {
  border-color: rgba(59, 130, 246, 0.3) !important;
}

/* Excel Card */
.excel-card {
  background: rgba(22, 27, 34, 0.9) !important;
  border: 1px solid rgba(51, 65, 85, 0.6) !important;
  overflow: hidden;
}

.excel-toolbar {
  border-bottom: 1px solid rgba(51, 65, 85, 0.6);
  background: rgba(30, 41, 59, 0.4);
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
  background: rgba(30, 41, 59, 0.8);
  color: #94a3b8;
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 12px;
  border-bottom: 2px solid rgba(51, 65, 85, 0.8);
  text-align: left;
  position: sticky;
  top: 0;
  z-index: 2;
  white-space: nowrap;
}

.excel-grid tbody td {
  padding: 0 12px;
  height: 42px;
  border-bottom: 1px solid rgba(51, 65, 85, 0.3);
  font-size: 13px;
  vertical-align: middle;
}

/* Master Row */
.master-row {
  transition: background-color 0.15s;
  cursor: default;
}
.master-row:hover {
  background: rgba(30, 41, 59, 0.4);
}
.master-row.expanded {
  background: rgba(30, 41, 59, 0.6);
  border-bottom: none;
}

.cell-expand {
  text-align: center;
  cursor: pointer;
  transition: background-color 0.15s;
}
.cell-expand:hover {
  background: rgba(59, 130, 246, 0.1);
}

.cell-number {
  text-align: center;
  color: #64748b;
  font-size: 12px;
}

.cell-pn {
  color: #60a5fa;
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
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
  background: rgba(15, 23, 42, 0.3);
  color: #e2e8f0;
  padding: 4px 8px;
  font-size: 12px;
  border-radius: 4px;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.item-input:hover {
  border-color: rgba(51, 65, 85, 0.5);
}
.item-input:focus {
  background: rgba(15, 23, 42, 0.7);
  border-color: #3b82f6;
  box-shadow: 0 0 0 1px rgba(59, 130, 246, 0.3);
}
.item-input::placeholder {
  color: #475569;
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
  background: rgba(15, 23, 42, 0.6);
  border-bottom: 2px solid rgba(59, 130, 246, 0.3) !important;
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

@media (max-width: 960px) {
  .quote-panel {
    padding: 12px 8px 12px 12px;
    margin-left: 0;
  }
}

.quote-grid-scroll {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.letter-spacing-wide {
  letter-spacing: 0.1em;
}

/* Quote Sub-Grid */
.quote-grid {
  width: 100%;
  border-collapse: collapse;
  min-width: 800px;
}

.quote-grid thead th {
  color: #64748b;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 6px 8px;
  border-bottom: 1px solid rgba(51, 65, 85, 0.5);
  text-align: left;
  white-space: nowrap;
}

.quote-grid tbody td {
  padding: 3px 4px;
  border-bottom: 1px solid rgba(51, 65, 85, 0.2);
}

.quote-row {
  transition: background-color 0.15s;
}
.quote-row:hover {
  background: rgba(51, 65, 85, 0.2);
}

/* Quote Inputs — Excel-like cells */
.quote-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  background: rgba(15, 23, 42, 0.4);
  color: #e2e8f0;
  padding: 4px 8px;
  font-size: 12px;
  border-radius: 4px;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.quote-input:hover {
  border-color: rgba(51, 65, 85, 0.6);
}
.quote-input:focus {
  background: rgba(15, 23, 42, 0.8);
  border-color: #3b82f6;
  box-shadow: 0 0 0 1px rgba(59, 130, 246, 0.3);
}
.quote-input::placeholder {
  color: #475569;
  font-style: italic;
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
  border: 1px dashed rgba(51, 65, 85, 0.5);
  border-radius: 8px;
}
</style>
