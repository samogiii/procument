<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/quotes" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Quote {{ quote.quoteNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <div class="d-flex flex-wrap align-center gap-1 gap-sm-2">
        <!-- Status Chip with Dropdown (admin only) -->
        <v-menu v-if="isAdmin" :disabled="isLocked">
          <template #activator="{ props: menuProps }">
            <v-chip
              :color="statusColor(quote.status)"
              v-bind="menuProps"
              class="cursor-pointer"
              :append-icon="isLocked ? 'mdi-lock' : 'mdi-chevron-down'"
              size="default"
            >
              {{ quote.status || '—' }}
            </v-chip>
          </template>
          <v-list density="compact" style="min-width: 180px">
            <v-list-subheader>Change Status</v-list-subheader>
            <v-list-item
              v-for="s in statuses"
              :key="s.value"
              :value="s.value"
              :active="quote.status === s.value"
              @click="onStatusSelect(s.value)"
            >
              <template #prepend>
                <v-icon :icon="s.icon" :color="s.color" size="18" />
              </template>
              <v-list-item-title>{{ s.label }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>
        <v-chip v-else :color="statusColor(quote.status)" size="default" :append-icon="isLocked ? 'mdi-lock' : undefined">{{ quote.status || '—' }}</v-chip>

        <v-btn v-if="quote.status !== 'Sent' && quote.status !== 'Accepted' && quote.status !== 'Rejected'" prepend-icon="mdi-pencil" variant="tonal" color="warning" size="small" @click="editQuote">Edit</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-shield-account" variant="tonal" size="small" @click="showPermissions = true">Perms</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-history" variant="tonal" size="small" @click="showAudit = true">Audit</v-btn>
        <v-btn v-if="isAdmin && quote.status === 'Accepted' || quote.status === 'Sent'" prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-file-excel" size="small" color="success" @click="exportToExcel">Excel Export</v-btn>
      </div>
    </div>

    <!-- Stat Cards -->
    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-outline" color="primary" label="Customer">
          <template v-if="isAdmin"><span v-if="quote.customerCode" class="text-medium-emphasis ml-1">{{ quote.customerCode }}</span></template>
          <template v-else>{{ quote.customerCode || '—' }}</template>
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ formatPrice(quote.totalAmount) }}
        </StatCard>
      </v-col>
      <!-- <v-col cols="12" md="3">
        <StatCard icon="mdi-cash-check" color="info" label="Final Price">
          <span v-if="quote.finalPrice != null">${{ formatPrice(quote.finalPrice) }}</span>
          <span v-else class="text-medium-emphasis">—</span>
        </StatCard>
      </v-col> -->
      <v-col cols="12" md="3">
        <v-card class="info-card pa-4 h-100">
          <div class="d-flex align-center gap-3">
            <v-avatar :color="exTypeOptions.find(e => e.value === quote.rfqExType)?.color || 'grey'" variant="tonal" size="40">
              <v-icon :icon="exTypeOptions.find(e => e.value === quote.rfqExType)?.icon || 'mdi-tag-outline'" size="20" />
            </v-avatar>
            <div>
              <p class="text-caption text-medium-emphasis mb-0">Ship To</p>
              <v-menu v-if="isAdmin">
                <template #activator="{ props: menuProps }">
                  <v-chip
                    :color="exTypeOptions.find(e => e.value === quote.rfqExType)?.color || 'grey'"
                    v-bind="menuProps"
                    class="cursor-pointer mt-1"
                    append-icon="mdi-chevron-down"
                    size="small"
                  >
                    {{ exTypeOptions.find(e => e.value === quote.rfqExType)?.label || 'Not Set' }}
                  </v-chip>
                </template>
                <v-list density="compact" style="min-width: 180px">
                  <v-list-subheader>Change Ship To</v-list-subheader>
                  <v-list-item
                    v-for="opt in exTypeMenuOptions"
                    :key="opt.value"
                    :active="quote.rfqExType === opt.value || (opt.value === 1 && quote.rfqExType === 2)"
                    @click="updateExType(opt.value)"
                  >
                    <template #prepend>
                      <v-icon :icon="opt.icon" :color="opt.color" size="18" />
                    </template>
                    <v-list-item-title>{{ opt.label }}</v-list-item-title>
                  </v-list-item>
                </v-list>
              </v-menu>
              <v-chip
                v-else
                :color="exTypeOptions.find(e => e.value === quote.rfqExType)?.color || 'grey'"
                class="mt-1"
                size="small"
              >
                {{ exTypeOptions.find(e => e.value === quote.rfqExType)?.label || 'Not Set' }}
              </v-chip>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-send-clock" color="info" label="Sent At"
          :value="quote.sentAt ? new Date(quote.sentAt).toLocaleString() : undefined"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-file-document-outline" color="info" label="RFQ">
          <nuxt-link v-if="quote.rfqId" :to="`/rfqs/${quote.rfqId}`" class="text-primary text-decoration-none">
            {{ quote.rfqName || `RFQ #${quote.rfqId}` }}
          </nuxt-link>
          <span v-else>—</span>
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-clock-outline" color="secondary" label="Created At"
          :value="quote.createdAt ? new Date(quote.createdAt).toLocaleString() : undefined"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-group-outline" color="accent" label="Assigned RFQ Users">
          <span v-if="quote.assignedUsers && quote.assignedUsers.length > 0">
            {{ quote.assignedUsers.map((u: any) => u.name).join(', ') }}
          </span>
          <span v-else class="text-medium-emphasis">—</span>
        </StatCard>
      </v-col>
      <v-col cols="12" md="3" v-if="quote.customerTermsAndConditions || isAdmin">
        <StatCard label="Customer Terms" color="accent" icon="mdi-file-document-outline" class="info-card pa-4 h-100">
          <div class="d-flex align-center gap-3">
            
            <div class="flex-grow-1">
              <p class="text-body-2 font-weight-medium mb-0" style="white-space: pre-wrap;">{{ quote.customerTermsAndConditions || '—' }}</p>
            </div>
          </div>
        </StatCard>
      </v-col>
    </v-row>

    <!-- Rejection Note (current quote) -->
    <v-alert
      v-if="quote.status === 'Rejected' && quote.rejectionNote"
      type="error"
      variant="tonal"
      class="mb-4"
      icon="mdi-close-circle-outline"
    >
      <div class="font-weight-bold mb-1">Rejection Reason</div>
      {{ quote.rejectionNote }}
    </v-alert>

    <!-- Rejection History: show rejected quotes when current quote is active -->
    <v-card
      v-if="rejectedSiblings.length"
      class="glass-card mb-5 border-s-4"
      style="border-color: #ef4444 !important;"
    >
      <v-card-title
        class="d-flex align-center gap-2 cursor-pointer py-3 px-4"
        @click="showRejectionHistory = !showRejectionHistory"
      >
        <v-icon icon="mdi-history" color="error" size="20" />
        <span class="text-body-1 font-weight-medium">Rejection History</span>
        <v-chip size="x-small" color="error" variant="tonal">{{ rejectedSiblings.length }} rejected quote{{ rejectedSiblings.length > 1 ? 's' : '' }}</v-chip>
        <v-spacer />
        <v-icon :icon="showRejectionHistory ? 'mdi-chevron-up' : 'mdi-chevron-down'" size="20" />
      </v-card-title>
      <v-expand-transition>
        <div v-if="showRejectionHistory">
          <v-divider />
          <v-card-text class="pa-0">
            <v-table density="compact">
              <thead>
                <tr>
                  <th>Quote #</th>
                  <th>Created</th>
                  <th>Total</th>
                  <th>Items</th>
                  <th>Rejection Reason</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="rq in rejectedSiblings" :key="rq.id">
                  <td>
                    <v-chip size="x-small" color="error" variant="tonal">{{ rq.quoteNumber }}</v-chip>
                  </td>
                  <td class="text-caption text-medium-emphasis">
                    {{ rq.createdAt ? new Date(rq.createdAt).toLocaleDateString() : '—' }}
                  </td>
                  <td class="text-caption">${{ formatPrice(rq.totalAmount) }}</td>
                  <td class="text-caption">{{ rq.items?.length || 0 }} parts</td>
                  <td class="text-caption text-error" style="max-width: 260px; white-space: normal;">
                    {{ rq.rejectionNote || '—' }}
                  </td>
                  <td>
                    <v-btn :to="`/quotes/${rq.id}`" size="x-small" variant="text" icon="mdi-open-in-new" />
                  </td>
                </tr>
              </tbody>
            </v-table>
          </v-card-text>
        </div>
      </v-expand-transition>
    </v-card>

    <!-- Yuan Settings (base-3 customers only) -->
    <v-card v-if="quote.customerBase === 3 && isAdmin" class="glass-card">
      <v-card-title class="d-flex align-center gap-2">
        <v-icon icon="mdi-currency-cny" color="warning" size="20" class="mr-1" />
        Yuan Pricing Settings
      </v-card-title>
      <v-card-text>
        <v-row dense align="center">
          <v-col cols="12" sm="4" md="3">
            <v-text-field
              v-model.number="yuanCoef"
              label="Tax Coefficient (Coef)"
              variant="outlined"
              density="compact"
              hide-details
              type="number"
              step="0.01"
              placeholder="e.g. 1.25"
            />
          </v-col>
          <v-col cols="12" sm="4" md="3">
            <v-text-field
              v-model.number="yuanExchangeRate"
              label="Exchange Rate (CNY/USD)"
              variant="outlined"
              density="compact"
              hide-details
              type="number"
              step="0.01"
              placeholder="e.g. 7.0"
            />
          </v-col>
          <v-col cols="auto">
            <v-chip v-if="yuanCoef && yuanExchangeRate" color="warning" variant="tonal" size="small" class="mr-2">
              Effective rate: ¥{{ ((yuanCoef || 1) * (yuanExchangeRate || 1)).toFixed(4) }}
            </v-chip>
            <v-btn color="warning" size="small" :loading="savingYuan" @click="saveYuanSettings">Save</v-btn>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <!-- Linked Invoices (Sales Orders) -->
    <v-card v-if="linkedInvoices.length" class="glass-card mb-6">
      <v-card-title class="d-flex align-center gap-2 pa-4 pb-2">
        <v-icon icon="mdi-receipt-text-outline" color="success" size="20" />
        <span class="text-body-1 font-weight-bold">Sales Orders</span>
        <v-chip size="x-small" color="success" variant="tonal">{{ linkedInvoices.length }}</v-chip>
      </v-card-title>
      <v-card-text class="pa-2">
        <div class="d-flex flex-wrap gap-2">
          <NuxtLink
            v-for="inv in linkedInvoices"
            :key="inv.id"
            :to="`/invoices/${inv.id}`"
            class="text-decoration-none"
          >
            <v-card variant="outlined" class="pa-3 d-flex align-center gap-3" style="min-width: 220px; cursor: pointer;">
              <v-icon icon="mdi-receipt-text-outline" color="success" size="18" />
              <div>
                <div class="text-body-2 font-weight-bold">{{ inv.invoiceNumber || `Invoice #${inv.id}` }}</div>
                <div class="text-caption text-medium-emphasis">${{ (inv.totalAmount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}</div>
              </div>
              <v-spacer />
              <v-chip :color="statusColor(inv.status)" size="x-small" variant="flat" class="ml-2">{{ inv.status }}</v-chip>
            </v-card>
          </NuxtLink>
        </div>
      </v-card-text>
    </v-card>

    <!-- All RFQ Items + Procurement Records -->
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center gap-2">
        RFQ Items
        <v-chip size="x-small" color="success" variant="tonal" class="ml-1">
          {{ selectedProcIds.size }} selected
        </v-chip>
      </v-card-title>
      <v-card-text class="pa-0">
        <div class="detail-table-wrap">
          <table class="detail-master-grid">
            <thead>
              <tr>
                <th style="width: 50px;">#</th>
                <th style="min-width: 140px;">Part Number</th>
                <th>Description</th>
                <th style="width: 70px;">Qty</th>
                <th style="width: 90px;">Condition</th>
                <th style="width: 100px;">Suppliers</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="(item, idx) in sortedRfqItems" :key="item.id">
                <!-- Master Row -->
                 <tr class="master-row" :class="{ 'master-row-inactive': !itemHasSelection(item.id) }">
                  <!--<td class="cell-number">
                    <div class="d-flex align-center gap-1">
                      <span>{{ idx + 1 }}</span>
                      <div v-if="canReorder && itemHasSelection(item.id)" class="d-flex flex-column">
                        <v-btn
                          icon
                          size="x-small"
                          variant="text"
                          density="compact"
                          :disabled="getQuoteItemIndex(item.id) <= 0"
                          @click="moveItem(item.id, -1)"
                        >
                          <v-icon size="14">mdi-chevron-up</v-icon>
                        </v-btn>
                        <v-btn
                          icon
                          size="x-small"
                          variant="text"
                          density="compact"
                          :disabled="getQuoteItemIndex(item.id) < 0 || getQuoteItemIndex(item.id) >= getQuoteItemCount() - 1"
                          @click="moveItem(item.id, 1)"
                        >
                          <v-icon size="14">mdi-chevron-down</v-icon>
                        </v-btn>
                      </div>
                    </div>
                  </td> -->
                  <td><span>{{ idx + 1 }}</span></td>
                  <td class="cell-pn" :class="{ 'cell-pn-inactive': !itemHasSelection(item.id) }">{{ item.partNumberName }}</td>
                  <td class="text-medium-emphasis" style="padding-left: 12px; font-size: 13px;">{{ item.description || '—' }}</td>
                  <td class="text-center" style="font-size: 13px;">{{ item.qty }}</td>
                  <td style="padding-left: 12px; font-size: 13px;">{{ item.condition || 'N/A' }}</td>
                  <td class="cell-status">
                    <span :class="getProcRecords(item.id).length > 0 ? 'text-success' : 'text-medium-emphasis'">
                      {{ getProcRecords(item.id).length }} price{{ getProcRecords(item.id).length !== 1 ? 's' : '' }}
                    </span>
                  </td>
                </tr>

                <!-- Detail: All procurement records for this item -->
                <tr v-if="getProcRecords(item.id).length > 0" class="detail-sub-row">
                  <td :colspan="6" class="detail-sub-cell">
                    <div class="proc-panel">
                      <table class="proc-grid">
                        <thead>
                          <tr>
                            <th style="width: 28px;"></th>
                            <th style="width: 36px;"></th>
                            <th style="opacity:1; min-width: 90px; position: sticky; left: 0;  background: var(--toolbar-bg); z-index: 3; border-right: 1px solid var(--card-border);">Supplier</th>
                            <th style="width: 120px;">Alt P/N</th>
                            <th style="width: 80px;">Condition</th>
                            <th style="width: 60px;">Qty</th>
                            <th style="width: 80px;">Cert</th>
                            <th style="width: 90px;">Tag Date</th>
                            <th style="width: 110px;">Shipping Point</th>
                            <th style="width: 100px;">Shipping Cost</th>
                            <th style="width: 90px;">Lead Time</th>
                            <th style="width: 140px;">Note</th>
                            <th style="width: 100px;">Buy Price</th>
                            <th style="width: 110px;">Total Buy Price</th>
                            <th style="width: 110px;">Repair Cost</th>
                            <th style="width: 65px;">Coef 1</th>
                            <th style="width: 65px;">Coef 2</th>
                            <th style="width: 65px;">Coef 3</th>
                            <th style="width: 100px;">Unit Price</th>
                            <th style="width: 110px;">Total Price</th>
                            <th style="width: 140px; color: #a78bfa;">My Notes</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr
                            v-for="rec in getProcRecords(item.id)"
                            :key="rec.id"
                            class="proc-row"
                            :class="[
                              selectedProcIds.has(rec.id) ? 'selected-proc-row' : 'unselected-proc-row',
                              rec.isShop ? 'shop-sub-row' : '',
                              dragOverRecId === rec.id ? 'drag-over-row' : '',
                              dragState?.recId === rec.id ? 'dragging-row' : ''
                            ]"
                            :draggable="!rec.isShop"
                            @dragstart="!rec.isShop && onDragStart(item.id, rec.id, $event)"
                            @dragover="!rec.isShop && onDragOver(rec.id, $event)"
                            @dragleave="onDragLeave($event)"
                            @drop="!rec.isShop && onDrop(item.id, rec.id, $event)"
                            @dragend="onDragEnd"
                          >
                            <td class="text-center">
                              <v-icon
                                v-if="selectedProcIds.has(rec.id)"
                                icon="mdi-check-circle"
                                color="success"
                                size="16"
                              />
                              <v-icon
                                v-else
                                icon="mdi-circle-outline"
                                color="grey"
                                size="16"
                              />
                            </td>
                            <td class="text-center drag-handle-cell">
                              <v-icon
                                v-if="!rec.isShop"
                                icon="mdi-drag-vertical"
                                size="18"
                                class="drag-handle-icon"
                              />
                            </td>
                            <td style="padding-left: 8px; font-size: 13px; position: sticky; left: 0; background: var(--toolbar-bg); opacity: 1; z-index: 2; border-right: 1px solid var(--card-border);">
                              <span v-if="rec.isShop" style="color:#ff9800; margin-right:4px; font-size:11px;">↳ 🔧</span>{{ rec.supplierName }}
                            </td>
                            <td style="padding-left: 8px; font-size: 12px; color: #fbbf24;">{{ rec.alt || '—' }}</td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.condition || 'N/A' }}</td>
                            <td class="text-center" style="font-size: 13px;">{{ rec.qty }}</td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.certName || '—' }}</td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.tagDate ? new Date(rec.tagDate).toLocaleDateString() : '—' }}</td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.shippingPoint || '—' }}</td>
                            <td class="mono-cell text-right pr-2">{{ rec.shippingCost ? '$' + formatPrice(rec.shippingCost) : '—' }}</td>
                            <td style="padding-left: 8px; font-size: 12px;">{{ rec.leadTime || '—' }}</td>
                            <td style="padding-left: 8px; font-size: 12px; max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" :title="rec.note">{{ rec.note || '—' }}</td>
                            <td style="font-family: monospace; text-align: right; padding-right: 10px; font-size: 13px;" class="text-medium-emphasis">
                              ${{ formatPrice(rec.price) }}
                            </td>
                            <td class="mono-cell text-right pr-2">
                              ${{ formatPrice((Number(rec.price) || 0) * (Number(rec.qty) || 1)) }}
                            </td>
                            <td class="text-center mono-cell" :style="rec.fixPrice ? 'color:#ff9800; font-weight:600;' : ''">
                              {{ rec.fixPrice ? '$' + formatPrice(rec.fixPrice) : '—' }}
                            </td>
                            <td class="text-center mono-cell">{{ rec.coef_1 ?? '—' }}</td>
                            <td class="text-center mono-cell">{{ rec.coef_2 ?? '—' }}</td>
                            <td class="text-center mono-cell">{{ rec.coef_3 ?? '—' }}</td>
                            <td class="mono-cell text-right pr-2">
                              ${{ formatPrice(rec.unitPrice) }}
                            </td>
                            <td class="mono-cell text-right pr-2" :class="selectedProcIds.has(rec.id) ? 'total-selected' : ''">
                              ${{ formatPrice(rec.totalPrice) }}
                            </td>
                            <td style="padding-left: 8px; font-size: 12px; max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" :title="rec.myNotes">{{ rec.myNotes || '—' }}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                </tr>
                <tr v-else class="detail-sub-row">
                  <td :colspan="6" class="detail-sub-cell">
                    <div class="proc-panel empty-proc">
                      <span class="text-caption text-medium-emphasis">No procurement records for this item.</span>
                    </div>
                  </td>
                </tr>
              </template>

              <tr v-if="!sortedRfqItems.length && !loading">
                <td :colspan="6" class="text-center pa-8">
                  <v-icon icon="mdi-file-document-outline" size="48" color="grey-darken-1" class="mb-3" />
                  <p class="text-body-2 text-medium-emphasis">No RFQ items found</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </v-card-text>
    </v-card>

    <!-- Dialogs -->
    <v-dialog v-model="showPermissions" max-width="600">
      <PermissionManager :entity-name="'Quote'" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-dialog v-model="showAudit" max-width="800">
      <BusinessAuditViewer entity-name="Quote" :entity-id="route.params.id as string" />
    </v-dialog>

    <QuotePdfGenerator v-model="showPdf" :quote="quote" />

    <!-- Rejection Note Dialog -->
    <v-dialog v-model="showRejectDialog" max-width="450" persistent>
      <v-card>
        <v-card-title class="text-h6">Reject Quote</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">Please provide a reason for rejecting this quote:</p>
          <v-textarea
            v-model="rejectionNote"
            label="Rejection Reason"
            variant="outlined"
            rows="3"
            auto-grow
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" @click="confirmReject">Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Under $1000 Warning Dialog -->
    <v-dialog v-model="showUnder1000Warning" max-width="480" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-alert-circle-outline" color="warning" class="mr-2" />
          Low Price Warning
        </v-card-title>
        <v-card-text class="pa-4">
          <div class="text-body-1 mb-3">The following items have a Total Price under <strong>$1,000</strong>:</div>
          <v-list density="compact" class="mb-3">
            <v-list-item
              v-for="item in under1000Items"
              :key="item.id ?? item.rfqItemId"
              :title="item.partNumberName || 'Unknown part'"
              :subtitle="'Total: $' + formatPrice(item.totalPrice)"
              prepend-icon="mdi-alert"
              color="warning"
            />
          </v-list>
          <div class="text-body-2 text-medium-emphasis">Are you sure you want to Accept this quote?</div>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="cancelUnder1000">No, Cancel</v-btn>
          <v-btn color="warning" variant="flat" @click="confirmUnder1000Accept">Yes, Accept Anyway</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Snackbar -->
    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
import * as XLSX from 'xlsx'

const route = useRoute()
const router = useRouter()
const api = useApi()
const authStore = useAuthStore()
const { statusColor } = useStatusColor()

const quote = ref<any>({})
const linkedInvoices = ref<any[]>([])
const apiPresets = ref<any[]>([])
const allRfqItems = ref<any[]>([])

async function loadPresets() {
  try {
    apiPresets.value = await api.get('/companypresets')
  } catch {
    apiPresets.value = []
  }
}

const allProcRecords = ref<any[]>([])
const selectedProcIds = ref(new Set<number>())
const loading = ref(true)
const showAllItems = ref(false)

// Rejection history — other rejected quotes for the same RFQ
const rejectedSiblings = ref<any[]>([])
const showRejectionHistory = ref(false)

const showPermissions = ref(false)
const showAudit = ref(false)
const showPdf = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const isAdmin = computed(() => authStore.isAdmin)

// value 2 kept for legacy display; both 1 and 2 show as Vendor/Customer
const exTypeOptions = [
  { value: 0, label: 'Warehouse', icon: 'mdi-warehouse', color: 'success' },
  { value: 1, label: 'Vendor/Customer', icon: 'mdi-truck-delivery-outline', color: 'info' },
  { value: 2, label: 'Vendor/Customer', icon: 'mdi-truck-delivery-outline', color: 'info' },
]
const exTypeMenuOptions = [
  { value: 0, label: 'Warehouse', icon: 'mdi-warehouse', color: 'success' },
  { value: 1, label: 'Vendor/Customer', icon: 'mdi-truck-delivery-outline', color: 'info' },
]

async function updateExType(newExType: number) {
  try {
    await api.patch(`/quotes/${route.params.id}/rfq-ex-type`, newExType)
    quote.value.rfqExType = newExType
    showSnack('Ship To updated', 'success')
  } catch {
    showSnack('Failed to update Ship To', 'error')
  }
}

const displayedItems = computed(() => {
  if (!showAllItems.value) return quote.value.items || []
  // Merge quote items with all RFQ items, marking unselected ones
  const quoteItemIds = new Set((quote.value.items || []).map((i: any) => i.rfqItemId))
  const allItems = [...(quote.value.items || [])]
  allRfqItems.value.forEach((rfqItem: any) => {
    if (!quoteItemIds.has(rfqItem.id)) {
      allItems.push({
        rfqItemId: rfqItem.id,
        rfqRef: rfqItem.rfqRef,
        partNumberName: rfqItem.partNumberName,
        alt: rfqItem.alt,
        condition: rfqItem.condition,
        qty: rfqItem.qty,
        supplierName: null,
        buyPrice: null,
        unitPrice: null,
        totalPrice: null,
        shippingCost: null,
        isUnselected: true,
      })
    }
  })
  return allItems.sort((a: any, b: any) => {
    const aRef = typeof a.rfqRef === 'number' ? a.rfqRef : Infinity
    const bRef = typeof b.rfqRef === 'number' ? b.rfqRef : Infinity
    return aRef - bRef
  })
})

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('quote', entityId)

const statuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Sent', label: 'Sent', icon: 'mdi-send', color: 'info' },
  { value: 'Accepted', label: 'Accepted', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Rejected', label: 'Rejected', icon: 'mdi-close-circle', color: 'error' },
]

onMounted(async () => {
  await loadQuote()
  await loadPresets()
  await checkLock()
})

async function loadQuote() {
  loading.value = true
  try {
    const q = await api.get<any>(`/quotes/${route.params.id}`)

    if (q.rfqId) {
      try {
        const [rfq, procRecords, allRfqQuotes] = await Promise.all([
          api.get<any>(`/rfqs/${q.rfqId}`),
          api.get<any[]>(`/rfqs/${q.rfqId}/supplier-quotes`),
          api.get<any[]>(`/quotes/by-rfq/${q.rfqId}`).catch(() => []),
        ])
        q.rfqName = rfq.name || `RFQ #${q.rfqId}`

        // Collect rejected sibling quotes (not this quote)
        rejectedSiblings.value = (allRfqQuotes || [])
          .filter((sq: any) => sq.status === 'Rejected' && sq.id !== q.id)

        // Build RFQ items list with enrichment
        allRfqItems.value = (rfq.items || []).map((item: any) => ({
          id: item.id,
          partNumberName: item.partNumberName,
          description: item.description || '',
          qty: item.qty,
          condition: item.condition || '',
        }))

        // Flatten parent records + nested shop records so shop rows appear
        // in the table individually (with their own FixPrice / selection highlight)
        const flatRecords: any[] = []
        for (const r of (procRecords || [])) {
          flatRecords.push(r)
          for (const shop of (r.shopRecords || [])) {
            flatRecords.push({ ...shop, isShop: true })
          }
        }
        allProcRecords.value = flatRecords

        // Build the set of selected procurement record IDs from quote items
        const ids = new Set<number>()
        ;(q.items || []).forEach((qi: any) => {
          if (qi.procumentRecordId) ids.add(qi.procumentRecordId)
        })
        selectedProcIds.value = ids

      } catch {
        // RFQ fetch failed, continue without enrichment
      }
    }

    quote.value = q
    // Init Yuan settings from loaded data
    yuanCoef.value = q.coefYuan ?? null
    yuanExchangeRate.value = q.exchangeRateYuan ?? null

    // Load linked invoices (Sales Orders) for this quote
    try {
      linkedInvoices.value = await api.get<any[]>(`/invoices/by-quote/${q.id}`)
    } catch {
      linkedInvoices.value = []
    }
  } catch {
    showSnack('Failed to load quote', 'error')
  } finally {
    loading.value = false
  }
}

function getProcRecords(rfqItemId: number) {
  return allProcRecords.value.filter(r => r.rfqItemId === rfqItemId)
}

function itemHasSelection(rfqItemId: number) {
  return getProcRecords(rfqItemId).some(r => selectedProcIds.value.has(r.id))
}

// ── Item sort ordering ──
// Items belonging to quote are ordered by their quote item SortOrder.
// Items not in the quote come last (preserve RFQ order).
const sortedRfqItems = computed(() => {
  const quoteItems: any[] = quote.value?.items || []
  const orderMap = new Map<number, number>() // rfqItemId -> sortOrder
  quoteItems.forEach((qi: any, idx: number) => {
    if (qi.rfqItemId != null) {
      orderMap.set(qi.rfqItemId, qi.sortOrder ?? idx)
    }
  })
  const arr = [...allRfqItems.value]
  arr.sort((a: any, b: any) => {
    const aIn = orderMap.has(a.id)
    const bIn = orderMap.has(b.id)
    if (aIn && bIn) return (orderMap.get(a.id)! - orderMap.get(b.id)!)
    if (aIn) return -1
    if (bIn) return 1
    return a.id - b.id
  })
  return arr
})

const canReorder = computed(() => {
  // Only allow reordering of items that are in this quote
  return (quote.value?.items?.length ?? 0) > 1
})

async function moveItem(rfqItemId: number, direction: -1 | 1) {
  const quoteItems: any[] = [...(quote.value?.items || [])]
  // Sort by current sortOrder first
  quoteItems.sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
  const idx = quoteItems.findIndex((q: any) => q.rfqItemId === rfqItemId)
  if (idx < 0) return
  const swapIdx = idx + direction
  if (swapIdx < 0 || swapIdx >= quoteItems.length) return
  ;[quoteItems[idx], quoteItems[swapIdx]] = [quoteItems[swapIdx], quoteItems[idx]]
  // Re-assign sortOrder values sequentially
  quoteItems.forEach((qi: any, i: number) => { qi.sortOrder = i })
  quote.value.items = quoteItems

  try {
    await api.patch(`/quotes/${route.params.id}/items-order`, {
      items: quoteItems.map((qi: any) => ({ id: qi.id, sortOrder: qi.sortOrder })),
    })
  } catch {
    showSnack('Failed to save order', 'error')
    await loadQuote()
  }
}

function getQuoteItemIndex(rfqItemId: number) {
  const quoteItems: any[] = quote.value?.items || []
  const sorted = [...quoteItems].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
  return sorted.findIndex((q: any) => q.rfqItemId === rfqItemId)
}

function getQuoteItemCount() {
  return quote.value?.items?.length || 0
}

// ── Procurement record (supplier) sort ordering ──
function getParentProcs(rfqItemId: number) {
  return getProcRecords(rfqItemId).filter((r: any) => !r.isShop)
}

function isFirstProc(rfqItemId: number, recId: number) {
  const arr = getParentProcs(rfqItemId)
  return arr.length === 0 || arr[0].id === recId
}

function isLastProc(rfqItemId: number, recId: number) {
  const arr = getParentProcs(rfqItemId)
  return arr.length === 0 || arr[arr.length - 1].id === recId
}

async function moveProcRecord(rfqItemId: number, recId: number, direction: -1 | 1) {
  const parents = getParentProcs(rfqItemId)
  const idx = parents.findIndex((r: any) => r.id === recId)
  if (idx < 0) return
  const swapIdx = idx + direction
  if (swapIdx < 0 || swapIdx >= parents.length) return

  // Reorder in local state (swap + reassign sortOrder)
  ;[parents[idx], parents[swapIdx]] = [parents[swapIdx], parents[idx]]
  parents.forEach((r: any, i: number) => { r.sortOrder = i })

  // Rebuild allProcRecords: keep other rfq items intact, replace this rfqItem's ordering while grouping shop children under their parent
  const others = allProcRecords.value.filter((r: any) => r.rfqItemId !== rfqItemId)
  const shopByParent = new Map<number, any[]>()
  for (const r of allProcRecords.value) {
    if (r.rfqItemId === rfqItemId && r.isShop && r.parentProcumentId != null) {
      if (!shopByParent.has(r.parentProcumentId)) shopByParent.set(r.parentProcumentId, [])
      shopByParent.get(r.parentProcumentId)!.push(r)
    }
  }
  const reinserted: any[] = []
  for (const p of parents) {
    reinserted.push(p)
    const shops = shopByParent.get(p.id) || []
    reinserted.push(...shops)
  }
  allProcRecords.value = [...others, ...reinserted]

  try {
    await api.patch(`/rfqs/${quote.value.rfqId}/supplier-quotes/order`, {
      items: parents.map((r: any) => ({ id: r.id, sortOrder: r.sortOrder })),
    })
  } catch {
    showSnack('Failed to save supplier order', 'error')
    await loadQuote()
  }
}

const showRejectDialog = ref(false)
const rejectionNote = ref('')

// ── Yuan settings (base-3 customers) ──
const yuanCoef = ref<number | null>(null)
const yuanExchangeRate = ref<number | null>(null)
const savingYuan = ref(false)

async function saveYuanSettings() {
  savingYuan.value = true
  try {
    await api.patch(`/quotes/${route.params.id}/yuan-settings`, {
      coefYuan: yuanCoef.value,
      exchangeRateYuan: yuanExchangeRate.value,
    })
    quote.value.coefYuan = yuanCoef.value
    quote.value.exchangeRateYuan = yuanExchangeRate.value
    showSnack('Yuan settings saved', 'success')
  } catch {
    showSnack('Failed to save Yuan settings', 'error')
  } finally {
    savingYuan.value = false
  }
}

const dragState = ref<{ rfqItemId: number; recId: number } | null>(null)
const dragOverRecId = ref<number | null>(null)

function onDragStart(rfqItemId: number, recId: number, event: DragEvent) {
  dragState.value = { rfqItemId, recId }
  event.dataTransfer!.effectAllowed = 'move'
  event.dataTransfer!.setData('text/plain', String(recId))
}

function onDragOver(recId: number, event: DragEvent) {
  if (!dragState.value || dragState.value.recId === recId) return
  event.preventDefault()
  event.dataTransfer!.dropEffect = 'move'
  dragOverRecId.value = recId
}

function onDragLeave(event: DragEvent) {
  const related = event.relatedTarget as HTMLElement | null
  if (!related || !(related.closest('tr'))) dragOverRecId.value = null
}

function onDrop(rfqItemId: number, targetRecId: number, event: DragEvent) {
  event.preventDefault()
  dragOverRecId.value = null
  if (!dragState.value || dragState.value.recId === targetRecId) { dragState.value = null; return }
  reorderProcByDrop(rfqItemId, dragState.value.recId, targetRecId)
  dragState.value = null
}

function onDragEnd() {
  dragState.value = null
  dragOverRecId.value = null
}

async function reorderProcByDrop(rfqItemId: number, sourceRecId: number, targetRecId: number) {
  const parents = getParentProcs(rfqItemId)
  const srcIdx = parents.findIndex((r: any) => r.id === sourceRecId)
  const tgtIdx = parents.findIndex((r: any) => r.id === targetRecId)
  if (srcIdx < 0 || tgtIdx < 0) return

  const reordered = [...parents]
  const [moved] = reordered.splice(srcIdx, 1)
  reordered.splice(tgtIdx, 0, moved)
  reordered.forEach((r: any, i: number) => { r.sortOrder = i })

  const others = allProcRecords.value.filter((r: any) => r.rfqItemId !== rfqItemId)
  const shopByParent = new Map<number, any[]>()
  for (const r of allProcRecords.value) {
    if (r.rfqItemId === rfqItemId && r.isShop && r.parentProcumentId != null) {
      if (!shopByParent.has(r.parentProcumentId)) shopByParent.set(r.parentProcumentId, [])
      shopByParent.get(r.parentProcumentId)!.push(r)
    }
  }
  const reinserted: any[] = []
  for (const p of reordered) {
    reinserted.push(p)
    reinserted.push(...(shopByParent.get(p.id) || []))
  }
  allProcRecords.value = [...others, ...reinserted]

  try {
    await api.patch(`/rfqs/${quote.value.rfqId}/supplier-quotes/order`, {
      items: reordered.map((r: any) => ({ id: r.id, sortOrder: r.sortOrder })),
    })
  } catch {
    showSnack('Failed to save supplier order', 'error')
    await loadQuote()
  }
}

const showUnder1000Warning = ref(false)
const under1000Items = ref<any[]>([])
const pendingAcceptStatus = ref('')

function onStatusSelect(newStatus: string) {
  if (newStatus === quote.value.status) return
  if (newStatus === 'Rejected') {
    rejectionNote.value = ''
    showRejectDialog.value = true
    return
  }
  if (newStatus === 'Accepted') {
    const cheapItems = (quote.value.items || []).filter((item: any) =>
      item.totalPrice != null && Number(item.totalPrice) < 1000
    )
    if (cheapItems.length > 0) {
      under1000Items.value = cheapItems
      pendingAcceptStatus.value = newStatus
      showUnder1000Warning.value = true
      return
    }
  }
  changeStatus(newStatus)
}

async function confirmUnder1000Accept() {
  showUnder1000Warning.value = false
  await changeStatus(pendingAcceptStatus.value)
}

function cancelUnder1000() {
  showUnder1000Warning.value = false
}

async function confirmReject() {
  showRejectDialog.value = false
  await changeStatus('Rejected', rejectionNote.value || undefined)
}

async function changeStatus(newStatus: string, note?: string) {
  try {
    await api.patch(`/quotes/${route.params.id}/status`, { status: newStatus, rejectionNote: note || null })
    quote.value.status = newStatus
    quote.value.rejectionNote = note || null
    showSnack(`Status changed to ${newStatus}`, 'success')
  } catch {
    showSnack('Failed to change status', 'error')
  }
}

function editQuote() {
  if (quote.value.rfqId) {
    router.push(`/rfqs/${quote.value.rfqId}/create-quote?editQuoteId=${route.params.id}`)
  } else {
    showSnack('No RFQ linked to this quote', 'warning')
  }
}

async function exportToExcel() {
  try {
    const q = quote.value
    if (!q || !q.items) return

    // 1. Find Company Preset based on customerBase (matches sortOrder)
    const preset = apiPresets.value.find(p => p.sortOrder === q.customerBase) || apiPresets.value[0]
    const companyName = preset?.name || 'JETRUX'

    // 2. Sort items as they appear in the UI
    const sortedItems = [...(q.items || [])].sort((a: any, b: any) => {
      const aRef = typeof a.rfqReference === 'string' ? a.rfqReference : (typeof a.rfqItemId === 'number' ? a.rfqItemId.toString() : '999')
      const bRef = typeof b.rfqReference === 'string' ? b.rfqReference : (typeof b.rfqItemId === 'number' ? b.rfqItemId.toString() : '999')
      if (aRef !== bRef) return aRef.localeCompare(bRef, undefined, { numeric: true, sensitivity: 'base' })
      
      const aProcSo = typeof a.procumentRecordSortOrder === 'number' ? a.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
      const bProcSo = typeof b.procumentRecordSortOrder === 'number' ? b.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
      if (aProcSo !== bProcSo) return aProcSo - bProcSo
      return 0
    })

    // 3. Build rows — same layout as the PDF quote
    const safeName = (s: string) => s.replace(/[/\\?%*:|"<>]/g, '_')
    const data: any[][] = [
      [companyName],
      ['QUOTATION'],
      [],
      ['Quote Number:', q.quoteNumber || '—', '', 'Date:', q.createdAt ? new Date(q.createdAt).toLocaleDateString() : '—'],
      ['Customer:', q.customerName || '—', '', 'RFQ:', q.rfqName || '—'],
      [],
      ['#', 'Ref', 'Part Number', 'Alt Part Number',  'Qty', 'Cond', 'Lead Time', 'Unit Price ($)', 'Total Price ($)']
    ]

    sortedItems.forEach((it: any, idx: number) => {
      data.push([
        idx + 1,
        it.rfqReference || '—',
        it.partNumberName || '—',
        it.alt || '',
        
        it.qty,
        it.condition || '—',
        it.leadTime || '—',
        Number(it.unitPrice || 0),
        Number(it.totalPrice || 0)
      ])
    })

    data.push([])
    data.push(['', '', '', '', '', '', '', '', 'Subtotal:', Number(q.totalAmount || 0)])
    data.push(['', '', '', '', '', '', '', '', 'Grand Total:', Number(q.totalAmount || 0)])

    if (q.customerTermsAndConditions) {
      data.push([], ['Terms & Conditions:'], [q.customerTermsAndConditions])
    }

    // 5. Create Workbook
    const ws = XLSX.utils.aoa_to_sheet(data)
    ws['!cols'] = [
      { wch: 5 },  // #
      { wch: 10 }, // Ref
      { wch: 22 }, // Part Number
      { wch: 22 }, // Alt Part Number
      { wch: 22 }, // Supplier
      { wch: 8 },  // Qty
      { wch: 8 },  // Cond
      { wch: 15 }, // Lead Time
      { wch: 14 }, // Unit Price
      { wch: 14 }, // Total Price
    ]

    const wb = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(wb, ws, 'Quotation')

    // 6. Download — same filename pattern as PDF quote
    const fileName = `${safeName(q.quoteNumber || 'QT')} - ${safeName(q.customerName || '')} - ${safeName(q.rfqName || '')}.xlsx`
    XLSX.writeFile(wb, fileName)
    showSnack('Excel exported successfully', 'success')
  } catch (err) {
    console.error('Excel export failed:', err)
    showSnack('Excel export failed', 'error')
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>

<style scoped>
.detail-table-wrap {
  overflow-x: auto;
}

.detail-master-grid {
  width: 100%;
  border-collapse: collapse;
  min-width: 700px;
}

.detail-master-grid thead th {
  background: var(--toolbar-bg, rgba(0,0,0,0.04));
  color: rgba(var(--v-theme-on-surface), 0.6);
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 10px 12px;
  border-bottom: 2px solid var(--excel-border, rgba(0,0,0,0.08));
  text-align: left;
  white-space: nowrap;
}

.detail-master-grid tbody td {
  padding: 0 12px;
  height: 40px;
  border-bottom: 1px solid var(--card-border, rgba(0,0,0,0.08));
  vertical-align: middle;
}

/* Master rows */
.master-row {
  background: var(--toolbar-bg, rgba(0,0,0,0.02));
}
.master-row-inactive {
  opacity: 0.5;
  font-style: italic;
}

.cell-number {
  text-align: center;
  opacity: 0.5;
  font-size: 12px;
}
.cell-pn {
  color: var(--pn-color, #60a5fa);
  font-weight: 600;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
}
.cell-pn-inactive {
  color: var(--v-medium-emphasis-opacity, rgba(0,0,0,0.38)) !important;
}
.cell-status {
  font-size: 12px;
  font-style: italic;
}

/* Detail sub-row */
.detail-sub-cell {
  padding: 0 !important;
  background: var(--excel-bg, rgba(0,0,0,0.01));
  border-bottom: 2px solid var(--card-hover-border, rgba(0,0,0,0.12)) !important;
}

/* Procurement panel */
.proc-panel {
  padding: 10px 16px 10px 48px;
  border-left: 3px solid #3b82f6;
  margin-left: 16px;
  overflow-x: auto;
  scrollbar-width: auto;
  scrollbar-color: var(--card-border) #252A37;
}

.proc-panel::-webkit-scrollbar {
  height: 10px;
}

.proc-panel::-webkit-scrollbar-track {
  background: #252A37;
  border-radius: 5px;
}

.proc-panel::-webkit-scrollbar-thumb {
  background: var(--card-border);
  border-radius: 5px;
}

.proc-panel::-webkit-scrollbar-thumb:hover {
  background: var(--row-hover);
}

.empty-proc {
  padding: 10px 16px 10px 48px;
}

/* Procurement grid */
.proc-grid {
  width: 100%;
  border-collapse: collapse;
  /* min-width: 2000px; */
}
.proc-grid thead th {
  opacity: 0.55;
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 5px 8px;
  border-bottom: 1px solid var(--card-border, rgba(0,0,0,0.08));
  text-align: left;
  white-space: nowrap;
}
.proc-grid tbody td {
  padding: 3px 4px;
  height: 36px;
  border-bottom: 1px solid var(--card-border, rgba(0,0,0,0.06));
  vertical-align: middle;
}

.proc-row {
  transition: background-color 0.15s;
}

/* Selected: green highlight */
.selected-proc-row {
  background: rgba(74, 222, 128, 0.10);
  border-left: 3px solid #4ade80;
}
.selected-proc-row:hover {
  background: rgba(74, 222, 128, 0.16);
}

.selected-proc-row td[style*="position: sticky"] {
  background: rgba(74, 222, 128, 0.15) !important;
}

/* Unselected: muted */
.unselected-proc-row {
  opacity: 0.45;
}
.unselected-proc-row:hover {
  opacity: 0.65;
  background: var(--row-hover);
}

.mono-cell {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 12px;
  opacity: 0.75;
}
.total-selected {
  color: #4ade80 !important;
  font-weight: 600;
  opacity: 1 !important;
}
.text-right { text-align: right; }
.pr-2 { padding-right: 8px !important; }
.text-center { text-align: center; }

/* Shop sub-rows: slightly indented and tinted orange */
.shop-sub-row {
  background: rgba(255, 152, 0, 0.04) !important;
  border-left: 2px solid rgba(255, 152, 0, 0.35);
}
.shop-sub-row:hover {
  background: rgba(255, 152, 0, 0.09) !important;
}
.shop-sub-row td[style*="position: sticky"] {
  background: rgba(255, 152, 0, 0.06) !important;
}

/* Drag and drop */
.drag-handle-cell {
  width: 36px;
}
.drag-handle-icon {
  opacity: 0.35;
  cursor: grab;
  transition: opacity 0.15s;
}
.proc-row:hover .drag-handle-icon {
  opacity: 0.8;
}
.dragging-row {
  opacity: 0.4;
}
.drag-over-row {
  border-top: 2px solid #3b82f6 !important;
  background: rgba(59, 130, 246, 0.06) !important;
}
</style>
