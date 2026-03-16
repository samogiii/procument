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
        <StatCard icon="mdi-calendar-clock" color="warning" label="Due Date"
          :value="invoice.dueDate ? new Date(invoice.dueDate).toLocaleDateString() : '—'"
        />
      </v-col>
      <v-col cols="12" md="3">
        <StatCard icon="mdi-calendar-check" color="info" label="Paid Date"
          :value="invoice.paidDate ? new Date(invoice.paidDate).toLocaleDateString() : 'Unpaid'"
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

    <v-card class="glass-card">
      <v-card-title>Line Items</v-card-title>
      <v-card-text>
        <v-data-table :headers="itemHeaders" :items="invoice.items || []" density="comfortable" :items-per-page="50">
          <template #item.expectedDeliveryDate="{ item }: { item: any }">
            {{ item.expectedDeliveryDate ? new Date(item.expectedDeliveryDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.unitPrice="{ item }: { item: any }">
            ${{ formatPrice(item.unitPrice) }}
          </template>
          <template #item.totalPrice="{ item }: { item: any }">
            ${{ formatPrice(item.totalPrice) }}
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Dialogs -->
    <v-dialog v-model="showPermissions" max-width="600">
      <PermissionManager :entity-name="'Invoice'" :entity-id="route.params.id as string" />
    </v-dialog>

    <v-dialog v-model="showAudit" max-width="700">
      <AuditLogViewer :entity-name="'Invoice'" :entity-id="route.params.id as string" />
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

    <InvoicePdfGenerator v-model="showPdf" :invoice="invoice" />
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const { statusColor } = useStatusColor()

const invoice = ref<any>({})
const showPermissions = ref(false)
const showAudit = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const isAdmin = computed(() => authStore.isAdmin)
const showPdf = ref(false)
const canCreateFinal = ref(false)
const creatingFinal = ref(false)

const entityId = computed(() => String(route.params.id))
const { isLocked, checkLock } = useFinalInvoiceLock('invoice', entityId)

const invoiceStatuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Pending', label: 'Pending', icon: 'mdi-clock-outline', color: 'warning' },
  { value: 'Paid', label: 'Paid', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Overdue', label: 'Overdue', icon: 'mdi-alert', color: 'error' },
  { value: 'Rejected', label: 'Rejected', icon: 'mdi-close-circle', color: 'error' },
]

const itemHeaders = [
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Description', key: 'description' },
  { title: 'Qty', key: 'qty' },
  { title: 'Lead Time', key: 'expectedDeliveryDate' },
  { title: 'Unit Price', key: 'unitPrice' },
  { title: 'Total', key: 'totalPrice' },
]

onMounted(async () => {
  await loadInvoice()
  await Promise.all([checkFinalEligibility(), checkLock()])
})

async function loadInvoice() {
  try {
    invoice.value = await api.get(`/invoices/${route.params.id}`)
  } catch {
    showSnack('Failed to load proforma invoice', 'error')
  }
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
