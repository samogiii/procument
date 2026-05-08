<template>
  <v-card class="glass-card mb-6">
    <v-card-title class="d-flex align-center">
      <v-icon icon="mdi-folder-multiple-outline" class="mr-2" size="20" />
      Documents
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="loadDocuments">Refresh</v-btn>
    </v-card-title>
    <v-card-text>
      <!-- PI-level documents -->
      <div class="mb-4">
        <div class="text-subtitle-2 mb-2 d-flex align-center">
          <v-icon icon="mdi-file-document-outline" size="18" class="mr-1" />
          SaleS Order Documents
        </div>
        <v-row dense>
          <v-col v-for="cat in piCategories" :key="cat.key" cols="12" sm="6" md="3">
            <v-card variant="outlined" class="pa-3 h-100 d-flex flex-column">
              <div class="d-flex align-center mb-2">
                <v-icon :icon="cat.icon" size="18" class="mr-1" :color="cat.color" />
                <span class="text-body-2 font-weight-medium">{{ cat.label }}</span>
                <v-spacer />
                <!-- Customer POP opens amount dialog; all others use file input directly -->
                <v-btn
                  size="x-small"
                  variant="tonal"
                  color="primary"
                  icon="mdi-plus"
                  @click="cat.key === 'customer_pop' ? openPopDialog() : triggerUpload(cat.key, null)"
                  :loading="uploading === cat.key"
                  title="Upload files"
                />
              </div>

              <!-- Payment progress bar for Customer POP -->
              <div v-if="cat.key === 'customer_pop' && paymentInfo" class="mb-2">
                <div class="d-flex align-center mb-1">
                  <span class="text-caption text-medium-emphasis">Paid: ${{ formatAmount(paymentInfo.totalPaid) }} / ${{ formatAmount(paymentInfo.invoiceTotal) }}</span>
                  <v-spacer />
                  <v-chip v-if="paymentInfo.isPaid" size="x-small" color="success" prepend-icon="mdi-check-circle">Paid</v-chip>
                </div>
                <v-progress-linear
                  :model-value="paymentProgress"
                  color="success"
                  bg-color="surface-variant"
                  rounded
                  height="4"
                />
              </div>

              <div class="flex-grow-1 overflow-y-auto" style="max-height: 150px;">
                <div v-if="piFilesByCategory(cat.key).length === 0" class="text-caption text-medium-emphasis italic pa-2">
                  No files uploaded.
                </div>
                <div v-for="f in piFilesByCategory(cat.key)" :key="f.name" class="d-flex align-center mb-1 pa-1 rounded hover-bg">
                  <v-icon icon="mdi-file-check" color="success" size="14" class="mr-1" />
                  <div class="flex-grow-1 min-width-0">
                    <span class="text-caption text-truncate d-block" :title="f.name">{{ f.name }}</span>
                    <!-- Show amount for Customer POP files -->
                    <span v-if="cat.key === 'customer_pop' && getPaymentForFile(f.name)" class="text-caption text-success font-weight-medium">
                      ${{ formatAmount(getPaymentForFile(f.name)!.amount) }}
                    </span>
                  </div>
                  <v-btn size="x-small" variant="text" color="info" icon="mdi-download" @click="downloadPI(f)" />
                  <v-btn size="x-small" variant="text" color="error" icon="mdi-delete" @click="deletePI(f)" />
                </div>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </div>

      <!-- Supplier-level documents -->
      <v-divider class="my-4" />
      <div class="text-subtitle-2 mb-2 d-flex align-center">
        <v-icon icon="mdi-truck-outline" size="18" class="mr-1" />
        Supplier Documents
        <v-chip v-if="suppliers.length" size="x-small" class="ml-2">{{ suppliers.length }}</v-chip>
      </div>

      <div v-if="!suppliers.length" class="text-caption text-medium-emphasis">
        No suppliers yet. Suppliers appear after POs are created for this Sales Order.
      </div>

      <v-expansion-panels v-else multiple variant="accordion">
        <v-expansion-panel v-for="s in suppliers" :key="s.supplierId">
          <v-expansion-panel-title>
            <div class="d-flex align-center">
              <v-icon icon="mdi-account-tie" size="18" class="mr-2" />
              <span class="font-weight-medium">{{ s.supplierName }}</span>
              <v-chip size="x-small" class="ml-2">{{ s.files.length }} files</v-chip>
            </div>
          </v-expansion-panel-title>
          <v-expansion-panel-text>
            <v-row dense>
              <v-col v-for="cat in supplierCategories" :key="cat.key" cols="12" md="3">
                <v-card variant="outlined" class="pa-3 h-100 d-flex flex-column">
                  <div class="d-flex align-center mb-2">
                    <v-icon :icon="cat.icon" size="18" class="mr-1" :color="cat.color" />
                    <span class="text-body-2 font-weight-medium">{{ cat.label }}</span>
                    <v-spacer />
                    <v-btn
                      size="x-small"
                      variant="tonal"
                      color="primary"
                      icon="mdi-plus"
                      @click="triggerUpload(cat.key, s.supplierId)"
                      :loading="uploading === `${cat.key}-${s.supplierId}`"
                      title="Upload files"
                    />
                  </div>

                  <div class="flex-grow-1 overflow-y-auto" style="max-height: 150px;">
                    <div v-if="supplierFilesByCategory(s, cat.key).length === 0" class="text-caption text-medium-emphasis italic pa-2">
                      No files uploaded.
                    </div>
                    <div v-for="f in supplierFilesByCategory(s, cat.key)" :key="f.name" class="d-flex align-center mb-1 pa-1 rounded hover-bg">
                      <v-icon icon="mdi-file-check" color="success" size="14" class="mr-1" />
                      <span class="text-caption text-truncate flex-grow-1" :title="f.name">{{ f.name }}</span>
                      <v-btn size="x-small" variant="text" color="info" icon="mdi-download" @click="downloadSupplier(s.supplierId, f)" />
                      <v-btn size="x-small" variant="text" color="error" icon="mdi-delete" @click="deleteSupplier(s.supplierId, f)" />
                    </div>
                  </div>
                </v-card>
              </v-col>
            </v-row>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
    </v-card-text>

    <!-- Hidden file input for non-POP categories -->
    <input
      ref="fileInputRef"
      type="file"
      multiple
      class="d-none"
      @change="onFileSelected"
    />

    <!-- Customer POP: Amount Dialog -->
    <v-dialog v-model="showPopDialog" max-width="460" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-cash-plus" class="mr-2" color="success" />
          Upload Customer POP
        </v-card-title>
        <v-card-text class="pa-4">
          <v-text-field
            v-model.number="popAmount"
            label="Payment Amount"
            type="number"
            min="0"
            step="0.01"
            prefix="$"
            variant="outlined"
            density="comfortable"
            :rules="[v => v > 0 || 'Amount must be greater than 0']"
            class="mb-3"
            autofocus
          />
          <v-text-field
            v-model="popNotes"
            label="Notes (optional)"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <div class="d-flex align-center gap-3 pa-3 rounded border" style="cursor:pointer" @click="popFileInputRef?.click()">
            <v-icon icon="mdi-paperclip" color="primary" />
            <span class="text-body-2 flex-grow-1">{{ popFile ? popFile.name : 'Click to attach POP file…' }}</span>
            <v-btn size="x-small" variant="tonal" color="primary">Browse</v-btn>
          </div>
          <input ref="popFileInputRef" type="file" class="d-none" @change="onPopFileChosen" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="cancelPopDialog">Cancel</v-btn>
          <v-btn
            color="success"
            variant="flat"
            prepend-icon="mdi-upload"
            :loading="uploadingPop"
            :disabled="!popFile || !popAmount || popAmount <= 0"
            @click="submitPopUpload"
          >Upload</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000">
      {{ snackbarText }}
    </v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

const props = defineProps<{ invoiceId: number | string }>()

const config = useRuntimeConfig()
const authStore = useAuthStore()
const api = useApi()

type FileInfo = { name: string; category: string; size: number; modifiedAt: string }
type SupplierSection = { supplierId: number; supplierName: string; files: FileInfo[] }
type PaymentRecord = { id: number; fileName: string; amount: number; notes?: string; createdAt: string }
type PaymentInfo = { payments: PaymentRecord[]; totalPaid: number; invoiceTotal: number; isPaid: boolean }

const loading = ref(false)
const uploading = ref<string | null>(null)
const piFiles = ref<FileInfo[]>([])
const suppliers = ref<SupplierSection[]>([])
const paymentInfo = ref<PaymentInfo | null>(null)

const piCategories = [
  { key: 'customer_pop', label: 'Customer POP', icon: 'mdi-receipt-text-check', color: 'success' },
  { key: 'customer_po', label: 'Customer PO', icon: 'mdi-file-document', color: 'primary' },
  { key: 'our_pi', label: 'Our PI', icon: 'mdi-file-pdf-box', color: 'error' },
  { key: 'quote', label: 'Quote', icon: 'mdi-file-sign', color: 'warning' },
]

const supplierCategories = [
  // PO is listed first so the auto-saved PO PDF for each supplier is always visible at the top.
  { key: 'po', label: 'PO', icon: 'mdi-file-document-multiple-outline', color: 'primary' },
  { key: 'supplier_invoice', label: 'Supplier Invoice', icon: 'mdi-invoice-text', color: 'primary' },
  { key: 'supplier_bank_info', label: 'Supplier Bank Info', icon: 'mdi-bank-outline', color: 'info' },
  { key: 'our_pop', label: 'Our POP to Supplier', icon: 'mdi-cash-check', color: 'success' },
  { key: 'dp', label: 'DP', icon: 'mdi-file-export-outline', color: 'warning' },
]

// ── Regular upload state ──
const fileInputRef = ref<HTMLInputElement | null>(null)
const pendingCategory = ref<string | null>(null)
const pendingSupplierId = ref<number | null>(null)

// ── Customer POP dialog state ──
const showPopDialog = ref(false)
const popAmount = ref<number | null>(null)
const popNotes = ref('')
const popFile = ref<File | null>(null)
const popFileInputRef = ref<HTMLInputElement | null>(null)
const uploadingPop = ref(false)

const paymentProgress = computed(() => {
  if (!paymentInfo.value || paymentInfo.value.invoiceTotal <= 0) return 0
  return Math.min(100, (paymentInfo.value.totalPaid / paymentInfo.value.invoiceTotal) * 100)
})

function getPaymentForFile(fileName: string): PaymentRecord | undefined {
  return paymentInfo.value?.payments.find(p => p.fileName === fileName)
}

function formatAmount(val: number) {
  return val.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function piFilesByCategory(category: string): FileInfo[] {
  return piFiles.value.filter(f => f.category === category)
}

function supplierFilesByCategory(s: SupplierSection, category: string): FileInfo[] {
  return s.files.filter(f => f.category === category)
}

async function loadDocuments() {
  loading.value = true
  try {
    const [docsData, paymentsData] = await Promise.all([
      api.get<any>(`/documents/proforma-invoice/${props.invoiceId}`),
      api.get<PaymentInfo>(`/documents/proforma-invoice/${props.invoiceId}/customer-payments`).catch(() => null),
    ])
    piFiles.value = docsData.piFiles || []
    suppliers.value = docsData.suppliers || []
    paymentInfo.value = paymentsData
  } catch (e) {
    showSnack('Failed to load documents', 'error')
  } finally {
    loading.value = false
  }
}

// ── Customer POP dialog ──

function openPopDialog() {
  popAmount.value = null
  popNotes.value = ''
  popFile.value = null
  showPopDialog.value = true
}

function cancelPopDialog() {
  showPopDialog.value = false
  if (popFileInputRef.value) popFileInputRef.value.value = ''
}

function onPopFileChosen(e: Event) {
  const target = e.target as HTMLInputElement
  popFile.value = target.files?.[0] ?? null
}

async function submitPopUpload() {
  if (!popFile.value || !popAmount.value || popAmount.value <= 0) return

  uploadingPop.value = true
  try {
    const formData = new FormData()
    formData.append('file', popFile.value)
    formData.append('amount', String(popAmount.value))
    if (popNotes.value.trim()) formData.append('notes', popNotes.value.trim())

    const result = await $fetch<any>(
      `${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/customer-pop`,
      {
        method: 'POST',
        body: formData,
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      }
    )

    showPopDialog.value = false
    if (result.justPaid) {
      showSnack('Payment uploaded — Invoice is now marked as PAID!', 'success')
    } else {
      const remaining = result.invoiceTotal - result.totalPaid
      showSnack(
        `Uploaded $${formatAmount(result.amount)}. Total paid: $${formatAmount(result.totalPaid)} / $${formatAmount(result.invoiceTotal)}` +
        (remaining > 0 ? ` ($${formatAmount(remaining)} remaining)` : ''),
        'success'
      )
    }
    await loadDocuments()
  } catch (err: any) {
    showSnack(err?.data?.message || 'Upload failed', 'error')
  } finally {
    uploadingPop.value = false
    if (popFileInputRef.value) popFileInputRef.value.value = ''
  }
}

// ── Regular upload (non-POP categories) ──

function triggerUpload(category: string, supplierId: number | null) {
  pendingCategory.value = category
  pendingSupplierId.value = supplierId
  fileInputRef.value?.click()
}

async function onFileSelected(e: Event) {
  const target = e.target as HTMLInputElement
  const files = Array.from(target.files || [])
  if (!files.length || !pendingCategory.value) return

  const category = pendingCategory.value
  const supplierId = pendingSupplierId.value
  uploading.value = supplierId != null ? `${category}-${supplierId}` : category

  const url = supplierId != null
    ? `/documents/proforma-invoice/${props.invoiceId}/supplier/${supplierId}/upload`
    : `/documents/proforma-invoice/${props.invoiceId}/upload`

  try {
    for (const file of files) {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('category', category)
      await $fetch(`${config.public.apiBase}${url}`, {
        method: 'POST',
        body: formData,
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
    }
    showSnack(files.length === 1 ? 'File uploaded successfully' : `${files.length} files uploaded successfully`, 'success')
    await loadDocuments()
  } catch (err: any) {
    showSnack(err?.data?.message || 'Upload failed', 'error')
  } finally {
    uploading.value = null
    pendingCategory.value = null
    pendingSupplierId.value = null
    if (target) target.value = ''
  }
}

// ── Download / Delete ──

async function downloadPI(f: FileInfo) {
  try {
    const blob = await $fetch<Blob>(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/file`, {
      method: 'GET',
      query: { name: f.name, category: f.category },
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    saveBlob(blob, f.name)
  } catch { showSnack('Download failed', 'error') }
}

async function downloadSupplier(supplierId: number, f: FileInfo) {
  try {
    const blob = await $fetch<Blob>(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/supplier/${supplierId}/file`, {
      method: 'GET',
      query: { name: f.name, category: f.category },
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    saveBlob(blob, f.name)
  } catch { showSnack('Download failed', 'error') }
}

async function deletePI(f: FileInfo) {
  if (!confirm(`Delete "${f.name}"?`)) return
  try {
    await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/file`, {
      method: 'DELETE',
      query: { name: f.name, category: f.category },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Deleted', 'success')
    await loadDocuments()
  } catch { showSnack('Delete failed', 'error') }
}

async function deleteSupplier(supplierId: number, f: FileInfo) {
  if (!confirm(`Delete "${f.name}"?`)) return
  try {
    await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/supplier/${supplierId}/file`, {
      method: 'DELETE',
      query: { name: f.name, category: f.category },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Deleted', 'success')
    await loadDocuments()
  } catch { showSnack('Delete failed', 'error') }
}

function saveBlob(blob: Blob, filename: string) {
  const url = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.setAttribute('download', filename)
  document.body.appendChild(link)
  link.click()
  link.parentNode?.removeChild(link)
  window.URL.revokeObjectURL(url)
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

onMounted(loadDocuments)
defineExpose({ loadDocuments })
</script>

<style scoped>
.hover-bg:hover {
  background: rgba(var(--v-theme-surface-variant), 0.1);
}
</style>
