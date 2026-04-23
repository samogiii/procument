<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/purchase-orders" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">PO {{ po.poNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <v-menu :disabled="isLocked">
        <template #activator="{ props: menuProps }">
          <v-chip
            :color="poStatusColor"
            v-bind="menuProps"
            class="cursor-pointer"
            :append-icon="isLocked ? 'mdi-lock' : 'mdi-chevron-down'"
            size="default"
          >
            {{ po.status || '—' }}
          </v-chip>
        </template>
        <v-list density="compact" style="min-width: 200px">
          <v-list-subheader>Change Status</v-list-subheader>
          <v-list-item
            v-for="s in poStatuses"
            :key="s.value"
            :value="s.value"
            :active="po.status === s.value"
            @click="changeStatus(s.value)"
          >
            <template #prepend>
              <v-icon :icon="s.icon" :color="s.color" size="18" />
            </template>
            <v-list-item-title>{{ s.label }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
      <v-btn v-if="isAdmin" prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="4">
        <StatCard icon="mdi-truck-delivery" color="primary" label="Supplier" :value="po.supplierName" />
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ formatPrice(po.totalAmount) }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-file-document-outline" color="info" label="Proforma Invoice" :value="po.invoiceNumber || '—'" />
      </v-col>
    </v-row>

    <!-- ── Admin Approval (visible to Admin/SuperAdmin; action buttons for SuperAdmin only) ── -->
    <v-card class="glass-card mb-6" v-if="isAdmin">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-shield-check" class="mr-2" size="20" color="warning" />
        Admin Approval
        <v-spacer />
        <v-chip
          size="small"
          :color="approvalColor(po.adminApproval)"
          :prepend-icon="approvalIcon(po.adminApproval)"
        >{{ po.adminApproval || 'Pending' }}</v-chip>
      </v-card-title>
      <v-card-text>
        <div v-if="po.adminApprovalNote" class="mb-3 text-body-2 text-medium-emphasis">
          <strong>Note:</strong> {{ po.adminApprovalNote }}
        </div>
        <div v-if="po.adminApproval !== 'Approved' && isSuperAdmin" class="d-flex flex-wrap gap-2">
          <v-btn color="success" variant="flat" prepend-icon="mdi-check" :loading="approving" @click="approvePo">Accept</v-btn>
          <v-btn color="error" variant="tonal" prepend-icon="mdi-close" :loading="approving" @click="showRejectDialog = true">Reject</v-btn>
        </div>
        <v-alert v-else-if="po.adminApproval !== 'Approved' && !isSuperAdmin" type="warning" variant="tonal" density="compact" class="mt-2" icon="mdi-lock">
          This PO is locked — only a SuperAdmin can Accept or Reject it.
        </v-alert>
        <v-alert v-else type="success" variant="tonal" density="compact" class="mt-2" icon="mdi-check-circle">
          Approved{{ po.adminApprovalAt ? ' at ' + new Date(po.adminApprovalAt).toLocaleString() : '' }} — Payment role can now process this PO.
        </v-alert>
      </v-card-text>
    </v-card>

    <!-- ── Payment Approval (visible if rejected or after admin approval) ── -->
    <v-card class="glass-card mb-6" v-if="po.adminApproval === 'Approved' || po.paymentApproval === 'Rejected'">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-cash-check" class="mr-2" size="20" color="success" />
        Payment Approval
        <v-spacer />
        <v-chip
          size="small"
          :color="po.paymentApproval === 'Rejected' ? 'error' : (po.paymentStatus === 'Submitted' ? 'success' : 'warning')"
          :prepend-icon="po.paymentApproval === 'Rejected' ? 'mdi-close-circle' : (po.paymentStatus === 'Submitted' ? 'mdi-check-circle' : 'mdi-clock-outline')"
        >
          {{ po.paymentApproval === 'Rejected' ? 'Rejected by Payment' : (po.paymentStatus === 'Submitted' ? 'Payment Submitted' : 'Awaiting Payment') }}
        </v-chip>
      </v-card-title>
      <v-card-text>
        <v-alert v-if="po.paymentApproval === 'Rejected'" type="error" variant="tonal" class="mb-3" icon="mdi-alert-circle">
          <strong>Rejected by Payment:</strong> {{ po.paymentApprovalNote }}
          <div class="text-caption mt-1">Please check the files, replace if necessary, and resubmit (SuperAdmin must re-approve).</div>
        </v-alert>
        <div v-else-if="po.paymentStatus === 'Submitted'" class="text-body-2">
          Payment has been submitted and is pending final acceptance.
        </div>
        <div v-else class="text-body-2 text-medium-emphasis">
          Awaiting payment submission from the Payment department.
        </div>
      </v-card-text>
    </v-card>

    <!-- ── Supplier Documents (visible to everyone who can see the PO) ── -->
    <v-card class="glass-card mb-6" v-if="po.invoiceId && po.supplierId">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-folder-multiple-outline" class="mr-2" size="20" color="primary" />
        Supplier Documents
        <v-chip v-if="supplierDocs.length" size="x-small" class="ml-2" variant="tonal" color="primary">{{ supplierDocs.length }}</v-chip>
        <v-spacer />
        <v-menu>
          <template #activator="{ props: menuProps }">
            <v-btn
              variant="tonal"
              color="primary"
              size="small"
              prepend-icon="mdi-upload"
              append-icon="mdi-chevron-down"
              :loading="uploadingSupplierDoc"
              v-bind="menuProps"
            >Upload</v-btn>
          </template>
          <v-list density="compact">
            <v-list-item @click="triggerUpload('supplier_invoice')">
              <template #prepend>
                <v-icon icon="mdi-file-document-outline" size="18" color="primary" />
              </template>
              <v-list-item-title>Supplier Invoice</v-list-item-title>
            </v-list-item>
            <v-list-item @click="triggerUpload('supplier_bank_info')">
              <template #prepend>
                <v-icon icon="mdi-bank-outline" size="18" color="success" />
              </template>
              <v-list-item-title>Supplier Bank Info</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>
        <input ref="supplierDocInputRef" type="file" class="d-none" @change="onSupplierDocSelected" />
      </v-card-title>
      <v-card-text>
        <div v-if="!supplierDocs.length" class="text-body-2 text-medium-emphasis">
          No supplier documents uploaded yet. Click <strong>Upload</strong> to add one.
        </div>
        <div v-else class="d-flex flex-column gap-2">
          <div
            v-for="f in supplierDocs"
            :key="f.name"
            class="d-flex align-center gap-3 pa-2 rounded file-row"
          >
            <v-icon icon="mdi-file-document-outline" color="primary" size="22" />
            <div class="d-flex flex-column" style="min-width:0; flex:1;">
              <span class="text-body-2 font-weight-medium text-truncate">{{ f.name }}</span>
              <span class="text-caption text-medium-emphasis">
                {{ formatBytes(f.size) }} · {{ new Date(f.modifiedAt).toLocaleString() }}
              </span>
            </div>
            <v-btn size="small" variant="tonal" color="info" prepend-icon="mdi-download" @click="downloadSupplierDoc(f.name)">Download</v-btn>
            <v-btn
              v-if="isAdmin"
              size="small"
              variant="text"
              color="error"
              icon="mdi-delete"
              :loading="deletingDoc === f.name"
              @click="deleteSupplierDoc(f.name)"
            />
          </div>
        </div>
      </v-card-text>
    </v-card>

    <!-- ── Full RFQ → Quote → Invoice → PO Trail (Admin only) ── -->
    <v-card class="glass-card mb-6" v-if="isAdmin && enriched">
      <v-card-title>
        <v-icon icon="mdi-chart-timeline-variant" class="mr-2" size="20" />
        Item Trail — RFQ → Procurement → Quote → Invoice → PO
      </v-card-title>
      <v-card-text class="pa-0">
        <v-table density="compact" class="enriched-table">
          <thead>
            <tr>
              <th>Part</th>
              <th>Description</th>
              <th>Qty</th>
              <th>Customer</th>
              <th>RFQ</th>
              <th>Proc. Supplier</th>
              <th>Buy Price</th>
              <th>Quote</th>
              <th>Quote Price</th>
              <th>Invoice</th>
              <th>Inv. Price</th>
              <th>PO Supplier</th>
              <th>PO Price</th>
              <th>PO Total</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(it, idx) in (enriched.items || [])" :key="idx">
              <td class="font-weight-medium">{{ it.partNumber || '—' }}</td>
              <td class="text-medium-emphasis" style="max-width: 280px; white-space: normal;">{{ it.description || '—' }}</td>
              <td>{{ it.qty }}</td>
              <td>{{ it.customerName || '—' }}</td>
              <td>{{ it.rfqNumber || '—' }}</td>
              <td>{{ it.procurementSupplier || '—' }}</td>
              <td>{{ it.procurementBuyPrice != null ? '$' + formatPrice(it.procurementBuyPrice) : '—' }}</td>
              <td>{{ it.quoteNumber || '—' }}</td>
              <td>{{ it.quoteUnitPrice != null ? '$' + formatPrice(it.quoteUnitPrice) : '—' }}</td>
              <td>{{ it.invoiceNumber || '—' }}</td>
              <td>{{ it.invoiceUnitPrice != null ? '$' + formatPrice(it.invoiceUnitPrice) : '—' }}</td>
              <td>{{ it.poSupplier || '—' }}</td>
              <td>${{ formatPrice(it.poUnitPrice) }}</td>
              <td class="font-weight-bold">${{ formatPrice(it.poTotalPrice) }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <!-- ── Import Details ── -->
    <v-card class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-bank" class="mr-2" size="20" />
        Import Details
        <v-spacer />
        <v-btn
          v-if="!editingImport"
          variant="tonal"
          size="small"
          prepend-icon="mdi-pencil"
          @click="editingImport = true"
        >Edit</v-btn>
        <template v-else>
          <v-btn variant="text" size="small" class="mr-1" @click="cancelImportEdit">Cancel</v-btn>
          <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-content-save" :loading="savingImport" @click="saveImport">Save</v-btn>
        </template>
      </v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="4">
            <v-text-field v-model="importForm.bankName" label="Bank Name" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="importForm.bankAccountNumber" label="Account Number" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="importForm.bankAddress" label="Bank Address" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.bankCity" label="Bank City" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.bankCountry" label="Bank Country" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.fedExAccount" label="FedEx Account" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.courierName" label="Courier Name" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-select v-model="importForm.shippingMethod" :items="['Air', 'Sea', 'Ground', 'Express']" label="Shipping Method" variant="outlined" density="compact" hide-details :readonly="!editingImport" clearable />
          </v-col>
          <v-col cols="12" md="3">
            <v-select v-model="importForm.incoterms" :items="['FOB', 'CIF', 'EXW', 'DDP', 'FCA', 'CPT', 'DAP']" label="Incoterms" variant="outlined" density="compact" hide-details :readonly="!editingImport" clearable />
          </v-col>
          <v-col cols="12">
            <v-textarea v-model="importForm.notes" label="Notes" variant="outlined" density="compact" hide-details rows="2" auto-grow :readonly="!editingImport" />
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <!-- ── Line Items with Track Numbers ── -->
    <v-card class="glass-card mb-6">
      <v-card-title>
        <v-icon icon="mdi-package-variant-closed" class="mr-2" size="20" />
        Line Items &amp; Tracking
      </v-card-title>
      <v-card-text>
        <v-table density="comfortable">
          <thead>
            <tr>
              <th style="width:40px;"></th>
              <th>Part</th>
              <th>Qty</th>
              <th>Unit Price</th>
              <th>Total</th>
              <th>Condition</th>
              <th style="width:60px;">Tracks</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(item, idx) in (po.items || [])" :key="item.id">
              <tr>
                <td>
                  <v-btn
                    icon
                    size="x-small"
                    variant="text"
                    @click="toggleItemExpand(item.id)"
                  >
                    <v-icon :icon="expandedItems.has(item.id) ? 'mdi-chevron-up' : 'mdi-chevron-down'" />
                  </v-btn>
                </td>
                <td class="font-weight-medium">{{ item.partNumberName || '—' }}</td>
                <td>{{ item.qty }}</td>
                <td>${{ formatPrice(item.unitPrice) }}</td>
                <td class="font-weight-bold">${{ formatPrice(item.totalPrice) }}</td>
                <td>{{ item.condition || '—' }}</td>
                <td>
                  <v-chip size="x-small" color="primary" variant="tonal">
                    {{ (item.trackNumbers || []).length }}
                  </v-chip>
                </td>
              </tr>
              <!-- Expanded Track Numbers -->
              <tr v-if="expandedItems.has(item.id)">
                <td :colspan="7" class="pa-0">
                  <div style="background: rgba(var(--v-theme-surface-variant), 0.08); padding: 12px 16px 12px 48px;">
                    <div class="d-flex align-center mb-2">
                      <span class="text-caption text-medium-emphasis font-weight-bold">TRACKING NUMBERS</span>
                      <v-spacer />
                      <v-btn size="x-small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="openAddTrack(item.id)">Add</v-btn>
                    </div>
                    <div v-if="!(item.trackNumbers || []).length" class="text-body-2 text-medium-emphasis py-2">
                      No tracking numbers yet.
                    </div>
                    <v-table v-else density="compact" class="bg-transparent">
                      <thead>
                        <tr>
                          <th>Track Number</th>
                          <th>Carrier</th>
                          <th>Notes</th>
                          <th>Added</th>
                          <th style="width:40px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="t in item.trackNumbers" :key="t.id">
                          <td class="font-weight-medium" style="color: #60a5fa;">{{ t.trackNumber }}</td>
                          <td>{{ t.carrier || '—' }}</td>
                          <td class="text-medium-emphasis">{{ t.notes || '—' }}</td>
                          <td class="text-caption">{{ t.createdAt ? new Date(t.createdAt).toLocaleDateString() : '—' }}</td>
                          <td>
                            <v-btn icon size="x-small" variant="text" color="error" @click="deleteTrack(item, t.id)">
                              <v-icon icon="mdi-delete-outline" size="16" />
                            </v-btn>
                          </td>
                        </tr>
                      </tbody>
                    </v-table>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <!-- Add Track Number Dialog -->
    <v-dialog v-model="showAddTrackDialog" max-width="450" persistent>
      <v-card>
        <v-card-title class="text-h6">Add Tracking Number</v-card-title>
        <v-card-text>
          <v-text-field v-model="trackForm.trackNumber" label="Track Number" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="trackForm.carrier" label="Carrier (e.g. FedEx, DHL)" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="trackForm.notes" label="Notes" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showAddTrackDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="savingTrack" :disabled="!trackForm.trackNumber.trim()" @click="addTrack">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Reject PO Dialog -->
    <v-dialog v-model="showRejectDialog" max-width="480" persistent>
      <v-card>
        <v-card-title class="text-h6">Reject Purchase Order</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">Provide a reason for rejection (optional):</p>
          <v-textarea v-model="rejectionNote" label="Rejection Note" variant="outlined" rows="3" auto-grow />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="approving" @click="rejectPo">Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>

    <PoPdfGenerator v-model="showPdf" :po-id="String(route.params.id)" />
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const po = ref<any>({})
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const poStatuses = [
  { value: 'Waiting For Admin Approval', label: 'Waiting For Admin Approval', icon: 'mdi-shield-clock', color: 'warning' },
  { value: 'Waiting For Payment', label: 'Waiting For Payment', icon: 'mdi-clock-outline', color: 'orange' },
  { value: 'Payment Done', label: 'Payment Done', icon: 'mdi-cash-check', color: 'success' },
  { value: 'Ship To Warehouse 1', label: 'Ship To Warehouse 1', icon: 'mdi-warehouse', color: 'indigo' },
  { value: 'Ship To Warehouse 2', label: 'Ship To Warehouse 2', icon: 'mdi-warehouse', color: 'deep-purple' },
  { value: 'Ship To Warehouse 3', label: 'Ship To Warehouse 3', icon: 'mdi-warehouse', color: 'blue-grey' },
  { value: 'Ship To Customer', label: 'Ship To Customer', icon: 'mdi-account-arrow-right', color: 'info' },
  { value: 'Completed', label: 'Completed', icon: 'mdi-check-all', color: 'teal' },
  { value: 'Cancelled', label: 'Cancelled', icon: 'mdi-cancel', color: 'grey' },
]

const isAdmin = computed(() => authStore.isAdmin)
const isSuperAdmin = computed(() => authStore.isSuperAdmin)
const showPdf = ref(false)

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('po', entityId)

const poStatusColor = computed(() => {
  const found = poStatuses.find(s => s.value === po.value.status)
  return found?.color || 'grey'
})

// ── Company Presets ──
const apiPresets = ref<any[]>([])
async function loadPresets() {
  try {
    apiPresets.value = await api.get('/company-presets')
  } catch {}
}

// ── Import Details ──
const editingImport = ref(false)
const savingImport = ref(false)
const importForm = ref<any>({
  bankName: '', bankAccountNumber: '', bankAddress: '',
  bankCity: '', bankCountry: '',
  fedExAccount: '', courierName: '',
  shippingMethod: '', incoterms: '', notes: '',
})
const importOriginal = ref<any>({})

async function loadImportDetail() {
  try {
    const detail = await api.get<any>(`/purchase-orders/${route.params.id}/import-detail`)
    if (detail) {
      importForm.value = { ...detail }
      importOriginal.value = { ...detail }
    }
  } catch {}
}

function cancelImportEdit() {
  importForm.value = { ...importOriginal.value }
  editingImport.value = false
}

async function saveImport() {
  savingImport.value = true
  try {
    const saved = await api.put<any>(`/purchase-orders/${route.params.id}/import-detail`, importForm.value)
    importForm.value = { ...saved }
    importOriginal.value = { ...saved }
    editingImport.value = false
    showSnack('Import details saved', 'success')
    // Auto-generate DasturPardakht (DP) PDF (best-effort; don't fail the save flow on PDF errors)
    try { await generateAndUploadDpPdf() } catch (e) { console.error('DP PDF generation failed', e) }
  } catch {
    showSnack('Failed to save import details', 'error')
  } finally {
    savingImport.value = false
  }
}

async function generateAndUploadDpPdf() {
  if (!po.value?.invoiceId || !po.value?.supplierId) return

  // Ensure dependencies are loaded
  if (!enriched.value) await loadEnriched()
  if (!apiPresets.value.length) await loadPresets()

  // Build items from enriched trail if available, else from po.items
  const trailItems = (enriched.value?.items || []).filter((it: any) =>
    it.poSupplier && it.poSupplier === po.value.supplierName
  )
  const items = (trailItems.length ? trailItems : (po.value.items || [])).map((it: any) => ({
    partNumber: it.partNumber || it.partNumberName || '—',
    qty: it.qty || 0,
    poSupplier: it.poSupplier || po.value.supplierName || '—',
    quotePrice: it.quoteUnitPrice ?? null,
    poPrice: it.poUnitPrice ?? it.unitPrice ?? 0,
    poTotal: it.poTotalPrice ?? it.totalPrice ?? 0,
  }))
  const grandTotal = items.reduce((s: number, i: any) => s + Number(i.poTotal || 0), 0)

  // Map customerBase to Company Preset name
  let companyPresetName = ''
  // Use enriched items first as they contain the customerBase from RFQ
  const firstItem = (enriched.value?.items || []).find((it: any) => it.customerBase != null)
  if (firstItem) {
    const match = apiPresets.value.find((p: any) => p.sortOrder === firstItem.customerBase)
    if (match) {
      companyPresetName = match.name
    } else {
      console.warn(`No CompanyPreset found with sortOrder ${firstItem.customerBase}`)
    }
  } else {
    console.warn('No items with customerBase found in enriched trail')
  }

  const payload = {
    poNumber: po.value.poNumber,
    documentDate: new Date().toISOString().slice(0, 10),
    supplierName: po.value.supplierName,
    currency: po.value.currency || 'USD',
    currencySymbol: '$',
    companyPresetName,
    bankName: importForm.value.bankName,
    bankAccountNumber: importForm.value.bankAccountNumber,
    bankAddress: importForm.value.bankAddress,
    bankCity: importForm.value.bankCity,
    bankCountry: importForm.value.bankCountry,
    swiftCode: importForm.value.swiftCode || null,
    notes: importForm.value.notes,
    items,
    grandTotal,
  }

  console.log('Generating DP PDF with payload:', payload)

  const blob = await $fetch<Blob>(`${config.public.apiBase}/pdf/dp`, {
    method: 'POST',
    body: payload,
    headers: { Authorization: `Bearer ${authStore.user?.token}` },
    responseType: 'blob',
  })

  // Upload the generated PDF to supplier folder under category "dp"
  const form = new FormData()
  const file = new File([blob], `DP-${po.value.poNumber || 'document'}.pdf`, { type: 'application/pdf' })
  form.append('file', file)
  form.append('category', 'dp')
  await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${po.value.invoiceId}/supplier/${po.value.supplierId}/upload`, {
    method: 'POST',
    body: form,
    headers: { Authorization: `Bearer ${authStore.user?.token}` },
  })
  await loadSupplierDocs()
  showSnack('DP PDF generated', 'success')
}

// ── Track Numbers ──
const expandedItems = ref(new Set<number>())
const showAddTrackDialog = ref(false)
const savingTrack = ref(false)
const addTrackItemId = ref<number | null>(null)
const trackForm = ref({ trackNumber: '', carrier: '', notes: '' })

function toggleItemExpand(id: number) {
  if (expandedItems.value.has(id)) {
    expandedItems.value.delete(id)
  } else {
    expandedItems.value.add(id)
  }
  expandedItems.value = new Set(expandedItems.value)
}

function openAddTrack(poItemId: number) {
  addTrackItemId.value = poItemId
  trackForm.value = { trackNumber: '', carrier: '', notes: '' }
  showAddTrackDialog.value = true
}

async function addTrack() {
  if (!addTrackItemId.value || !trackForm.value.trackNumber.trim()) return
  savingTrack.value = true
  try {
    const newTrack = await api.post<any>(`/purchase-orders/items/${addTrackItemId.value}/track-numbers`, trackForm.value)
    const item = (po.value.items || []).find((i: any) => i.id === addTrackItemId.value)
    if (item) {
      if (!item.trackNumbers) item.trackNumbers = []
      item.trackNumbers.unshift(newTrack)
    }
    showAddTrackDialog.value = false
    showSnack('Tracking number added', 'success')
  } catch {
    showSnack('Failed to add tracking number', 'error')
  } finally {
    savingTrack.value = false
  }
}

async function deleteTrack(item: any, trackId: number) {
  try {
    await api.del(`/purchase-orders/track-numbers/${trackId}`)
    item.trackNumbers = (item.trackNumbers || []).filter((t: any) => t.id !== trackId)
    showSnack('Tracking number removed', 'success')
  } catch {
    showSnack('Failed to delete tracking number', 'error')
  }
}

// ── Admin Approval ──
const approving = ref(false)
const showRejectDialog = ref(false)
const rejectionNote = ref('')
const enriched = ref<any>(null)

function approvalColor(v: string | undefined) {
  if (v === 'Approved') return 'success'
  if (v === 'Rejected') return 'error'
  return 'warning'
}
function approvalIcon(v: string | undefined) {
  if (v === 'Approved') return 'mdi-check-circle'
  if (v === 'Rejected') return 'mdi-close-circle'
  return 'mdi-clock-outline'
}

async function approvePo() {
  approving.value = true
  try {
    await api.patch(`/purchase-orders/${route.params.id}/admin-approval`, { decision: 'Approved', note: null })
    po.value.adminApproval = 'Approved'
    po.value.adminApprovalAt = new Date().toISOString()
    po.value.status = 'Waiting For Payment'
    showSnack('PO approved', 'success')
  } catch { showSnack('Failed to approve', 'error') }
  finally { approving.value = false }
}

async function rejectPo() {
  approving.value = true
  try {
    await api.patch(`/purchase-orders/${route.params.id}/admin-approval`, { decision: 'Rejected', note: rejectionNote.value || null })
    po.value.adminApproval = 'Rejected'
    po.value.adminApprovalNote = rejectionNote.value || null
    po.value.status = 'Waiting For Admin Approval'
    showRejectDialog.value = false
    rejectionNote.value = ''
    showSnack('PO rejected', 'warning')
  } catch { showSnack('Failed to reject', 'error') }
  finally { approving.value = false }
}

async function loadEnriched() {
  try {
    enriched.value = await api.get(`/purchase-orders/${route.params.id}/enriched`)
  } catch {}
}

// ── Supplier Documents ──
type SupplierFile = { name: string; size: number; modifiedAt: string }
const supplierDocs = ref<SupplierFile[]>([])
const uploadingSupplierDoc = ref(false)
const deletingDoc = ref<string | null>(null)
const supplierDocInputRef = ref<HTMLInputElement | null>(null)
const uploadCategory = ref<string>('supplier_invoice')
const config = useRuntimeConfig()

function triggerUpload(category: string) {
  uploadCategory.value = category
  supplierDocInputRef.value?.click()
}

function formatBytes(bytes: number) {
  if (!bytes) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  let i = 0, b = bytes
  while (b >= 1024 && i < units.length - 1) { b /= 1024; i++ }
  return `${b.toFixed(b < 10 && i > 0 ? 1 : 0)} ${units[i]}`
}

async function loadSupplierDocs() {
  if (!po.value?.invoiceId || !po.value?.supplierId) return
  try {
    const data = await api.get<any>(`/documents/proforma-invoice/${po.value.invoiceId}`)
    const section = (data?.suppliers || []).find((s: any) => s.supplierId === po.value.supplierId)
    supplierDocs.value = (section?.files || []).filter((f: SupplierFile) =>
      f.name.startsWith('Supplier Invoice') ||
      f.name.startsWith('supplier_invoice') ||
      f.name.startsWith('Supplier Bank Info') ||
      f.name.startsWith('supplier_bank_info') ||
      f.name.startsWith('DP') ||
      f.name.startsWith('dp ')
    )
  } catch {
    supplierDocs.value = []
  }
}

async function onSupplierDocSelected(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !po.value?.invoiceId || !po.value?.supplierId) return
  uploadingSupplierDoc.value = true
  try {
    const form = new FormData()
    form.append('file', file)
    form.append('category', uploadCategory.value || 'supplier_invoice')
    await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${po.value.invoiceId}/supplier/${po.value.supplierId}/upload`, {
      method: 'POST',
      body: form,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Supplier document uploaded', 'success')
    await loadSupplierDocs()
  } catch (err: any) {
    showSnack(err?.data?.message || 'Upload failed', 'error')
  } finally {
    uploadingSupplierDoc.value = false
    if (input) input.value = ''
  }
}

async function downloadSupplierDoc(name: string) {
  if (!po.value?.invoiceId || !po.value?.supplierId) return
  try {
    const blob = await $fetch<Blob>(
      `${config.public.apiBase}/documents/proforma-invoice/${po.value.invoiceId}/supplier/${po.value.supplierId}/file`,
      {
        method: 'GET',
        query: { name },
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
        responseType: 'blob',
      }
    )
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = name
    document.body.appendChild(a); a.click(); a.remove()
    URL.revokeObjectURL(url)
  } catch { showSnack('Download failed', 'error') }
}

async function deleteSupplierDoc(name: string) {
  if (!po.value?.invoiceId || !po.value?.supplierId) return
  if (!confirm(`Delete "${name}"?`)) return
  deletingDoc.value = name
  try {
    await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${po.value.invoiceId}/supplier/${po.value.supplierId}/file`, {
      method: 'DELETE',
      query: { name },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Deleted', 'success')
    await loadSupplierDocs()
  } catch { showSnack('Delete failed', 'error') }
  finally { deletingDoc.value = null }
}

// ── Load Data ──
onMounted(async () => {
  try { po.value = await api.get(`/purchase-orders/${route.params.id}`) } catch {}
  await Promise.all([loadImportDetail(), checkLock(), loadEnriched(), loadSupplierDocs(), loadPresets()])
})

// ── Status ──
async function changeStatus(newStatus: string) {
  if (newStatus === po.value.status) return

  // 1. If waiting for admin approval, manual change is blocked
  if (po.value.adminApproval !== 'Approved' && po.value.status === 'Waiting For Admin Approval') {
    showSnack('Cannot manually change status until SuperAdmin approves', 'warning')
    return
  }

  // 2. If waiting for payment, manual change is blocked
  if (po.value.adminApproval === 'Approved' && po.value.paymentStatus !== 'Submitted') {
    showSnack('Cannot manually change status while Awaiting Payment', 'warning')
    return
  }

  try {
    await api.patch(`/purchase-orders/${po.value.id}/status`, { status: newStatus })
    po.value.status = newStatus
    showSnack(`Status changed to ${newStatus}`, 'success')
  } catch {
    showSnack('Failed to change status', 'error')
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>

<style scoped>
/* Theme-aware file row — adapts to both light and dark themes via Vuetify CSS vars. */
.file-row {
  background-color: rgba(var(--v-theme-on-surface), 0.06);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  transition: background-color 0.15s ease;
}
.file-row:hover {
  background-color: rgba(var(--v-theme-on-surface), 0.1);
}
</style>
