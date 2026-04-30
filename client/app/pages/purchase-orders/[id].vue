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
      <!-- v-if="isAdmin" For admin PDF button -->
      <v-btn prepend-icon="mdi-file-pdf-box" size="small" color="error" class="mr-1" @click="showPdf = true">PDF</v-btn>
      <v-btn prepend-icon="mdi-file-export-outline" size="small" color="warning" class="mr-1" @click="showPrDialog = true">PR</v-btn>
      <v-btn
        v-if="isAdmin || assignedUsers.some(u => u.userId === authStore.user?.id)"
        prepend-icon="mdi-keyboard-return"
        size="small"
        variant="tonal"
        color="warning"
        :disabled="isTerminalState"
        @click="openReturnDialog"
      >
        Return
      </v-btn>
    </div>

    <v-row class="mb-6">
      <!-- Assigned Users (Admin only, moved to top) -->
      <v-col v-if="isAdmin" cols="12" md="3">
        <v-card class="glass-card pa-4 h-100 d-flex flex-column">
          <div class="d-flex align-center gap-3 mb-3">
            <v-avatar color="primary" variant="tonal" size="40">
              <v-icon icon="mdi-shield-account-outline" size="20" />
            </v-avatar>
            <div class="flex-grow-1 min-width-0">
              <p class="text-caption text-medium-emphasis mb-0">Assigned Users</p>
              <div class="d-flex align-center">
                <span class="text-body-2 font-weight-medium">{{ assignedUsers.length }}</span>
                <v-spacer />
                <v-btn icon="mdi-account-plus" size="x-small" variant="tonal" color="primary" @click="showAddAssignDialog = true" />
              </div>
            </div>
          </div>
          <div v-if="assignedUsers.length" class="overflow-y-auto pr-1" style="max-height: 80px;">
            <div v-for="p in assignedUsers" :key="p.id" class="d-flex align-center gap-2 mb-1 pa-1 rounded" style="background: rgba(var(--v-theme-on-surface), 0.04);">
              <span class="text-caption flex-grow-1 text-truncate" :title="p.user?.username || p.user?.email">{{ p.user?.name || p.user?.email }}</span>
              <v-chip size="x-small" :color="p.permission === 'Edit' ? 'success' : 'info'" variant="tonal" class="px-1" style="height: 16px; font-size: 10px;">{{ p.permission }}</v-chip>
              <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" :loading="revokingId === p.id" @click="revokeAssignment(p)" />
            </div>
          </div>
          <div v-else class="text-caption text-medium-emphasis text-center py-2">No users assigned</div>
        </v-card>
      </v-col>

      <v-col cols="12" :md="isAdmin ? 3 : 4">
        <StatCard icon="mdi-truck-delivery" color="primary" label="Supplier" :value="po.supplierName" class="h-100" />
      </v-col>
      <v-col cols="12" :md="isAdmin ? 3 : 4">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount" class="h-100">
          ${{ formatPrice(po.totalAmount) }}
        </StatCard>
      </v-col>
      <v-col cols="12" :md="isAdmin ? 3 : 4">
        <StatCard icon="mdi-file-document-outline" color="info" label="Proforma Invoice" :value="po.invoiceNumber || '—'" class="h-100" />
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
          Approved{{ po.adminApprovalAt ? ' at ' + new Date(po.adminApprovalAt).toLocaleString() : '' }} — SuperAdmin has accepted the PO.
        </v-alert>

        <!-- ── Document Verification (visible after SuperAdmin approval) ── -->
        <div v-if="po.adminApproval === 'Approved'" class="mt-4 pa-4 rounded border-dashed">
          <div class="d-flex align-center mb-4">
            <div>
              <div class="text-subtitle-2 font-weight-bold">Step 2: Document Verification</div>
              <div class="text-caption text-medium-emphasis">Critical payment documents track.</div>
            </div>
            <v-spacer />
            <v-chip v-if="po.status !== 'Waiting For Documents' && po.status !== 'Waiting For Admin Approval'" color="success" size="small" prepend-icon="mdi-check-decagram">Documents Verified</v-chip>
            <v-btn
              v-else-if="po.status === 'Waiting For Documents'"
              color="primary"
              variant="flat"
              prepend-icon="mdi-file-check"
              :loading="approving"
              @click="acceptDocuments"
            >Accept Documents</v-btn>
          </div>

          <v-divider class="mb-4" />

          <v-row dense>
            <!-- Customer POP -->
            <v-col cols="12" md="3">
              <div class="pa-3 rounded border" style="background: rgba(var(--v-theme-primary), 0.03);">
                <div class="d-flex align-center mb-3">
                  <v-icon icon="mdi-account-cash" size="18" class="mr-2" color="primary" />
                  <span class="text-caption font-weight-bold uppercase">Customer POP</span>
                  <v-spacer />
                  <v-btn size="x-small" variant="text" icon="mdi-plus" color="primary" @click="triggerPiUpload('customer_pop')" />
                </div>
                <div v-if="piDocs.filter(f => f.category === 'customer_pop').length" class="d-flex flex-column gap-2">
                  <div v-for="f in piDocs.filter(f => f.category === 'customer_pop')" :key="f.name + f.originalInvoiceId" class="d-flex align-center pa-1 rounded bg-surface hover-bg-surface-variant">
                    <v-icon icon="mdi-file-pdf-box" size="14" color="error" class="mr-1" />
                    <span class="text-caption text-truncate flex-grow-1" style="max-width: 200px;" :title="f.displayName || f.name">{{ f.displayName || f.name }}</span>
                    <v-btn icon="mdi-download" size="x-small" variant="text" @click="downloadSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="deleteSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                  </div>
                </div>
                <div v-else class="text-center py-2">
                  <v-btn size="x-small" variant="tonal" color="primary" block prepend-icon="mdi-upload" @click="triggerPiUpload('customer_pop')">Upload</v-btn>
                </div>
              </div>
            </v-col>
            <!-- Customer PO -->
            <v-col cols="12" md="3">
              <div class="pa-3 rounded border" style="background: rgba(var(--v-theme-secondary), 0.03);">
                <div class="d-flex align-center mb-3">
                  <v-icon icon="mdi-file-document" size="18" class="mr-2" color="secondary" />
                  <span class="text-caption font-weight-bold uppercase">Customer PO</span>
                  <v-spacer />
                  <v-btn size="x-small" variant="text" icon="mdi-plus" color="secondary" @click="triggerPiUpload('customer_po')" />
                </div>
                <div v-if="piDocs.filter(f => f.category === 'customer_po').length" class="d-flex flex-column gap-2">
                  <div v-for="f in piDocs.filter(f => f.category === 'customer_po')" :key="f.name + f.originalInvoiceId" class="d-flex align-center pa-1 rounded bg-surface hover-bg-surface-variant">
                    <v-icon icon="mdi-file-pdf-box" size="14" color="error" class="mr-1" />
                    <span class="text-caption text-truncate flex-grow-1" style="max-width: 200px;" :title="f.displayName || f.name">{{ f.displayName || f.name }}</span>
                    <v-btn icon="mdi-download" size="x-small" variant="text" @click="downloadSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="deleteSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                  </div>
                </div>
                <div v-else class="text-center py-2">
                  <v-btn size="x-small" variant="tonal" color="secondary" block prepend-icon="mdi-upload" @click="triggerPiUpload('customer_po')">Upload</v-btn>
                </div>
              </div>
            </v-col>
            <!-- Our PI -->
            <v-col cols="12" md="3">
              <div class="pa-3 rounded border" style="background: rgba(var(--v-theme-info), 0.03);">
                <div class="d-flex align-center mb-3">
                  <v-icon icon="mdi-file-document-outline" size="18" class="mr-2" color="info" />
                  <span class="text-caption font-weight-bold uppercase">Our PI</span>
                  <v-spacer />
                  <v-btn size="x-small" variant="text" icon="mdi-plus" color="info" @click="triggerPiUpload('our_pi')" />
                </div>
                <div v-if="piDocs.filter(f => f.category === 'our_pi').length" class="d-flex flex-column gap-1">
                  <div v-for="f in piDocs.filter(f => f.category === 'our_pi')" :key="f.name + f.originalInvoiceId" class="d-flex align-center pa-1 rounded bg-surface hover-bg-surface-variant">
                    <v-icon icon="mdi-file-pdf-box" size="14" color="error" class="mr-1" />
                    <span class="text-caption text-truncate flex-grow-1" style="max-width: 200px;" :title="f.displayName || f.name">{{ f.displayName || f.name }}</span>
                    <v-btn icon="mdi-download" size="x-small" variant="text" @click="downloadSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="deleteSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                  </div>
                </div>
                <div v-else class="text-center py-2">
                  <v-btn size="x-small" variant="tonal" color="info" block prepend-icon="mdi-upload" @click="triggerPiUpload('our_pi')">Upload</v-btn>
                </div>
              </div>
            </v-col>
            <!-- Our POP to Supplier -->
            <v-col cols="12" md="3">
              <div class="pa-3 rounded border" style="background: rgba(var(--v-theme-success), 0.03);">
                <div class="d-flex align-center mb-3">
                  <v-icon icon="mdi-cash-register" size="18" class="mr-2" color="success" />
                  <span class="text-caption font-weight-bold uppercase">Our POP</span>
                  <v-spacer />
                  <v-btn size="x-small" variant="text" icon="mdi-plus" color="success" @click="triggerUpload('our_pop')" />
                </div>
                <div v-if="supplierDocs.filter(f => f.category === 'our_pop').length" class="d-flex flex-column gap-1">
                  <div v-for="f in supplierDocs.filter(f => f.category === 'our_pop')" :key="f.name + f.originalInvoiceId" class="d-flex align-center pa-1 rounded bg-surface hover-bg-surface-variant">
                    <v-icon icon="mdi-file-pdf-box" size="14" color="error" class="mr-1" />
                    <span class="text-caption text-truncate flex-grow-1" style="max-width: 200px;" :title="f.displayName || f.name">{{ f.displayName || f.name }}</span>
                    <v-btn icon="mdi-download" size="x-small" variant="text" @click="downloadSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="deleteSupplierDoc(f.name, f.originalInvoiceId, f.category)" />
                  </div>
                </div>
                <div v-else class="text-center py-2">
                  <v-btn size="x-small" variant="tonal" color="success" block prepend-icon="mdi-upload" @click="triggerUpload('our_pop')">Upload</v-btn>
                </div>
              </div>
            </v-col>
          </v-row>
          <input ref="piDocInputRef" type="file" class="d-none" @change="onPiDocSelected" />
        </div>
      </v-card-text>
    </v-card>

    <!-- ── Payment Approval (admin/payment only, visible if rejected or after admin approval) ── -->
    <v-card class="glass-card mb-6" v-if="isAdmin && (po.adminApproval === 'Approved' || po.paymentApproval === 'Rejected')">
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
            <v-list-item @click="showPrDialog = true">
              <template #prepend>
                <v-icon icon="mdi-file-export-outline" size="18" color="warning" />
              </template>
              <v-list-item-title>Payment Request (PR)</v-list-item-title>
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
            <v-btn size="small" variant="tonal" color="info" prepend-icon="mdi-download" @click="downloadSupplierDoc(f.name, undefined, f.category)">Download</v-btn>
            <v-btn
              v-if="isAdmin"
              size="small"
              variant="text"
              color="error"
              icon="mdi-delete"
              :loading="deletingDoc === f.name"
              @click="deleteSupplierDoc(f.name, undefined, f.category)"
            />
          </div>
        </div>
      </v-card-text>
    </v-card>

    <!-- ── Full RFQ → Quote → Invoice → PO Trail (Admin only) ── -->
    <v-card class="glass-card mb-6" v-if="isAdmin && enriched">
      <v-card-title>
        <v-icon icon="mdi-chart-timeline-variant" class="mr-2" size="20" />
        Item Trail — RFQ → Quote → Invoice → PO
      </v-card-title>
      <v-card-text class="pa-0">
        <v-table density="compact" class="enriched-table header-border">
          <thead>
            <tr>
              <th rowspan="2" class="border-end">Part</th>
              <th rowspan="2" class="border-end">Description</th>
              <th rowspan="2" class="text-center border-end">Qty</th>
              <th rowspan="2" class="border-end">Customer Code</th>
              <th rowspan="2" class="border-end">RFQ</th>


              <th rowspan="2" class="border-end">Quote</th>
              <th rowspan="2" class="border-end">PI</th>
              <th colspan="2" class="text-center border-end grouped-header">PI Price</th>
              <th colspan="2" class="text-center grouped-header">PO Price</th>
            </tr>
            <tr>
              <th class="text-center sub-header">UP</th>
              <th class="text-center sub-header border-end">TP</th>
              <th class="text-center sub-header">UP</th>
              <th class="text-center sub-header">TP</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(it, idx) in (enriched.items || [])" :key="idx">
              <td class="font-weight-medium">{{ it.partNumber || '—' }}</td>
              <td class="text-medium-emphasis" style="max-width: 250px; white-space: normal;">{{ it.description || '—' }}</td>
              <td class="text-center">{{ it.qty }}</td>
              <td>
                <div class="d-flex flex-column">
                  <span>{{ it.customerCode }}</span>
                  <!-- <span v-if="it.customerCode" class="text-caption text-medium-emphasis"></span> -->
                </div>
              </td>
              <td>
                <NuxtLink v-if="it.rfqId" :to="`/rfqs/${it.rfqId}`" class="text-primary text-decoration-none hover-underline font-weight-medium">
                  {{ it.rfqNumber || '—' }}
                </NuxtLink>
                <span v-else>—</span>
              </td>
              <td>
                <NuxtLink v-if="it.quoteId" :to="`/quotes/${it.quoteId}`" class="text-primary text-decoration-none hover-underline font-weight-medium">
                  {{ it.quoteNumber || '—' }}
                </NuxtLink>
                <span v-else>—</span>
              </td>
              <td>
                <NuxtLink v-if="it.invoiceId" :to="`/invoices/${it.invoiceId}`" class="text-primary text-decoration-none hover-underline font-weight-medium">
                  {{ it.invoiceNumber || '—' }}
                </NuxtLink>
                <span v-else>—</span>
              </td>
              <!-- Invoice Price -->
              <td class="text-center">{{ it.invoiceUnitPrice != null ? '$' + formatPrice(it.invoiceUnitPrice) : '—' }}</td>
              <td class="text-center border-end font-weight-medium" style="background: rgba(var(--v-theme-on-surface), 0.02);">
                {{ it.invoiceUnitPrice != null ? '$' + formatPrice(it.invoiceUnitPrice * it.qty) : '—' }}
              </td>
              <!-- PO Price -->
              <td class="text-center">${{ formatPrice(it.poUnitPrice) }}</td>
              <td class="text-center font-weight-bold" style="background: rgba(var(--v-theme-on-surface), 0.02);">
                ${{ formatPrice(it.poTotalPrice) }}
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <!-- ── Import Details (Split into Bank and Shipping) ── -->
    <v-row>
      <v-col cols="12" md="6">
        <v-card class="glass-card mb-6 h-100">
          <v-card-title class="d-flex align-center">
            <v-icon icon="mdi-bank" class="mr-2" size="20" color="success" />
            Bank Information
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
              <v-col cols="12">
                <v-text-field v-model="importForm.bankName" label="Bank Name" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="importForm.bankAccountNumber" label="Account Number" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="importForm.bankAddress" label="Bank Address" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <!-- <v-col cols="12" md="6">
                <v-text-field v-model="importForm.bankCity" label="Bank City" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="importForm.bankCountry" label="Bank Country" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col> -->
              <v-col cols="12" md="6">
                <v-text-field v-model="importForm.swiftCode" label="Swift Code" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="importForm.aba" label="ABA (Routing Number)" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model.number="importForm.wirefee" label="Wire Fee" type="number" prefix="$" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </v-col>
      
      <v-col cols="12" md="6">
        <v-card class="glass-card mb-6 h-100">
          <v-card-title class="d-flex align-center">
            <v-icon icon="mdi-truck-outline" class="mr-2" size="20" color="primary" />
            Shipping Details
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
              <v-col cols="12" md="6">
                <v-text-field v-model="importForm.fedExAccount" label="FedEx Account" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="importForm.courierName" label="Courier Name" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12" md="6">
                <v-select v-model="importForm.shippingMethod" :items="['Air', 'Sea', 'Ground', 'Express']" label="Shipping Method" variant="outlined" density="compact" hide-details :readonly="!editingImport" clearable class="mb-2" />
              </v-col>
              <v-col cols="12" md="6">
                <v-select v-model="importForm.incoterms" :items="['FOB', 'CIF', 'EXW', 'DDP', 'FCA', 'CPT', 'DAP']" label="Incoterms" variant="outlined" density="compact" hide-details :readonly="!editingImport" clearable class="mb-2" />
              </v-col>
              <v-col cols="12">
                <v-textarea v-model="importForm.notes" label="Notes" variant="outlined" density="compact" hide-details rows="4" auto-grow :readonly="!editingImport" />
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- ── PDF Totals (Processing Fee / Shipping / Tax) ──
         These three values feed straight into the PO PDF totals block. They live on the
         PurchaseOrder row itself (separate from per-item shipping costs). The PDF generator
         pre-fills from here, so editing here updates the next PDF you produce. -->
    <v-card class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-calculator-variant-outline" class="mr-2" size="20" />
        PDF Totals
        <v-spacer />
        <v-btn
          v-if="!editingTotals"
          variant="tonal"
          size="small"
          prepend-icon="mdi-pencil"
          @click="editingTotals = true"
        >Edit</v-btn>
        <template v-else>
          <v-btn variant="text" size="small" class="mr-1" @click="cancelTotalsEdit">Cancel</v-btn>
          <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-content-save" :loading="savingTotals" @click="saveTotals">Save</v-btn>
        </template>
      </v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="4">
            <v-text-field
              v-model.number="totalsForm.processingFee"
              label="Processing Fee"
              type="number"
              prefix="$"
              variant="outlined"
              density="compact"
              hide-details
              :readonly="!editingTotals"
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field
              v-model.number="totalsForm.shipping"
              label="Shipping"
              type="number"
              prefix="$"
              variant="outlined"
              density="compact"
              hide-details
              :readonly="!editingTotals"
              hint="PO-level shipping amount — separate from per-item shipping costs"
              persistent-hint
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field
              v-model.number="totalsForm.tax"
              label="Tax"
              type="number"
              prefix="$"
              variant="outlined"
              density="compact"
              hide-details
              :readonly="!editingTotals"
            />
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
              <th>Total Price</th>
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

    <!-- Assign User Dialog -->
    <v-dialog v-model="showAddAssignDialog" max-width="480">
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-account-plus" class="mr-2" color="primary" />
          Assign User to PO
        </v-card-title>
        <v-card-text>
          <v-autocomplete
            v-model="newAssignUserId"
            :items="availableUsersForAssign"
            item-title="label"
            item-value="id"
            label="User"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-account"
          />
          <v-select
            v-model="newAssignPermission"
            :items="[{ title: 'View', value: 'View' }, { title: 'Edit', value: 'Edit' }]"
            label="Permission"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-shield-key-outline"
            class="mt-2"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showAddAssignDialog = false">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="assigning"
            :disabled="!newAssignUserId"
            @click="assignUser"
          >Assign</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>

    <!-- Return Items to Procurement Dialog -->
    <v-dialog v-model="showReturnDialog" max-width="550" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-keyboard-return" class="mr-2" color="warning" />
          Return Items to Procurement
        </v-card-title>
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            Items selected below will be recycled back into the **Procurement layer** for re-sourcing.
            This action soft-deletes the current PO line items.
          </p>
          
          <v-textarea
            v-model="returnForm.reason"
            label="Reason for Return"
            placeholder="e.g. Supplier stock-out, price change, etc."
            variant="outlined"
            rows="3"
            auto-grow
            class="mb-4"
            required
          />

          <div class="text-caption font-weight-bold uppercase mb-2">Select Items to Return</div>
          <v-list density="compact" class="bg-transparent border rounded">
            <v-list-item v-for="item in po.items" :key="item.id">
              <template #prepend>
                <v-checkbox
                  v-model="returnForm.itemIds"
                  :value="item.id"
                  density="compact"
                  hide-details
                />
              </template>
              <v-list-item-title class="text-body-2 font-weight-medium">{{ item.partNumberName }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption">Qty: {{ item.qty }} @ ${{ formatPrice(item.unitPrice) }}</v-list-item-subtitle>
            </v-list-item>
          </v-list>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showReturnDialog = false">Cancel</v-btn>
          <v-btn
            color="warning"
            variant="flat"
            :loading="returning"
            :disabled="!returnForm.reason.trim() || !returnForm.itemIds.length"
            @click="returnPo"
          >Confirm Return</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PoPdfGenerator v-model="showPdf" :po-id="String(route.params.id)" />
    <PaymentRequestPdfGenerator v-model="showPrDialog" :po-id="String(route.params.id)" :po="po" :import-detail="importForm" :enriched="enriched" />
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
  { value: 'Waiting For Documents', label: 'Waiting For Documents', icon: 'mdi-file-clock', color: 'blue' },
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
const showPrDialog = ref(false)

const isTerminalState = computed(() => 
  ['Completed', 'Cancelled', 'Returned'].includes(po.value.status)
)

// ── Return Workflow ──
type ReturnPOResponse = {
  poId: number
  fullReturn: boolean
  poStatus: string
  returnedPOItemIds: number[]
  reopenedProcurementIds: number[]
  skippedPOItemIds: number[]
  warnings: string[]
}
const showReturnDialog = ref(false)
const returning = ref(false)
const returnForm = ref({
  reason: '',
  itemIds: [] as number[]
})

function openReturnDialog() {
  returnForm.value = {
    reason: '',
    itemIds: (po.value.items || []).map((i: any) => i.id) // Default to all items
  }
  showReturnDialog.value = true
}

async function returnPo() {
  if (!returnForm.value.reason.trim()) {
    showSnack('Please provide a reason for return', 'warning')
    return
  }
  returning.value = true
  try {
    const res = await api.post<ReturnPOResponse>(`/purchase-orders/${route.params.id}/return`, {
      reason: returnForm.value.reason,
      itemIds: returnForm.value.itemIds
    })
    
    if (res.warnings?.length) {
      console.warn('Return completed with warnings:', res.warnings)
    }

    showSnack(res.fullReturn ? 'PO returned to Procurement' : 'Items returned to Procurement', 'success')
    showReturnDialog.value = false
    
    // Reload everything to reflect recycled state
    await Promise.all([loadPo(), loadEnriched(), loadSupplierDocs()])
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to return items', 'error')
  } finally {
    returning.value = false
  }
}

async function loadPo() {
  try {
    po.value = await api.get(`/purchase-orders/${route.params.id}`)
    // Hydrate the PDF totals form from the PO response (processingFee / shipping / tax)
    loadTotalsFromPo()
  } catch {}
}

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('po', entityId)

const poStatusColor = computed(() => {
  const found = poStatuses.find(s => s.value === po.value.status)
  return found?.color || 'grey'
})

// ── Assigned Users (admin only) ──
const assignedUsers = ref<any[]>([])
const allUsers = ref<any[]>([])
const showAddAssignDialog = ref(false)
const newAssignUserId = ref<number | null>(null)
const newAssignPermission = ref<'View' | 'Edit'>('Edit')
const assigning = ref(false)
const revokingId = ref<number | null>(null)

const availableUsersForAssign = computed(() => {
  const assignedIds = new Set(assignedUsers.value.map(p => p.userId))
  return allUsers.value
    .filter(u => !assignedIds.has(u.id))
    .map(u => ({ id: u.id, label: u.name || u.email || `User #${u.id}` }))
})

async function loadAssignedUsers() {
  try {
    assignedUsers.value = await api.get(`/permissions/PO/${route.params.id}`)
  } catch {
    assignedUsers.value = []
  }
}

async function loadAllUsers() {
  try {
    const all = await api.get<any[]>('/users')
    const allowed = ['GHS', 'SNP', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']
    // Matching against username which is likely what 'GHS' etc are
    allUsers.value = all.filter(u => allowed.includes(u.username) || allowed.includes(u.name))
  } catch {
    allUsers.value = []
  }
}

async function assignUser() {
  if (!newAssignUserId.value) return
  assigning.value = true
  try {
    await api.post('/permissions/assign', {
      userId: newAssignUserId.value,
      entityName: 'PO',
      entityId: String(route.params.id),
      permission: newAssignPermission.value,
    })
    showSnack('User assigned', 'success')
    showAddAssignDialog.value = false
    newAssignUserId.value = null
    newAssignPermission.value = 'Edit'
    await loadAssignedUsers()
  } catch {
    showSnack('Failed to assign user', 'error')
  } finally {
    assigning.value = false
  }
}

async function revokeAssignment(p: any) {
  revokingId.value = p.id
  try {
    await api.post('/permissions/revoke', {
      userId: p.userId,
      entityName: 'PO',
      entityId: String(route.params.id),
      permission: p.permission,
    })
    showSnack('User removed', 'success')
    await loadAssignedUsers()
  } catch {
    showSnack('Failed to remove user', 'error')
  } finally {
    revokingId.value = null
  }
}

// ── Company Presets ──
const apiPresets = ref<any[]>([])
async function loadPresets() {
  try {
    apiPresets.value = await api.get('/companypresets')
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
  swiftCode: '', aba: '', wirefee: 0,
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
  } catch {
    showSnack('Failed to save import details', 'error')
  } finally {
    savingImport.value = false
  }
}

// ── PDF Totals (Processing Fee / Shipping / Tax) ──
// These three values live on the PurchaseOrder row and feed the PDF totals block.
// They're independent of per-item shippingCost — Shipping here is a flat PO-level number.
const editingTotals = ref(false)
const savingTotals = ref(false)
const totalsForm = ref<{ processingFee: number | null; shipping: number | null; tax: number | null }>({
  processingFee: null,
  shipping: null,
  tax: null,
})
const totalsOriginal = ref<typeof totalsForm.value>({ processingFee: null, shipping: null, tax: null })

function loadTotalsFromPo() {
  if (!po.value) return
  totalsForm.value = {
    processingFee: po.value.processingFee ?? null,
    shipping: po.value.shipping ?? null,
    tax: po.value.tax ?? null,
  }
  totalsOriginal.value = { ...totalsForm.value }
}

function cancelTotalsEdit() {
  totalsForm.value = { ...totalsOriginal.value }
  editingTotals.value = false
}

async function saveTotals() {
  savingTotals.value = true
  try {
    const saved = await api.patch<any>(`/purchase-orders/${route.params.id}/totals`, {
      processingFee: totalsForm.value.processingFee ?? null,
      shipping: totalsForm.value.shipping ?? null,
      tax: totalsForm.value.tax ?? null,
    })
    totalsForm.value = {
      processingFee: saved?.processingFee ?? null,
      shipping: saved?.shipping ?? null,
      tax: saved?.tax ?? null,
    }
    totalsOriginal.value = { ...totalsForm.value }
    // Mirror onto the in-memory PO so any open PDF generator picks up fresh values next render
    if (po.value) {
      po.value.processingFee = totalsForm.value.processingFee
      po.value.shipping = totalsForm.value.shipping
      po.value.tax = totalsForm.value.tax
    }
    editingTotals.value = false
    showSnack('PDF totals saved', 'success')
  } catch {
    showSnack('Failed to save PDF totals', 'error')
  } finally {
    savingTotals.value = false
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
  let companyPresetName = 'JetRux'
  // Use enriched items first as they contain the customerBase from RFQ
  // const firstItem = (enriched.value?.items || []).find((it: any) => it.customerBase != null)
  if (true) {
    const match = apiPresets.value.find((p: any) => p.sortOrder === 105)
    console.log(match)
    if (match) {
      true
    } else {
      // console.warn(`No CompanyPreset found with sortOrder ${firstItem.customerBase}`)
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
    // New Flow: After SuperAdmin approval, move to Waiting For Documents
    po.value.status = 'Waiting For Documents'
    await api.patch(`/purchase-orders/${po.value.id}/status`, { status: 'Waiting For Documents' })
    showSnack('PO approved. Now waiting for documents verification.', 'success')
  } catch { showSnack('Failed to approve', 'error') }
  finally { approving.value = false }
}

async function acceptDocuments() {
  approving.value = true
  try {
    // Transition from Waiting For Documents to Waiting For Payment
    await api.patch(`/purchase-orders/${route.params.id}/status`, { status: 'Waiting For Payment' })
    po.value.status = 'Waiting For Payment'
    showSnack('Documents verified. Status moved to Waiting For Payment.', 'success')
  } catch { showSnack('Failed to verify documents', 'error') }
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
type SupplierFile = { name: string; category: string; size: number; modifiedAt: string; invoiceNumber?: string; originalInvoiceId?: number; displayName?: string }
const supplierDocs = ref<SupplierFile[]>([])
const piDocs = ref<SupplierFile[]>([])
const uploadingSupplierDoc = ref(false)
const uploadingPiDoc = ref(false)
const deletingDoc = ref<string | null>(null)
const supplierDocInputRef = ref<HTMLInputElement | null>(null)
const piDocInputRef = ref<HTMLInputElement | null>(null)
const uploadCategory = ref<string>('supplier_invoice')
const uploadPiCategory = ref<string>('customer_pop')
const config = useRuntimeConfig()

function triggerUpload(category: string) {
  uploadCategory.value = category
  supplierDocInputRef.value?.click()
}

function triggerPiUpload(category: string) {
  uploadPiCategory.value = category
  piDocInputRef.value?.click()
}

function formatBytes(bytes: number) {
  if (!bytes) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  let i = 0, b = bytes
  while (b >= 1024 && i < units.length - 1) { b /= 1024; i++ }
  return `${b.toFixed(b < 10 && i > 0 ? 1 : 0)} ${units[i]}`
}

async function loadSupplierDocs() {
  if (!po.value?.supplierId) return
  try {
    // 1. Collect all unique invoice IDs linked to this PO
    const invoiceIds = new Set<number>()
    if (po.value.invoiceId) invoiceIds.add(po.value.invoiceId)
    if (enriched.value?.items) {
      enriched.value.items.forEach((it: any) => {
        if (it.invoiceId) invoiceIds.add(it.invoiceId)
      })
    }

    if (invoiceIds.size === 0) return

    // 2. Fetch docs for each invoice in parallel
    const allPiDocs: SupplierFile[] = []
    const allSupplierDocs: SupplierFile[] = []

    await Promise.all(Array.from(invoiceIds).map(async (id) => {
      try {
        const data = await api.get<any>(`/documents/proforma-invoice/${id}`)
        const invNum = data?.invoiceNumber || String(id)
        
        // PI Level Docs (Customer POP, Customer PO, Our PI)
        const piFiles = (data?.piFiles || []).map((f: any) => ({
          ...f,
          invoiceNumber: invNum,
          originalInvoiceId: id
        })).filter((f: any) =>
          f.category === 'customer_pop' ||
          f.category === 'customer_po' ||
          f.category === 'our_pi'
        )
        allPiDocs.push(...piFiles)

        // Supplier Level Docs (Our POP, Supplier Invoice, Supplier Bank Info) for THIS PO's supplier
        const section = (data?.suppliers || []).find((s: any) => s.supplierId === po.value.supplierId)
        if (section?.files) {
          const sFiles = section.files.map((f: any) => ({
            ...f,
            invoiceNumber: invNum,
            originalInvoiceId: id
          })).filter((f: any) =>
            f.category === 'supplier_invoice' ||
            f.category === 'supplier_bank_info' ||
            f.category === 'our_pop' ||
            f.category === 'dp' ||
            // Auto-saved PO PDFs (written by PdfController.GeneratePo to <Invoice>/<Supplier>/PO/)
            f.category === 'po'
          )
          allSupplierDocs.push(...sFiles)
        }
      } catch (e) {
        console.warn(`Failed to load docs for invoice ${id}`, e)
      }
    }))

    // 3. Handle duplicates and source identification
    const processDuplicates = (files: SupplierFile[]) => {
      const nameGroups = new Map<string, SupplierFile[]>()
      files.forEach(f => {
        if (!nameGroups.has(f.name)) nameGroups.set(f.name, [])
        // Only add if this specific file from this specific source isn't already there
        if (!nameGroups.get(f.name)!.some(existing => existing.originalInvoiceId === f.originalInvoiceId)) {
          nameGroups.get(f.name)!.push(f)
        }
      })

      const finalFiles: SupplierFile[] = []
      const hasMultipleInvoices = invoiceIds.size > 1

      nameGroups.forEach((group, originalName) => {
        group.forEach(f => {
          // Always show invoice number if there are multiple invoices involved in this PO
          const displayName = hasMultipleInvoices 
            ? `${originalName} (${f.invoiceNumber})`
            : originalName
            
          finalFiles.push({
            ...f,
            displayName
          })
        })
      })
      return finalFiles
    }

    piDocs.value = processDuplicates(allPiDocs)
    supplierDocs.value = processDuplicates(allSupplierDocs)

  } catch {
    supplierDocs.value = []
    piDocs.value = []
  }
}

async function onSupplierDocSelected(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !po.value?.supplierId) return
  
  // Collect all unique invoice IDs linked to this PO
  const invoiceIds = new Set<number>()
  if (po.value.invoiceId) invoiceIds.add(po.value.invoiceId)
  if (enriched.value?.items) {
    enriched.value.items.forEach((it: any) => {
      if (it.invoiceId) invoiceIds.add(it.invoiceId)
    })
  }
  if (invoiceIds.size === 0) return

  uploadingSupplierDoc.value = true
  try {
    const category = uploadCategory.value || 'supplier_invoice'
    
    // Upload to ALL linked invoices as requested
    await Promise.all(Array.from(invoiceIds).map(async (invId) => {
      const form = new FormData()
      form.append('file', file)
      form.append('category', category)
      await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${invId}/supplier/${po.value.supplierId}/upload`, {
        method: 'POST',
        body: form,
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
    }))

    showSnack(`Supplier document uploaded to ${invoiceIds.size} invoices`, 'success')
    await loadSupplierDocs()
  } catch (err: any) {
    showSnack(err?.data?.message || 'Upload failed', 'error')
  } finally {
    uploadingSupplierDoc.value = false
    if (input) input.value = ''
  }
}

async function onPiDocSelected(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  // Collect all unique invoice IDs linked to this PO
  const invoiceIds = new Set<number>()
  if (po.value.invoiceId) invoiceIds.add(po.value.invoiceId)
  if (enriched.value?.items) {
    enriched.value.items.forEach((it: any) => {
      if (it.invoiceId) invoiceIds.add(it.invoiceId)
    })
  }
  if (invoiceIds.size === 0) return

  uploadingPiDoc.value = true
  try {
    const category = uploadPiCategory.value || 'customer_pop'

    // Upload to ALL linked invoices as requested
    await Promise.all(Array.from(invoiceIds).map(async (invId) => {
      const form = new FormData()
      form.append('file', file)
      form.append('category', category)
      await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${invId}/upload`, {
        method: 'POST',
        body: form,
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
    }))

    showSnack(`Document uploaded to ${invoiceIds.size} invoices`, 'success')
    await loadSupplierDocs()
  } catch (err: any) {
    showSnack(err?.data?.message || 'Upload failed', 'error')
  } finally {
    uploadingPiDoc.value = false
    if (input) input.value = ''
  }
}

async function downloadSupplierDoc(name: string, overrideInvoiceId?: number, category?: string) {
  const invId = overrideInvoiceId || po.value?.invoiceId
  if (!invId || !po.value?.supplierId) return
  try {
    const isPiDoc = piDocs.value.some(f => f.name === name && (f.originalInvoiceId === invId || !f.originalInvoiceId))
    const url = isPiDoc
      ? `${config.public.apiBase}/documents/proforma-invoice/${invId}/file`
      : `${config.public.apiBase}/documents/proforma-invoice/${invId}/supplier/${po.value.supplierId}/file`

    const blob = await $fetch<Blob>(url, {
      method: 'GET',
      query: { name, ...(category ? { category } : {}) },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
      responseType: 'blob',
    })
    const blobUrl = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = blobUrl; a.download = name
    document.body.appendChild(a); a.click(); a.remove()
    URL.revokeObjectURL(blobUrl)
  } catch { showSnack('Download failed', 'error') }
}

async function deleteSupplierDoc(name: string, overrideInvoiceId?: number, category?: string) {
  const invId = overrideInvoiceId || po.value?.invoiceId
  if (!invId) return
  if (!confirm(`Delete "${name}"?`)) return
  deletingDoc.value = name
  try {
    const isPiDoc = piDocs.value.some(f => f.name === name && (f.originalInvoiceId === invId || !f.originalInvoiceId))
    const url = isPiDoc
      ? `${config.public.apiBase}/documents/proforma-invoice/${invId}/file`
      : `${config.public.apiBase}/documents/proforma-invoice/${invId}/supplier/${po.value.supplierId}/file`

    await $fetch(url, {
      method: 'DELETE',
      query: { name, ...(category ? { category } : {}) },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Deleted', 'success')
    await loadSupplierDocs()
  } catch { showSnack('Delete failed', 'error') }
  finally { deletingDoc.value = null }
}

// ── Load Data ──
onMounted(async () => {
  try {
    po.value = await api.get(`/purchase-orders/${route.params.id}`)
    // Hydrate the PDF totals form (processingFee / shipping / tax)
    loadTotalsFromPo()
    // Important: wait for enriched trail to identify ALL linked invoices
    await loadEnriched()
    // Then load everything else
    const tasks: Promise<any>[] = [loadImportDetail(), checkLock(), loadSupplierDocs(), loadPresets()]
    if (isAdmin.value) {
      tasks.push(loadAssignedUsers(), loadAllUsers())
    }
    await Promise.all(tasks)
  } catch {}
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

.enriched-table :deep(th) {
  font-weight: bold !important;
  font-size: 0.75rem !important;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 8px 12px !important;
}
.enriched-table .sub-header {
  font-size: 0.7rem !important;
  opacity: 0.7;
  height: 32px !important;
}
.grouped-header {
  background-color: rgba(var(--v-theme-on-surface), 0.05) !important;
}
</style>
