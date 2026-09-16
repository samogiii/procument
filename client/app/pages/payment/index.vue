<template>
  <div>
    <div class="d-flex align-center mb-4 mb-md-6">
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Payments</h1>
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="loadAll">Refresh</v-btn>
    </div>

    <v-card class="glass-card">
      <v-tabs v-model="activeTab" bg-color="transparent" color="primary">
        <v-tab value="acceptance">
          <v-icon start size="18">mdi-shield-check-outline</v-icon>
          Payment Request
          <v-chip v-if="acceptancePoBadge" size="x-small" color="warning" variant="tonal" class="ml-2">
            {{ acceptancePoBadge }}
          </v-chip>
        </v-tab>
        <v-tab value="remaining">
          <v-icon start size="18">mdi-cash-clock</v-icon>
          Ready To pay
          <v-chip v-if="remainingPoRows.length" size="x-small" color="warning" variant="tonal" class="ml-2">{{ remainingPoRows.length }}</v-chip>
        </v-tab>
        <v-tab value="finished">
          <v-icon start size="18">mdi-check-circle-outline</v-icon>
          Finished Payments
          <v-chip v-if="finishedPoRows.length" size="x-small" color="success" variant="tonal" class="ml-2">{{ finishedPoRows.length }}</v-chip>
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
              <template #item.customerName="{ item }">{{ item.customerName || '—' }}</template>
              <template #item.requestUsers="{ item }">
                <span>{{ item.requestUsers?.length ? item.requestUsers.join(', ') : '—' }}</span>
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
          </v-card-text>
        </v-tabs-window-item>

        <!-- ═══════ TAB 3: Remaining PO Payments ═══════ -->
        <v-tabs-window-item value="remaining">
          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">Approved Purchase Orders that still have a supplier balance to pay.</p>
            <v-card variant="tonal" color="warning" class="payment-total-card mb-4">
              <v-card-text class="d-flex align-center py-3">
                <v-icon icon="mdi-cash-clock" size="28" class="mr-3" />
                <div>
                  <div class="text-caption">Total Ready To Pay</div>
                  <div class="text-h6 font-weight-bold">${{ formatPrice(remainingPaymentsTotal) }}</div>
                </div>
                <v-spacer />
                <v-chip color="warning" variant="flat">{{ remainingPoRows.length }} PO{{ remainingPoRows.length === 1 ? '' : 's' }}</v-chip>
              </v-card-text>
            </v-card>
            <v-data-table
              :headers="remainingPaymentHeaders"
              :items="remainingPoRows"
              :loading="loading"
              density="comfortable"
              :items-per-page="25"
              hover
              class="cursor-pointer"
              @click:row="(_, row) => openPo(row.item, 'withdraw')"
            >
              <template #item.poNumber="{ item }"><span class="font-weight-bold">{{ item.poNumber }}</span></template>
              <template #item.prNumber="{ item }"><span>{{ item.prNumber ? `PR-${item.prNumber}` : '—' }}</span></template>
              <template #item.customerName="{ item }">{{ item.customerName || '—' }}</template>
              <template #item.requestUsers="{ item }">{{ item.requestUsers?.length ? item.requestUsers.join(', ') : '—' }}</template>
              <template #item.totalAmount="{ item }">${{ formatPrice(item.totalAmount) }}</template>
              <template #item.paidAmount="{ item }">${{ formatPrice(item.paidAmount) }}</template>
              <template #item.remainingAmount="{ item }"><span :class="item.remainingAmount > 0 ? 'text-warning font-weight-bold' : 'text-success font-weight-bold'">${{ formatPrice(item.remainingAmount) }}</span></template>
              <template #item.paymentStatus="{ item }"><v-chip size="small" color="warning" variant="tonal">{{ item.waitingForFinalAmount ? 'Waiting For Final Amount' : 'Remaining' }}</v-chip></template>
              <template #item.actions="{ item }"><v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-eye" @click.stop="openPo(item, 'withdraw')">Open</v-btn></template>
            </v-data-table>
          </v-card-text>
        </v-tabs-window-item>

        <!-- ═══════ TAB 4: Finished PO Payments ═══════ -->
        <v-tabs-window-item value="finished">
          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">Purchase Orders whose supplier payments have been completed.</p>
            <v-card variant="tonal" color="success" class="payment-total-card mb-4">
              <v-card-text class="d-flex align-center py-3">
                <v-icon icon="mdi-check-circle-outline" size="28" class="mr-3" />
                <div>
                  <div class="text-caption">Total Finished Payments</div>
                  <div class="text-h6 font-weight-bold">${{ formatPrice(finishedPaymentsTotal) }}</div>
                </div>
                <v-spacer />
                <v-chip color="success" variant="flat">{{ finishedPoRows.length }} PO{{ finishedPoRows.length === 1 ? '' : 's' }}</v-chip>
              </v-card-text>
            </v-card>
            <v-data-table
              :headers="remainingPaymentHeaders"
              :items="finishedPoRows"
              :loading="loading"
              density="comfortable"
              :items-per-page="25"
              hover
              class="cursor-pointer"
              @click:row="(_, row) => openPo(row.item, 'withdraw')"
            >
              <template #item.poNumber="{ item }"><span class="font-weight-bold">{{ item.poNumber }}</span></template>
              <template #item.prNumber="{ item }"><span>{{ item.prNumber ? `PR-${item.prNumber}` : '—' }}</span></template>
              <template #item.customerName="{ item }">{{ item.customerName || '—' }}</template>
              <template #item.requestUsers="{ item }">{{ item.requestUsers?.length ? item.requestUsers.join(', ') : '—' }}</template>
              <template #item.totalAmount="{ item }">${{ formatPrice(item.totalAmount) }}</template>
              <template #item.paidAmount="{ item }"><span class="text-success font-weight-bold">${{ formatPrice(item.paidAmount) }}</span></template>
              <template #item.remainingAmount="{ item }"><span class="text-success font-weight-bold">${{ formatPrice(item.remainingAmount) }}</span></template>
              <template #item.paymentStatus><v-chip size="small" color="success" variant="tonal">Finished</v-chip></template>
              <template #item.actions="{ item }"><v-btn size="small" variant="tonal" color="success" prepend-icon="mdi-eye" @click.stop="openPo(item, 'withdraw')">Open</v-btn></template>
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
          <v-chip v-else size="small" :color="waitingForFinalAmount ? 'warning' : selectedPo.paymentStatus === 'Submitted' ? 'success' : 'primary'">
            {{ waitingForFinalAmount ? 'Waiting For Final Amount' : selectedPo.paymentStatus === 'Submitted' ? 'Submitted' : 'Awaiting POP' }}
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
              <v-alert type="info" variant="tonal" density="compact">Paid to supplier: ${{ formatPrice(selectedPaid) }} · Remaining: ${{ formatPrice(selectedRemaining) }}. Select the actual wallet with each POP.</v-alert>
            </v-col>
          </v-row>

          <v-divider class="mb-4" />

          <v-expansion-panels v-model="detailPanels" multiple variant="accordion" class="mb-4">
            <v-expansion-panel value="documents">
              <v-expansion-panel-title>
                <div class="d-flex align-center">
                  <v-icon icon="mdi-folder-information-outline" size="20" class="mr-2" color="primary" />
                  <span class="font-weight-bold">Supporting Documents</span>
                  <span class="text-caption text-medium-emphasis ml-2">Supplier invoice, bank info, payment request and customer POP</span>
                </div>
              </v-expansion-panel-title>
              <v-expansion-panel-text>

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

              </v-expansion-panel-text>
            </v-expansion-panel>

            <v-expansion-panel v-if="dialogMode === 'withdraw'" value="payment">
              <v-expansion-panel-title>
                <div class="d-flex align-center">
                  <v-icon icon="mdi-cash-check" size="20" class="mr-2" color="warning" />
                  <span class="font-weight-bold">Supplier Payment</span>
                  <v-chip v-if="waitingForFinalAmount" size="x-small" color="warning" variant="flat" class="ml-2">Waiting For Final Amount</v-chip>
                </div>
              </v-expansion-panel-title>
              <v-expansion-panel-text>
                <div v-for="payment in paymentHistory" :key="payment.id" class="d-flex align-center gap-2 mb-2 pa-2 rounded file-row">
                  <v-chip size="x-small" :color="payment.category === 'Bank Fee and Others' ? 'orange' : 'primary'" variant="tonal">{{ payment.category }}</v-chip>
                  <div class="flex-grow-1 text-body-2">
                    PR-{{ payment.prNumber }} · {{ formatPrice(payment.walletAmount) }} {{ payment.walletCurrency }}
                    <span v-if="payment.category === 'Supplier Payment'"> · ${{ formatPrice(payment.amount) }} supplier payment</span>
                  </div>
                  <v-btn v-if="payment.popFileName" size="small" icon="mdi-eye-outline" @click="downloadPaymentProof(payment)" />
                </div>

                <template v-if="waitingForFinalAmount && paymentDraft?.pop">
                  <v-alert type="warning" variant="tonal" density="compact" class="my-3">
                    The POP is saved. Keep this payment in Ready To Pay until the bank reports its final withdrawn amount.
                  </v-alert>
                  <v-text-field
                    v-model.number="finalWalletAmount"
                    type="number"
                    min="0.01"
                    step="0.01"
                    :prefix="currencySymbol(paymentDraft.walletCurrency)"
                    :label="`Final withdrawn amount (${paymentDraft.walletCurrency}) *`"
                    :hint="`Original POP debit: ${formatPrice(paymentDraft.pop.initialWalletAmount)} ${paymentDraft.walletCurrency}`"
                    persistent-hint
                    variant="outlined"
                    class="mb-3"
                  />
                  <v-alert v-if="calculatedBankFee >= 0" type="info" variant="tonal" density="compact" class="mb-3">
                    Bank Fee and Others: <strong>{{ currencySymbol(paymentDraft.walletCurrency) }}{{ formatPrice(calculatedBankFee) }} {{ paymentDraft.walletCurrency }}</strong>
                  </v-alert>
                  <v-btn color="success" variant="flat" prepend-icon="mdi-check-circle" :loading="savingFinalAmount"
                    :disabled="finalWalletAmount < Number(paymentDraft.pop.initialWalletAmount)" @click="saveFinalAmount">
                    Save Final Amount &amp; Finish
                  </v-btn>
                </template>

                <template v-else>
                  <v-select v-model="pickerRequestId" :items="openRequests" item-title="label" item-value="id"
                    label="Payment Request *" variant="outlined" class="mb-3" />
                  <v-alert v-if="!openRequests.length" type="info" variant="tonal" class="mb-3">Create a payment request on the PO page before saving payment details.</v-alert>
                  <v-text-field v-model.number="pickerAmount" type="number" min="0.01" step="0.01" prefix="$"
                    label="Amount paid (USD) *" variant="outlined" :hint="`PR remaining: $${formatPrice(pickerRequest?.remainingAmount)}`" persistent-hint class="mb-3" />
                  <v-select v-model="pickerWalletId" :items="walletBoxes" item-title="label" item-value="id"
                    label="Withdrawn From Wallet *" variant="outlined" prepend-inner-icon="mdi-bank-outline" class="mb-3" />
                  <v-text-field v-if="pickerWallet && pickerWallet.currency !== 'USD'" v-model.number="pickerExchangeRate"
                    type="number" min="0.000001" step="0.000001" :label="`${pickerWallet.currency} per 1 USD *`" variant="outlined" />
                  <p v-if="pickerWallet" class="text-body-2 mb-3">Wallet debit: {{ currencySymbol(pickerWallet.currency) }}{{ formatPrice(pickerAmount * (pickerWallet.currency === 'USD' ? 1 : pickerExchangeRate || 0)) }} {{ pickerWallet.currency }}</p>

                  <div class="d-flex flex-wrap gap-2">
                    <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :loading="savingPaymentDetails"
                      :disabled="!paymentDetailsValid" @click="savePaymentDetails">Save Payment Details</v-btn>
                    <v-btn color="warning" variant="tonal" prepend-icon="mdi-upload" :disabled="!paymentDraft"
                      @click="openPopUploadDialog">Upload POP</v-btn>
                  </div>
                  <v-alert v-if="paymentDraft" type="success" variant="tonal" density="compact" class="mt-3">
                    Details saved for PR-{{ paymentDraft.prNumber }}. You can close this window and upload the POP later.
                  </v-alert>
                </template>
              </v-expansion-panel-text>
            </v-expansion-panel>
          </v-expansion-panels>

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

          <v-alert v-else type="info" variant="tonal" density="compact" class="mt-3">Each POP records the amount paid and debits the selected wallet. The PO is completed for payment automatically when its balance reaches zero.</v-alert>
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDetail = false">Close</v-btn>
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

  <!-- ═══════ POP Upload Dialog — payment details are saved in the first modal ═══════ -->
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
        <v-alert type="info" variant="tonal" density="compact" class="mb-4">
          Payment details are already saved. Add only the POP image or PDF here.
        </v-alert>
        <FileDropZone
          :on-upload="uploadPopFiles"
          label="Drag the POP image here or click to browse"
          accept="image/*,application/pdf,.pdf,.png,.jpg,.jpeg"
          :multiple="false"
        />
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-btn variant="text" @click="showWalletPickerDialog = false">Close</v-btn>
        <v-spacer />
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
type WalletBox = { id: number; label: string; currency: string; company?: string }

// ── Tabs ──────────────────────────────────────────────────────────────────────
const activeTab = ref<'acceptance' | 'remaining' | 'finished'>('acceptance')

// ── PO Queue ──────────────────────────────────────────────────────────────────
const loading = ref(false)
const queue = ref<POItem[]>([])
const paymentRequests = ref<any[]>([])
const isPaymentStage = (po: POItem) =>
  po.paymentApproval === 'Accepted' || po.paymentStatus === 'Submitted'
    || po.paymentStatus === 'PartiallyPaid' || po.paymentStatus === 'WaitingForFinalAmount'
const acceptanceQueue = computed(() => queue.value.filter(p => !isPaymentStage(p)))
// Older withdrawals were submitted before PaymentApproval and the supplier-payment
// ledger existed. Keep them in the Remaining Payments history as well.
const remainingPaymentQueue = computed(() => queue.value.filter(isPaymentStage))
const paymentProgressRows = computed(() => remainingPaymentQueue.value.map(po => {
  const requests = paymentRequests.value.filter(pr => pr.poId === po.id)
  const totalAmount = Number(requests[0]?.poTotalAmount ?? po.totalAmount) || 0
  const recordedPaidAmount = requests.reduce((sum, pr) => sum + Number(pr.paidAmount || 0), 0)
  // A legacy submitted PO has no individual POP ledger entries, but its submitted
  // status confirms that the full PO amount was paid before ledger tracking began.
  const paidAmount = recordedPaidAmount > 0 || po.paymentStatus !== 'Submitted'
    ? recordedPaidAmount
    : totalAmount
  return { ...po, totalAmount, paidAmount, prNumber: requests.map(pr => pr.prNumber).join(', '),
    remainingAmount: Math.max(0, totalAmount - paidAmount), waitingForFinalAmount: po.paymentStatus === 'WaitingForFinalAmount' }
}))
const remainingPoRows = computed(() => paymentProgressRows.value.filter(po => po.remainingAmount > 0 || po.waitingForFinalAmount))
const finishedPoRows = computed(() => paymentProgressRows.value.filter(po => po.remainingAmount <= 0 && !po.waitingForFinalAmount))
const remainingPaymentsTotal = computed(() => remainingPoRows.value.reduce((sum, po) => sum + po.remainingAmount, 0))
const finishedPaymentsTotal = computed(() => finishedPoRows.value.reduce((sum, po) => sum + po.paidAmount, 0))
const acceptancePoBadge = computed(() => acceptanceQueue.value.length)

// ── Wallet boxes (for create dialog) ─────────────────────────────────────────
const walletBoxes = ref<WalletBox[]>([])

// ── Saved payment details + POP/final-bank-amount workflow ───────────────────
const showWalletPickerDialog = ref(false)
const pickerWalletId = ref<number | null>(null)
const pickerRequestId = ref<number | null>(null)
const pickerAmount = ref(0)
const pickerExchangeRate = ref<number | null>(null)
const paymentDraft = ref<any>(null)
const savingPaymentDetails = ref(false)
const savingFinalAmount = ref(false)
const finalWalletAmount = ref(0)
const paymentHistory = ref<any[]>([])
const selectedRequests = computed(() => paymentRequests.value.filter(pr => pr.poId === selectedPo.value?.id))
const openRequests = computed(() => selectedRequests.value.filter(pr => pr.remainingAmount > 0).map(pr => ({ ...pr, label: `PR-${pr.prNumber} · remaining $${formatPrice(pr.remainingAmount)}` })))
const pickerRequest = computed(() => openRequests.value.find(pr => pr.id === pickerRequestId.value))
const pickerWallet = computed(() => walletBoxes.value.find(w => w.id === pickerWalletId.value))
const selectedTotal = computed(() => Number(selectedRequests.value[0]?.poTotalAmount ?? selectedPo.value?.totalAmount ?? 0))
const selectedPaid = computed(() => {
  const recorded = selectedRequests.value.reduce((sum, pr) => sum + Number(pr.paidAmount || 0), 0)
  return recorded > 0 || selectedPo.value?.paymentStatus !== 'Submitted' ? recorded : selectedTotal.value
})
const selectedRemaining = computed(() => Math.max(0, selectedTotal.value - selectedPaid.value))
const paymentDetailsValid = computed(() => !!pickerRequest.value && !!pickerWallet.value
  && pickerAmount.value > 0 && pickerAmount.value <= pickerRequest.value.remainingAmount && pickerAmount.value <= selectedRemaining.value
  && (pickerWallet.value.currency === 'USD' || Number(pickerExchangeRate.value) > 0))
const waitingForFinalAmount = computed(() => selectedPo.value?.paymentStatus === 'WaitingForFinalAmount' || !!paymentDraft.value?.pop)
const calculatedBankFee = computed(() => finalWalletAmount.value - Number(paymentDraft.value?.pop?.initialWalletAmount || 0))
watch(pickerRequestId, () => {
  if (paymentDraft.value?.paymentRequestId === pickerRequestId.value) return
  pickerAmount.value = pickerRequest.value?.remainingAmount ?? 0
})
watch(pickerWalletId, () => {
  if (paymentDraft.value?.walletId === pickerWalletId.value) return
  pickerExchangeRate.value = null
})


// ── PO detail dialog ──────────────────────────────────────────────────────────
const showDetail = ref(false)
const selectedPo = ref<POItem | null>(null)
const dialogMode = ref<'accept' | 'withdraw'>('accept')
const detailPanels = ref<string[]>(['payment'])

const supplierInvoiceFile = ref<FileInfo | null>(null)
const bankInfoFiles = ref<FileInfo[]>([])
const dpFiles = ref<FileInfo[]>([])
const importDetail = ref<any>(null)

type CustomerPayment = { id: number; fileName: string; amount: number; notes?: string | null; createdAt: string }
const customerPayments = ref<CustomerPayment[]>([])
const customerTotalPaid = ref(0)
const customerInvoiceTotal = ref<number | null>(null)
const customerIsPaid = ref(false)

const accepting = ref(false)
const rejecting = ref(false)
const showRejectDialog = ref(false)
const rejectionNote = ref('')
// popInputRef removed — POP upload is now handled via the dialog

// ── Table headers ─────────────────────────────────────────────────────────────
const acceptanceHeaders = [
  { title: 'PO Number', key: 'poNumber' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Sales Order', key: 'invoiceNumber' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Requested By', key: 'requestUsers', sortable: false },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Admin Approved', key: 'adminApprovalAt' },
  { title: 'Status', key: 'paymentApproval' },
  { title: '', key: 'actions', sortable: false, width: 120 },
]
const remainingPaymentHeaders = [
  { title: 'PO#', key: 'poNumber' },
  { title: 'PR#', key: 'prNumber', sortable: false },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Requested By', key: 'requestUsers', sortable: false },
  { title: 'Total PO Price', key: 'totalAmount' },
  { title: 'Paid to Supplier', key: 'paidAmount' },
  { title: 'Remaining to Pay', key: 'remainingAmount' },
  { title: 'Payment Status', key: 'paymentStatus' },
  { title: '', key: 'actions', sortable: false, width: 100 },
]
// ── Load all data ─────────────────────────────────────────────────────────────
async function loadAll() {
  await Promise.all([loadQueue(), loadPaymentRequests(), loadWalletBoxes()])
}

async function loadQueue() {
  loading.value = true
  try { queue.value = await api.get<any[]>('/purchase-orders/payment-queue') }
  catch { showSnack('Failed to load payment queue', 'error') }
  finally { loading.value = false }
}

async function loadPaymentRequests() {
  try { paymentRequests.value = await api.get<any[]>('/paymentrequests') }
  catch { paymentRequests.value = [] }
}

async function loadWalletBoxes() {
  try {
    const boxes = await api.get<any[]>('/supplier-payments/wallets')
    walletBoxes.value = boxes.map((b: any) => ({
      id: b.id,
      // Wallets are identified by their own name, not the company preset behind them.
      label: `${b.name} (${b.currency})`,
      currency: b.currency,
      company: b.companyName,
    }))
  } catch { /* silent */ }
}

// ── PO actions ────────────────────────────────────────────────────────────────
async function openPo(po: POItem, mode: 'accept' | 'withdraw') {
  selectedPo.value = queue.value.find(p => p.id === po.id) || po
  paymentHistory.value = []
  paymentDraft.value = null
  detailPanels.value = mode === 'withdraw' ? ['payment'] : []
  try {
    await loadPaymentRequests()
    paymentHistory.value = await api.get<any[]>(`/supplier-payments/po/${po.id}`)
    paymentDraft.value = await api.get<any>(`/supplier-payments/po/${po.id}/draft`)
  } catch { showSnack('Failed to load supplier payments', 'error') }
  if (paymentDraft.value) {
    pickerRequestId.value = paymentDraft.value.paymentRequestId
    pickerWalletId.value = paymentDraft.value.walletId
    pickerExchangeRate.value = paymentDraft.value.exchangeRate
    pickerAmount.value = Number(paymentDraft.value.amount)
    finalWalletAmount.value = Number(paymentDraft.value.pop?.initialWalletAmount || 0)
  } else {
    pickerRequestId.value = openRequests.value[0]?.id ?? null
    pickerWalletId.value = null
    pickerExchangeRate.value = null
    pickerAmount.value = pickerRequest.value?.remainingAmount ?? 0
    finalWalletAmount.value = 0
  }
  dialogMode.value = mode
  showDetail.value = true
  supplierInvoiceFile.value = null
  bankInfoFiles.value = []
  dpFiles.value = []
  importDetail.value = null
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
    showSnack('Payment request accepted — moved to Remaining Payments', 'success')
    showDetail.value = false
    await loadQueue()
    activeTab.value = 'remaining'
  } catch (e: any) {
    showSnack(e?.data?.message || 'Accept failed', 'error')
  } finally { accepting.value = false }
}

function openPopUploadDialog() {
  if (!paymentDraft.value) {
    showSnack('Save the payment details before uploading the POP', 'warning')
    return
  }
  showWalletPickerDialog.value = true
}

async function savePaymentDetails() {
  if (!selectedPo.value || !paymentDetailsValid.value || savingPaymentDetails.value) return
  savingPaymentDetails.value = true
  try {
    paymentDraft.value = await api.post(`/supplier-payments/po/${selectedPo.value.id}/draft`, {
      paymentRequestId: pickerRequestId.value,
      walletId: pickerWalletId.value,
      amount: pickerAmount.value,
      exchangeRate: pickerWallet.value?.currency === 'USD' ? 1 : pickerExchangeRate.value,
    })
    showSnack('Payment details saved. You can upload the POP now or later.', 'success')
    await loadPaymentRequests()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not save payment details', 'error')
  } finally { savingPaymentDetails.value = false }
}

async function uploadPopFiles(files: File[], onProgress: (done: number) => void) {
  if (!selectedPo.value || !paymentDraft.value || files.length === 0) return
  try {
    const form = new FormData()
    form.append('file', files[0]!)
    await $fetch(`${api.baseURL}/supplier-payments/po/${selectedPo.value.id}/pop`, {
      method: 'POST', body: form, headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    onProgress(1)
    showSnack('POP uploaded. Waiting for the bank’s final withdrawn amount.', 'success')
    showWalletPickerDialog.value = false
    await loadQueue()
    await openPo(selectedPo.value, 'withdraw')
  } catch (e: any) {
    showSnack(e?.data?.message || 'POP upload failed. The saved payment details were kept.', 'error')
    throw e
  }
}

async function saveFinalAmount() {
  if (!selectedPo.value || !paymentDraft.value?.pop || savingFinalAmount.value) return
  savingFinalAmount.value = true
  try {
    const result = await api.post<any>(`/supplier-payments/po/${selectedPo.value.id}/final-amount`, {
      finalWalletAmount: finalWalletAmount.value,
    })
    showSnack(result.bankFee > 0
      ? `Final amount saved. ${formatPrice(result.bankFee)} ${result.walletCurrency} recorded as Bank Fee and Others.`
      : 'Final amount saved with no additional bank fee.', 'success')
    showDetail.value = false
    await loadAll()
    activeTab.value = result.complete ? 'finished' : 'remaining'
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not save the final bank amount', 'error')
  } finally { savingFinalAmount.value = false }
}

async function downloadPaymentProof(payment: any) {
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/supplier-payments/${payment.id}/file`, {
      responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob, payment.popFileName)
  } catch { showSnack('Failed to open payment proof', 'error') }
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

// ── Helpers ───────────────────────────────────────────────────────────────────
function currencySymbol(currency: string) {
  return ({ USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' } as Record<string, string>)[currency] ?? `${currency} `
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

.payment-total-card {
  max-width: 440px;
}
</style>
