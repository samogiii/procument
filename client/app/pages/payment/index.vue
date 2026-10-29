<template>
  <div>
    <div class="d-flex align-center mb-4 mb-md-6">
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Payments</h1>
      <v-spacer />
      <v-btn v-if="whSyncEnabled" variant="tonal" size="small" color="primary" prepend-icon="mdi-cellphone-arrow-down"
        class="mr-2" :loading="whSyncing" @click="runWarehouseSync">Sync warehouse</v-btn>
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="loadAll">Refresh</v-btn>
    </div>

    <v-alert v-if="whSyncing" type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-sync">{{ whSyncText }}</v-alert>
    <v-alert v-else-if="whSyncResult" :type="whSyncResult.skipped.length ? 'warning' : 'success'" variant="tonal" density="compact"
      class="mb-4" closable @click:close="whSyncResult = null">
      Warehouse: {{ whSyncResult.paymentsSent }} payment{{ whSyncResult.paymentsSent === 1 ? '' : 's' }} sent ·
      {{ whSyncResult.paid }} paid and waiting for your review in Ready To pay · {{ whSyncResult.docs }} document{{ whSyncResult.docs === 1 ? '' : 's' }} sent
      <ul v-if="whSyncResult.skipped.length" class="mt-1 ml-4 text-body-2">
        <li v-for="(s, i) in whSyncResult.skipped" :key="i">{{ s }}</li>
      </ul>
    </v-alert>

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
              <template #item.poNumber="{ item }">
                <div class="d-flex align-center ga-2">
                  <NuxtLink :to="item.poLink || `/purchase-orders/${item.id}`" class="font-weight-bold text-primary text-decoration-none" @click.stop>
                    {{ item.poNumber }}
                  </NuxtLink>
                  <v-chip size="x-small" :color="item.poType === 'Stock PO' ? 'teal' : 'grey'" variant="tonal">{{ item.poType || 'Customer PO' }}</v-chip>
                </div>
              </template>
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
              <template #item.poNumber="{ item }">
                <div class="d-flex align-center ga-2">
                  <NuxtLink :to="item.poLink || `/purchase-orders/${item.id}`" class="font-weight-bold text-primary text-decoration-none" @click.stop>{{ item.poNumber }}</NuxtLink>
                  <v-chip size="x-small" :color="item.poType === 'Stock PO' ? 'teal' : 'grey'" variant="tonal">{{ item.poType || 'Customer PO' }}</v-chip>
                </div>
              </template>
              <template #item.prNumber="{ item }"><span>{{ item.prNumber ? `PR-${item.prNumber}` : '—' }}</span></template>
              <template #item.customerName="{ item }">{{ item.customerName || '—' }}</template>
              <template #item.requestUsers="{ item }">{{ item.requestUsers?.length ? item.requestUsers.join(', ') : '—' }}</template>
              <template #item.totalAmount="{ item }">${{ formatPrice(item.totalAmount) }}</template>
              <template #item.paidAmount="{ item }">${{ formatPrice(item.paidAmount) }}</template>
              <template #item.remainingAmount="{ item }"><span :class="item.remainingAmount > 0 ? 'text-warning font-weight-bold' : 'text-success font-weight-bold'">${{ formatPrice(item.remainingAmount) }}</span></template>
              <template #item.paymentStatus="{ item }">
                <v-chip v-if="item.warehousePaid" size="small" color="info" variant="flat" prepend-icon="mdi-cellphone-check">Paid in warehouse — review</v-chip>
                <v-chip v-else size="small" color="warning" variant="tonal">
                  {{ item.waitingForFinalAmount ? 'Waiting For Final Amount' : item.hasDetails ? 'Waiting For POP' : 'Remaining' }}
                </v-chip>
              </template>
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
              <template #item.poNumber="{ item }">
                <div class="d-flex align-center ga-2">
                  <NuxtLink :to="item.poLink || `/purchase-orders/${item.id}`" class="font-weight-bold text-primary text-decoration-none" @click.stop>{{ item.poNumber }}</NuxtLink>
                  <v-chip size="x-small" :color="item.poType === 'Stock PO' ? 'teal' : 'grey'" variant="tonal">{{ item.poType || 'Customer PO' }}</v-chip>
                </div>
              </template>
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
              <div class="font-weight-bold text-success">${{ formatPrice(dialogMode === 'withdraw' ? selectedTotal : selectedPo.totalAmount) }}</div>
            </v-col>
            <v-col v-if="dialogMode === 'withdraw'" cols="6" md="3">
              <div class="text-caption text-medium-emphasis">Wire Fee</div>
              <div class="font-weight-medium">${{ formatPrice(pickerWireFee) }}</div>
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
                  <span class="text-caption text-medium-emphasis ml-2">Supplier invoice, PO PDF, bank info, payment request{{ isStockPo ? '' : ' and customer POP' }}</span>
                </div>
              </v-expansion-panel-title>
              <v-expansion-panel-text>

          <!-- Supplier Invoice (Stock POs: the PI and invoice uploaded on the PO page) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-invoice-text" size="18" class="mr-1" color="primary" />
              Supplier Invoice
            </div>
            <div v-if="supplierInvoiceFiles.length" class="d-flex flex-column gap-2">
              <div v-for="f in supplierInvoiceFiles" :key="f.docId ?? f.name" class="d-flex align-center gap-2 pa-2 rounded file-row">
                <v-icon icon="mdi-file-check" color="success" size="20" />
                <div class="d-flex flex-column flex-grow-1" style="min-width:0;">
                  <span class="text-body-2 font-weight-medium text-truncate">{{ f.name }}</span>
                  <span v-if="f.label" class="text-caption text-medium-emphasis">{{ f.label }}</span>
                </div>
                <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-eye-outline" @click="downloadSupplierFile(f)">Preview</v-btn>
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No supplier invoice uploaded yet.</v-alert>
          </div>

          <!-- PO PDF (saved each time the PO PDF is generated) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-file-pdf-box" size="18" class="mr-1" color="error" />
              PO PDF
            </div>
            <div v-if="poPdfFiles.length" class="d-flex flex-column gap-2">
              <div v-for="f in poPdfFiles" :key="f.name" class="d-flex align-center gap-2 pa-2 rounded file-row">
                <v-icon icon="mdi-file-pdf-box" color="error" size="20" />
                <div class="d-flex flex-column flex-grow-1">
                  <span class="text-body-2 font-weight-medium">{{ f.name }}</span>
                  <span class="text-caption text-medium-emphasis">{{ new Date(f.modifiedAt).toLocaleString() }}</span>
                </div>
                <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadSupplierFile(f)" />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No PO PDF generated yet.</v-alert>
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
                <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadSupplierFile(f)" />
              </div>
            </div>
            <v-alert v-else-if="!hasBankDetail" type="info" variant="tonal" density="compact" icon="mdi-information-outline">No bank info uploaded yet.</v-alert>
          </div>

          <!-- Supplier Bank Detail (Bank Information on the PO page) -->
          <div v-if="hasBankDetail" class="mb-4 pa-3 rounded file-row">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-bank" size="18" class="mr-1" color="success" />
              Supplier Bank Detail
            </div>
            <v-row dense>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Beneficiary</div><div class="text-body-2">{{ importDetail.beneficiary || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Reference</div><div class="text-body-2">{{ importDetail.reference || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Bank Name</div><div class="text-body-2">{{ importDetail.bankName || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Account Number</div><div class="text-body-2">{{ importDetail.bankAccountNumber || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Bank Address</div><div class="text-body-2">{{ importDetail.bankAddress || '—' }}</div></v-col>
              <v-col cols="6" md="3"><div class="text-caption text-medium-emphasis">Bank City</div><div class="text-body-2">{{ importDetail.bankCity || '—' }}</div></v-col>
              <v-col cols="6" md="3"><div class="text-caption text-medium-emphasis">Bank Country</div><div class="text-body-2">{{ importDetail.bankCountry || '—' }}</div></v-col>
              <v-col cols="6" md="3"><div class="text-caption text-medium-emphasis">Swift Code</div><div class="text-body-2">{{ importDetail.swiftCode || '—' }}</div></v-col>
              <v-col cols="6" md="3"><div class="text-caption text-medium-emphasis">ABA (Routing)</div><div class="text-body-2">{{ importDetail.aba || '—' }}</div></v-col>
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
                <v-btn size="small" variant="tonal" color="primary" icon="mdi-eye-outline" @click="downloadSupplierFile(f)" />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">No Payment Request generated yet.</v-alert>
          </div>

          <!-- Customer Paid POPs (Stock POs have no customer) -->
          <div v-if="!isStockPo" class="mb-4">
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

            <v-expansion-panel value="payment">
              <v-expansion-panel-title>
                <div class="d-flex align-center">
                  <v-icon icon="mdi-cash-check" size="20" class="mr-2" color="warning" />
                  <span class="font-weight-bold">Supplier Payment</span>
                  <v-chip v-if="paymentDraft?.warehouse" size="x-small" color="info" variant="flat" class="ml-2">Paid in warehouse</v-chip>
                  <v-chip v-else-if="waitingForFinalAmount" size="x-small" color="warning" variant="flat" class="ml-2">Waiting For Final Amount</v-chip>
                  <span v-if="dialogMode === 'accept'" class="text-caption text-medium-emphasis ml-2">Saving these details accepts the request</span>
                </div>
              </v-expansion-panel-title>
              <v-expansion-panel-text>
                <!-- Supplier credit the PO's company holds with this supplier (or another company's, with a way to switch) -->
                <SupplierCreditSuggestion v-if="dialogMode === 'withdraw'" :po-id="selectedPo.id" :refresh-key="creditRefreshKey" @applied="onCreditApplied" />

                <!-- rowKey is unique across wallet payments and credit applications (their ids can collide) -->
                <div v-for="payment in paymentHistory" :key="payment.rowKey ?? payment.id" class="d-flex align-center gap-2 mb-2 pa-2 rounded file-row">
                  <v-chip size="x-small" :color="historyChipColor(payment.category)" variant="tonal">{{ payment.category }}</v-chip>
                  <div v-if="payment.creditEntryId" class="flex-grow-1 text-body-2">
                    PR-{{ payment.prNumber }} · {{ payment.amount < 0 ? '−' : '' }}${{ formatPrice(Math.abs(payment.amount)) }}
                    {{ payment.amount < 0 ? 'credit returned' : 'paid from credit' }} ({{ payment.walletName }})
                  </div>
                  <div v-else class="flex-grow-1 text-body-2">
                    PR-{{ payment.prNumber }} · {{ formatPrice(payment.walletAmount) }} {{ payment.walletCurrency }}
                    <span v-if="payment.category === 'Supplier Payment'"> · ${{ formatPrice(payment.amount) }} supplier payment</span>
                  </div>
                  <v-btn v-if="payment.popFileName" size="small" icon="mdi-eye-outline" @click="downloadPaymentProof(payment)" />
                </div>

                <!-- Paid from the warehouse app: review its POP and final amount -->
                <template v-if="dialogMode === 'withdraw' && paymentDraft?.warehouse">
                  <v-alert type="info" variant="tonal" density="compact" icon="mdi-cellphone-check" class="my-3">
                    Paid in the warehouse by <strong>{{ paymentDraft.warehouse.paidBy }}</strong>
                    on {{ new Date(paymentDraft.warehouse.paidAt).toLocaleString() }}. Check the POP and amounts, then accept or send it back.
                  </v-alert>
                  <div class="d-flex align-center gap-2 pa-2 rounded file-row mb-3">
                    <v-icon icon="mdi-file-document-check-outline" color="success" size="20" />
                    <span class="text-body-2 font-weight-medium flex-grow-1 text-truncate">{{ paymentDraft.warehouse.fileName }}</span>
                    <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-eye-outline" @click="previewWarehousePop">Preview</v-btn>
                  </div>
                  <v-row dense class="mb-2">
                    <v-col cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Amount paid</div>
                      <div class="font-weight-bold">${{ formatPrice(paymentDraft.amount) }}</div>
                    </v-col>
                    <v-col v-if="paymentDraft.walletCurrency !== 'USD'" cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Rate ({{ paymentDraft.walletCurrency }} per 1 USD)</div>
                      <div class="font-weight-bold">{{ paymentDraft.warehouse.exchangeRate }}</div>
                      <div v-if="paymentDraft.exchangeRate && paymentDraft.exchangeRate !== paymentDraft.warehouse.exchangeRate" class="text-caption text-medium-emphasis">
                        saved here: {{ paymentDraft.exchangeRate }}
                      </div>
                    </v-col>
                    <v-col cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Wallet debit ({{ paymentDraft.walletName }})</div>
                      <div class="font-weight-medium">{{ currencySymbol(paymentDraft.walletCurrency) }}{{ formatPrice(paymentDraft.warehouse.initialWalletAmount) }}</div>
                    </v-col>
                    <v-col cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Final amount (bank)</div>
                      <div class="font-weight-bold text-success">{{ currencySymbol(paymentDraft.walletCurrency) }}{{ formatPrice(paymentDraft.warehouse.finalAmount) }}</div>
                    </v-col>
                  </v-row>
                  <PaymentFinalCalc class="mb-3" :amount="paymentDraft.amount" :currency="paymentDraft.walletCurrency"
                    :rate="paymentDraft.warehouse.exchangeRate" :debit="paymentDraft.warehouse.initialWalletAmount"
                    :handling="paymentDraft.warehouse.handlingFee" :wire="paymentDraft.warehouse.bankWireFee" />
                  <div class="d-flex flex-wrap gap-2">
                    <v-btn color="error" variant="tonal" prepend-icon="mdi-undo" @click="showWarehouseRejectDialog = true">Send Back</v-btn>
                    <v-btn color="success" variant="flat" prepend-icon="mdi-check-circle" :loading="acceptingWarehouse" @click="acceptWarehousePayment">
                      Accept &amp; Finish
                    </v-btn>
                  </div>
                </template>

                <template v-else-if="waitingForFinalAmount && paymentDraft?.pop">
                  <v-alert type="warning" variant="tonal" density="compact" class="my-3">
                    The POP is saved. Keep this payment in Ready To Pay until the bank reports its final withdrawn amount.
                  </v-alert>
                  <v-row dense>
                    <v-col cols="6">
                      <v-text-field v-model.number="handlingFee" type="number" min="0" step="0.01"
                        :prefix="currencySymbol(paymentDraft.walletCurrency)" :label="`Handling fee (${paymentDraft.walletCurrency})`"
                        variant="outlined" hide-details />
                    </v-col>
                    <v-col cols="6">
                      <v-text-field v-model.number="bankWireFee" type="number" min="0" step="0.01"
                        :prefix="currencySymbol(paymentDraft.walletCurrency)" :label="`Wire fee (${paymentDraft.walletCurrency})`"
                        variant="outlined" hide-details />
                    </v-col>
                  </v-row>
                  <PaymentFinalCalc class="my-3" :amount="paymentDraft.amount" :currency="paymentDraft.walletCurrency"
                    :rate="paymentDraft.walletCurrency === 'USD' ? null : paymentDraft.exchangeRate"
                    :debit="paymentDraft.pop.initialWalletAmount" :handling="handlingFee" :wire="bankWireFee" />
                  <v-btn color="success" variant="flat" prepend-icon="mdi-check-circle" :loading="savingFinalAmount"
                    :disabled="!feesValid" @click="saveFinalAmount">
                    Accept Final Price &amp; Finish
                  </v-btn>
                </template>

                <!-- Ready To Pay with saved details: only the POP (here or from the warehouse app) is left -->
                <template v-else-if="dialogMode === 'withdraw' && paymentDraft && !editingDetails">
                  <v-alert v-if="paymentDraft.warehouseRejectNote" type="warning" variant="tonal" density="compact" class="my-3">
                    Sent back to the warehouse: {{ paymentDraft.warehouseRejectNote }}
                  </v-alert>
                  <v-row dense class="my-2">
                    <v-col cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Payment Request</div>
                      <div class="font-weight-medium">PR-{{ paymentDraft.prNumber }}</div>
                    </v-col>
                    <v-col cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Amount paid (USD)</div>
                      <div class="font-weight-bold">${{ formatPrice(paymentDraft.amount) }}</div>
                    </v-col>
                    <v-col cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Wire Fee (USD)</div>
                      <div class="font-weight-medium">${{ formatPrice(paymentDraft.wireFee) }}</div>
                    </v-col>
                    <v-col cols="6" md="3">
                      <div class="text-caption text-medium-emphasis">Withdrawn From Wallet</div>
                      <div class="font-weight-medium">{{ paymentDraft.walletName }} ({{ paymentDraft.walletCurrency }})</div>
                    </v-col>
                    <v-col v-if="paymentDraft.walletCurrency !== 'USD'" cols="12">
                      <div class="text-body-2">
                        Wallet debit: {{ currencySymbol(paymentDraft.walletCurrency) }}{{ formatPrice(paymentDraft.amount * (paymentDraft.exchangeRate || 0)) }}
                        {{ paymentDraft.walletCurrency }} at {{ paymentDraft.exchangeRate }} {{ paymentDraft.walletCurrency }} per 1 USD
                        <span class="text-medium-emphasis">(the warehouse can enter the actual rate)</span>
                      </div>
                    </v-col>
                  </v-row>
                  <div class="d-flex flex-wrap gap-2">
                    <v-btn color="warning" variant="flat" prepend-icon="mdi-upload" @click="openPopUploadDialog">Upload POP</v-btn>
                    <v-btn variant="text" prepend-icon="mdi-pencil-outline" @click="editingDetails = true">Change details</v-btn>
                  </div>
                  <p v-if="whSyncEnabled" class="text-caption text-medium-emphasis mt-2">
                    Or let the warehouse pay it: <strong>Sync warehouse</strong> sends it to the phone app, the next sync brings its POP back here.
                  </p>
                </template>

                <template v-else>
                  <v-select v-model="pickerRequestId" :items="openRequests" item-title="label" item-value="id"
                    label="Payment Request *" variant="outlined" class="mb-3" />
                  <v-alert v-if="!openRequests.length" type="info" variant="tonal" class="mb-3">Create a payment request on the PO page before saving payment details.</v-alert>
                  <v-text-field v-model.number="pickerAmount" type="number" min="0.01" step="0.01" prefix="$"
                    label="Amount paid (USD) *" variant="outlined" :hint="`PR remaining: $${formatPrice(pickerRequestRemaining)}`" persistent-hint class="mb-3" />
                  <v-text-field v-model.number="pickerWireFee" type="number" min="0" step="0.01" prefix="$"
                    label="Wire Fee (USD)" variant="outlined" hint="Included in the PR and PO payment total" persistent-hint class="mb-3" />
                  <v-select v-model="pickerWalletId" :items="walletBoxes" item-title="label" item-value="id"
                    label="Withdrawn From Wallet *" variant="outlined" prepend-inner-icon="mdi-bank-outline" class="mb-3" />
                  <v-text-field v-if="pickerWallet && pickerWallet.currency !== 'USD'" v-model.number="pickerExchangeRate"
                    type="number" min="0.000001" step="0.000001" :label="`${pickerWallet.currency} per 1 USD *`" variant="outlined" />
                  <p v-if="pickerWallet" class="text-body-2 mb-3">Wallet debit: {{ currencySymbol(pickerWallet.currency) }}{{ formatPrice(pickerAmount * (pickerWallet.currency === 'USD' ? 1 : pickerExchangeRate || 0)) }} {{ pickerWallet.currency }}</p>

                  <!-- The Payment Request tab saves (and accepts) from the buttons below the panels -->
                  <div v-if="dialogMode === 'withdraw'" class="d-flex flex-wrap gap-2">
                    <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :loading="savingPaymentDetails"
                      :disabled="!paymentDetailsValid" @click="savePaymentDetails">Save Payment Details</v-btn>
                    <v-btn v-if="editingDetails" variant="text" @click="cancelEditDetails">Cancel</v-btn>
                  </div>
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
              <v-btn class="flex-grow-1" color="success" variant="flat" prepend-icon="mdi-content-save-check" :loading="savingPaymentDetails"
                :disabled="!paymentDetailsValid" @click="savePaymentDetails">Save Payment Details &amp; Accept</v-btn>
            </div>
            <p class="text-caption text-medium-emphasis mt-2">Saving the payment details accepts the request and moves it to Ready To pay, where only the POP and final amount are left.</p>
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

    <!-- Warehouse payment: send back -->
    <v-dialog v-model="showWarehouseRejectDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title class="pa-4">Send Back to Warehouse</v-card-title>
        <v-card-text class="pa-4">
          <div class="mb-4 text-body-2">The warehouse POP for <strong>{{ selectedPo?.poNumber }}</strong> is removed. After the next sync the phone shows your note and asks for a new POP and amount.</div>
          <v-textarea v-model="warehouseRejectNote" label="What should the warehouse fix?" rows="3" variant="outlined" density="comfortable" counter="500" hide-details="auto" />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showWarehouseRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="tonal" :disabled="!warehouseRejectNote.trim()" :loading="rejectingWarehouse" @click="rejectWarehousePayment">Send Back</v-btn>
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
import type { ApplySupplierCreditResult } from '~/utils/supplierCredits'
import type { WarehousePaymentSyncSummary } from '~/composables/useWarehouseSync'

const api = useApi()
const config = useRuntimeConfig()
const authStore = useAuthStore()
const docPreview = useDocPreview()

/** A supplier-side file; docId = a Stock PO document (PO page uploads), label = its kind. */
type FileInfo = { name: string; size?: number; modifiedAt: string; category: string; docId?: number; label?: string }
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
    remainingAmount: Math.max(0, totalAmount - paidAmount), waitingForFinalAmount: po.paymentStatus === 'WaitingForFinalAmount',
    hasDetails: requests.some(pr => pr.pendingUploadId), warehousePaid: requests.some(pr => pr.warehousePaidAt) }
}))
const remainingPoRows = computed(() => paymentProgressRows.value.filter(po => po.remainingAmount > 0 || po.waitingForFinalAmount || po.warehousePaid))
const finishedPoRows = computed(() => paymentProgressRows.value.filter(po => po.remainingAmount <= 0 && !po.waitingForFinalAmount && !po.warehousePaid))
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
const pickerWireFee = ref(0)
const paymentDraft = ref<any>(null)
const savingPaymentDetails = ref(false)
const savingFinalAmount = ref(false)
// Bank charges on the POP (wallet currency); the final amount = wallet debit + handling + wire fee.
const handlingFee = ref<number | null>(0)
const bankWireFee = ref<number | null>(0)
const paymentHistory = ref<any[]>([])
/** Ready To Pay with saved details shows them read-only; "Change details" opens the form again. */
const editingDetails = ref(false)
/** Bumped whenever what is left to pay may have changed, so the credit suggestion reloads. */
const creditRefreshKey = ref(0)
const selectedRequests = computed(() => paymentRequests.value.filter(pr => pr.poId === selectedPo.value?.id))
const openRequests = computed(() => selectedRequests.value.filter(pr => pr.remainingAmount > 0).map(pr => ({ ...pr, label: `PR-${pr.prNumber} · remaining $${formatPrice(pr.remainingAmount)}` })))
const pickerRequest = computed(() => openRequests.value.find(pr => pr.id === pickerRequestId.value))
const pickerWallet = computed(() => walletBoxes.value.find(w => w.id === pickerWalletId.value))
const storedWireFee = computed(() => Number(selectedRequests.value[0]?.wireFee ?? 0))
const wireFeeDelta = computed(() => Number(pickerWireFee.value || 0) - storedWireFee.value)
const selectedTotal = computed(() => Number(selectedRequests.value[0]?.poTotalAmount ?? selectedPo.value?.totalAmount ?? 0) + wireFeeDelta.value)
const pickerRequestRemaining = computed(() => Math.max(0, Number(pickerRequest.value?.remainingAmount ?? 0) + wireFeeDelta.value))
const selectedPaid = computed(() => {
  const recorded = selectedRequests.value.reduce((sum, pr) => sum + Number(pr.paidAmount || 0), 0)
  return recorded > 0 || selectedPo.value?.paymentStatus !== 'Submitted' ? recorded : selectedTotal.value
})
const selectedRemaining = computed(() => Math.max(0, selectedTotal.value - selectedPaid.value))
const paymentDetailsValid = computed(() => !!pickerRequest.value && !!pickerWallet.value
  && pickerWireFee.value >= 0 && pickerAmount.value > 0 && pickerAmount.value <= pickerRequestRemaining.value && pickerAmount.value <= selectedRemaining.value
  && (pickerWallet.value.currency === 'USD' || Number(pickerExchangeRate.value) > 0))
const waitingForFinalAmount = computed(() => selectedPo.value?.paymentStatus === 'WaitingForFinalAmount' || !!paymentDraft.value?.pop)
const fee = (v: number | null) => Number(v || 0)
const feesValid = computed(() => [handlingFee.value, bankWireFee.value].every(v => fee(v) >= 0 && Math.abs(Math.round(fee(v) * 100) - fee(v) * 100) < 1e-6))
watch(pickerRequestId, () => {
  if (paymentDraft.value?.paymentRequestId === pickerRequestId.value) return
  pickerAmount.value = pickerRequest.value?.remainingAmount ?? 0
})
watch(pickerWalletId, () => {
  if (paymentDraft.value?.walletId === pickerWalletId.value) return
  pickerExchangeRate.value = null
})
watch(pickerWireFee, (next, previous) => {
  const delta = Number(next || 0) - Number(previous || 0)
  if (!delta) return
  const previousRemaining = pickerRequestRemaining.value - delta
  if (Math.abs(Number(pickerAmount.value) - previousRemaining) < 0.01)
    pickerAmount.value = Math.max(0, Number(pickerAmount.value) + delta)
})


// ── PO detail dialog ──────────────────────────────────────────────────────────
const showDetail = ref(false)
const selectedPo = ref<POItem | null>(null)
const dialogMode = ref<'accept' | 'withdraw'>('accept')
const detailPanels = ref<string[]>(['payment'])

const supplierInvoiceFiles = ref<FileInfo[]>([])
const poPdfFiles = ref<FileInfo[]>([])
const bankInfoFiles = ref<FileInfo[]>([])
const dpFiles = ref<FileInfo[]>([])
const importDetail = ref<any>(null)
const isStockPo = computed(() => selectedPo.value?.origin === 'Stock')
const hasBankDetail = computed(() => {
  const d = importDetail.value
  return !!d && !!(d.beneficiary || d.bankName || d.bankAccountNumber || d.swiftCode || d.aba)
})

type CustomerPayment = { id: number; fileName: string; amount: number; notes?: string | null; createdAt: string }
const customerPayments = ref<CustomerPayment[]>([])
const customerTotalPaid = ref(0)
const customerInvoiceTotal = ref<number | null>(null)
const customerIsPaid = ref(false)

const rejecting = ref(false)
const showRejectDialog = ref(false)
const rejectionNote = ref('')

// ── Warehouse app (procument-warehouse): payments synced to the phone and paid there ──
const warehouseSync = useWarehouseSync()
const whSyncEnabled = ref(false)
const whSyncing = ref(false)
const whSyncText = ref('')
const whSyncResult = ref<WarehousePaymentSyncSummary | null>(null)
const acceptingWarehouse = ref(false)
const rejectingWarehouse = ref(false)
const showWarehouseRejectDialog = ref(false)
const warehouseRejectNote = ref('')

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
  creditRefreshKey.value++
  editingDetails.value = false
  detailPanels.value = ['payment']
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
    pickerWireFee.value = Number(paymentDraft.value.wireFee ?? selectedRequests.value[0]?.wireFee ?? 0)
    handlingFee.value = 0
    bankWireFee.value = 0
  } else {
    pickerRequestId.value = openRequests.value[0]?.id ?? null
    pickerWalletId.value = null
    pickerExchangeRate.value = null
    pickerWireFee.value = Number(selectedRequests.value[0]?.wireFee ?? 0)
    pickerAmount.value = pickerRequest.value?.remainingAmount ?? 0
    handlingFee.value = 0
    bankWireFee.value = 0
  }
  dialogMode.value = mode
  showDetail.value = true
  supplierInvoiceFiles.value = []
  poPdfFiles.value = []
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

  // Bank details are entered on the PO page for every PO, with or without a Sales Order.
  api.get(`/purchase-orders/${po.id}/import-detail`).then(d => { importDetail.value = d }).catch(() => {})

  if (selectedPo.value?.origin === 'Stock') {
    // Stock POs have no Sales Order: their files are keyed by the PO itself.
    try {
      const [docs, generated] = await Promise.all([
        api.get<any[]>(`/purchase-orders/${po.id}/documents`),
        api.get<FileInfo[]>(`/purchase-orders/${po.id}/documents/generated`),
      ])
      supplierInvoiceFiles.value = docs
        .filter(d => d.category === 'SupplierInvoice' || d.category === 'SupplierPI')
        .map(d => ({
          name: d.fileName,
          modifiedAt: d.createdAt,
          category: d.category,
          docId: d.id,
          label: [d.category === 'SupplierPI' ? 'Supplier PI' : 'Supplier Invoice', d.documentNumber,
            d.amount != null ? `$${formatPrice(d.amount)}` : null].filter(Boolean).join(' · '),
        }))
      poPdfFiles.value = generated.filter(f => f.category === 'po')
      dpFiles.value = generated.filter(f => f.category === 'dp')
    } catch { showSnack('Failed to load Stock PO documents', 'error') }
  } else if (po.invoiceId && po.supplierId) {
    try {
      const data = await api.get<any>(`/documents/proforma-invoice/${po.invoiceId}`)
      const supplierSection = (data.suppliers || []).find((s: any) => s.supplierId === po.supplierId)
      if (supplierSection) {
        const files: FileInfo[] = supplierSection.files || []
        supplierInvoiceFiles.value = files.filter((f: FileInfo) => f.category === 'supplier_invoice')
        bankInfoFiles.value = files.filter((f: FileInfo) => f.category === 'supplier_bank_info')
        dpFiles.value = files.filter((f: FileInfo) => f.category === 'dp')
        // The supplier's PO folder can hold other POs of this Sales Order: keep this PO's ("PO-12.pdf", "PO-12 (1).pdf").
        const poNumber = selectedPo.value?.poNumber
        poPdfFiles.value = files.filter((f: FileInfo) => f.category === 'po'
          && !!poNumber && (f.name === `${poNumber}.pdf` || f.name.startsWith(`${poNumber} (`)))
      }
    } catch {}
  }
}

function openPopUploadDialog() {
  if (!paymentDraft.value) {
    showSnack('Save the payment details before uploading the POP', 'warning')
    return
  }
  showWalletPickerDialog.value = true
}

/** In the Payment Request tab this also accepts the request (one transaction on the server). */
async function savePaymentDetails() {
  if (!selectedPo.value || !paymentDetailsValid.value || savingPaymentDetails.value) return
  const accept = dialogMode.value === 'accept'
  savingPaymentDetails.value = true
  try {
    await api.post(`/supplier-payments/po/${selectedPo.value.id}/draft`, {
      paymentRequestId: pickerRequestId.value,
      walletId: pickerWalletId.value,
      amount: pickerAmount.value,
      exchangeRate: pickerWallet.value?.currency === 'USD' ? 1 : pickerExchangeRate.value,
      wireFee: pickerWireFee.value,
      accept,
    })
    if (accept) {
      showSnack('Accepted — moved to Ready To pay. Upload the POP there or sync it to the warehouse.', 'success')
      showDetail.value = false
      await loadAll()
      activeTab.value = 'remaining'
      return
    }
    // Re-read so the summary has the wallet name and every saved field.
    paymentDraft.value = await api.get<any>(`/supplier-payments/po/${selectedPo.value.id}/draft`)
    editingDetails.value = false
    showSnack('Payment details saved. You can upload the POP now or later.', 'success')
    await loadPaymentRequests()
    creditRefreshKey.value++
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not save payment details', 'error')
  } finally { savingPaymentDetails.value = false }
}

/** Back to the saved values, as they were when the dialog opened. */
function cancelEditDetails() {
  editingDetails.value = false
  if (!paymentDraft.value) return
  pickerRequestId.value = paymentDraft.value.paymentRequestId
  pickerWalletId.value = paymentDraft.value.walletId
  pickerExchangeRate.value = paymentDraft.value.exchangeRate
  pickerAmount.value = Number(paymentDraft.value.amount)
  pickerWireFee.value = Number(paymentDraft.value.wireFee ?? 0)
}

async function runWarehouseSync() {
  if (whSyncing.value) return
  whSyncing.value = true
  whSyncResult.value = null
  try {
    whSyncResult.value = await warehouseSync.syncPayments(text => { whSyncText.value = text })
    await loadAll()
  } catch (e: any) {
    showSnack(e?.data?.message || e?.message || 'Warehouse sync failed', 'error')
  } finally { whSyncing.value = false }
}

async function previewWarehousePop() {
  if (!selectedPo.value || !paymentDraft.value?.warehouse) return
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/supplier-payments/po/${selectedPo.value.id}/warehouse-pop`, {
      responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob, paymentDraft.value.warehouse.fileName)
  } catch { showSnack('Failed to open the warehouse POP', 'error') }
}

async function acceptWarehousePayment() {
  if (!selectedPo.value || acceptingWarehouse.value) return
  acceptingWarehouse.value = true
  try {
    const result = await api.post<any>(`/supplier-payments/po/${selectedPo.value.id}/warehouse-accept`, {})
    showSnack(result.bankFee > 0
      ? `Warehouse payment accepted. ${formatPrice(result.bankFee)} ${result.walletCurrency} recorded as Bank Fee and Others.`
      : 'Warehouse payment accepted.', 'success')
    showDetail.value = false
    await loadAll()
    activeTab.value = result.complete ? 'finished' : 'remaining'
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not accept the warehouse payment', 'error')
  } finally { acceptingWarehouse.value = false }
}

async function rejectWarehousePayment() {
  if (!selectedPo.value || !warehouseRejectNote.value.trim()) return
  rejectingWarehouse.value = true
  try {
    await api.post(`/supplier-payments/po/${selectedPo.value.id}/warehouse-reject`, { note: warehouseRejectNote.value.trim() })
    showSnack('Sent back. The warehouse sees your note after the next sync.', 'warning')
    showWarehouseRejectDialog.value = false
    warehouseRejectNote.value = ''
    await loadPaymentRequests()
    await openPo(selectedPo.value, 'withdraw')
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not send the payment back', 'error')
  } finally { rejectingWarehouse.value = false }
}

function historyChipColor(category: string) {
  switch (category) {
    case 'Bank Fee and Others': return 'orange'
    case 'Supplier Credit': return 'success'
    case 'Supplier Credit Reversal': return 'grey'
    default: return 'primary'
  }
}

/** Supplier credit was used on this PO: refresh the numbers, the history and the wallet form. */
async function onCreditApplied(result: ApplySupplierCreditResult) {
  if (!selectedPo.value) return
  const poId = selectedPo.value.id
  // "Submitted" = fully paid and closed. A fully covered PO whose POP still waits for its final
  // bank amount stays open: that step finishes it.
  if (result.complete && result.poPaymentStatus === 'Submitted') {
    showSnack('Paid in full with supplier credit.', 'success')
    showDetail.value = false
    await loadAll()
    activeTab.value = 'finished'
    return
  }
  showSnack(result.complete
    ? 'Credit applied — the PO is covered. Save the final bank amount of the pending POP to finish.'
    : `Credit applied — $${formatPrice(result.remainingAmount)} left to pay on this PO.`, 'success')
  await Promise.all([loadQueue(), loadPaymentRequests()])
  selectedPo.value = queue.value.find(p => p.id === poId) || selectedPo.value
  try {
    paymentHistory.value = await api.get<any[]>(`/supplier-payments/po/${poId}`)
    paymentDraft.value = await api.get<any>(`/supplier-payments/po/${poId}/draft`)
  } catch { /* the lists refresh on the next open */ }
  // No saved wallet draft: offer exactly what is left, moving on if credit cleared the selected PR.
  if (!paymentDraft.value) {
    if (!pickerRequest.value) pickerRequestId.value = openRequests.value[0]?.id ?? null
    pickerAmount.value = pickerRequest.value?.remainingAmount ?? 0
  }
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
      handlingFee: fee(handlingFee.value),
      bankWireFee: fee(bankWireFee.value),
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

async function downloadSupplierFile(f: FileInfo) {
  const po = selectedPo.value
  if (!po) return
  const headers = { Authorization: `Bearer ${authStore.user?.token}` }
  try {
    const blob = f.docId != null
      ? await $fetch<Blob>(`${api.baseURL}/purchase-orders/${po.id}/documents/${f.docId}/download`, { responseType: 'blob', headers })
      : po.origin === 'Stock'
        ? await $fetch<Blob>(`${api.baseURL}/purchase-orders/${po.id}/documents/generated/${f.category}/download`,
            { query: { fileName: f.name }, responseType: 'blob', headers })
        : await $fetch<Blob>(`${api.baseURL}/documents/proforma-invoice/${po.invoiceId}/supplier/${po.supplierId}/file`,
            { query: { name: f.name, category: f.category }, responseType: 'blob', headers })
    docPreview.previewBlob(blob as Blob, f.name)
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

onMounted(() => {
  loadAll()
  warehouseSync.getPaymentConfig().then(c => { whSyncEnabled.value = c.enabled }).catch(() => {})
})
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
