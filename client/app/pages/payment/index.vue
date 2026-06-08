<template>
  <div>
    <div class="d-flex align-center mb-4 mb-md-6">
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Payment Withdraw</h1>
      <v-spacer />
      <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-swap-horizontal" class="mr-2" @click="openCreateTransfer">New Wallet Transfer</v-btn>
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="loadAll">Refresh</v-btn>
    </div>

    <v-card class="glass-card">
      <v-tabs v-model="activeTab" bg-color="transparent" color="primary">
        <v-tab value="acceptance">
          <v-icon start size="18">mdi-shield-check-outline</v-icon>
          Payment Acceptance
          <v-chip v-if="acceptancePoBadge + acceptanceWtBadge" size="x-small" color="warning" variant="tonal" class="ml-2">
            {{ acceptancePoBadge + acceptanceWtBadge }}
          </v-chip>
        </v-tab>
        <v-tab value="withdraw">
          <v-icon start size="18">mdi-cash-multiple</v-icon>
          Withdraw Panel
          <v-chip v-if="withdrawPoBadge + withdrawWtBadge" size="x-small" color="primary" variant="tonal" class="ml-2">
            {{ withdrawPoBadge + withdrawWtBadge }}
          </v-chip>
        </v-tab>
      </v-tabs>

      <v-divider />

      <v-tabs-window v-model="activeTab">
        <!-- ═══════ TAB 1: Payment Acceptance ═══════ -->
        <v-tabs-window-item value="acceptance">
          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">
              Review supporting documents and accept payment requests from the procurement team.
            </p>

            <!-- PO Payment Requests -->
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-file-document-outline" size="18" class="mr-1" color="primary" />
              PO Payment Requests
              <v-chip size="x-small" color="warning" variant="tonal" class="ml-2">{{ acceptanceQueue.length }}</v-chip>
            </div>
            <v-data-table
              :headers="acceptanceHeaders"
              :items="acceptanceQueue"
              :loading="loading"
              density="comfortable"
              :items-per-page="25"
              hover
              class="cursor-pointer mb-6"
              @click:row="(_, row) => openPo(row.item, 'accept')"
            >
              <template #item.totalAmount="{ item }">
                ${{ formatPrice(item.totalAmount) }}
              </template>
              <template #item.preferredWalletName="{ item }">
                <div v-if="item.preferredWalletName">
                  <div class="text-caption font-weight-medium">{{ item.preferredWalletName }}</div>
                  <div class="text-caption text-medium-emphasis">{{ item.preferredWalletCompany }}</div>
                </div>
                <span v-else class="text-caption text-medium-emphasis">—</span>
              </template>
              <template #item.paymentApproval="{ item }">
                <v-chip
                  size="small"
                  :color="item.paymentApproval === 'Rejected' ? 'error' : 'warning'"
                  :prepend-icon="item.paymentApproval === 'Rejected' ? 'mdi-alert-circle' : 'mdi-clock-outline'"
                >{{ item.paymentApproval === 'Rejected' ? 'Rejected' : 'Pending Review' }}</v-chip>
              </template>
              <template #item.adminApprovalAt="{ item }">
                {{ item.adminApprovalAt ? new Date(item.adminApprovalAt).toLocaleDateString() : '—' }}
              </template>
              <template #item.actions="{ item }">
                <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-eye" @click.stop="openPo(item, 'accept')">Review</v-btn>
              </template>
            </v-data-table>

            <!-- Wallet Transfer Requests -->
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-swap-horizontal" size="18" class="mr-1" color="deep-purple" />
              Wallet Transfer Requests
              <v-chip size="x-small" color="deep-purple" variant="tonal" class="ml-2">{{ acceptanceTransfers.length }}</v-chip>
            </div>
            <v-data-table
              :headers="walletAcceptanceHeaders"
              :items="acceptanceTransfers"
              :loading="wtLoading"
              density="comfortable"
              :items-per-page="25"
              hover
              class="cursor-pointer"
              @click:row="(_, row) => openTransfer(row.item, 'accept')"
            >
              <template #item.withdrawAmount="{ item }">
                <span class="font-weight-medium">{{ item.withdrawAmount }} {{ item.fromCurrency }}</span>
                <span class="text-caption text-medium-emphasis mx-1">→</span>
                <span class="font-weight-medium">{{ item.depositAmount }} {{ item.toCurrency }}</span>
              </template>
              <template #item.status="{ item }">
                <v-chip size="small" :color="wtStatusColor(item.status)" :prepend-icon="wtStatusIcon(item.status)">
                  {{ item.status }}
                </v-chip>
              </template>
              <template #item.createdAt="{ item }">
                {{ new Date(item.createdAt).toLocaleDateString() }}
              </template>
              <template #item.actions="{ item }">
                <v-btn size="small" variant="tonal" color="deep-purple" prepend-icon="mdi-eye" @click.stop="openTransfer(item, 'accept')">Review</v-btn>
              </template>
            </v-data-table>
          </v-card-text>
        </v-tabs-window-item>

        <!-- ═══════ TAB 2: Withdraw Panel ═══════ -->
        <v-tabs-window-item value="withdraw">
          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">
              Upload proof of payment (POP) to supplier and submit accepted payment orders.
            </p>

            <!-- PO Withdraw -->
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-file-document-outline" size="18" class="mr-1" color="primary" />
              PO Payments
              <v-chip size="x-small" color="primary" variant="tonal" class="ml-2">{{ withdrawQueue.length }}</v-chip>
            </div>
            <v-data-table
              :headers="withdrawHeaders"
              :items="withdrawQueue"
              :loading="loading"
              density="comfortable"
              :items-per-page="25"
              hover
              class="cursor-pointer mb-6"
              @click:row="(_, row) => openPo(row.item, 'withdraw')"
            >
              <template #item.totalAmount="{ item }">
                ${{ formatPrice(item.totalAmount) }}
              </template>
              <template #item.preferredWalletName="{ item }">
                <div v-if="item.preferredWalletName">
                  <div class="text-caption font-weight-medium">{{ item.preferredWalletName }}</div>
                  <div class="text-caption text-medium-emphasis">{{ item.preferredWalletCompany }}</div>
                </div>
                <span v-else class="text-caption text-medium-emphasis">—</span>
              </template>
              <template #item.paymentStatus="{ item }">
                <v-chip
                  size="small"
                  :color="item.paymentStatus === 'Submitted' ? 'success' : 'primary'"
                  :prepend-icon="item.paymentStatus === 'Submitted' ? 'mdi-check-circle' : 'mdi-cash-multiple'"
                >{{ item.paymentStatus === 'Submitted' ? 'Submitted' : 'Awaiting POP' }}</v-chip>
              </template>
              <template #item.adminApprovalAt="{ item }">
                {{ item.adminApprovalAt ? new Date(item.adminApprovalAt).toLocaleDateString() : '—' }}
              </template>
              <template #item.actions="{ item }">
                <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-eye" @click.stop="openPo(item, 'withdraw')">Open</v-btn>
              </template>
            </v-data-table>

            <!-- Wallet Transfer Withdraw -->
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-swap-horizontal" size="18" class="mr-1" color="deep-purple" />
              Wallet Transfers — Upload POP & Execute
              <v-chip size="x-small" color="deep-purple" variant="tonal" class="ml-2">{{ withdrawTransfers.length }}</v-chip>
            </div>
            <v-data-table
              :headers="walletWithdrawHeaders"
              :items="withdrawTransfers"
              :loading="wtLoading"
              density="comfortable"
              :items-per-page="25"
              hover
              class="cursor-pointer"
              @click:row="(_, row) => openTransfer(row.item, 'withdraw')"
            >
              <template #item.withdrawAmount="{ item }">
                <span class="font-weight-medium">{{ item.withdrawAmount }} {{ item.fromCurrency }}</span>
                <span class="text-caption text-medium-emphasis mx-1">→</span>
                <span class="font-weight-medium">{{ item.depositAmount }} {{ item.toCurrency }}</span>
              </template>
              <template #item.status="{ item }">
                <v-chip size="small" :color="wtStatusColor(item.status)" :prepend-icon="wtStatusIcon(item.status)">
                  {{ item.status }}
                </v-chip>
              </template>
              <template #item.createdAt="{ item }">
                {{ new Date(item.createdAt).toLocaleDateString() }}
              </template>
              <template #item.actions="{ item }">
                <v-btn size="small" variant="tonal" color="deep-purple" prepend-icon="mdi-upload" @click.stop="openTransfer(item, 'withdraw')">
                  {{ item.status === 'Completed' ? 'View' : 'Upload POP' }}
                </v-btn>
              </template>
            </v-data-table>
          </v-card-text>
        </v-tabs-window-item>
      </v-tabs-window>
    </v-card>

    <!-- ═══════ PO Detail Dialog ═══════ -->
    <v-dialog v-model="showDetail" max-width="940" scrollable>
      <v-card v-if="selectedPo">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon :icon="dialogMode === 'accept' ? 'mdi-shield-check-outline' : 'mdi-cash-multiple'" class="mr-2" />
          {{ selectedPo.poNumber }}
          <v-chip size="small" color="grey" variant="tonal" class="ml-2">{{ selectedPo.supplierName }}</v-chip>
          <v-spacer />
          <v-chip v-if="dialogMode === 'accept'" size="small" color="warning" prepend-icon="mdi-clock-outline">
            {{ selectedPo.paymentApproval === 'Rejected' ? 'Previously Rejected' : 'Pending Acceptance' }}
          </v-chip>
          <v-chip v-else size="small" :color="selectedPo.paymentStatus === 'Submitted' ? 'success' : 'primary'">
            {{ selectedPo.paymentStatus === 'Submitted' ? 'Submitted' : 'Awaiting POP' }}
          </v-chip>
        </v-card-title>

        <v-divider />

        <v-card-text style="max-height: 75vh; overflow-y: auto;">
          <v-row dense class="mb-4 mt-2">
            <v-col cols="6" md="3">
              <div class="text-caption text-medium-emphasis">Supplier</div>
              <div class="font-weight-medium text-body-2">{{ selectedPo.supplierName }}</div>
            </v-col>
            <v-col cols="6" md="3">
              <div class="text-caption text-medium-emphasis">Total Amount</div>
              <div class="font-weight-bold text-success">${{ formatPrice(selectedPo.totalAmount) }}</div>
            </v-col>
            <v-col cols="6" md="3">
              <div class="text-caption text-medium-emphasis">Sales Order</div>
              <div class="text-body-2">{{ selectedPo.invoiceNumber || '—' }}</div>
            </v-col>
            <v-col cols="6" md="3">
              <div class="text-caption text-medium-emphasis">Approved</div>
              <div class="text-body-2">{{ selectedPo.adminApprovalAt ? new Date(selectedPo.adminApprovalAt).toLocaleDateString() : '—' }}</div>
            </v-col>
            <v-col cols="12">
              <v-card
                variant="tonal"
                :color="selectedPo.preferredWalletId ? 'primary' : 'grey'"
                density="compact"
                class="pa-3 d-flex align-center gap-3"
              >
                <v-icon icon="mdi-bank-outline" size="22" />
                <div>
                  <div class="text-caption text-medium-emphasis">Pay From Wallet</div>
                  <div v-if="selectedPo.preferredWalletName" class="font-weight-bold text-body-2">
                    {{ selectedPo.preferredWalletName }}
                    <span class="text-caption text-medium-emphasis ml-1">· {{ selectedPo.preferredWalletCompany }}</span>
                  </div>
                  <div v-else class="text-body-2 text-medium-emphasis">No wallet selected</div>
                </div>
              </v-card>
            </v-col>
          </v-row>

          <v-divider class="mb-4" />

          <!-- Supplier Invoice -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-invoice-text" size="18" class="mr-1" color="primary" />
              Supplier Invoice
            </div>
            <div v-if="supplierInvoiceFile" class="d-flex align-center gap-2 pa-2 rounded file-row">
              <v-icon icon="mdi-file-check" color="success" size="20" />
              <span class="text-body-2 flex-grow-1">{{ supplierInvoiceFile.name }}</span>
              <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-eye-outline" @click="downloadSupplierFile(supplierInvoiceFile.name, 'supplier_invoice')">Preview</v-btn>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No supplier invoice uploaded yet.</v-alert>
          </div>

          <!-- Supplier Bank Info -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-bank-outline" size="18" class="mr-1" color="success" />
              Supplier Bank Info
            </div>
            <div v-if="bankInfoFiles.length" class="d-flex flex-column gap-2">
              <div v-for="f in bankInfoFiles" :key="f.name" class="d-flex align-center gap-2 pa-2 rounded file-row">
                <v-icon icon="mdi-file-document-outline" color="success" size="20" />
                <div class="d-flex flex-column flex-grow-1">
                  <span class="text-body-2 font-weight-medium">{{ f.name }}</span>
                  <span class="text-caption text-medium-emphasis">{{ new Date(f.modifiedAt).toLocaleString() }}</span>
                </div>
                <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadSupplierFile(f.name, 'supplier_bank_info')" />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No bank info uploaded yet.</v-alert>
          </div>

          <!-- Supplier Bank Detail (from PO Import) -->
          <div v-if="importDetail && (importDetail.bankName || importDetail.bankAccountNumber)" class="mb-4 pa-3 rounded file-row">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-bank" size="18" class="mr-1" color="success" />
              Supplier Bank Detail
            </div>
            <v-row dense>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Bank Name</div><div class="text-body-2">{{ importDetail.bankName || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Account Number</div><div class="text-body-2">{{ importDetail.bankAccountNumber || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Bank Address</div><div class="text-body-2">{{ importDetail.bankAddress || '—' }}</div></v-col>
              <v-col cols="6" md="3"><div class="text-caption text-medium-emphasis">Bank City</div><div class="text-body-2">{{ importDetail.bankCity || '—' }}</div></v-col>
              <v-col cols="6" md="3"><div class="text-caption text-medium-emphasis">Bank Country</div><div class="text-body-2">{{ importDetail.bankCountry || '—' }}</div></v-col>
            </v-row>
          </div>

          <!-- Payment Request (PR) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-file-certificate-outline" size="18" class="mr-1" color="primary" />
              Payment Request (PR)
            </div>
            <div v-if="dpFiles.length" class="d-flex flex-column gap-2">
              <div v-for="f in dpFiles" :key="f.name" class="d-flex align-center gap-2 pa-2 rounded file-row">
                <v-icon icon="mdi-file-pdf-box" color="primary" size="20" />
                <div class="d-flex flex-column flex-grow-1">
                  <span class="text-body-2 font-weight-medium">{{ f.name }}</span>
                  <span class="text-caption text-medium-emphasis">{{ new Date(f.modifiedAt).toLocaleString() }}</span>
                </div>
                <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadSupplierFile(f.name, 'dp')" />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No Payment Request generated yet.</v-alert>
          </div>

          <!-- Customer Paid POPs -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-cash-plus" size="18" class="mr-1" color="success" />
              Customer Paid POPs
              <v-chip v-if="customerPayments.length" size="x-small" color="success" variant="tonal" class="ml-2">{{ customerPayments.length }}</v-chip>
              <v-spacer />
              <span v-if="customerPayments.length" class="text-caption text-medium-emphasis">
                Paid: <strong class="text-success">${{ formatPrice(customerTotalPaid) }}</strong>
                <span v-if="customerInvoiceTotal != null"> / ${{ formatPrice(customerInvoiceTotal) }}</span>
                <v-chip v-if="customerIsPaid" size="x-small" color="success" variant="flat" class="ml-2">PAID</v-chip>
              </span>
            </div>
            <div v-if="customerPayments.length" class="d-flex flex-column gap-2">
              <div v-for="p in customerPayments" :key="p.id" class="d-flex align-center gap-2 pa-2 rounded file-row">
                <v-icon icon="mdi-file-document-check-outline" color="success" size="20" />
                <div class="d-flex flex-column flex-grow-1" style="min-width:0;">
                  <span class="text-body-2 font-weight-medium text-truncate">{{ p.fileName }}</span>
                  <span class="text-caption text-medium-emphasis">
                    Amount: <strong class="text-success">${{ formatPrice(p.amount) }}</strong>
                    · {{ new Date(p.createdAt).toLocaleString() }}
                    <span v-if="p.notes"> · {{ p.notes }}</span>
                  </span>
                </div>
                <v-btn size="small" variant="tonal" color="success" icon="mdi-eye-outline" @click="downloadCustomerPop(p.fileName)" />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No customer payments uploaded yet.</v-alert>
          </div>

          <v-divider class="mb-4" />

          <!-- TAB 2 ONLY: Our POP to Supplier -->
          <div v-if="dialogMode === 'withdraw'" class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-cash-check" size="18" class="mr-1" color="warning" />
              Our POP to Supplier
            </div>
            <div v-for="f in popFiles" :key="f.name" class="d-flex align-center gap-2 mb-2 pa-2 rounded file-row">
              <v-icon :icon="f.name.includes('_final') ? 'mdi-file-star' : 'mdi-file-check'" :color="f.name.includes('_final') ? 'amber-darken-2' : 'success'" size="20" />
              <div class="d-flex flex-column flex-grow-1">
                <span class="text-body-2 font-weight-medium">{{ f.name }}</span>
                <span class="text-caption text-medium-emphasis">{{ new Date(f.modifiedAt).toLocaleString() }}</span>
              </div>
              <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadSupplierFile(f.name, 'our_pop')" />
            </div>
            <div v-if="!isFinalPopUploaded" class="mt-2">
              <v-btn variant="tonal" color="warning" prepend-icon="mdi-upload" :disabled="popFiles.length >= 10" @click="openPopUploadDialog">
                {{ popFiles.length > 0 ? 'Add Another POP' : 'Upload POP File' }}
              </v-btn>
              <span v-if="popFiles.length >= 10" class="text-caption text-error ml-2">Max 10 POPs reached.</span>
            </div>
            <v-alert v-else type="success" variant="tonal" density="compact" icon="mdi-shield-check" class="mt-2">Final POP uploaded. No further uploads allowed.</v-alert>
          </div>

          <!-- TAB 1: Accept / Reject Actions -->
          <div v-if="dialogMode === 'accept'">
            <v-alert v-if="selectedPo.paymentApproval === 'Rejected'" type="warning" variant="tonal" density="compact" icon="mdi-alert-circle" class="mb-4">
              This request was previously rejected. Review the documents and accept or reject again.
            </v-alert>
            <div class="d-flex gap-3">
              <v-btn class="flex-grow-1" color="error" variant="tonal" prepend-icon="mdi-close-circle" :loading="rejecting" @click="showRejectDialog = true">Reject</v-btn>
              <v-btn class="flex-grow-1" color="success" variant="flat" prepend-icon="mdi-check-circle" :loading="accepting" @click="acceptPayment">Accept Payment</v-btn>
            </div>
          </div>

          <!-- TAB 2: Submit Action -->
          <div v-else>
            <v-alert v-if="selectedPo.paymentStatus === 'Submitted'" type="success" variant="tonal" icon="mdi-check-circle">
              Payment submitted{{ selectedPo.paymentSubmittedAt ? ' at ' + new Date(selectedPo.paymentSubmittedAt).toLocaleString() : '' }}.
            </v-alert>
            <div v-else class="d-flex flex-column gap-2">
              <div v-if="popFiles.length === 0" class="text-caption text-medium-emphasis text-center">Upload at least one POP file to enable submit.</div>
              <v-btn color="success" variant="flat" prepend-icon="mdi-send-check" :disabled="popFiles.length === 0" :loading="submitting" @click="submitPayment">Submit Payment</v-btn>
            </div>
          </div>
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDetail = false">Close</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ═══════ Wallet Transfer Detail Dialog ═══════ -->
    <v-dialog v-model="showWtDetail" max-width="680" scrollable>
      <v-card v-if="selectedWt" class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-swap-horizontal" class="mr-2" color="deep-purple" />
          Wallet Transfer #{{ selectedWt.id }}
          <v-spacer />
          <v-chip size="small" :color="wtStatusColor(selectedWt.status)" :prepend-icon="wtStatusIcon(selectedWt.status)">
            {{ selectedWt.status }}
          </v-chip>
        </v-card-title>

        <v-divider />

        <v-card-text style="max-height: 75vh; overflow-y: auto;">
          <!-- Transfer summary -->
          <v-row dense class="mb-4 mt-2">
            <v-col cols="12" sm="5">
              <v-card variant="tonal" color="deep-purple" class="pa-3 text-center">
                <div class="text-caption text-medium-emphasis mb-1">From Wallet</div>
                <div class="font-weight-bold">{{ selectedWt.fromBoxName }}</div>
                <div class="text-h6 font-weight-bold mt-1">{{ selectedWt.withdrawAmount }} <span class="text-body-2">{{ selectedWt.fromCurrency }}</span></div>
              </v-card>
            </v-col>
            <v-col cols="12" sm="2" class="d-flex align-center justify-center">
              <div class="text-center">
                <v-icon icon="mdi-arrow-right-bold" color="deep-purple" size="28" />
                <div v-if="selectedWt.exchangeRate" class="text-caption text-medium-emphasis mt-1">× {{ selectedWt.exchangeRate }}</div>
              </div>
            </v-col>
            <v-col cols="12" sm="5">
              <v-card variant="tonal" color="success" class="pa-3 text-center">
                <div class="text-caption text-medium-emphasis mb-1">To Wallet</div>
                <div class="font-weight-bold">{{ selectedWt.toBoxName }}</div>
                <div class="text-h6 font-weight-bold mt-1">{{ selectedWt.depositAmount }} <span class="text-body-2">{{ selectedWt.toCurrency }}</span></div>
              </v-card>
            </v-col>
          </v-row>

          <v-row dense class="mb-4">
            <v-col cols="6">
              <div class="text-caption text-medium-emphasis">Requested By</div>
              <div class="text-body-2">{{ selectedWt.createdByName }}</div>
            </v-col>
            <v-col cols="6">
              <div class="text-caption text-medium-emphasis">Date</div>
              <div class="text-body-2">{{ new Date(selectedWt.createdAt).toLocaleString() }}</div>
            </v-col>
            <v-col v-if="selectedWt.notes" cols="12">
              <div class="text-caption text-medium-emphasis">Notes</div>
              <div class="text-body-2">{{ selectedWt.notes }}</div>
            </v-col>
          </v-row>

          <v-divider class="mb-4" />

          <!-- Rejection note (if rejected) -->
          <v-alert v-if="selectedWt.status === 'Rejected'" type="error" variant="tonal" density="compact" icon="mdi-close-circle" class="mb-4">
            <strong>Rejected:</strong> {{ selectedWt.rejectionNote || 'No reason provided.' }}
          </v-alert>

          <!-- POP section (shown in both modes for info, upload only in withdraw mode) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-cash-check" size="18" class="mr-1" color="warning" />
              Proof of Payment (POP)
            </div>
            <div v-if="selectedWt.popFileName" class="d-flex align-center gap-2 pa-2 rounded file-row">
              <v-icon icon="mdi-file-check" color="success" size="20" />
              <span class="text-body-2 flex-grow-1">{{ selectedWt.popFileName }}</span>
              <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadWtPop(selectedWt)" />
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No POP uploaded yet.</v-alert>

            <!-- Upload POP — only in withdraw mode when Accepted and not Completed -->
            <div v-if="wtDialogMode === 'withdraw' && selectedWt.status === 'Accepted'" class="mt-3">
              <v-btn variant="flat" color="deep-purple" prepend-icon="mdi-upload" :loading="wtUploading" @click="wtPopInputRef?.click()">
                Upload POP &amp; Execute Transfer
              </v-btn>
              <div class="text-caption text-medium-emphasis mt-1">
                Uploading the POP will automatically execute the wallet transfer.
              </div>
              <input ref="wtPopInputRef" type="file" class="d-none" @change="onWtPopSelected" />
            </div>

            <v-alert v-if="selectedWt.status === 'Completed'" type="success" variant="tonal" icon="mdi-check-circle" class="mt-3">
              Transfer completed on {{ selectedWt.completedAt ? new Date(selectedWt.completedAt).toLocaleString() : '—' }}.
              The wallet balances have been updated automatically.
            </v-alert>
          </div>

          <v-divider class="mb-4" />

          <!-- Accept / Reject — only in acceptance mode when Pending -->
          <div v-if="wtDialogMode === 'accept' && selectedWt.status === 'Pending'">
            <div class="d-flex gap-3">
              <v-btn class="flex-grow-1" color="error" variant="tonal" prepend-icon="mdi-close-circle" :loading="wtRejecting" @click="showWtRejectDialog = true">Reject</v-btn>
              <v-btn class="flex-grow-1" color="success" variant="flat" prepend-icon="mdi-check-circle" :loading="wtAccepting" @click="acceptTransfer">Accept Transfer</v-btn>
            </div>
          </div>
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showWtDetail = false">Close</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ═══════ Create Wallet Transfer Dialog ═══════ -->
    <v-dialog v-model="showCreateWt" max-width="560">
      <v-card class="glass-card">
        <v-card-title class="pa-4">
          <v-icon icon="mdi-swap-horizontal" class="mr-2" color="deep-purple" />
          New Wallet Transfer Request
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-select
            v-model="createWt.fromBoxId"
            :items="walletBoxes"
            item-title="label"
            item-value="id"
            label="From Wallet *"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-select
            v-model="createWt.toBoxId"
            :items="walletBoxes.filter(b => b.id !== createWt.fromBoxId)"
            item-title="label"
            item-value="id"
            label="To Wallet *"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-row dense>
            <v-col cols="6">
              <v-text-field
                v-model.number="createWt.withdrawAmount"
                label="Withdraw Amount *"
                type="number"
                min="0"
                variant="outlined"
                density="comfortable"
                :suffix="fromBoxCurrency"
              />
            </v-col>
            <v-col cols="6">
              <v-text-field
                v-model.number="createWt.depositAmount"
                label="Deposit Amount *"
                type="number"
                min="0"
                variant="outlined"
                density="comfortable"
                :suffix="toBoxCurrency"
              />
            </v-col>
          </v-row>
          <v-text-field
            v-model.number="createWt.exchangeRate"
            label="Exchange Rate (optional)"
            type="number"
            min="0"
            step="0.0001"
            variant="outlined"
            density="comfortable"
            class="mb-3"
            hint="Leave blank if same currency"
            persistent-hint
          />
          <v-textarea
            v-model="createWt.notes"
            label="Notes (optional)"
            rows="2"
            variant="outlined"
            density="comfortable"
            hide-details
          />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showCreateWt = false">Cancel</v-btn>
          <v-btn color="deep-purple" variant="flat" :loading="wtCreating" :disabled="!createWtValid" @click="submitCreateTransfer">Create Request</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Wallet Transfer Reject Dialog -->
    <v-dialog v-model="showWtRejectDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title class="pa-4">Reject Transfer Request</v-card-title>
        <v-card-text class="pa-4">
          <div class="mb-4 text-body-2">Rejecting Transfer #{{ selectedWt?.id }} will notify the requester.</div>
          <v-textarea v-model="wtRejectionNote" label="Rejection Reason" rows="3" variant="outlined" density="comfortable" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showWtRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="tonal" :disabled="!wtRejectionNote.trim()" :loading="wtRejecting" @click="confirmRejectTransfer">Confirm Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- PO Reject Dialog -->
    <v-dialog v-model="showRejectDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title class="pa-4">Reject Payment Request</v-card-title>
        <v-card-text class="pa-4">
          <div class="mb-4 text-body-2">Rejecting PO <strong>{{ selectedPo?.poNumber }}</strong> will return it to the procurement team for corrections.</div>
          <v-textarea v-model="rejectionNote" label="Rejection Reason" placeholder="Explain why this payment request is being rejected..." rows="3" variant="outlined" density="comfortable" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="tonal" :disabled="!rejectionNote.trim()" :loading="rejecting" @click="confirmReject">Confirm Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">{{ snackbarText }}</v-snackbar>

  <!-- ═══════ POP Upload Dialog (with wallet picker for first POP) ═══════ -->
  <v-dialog v-model="showWalletPickerDialog" max-width="520" persistent>
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4 gap-2">
        <v-icon icon="mdi-cash-check" color="warning" />
        Upload Proof of Payment
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-4">
        <p class="text-body-2 text-medium-emphasis mb-4">
          PO: <strong>{{ selectedPo?.poNumber }}</strong> — Supplier: <strong>{{ selectedPo?.supplierName }}</strong>
        </p>

        <!-- Wallet picker — shown only for the first POP upload -->
        <template v-if="popFiles.length === 0">
          <p class="text-body-2 mb-3">
            <v-icon icon="mdi-information-outline" size="16" class="mr-1" color="info" />
            Select which company wallet this payment was withdrawn from.
          </p>
          <v-alert
            v-if="selectedPo?.preferredWalletId"
            density="compact"
            type="info"
            variant="tonal"
            class="mb-3"
            icon="mdi-star-outline"
          >
            Preferred: <strong>{{ selectedPo.preferredWalletCompany }} — {{ selectedPo.preferredWalletName }}</strong>
          </v-alert>
          <v-select
            v-model="pickerWalletId"
            :items="walletBoxes"
            item-title="label"
            item-value="id"
            label="Withdrawn From Wallet *"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-bank-outline"
            class="mb-3"
          >
            <template #item="{ item, props: itemProps }">
              <v-list-item v-bind="itemProps">
                <template #append>
                  <v-chip v-if="item.raw.id === selectedPo?.preferredWalletId" size="x-small" color="info" variant="tonal">preferred</v-chip>
                </template>
              </v-list-item>
            </template>
          </v-select>
        </template>

        <!-- File picker -->
        <v-file-input
          v-model="popDialogFile"
          label="POP File *"
          variant="outlined"
          density="comfortable"
          prepend-icon="mdi-file-upload-outline"
          class="mb-2"
          clearable
        />

        <!-- Is Final POP -->
        <v-checkbox
          v-model="isFinalPop"
          label="Is This Final POP?"
          density="compact"
          hide-details
          color="amber-darken-2"
        />
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-btn variant="text" @click="showWalletPickerDialog = false">Cancel</v-btn>
        <v-spacer />
        <v-btn
          color="warning"
          variant="flat"
          :disabled="!popDialogFile || (popFiles.length === 0 && !pickerWalletId)"
          :loading="uploadingPop"
          @click="confirmPopUpload"
        >
          <v-icon start>mdi-upload</v-icon>
          Upload POP
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const config = useRuntimeConfig()
const authStore = useAuthStore()
const docPreview = useDocPreview()

type FileInfo = { name: string; size: number; modifiedAt: string; category: string }
type POItem = any
type WalletTransfer = {
  id: number
  fromBoxId: number; fromBoxName: string; fromCurrency: string
  toBoxId: number; toBoxName: string; toCurrency: string
  withdrawAmount: number; depositAmount: number; exchangeRate?: number
  notes?: string; status: string; popFileName?: string; rejectionNote?: string
  createdByName: string; createdAt: string; acceptedAt?: string; completedAt?: string
}
type WalletBox = { id: number; label: string; currency: string; company?: string }

// ── Tabs ──────────────────────────────────────────────────────────────────────
const activeTab = ref<'acceptance' | 'withdraw'>('acceptance')

// ── PO Queue ──────────────────────────────────────────────────────────────────
const loading = ref(false)
const queue = ref<POItem[]>([])
const acceptanceQueue = computed(() => queue.value.filter(p => p.paymentApproval !== 'Accepted'))
const withdrawQueue = computed(() => queue.value.filter(p => p.paymentApproval === 'Accepted'))
const acceptancePoBadge = computed(() => acceptanceQueue.value.length)
const withdrawPoBadge = computed(() => withdrawQueue.value.filter(p => p.paymentStatus !== 'Submitted').length)

// ── Wallet Transfers ──────────────────────────────────────────────────────────
const wtLoading = ref(false)
const walletTransfers = ref<WalletTransfer[]>([])
const acceptanceTransfers = computed(() => walletTransfers.value.filter(t => t.status === 'Pending' || t.status === 'Rejected'))
const withdrawTransfers = computed(() => walletTransfers.value.filter(t => t.status === 'Accepted' || t.status === 'Completed'))
const acceptanceWtBadge = computed(() => acceptanceTransfers.value.filter(t => t.status === 'Pending').length)
const withdrawWtBadge = computed(() => withdrawTransfers.value.filter(t => t.status === 'Accepted').length)

// ── Wallet boxes (for create dialog) ─────────────────────────────────────────
const walletBoxes = ref<WalletBox[]>([])

// ── POP upload dialog (wallet picker + file picker) ───────────────────────────
const showWalletPickerDialog = ref(false)
const pickerWalletId = ref<number | null>(null)
const popDialogFile = ref<File | null>(null)

// ── PO detail dialog ──────────────────────────────────────────────────────────
const showDetail = ref(false)
const selectedPo = ref<POItem | null>(null)
const dialogMode = ref<'accept' | 'withdraw'>('accept')

const supplierInvoiceFile = ref<FileInfo | null>(null)
const bankInfoFiles = ref<FileInfo[]>([])
const dpFiles = ref<FileInfo[]>([])
const importDetail = ref<any>(null)
const popFiles = ref<FileInfo[]>([])

type CustomerPayment = { id: number; fileName: string; amount: number; notes?: string | null; createdAt: string }
const customerPayments = ref<CustomerPayment[]>([])
const customerTotalPaid = ref(0)
const customerInvoiceTotal = ref<number | null>(null)
const customerIsPaid = ref(false)

const isFinalPop = ref(false)
const isFinalPopUploaded = computed(() => popFiles.value.some(f => f.name.includes('_final')))
const uploadingPop = ref(false)
const submitting = ref(false)
const accepting = ref(false)
const rejecting = ref(false)
const showRejectDialog = ref(false)
const rejectionNote = ref('')
// popInputRef removed — POP upload is now handled via the dialog

// ── Wallet Transfer detail dialog ─────────────────────────────────────────────
const showWtDetail = ref(false)
const selectedWt = ref<WalletTransfer | null>(null)
const wtDialogMode = ref<'accept' | 'withdraw'>('accept')
const wtAccepting = ref(false)
const wtRejecting = ref(false)
const wtUploading = ref(false)
const showWtRejectDialog = ref(false)
const wtRejectionNote = ref('')
const wtPopInputRef = ref<HTMLInputElement | null>(null)

// ── Create transfer dialog ────────────────────────────────────────────────────
const showCreateWt = ref(false)
const wtCreating = ref(false)
const createWt = reactive({ fromBoxId: null as number | null, toBoxId: null as number | null, withdrawAmount: 0, depositAmount: 0, exchangeRate: null as number | null, notes: '' })
const fromBoxCurrency = computed(() => walletBoxes.value.find(b => b.id === createWt.fromBoxId)?.currency ?? '')
const toBoxCurrency = computed(() => walletBoxes.value.find(b => b.id === createWt.toBoxId)?.currency ?? '')
const createWtValid = computed(() => !!createWt.fromBoxId && !!createWt.toBoxId && createWt.withdrawAmount > 0 && createWt.depositAmount > 0)

// ── Table headers ─────────────────────────────────────────────────────────────
const acceptanceHeaders = [
  { title: 'PO Number', key: 'poNumber' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Sales Order', key: 'invoiceNumber' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Pay Wallet', key: 'preferredWalletName' },
  { title: 'Admin Approved', key: 'adminApprovalAt' },
  { title: 'Status', key: 'paymentApproval' },
  { title: '', key: 'actions', sortable: false, width: 120 },
]
const withdrawHeaders = [
  { title: 'PO Number', key: 'poNumber' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Sales Order', key: 'invoiceNumber' },
  { title: 'Pay Wallet', key: 'preferredWalletName' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Admin Approved', key: 'adminApprovalAt' },
  { title: 'Status', key: 'paymentStatus' },
  { title: '', key: 'actions', sortable: false, width: 120 },
]
const walletAcceptanceHeaders = [
  { title: 'From → To', key: 'fromBoxName' },
  { title: 'Amount', key: 'withdrawAmount' },
  { title: 'Requested By', key: 'createdByName' },
  { title: 'Date', key: 'createdAt' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: 120 },
]
const walletWithdrawHeaders = [
  { title: 'From → To', key: 'fromBoxName' },
  { title: 'Amount', key: 'withdrawAmount' },
  { title: 'Requested By', key: 'createdByName' },
  { title: 'Date', key: 'createdAt' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: 140 },
]

// ── Load all data ─────────────────────────────────────────────────────────────
async function loadAll() {
  await Promise.all([loadQueue(), loadWalletTransfers(), loadWalletBoxes()])
}

async function loadQueue() {
  loading.value = true
  try { queue.value = await api.get<any[]>('/purchase-orders/payment-queue') }
  catch { showSnack('Failed to load payment queue', 'error') }
  finally { loading.value = false }
}

async function loadWalletTransfers() {
  wtLoading.value = true
  try { walletTransfers.value = await api.get<WalletTransfer[]>('/wallet-transfers') }
  catch { showSnack('Failed to load wallet transfers', 'error') }
  finally { wtLoading.value = false }
}

async function loadWalletBoxes() {
  try {
    const boxes = await api.get<any[]>('/payment-boxes/simple-list')
    walletBoxes.value = boxes.map((b: any) => ({
      id: b.id,
      label: `${b.companyName} — ${b.name} (${b.currency})`,
      currency: b.currency,
      company: b.companyName,
    }))
  } catch { /* silent */ }
}

// ── PO actions ────────────────────────────────────────────────────────────────
async function openPo(po: POItem, mode: 'accept' | 'withdraw') {
  selectedPo.value = po
  dialogMode.value = mode
  showDetail.value = true
  supplierInvoiceFile.value = null
  bankInfoFiles.value = []
  dpFiles.value = []
  importDetail.value = null
  popFiles.value = []
  isFinalPop.value = false
  customerPayments.value = []
  customerTotalPaid.value = 0
  customerInvoiceTotal.value = null
  customerIsPaid.value = false

  if (po.invoiceId) {
    try {
      const cp = await api.get<any>(`/documents/proforma-invoice/${po.invoiceId}/customer-payments`)
      customerPayments.value = cp.payments ?? []
      customerTotalPaid.value = cp.totalPaid ?? 0
      customerInvoiceTotal.value = cp.invoiceTotal ?? null
      customerIsPaid.value = cp.isPaid === true
    } catch { /* silent */ }
  }

  if (po.invoiceId && po.supplierId) {
    try {
      const data = await api.get<any>(`/documents/proforma-invoice/${po.invoiceId}`)
      const supplierSection = (data.suppliers || []).find((s: any) => s.supplierId === po.supplierId)
      if (supplierSection) {
        const files: FileInfo[] = supplierSection.files || []
        supplierInvoiceFile.value = files.find((f: FileInfo) => f.category === 'supplier_invoice') || null
        bankInfoFiles.value = files.filter((f: FileInfo) => f.category === 'supplier_bank_info')
        dpFiles.value = files.filter((f: FileInfo) => f.category === 'dp')
        popFiles.value = files.filter((f: FileInfo) => f.category === 'our_pop').sort((a, b) => {
          const nA = parseInt(a.name.match(/\d+/)?.[0] || '0')
          const nB = parseInt(b.name.match(/\d+/)?.[0] || '0')
          return nA - nB
        })
      }
    } catch {}
    try { importDetail.value = await api.get(`/purchase-orders/${po.id}/import-detail`) } catch {}
  }
}

async function acceptPayment() {
  if (!selectedPo.value) return
  accepting.value = true
  try {
    await api.patch(`/purchase-orders/${selectedPo.value.id}/payment-approval`, { decision: 'Accepted' })
    showSnack('Payment request accepted — moved to Withdraw Panel', 'success')
    showDetail.value = false
    await loadQueue()
    activeTab.value = 'withdraw'
  } catch (e: any) {
    showSnack(e?.data?.message || 'Accept failed', 'error')
  } finally { accepting.value = false }
}

function openPopUploadDialog() {
  pickerWalletId.value = selectedPo.value?.preferredWalletId ?? null
  popDialogFile.value = null
  isFinalPop.value = false
  showWalletPickerDialog.value = true
}

async function confirmPopUpload() {
  if (!selectedPo.value || !popDialogFile.value) return
  uploadingPop.value = true
  try {
    // 1. Upload the POP file to the document store
    const form = new FormData()
    form.append('file', popDialogFile.value)
    form.append('category', 'our_pop')
    form.append('isFinal', isFinalPop.value.toString())
    await $fetch(
      `${api.baseURL}/documents/proforma-invoice/${selectedPo.value.invoiceId}/supplier/${selectedPo.value.supplierId}/upload`,
      { method: 'POST', body: form, headers: { Authorization: `Bearer ${authStore.user?.token}` } }
    )

    // 2. On first POP: record wallet withdrawal transaction
    if (popFiles.value.length === 0 && pickerWalletId.value) {
      try {
        await api.post(`/purchase-orders/${selectedPo.value.id}/record-pop-withdrawal`, { walletId: pickerWalletId.value })
      } catch (e: any) {
        // Conflict (409) means already recorded — treat as success
        if (e?.response?.status !== 409 && e?.status !== 409) throw e
      }
    }

    showSnack(isFinalPop.value ? 'Final POP uploaded' : 'POP uploaded successfully', 'success')
    showWalletPickerDialog.value = false
    popDialogFile.value = null
    await openPo(selectedPo.value, 'withdraw')
  } catch (e: any) {
    showSnack(e?.data?.message || 'Upload failed', 'error')
  } finally { uploadingPop.value = false }
}

async function confirmReject() {
  if (!selectedPo.value || !rejectionNote.value.trim()) return
  rejecting.value = true
  try {
    await api.patch(`/purchase-orders/${selectedPo.value.id}/payment-approval`, { decision: 'Rejected', note: rejectionNote.value.trim() })
    showSnack('Payment request rejected', 'warning')
    showRejectDialog.value = false
    rejectionNote.value = ''
    showDetail.value = false
    await loadQueue()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Rejection failed', 'error')
  } finally { rejecting.value = false }
}

async function submitPayment() {
  if (!selectedPo.value) return
  submitting.value = true
  try {
    const r = await api.patch<any>(`/purchase-orders/${selectedPo.value.id}/submit-payment`, {})
    selectedPo.value.paymentStatus = 'Submitted'
    selectedPo.value.paymentSubmittedAt = r?.paymentSubmittedAt || new Date().toISOString()
    showSnack('Payment submitted successfully', 'success')
    await loadQueue()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Submit failed', 'error')
  } finally { submitting.value = false }
}

// ── Wallet transfer actions ───────────────────────────────────────────────────
function openTransfer(wt: WalletTransfer, mode: 'accept' | 'withdraw') {
  selectedWt.value = wt
  wtDialogMode.value = mode
  wtRejectionNote.value = ''
  showWtDetail.value = true
}

function openCreateTransfer() {
  createWt.fromBoxId = null
  createWt.toBoxId = null
  createWt.withdrawAmount = 0
  createWt.depositAmount = 0
  createWt.exchangeRate = null
  createWt.notes = ''
  showCreateWt.value = true
}

async function submitCreateTransfer() {
  wtCreating.value = true
  try {
    await api.post('/wallet-transfers', {
      fromBoxId: createWt.fromBoxId,
      toBoxId: createWt.toBoxId,
      withdrawAmount: createWt.withdrawAmount,
      depositAmount: createWt.depositAmount,
      exchangeRate: createWt.exchangeRate || null,
      notes: createWt.notes || null,
    })
    showSnack('Transfer request created — pending acceptance', 'success')
    showCreateWt.value = false
    await loadWalletTransfers()
    activeTab.value = 'acceptance'
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to create transfer request', 'error')
  } finally { wtCreating.value = false }
}

async function acceptTransfer() {
  if (!selectedWt.value) return
  wtAccepting.value = true
  try {
    await api.patch(`/wallet-transfers/${selectedWt.value.id}/review`, { decision: 'Accept', note: null })
    showSnack('Transfer accepted — moved to Withdraw Panel', 'success')
    showWtDetail.value = false
    await loadWalletTransfers()
    activeTab.value = 'withdraw'
  } catch (e: any) {
    showSnack(e?.data?.message || 'Accept failed', 'error')
  } finally { wtAccepting.value = false }
}

async function confirmRejectTransfer() {
  if (!selectedWt.value || !wtRejectionNote.value.trim()) return
  wtRejecting.value = true
  try {
    await api.patch(`/wallet-transfers/${selectedWt.value.id}/review`, { decision: 'Reject', note: wtRejectionNote.value.trim() })
    showSnack('Transfer rejected', 'warning')
    showWtRejectDialog.value = false
    showWtDetail.value = false
    await loadWalletTransfers()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Reject failed', 'error')
  } finally { wtRejecting.value = false }
}

async function onWtPopSelected(e: Event) {
  const target = e.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file || !selectedWt.value) return
  wtUploading.value = true
  try {
    const form = new FormData()
    form.append('file', file)
    await $fetch(
      `${api.baseURL}/wallet-transfers/${selectedWt.value.id}/upload-pop`,
      { method: 'POST', body: form, headers: { Authorization: `Bearer ${authStore.user?.token}` } }
    )
    showSnack('POP uploaded — transfer executed successfully!', 'success')
    showWtDetail.value = false
    await loadWalletTransfers()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Upload failed', 'error')
  } finally {
    wtUploading.value = false
    if (target) target.value = ''
  }
}

async function downloadWtPop(wt: WalletTransfer) {
  try {
    const blob = await $fetch<Blob>(
      `${api.baseURL}/wallet-transfers/${wt.id}/pop-file`,
      { method: 'GET', responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` } }
    )
    docPreview.previewBlob(blob as Blob, wt.popFileName ?? 'pop')
  } catch { showSnack('Failed to open file', 'error') }
}

// ── PO file downloads ─────────────────────────────────────────────────────────
async function downloadCustomerPop(name: string) {
  if (!selectedPo.value?.invoiceId) return
  try {
    const blob = await $fetch<Blob>(
      `${api.baseURL}/documents/proforma-invoice/${selectedPo.value.invoiceId}/file`,
      { method: 'GET', query: { name, category: 'customer_pop' }, responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` } }
    )
    docPreview.previewBlob(blob as Blob, name)
  } catch { showSnack('Failed to open file', 'error') }
}

async function downloadSupplierFile(name: string, category: string) {
  if (!selectedPo.value) return
  try {
    const blob = await $fetch<Blob>(
      `${api.baseURL}/documents/proforma-invoice/${selectedPo.value.invoiceId}/supplier/${selectedPo.value.supplierId}/file`,
      { method: 'GET', query: { name, category }, responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` } }
    )
    docPreview.previewBlob(blob as Blob, name)
  } catch { showSnack('Failed to open file', 'error') }
}

// onPopSelected removed — POP upload is now handled by confirmPopUpload() via the dialog

// ── Helpers ───────────────────────────────────────────────────────────────────
function wtStatusColor(status: string) {
  return { Pending: 'warning', Accepted: 'primary', Completed: 'success', Rejected: 'error' }[status] ?? 'grey'
}
function wtStatusIcon(status: string) {
  return { Pending: 'mdi-clock-outline', Accepted: 'mdi-check', Completed: 'mdi-check-circle', Rejected: 'mdi-close-circle' }[status] ?? 'mdi-help'
}
function formatPrice(v: any) {
  if (v == null || isNaN(Number(v))) return '0.00'
  return Number(v).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(t: string, c: string) { snackbarText.value = t; snackbarColor.value = c; snackbar.value = true }

onMounted(loadAll)
</script>

<style scoped>
.file-row {
  background-color: rgba(var(--v-theme-on-surface), 0.06);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}
</style>
