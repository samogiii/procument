<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" class="mr-1 flex-shrink-0" size="small" @click="$router.back()" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Sales Order {{ invoice.invoiceNumber || `#${route.params.id}` }}</h1>
      <!-- Base 1 number inherited from the source quote; editable so it can be added
           where there is none or corrected where it was inherited. -->
      <B1NumberEditor
        v-model="invoice.b1ProformaInvoiceNumber"
        :endpoint="`/invoices/${route.params.id}/b1-number`"
        :editable="isAdmin"
        label="B1 Proforma Invoice Number"
        placeholder="P101-60701-10"
      />
      <v-spacer />
      <div class="d-flex flex-wrap align-center gap-1 gap-sm-2">
        <!-- Assigned users can start the PI; admins can also finish it manually. -->
        <v-menu>
          <template #activator="{ props: menuProps }">
            <v-chip
              :color="statusColor(invoice.status)"
              v-bind="menuProps"
              class="cursor-pointer"
              append-icon="mdi-chevron-down"
              size="default"
            >
              {{ invoice.status || '—' }}
            </v-chip>
          </template>
          <v-list density="compact" style="min-width: 210px">
            <v-list-subheader>Change Status</v-list-subheader>
            <v-list-item
              v-for="s in availableInvoiceStatuses"
              :key="s.value"
              :value="s.value"
              :active="invoice.status === s.value"
              @click="onStatusSelect(s.value)"
            >
              <template #prepend>
                <v-icon :icon="s.icon" :color="s.color" size="18" />
              </template>
              <v-list-item-title>{{ s.label }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>
        <!-- Payment Status badge (read-only) -->
        <v-chip
          v-if="invoice.paymentStatus"
          size="default"
          :color="paymentStatusColor(invoice.paymentStatus)"
          variant="tonal"
          prepend-icon="mdi-credit-card-outline"
        >
          {{ invoice.paymentTermDisplay || invoice.paymentStatus }}<span v-if="invoice.paymentStatus === 'Prepayment' && invoice.prepaymentPercent">&nbsp;{{ invoice.prepaymentPercent }}%</span>
        </v-chip>

        <v-menu v-if="isAdmin" v-model="assignmentMenu" :close-on-content-click="false" location="bottom end">
          <template #activator="{ props: assignmentMenuProps }">
            <v-btn
              v-bind="assignmentMenuProps"
              prepend-icon="mdi-account-multiple-check-outline"
              variant="tonal"
              size="small"
              :color="savingAssignments ? 'warning' : 'primary'"
              :loading="savingAssignments"
            >
              Assign<span v-if="piAssignedUserIds.length"> ({{ piAssignedUserIds.length }})</span>
            </v-btn>
          </template>
          <v-card width="480" max-width="calc(100vw - 24px)" class="glass-card">
            <v-card-title class="d-flex align-center text-subtitle-1 py-3">
              PI &amp; Part Assignments
              <v-spacer />
              <v-chip v-if="savingAssignments" size="x-small" color="warning" variant="tonal" prepend-icon="mdi-sync">Saving</v-chip>
              <v-chip v-else-if="assignmentsSaved" size="x-small" color="success" variant="tonal" prepend-icon="mdi-check">Saved</v-chip>
              <v-btn v-else-if="assignmentSaveFailed" size="x-small" color="error" variant="tonal" prepend-icon="mdi-refresh" @click="saveAssignments">Retry</v-btn>
              <span v-else class="text-caption text-medium-emphasis">Auto-save</span>
            </v-card-title>
            <v-divider />
            <v-card-text class="pa-3">
              <v-autocomplete
                v-model="piAssignedUserIds"
                :items="assignmentUsers"
                item-title="name"
                item-value="id"
                label="Whole PI"
                multiple
                chips
                closable-chips
                clearable
                variant="outlined"
                density="compact"
                hide-details
                @update:model-value="queueAssignmentSave"
              />
              <div class="text-caption text-medium-emphasis mt-1 mb-3">Parts without an override inherit these users.</div>

              <div class="text-caption font-weight-bold mb-2">Part overrides</div>
              <div class="d-flex flex-column ga-2 overflow-y-auto pr-1" style="max-height: 360px">
                <div v-for="item in itemsWithFinalPrice" :key="item.id" class="rounded border pa-2">
                  <div class="d-flex align-center mb-1">
                    <span class="text-caption font-weight-bold" style="font-family: monospace">{{ item.alt || item.partNumberName || `Part #${item.id}` }}</span>
                    <v-spacer />
                    <span v-if="!item._assignedUserIds?.length" class="text-caption text-medium-emphasis">Inherits PI</span>
                  </div>
                  <v-autocomplete
                    v-model="item._assignedUserIds"
                    :items="assignmentUsers"
                    item-title="name"
                    item-value="id"
                    multiple
                    chips
                    closable-chips
                    clearable
                    density="compact"
                    variant="outlined"
                    placeholder="Use PI assignment"
                    hide-details
                    @update:model-value="queueAssignmentSave"
                  />
                </div>
              </div>
            </v-card-text>
          </v-card>
        </v-menu>

        <v-btn v-if="isAdmin" prepend-icon="mdi-history" variant="tonal" size="small" @click="showAudit = true">Audit</v-btn>
        <v-btn
          v-if="isAdmin && !invoice.isCancelled"
          prepend-icon="mdi-cancel"
          size="small"
          color="error"
          variant="tonal"
          :disabled="isLocked"
          @click="showCancelConfirm = true"
        >Cancel</v-btn>
        <v-btn
          v-if="isAdmin || !['Draft', 'Pending', 'Cancelled'].includes(invoice.status)"
          prepend-icon="mdi-file-pdf-box"
          size="small"
          color="error"
          @click="showPdf = true"
        >PDF</v-btn>
        <!-- <v-btn
          v-if="procurementId && !['Draft', 'Pending'].includes(invoice.status)"
          :to="`/procurements/${procurementId}`"
          variant="tonal"
          color="primary"
          size="small"
          prepend-icon="mdi-clipboard-edit-outline"
        >
          View Procurement
        </v-btn> -->
        <v-btn
          v-if="canCreateFinal"
          prepend-icon="mdi-receipt-text-check"
          size="small"
          color="success"
          variant="flat"
          :loading="creatingFinal"
          @click="createFinalInvoice"
        >Create Final Invoice</v-btn>
      </div>
    </div>

    <v-alert
      v-if="invoice.paymentDueWarning"
      type="warning"
      variant="tonal"
      class="mb-4"
      icon="mdi-clock-alert-outline"
    >
      {{ invoice.paymentTermDisplay }} is due soon or overdue. Please follow up for the customer payment and POP.
    </v-alert>

    <!-- Cancelled Banner -->
    <v-alert
      v-if="invoice.isCancelled"
      type="error"
      variant="tonal"
      class="mb-4"
      icon="mdi-cancel"
      prominent
    >
      This Sales Order was <strong>cancelled</strong>
      <span v-if="invoice.cancelledAt"> on {{ new Date(invoice.cancelledAt).toLocaleDateString() }}</span>.
      It cannot be edited or used to create a Final Invoice.
    </v-alert>

    <!-- Source Quote Banner -->
    <v-card v-if="invoice.quoteId" class="glass-card mb-4 border-s-lg" :style="{ borderColor: 'rgb(var(--v-theme-info))' }">
      <v-card-text class="pa-3">
        <div class="d-flex align-center gap-2">
          <v-icon icon="mdi-file-document-outline" color="info" size="18" />
          <span class="text-caption text-medium-emphasis">Created from</span>
          <NuxtLink :to="`/quotes/${invoice.quoteId}`" class="text-body-2 font-weight-bold text-primary text-decoration-none hover-underline">
            Quote #{{ invoice.quoteId }}
          </NuxtLink>
        </div>
      </v-card-text>
    </v-card>

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-outline" color="primary" label="Customer" :value="invoice.customerCode" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ formatPrice(invoice.totalAmount) }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-receipt-text-outline" color="purple" label="Customer PO" :value="invoice.customerPONumber || '—'" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-text-box-outline" color="blue" label="Subject" :value="invoice.subject || '—'" />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar-clock" color="warning" label="Due Date"
          :value="invoice.dueDate ? new Date(invoice.dueDate).toLocaleDateString() : '—'"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar-check" color="info" label="Paid Date"
          :value="invoice.paidDate ? new Date(invoice.paidDate).toLocaleDateString() : 'Unpaid'"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard
          icon="mdi-tag-outline"
          :color="rfqExTypeMeta.color"
          label="RFQ Ex"
          :value="rfqExTypeMeta.label"
        />
      </v-col>
    </v-row>

    <!-- Edit Details -->
    <v-card class="glass-card mb-6" v-if="!isLocked">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-pencil-box-outline" class="mr-2" size="20" />
        PI Details
        <v-spacer />
        <v-btn v-if="isAdmin && !editingDetails" variant="tonal" size="small" prepend-icon="mdi-pencil" @click="startEditDetails">Edit</v-btn>
        <template v-else>
          <v-btn variant="text" size="small" class="mr-1" @click="cancelEditDetails">Cancel</v-btn>
          <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-content-save" :loading="savingDetails" @click="saveDetails">Save</v-btn>
        </template>
      </v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="4">
            <v-text-field v-model="detailsForm.subject" label="Subject" variant="outlined" density="compact" hide-details :readonly="!editingDetails" clearable />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="detailsForm.customerPONumber" label="Customer PO Number" variant="outlined" density="compact" hide-details :readonly="!editingDetails" clearable />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="detailsForm.customerPODate" label="Customer PO Date" variant="outlined" density="compact" hide-details type="date" :readonly="!editingDetails" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="detailsForm.dueDate" label="Due Date" variant="outlined" density="compact" hide-details type="date" :readonly="!editingDetails" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="detailsForm.deadlineDate" label="Deadline Date" variant="outlined" density="compact" hide-details type="date" :readonly="!editingDetails" />
          </v-col>
          <v-col cols="12" md="4">
            <v-select
              v-model="detailsForm.paymentStatus"
              :items="['Prepayment', 'CAD', 'Net', 'Credit']"
              label="Payment Terms"
              variant="outlined"
              density="compact"
              hide-details
              :readonly="!editingDetails"
              clearable
            />
          </v-col>
          <v-col cols="12" md="4" v-if="['CAD', 'Net'].includes(detailsForm.paymentStatus)">
            <v-text-field
              v-model.number="detailsForm.paymentTermDays"
              :label="`${detailsForm.paymentStatus} Days`"
              variant="outlined"
              density="compact"
              hide-details
              type="number"
              min="1"
              max="3650"
              :readonly="!editingDetails"
            />
          </v-col>
          <v-col cols="12" md="4" v-if="detailsForm.paymentStatus === 'Prepayment'">
            <v-text-field
              v-model.number="detailsForm.prepaymentPercent"
              label="Prepayment Percent (%)"
              variant="outlined"
              density="compact"
              hide-details
              type="number"
              min="0"
              max="100"
              :readonly="!editingDetails"
            />
          </v-col>
          <!-- Accounting wallet used for automatic Customer POP deposits -->
          <v-col cols="12" md="4">
            <v-select
              v-model="detailsForm.defaultDepositWalletId"
              :items="walletOptions"
              item-title="title"
              item-value="value"
              label="Customer POP Deposit Wallet"
              variant="outlined"
              density="compact"
              hint="Customer POP payments are deposited into this wallet automatically"
              persistent-hint
              :readonly="!editingDetails"
              clearable
              prepend-inner-icon="mdi-wallet-outline"
              no-data-text="No wallets created"
            />
          </v-col>
          <!-- Bank account printed on the PI/PDF; this is separate from the accounting wallet -->
          <v-col cols="12" md="4">
            <v-select
              v-model="detailsForm.defaultBankAccountId"
              :items="bankAccountOptions"
              item-title="title"
              item-value="value"
              label="PI / PDF Bank Account"
              variant="outlined"
              density="compact"
              hide-details
              :readonly="!editingDetails"
              clearable
              prepend-inner-icon="mdi-bank-outline"
              no-data-text="No bank accounts configured"
            >
              <template #item="{ item, props: itemProps }">
                <v-list-item v-bind="itemProps">
                  <template #subtitle>
                    <span class="text-caption text-medium-emphasis">{{ item.raw.presetName }}</span>
                  </template>
                </v-list-item>
              </template>
            </v-select>
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model.number="detailsForm.tax" label="Tax" variant="outlined" density="compact" hide-details type="number" :readonly="!editingDetails" prefix="$" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model.number="detailsForm.shipping" label="Shipping" variant="outlined" density="compact" hide-details type="number" :readonly="!editingDetails" prefix="$" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model.number="detailsForm.processingFee" label="Processing Fee" variant="outlined" density="compact" hide-details type="number" :readonly="!editingDetails" prefix="$" />
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-card class="glass-card">
      <v-card-title class="d-flex align-center">
        Line Items
        <v-spacer />
        <v-btn
          v-if="canEditItems && itemsDirty"
          variant="tonal"
          color="warning"
          size="small"
          prepend-icon="mdi-content-save"
          :loading="savingItems"
          @click="saveItemEdits"
        >Save Changes</v-btn>
      </v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="itemsWithFinalPrice" density="comfortable" :items-per-page="50">
          <!-- Ref (#) -->
          <template #item.rfqReference="{ item }: { item: any }">
            <span class="text-caption font-weight-bold text-primary">{{ item.rfqReference || '—' }}</span>
          </template>
          <!-- Part No. — show Alt when present, original as secondary -->
          <template #item.partNumberName="{ item }: { item: any }">
            <div>
              <span class="font-weight-medium" style="font-family: monospace; font-size: 13px;">
                {{ item.alt || item.partNumberName || '—' }}
              </span>
              <div v-if="item.alt" class="text-caption text-medium-emphasis" style="font-size: 10px;">
                orig: {{ item.partNumberName }}
              </div>
            </div>
          </template>
          <!-- Description -->
          <template #item.description="{ item }: { item: any }">
            <span class="text-caption text-medium-emphasis">{{ item.description || '—' }}</span>
          </template>
          <template #item.status="{ item }: { item: any }">
            <v-chip size="x-small" :color="statusColor(item.status)" variant="tonal">{{ item.status || 'Not Started' }}</v-chip>
          </template>
          <template #item.assignedUsers="{ item }: { item: any }">
            <div class="d-flex flex-wrap ga-1 py-1">
              <v-chip v-for="user in item.assignedUsers || []" :key="user.id" size="x-small" color="primary" variant="tonal">
                {{ user.name }}
              </v-chip>
              <span v-if="!item.assignedUsers?.length" class="text-medium-emphasis">—</span>
            </div>
            <div v-if="!item.hasExplicitAssignments && item.assignedUsers?.length" class="text-caption text-medium-emphasis">From PI</div>
          </template>
          <!-- Qty (editable) -->
          <template #item.qty="{ item }: { item: any }">
            <v-text-field
              v-model.number="item._qty"
              type="number"
              density="compact"
              hide-details
              variant="outlined"
              min="1"
              step="1"
              :readonly="!canEditItems"
              style="min-width:80px; max-width:100px;"
              @update:model-value="onItemChange(item)"
            />
          </template>
          <!-- CD (Condition) -->
          <template #item.condition="{ item }: { item: any }">
            <v-text-field
              v-model="item._condition"
              density="compact"
              hide-details
              variant="outlined"
              placeholder="Condition"
              maxlength="100"
              :readonly="!canEditItems"
              style="min-width:110px; max-width:150px;"
              @update:model-value="onItemChange(item)"
            />
          </template>
          <!-- Cert -->
          <template #item.certName="{ item }: { item: any }">
            <span class="text-caption">{{ item.certName || '—' }}</span>
          </template>
          <!-- Unit Price (editable) -->
          <template #item.unitPrice="{ item }: { item: any }">
            <v-text-field
              v-model.number="item._unitPrice"
              type="number"
              density="compact"
              hide-details
              variant="outlined"
              prefix="$"
              step="0.01"
              min="0"
              :readonly="!canEditItems"
              style="min-width:110px; max-width:140px;"
              @update:model-value="onItemChange(item)"
            />
            <div v-if="item.originalUnitPrice != null && item._unitPrice < item.originalUnitPrice" class="text-caption text-medium-emphasis mt-1">
              orig ${{ formatPrice(item.originalUnitPrice) }}
            </div>
          </template>
          <!-- Total -->
          <template #item.totalPrice="{ item }: { item: any }">
            <span style="font-family:monospace; color: rgb(var(--v-theme-success));">
              ${{ formatPrice((item._qty || 0) * (item._unitPrice || 0)) }}
            </span>
          </template>
          <!-- Discount -->
          <template #item.discount="{ item }: { item: any }">
            <span v-if="item._discount > 0" style="color:#e53935; font-weight:600;">
              -${{ formatPrice(item._discount) }}
            </span>
            <span v-else-if="item._discount < 0" style="color:rgb(var(--v-theme-primary)); font-weight:600;">
              +${{ formatPrice(Math.abs(item._discount)) }}
            </span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <!-- Delivery (editable) -->
          <template #item.expectedDeliveryDate="{ item }: { item: any }">
            <v-text-field
              v-model="item._delivery"
              type="date"
              density="compact"
              hide-details
              variant="outlined"
              :readonly="!canEditItems"
              style="min-width:140px; max-width:160px;"
              @update:model-value="onItemChange(item)"
            />
          </template>
          <template #item.actions="{ item }: { item: any }">
            <v-btn
              v-if="canEditItems"
              icon="mdi-delete-outline"
              size="x-small"
              variant="text"
              color="error"
              :loading="removingItemId === item.id"
              :disabled="removingItemId !== null"
              title="Remove item"
              @click="removeInvoiceItem(item)"
            />
          </template>
          <!-- Totals footer row -->
          <template #body.append>
            <tr style="background: rgba(var(--v-theme-surface-variant), 0.4); font-weight: 600;">
              <td colspan="4" class="text-right text-caption text-medium-emphasis px-3 py-2">Totals:</td>
              <td class="px-3 py-2"></td>
              <td class="px-3 py-2"></td>
              <td class="px-3 py-2"></td>
              <td class="px-3 py-2"></td>
              <td class="px-3 py-2 text-caption" style="font-family: monospace;">
                Σ unit: ${{ formatPrice(totalUnitSum) }}
              </td>
              <td class="px-3 py-2" style="font-family: monospace; color: rgb(var(--v-theme-success));">
                ${{ formatPrice(totalAmount) }}
              </td>
              <td class="px-3 py-2" style="color: #e53935;">
                <span v-if="totalDiscount > 0">-${{ formatPrice(totalDiscount) }}</span>
                <span v-else class="text-medium-emphasis">—</span>
              </td>
              <td class="px-3 py-2"></td>
              <td class="px-3 py-2"></td>
            </tr>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Dialogs -->
    <v-dialog v-model="showAudit" max-width="800">
      <BusinessAuditViewer entity-name="Invoice" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000">
      {{ snackbarText }}
    </v-snackbar>

    <!-- Keep the POP wallet selectable after a Final Invoice exists. The full PI
         details remain protected, but a missing wallet must not block POP uploads. -->
    <v-card v-if="isLocked && isAdmin" class="glass-card mt-6 mb-4">
      <v-card-text>
        <v-select
          v-model="detailsForm.defaultDepositWalletId"
          :items="walletOptions"
          item-title="title"
          item-value="value"
          label="Customer POP Deposit Wallet"
          variant="outlined"
          density="compact"
          hint="You can change this wallet after the Final Invoice is created"
          persistent-hint
          clearable
          prepend-inner-icon="mdi-wallet-outline"
          no-data-text="No wallets created"
          :loading="savingDepositWallet"
          :disabled="savingDepositWallet"
          @update:model-value="saveDepositWallet"
        />
      </v-card-text>
    </v-card>

    <!-- Documents -->
    <InvoiceDocuments
      :invoice-id="Number(route.params.id)"
      :deposit-wallet="selectedDepositWallet"
      :wallets="wallets"
      class="mt-6"
      ref="documentsRef"
      @wallet-selected="onPopWalletSelected"
    />

    <InvoicePdfGenerator v-model="showPdf" :invoice="invoice" @pdf-uploaded="documentsRef?.loadDocuments()" />

    <!-- Cancel Confirmation Dialog -->
    <v-dialog v-model="showCancelConfirm" max-width="440">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center text-error">
          <v-icon icon="mdi-alert-circle-outline" class="mr-2" />
          Cancel Sales Order?
        </v-card-title>
        <v-card-text>
          Are you sure you want to <strong>cancel</strong> Sales Order <strong>{{ invoice.invoiceNumber }}</strong>?
          <div class="mt-2 text-caption text-medium-emphasis">
            This is a soft cancellation — the record remains visible under the "Cancelled" status filter.
            You cannot undo this action or create a Final Invoice from a cancelled order.
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showCancelConfirm = false">Keep</v-btn>
          <v-btn color="error" variant="flat" :loading="cancelling" @click="cancelInvoice">Yes, Cancel</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Procurement Choice Dialog -->
    <v-dialog v-model="showProcurementChoice" max-width="500">
      <v-card>
        <v-card-title class="text-h6">Procurement Process</v-card-title>
        <v-card-text class="pt-2">
          Do you want to use the standard **Procurement** process (supplier quotes, expert sourcing) for this invoice?
          <div class="mt-4 text-caption text-medium-emphasis">
            Choosing "No" will automatically create the procurement and finalize all items based on existing quotes.
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" color="primary" @click="confirmProcurementChoice(false)">No (Auto-Finalize)</v-btn>
          <v-btn variant="flat" color="primary" @click="confirmProcurementChoice(true)">Yes (Standard)</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- QTY Mismatch Error Dialog (auto-finalize blocked) -->
    <v-dialog v-model="showQtyMismatchError" max-width="520" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 text-error">
          <v-icon icon="mdi-alert-circle-outline" color="error" class="mr-2" />
          Cannot Auto-Finalize
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <p class="text-body-2 mb-3">{{ qtyMismatchMessage }}</p>
          <v-alert type="info" variant="tonal" density="compact" class="mt-2">
            <strong>What to do:</strong> Click "Go Manually" below to set the invoice to Running and then adjust quantities in the Procurement page.
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showQtyMismatchError = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" @click="proceedManually">Go Manually</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const { statusColor } = useStatusColor()

const invoice = ref<any>({})

// Show the sales order number instead of the raw id in the breadcrumb trail
const { setBreadcrumbLabel } = useBreadcrumb()
watchEffect(() => setBreadcrumbLabel(invoice.value?.invoiceNumber))

const procurementId = ref<number | null>(null)
const documentsRef = ref<any>(null)
const showAudit = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const showProcurementChoice = ref(false)
const pendingStatus = ref('')

// Cancel
const showCancelConfirm = ref(false)
const cancelling = ref(false)

const isAdmin = computed(() => authStore.isAdmin)
const canEditItems = computed(() => {
  if (isAdmin.value) return true
  const userId = authStore.user?.id
  if (!userId) return false
  return (invoice.value.assignedUsers || []).some((user: any) => user.id === userId)
    || (invoice.value.items || []).some((item: any) =>
      (item.assignedUsers || []).some((user: any) => user.id === userId))
})
const showPdf = ref(false)
const canCreateFinal = ref(false)
const creatingFinal = ref(false)

const assignmentUsers = ref<any[]>([])
const piAssignedUserIds = ref<number[]>([])
const assignmentsDirty = ref(false)
const savingAssignments = ref(false)
const assignmentMenu = ref(false)
const assignmentsSaved = ref(false)
const assignmentSaveFailed = ref(false)
let assignmentRevision = 0
let assignmentSaveTimer: ReturnType<typeof setTimeout> | null = null
let assignmentSavedTimer: ReturnType<typeof setTimeout> | null = null

// Edit Details
const editingDetails = ref(false)
const savingDetails = ref(false)
const detailsForm = ref<any>({ customerPONumber: '', customerPODate: '', dueDate: '', deadlineDate: '', subject: '', paymentStatus: '', paymentTermDays: null, prepaymentPercent: null, defaultDepositWalletId: null, defaultBankAccountId: null })
const detailsOriginal = ref<any>({})

// Wallets for deposit
const wallets = ref<any[]>([])
const savingDepositWallet = ref(false)
const walletOptions = computed(() => {
  return wallets.value.map((w: any) => ({
    title: `${w.name || `Wallet ${w.id}`} (${w.currency})${w.companyName ? ` · ${w.companyName}` : ''}`,
    value: w.id,
  }))
})
const selectedDepositWallet = computed(() =>
  wallets.value.find((w: any) => w.id === detailsForm.value.defaultDepositWalletId) ?? null
)

// Bank accounts from company presets
const allPresets = ref<any[]>([])
const bankAccountOptions = computed(() => {
  return allPresets.value.flatMap((p: any) =>
    (p.bankAccounts || []).map((ba: any) => ({
      title: `${ba.accountName}${ba.bankName ? ' · ' + ba.bankName : ''}`,
      subtitle: p.name,
      value: ba.id,
      presetName: p.name,
    }))
  )
})

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('invoice', entityId)

const invoiceStatuses = [
  { value: 'Draft',                  label: 'Draft',                  icon: 'mdi-file-edit-outline',   color: 'grey'    },
  { value: 'Waiting For Prepayment', label: 'Waiting For Prepayment', icon: 'mdi-cash-clock',          color: 'orange'  },
  { value: 'Running',                label: 'Running',                icon: 'mdi-play-circle-outline', color: 'blue'    },
  { value: 'Finish',                 label: 'Finish',                 icon: 'mdi-check-circle',        color: 'success' },
]
const availableInvoiceStatuses = computed(() => isAdmin.value
  ? invoiceStatuses
  : invoiceStatuses.filter(s => ['Waiting For Prepayment', 'Running'].includes(s.value)))

function paymentStatusColor(ps: string): string {
  if (ps === 'Net') return 'indigo'
  if (ps === 'CAD') return 'cyan'
  if (ps === 'Prepayment') return 'orange'
  if (ps === 'Credit') return 'purple'
  return 'grey'
}

const itemHeaders = [
  { title: 'Ref', key: 'rfqReference', sortable: false, width: '60px' },
  { title: 'Part No.', key: 'partNumberName', sortable: false },
  { title: 'Description', key: 'description', sortable: false },
  { title: 'Part Status', key: 'status', sortable: false, width: '170px' },
  { title: 'Assigned User', key: 'assignedUsers', sortable: false, width: '180px' },
  { title: 'Qty', key: 'qty', sortable: false, width: '110px' },
  { title: 'CD', key: 'condition', sortable: false, width: '80px' },
  { title: 'Cert', key: 'certName', sortable: false, width: '100px' },
  { title: 'Unit Price', key: 'unitPrice', sortable: false, width: '155px' },
  { title: 'Total', key: 'totalPrice', sortable: false, width: '120px' },
  { title: 'Discount', key: 'discount', sortable: false, width: '120px' },
  { title: 'Delivery', key: 'expectedDeliveryDate', sortable: false, width: '170px' },
  { title: '', key: 'actions', sortable: false, width: '56px' },
]

// Editable items with local _qty, _unitPrice, _discount
const itemsWithFinalPrice = ref<any[]>([])
const itemsDirty = ref(false)
const savingItems = ref(false)
const removingItemId = ref<number | null>(null)

// ── RFQ Ex box ── (1 and 2 both display as Vendor/Customer)
const rfqExTypeOptions = [
  { value: 0, label: 'Ex Warehouse', color: 'success' },
  { value: 1, label: 'Vendor/Customer', color: 'info' },
  { value: 2, label: 'Vendor/Customer', color: 'info' },
]
const rfqExTypeMeta = computed(() => {
  const t = invoice.value?.rfqExType
  const found = rfqExTypeOptions.find(o => o.value === t)
  return found ? { label: found.label, color: found.color } : { label: '—', color: 'grey' }
})

function initItemPrices() {
  itemsWithFinalPrice.value = (invoice.value.items || []).map((it: any) => ({
    ...it,
    _qty: Number(it.qty),
    _unitPrice: Number(it.unitPrice),
    _condition: it.condition || '',
    _origQty: Number(it.qty),
    _origUnitPrice: Number(it.unitPrice),
    _discount: it.discount || 0,
    _delivery: it.expectedDeliveryDate ? it.expectedDeliveryDate.substring(0, 10) : '',
    _assignedUserIds: (it.directAssignedUsers || []).map((user: any) => user.id),
  }))
  itemsDirty.value = false
  assignmentsDirty.value = false
}

function onItemChange(item: any) {
  const ref = Number(item.originalUnitPrice ?? item._origUnitPrice ?? item._unitPrice)
  const unit = Number(item._unitPrice || 0)
  const qty = Number(item._qty || 0)
  const perUnit = ref - unit
  item._discount = Number((perUnit * qty).toFixed(2))
  itemsDirty.value = true
}

async function saveItemEdits(): Promise<boolean> {
  savingItems.value = true
  try {
    const payload = {
      items: itemsWithFinalPrice.value.map((it: any) => ({
        id: it.id,
        qty: Number(it._qty) || 1,
        unitPrice: Number(it._unitPrice) || 0,
        condition: it._condition?.trim() || null,
        expectedDeliveryDate: it._delivery || null,
      }))
    }
    await api.patch(`/invoices/${route.params.id}/items`, payload)
    itemsDirty.value = false
    showSnack('Items saved', 'success')
    await loadInvoice()
    return true
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to save items', 'error')
    return false
  } finally {
    savingItems.value = false
  }
}

async function removeInvoiceItem(item: any) {
  const warning = `Remove ${item.alt || item.partNumberName || 'this item'} from the Proforma Invoice?\n\n`
    + 'It will also be removed from any Final Invoice. If it belongs to a Purchase Order, that PO will be cancelled.'
  if (!confirm(warning)) return

  if (itemsDirty.value && !await saveItemEdits()) return

  removingItemId.value = item.id
  try {
    const result = await api.del<any>(`/invoices/${route.params.id}/items/${item.id}`)
    const cancelled = result?.cancelledPurchaseOrders || []
    showSnack(cancelled.length
      ? `Item removed. Cancelled PO: ${cancelled.join(', ')}`
      : 'Item removed from the Proforma and Final Invoice', 'success')
    await loadInvoice()
    await checkFinalEligibility()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to remove item', 'error')
  } finally {
    removingItemId.value = null
  }
}

onMounted(async () => {
  if (isAdmin.value) {
    try {
      assignmentUsers.value = (await api.get<any[]>('/users'))
        .filter((user: any) => user.isActive)
    } catch {
      assignmentUsers.value = []
    }
  }
  await loadInvoice()
  await Promise.all([checkFinalEligibility(), checkLock()])
})

async function loadInvoice() {
  try {
    invoice.value = await api.get(`/invoices/${route.params.id}`)
    piAssignedUserIds.value = (invoice.value.directAssignedUsers?.length
      ? invoice.value.directAssignedUsers
      : invoice.value.assignedUsers || []).map((user: any) => user.id)
    detailsForm.value = {
      customerPONumber: invoice.value.customerPONumber || '',
      customerPODate: invoice.value.customerPODate ? invoice.value.customerPODate.substring(0, 10) : '',
      dueDate: invoice.value.dueDate ? invoice.value.dueDate.substring(0, 10) : '',
      deadlineDate: invoice.value.deadlineDate ? invoice.value.deadlineDate.substring(0, 10) : '',
      subject: invoice.value.subject || '',
      paymentStatus: invoice.value.paymentStatus || '',
      paymentTermDays: invoice.value.paymentTermDays ?? null,
      prepaymentPercent: invoice.value.prepaymentPercent ?? null,
      defaultDepositWalletId: invoice.value.defaultDepositWalletId ?? null,
      defaultBankAccountId: invoice.value.defaultBankAccountId ?? null,
      tax: invoice.value.tax ?? 0,
      shipping: invoice.value.shipping ?? 0,
      processingFee: invoice.value.processingFee ?? 0,
    }
    detailsOriginal.value = { ...detailsForm.value }
    initItemPrices()

    // A PI can deposit into any real wallet created in Payments. Bank/PDF presets
    // are intentionally loaded separately below.
    try {
      wallets.value = await api.get('/payment-boxes/simple-list')
    } catch {
      wallets.value = []
    }
    // Load company presets with bank accounts for the bank selector
    try {
      allPresets.value = await api.get('/companypresets')
    } catch {
      allPresets.value = []
    }

    // Fetch related procurement if applicable (created when status → Running)
    if (!['Draft', 'Pending'].includes(invoice.value.status)) {
      const res = await api.get<any>(`/procurements?search=${invoice.value.invoiceNumber}&pageSize=1`)
      procurementId.value = res?.items?.[0]?.id ?? null
    }
  } catch {
    showSnack('Failed to load Sale Order', 'error')
  }
}

function scheduleAssignmentSave() {
  if (assignmentSaveTimer) clearTimeout(assignmentSaveTimer)
  assignmentSaveTimer = setTimeout(() => void saveAssignments(), 600)
}

function queueAssignmentSave() {
  assignmentRevision += 1
  assignmentsDirty.value = true
  assignmentsSaved.value = false
  assignmentSaveFailed.value = false
  scheduleAssignmentSave()
}

async function saveAssignments() {
  if (savingAssignments.value) return
  if (assignmentSaveTimer) {
    clearTimeout(assignmentSaveTimer)
    assignmentSaveTimer = null
  }

  const savingRevision = assignmentRevision
  const payload = {
    invoiceUserIds: [...piAssignedUserIds.value],
    items: itemsWithFinalPrice.value.map((item: any) => ({
      invoiceItemId: item.id,
      userIds: [...(item._assignedUserIds || [])],
    })),
  }
  assignmentsDirty.value = false
  savingAssignments.value = true
  try {
    const updated: any = await api.put(`/invoices/${route.params.id}/assignments`, payload)
    if (savingRevision === assignmentRevision) {
      invoice.value.assignedUsers = updated.assignedUsers || []
      invoice.value.directAssignedUsers = updated.directAssignedUsers || []
      invoice.value.hasExplicitAssignments = updated.hasExplicitAssignments
      const updatedItems = new Map((updated.items || []).map((item: any) => [item.id, item]))
      for (const item of itemsWithFinalPrice.value) {
        const savedItem: any = updatedItems.get(item.id)
        if (!savedItem) continue
        item.assignedUsers = savedItem.assignedUsers || []
        item.directAssignedUsers = savedItem.directAssignedUsers || []
        item.hasExplicitAssignments = savedItem.hasExplicitAssignments
        item._assignedUserIds = item.directAssignedUsers.map((user: any) => user.id)
      }
      assignmentsSaved.value = true
      if (assignmentSavedTimer) clearTimeout(assignmentSavedTimer)
      assignmentSavedTimer = setTimeout(() => { assignmentsSaved.value = false }, 1800)
    }
  } catch (e: any) {
    if (savingRevision === assignmentRevision) {
      assignmentsDirty.value = true
      assignmentSaveFailed.value = true
    }
    showSnack(e?.data?.message || 'Failed to save assignments', 'error')
  } finally {
    savingAssignments.value = false
    if (assignmentRevision > savingRevision) {
      assignmentsDirty.value = true
      scheduleAssignmentSave()
    }
  }
}

onBeforeUnmount(() => {
  if (assignmentSaveTimer) {
    clearTimeout(assignmentSaveTimer)
    assignmentSaveTimer = null
    if (assignmentsDirty.value && !savingAssignments.value) void saveAssignments()
  }
  if (assignmentSavedTimer) clearTimeout(assignmentSavedTimer)
})

function startEditDetails() {
  editingDetails.value = true
}

function cancelEditDetails() {
  detailsForm.value = { ...detailsOriginal.value }
  editingDetails.value = false
}

async function saveDepositWallet(walletId: number | null) {
  savingDepositWallet.value = true
  try {
    await api.patch(`/invoices/${route.params.id}/default-wallet`, {
      walletId: walletId || null
    })
    detailsOriginal.value.defaultDepositWalletId = walletId || null
    showSnack('Customer POP deposit wallet saved', 'success')
  } catch (e: any) {
    detailsForm.value.defaultDepositWalletId = detailsOriginal.value.defaultDepositWalletId ?? null
    showSnack(e?.data?.message || e?.data || 'Failed to save deposit wallet', 'error')
  } finally {
    savingDepositWallet.value = false
  }
}

function onPopWalletSelected(walletId: number) {
  detailsForm.value.defaultDepositWalletId = walletId
  detailsOriginal.value.defaultDepositWalletId = walletId
  if (invoice.value) invoice.value.defaultDepositWalletId = walletId
}

async function saveDetails() {
  savingDetails.value = true
  try {
    const payload = {
      dueDate: detailsForm.value.dueDate || null,
      deadlineDate: detailsForm.value.deadlineDate || null,
      customerPONumber: detailsForm.value.customerPONumber || null,
      customerPODate: detailsForm.value.customerPODate || null,
      subject: detailsForm.value.subject || null,
      paymentStatus: detailsForm.value.paymentStatus || null,
      paymentTermDays: ['CAD', 'Net'].includes(detailsForm.value.paymentStatus) ? detailsForm.value.paymentTermDays : null,
      prepaymentPercent: detailsForm.value.paymentStatus === 'Prepayment' ? detailsForm.value.prepaymentPercent : null,
      tax: detailsForm.value.tax,
      shipping: detailsForm.value.shipping,
      processingFee: detailsForm.value.processingFee,
    }
    
    await api.patch(`/invoices/${route.params.id}`, payload)

    // Save default wallet if changed
    if (detailsForm.value.defaultDepositWalletId !== detailsOriginal.value.defaultDepositWalletId) {
      await api.patch(`/invoices/${route.params.id}/default-wallet`, {
        walletId: detailsForm.value.defaultDepositWalletId || null
      })
    }
    // Save default bank account if changed
    if (detailsForm.value.defaultBankAccountId !== detailsOriginal.value.defaultBankAccountId) {
      await api.patch(`/invoices/${route.params.id}/default-bank-account`, {
        bankAccountId: detailsForm.value.defaultBankAccountId || null
      })
    }

    detailsOriginal.value = { ...detailsForm.value }
    editingDetails.value = false
    showSnack('Details saved', 'success')
    await loadInvoice()
  } catch (e: any) { showSnack(e?.data?.message || e?.data || 'Failed to save details', 'error') }
  finally { savingDetails.value = false }
}

async function checkFinalEligibility() {
  try {
    const res = await api.get<any>(`/final-invoices/check-eligibility/${route.params.id}`)
    canCreateFinal.value = res?.eligible === true
  } catch {
    canCreateFinal.value = false
  }
}

async function createFinalInvoice() {
  creatingFinal.value = true
  try {
    const result = await api.post<any>('/final-invoices', { proformaInvoiceId: Number(route.params.id) })
    showSnack(`Final Invoice ${result.invoiceNumber} created!`, 'success')
    navigateTo(`/final-invoices/${result.id}`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to create final invoice', 'error')
  } finally {
    creatingFinal.value = false
  }
}

function onStatusSelect(newStatus: string) {
  if (newStatus === invoice.value.status) return

  if (newStatus === 'Running' && isAdmin.value) {
    pendingStatus.value = newStatus
    showProcurementChoice.value = true
    return
  }

  updateStatus(newStatus)
}

function confirmProcurementChoice(useStandard: boolean) {
  showProcurementChoice.value = false
  updateStatus(pendingStatus.value, !useStandard)
}

// ── QTY mismatch error dialog ──
const showQtyMismatchError = ref(false)
const qtyMismatchMessage = ref('')

async function proceedManually() {
  showQtyMismatchError.value = false
  await updateStatus(pendingStatus.value, false) // re-try without auto-finalize
}

async function updateStatus(newStatus: string, autoFinalize = false) {
  try {
    // Auto-save unsaved item changes before creating procurement
    if (['Waiting For Prepayment', 'Running'].includes(newStatus) && itemsDirty.value) {
      await saveItemEdits()
    }
    await api.patch(`/invoices/${route.params.id}/status`, {
      status: newStatus,
      autoFinalize
    })
    invoice.value.status = newStatus
    showSnack(`Status updated to ${newStatus}`, 'success')
    // Re-fetch procurement link when status moves to Running
    if (['Waiting For Prepayment', 'Running'].includes(newStatus)) await loadInvoice()
  } catch (e: any) {
    const msg: string = e?.data?.message || ''
    if (autoFinalize && msg.includes('auto-finalize')) {
      // Show the dedicated mismatch dialog instead of a plain snackbar
      qtyMismatchMessage.value = msg
      showQtyMismatchError.value = true
    } else {
      showSnack(msg || 'Failed to update status', 'error')
    }
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Line item totals ──
const totalAmount = computed(() =>
  itemsWithFinalPrice.value.reduce((s: number, it: any) => s + (Number(it._qty) || 0) * (Number(it._unitPrice) || 0), 0)
)
const totalUnitSum = computed(() =>
  itemsWithFinalPrice.value.reduce((s: number, it: any) => s + (Number(it._unitPrice) || 0), 0)
)
const totalDiscount = computed(() =>
  itemsWithFinalPrice.value.reduce((s: number, it: any) => s + Math.max(0, Number(it._discount) || 0), 0)
)

// ── Cancel invoice ──
async function cancelInvoice() {
  cancelling.value = true
  try {
    await api.post(`/invoices/${route.params.id}/cancel`, {})
    showCancelConfirm.value = false
    showSnack('Sales Order cancelled', 'success')
    await loadInvoice()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to cancel Sales Order', 'error')
  } finally {
    cancelling.value = false
  }
}
</script>
