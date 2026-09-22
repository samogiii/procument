<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" class="mr-1 flex-shrink-0" size="small" @click="$router.back()" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">PO {{ po.poNumber || `#${route.params.id}` }}</h1>
      <!-- Company preset picked at PO creation — drives the paying wallet and the PDF branding -->
      <v-chip
        v-if="po.companyPresetName"
        size="small"
        color="primary"
        variant="tonal"
        prepend-icon="mdi-domain"
        :title="po.preferredWalletName ? `Paid from wallet: ${po.preferredWalletName}` : undefined"
      >
        {{ po.companyPresetName }}
      </v-chip>
      <v-chip v-if="isStock" size="small" color="purple" variant="tonal" prepend-icon="mdi-warehouse"
        title="Bought for Our Stock — no customer or Sales Order behind it">
        Stock PO
      </v-chip>
      <v-spacer />
      <v-btn v-if="isStock && po.status === 'Draft'" prepend-icon="mdi-pencil" size="small" variant="tonal" color="primary"
        :to="`/our-inventory/purchase-orders/new?id=${route.params.id}`">
        Edit draft
      </v-btn>
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
        v-if="!isStock && (isAdmin || assignedUsers.some(u => u.userId === authStore.user?.id))"
        prepend-icon="mdi-keyboard-return"
        size="small"
        variant="tonal"
        color="warning"
        :disabled="isTerminalState"
        @click="openReturnDialog"
      >
        Return
      </v-btn>
      <v-btn
        prepend-icon="mdi-cancel"
        size="small"
        variant="tonal"
        color="error"
        :disabled="isTerminalState"
        @click="showCancelDialog = true"
      >
        Cancel PO
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
        <StatCard v-if="isStock" icon="mdi-warehouse" color="purple" label="Ship to (Our Stock)" :value="po.destinationWarehouseName || '—'" class="h-100" />
        <StatCard v-else icon="mdi-file-document-outline" color="info" label="Sales Order" :value="po.invoiceNumber || '—'" class="h-100" />
      </v-col>
    </v-row>

    <v-card class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-timeline-check-outline" class="mr-2" size="20" color="primary" />
        Part Statuses
      </v-card-title>
      <v-card-text>
        <v-table density="compact">
          <thead><tr><th>Part</th><th>Status</th><th v-if="hasInShopParts">In Shop lead time</th></tr></thead>
          <tbody>
            <tr v-for="item in (po.items || [])" :key="item.id">
              <td class="font-weight-medium">{{ item.partNumberName || '—' }}</td>
              <td><v-chip size="small" :color="statusColorForPart(item.status)" variant="tonal">{{ item.displayStatus || item.status || 'Not Started' }}</v-chip></td>
              <td v-if="hasInShopParts">
                <v-text-field v-if="isItemInShop(item)" v-model.number="item.inShopLeadTimeDays" type="number" min="0" max="3650" suffix="days"
                  density="compact" variant="outlined" hide-details style="max-width:150px" :loading="savingLeadTimeId === item.id"
                  @change="saveInShopLeadTime(item)" />
                <span v-else class="text-medium-emphasis">—</span>
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <!-- ── Subject (free-text; editable by anyone who can access this PO) ── -->
    <v-card class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-text-box-outline" class="mr-2" size="20" color="blue" />
        Subject
        <v-spacer />
        <template v-if="!editingSubject">
          <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-pencil" @click="startSubjectEdit">Edit</v-btn>
        </template>
        <template v-else>
          <v-btn size="small" variant="text" class="mr-2" @click="cancelSubjectEdit">Cancel</v-btn>
          <v-btn size="small" variant="flat" color="success" prepend-icon="mdi-content-save" :loading="savingSubject" @click="saveSubject">Save</v-btn>
        </template>
      </v-card-title>
      <v-card-text>
        <v-text-field
          v-model="subjectForm"
          label="Subject"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          :readonly="!editingSubject"
          placeholder="—"
        />
      </v-card-text>
    </v-card>

    <!-- ── Stock PO: admin approval (submitted drafts wait here) ── -->
    <v-card v-if="isStock && isAdmin && po.status === 'Waiting For Admin Approval'" class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-shield-check-outline" class="mr-2" size="20" color="orange" />
        Admin Approval
      </v-card-title>
      <v-card-text>
        <p class="text-body-2 mb-3">
          This Stock PO was submitted for approval. Approving moves it to <strong>Waiting For Supplier Documents</strong>;
          rejecting sends it back to <strong>Draft</strong> so it can be corrected.
        </p>
        <v-text-field v-model="approvalNote" label="Note (required to reject)" variant="outlined" density="compact" hide-details />
        <div class="d-flex gap-2 mt-3">
          <v-btn color="success" variant="flat" prepend-icon="mdi-check" :loading="approving === 'Approved'" :disabled="!!approving" @click="decideApproval('Approved')">Approve</v-btn>
          <v-btn color="error" variant="tonal" prepend-icon="mdi-close" :loading="approving === 'Rejected'" :disabled="!!approving || !approvalNote.trim()" @click="decideApproval('Rejected')">Reject</v-btn>
        </div>
      </v-card-text>
    </v-card>
    <v-alert v-if="isStock && po.status === 'Draft' && po.adminApproval === 'Rejected'" type="error" variant="tonal" class="mb-6" icon="mdi-close-circle">
      <strong>Rejected by admin:</strong> {{ po.adminApprovalNote || 'no note' }} — edit the draft and submit it again.
    </v-alert>

    <!-- ── Stock PO: supplier documents keyed by PO (there is no Sales Order to file them under) ── -->
    <PurchaseOrderDocuments v-if="isStock" :po-id="route.params.id as string" :po-total="po.totalAmount" @changed="loadPo" @notify="showSnack" />

    <!-- ── Stock PO: what has been received into Our Stock ── -->
    <v-card v-if="isStock" class="glass-card mb-6">
      <v-card-title class="d-flex align-center flex-wrap gap-2">
        <v-icon icon="mdi-package-variant-closed-check" class="mr-1" size="20" color="success" />
        Received into Our Stock
        <v-chip v-if="stockReceipts" size="x-small" variant="tonal" :color="stockReceipts.fullyReceived ? 'success' : 'primary'">
          {{ fmtQty(receivedTotal) }} / {{ orderedTotal }}
        </v-chip>
        <v-spacer />
        <v-btn v-if="receivedTotal > 0" size="small" variant="tonal" color="primary" prepend-icon="mdi-file-pdf-box"
          :loading="stockPdf.loading.value === 'grn'" @click="stockPdf.openGrn(route.params.id as string, [], po.poNumber)">
          GRN
        </v-btn>
        <v-btn v-if="isAdmin && stockReceipts && !stockReceipts.fullyReceived && po.adminApproval === 'Approved' && !isTerminalState"
          size="small" variant="tonal" color="success" prepend-icon="mdi-truck-check-outline" @click="showStockReceive = true">
          Receive without track
        </v-btn>
      </v-card-title>
      <v-card-text>
        <v-table v-if="stockReceipts?.lines?.length" density="compact">
          <thead>
            <tr><th>#</th><th>P/N</th><th>Cond.</th><th class="text-end">Ordered</th><th class="text-end">Received</th><th class="text-end">Remaining</th><th>Receipts</th></tr>
          </thead>
          <tbody>
            <tr v-for="l in stockReceipts.lines" :key="l.poItemId">
              <td class="text-medium-emphasis">{{ l.poRef }}</td>
              <td>
                <NuxtLink v-if="l.stockItemId" :to="`/our-inventory/${l.stockItemId}`" class="text-primary text-decoration-none font-weight-medium">{{ l.partNumber }}</NuxtLink>
                <span v-else class="font-weight-medium">{{ l.partNumber }}</span>
              </td>
              <td>{{ l.condition }}</td>
              <td class="text-end">{{ l.qtyOrdered }}</td>
              <td class="text-end">{{ fmtQty(l.qtyReceived) }}</td>
              <td class="text-end" :class="l.qtyRemaining > 0 ? 'font-weight-bold' : 'text-success'">{{ fmtQty(l.qtyRemaining) }}</td>
              <td>
                <div v-for="m in l.movements" :key="m.id" class="text-caption">
                  <span :class="m.qty < 0 ? 'text-error' : ''">{{ m.qty > 0 ? '+' : '' }}{{ fmtQty(m.qty) }}</span>
                  · {{ m.trackNumber ? `track ${m.trackNumber}` : 'manual' }} · {{ m.warehouseName }} · {{ new Date(m.createdAt).toLocaleDateString() }}
                  <span v-if="m.serials?.length" class="text-medium-emphasis">· S/N {{ m.serials.join(', ') }}</span>
                </div>
                <span v-if="!l.movements.length" class="text-caption text-medium-emphasis">—</span>
              </td>
            </tr>
          </tbody>
        </v-table>
        <div class="text-caption text-medium-emphasis mt-2">
          Stock is added automatically when a track number for this PO is accepted in the warehouse.
        </div>
        <v-alert v-if="stockPdf.error.value" type="error" variant="tonal" density="compact" class="mt-2">{{ stockPdf.error.value }}</v-alert>
        <DocPreviewModal
          :open="stockPdf.preview.open.value"
          :blob-url="stockPdf.preview.blobUrl.value"
          :file-name="stockPdf.preview.fileName.value"
          :mime-type="stockPdf.preview.mimeType.value"
          @close="stockPdf.preview.close()"
        />
      </v-card-text>
    </v-card>
    <StockReceiveDialog v-if="isStock" v-model="showStockReceive" :po-id="route.params.id as string" @received="onStockReceived" />

    <!-- Documents are grouped by business owner so users can find the right upload quickly. -->
    <v-card v-if="!isStock" class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-folder-multiple-outline" class="mr-2" size="20" color="primary" />
        PO Document Center
        <v-chip v-if="allPoDocumentCount" size="x-small" class="ml-2" variant="tonal" color="primary">
          {{ allPoDocumentCount }} files
        </v-chip>
        <v-spacer />
        <v-btn size="small" variant="tonal" color="warning" prepend-icon="mdi-file-export-outline" @click="showPrDialog = true">
          Payment Request (PR)
        </v-btn>
      </v-card-title>
      <v-card-text>
        <v-alert type="info" variant="tonal" density="compact" class="mb-4">
          Supplier Invoice and Supplier Bank Info move the PO to <strong>Waiting For PR</strong>. End User Documents are stored without changing the PO status.
        </v-alert>

        <!-- <section class="document-section mb-5">
          <div class="d-flex align-center mb-3">
            <v-icon icon="mdi-account-file-outline" color="info" size="20" class="mr-2" />
            <div>
              <div class="text-subtitle-2 font-weight-bold">Customer & PI Documents</div>
              <div class="text-caption text-medium-emphasis">Documents received from the customer or issued with the PI.</div>
            </div>
          </div>
          <v-row dense>
            <v-col v-for="category in piDocumentCategories" :key="category.key" cols="12" md="4">
              <DocumentCategoryCard
                :category="category"
                :files="documentFiles(category)"
                :can-upload="canUploadDocument(category)"
                :loading="uploadingPiDoc"
                :deleting-file="deletingDoc"
                :can-delete="isAdmin"
                @upload="uploadDocument(category)"
                @download="downloadSupplierDoc($event.name, $event.originalInvoiceId, $event.category)"
                @delete="deleteSupplierDoc($event.name, $event.originalInvoiceId, $event.category)"
              />
            </v-col>
          </v-row>
        </section> -->

        <v-divider class="mb-5" />

        <section class="document-section">
          <div class="d-flex align-center mb-3">
            <v-icon icon="mdi-truck-delivery-outline" color="success" size="20" class="mr-2" />
            <div>
              <div class="text-subtitle-2 font-weight-bold">Supplier & Payment Documents</div>
              <div class="text-caption text-medium-emphasis">Supplier documents, End User documents, generated PO/DP files, and payment proof.</div>
            </div>
          </div>
          <v-row dense>
            <v-col v-for="category in supplierDocumentCategories" :key="category.key" cols="12" md="4">
              <DocumentCategoryCard
                :category="category"
                :files="documentFiles(category)"
                :can-upload="canUploadDocument(category)"
                :loading="uploadingSupplierDoc"
                :deleting-file="deletingDoc"
                :can-delete="isAdmin"
                @upload="uploadDocument(category)"
                @download="downloadSupplierDoc($event.name, $event.originalInvoiceId, $event.category)"
                @delete="deleteSupplierDoc($event.name, $event.originalInvoiceId, $event.category)"
              />
            </v-col>
          </v-row>
        </section>

        <input ref="piDocInputRef" type="file" class="d-none" @change="onPiDocSelected" />
        <input ref="supplierDocInputRef" type="file" class="d-none" @change="onSupplierDocSelected" />
      </v-card-text>
    </v-card>

    <!-- ── Payment request review ── -->
    <v-card class="glass-card mb-6" v-if="isAdmin">
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
          <div class="text-caption mt-1">Delete the rejected PR, correct it, and download a new PR.</div>
        </v-alert>
        <div v-else-if="po.paymentStatus === 'Submitted'" class="text-body-2">
          Payment has been submitted and is pending final acceptance.
        </div>
        <div v-else class="text-body-2 text-medium-emphasis">
          Awaiting payment submission from the Payment department.
        </div>
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
                <v-text-field v-model="importForm.beneficiary" label="Beneficiary" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="importForm.reference" label="Reference" variant="outlined" density="compact" hide-details :readonly="!editingImport" class="mb-2" />
              </v-col>
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
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-package-variant-closed" class="mr-2" size="20" />
        Line Items &amp; Tracking
        <v-spacer />
        <v-btn v-if="isAdmin" size="x-small" variant="tonal" color="secondary" prepend-icon="mdi-barcode-scan" @click="openBulkAddTrack">
          Bulk Add Track
        </v-btn>
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

                <!-- Unit Price — Admin/SuperAdmin can click pencil to edit inline -->
                <td>
                  <div v-if="isAdmin && editingPriceItemId === item.id" class="d-flex align-center gap-1" style="min-width:160px">
                    <v-text-field
                      v-model.number="editPriceValue"
                      type="number"
                      prefix="$"
                      density="compact"
                      variant="outlined"
                      hide-details
                      style="max-width:110px"
                      @keyup.enter="savePriceOverride(item)"
                      @keyup.esc="cancelPriceEdit"
                      autofocus
                    />
                    <v-btn icon size="x-small" color="success" variant="tonal" :loading="savingPrice" @click="savePriceOverride(item)">
                      <v-icon>mdi-check</v-icon>
                    </v-btn>
                    <v-btn icon size="x-small" variant="text" @click="cancelPriceEdit">
                      <v-icon>mdi-close</v-icon>
                    </v-btn>
                  </div>
                  <div v-else class="d-flex align-center gap-1">
                    <span>${{ formatPrice(item.unitPrice) }}</span>
                    <v-btn v-if="isAdmin" icon size="x-small" variant="text" color="secondary" @click="openPriceEdit(item)" title="Override price">
                      <v-icon size="14">mdi-pencil</v-icon>
                    </v-btn>
                  </div>
                </td>

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
                    <div v-for="t in item.trackNumbers" :key="t.id" class="mb-3">
                      <!-- Track header row -->
                      <div class="d-flex align-center gap-2 mb-2 px-1">
                        <v-icon icon="mdi-barcode-scan" size="14" color="primary" />
                        <span class="font-weight-bold text-primary text-body-2">{{ t.trackNumber }}</span>
                        <span v-if="t.carrier" class="text-caption text-medium-emphasis">· {{ t.carrier }}</span>
                        <v-chip
                          :color="t.status === 'Active' ? 'success' : 'error'"
                          size="x-small"
                          variant="tonal"
                          class="ml-1"
                        >{{ t.status }}</v-chip>
                        <v-chip v-if="t.warehouseName" size="x-small" variant="tonal" color="secondary">
                          <v-icon icon="mdi-home-city-outline" size="10" class="mr-1" />{{ t.warehouseName }}
                        </v-chip>
                        <v-spacer />
                        <v-btn icon size="x-small" variant="text" color="error" @click="deleteTrack(item, t.id)">
                          <v-icon icon="mdi-delete-outline" size="16" />
                        </v-btn>
                      </div>

                      <!-- Inventory status panel -->
                      <v-card
                        variant="outlined"
                        rounded="lg"
                        class="mb-1"
                        :color="inventoryStatusColor(t)"
                      >
                        <v-card-text class="pa-3">
                          <!-- No submission yet -->
                          <div v-if="!t.inventoryItems?.length" class="text-caption text-medium-emphasis d-flex align-center gap-1">
                            <v-icon icon="mdi-clock-outline" size="14" />
                            Waiting for inventory submission
                          </div>

                          <!-- Inventory submitted -->
                          <template v-else>
                            <div v-for="inv in t.inventoryItems" :key="inv.id">
                              <!-- Qty summary -->
                              <div class="d-flex align-center gap-3 flex-wrap mb-2">
                                <span class="text-caption">
                                  <strong>Expected:</strong> {{ inv.expectedQty ?? '—' }}
                                </span>
                                <span class="text-caption">
                                  <strong>Received:</strong>
                                  <span :class="inv.actualQty !== inv.expectedQty ? 'text-error font-weight-bold' : 'text-success'">
                                    {{ inv.actualQty ?? '—' }}
                                  </span>
                                </span>
                                <v-chip
                                  v-if="inv.actualQty != null && inv.actualQty !== inv.expectedQty"
                                  size="x-small"
                                  color="error"
                                  variant="tonal"
                                >
                                  <v-icon icon="mdi-alert" size="12" class="mr-1" />
                                  Qty mismatch ({{ inv.expectedQty - inv.actualQty > 0 ? '-' : '+' }}{{ Math.abs((inv.expectedQty ?? 0) - (inv.actualQty ?? 0)) }})
                                </v-chip>
                                <v-chip
                                  v-if="inv.isAvailable === false"
                                  size="x-small"
                                  color="error"
                                  variant="tonal"
                                >
                                  <v-icon icon="mdi-close-circle" size="12" class="mr-1" />
                                  Not Available
                                </v-chip>
                                <v-chip
                                  v-if="inv.isAvailable === true"
                                  size="x-small"
                                  color="success"
                                  variant="tonal"
                                >Available</v-chip>
                              </div>

                              <!-- Review status + actions -->
                              <div class="d-flex align-center gap-2 flex-wrap">
                                <v-chip
                                  :color="inv.status === 'Accepted' ? 'success' : inv.status === 'Rejected' ? 'error' : 'warning'"
                                  size="x-small"
                                  variant="flat"
                                >
                                  <v-icon
                                    :icon="inv.status === 'Accepted' ? 'mdi-check-circle' : inv.status === 'Rejected' ? 'mdi-close-circle' : 'mdi-clock-outline'"
                                    size="12"
                                    class="mr-1"
                                  />
                                  {{ inv.status }}
                                </v-chip>
                                <span v-if="inv.reviewNote" class="text-caption text-medium-emphasis">
                                  "{{ inv.reviewNote }}"
                                </span>

                                <!-- Accept / Reject buttons (Admin + Expert) -->
                                <template v-if="inv.status === 'Pending' && (authStore.isAdmin || authStore.user?.role === 'Expert')">
                                  <v-spacer />
                                  <v-btn
                                    size="x-small"
                                    color="success"
                                    variant="flat"
                                    prepend-icon="mdi-check"
                                    :loading="reviewingItem === inv.id + '-accept'"
                                    @click="reviewInventoryItem(t.id, inv.id, 'Accept')"
                                  >Accept</v-btn>
                                  <v-btn
                                    size="x-small"
                                    color="error"
                                    variant="tonal"
                                    prepend-icon="mdi-close"
                                    :loading="reviewingItem === inv.id + '-reject'"
                                    @click="openRejectNoteDialog(t.id, inv.id)"
                                  >Reject</v-btn>
                                </template>
                              </div>

                              <!-- Documents from inventory -->
                              <div v-if="t.inventoryDocs?.length" class="mt-2">
                                <div class="text-caption text-medium-emphasis mb-1">Documents from inventory:</div>
                                <div class="d-flex flex-wrap gap-1">
                                  <v-chip
                                    v-for="doc in t.inventoryDocs"
                                    :key="doc.id"
                                    size="x-small"
                                    variant="tonal"
                                    :prepend-icon="docPreview.isPreviewable(doc.originalFileName, doc.mimeType) ? 'mdi-eye-outline' : 'mdi-file-outline'"
                                    class="cursor-pointer"
                                    @click="downloadInventoryDoc(doc.id, doc.originalFileName, doc.mimeType)"
                                  >
                                    {{ doc.originalFileName }}
                                  </v-chip>
                                </div>
                              </div>
                            </div>
                          </template>
                        </v-card-text>
                      </v-card>
                    </div>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <!-- Reject Note Dialog -->
    <v-dialog v-model="rejectNoteDialog" max-width="440" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">
          <v-icon icon="mdi-close-circle-outline" color="error" class="mr-2" />
          Reject Part
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-alert type="warning" density="compact" variant="tonal" class="mb-3">
            This part will be marked <strong>Rejected</strong> and the PO will be flagged as <strong>Issue</strong>.
          </v-alert>
          <v-textarea
            v-model="rejectNoteValue"
            label="Rejection Note (visible to Inventory user)"
            variant="outlined"
            density="compact"
            rows="3"
            auto-grow
            placeholder="e.g. Wrong part number received, quantity mismatch, damaged item..."
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="rejectNoteDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="reviewingItem != null" @click="confirmRejectWithNote">Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Add Track Number Dialog -->
    <v-dialog v-model="showAddTrackDialog" max-width="460" persistent>
      <v-card>
        <v-card-title class="text-h6">Add Tracking Number</v-card-title>
        <v-card-text>
          <v-text-field v-model="trackForm.trackNumber" label="Track Number" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="trackForm.carrier" label="Carrier (e.g. FedEx, DHL)" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="trackForm.notes" label="Notes" variant="outlined" density="compact" class="mb-3" />
          <v-autocomplete
            v-model="trackForm.warehouseId"
            :items="trackWarehouses"
            item-title="name"
            item-value="id"
            label="Destination Warehouse"
            variant="outlined"
            density="compact"
            clearable
            prepend-inner-icon="mdi-home-city-outline"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showAddTrackDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="savingTrack" :disabled="!trackForm.trackNumber.trim()" @click="addTrack">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Bulk Add Track Number Dialog -->
    <v-dialog v-model="showBulkTrackDialog" max-width="600" persistent scrollable>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">
          <v-icon icon="mdi-barcode-scan" class="mr-2" color="secondary" />
          Bulk Add Track Number
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-caption text-medium-emphasis mb-3">Select parts and assign one track number to all of them.</p>

          <!-- Track details -->
          <v-text-field v-model="bulkTrackForm.trackNumber" label="Track Number *" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="bulkTrackForm.carrier" label="Carrier (e.g. FedEx, DHL)" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="bulkTrackForm.notes" label="Notes" variant="outlined" density="compact" class="mb-3" />
          <v-autocomplete
            v-model="bulkTrackForm.warehouseId"
            :items="trackWarehouses"
            item-title="name"
            item-value="id"
            label="Destination Warehouse"
            variant="outlined"
            density="compact"
            clearable
            prepend-inner-icon="mdi-home-city-outline"
            class="mb-4"
          />

          <!-- Part selection -->
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">SELECT PARTS</div>
          <v-list density="compact" class="border rounded">
            <v-list-item
              v-for="item in (po?.items || [])"
              :key="item.id"
              :value="item.id"
            >
              <template #prepend>
                <v-checkbox
                  v-model="bulkSelectedItemIds"
                  :value="item.id"
                  density="compact"
                  hide-details
                  color="secondary"
                />
              </template>
              <v-list-item-title class="text-body-2">{{ item.partNumberName || '—' }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption">Qty: {{ item.qty }}</v-list-item-subtitle>
            </v-list-item>
          </v-list>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="showBulkTrackDialog = false">Cancel</v-btn>
          <v-btn
            color="secondary"
            variant="flat"
            :loading="savingBulkTrack"
            :disabled="!bulkTrackForm.trackNumber.trim() || !bulkSelectedItemIds.length"
            @click="addBulkTrack"
          >
            Add to {{ bulkSelectedItemIds.length }} part(s)
          </v-btn>
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

    <!-- Document Preview Modal -->
    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />

    <!-- Return Items to Procurement Dialog -->
    <v-dialog v-model="showReturnDialog" max-width="560" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-keyboard-return" class="mr-2" color="warning" />
          Return Items to Procurement
        </v-card-title>
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            Enter how many units of each item to return. Items with 0 are skipped.
            Partial quantities reduce the PO line; the returned units go back to the procurement pool.
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

          <div class="text-caption font-weight-bold text-uppercase mb-2">Return Quantities</div>
          <v-table density="compact" class="border rounded">
            <thead>
              <tr>
                <th class="text-left text-caption">Part Number</th>
                <th class="text-center text-caption" style="width:90px">In PO</th>
                <th class="text-center text-caption" style="width:120px">Return Qty</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in po.items" :key="item.id">
                <td class="text-body-2">{{ item.partNumberName }}</td>
                <td class="text-center text-body-2">{{ item.qty }}</td>
                <td class="text-center py-1">
                  <v-text-field
                    v-model.number="returnForm.itemQtys[item.id]"
                    type="number"
                    :min="0"
                    :max="item.qty"
                    variant="outlined"
                    density="compact"
                    hide-details
                    style="max-width:90px"
                    class="mx-auto"
                  />
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showReturnDialog = false">Cancel</v-btn>
          <v-btn
            color="warning"
            variant="flat"
            :loading="returning"
            :disabled="!returnForm.reason.trim() || !hasAnyReturnQty"
            @click="returnPo"
          >Confirm Return</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PoPdfGenerator v-model="showPdf" :po-id="String(route.params.id)" />
    <PaymentRequestPdfGenerator v-model="showPrDialog" :po-id="String(route.params.id)" :po="po" :import-detail="importForm" :enriched="enriched" />

    <!-- ── Cancel PO Confirmation ── -->
    <v-dialog v-model="showCancelDialog" max-width="480">
      <v-card>
        <v-card-title class="d-flex align-center gap-2 pa-4">
          <v-icon icon="mdi-cancel" color="error" />
          Cancel Purchase Order
        </v-card-title>
        <v-card-text class="px-4 pb-2">
          <v-alert type="warning" variant="tonal" class="mb-3">
            This will cancel <strong>{{ po.poNumber }}</strong> and release all items back to Order Items so a new PO can be created.
            The linked invoice will be marked <strong>PO Cancelled</strong>.
          </v-alert>
          <p class="text-body-2 text-medium-emphasis">This action cannot be undone. Are you sure you want to cancel this PO?</p>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showCancelDialog = false">Go Back</v-btn>
          <v-btn color="error" variant="flat" :loading="cancelling" @click="cancelPo">Confirm Cancel</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const po = ref<any>({})

// Show the PO number instead of the raw id in the breadcrumb trail
const { setBreadcrumbLabel } = useBreadcrumb()
watchEffect(() => setBreadcrumbLabel(po.value?.poNumber))

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const savingLeadTimeId = ref<number | null>(null)

/** Stock POs buy for Our Stock: no customer, Sales Order, return-to-procurement or EndUser / In Shop routes. */
const isStock = computed(() => po.value?.origin === 'Stock')
const STOCK_HIDDEN_STATUSES = new Set(['Not Started', 'Sourcing', 'EndUser', 'In Shop'])

const allPoStatuses = [
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
]
const poStatuses = computed(() => isStock.value ? allPoStatuses.filter(s => !STOCK_HIDDEN_STATUSES.has(s.value)) : allPoStatuses)

function statusColorForPart(status: string) {
  return allPoStatuses.find(s => s.value.toLowerCase() === (status || '').toLowerCase())?.color || 'grey'
}

function isItemInShop(item: any) {
  return (item?.status || '').toLowerCase() === 'in shop'
}

const hasInShopParts = computed(() => (po.value?.items || []).some(isItemInShop))

async function saveInShopLeadTime(item: any) {
  savingLeadTimeId.value = item.id
  try {
    const result = await api.patch<any>(`/purchase-orders/items/${item.id}/in-shop`, { days: Number(item.inShopLeadTimeDays) || 0 })
    Object.assign(item, result)
    showSnack(`Lead time saved for ${item.partNumberName || 'part'}`, 'success')
  } catch (e: any) { showSnack(e?.data?.message || 'Failed to save lead time', 'error') }
  finally { savingLeadTimeId.value = null }
}

const isAdmin = computed(() => authStore.isAdmin)
const isSuperAdmin = computed(() => authStore.isSuperAdmin)
const showPdf = ref(false)
const showPrDialog = ref(false)

const isTerminalState = computed(() =>
  ['Completed', 'Cancelled', 'Returned'].includes(po.value.status)
)

// ── Price override (SuperAdmin only) ──
const editingPriceItemId = ref<number | null>(null)
const editPriceValue = ref<number>(0)
const savingPrice = ref(false)

function openPriceEdit(item: any) {
  editingPriceItemId.value = item.id
  editPriceValue.value = item.unitPrice
}

function cancelPriceEdit() {
  editingPriceItemId.value = null
}

async function savePriceOverride(item: any) {
  if (savingPrice.value) return
  savingPrice.value = true
  try {
    await api.patch(`/purchase-orders/items/${item.id}/price`, { unitPrice: editPriceValue.value })
    editingPriceItemId.value = null
    await loadPo()
  } catch (e: any) {
    snackbar.value = { show: true, message: e?.data?.message || 'Failed to update price', color: 'error' }
  } finally {
    savingPrice.value = false
  }
}

// ── Cancel PO Workflow ──
const showCancelDialog = ref(false)
const cancelling = ref(false)

async function cancelPo() {
  cancelling.value = true
  try {
    await api.patch(`/purchase-orders/${po.value.id}/status`, { status: 'Cancelled' })
    showCancelDialog.value = false
    await loadPo()
    showSnack('Purchase Order cancelled. Items are back in Order Items.', 'success')
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to cancel PO', 'error')
  } finally {
    cancelling.value = false
  }
}

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
  itemQtys: {} as Record<number, number>
})

const hasAnyReturnQty = computed(() =>
  Object.values(returnForm.value.itemQtys).some(q => q > 0)
)

function openReturnDialog() {
  const qtys: Record<number, number> = {}
  for (const item of (po.value?.items ?? [])) qtys[item.id] = 0
  returnForm.value = { reason: '', itemQtys: qtys }
  showReturnDialog.value = true
}

async function returnPo() {
  if (!returnForm.value.reason.trim()) {
    showSnack('Please provide a reason for return', 'warning')
    return
  }
  // Build itemQtys, clamping to [1, item.qty] and skipping zeros
  const itemQtys: Record<number, number> = {}
  for (const item of (po.value?.items ?? [])) {
    const raw = returnForm.value.itemQtys[item.id] ?? 0
    const qty = Math.min(Math.max(Math.floor(raw), 0), item.qty)
    if (qty > 0) itemQtys[item.id] = qty
  }
  if (Object.keys(itemQtys).length === 0) {
    showSnack('Enter at least one return quantity', 'warning')
    return
  }
  returning.value = true
  try {
    const res = await api.post<ReturnPOResponse>(`/purchase-orders/${route.params.id}/return`, {
      reason: returnForm.value.reason,
      itemQtys
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

// ── Stock PO: approval + receipts ──
const approvalNote = ref('')
const approving = ref<'' | 'Approved' | 'Rejected'>('')
async function decideApproval(decision: 'Approved' | 'Rejected') {
  approving.value = decision
  try {
    await api.patch(`/purchase-orders/${route.params.id}/admin-approval`, { decision, note: approvalNote.value.trim() || null })
    approvalNote.value = ''
    showSnack(decision === 'Approved' ? 'Stock PO approved' : 'Stock PO sent back to draft', decision === 'Approved' ? 'success' : 'warning')
    await loadPo()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not save the decision', 'error')
  } finally {
    approving.value = ''
  }
}

const stockReceipts = ref<any>(null)
const stockPdf = useStockPdf()
const showStockReceive = ref(false)
const fmtQty = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })
const orderedTotal = computed(() => (stockReceipts.value?.lines || []).reduce((s: number, l: any) => s + l.qtyOrdered, 0))
const receivedTotal = computed(() => (stockReceipts.value?.lines || []).reduce((s: number, l: any) => s + Number(l.qtyReceived), 0))
async function loadStockReceipts() {
  try { stockReceipts.value = await api.get<any>(`/our-inventory/purchase-orders/${route.params.id}/receipts`) }
  catch { stockReceipts.value = null }
}
// The PO is (re)loaded from several places; refresh receipts whenever a Stock PO arrives.
watch(po, (v) => { if (v?.origin === 'Stock') loadStockReceipts() })

async function onStockReceived(summary: any) {
  stockReceipts.value = summary
  showSnack(summary?.fullyReceived ? 'All lines received into stock' : 'Stock received', 'success')
  await loadPo()
}

async function loadPo() {
  try {
    po.value = await api.get(`/purchase-orders/${route.params.id}`)
    // Hydrate the PDF totals form from the PO response (processingFee / shipping / tax)
    loadTotalsFromPo()
    // Hydrate the Subject field
    if (!editingSubject.value) subjectForm.value = po.value?.subject || ''
  } catch {}
}

// ── Subject (free-text) ──
const subjectForm = ref('')
const editingSubject = ref(false)
const savingSubject = ref(false)

function startSubjectEdit() {
  subjectForm.value = po.value?.subject || ''
  editingSubject.value = true
}
function cancelSubjectEdit() {
  subjectForm.value = po.value?.subject || ''
  editingSubject.value = false
}
async function saveSubject() {
  savingSubject.value = true
  try {
    const saved = await api.patch<any>(`/purchase-orders/${route.params.id}/subject`, {
      subject: subjectForm.value || null,
    })
    subjectForm.value = saved?.subject || ''
    if (po.value) po.value.subject = saved?.subject || null
    editingSubject.value = false
    showSnack('Subject saved', 'success')
  } catch {
    showSnack('Failed to save subject', 'error')
  } finally {
    savingSubject.value = false
  }
}

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('po', entityId)

const poStatusColor = computed(() => {
  const found = allPoStatuses.find(s => s.value === po.value.status)
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
    const allowed = ['GHS', 'MOR', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM','AZA', 'SDR']
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
  beneficiary: '', reference: '', bankName: '', bankAccountNumber: '', bankAddress: '',
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

  const blob = await $fetch<Blob>(`${api.baseURL}/pdf/dp`, {
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
  await $fetch(`${api.baseURL}/documents/proforma-invoice/${po.value.invoiceId}/supplier/${po.value.supplierId}/upload`, {
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
const trackForm = ref({ trackNumber: '', carrier: '', notes: '', warehouseId: null as number | null })
const trackWarehouses = ref<any[]>([])

// Bulk Add Track
const showBulkTrackDialog = ref(false)
const savingBulkTrack = ref(false)
const bulkSelectedItemIds = ref<number[]>([])
const bulkTrackForm = ref({ trackNumber: '', carrier: '', notes: '', warehouseId: null as number | null })

function toggleItemExpand(id: number) {
  if (expandedItems.value.has(id)) {
    expandedItems.value.delete(id)
  } else {
    expandedItems.value.add(id)
    // Refresh inventory data for all track numbers of this item when expanding
    const item = (po.value?.items || []).find((i: any) => i.id === id)
    for (const t of (item?.trackNumbers || [])) {
      loadInventoryDataForTrack(t.id)
    }
  }
  expandedItems.value = new Set(expandedItems.value)
}

async function openAddTrack(poItemId: number) {
  addTrackItemId.value = poItemId
  trackForm.value = { trackNumber: '', carrier: '', notes: '', warehouseId: null }
  // Load warehouses linked to the current PO's company preset
  try {
    const presetId = po.value?.companyPresetId
    if (presetId) {
      trackWarehouses.value = await api.get(`/companypresets/${presetId}/warehouses`)
    } else {
      trackWarehouses.value = await api.get('/warehouses')
    }
  } catch {
    trackWarehouses.value = await api.get('/warehouses').catch(() => [])
  }
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

async function openBulkAddTrack() {
  bulkTrackForm.value = { trackNumber: '', carrier: '', notes: '', warehouseId: null }
  bulkSelectedItemIds.value = []
  try {
    const presetId = po.value?.companyPresetId
    if (presetId) {
      trackWarehouses.value = await api.get(`/companypresets/${presetId}/warehouses`)
    } else {
      trackWarehouses.value = await api.get('/warehouses')
    }
  } catch {
    trackWarehouses.value = await api.get('/warehouses').catch(() => [])
  }
  showBulkTrackDialog.value = true
}

async function addBulkTrack() {
  if (!bulkTrackForm.value.trackNumber.trim() || !bulkSelectedItemIds.value.length) return
  savingBulkTrack.value = true
  try {
    for (const itemId of bulkSelectedItemIds.value) {
      const newTrack = await api.post<any>(`/purchase-orders/items/${itemId}/track-numbers`, bulkTrackForm.value)
      const poItem = (po.value.items || []).find((i: any) => i.id === itemId)
      if (poItem) {
        if (!poItem.trackNumbers) poItem.trackNumbers = []
        poItem.trackNumbers.unshift(newTrack)
      }
    }
    showBulkTrackDialog.value = false
    showSnack(`Track number added to ${bulkSelectedItemIds.value.length} part(s)`, 'success')
  } catch {
    showSnack('Failed to add bulk track number', 'error')
  } finally {
    savingBulkTrack.value = false
  }
}

// ── Inventory Review ──
const reviewingItem = ref<string | null>(null)
const rejectNoteDialog = ref(false)
const rejectNoteValue = ref('')
const pendingRejectTrackId = ref<number | null>(null)
const pendingRejectItemId = ref<number | null>(null)

function openRejectNoteDialog(trackId: number, itemId: number) {
  pendingRejectTrackId.value = trackId
  pendingRejectItemId.value = itemId
  rejectNoteValue.value = ''
  rejectNoteDialog.value = true
}

async function confirmRejectWithNote() {
  if (!pendingRejectTrackId.value || !pendingRejectItemId.value) return
  rejectNoteDialog.value = false
  await reviewInventoryItem(pendingRejectTrackId.value, pendingRejectItemId.value, 'Reject', rejectNoteValue.value)
}

function inventoryStatusColor(track: any) {
  const items = track.inventoryItems || []
  if (!items.length) return undefined
  if (items.some((i: any) => i.status === 'Rejected')) return 'error'
  if (items.some((i: any) => i.status === 'Accepted')) return 'success'
  return 'warning'
}

async function reviewInventoryItem(trackId: number, itemId: number, action: 'Accept' | 'Reject', note?: string) {
  const key = `${itemId}-${action.toLowerCase()}`
  reviewingItem.value = key
  try {
    await api.post(`/shipping/track-numbers/${trackId}/items/${itemId}/review`, { action, note: note || null })
    // Refresh inventory items for this track
    await loadInventoryDataForTrack(trackId)
    showSnack(`Part ${action === 'Accept' ? 'accepted' : 'rejected'} successfully`, action === 'Accept' ? 'success' : 'warning')
  } catch {
    showSnack('Review failed', 'error')
  } finally {
    reviewingItem.value = null
  }
}

async function loadInventoryDataForTrack(trackId: number) {
  try {
    const trackData = await api.get<any>(`/shipping/track-numbers/${trackId}/review`).catch(() => null)
    if (!trackData) return
    for (const item of (po.value?.items || [])) {
      const t = (item.trackNumbers || []).find((tn: any) => tn.id === trackId)
      if (t) {
        t.inventoryItems = trackData.items || []
        t.inventoryDocs = trackData.documents || []
        t.status = trackData.status || t.status
        t.warehouseName = t.warehouseName || trackData.warehouseName
        break
      }
    }
  } catch { /* silent */ }
}

async function loadAllInventoryData() {
  const allTrackIds: number[] = []
  for (const item of (po.value?.items || [])) {
    for (const t of (item.trackNumbers || [])) {
      allTrackIds.push(t.id)
    }
  }
  if (!allTrackIds.length) return
  await Promise.allSettled(allTrackIds.map(id => loadInventoryDataForTrack(id)))
}

function downloadInventoryDoc(docId: number, fileName = 'document', mimeType?: string) {
  docPreview.preview(`/shipping/documents/${docId}/file`, fileName, mimeType)
}

const enriched = ref<any>(null)

async function loadEnriched() {
  try {
    enriched.value = await api.get(`/purchase-orders/${route.params.id}/enriched`)
  } catch {}
}

// ── Supplier Documents ──
type SupplierFile = { name: string; category: string; size: number; modifiedAt: string; invoiceNumber?: string; originalInvoiceId?: number; displayName?: string }
type PoDocumentCategory = {
  key: string
  title: string
  description: string
  source: 'pi' | 'supplier'
  upload: 'admin' | 'all' | 'none'
  icon: string
  color: string
  readOnlyNote?: string
}
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

const piDocumentCategories: PoDocumentCategory[] = [
  { key: 'customer_pop', title: 'Customer POP', description: 'Customer proof of payment', source: 'pi', upload: 'admin', icon: 'mdi-account-cash-outline', color: 'primary' },
  { key: 'customer_po', title: 'Customer PO', description: 'Purchase order received from customer', source: 'pi', upload: 'admin', icon: 'mdi-file-sign', color: 'secondary' },
  { key: 'our_pi', title: 'Our PI', description: 'Proforma invoice issued to customer', source: 'pi', upload: 'admin', icon: 'mdi-file-document-outline', color: 'info' },
]

const supplierDocumentCategories: PoDocumentCategory[] = [
  { key: 'supplier_invoice', title: 'Supplier Invoice', description: 'Invoice received from supplier', source: 'supplier', upload: 'all', icon: 'mdi-receipt-text-outline', color: 'primary' },
  { key: 'supplier_bank_info', title: 'Supplier Bank Info', description: 'Supplier payment instructions', source: 'supplier', upload: 'all', icon: 'mdi-bank-outline', color: 'success' },
  { key: 'end_user_document', title: 'End User Document', description: 'End-user declaration or supporting file', source: 'supplier', upload: 'all', icon: 'mdi-account-file-text-outline', color: 'deep-purple' },
  { key: 'our_pop', title: 'Our POP', description: 'Payment proof sent to supplier', source: 'supplier', upload: 'none', icon: 'mdi-cash-check', color: 'success', readOnlyNote: 'Uploaded from Payment' },
  { key: 'dp', title: 'PR Document', description: 'Generated payment request document', source: 'supplier', upload: 'none', icon: 'mdi-file-certificate-outline', color: 'warning', readOnlyNote: 'Generated by the system' },
  { key: 'po', title: 'Generated PO', description: 'Downloaded purchase-order PDF', source: 'supplier', upload: 'none', icon: 'mdi-file-pdf-box', color: 'error', readOnlyNote: 'Generated when the PO is downloaded' },
]

const allPoDocumentCount = computed(() => piDocs.value.length + supplierDocs.value.length)

function documentFiles(category: PoDocumentCategory) {
  const files = category.source === 'pi' ? piDocs.value : supplierDocs.value
  return files.filter(file => file.category === category.key)
}

function canUploadDocument(category: PoDocumentCategory) {
  if (category.upload === 'none') return false
  if (category.upload === 'admin' && !isAdmin.value) return false
  return !!po.value?.invoiceId && (category.source === 'pi' || !!po.value?.supplierId)
}

function uploadDocument(category: PoDocumentCategory) {
  if (category.source === 'pi') triggerPiUpload(category.key)
  else triggerUpload(category.key)
}

function triggerUpload(category: string) {
  uploadCategory.value = category
  supplierDocInputRef.value?.click()
}

function triggerPiUpload(category: string) {
  uploadPiCategory.value = category
  piDocInputRef.value?.click()
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
            f.category === 'end_user_document' ||
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
      await $fetch(`${api.baseURL}/documents/proforma-invoice/${invId}/supplier/${po.value.supplierId}/upload`, {
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
      await $fetch(`${api.baseURL}/documents/proforma-invoice/${invId}/upload`, {
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
      ? `${api.baseURL}/documents/proforma-invoice/${invId}/file`
      : `${api.baseURL}/documents/proforma-invoice/${invId}/supplier/${po.value.supplierId}/file`

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
      ? `${api.baseURL}/documents/proforma-invoice/${invId}/file`
      : `${api.baseURL}/documents/proforma-invoice/${invId}/supplier/${po.value.supplierId}/file`

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
    // Hydrate the Subject field
    if (!editingSubject.value) subjectForm.value = po.value?.subject || ''
    // Important: wait for enriched trail to identify ALL linked invoices
    await loadEnriched()
    // Then load everything else
    const tasks: Promise<any>[] = [loadImportDetail(), checkLock(), loadSupplierDocs(), loadPresets()]
    if (isAdmin.value) {
      tasks.push(loadAssignedUsers(), loadAllUsers())
    }
    await Promise.all(tasks)
    // Load inventory submission data for all track numbers (async, non-blocking)
    loadAllInventoryData()
  } catch {}
})

// ── Status ──
async function changeStatus(newStatus: string) {
  if (newStatus === po.value.status) return

  try {
    await api.patch(`/purchase-orders/${po.value.id}/status`, { status: newStatus })
    po.value.status = newStatus
    if (newStatus === 'In Shop' || newStatus === 'EndUser') po.value.fulfillmentMode = newStatus
    po.value = await api.get(`/purchase-orders/${route.params.id}`)
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

