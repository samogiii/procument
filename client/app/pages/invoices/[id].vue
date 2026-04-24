<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/invoices" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Proforma Invoice {{ invoice.invoiceNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <div class="d-flex flex-wrap align-center gap-1 gap-sm-2">
        <!-- Status Chip with Dropdown (admin only) -->
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
          <v-list density="compact" style="min-width: 180px">
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

        <v-btn v-if="isAdmin" prepend-icon="mdi-shield-account" variant="tonal" size="small" @click="showPermissions = true">Perms</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-history" variant="tonal" size="small" @click="showAudit = true">Audit</v-btn>
        <v-btn v-if="isAdmin" prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
        <v-btn
          v-if="procurementId && (invoice.status !== 'Draft' && invoice.status !== 'Pending')"
          :to="`/procurements/${procurementId}`"
          variant="tonal"
          color="primary"
          size="small"
          prepend-icon="mdi-clipboard-edit-outline"
        >
          View Procurement
        </v-btn>
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

    <v-row class="mb-6">
      <v-col cols="12" md="3">
        <StatCard icon="mdi-account-outline" color="primary" label="Customer" :value="invoice.customerName" />
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

    <!-- Rejection Note -->
    <v-alert
      v-if="invoice.status === 'Rejected' && invoice.rejectionNote"
      type="error"
      variant="tonal"
      class="mb-6"
      icon="mdi-close-circle-outline"
    >
      <div class="font-weight-bold mb-1">Rejection Reason</div>
      {{ invoice.rejectionNote }}
    </v-alert>

    <!-- Edit Details -->
    <v-card class="glass-card mb-6" v-if="!isLocked">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-pencil-box-outline" class="mr-2" size="20" />
        Invoice Details
        <v-spacer />
        <v-btn v-if="!editingDetails" variant="tonal" size="small" prepend-icon="mdi-pencil" @click="startEditDetails">Edit</v-btn>
        <template v-else>
          <v-btn variant="text" size="small" class="mr-1" @click="cancelEditDetails">Cancel</v-btn>
          <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-content-save" :loading="savingDetails" @click="saveDetails">Save</v-btn>
        </template>
      </v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="6">
            <v-text-field v-model="detailsForm.customerPONumber" label="Customer PO Number" variant="outlined" density="compact" hide-details :readonly="!editingDetails" clearable />
          </v-col>
          <v-col cols="12" md="6">
            <v-text-field v-model="detailsForm.dueDate" label="Due Date" variant="outlined" density="compact" hide-details type="date" :readonly="!editingDetails" />
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
          <template #item.expectedDeliveryDate="{ item }: { item: any }">
            {{ item.expectedDeliveryDate ? new Date(item.expectedDeliveryDate).toLocaleDateString() : '—' }}
          </template>
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
          <template #item.totalPrice="{ item }: { item: any }">
            ${{ formatPrice((item._qty || 0) * (item._unitPrice || 0)) }}
          </template>
          <template #item.discount="{ item }: { item: any }">
            <span v-if="item._discount > 0" style="color:#e53935; font-weight:600;">
              -${{ formatPrice(item._discount) }}
            </span>
            <span v-else-if="item._discount < 0" style="color:rgb(var(--v-theme-primary)); font-weight:600;">
              +${{ formatPrice(Math.abs(item._discount)) }}
            </span>
            <span v-else class="text-medium-emphasis">—</span>
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

    <!-- Rejection Note Dialog -->
    <v-dialog v-model="showRejectDialog" max-width="450" persistent>
      <v-card>
        <v-card-title class="text-h6">Reject Proforma Invoice</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">Please provide a reason for rejecting this invoice:</p>
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

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000">
      {{ snackbarText }}
    </v-snackbar>

    <!-- Documents -->
    <InvoiceDocuments :invoice-id="Number(route.params.id)" class="mt-6" ref="documentsRef" />

    <InvoicePdfGenerator v-model="showPdf" :invoice="invoice" @pdf-uploaded="documentsRef?.loadDocuments()" />
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

const isAdmin = computed(() => authStore.isAdmin)
const showPdf = ref(false)
const canCreateFinal = ref(false)
const creatingFinal = ref(false)

// Edit Details
const editingDetails = ref(false)
const savingDetails = ref(false)
const detailsForm = ref<any>({ customerPONumber: '', dueDate: '' })
const detailsOriginal = ref<any>({})

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('invoice', entityId)

const invoiceStatuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Pending', label: 'Pending', icon: 'mdi-clock-outline', color: 'warning' },
  { value: 'Accepted', label: 'Accepted', icon: 'mdi-check-decagram-outline', color: 'success' },
  { value: 'Net30', label: 'Net30', icon: 'mdi-calendar-clock', color: 'indigo' },
  { value: 'CAD', label: 'CAD', icon: 'mdi-currency-usd', color: 'cyan' },
  { value: 'Paid', label: 'Paid', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Prepeyment', label: 'Prepeyment', icon: 'mdi-cash-marker', color: 'orange' },
  { value: 'Rejected', label: 'Rejected', icon: 'mdi-close-circle', color: 'error' },
]

const itemHeaders = [
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Description', key: 'description' },
  { title: 'Qty', key: 'qty', sortable: false },
  { title: 'Lead Time', key: 'expectedDeliveryDate' },
  { title: 'Unit Price', key: 'unitPrice', sortable: false },
  { title: 'Total', key: 'totalPrice' },
  { title: 'Discount', key: 'discount', sortable: false },
]

// Editable items with local _qty, _unitPrice, _discount
const itemsWithFinalPrice = ref<any[]>([])
const itemsDirty = ref(false)
const savingItems = ref(false)

// ── RFQ Ex box ──
const rfqExTypeOptions = [
  { value: 0, label: 'Ex Warehouse', color: 'success' },
  { value: 1, label: 'Ex Vendor', color: 'info' },
  { value: 2, label: 'Ex Customer', color: 'warning' },
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
      dueDate: invoice.value.dueDate ? invoice.value.dueDate.substring(0, 10) : '',
    }
    detailsOriginal.value = { ...detailsForm.value }
    initItemPrices()

    // Fetch related procurement if applicable
    if (invoice.value.status !== 'Draft' && invoice.value.status !== 'Pending') {
      const res = await api.get<any>(`/procurements?search=${invoice.value.invoiceNumber}&pageSize=1`)
      procurementId.value = res?.items?.[0]?.id ?? null
    }
  } catch {
    showSnack('Failed to load proforma invoice', 'error')
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
    await api.put(`/invoices/${route.params.id}`, detailsForm.value)
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

const showRejectDialog = ref(false)
const rejectionNote = ref('')

function onStatusSelect(newStatus: string) {
  if (newStatus === invoice.value.status) return
  if (newStatus === 'Rejected') {
    rejectionNote.value = ''
    showRejectDialog.value = true
    return
  }
  updateStatus(newStatus)
}

async function confirmReject() {
  showRejectDialog.value = false
  await updateStatus('Rejected', rejectionNote.value || undefined)
}

async function updateStatus(newStatus: string, note?: string) {
  try {
    await api.patch(`/invoices/${route.params.id}/status`, { status: newStatus, rejectionNote: note || null })
    invoice.value.status = newStatus
    invoice.value.rejectionNote = note || null
    showSnack(`Status updated to ${newStatus}`, 'success')
  } catch {
    showSnack('Failed to update status', 'error')
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>
