<template>
  <div>
    <div class="d-flex align-center mb-4 mb-md-6">
      <h1 class="text-h6 text-sm-h5 font-weight-bold">Payment Dashboard</h1>
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="loadQueue">Refresh</v-btn>
    </div>

    <v-row class="mb-4">
      <v-col cols="12" md="4">
        <StatCard icon="mdi-clock-outline" color="warning" label="Awaiting Payment" :value="String(pendingCount)" />
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-check-circle" color="success" label="Submitted" :value="String(submittedCount)" />
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-clipboard-list-outline" color="primary" label="Total Approved" :value="String(queue.length)" />
      </v-col>
    </v-row>

    <v-card class="glass-card">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-cash-multiple" class="mr-2" size="20" />
        Approved Purchase Orders
      </v-card-title>
      <v-card-text>
        <v-data-table
          :headers="headers"
          :items="queue"
          :loading="loading"
          density="comfortable"
          :items-per-page="50"
          @click:row="onRowClick"
          class="cursor-pointer"
        >
          <template #item.poNumber="{ item }: { item: any }">
            <span class="font-weight-bold">{{ item.poNumber }}</span>
          </template>
          <template #item.totalAmount="{ item }: { item: any }">
            ${{ formatPrice(item.totalAmount) }}
          </template>
          <template #item.paymentStatus="{ item }: { item: any }">
            <v-chip
              v-if="item.paymentApproval === 'Rejected'"
              size="small"
              color="error"
              prepend-icon="mdi-alert-circle"
            >Rejected</v-chip>
            <v-chip
              v-else
              size="small"
              :color="item.paymentStatus === 'Submitted' ? 'success' : 'warning'"
              :prepend-icon="item.paymentStatus === 'Submitted' ? 'mdi-check-circle' : 'mdi-clock-outline'"
            >{{ item.paymentStatus === 'Submitted' ? 'Submitted' : 'Pending' }}</v-chip>
          </template>
          <template #item.adminApprovalAt="{ item }: { item: any }">
            {{ item.adminApprovalAt ? new Date(item.adminApprovalAt).toLocaleDateString() : '—' }}
          </template>
          <template #item.actions="{ item }: { item: any }">
            <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-eye" @click.stop="openPo(item)">Open</v-btn>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- PO Detail Drawer/Dialog -->
    <v-dialog v-model="showDetail" max-width="900">
      <v-card v-if="selectedPo">
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-receipt-text" class="mr-2" />
          {{ selectedPo.poNumber }}
          <v-spacer />
          <v-chip
            size="small"
            :color="selectedPo.paymentStatus === 'Submitted' ? 'success' : 'warning'"
          >{{ selectedPo.paymentStatus === 'Submitted' ? 'Submitted' : 'Awaiting Payment' }}</v-chip>
        </v-card-title>
        <v-card-text>
          <v-row dense class="mb-4">
            <v-col cols="12" md="4">
              <div class="text-caption text-medium-emphasis">Supplier</div>
              <div class="font-weight-medium">{{ selectedPo.supplierName }}</div>
            </v-col>
            <v-col cols="12" md="4">
              <div class="text-caption text-medium-emphasis">Total Amount</div>
              <div class="font-weight-medium">${{ formatPrice(selectedPo.totalAmount) }}</div>
            </v-col>
            <v-col cols="12" md="4">
              <div class="text-caption text-medium-emphasis">Status</div>
              <div class="d-flex align-center gap-1">
                <v-chip
                  size="small"
                  :color="selectedPo.paymentStatus === 'Submitted' ? 'success' : 'warning'"
                >{{ selectedPo.paymentStatus === 'Submitted' ? 'Submitted' : 'Awaiting Payment' }}</v-chip>
                <v-chip
                  v-if="selectedPo.paymentApproval !== 'Pending'"
                  size="small"
                  :color="selectedPo.paymentApproval === 'Accepted' ? 'success' : 'error'"
                  variant="tonal"
                >{{ selectedPo.paymentApproval }}</v-chip>
              </div>
            </v-col>
          </v-row>

          <v-divider class="my-3" />

          <!-- Supplier Invoice (read-only) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-invoice-text" size="18" class="mr-1" />
              Supplier Invoice
            </div>
            <div v-if="supplierInvoiceFile" class="d-flex align-center gap-2">
              <v-icon icon="mdi-file-check" color="success" size="20" />
              <span class="text-body-2">{{ supplierInvoiceFile.name }}</span>
              <v-spacer />
              <v-btn size="small" variant="tonal" color="info" prepend-icon="mdi-download" @click="downloadSupplierFile(supplierInvoiceFile.name)">Download</v-btn>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">
              No supplier invoice uploaded yet. Ask the procurement team to upload it.
            </v-alert>
          </div>

          <!-- Supplier Bank Info (read-only uploads from PO page) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-bank-outline" size="18" class="mr-1" color="success" />
              Supplier Bank Info
            </div>
            <div v-if="bankInfoFiles.length" class="d-flex flex-column gap-2">
              <div v-for="f in bankInfoFiles" :key="f.name" class="d-flex align-center gap-2 pa-2 rounded file-row">
                <v-icon icon="mdi-file-document-outline" color="success" size="20" />
                <div class="d-flex flex-column">
                  <span class="text-body-2 font-weight-medium">{{ f.name }}</span>
                  <span class="text-caption text-medium-emphasis">{{ new Date(f.modifiedAt).toLocaleString() }}</span>
                </div>
                <v-spacer />
                <v-btn size="small" variant="tonal" color="info" icon="mdi-download" @click="downloadSupplierFile(f.name)" />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">
              No Supplier Bank Info uploaded yet.
            </v-alert>
          </div>

          <v-divider class="my-3" />

          <!-- DP Files (auto-generated when Import Details saved) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-file-certificate-outline" size="18" class="mr-1" color="primary" />
              DasturPardakht (DP)
            </div>
            <div v-if="dpFiles.length" class="d-flex flex-column gap-2">
              <div v-for="f in dpFiles" :key="f.name" class="d-flex align-center gap-2 pa-2 rounded file-row">
                <v-icon icon="mdi-file-pdf-box" color="primary" size="20" />
                <div class="d-flex flex-column">
                  <span class="text-body-2 font-weight-medium">{{ f.name }}</span>
                  <span class="text-caption text-medium-emphasis">{{ new Date(f.modifiedAt).toLocaleString() }}</span>
                </div>
                <v-spacer />
                <v-btn size="small" variant="tonal" color="info" icon="mdi-download" @click="downloadSupplierFile(f.name)" />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">
              No DP generated yet. It will be created automatically when the Import Details are saved on the PO page.
            </v-alert>
          </div>

          <!-- Bank Detail (from PO Import Details) -->
          <div v-if="importDetail && (importDetail.bankName || importDetail.bankAccountNumber)" class="mb-4 pa-3 rounded file-row">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-bank" size="18" class="mr-1" color="success" />
              Supplier Bank Detail
            </div>
            <v-row dense>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Bank Name</div><div>{{ importDetail.bankName || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Account Number</div><div>{{ importDetail.bankAccountNumber || '—' }}</div></v-col>
              <v-col cols="12" md="6"><div class="text-caption text-medium-emphasis">Bank Address</div><div>{{ importDetail.bankAddress || '—' }}</div></v-col>
              <v-col cols="12" md="3"><div class="text-caption text-medium-emphasis">Bank City</div><div>{{ importDetail.bankCity || '—' }}</div></v-col>
              <v-col cols="12" md="3"><div class="text-caption text-medium-emphasis">Bank Country</div><div>{{ importDetail.bankCountry || '—' }}</div></v-col>
            </v-row>
          </div>

          <v-divider class="my-3" />

          <!-- Customer Paid POPs (incoming — what the customer paid us) -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-cash-plus" size="18" class="mr-1" color="success" />
              Customer Paid POPs
              <v-chip v-if="customerPayments.length" size="x-small" color="success" variant="tonal" class="ml-2">
                {{ customerPayments.length }}
              </v-chip>
              <v-spacer />
              <span v-if="customerPayments.length" class="text-caption text-medium-emphasis">
                Paid: <strong class="text-success">${{ formatPrice(customerTotalPaid) }}</strong>
                <span v-if="customerInvoiceTotal != null"> / ${{ formatPrice(customerInvoiceTotal) }}</span>
                <v-chip v-if="customerIsPaid" size="x-small" color="success" variant="flat" class="ml-2">PAID</v-chip>
              </span>
            </div>
            <div v-if="customerPayments.length" class="d-flex flex-column gap-2">
              <div
                v-for="p in customerPayments"
                :key="p.id"
                class="d-flex align-center gap-2 pa-2 rounded file-row"
              >
                <v-icon icon="mdi-file-document-check-outline" color="success" size="20" />
                <div class="d-flex flex-column" style="min-width: 0; flex: 1;">
                  <span class="text-body-2 font-weight-medium text-truncate">{{ p.fileName }}</span>
                  <span class="text-caption text-medium-emphasis">
                    Amount: <strong class="text-success">${{ formatPrice(p.amount) }}</strong>
                    · {{ new Date(p.createdAt).toLocaleString() }}
                    <span v-if="p.notes"> · {{ p.notes }}</span>
                  </span>
                </div>
                <v-btn
                  size="small"
                  variant="tonal"
                  color="success"
                  icon="mdi-download"
                  @click="downloadCustomerPop(p.fileName)"
                />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact" icon="mdi-information-outline">
              No customer payments uploaded yet.
            </v-alert>
          </div>

          <v-divider class="my-3" />

          <!-- POP Upload -->
          <div class="mb-4">
            <div class="text-subtitle-2 mb-2 d-flex align-center">
              <v-icon icon="mdi-cash-check" size="18" class="mr-1" />
              Our POP to Supplier
            </div>
            <div v-for="f in popFiles" :key="f.name" class="d-flex align-center gap-2 mb-2 pa-2 rounded file-row">
              <v-icon :icon="f.name.includes('_final') ? 'mdi-file-star' : 'mdi-file-check'" :color="f.name.includes('_final') ? 'amber-darken-2' : 'success'" size="20" />
              <div class="d-flex flex-column">
                <span class="text-body-2 font-weight-medium">{{ f.name }}</span>
                <span class="text-caption text-medium-emphasis">{{ new Date(f.modifiedAt).toLocaleString() }}</span>
              </div>
              <v-spacer />
              <v-btn size="small" variant="tonal" color="info" icon="mdi-download" @click="downloadSupplierFile(f.name)" />
            </div>

            <div v-if="!isFinalPopUploaded" class="mt-4">
              <v-checkbox
                v-model="isFinalPop"
                label="Is This Final POP?"
                density="compact"
                hide-details
                color="amber-darken-2"
                class="mb-2"
              />
              <v-btn
                variant="tonal"
                color="primary"
                prepend-icon="mdi-upload"
                :loading="uploadingPop"
                :disabled="popFiles.length >= 10"
                @click="popInputRef?.click()"
              >{{ popFiles.length > 0 ? 'Add Another POP' : 'Upload POP File' }}</v-btn>
              <div v-if="popFiles.length >= 10" class="text-caption text-error mt-1">Maximum 10 POPs reached.</div>
            </div>
            <v-alert v-else type="success" variant="tonal" density="compact" icon="mdi-shield-check" class="mt-2">
              Final POP has been uploaded. No further uploads allowed.
            </v-alert>
            <input ref="popInputRef" type="file" class="d-none" @change="onPopSelected" />
          </div>

          <v-divider class="my-3" />

          <!-- Submit -->
          <v-alert
            v-if="selectedPo.paymentStatus === 'Submitted'"
            type="success"
            variant="tonal"
            icon="mdi-check-circle"
          >
            Payment submitted{{ selectedPo.paymentSubmittedAt ? ' at ' + new Date(selectedPo.paymentSubmittedAt).toLocaleString() : '' }}.
          </v-alert>
          <div v-else class="d-flex gap-2">
            <v-btn
              class="flex-grow-1"
              color="error"
              variant="tonal"
              prepend-icon="mdi-close-circle"
              :loading="rejecting"
              @click="showRejectDialog = true"
            >Reject PO</v-btn>
            <v-btn
              class="flex-grow-1"
              color="success"
              prepend-icon="mdi-send-check"
              :disabled="popFiles.length === 0"
              :loading="submitting"
              @click="submitPayment"
            >Submit Payment</v-btn>
          </div>
          <div v-if="popFiles.length === 0 && selectedPo.paymentStatus !== 'Submitted'" class="text-caption text-medium-emphasis mt-2 text-center">
            Upload at least one POP file to enable submit.
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDetail = false">Close</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Reject Dialog -->
    <v-dialog v-model="showRejectDialog" max-width="500">
      <v-card>
        <v-card-title>Reject Purchase Order</v-card-title>
        <v-card-text>
          <div class="mb-4">Are you sure you want to reject PO {{ selectedPo?.poNumber }}? It will be returned to the procurement team for corrections.</div>
          <v-textarea
            v-model="rejectionNote"
            label="Rejection Reason"
            placeholder="Explain why this PO is being rejected..."
            rows="3"
            variant="outlined"
            density="comfortable"
            hide-details
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" :disabled="!rejectionNote.trim()" :loading="rejecting" @click="confirmReject">Confirm Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

const api = useApi()
const config = useRuntimeConfig()
const authStore = useAuthStore()

type FileInfo = { name: string; size: number; modifiedAt: string }
type POItem = any

const loading = ref(false)
const queue = ref<POItem[]>([])
const showDetail = ref(false)
const selectedPo = ref<POItem | null>(null)
const supplierInvoiceFile = ref<FileInfo | null>(null)
const bankInfoFiles = ref<FileInfo[]>([])
const dpFiles = ref<FileInfo[]>([])
const importDetail = ref<any>(null)
const popFiles = ref<FileInfo[]>([])
// Customer paid POPs (uploaded against the proforma invoice — payment user can download them here)
type CustomerPayment = { id: number; fileName: string; amount: number; notes?: string | null; createdAt: string }
const customerPayments = ref<CustomerPayment[]>([])
const customerTotalPaid = ref(0)
const customerInvoiceTotal = ref<number | null>(null)
const customerIsPaid = ref(false)
const isFinalPop = ref(false)
const isFinalPopUploaded = computed(() => popFiles.value.some(f => f.name.includes('_final')))
const uploadingPop = ref(false)
const submitting = ref(false)
const rejecting = ref(false)
const showRejectDialog = ref(false)
const rejectionNote = ref('')
const popInputRef = ref<HTMLInputElement | null>(null)

const pendingCount = computed(() => queue.value.filter(p => p.paymentStatus !== 'Submitted').length)
const submittedCount = computed(() => queue.value.filter(p => p.paymentStatus === 'Submitted').length)

const headers = [
  { title: 'PO Number', key: 'poNumber' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Proforma Invoice', key: 'invoiceNumber' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Approved', key: 'adminApprovalAt' },
  { title: 'Status', key: 'paymentStatus' },
  { title: '', key: 'actions', sortable: false, width: 120 },
]

async function loadQueue() {
  loading.value = true
  try {
    queue.value = await api.get<any[]>('/purchase-orders/payment-queue')
  } catch { showSnack('Failed to load payment queue', 'error') }
  finally { loading.value = false }
}

function onRowClick(_: any, row: any) {
  openPo(row.item)
}

async function openPo(po: POItem) {
  selectedPo.value = po
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
    // Customer paid POPs are tied to the proforma invoice, not the supplier
    try {
      const cp = await api.get<any>(`/documents/proforma-invoice/${po.invoiceId}/customer-payments`)
      customerPayments.value = cp.payments ?? []
      customerTotalPaid.value = cp.totalPaid ?? 0
      customerInvoiceTotal.value = cp.invoiceTotal ?? null
      customerIsPaid.value = cp.isPaid === true
    } catch { /* silent — section just shows empty alert */ }
  }
  if (po.invoiceId && po.supplierId) {
    try {
      const data = await api.get<any>(`/documents/proforma-invoice/${po.invoiceId}`)
      const supplierSection = (data.suppliers || []).find((s: any) => s.supplierId === po.supplierId)
      if (supplierSection) {
        const files: FileInfo[] = supplierSection.files || []
        supplierInvoiceFile.value = files.find((f: FileInfo) =>
          f.name.startsWith('Supplier Invoice') ||
          f.name.startsWith('supplier_invoice.') ||
          f.name.startsWith('supplier_invoice_')
        ) || null
        bankInfoFiles.value = files.filter((f: FileInfo) =>
          f.name.startsWith('Supplier Bank Info') || f.name.startsWith('supplier_bank_info')
        )
        dpFiles.value = files.filter((f: FileInfo) =>
          f.name.startsWith('DP') || f.name.startsWith('dp ')
        )
        popFiles.value = files.filter((f: FileInfo) =>
          f.name.startsWith('Our POP number')
        ).sort((a, b) => {
          const nA = parseInt(a.name.match(/\d+/)?. [0] || '0')
          const nB = parseInt(b.name.match(/\d+/)?. [0] || '0')
          return nA - nB
        })
      }
    } catch {}
    // Load the PO's import detail (supplier bank info captured on the PO page)
    try {
      importDetail.value = await api.get(`/purchase-orders/${po.id}/import-detail`)
    } catch {}
  }
}

// Customer paid POP download — files live under the invoice's "Customer POP" category folder.
async function downloadCustomerPop(name: string) {
  if (!selectedPo.value?.invoiceId) return
  try {
    const blob = await $fetch<Blob>(
      `${config.public.apiBase}/documents/proforma-invoice/${selectedPo.value.invoiceId}/file`,
      {
        method: 'GET',
        query: { name, category: 'customer_pop' },
        responseType: 'blob',
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      }
    )
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', name)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch { showSnack('Download failed', 'error') }
}

async function downloadSupplierFile(name: string) {
  if (!selectedPo.value) return
  try {
    const blob = await $fetch<Blob>(
      `${config.public.apiBase}/documents/proforma-invoice/${selectedPo.value.invoiceId}/supplier/${selectedPo.value.supplierId}/file`,
      {
        method: 'GET',
        query: { name },
        responseType: 'blob',
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      }
    )
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', name)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch { showSnack('Download failed', 'error') }
}

async function onPopSelected(e: Event) {
  const target = e.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file || !selectedPo.value) return
  uploadingPop.value = true
  try {
    const form = new FormData()
    form.append('file', file)
    form.append('category', 'our_pop')
    form.append('isFinal', isFinalPop.value.toString())
    await $fetch(
      `${config.public.apiBase}/documents/proforma-invoice/${selectedPo.value.invoiceId}/supplier/${selectedPo.value.supplierId}/upload`,
      {
        method: 'POST',
        body: form,
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      }
    )
    showSnack(isFinalPop.value ? 'Final POP uploaded' : 'POP added', 'success')
    isFinalPop.value = false
    await openPo(selectedPo.value)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Upload failed', 'error')
  }
  finally {
    uploadingPop.value = false
    if (target) target.value = ''
  }
}

async function submitPayment() {
  if (!selectedPo.value) return
  submitting.value = true
  try {
    // Implicitly set payment approval to Accepted when submitting
    await api.patch(`/purchase-orders/${selectedPo.value.id}/payment-approval`, { decision: 'Accepted' })
    const r = await api.patch<any>(`/purchase-orders/${selectedPo.value.id}/submit-payment`, {})
    selectedPo.value.paymentStatus = 'Submitted'
    selectedPo.value.paymentSubmittedAt = r?.paymentSubmittedAt || new Date().toISOString()
    showSnack('Payment submitted', 'success')
    await loadQueue()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Submit failed', 'error')
  } finally { submitting.value = false }
}

async function confirmReject() {
  if (!selectedPo.value || !rejectionNote.value.trim()) return
  rejecting.value = true
  try {
    await api.patch(`/purchase-orders/${selectedPo.value.id}/payment-approval`, {
      decision: 'Rejected',
      note: rejectionNote.value.trim()
    })
    showSnack('PO rejected and returned to procurement', 'warning')
    showRejectDialog.value = false
    rejectionNote.value = ''
    showDetail.value = false
    await loadQueue()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Rejection failed', 'error')
  } finally { rejecting.value = false }
}

// ── Snackbar ──
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(t: string, c: string) { snackbarText.value = t; snackbarColor.value = c; snackbar.value = true }

onMounted(loadQueue)
</script>

<style scoped>
/* Theme-aware file row — adapts to both light and dark themes. Replaces the old
   bg-grey-lighten-4 which was invisible/off in dark mode. */
.file-row {
  background-color: rgba(var(--v-theme-on-surface), 0.06);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}
</style>
