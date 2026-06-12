<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/invoices" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Sales Order {{ invoice.invoiceNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <div class="d-flex flex-wrap align-center gap-1 gap-sm-2">
        <!-- Invoice Status Chip with Dropdown (admin only) -->
        <v-menu v-if="isAdmin" :disabled="isLocked">
          <template #activator="{ props: menuProps }">
            <v-chip
              :color="statusColor(invoice.status)"
              v-bind="menuProps"
              class="cursor-pointer"
              :append-icon="isLocked ? 'mdi-lock' : 'mdi-chevron-down'"
              size="default"
            >
              {{ invoice.status || '—' }}
            </v-chip>
          </template>
          <v-list density="compact" style="min-width: 210px">
            <v-list-subheader>Change Status</v-list-subheader>
            <v-list-item
              v-for="s in invoiceStatuses"
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
        <v-chip v-else :color="statusColor(invoice.status)" size="default" :append-icon="isLocked ? 'mdi-lock' : undefined">{{ invoice.status || '—' }}</v-chip>

        <!-- Payment Status badge (read-only) -->
        <v-chip
          v-if="invoice.paymentStatus"
          size="default"
          :color="paymentStatusColor(invoice.paymentStatus)"
          variant="tonal"
          prepend-icon="mdi-credit-card-outline"
        >
          {{ invoice.paymentStatus }}<span v-if="invoice.paymentStatus === 'Prepayment' && invoice.prepaymentPercent">&nbsp;{{ invoice.prepaymentPercent }}%</span>
        </v-chip>

        <v-btn v-if="isAdmin" prepend-icon="mdi-shield-account" variant="tonal" size="small" @click="showPermissions = true">Perms</v-btn>
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
        <v-btn v-if="!editingDetails" variant="tonal" size="small" prepend-icon="mdi-pencil" @click="startEditDetails">Edit</v-btn>
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
              :items="['Net30', 'CAD', 'Prepayment']"
              label="Payment Status"
              variant="outlined"
              density="compact"
              hide-details
              :readonly="!editingDetails"
              clearable
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
          <v-col cols="12" md="4">
            <v-select
              v-model="detailsForm.defaultDepositWalletId"
              :items="walletOptions"
              label="Deposit Wallet"
              variant="outlined"
              density="compact"
              hide-details
              :readonly="!editingDetails"
              clearable
              prepend-inner-icon="mdi-wallet-outline"
            />
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
          v-if="itemsDirty"
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
              style="min-width:80px; max-width:100px;"
              @update:model-value="onItemChange(item)"
            />
          </template>
          <!-- CD (Condition) -->
          <template #item.condition="{ item }: { item: any }">
            <v-chip v-if="item.condition" size="x-small" color="info" variant="tonal">{{ item.condition }}</v-chip>
            <span v-else class="text-medium-emphasis">—</span>
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
              style="min-width:140px; max-width:160px;"
              @update:model-value="onItemChange(item)"
            />
          </template>
          <!-- Totals footer row -->
          <template #body.append>
            <tr style="background: rgba(var(--v-theme-surface-variant), 0.4); font-weight: 600;">
              <td colspan="4" class="text-right text-caption text-medium-emphasis px-3 py-2">Totals:</td>
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
            </tr>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Dialogs -->
    <v-dialog v-model="showPermissions" max-width="600">
      <PermissionManager :entity-name="'Invoice'" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-dialog v-model="showAudit" max-width="800">
      <BusinessAuditViewer entity-name="Invoice" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000">
      {{ snackbarText }}
    </v-snackbar>

    <!-- Documents -->
    <InvoiceDocuments :invoice-id="Number(route.params.id)" class="mt-6" ref="documentsRef" />

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
const procurementId = ref<number | null>(null)
const documentsRef = ref<any>(null)
const showPermissions = ref(false)
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
const showPdf = ref(false)
const canCreateFinal = ref(false)
const creatingFinal = ref(false)

// Edit Details
const editingDetails = ref(false)
const savingDetails = ref(false)
const detailsForm = ref<any>({ customerPONumber: '', customerPODate: '', dueDate: '', deadlineDate: '', subject: '', paymentStatus: '', prepaymentPercent: null, defaultDepositWalletId: null })
const detailsOriginal = ref<any>({})

// Wallets for deposit
const wallets = ref<any[]>([])
const walletOptions = computed(() => {
  return wallets.value.map((w: any) => ({
    title: w.name ? `${w.name} (${w.currency})` : `${w.companyPresetName} (${w.currency})`,
    value: w.id,
  }))
})

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('invoice', entityId)

const invoiceStatuses = [
  { value: 'Draft',                  label: 'Draft',                  icon: 'mdi-file-edit-outline',   color: 'grey'    },
  { value: 'Pending',                label: 'Pending',                icon: 'mdi-clock-outline',       color: 'warning' },
  { value: 'Running',                label: 'Running',                icon: 'mdi-play-circle-outline', color: 'blue'    },
  { value: 'Waiting For PrePayment', label: 'Waiting For PrePayment', icon: 'mdi-cash-clock',          color: 'orange'  },
  { value: 'Delivered',              label: 'Delivered',              icon: 'mdi-truck-check-outline', color: 'teal'    },
  { value: 'Finish',                 label: 'Finish',                 icon: 'mdi-check-circle',        color: 'success' },
]

function paymentStatusColor(ps: string): string {
  if (ps === 'Net30') return 'indigo'
  if (ps === 'CAD') return 'cyan'
  if (ps === 'Prepayment') return 'orange'
  return 'grey'
}

const itemHeaders = [
  { title: 'Ref', key: 'rfqReference', sortable: false, width: '60px' },
  { title: 'Part No.', key: 'partNumberName', sortable: false },
  { title: 'Description', key: 'description', sortable: false },
  { title: 'Qty', key: 'qty', sortable: false, width: '110px' },
  { title: 'CD', key: 'condition', sortable: false, width: '80px' },
  { title: 'Cert', key: 'certName', sortable: false, width: '100px' },
  { title: 'Unit Price', key: 'unitPrice', sortable: false, width: '155px' },
  { title: 'Total', key: 'totalPrice', sortable: false, width: '120px' },
  { title: 'Discount', key: 'discount', sortable: false, width: '120px' },
  { title: 'Delivery', key: 'expectedDeliveryDate', sortable: false, width: '170px' },
]

// Editable items with local _qty, _unitPrice, _discount
const itemsWithFinalPrice = ref<any[]>([])
const itemsDirty = ref(false)
const savingItems = ref(false)

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
    _origQty: Number(it.qty),
    _origUnitPrice: Number(it.unitPrice),
    _discount: it.discount || 0,
    _delivery: it.expectedDeliveryDate ? it.expectedDeliveryDate.substring(0, 10) : '',
  }))
  itemsDirty.value = false
}

function onItemChange(item: any) {
  const ref = Number(item.originalUnitPrice ?? item._origUnitPrice ?? item._unitPrice)
  const unit = Number(item._unitPrice || 0)
  const qty = Number(item._qty || 0)
  const perUnit = ref - unit
  item._discount = Number((perUnit * qty).toFixed(2))
  itemsDirty.value = true
}

async function saveItemEdits() {
  savingItems.value = true
  try {
    const payload = {
      items: itemsWithFinalPrice.value.map((it: any) => ({
        id: it.id,
        qty: Number(it._qty) || 1,
        unitPrice: Number(it._unitPrice) || 0,
        expectedDeliveryDate: it._delivery || null,
      }))
    }
    await api.patch(`/invoices/${route.params.id}/items`, payload)
    itemsDirty.value = false
    showSnack('Items saved', 'success')
    await loadInvoice()
  } catch {
    showSnack('Failed to save items', 'error')
  } finally {
    savingItems.value = false
  }
}

onMounted(async () => {
  await loadInvoice()
  await Promise.all([checkFinalEligibility(), checkLock()])
})

async function loadInvoice() {
  try {
    invoice.value = await api.get(`/invoices/${route.params.id}`)
    detailsForm.value = {
      customerPONumber: invoice.value.customerPONumber || '',
      customerPODate: invoice.value.customerPODate ? invoice.value.customerPODate.substring(0, 10) : '',
      dueDate: invoice.value.dueDate ? invoice.value.dueDate.substring(0, 10) : '',
      deadlineDate: invoice.value.deadlineDate ? invoice.value.deadlineDate.substring(0, 10) : '',
      subject: invoice.value.subject || '',
      paymentStatus: invoice.value.paymentStatus || '',
      prepaymentPercent: invoice.value.prepaymentPercent ?? null,
      defaultDepositWalletId: invoice.value.defaultDepositWalletId ?? null,
      tax: invoice.value.tax ?? 0,
      shipping: invoice.value.shipping ?? 0,
      processingFee: invoice.value.processingFee ?? 0,
    }
    detailsOriginal.value = { ...detailsForm.value }
    initItemPrices()

    // Fetch wallets for this customer to map them
    if (invoice.value.customerId) {
      try {
        wallets.value = await api.get(`/payment-boxes/for-customer/${invoice.value.customerId}`)
      } catch {
        wallets.value = []
      }
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

function startEditDetails() {
  editingDetails.value = true
}

function cancelEditDetails() {
  detailsForm.value = { ...detailsOriginal.value }
  editingDetails.value = false
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

    detailsOriginal.value = { ...detailsForm.value }
    editingDetails.value = false
    showSnack('Details saved', 'success')
    await loadInvoice()
  } catch { showSnack('Failed to save details', 'error') }
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
    if (newStatus === 'Running' && itemsDirty.value) {
      await saveItemEdits()
    }
    await api.patch(`/invoices/${route.params.id}/status`, {
      status: newStatus,
      autoFinalize
    })
    invoice.value.status = newStatus
    showSnack(`Status updated to ${newStatus}`, 'success')
    // Re-fetch procurement link when status moves to Running
    if (newStatus === 'Running') await loadInvoice()
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
