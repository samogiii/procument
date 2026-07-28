<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="close" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Send Quote Email — {{ quote?.quoteNumber }}</v-toolbar-title>
        <v-spacer />
        <v-btn color="primary" variant="flat" prepend-icon="mdi-send" :loading="sending" :disabled="!canSend" @click="sendEmail">Send</v-btn>
      </v-toolbar>

      <div class="d-flex flex-grow-1" style="overflow:hidden; min-height:0;">
        <!-- Column 1: Email fields -->
        <div class="pa-4" style="width:26%; border-right:1px solid rgba(var(--v-border-color),0.15); overflow-y:auto;">
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">EMAIL DETAILS</div>
          <v-text-field
            v-model="subject"
            label="Subject"
            variant="outlined"
            density="compact"
            class="mb-3"
            hide-details
          />
          <v-combobox
            v-model="toEmail"
            :items="contactOptions"
            item-title="label"
            item-value="email"
            label="To"
            variant="outlined"
            density="compact"
            class="mb-3"
            hide-details
            prepend-inner-icon="mdi-account-outline"
            @update:model-value="onToChange"
          />
          <v-text-field
            v-model="toName"
            label="Recipient Name"
            variant="outlined"
            density="compact"
            class="mb-3"
            hide-details
          />
          <v-textarea
            v-model="body"
            label="Body"
            variant="outlined"
            density="compact"
            rows="10"
            auto-grow
            hide-details
          />
        </div>

        <!-- Column 2: Attachments -->
        <div class="pa-4" style="width:26%; border-right:1px solid rgba(var(--v-border-color),0.15); overflow-y:auto;">
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">PDF ATTACHMENT</div>
          <v-btn-toggle v-model="pdfMode" mandatory color="primary" density="compact" class="mb-3 d-flex">
            <v-btn value="auto" size="small" class="flex-grow-1">Auto</v-btn>
            <v-btn value="upload" size="small" class="flex-grow-1">Upload</v-btn>
            <v-btn value="none" size="small" class="flex-grow-1">None</v-btn>
          </v-btn-toggle>

          <div v-if="pdfMode === 'auto'" class="mb-4">
            <div v-if="generatingPdf" class="d-flex align-center gap-2 text-body-2 text-medium-emphasis">
              <v-progress-circular indeterminate size="18" width="2" />
              Generating…
            </div>
            <div v-else-if="generatedPdfBlob" class="d-flex align-center gap-2 text-body-2">
              <v-icon icon="mdi-file-pdf-box" color="error" />
              {{ generatedPdfFileName }}
              <v-chip size="x-small" variant="tonal">{{ formatSize(generatedPdfBlob.size) }}</v-chip>
            </div>
            <div v-else class="text-body-2 text-error">
              Failed to generate. <a href="#" @click.prevent="generatePdf">Retry</a>
            </div>
          </div>
          <v-file-input
            v-else-if="pdfMode === 'upload'"
            v-model="uploadedPdfFile"
            label="Upload PDF"
            variant="outlined"
            density="compact"
            hide-details
            accept="application/pdf"
            prepend-icon=""
            prepend-inner-icon="mdi-paperclip"
            class="mb-4"
          />

          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">EXCEL ATTACHMENT</div>
          <v-btn-toggle v-model="excelMode" mandatory color="primary" density="compact" class="mb-3 d-flex">
            <v-btn value="auto" size="small" class="flex-grow-1">Auto</v-btn>
            <v-btn value="upload" size="small" class="flex-grow-1">Upload</v-btn>
            <v-btn value="none" size="small" class="flex-grow-1">None</v-btn>
          </v-btn-toggle>

          <div v-if="excelMode === 'auto'">
            <div v-if="generatingExcel" class="d-flex align-center gap-2 text-body-2 text-medium-emphasis">
              <v-progress-circular indeterminate size="18" width="2" />
              Generating…
            </div>
            <div v-else-if="generatedExcelBlob" class="d-flex align-center gap-2 text-body-2">
              <v-icon icon="mdi-file-excel-box" color="success" />
              {{ generatedExcelFileName }}
              <v-chip size="x-small" variant="tonal">{{ formatSize(generatedExcelBlob.size) }}</v-chip>
            </div>
            <div v-else class="text-body-2 text-error">
              Failed to generate. <a href="#" @click.prevent="generateExcel">Retry</a>
            </div>
          </div>
          <v-file-input
            v-else-if="excelMode === 'upload'"
            v-model="uploadedExcelFile"
            label="Upload Excel"
            variant="outlined"
            density="compact"
            hide-details
            accept=".xlsx,.xls"
            prepend-icon=""
            prepend-inner-icon="mdi-paperclip"
          />

          <v-divider class="my-4" />

          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">ADDITIONAL FILES</div>
          <v-file-input
            v-model="pickedExtraFiles"
            label="Add files"
            variant="outlined"
            density="compact"
            hide-details
            multiple
            prepend-icon=""
            prepend-inner-icon="mdi-paperclip-plus"
            class="mb-3"
          />
          <div v-if="extraFiles.length" class="d-flex flex-column gap-1">
            <div
              v-for="(file, i) in extraFiles"
              :key="`${file.name}-${i}`"
              class="d-flex align-center gap-2 text-body-2"
            >
              <v-icon :icon="fileIcon(file.name)" size="18" />
              <span class="text-truncate flex-grow-1">{{ file.name }}</span>
              <v-chip size="x-small" variant="tonal">{{ formatSize(file.size) }}</v-chip>
              <v-btn icon="mdi-close" size="x-small" variant="text" @click="removeExtraFile(i)" />
            </div>
            <div class="text-caption text-medium-emphasis mt-1">
              Total attached: {{ formatSize(totalAttachmentSize) }}
            </div>
          </div>
          <div v-if="oversized" class="text-caption text-error mt-1">
            Attachments exceed {{ formatSize(MAX_TOTAL_BYTES) }} — remove some files before sending.
          </div>
        </div>

        <!-- Column 3: Live preview -->
        <div class="d-flex flex-column" style="width:48%; overflow:hidden;">
          <v-tabs v-model="previewTab" density="compact" color="primary">
            <v-tab value="email">Email</v-tab>
            <v-tab value="pdf">PDF</v-tab>
            <v-tab value="excel">Excel</v-tab>
          </v-tabs>
          <v-divider />

          <div v-show="previewTab === 'email'" class="pa-4" style="overflow-y:auto;">
            <v-card variant="outlined" class="pa-4">
              <div class="mb-2"><span class="font-weight-bold">Subject:</span> {{ subject || '—' }}</div>
              <div class="mb-2"><span class="font-weight-bold">To:</span> {{ toName ? `${toName} <${toEmail}>` : (toEmail || '—') }}</div>
              <v-divider class="my-3" />
              <div style="white-space: pre-wrap;" class="text-body-2">{{ body || '—' }}</div>
              <v-divider class="my-3" />
              <div class="d-flex flex-column gap-1 text-body-2">
                <div class="d-flex align-center gap-2">
                  <v-icon icon="mdi-paperclip" size="16" />
                  <span v-if="pdfMode !== 'none'">{{ pdfFileNameForDisplay || 'No PDF selected' }}</span>
                  <span v-else class="text-medium-emphasis">No PDF attached</span>
                </div>
                <div class="d-flex align-center gap-2">
                  <v-icon icon="mdi-paperclip" size="16" />
                  <span v-if="excelMode !== 'none'">{{ excelFileNameForDisplay || 'No Excel selected' }}</span>
                  <span v-else class="text-medium-emphasis">No Excel attached</span>
                </div>
                <div v-for="(file, i) in extraFiles" :key="`preview-${file.name}-${i}`" class="d-flex align-center gap-2">
                  <v-icon icon="mdi-paperclip" size="16" />
                  <span>{{ file.name }}</span>
                </div>
              </div>
            </v-card>
          </div>

          <div v-show="previewTab === 'pdf'" class="flex-grow-1" style="min-height:0;">
            <iframe v-if="pdfPreviewUrl" :src="pdfPreviewUrl" style="width:100%; height:100%; border:none;" />
            <div v-else class="pa-4 text-body-2 text-medium-emphasis">No PDF to preview.</div>
          </div>

          <div v-show="previewTab === 'excel'" class="pa-4" style="overflow:auto;">
            <div v-if="excelPreviewHtml" class="excel-preview" v-html="excelPreviewHtml" />
            <div v-else class="text-body-2 text-medium-emphasis">No Excel to preview.</div>
          </div>
        </div>
      </div>

      <v-snackbar v-model="snackbar" color="error" :timeout="4000" location="bottom end">
        {{ snackbarText }}
      </v-snackbar>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { buildQuotePdfPayload, fetchQuotePdfBlob, buildQuotePdfFileName } from '~/composables/useQuotePdfPayload'
import { buildQuoteExcelBlob, buildQuoteExcelPreviewHtml, buildQuoteExcelFileName } from '~/composables/useQuoteExcelPayload'

const props = defineProps<{ quote: any, preset: any }>()
const model = defineModel<boolean>({ default: false })
const emit = defineEmits<{ (e: 'sent'): void, (e: 'sent-folder-warning', message: string): void }>()

const api = useApi()
const authStore = useAuthStore()

const subject = ref('')
const toEmail = ref('')
const toName = ref('')
const body = ref('')
const sending = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const previewTab = ref<'email' | 'pdf' | 'excel'>('email')

type AttachmentMode = 'auto' | 'upload' | 'none'
const pdfMode = ref<AttachmentMode>('auto')
const excelMode = ref<AttachmentMode>('none')
const generatingPdf = ref(false)
const generatingExcel = ref(false)
const generatedPdfBlob = ref<Blob | null>(null)
const generatedExcelBlob = ref<Blob | null>(null)
const generatedPdfFileName = ref('')
const generatedExcelFileName = ref('')
const uploadedPdfFile = ref<File | null>(null)
const uploadedExcelFile = ref<File | null>(null)
const excelPreviewHtml = ref('')

// Free-form extra attachments. `pickedExtraFiles` is only the file-input buffer —
// it drains into `extraFiles` so the user can add files in several batches.
const extraFiles = ref<File[]>([])
const pickedExtraFiles = ref<File[]>([])

// Backend caps the request at 20 MB; keep headroom for the multipart envelope.
const MAX_TOTAL_BYTES = 18 * 1024 * 1024

interface ContactOption { label: string; email: string; name?: string }

const contactOptions = computed<ContactOption[]>(() => {
  const contacts = parseContacts(props.quote?.customerContacts)
  if (contacts.length) {
    return contacts.map(c => ({ label: `${c.name} <${c.email}>`, email: c.email, name: c.name }))
  }
  if (props.quote?.customerEmail) {
    return [{ label: props.quote.customerEmail, email: props.quote.customerEmail }]
  }
  return []
})

function parseContacts(raw?: string): { name: string; email: string }[] {
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed.filter((c: any) => c?.email) : []
  } catch {
    return []
  }
}

function onToChange(val: any) {
  if (val && typeof val === 'object') {
    toEmail.value = val.email
    toName.value = val.name || toName.value
  } else if (typeof val === 'string') {
    toEmail.value = val
  }
}

const pdfFileNameForDisplay = computed(() =>
  pdfMode.value === 'upload' ? uploadedPdfFile.value?.name : generatedPdfFileName.value)
const excelFileNameForDisplay = computed(() =>
  excelMode.value === 'upload' ? uploadedExcelFile.value?.name : generatedExcelFileName.value)

const pdfReady = computed(() => {
  if (pdfMode.value === 'none') return true
  if (pdfMode.value === 'upload') return !!uploadedPdfFile.value
  return !!generatedPdfBlob.value && !generatingPdf.value
})
const excelReady = computed(() => {
  if (excelMode.value === 'none') return true
  if (excelMode.value === 'upload') return !!uploadedExcelFile.value
  return !!generatedExcelBlob.value && !generatingExcel.value
})
const hasAnyAttachment = computed(() =>
  pdfMode.value !== 'none' || excelMode.value !== 'none' || extraFiles.value.length > 0)

const totalAttachmentSize = computed(() => {
  let total = extraFiles.value.reduce((sum, f) => sum + f.size, 0)
  if (pdfMode.value === 'upload') total += uploadedPdfFile.value?.size || 0
  else if (pdfMode.value === 'auto') total += generatedPdfBlob.value?.size || 0
  if (excelMode.value === 'upload') total += uploadedExcelFile.value?.size || 0
  else if (excelMode.value === 'auto') total += generatedExcelBlob.value?.size || 0
  return total
})
const oversized = computed(() => totalAttachmentSize.value > MAX_TOTAL_BYTES)

const canSend = computed(() => {
  if (!toEmail.value || !subject.value) return false
  if (!hasAnyAttachment.value) return false
  if (oversized.value) return false
  return pdfReady.value && excelReady.value
})

function fileIcon(name: string) {
  const ext = name.slice(name.lastIndexOf('.')).toLowerCase()
  if (ext === '.pdf') return 'mdi-file-pdf-box'
  if (ext === '.xlsx' || ext === '.xls' || ext === '.csv') return 'mdi-file-excel-box'
  if (ext === '.doc' || ext === '.docx') return 'mdi-file-word-box'
  if (['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp'].includes(ext)) return 'mdi-file-image'
  if (['.zip', '.rar', '.7z'].includes(ext)) return 'mdi-folder-zip'
  return 'mdi-file-outline'
}

function removeExtraFile(index: number) {
  extraFiles.value.splice(index, 1)
}

watch(pickedExtraFiles, (picked) => {
  if (!picked?.length) return
  for (const file of picked) {
    const duplicate = extraFiles.value.some(f => f.name === file.name && f.size === file.size)
    if (!duplicate) extraFiles.value.push(file)
  }
  pickedExtraFiles.value = []
})

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// ── PDF preview (object URL, revoked on change) ──
const pdfPreviewUrl = ref<string | null>(null)
function setPdfPreview(blob: Blob | null) {
  if (pdfPreviewUrl.value) URL.revokeObjectURL(pdfPreviewUrl.value)
  pdfPreviewUrl.value = blob ? URL.createObjectURL(blob) : null
}

async function generatePdf() {
  generatingPdf.value = true
  generatedPdfBlob.value = null
  try {
    const payload = buildQuotePdfPayload(props.quote, props.preset)
    const blob = await fetchQuotePdfBlob(payload)
    generatedPdfBlob.value = blob
    generatedPdfFileName.value = buildQuotePdfFileName(props.quote)
    setPdfPreview(blob)
  } catch {
    generatedPdfBlob.value = null
    setPdfPreview(null)
  } finally {
    generatingPdf.value = false
  }
}

function generateExcel() {
  generatingExcel.value = true
  try {
    const blob = buildQuoteExcelBlob(props.quote, props.preset)
    generatedExcelBlob.value = blob
    generatedExcelFileName.value = buildQuoteExcelFileName(props.quote)
    excelPreviewHtml.value = buildQuoteExcelPreviewHtml(props.quote, props.preset)
  } catch {
    generatedExcelBlob.value = null
    excelPreviewHtml.value = ''
  } finally {
    generatingExcel.value = false
  }
}

watch(pdfMode, (mode) => {
  if (mode === 'auto' && !generatedPdfBlob.value) generatePdf()
  if (mode === 'upload') setPdfPreview(uploadedPdfFile.value)
  if (mode === 'none') setPdfPreview(null)
})
watch(uploadedPdfFile, (file) => {
  if (pdfMode.value === 'upload') setPdfPreview(file)
})
watch(excelMode, (mode) => {
  if (mode === 'auto' && !generatedExcelBlob.value) generateExcel()
  if (mode === 'none') excelPreviewHtml.value = ''
})
watch(uploadedExcelFile, () => {
  // Uploaded spreadsheets aren't parsed client-side for preview — only auto-generated ones render inline.
  if (excelMode.value === 'upload') excelPreviewHtml.value = ''
})

function buildSignature(): string {
  const preset = props.preset
  const lines = [
    'Best regards,',
    preset?.smtpFromDisplayName || preset?.name,
    preset?.phone,
    preset?.smtpFromEmail || preset?.email,
    preset?.website,
  ].filter((l): l is string => !!l)
  return lines.join('\n')
}

function resetForm() {
  subject.value = `${props.quote?.quoteNumber || ''} Quotation For RFQ ${props.quote?.rfqName || ''}`
  const defaultContact = contactOptions.value[0]
  toEmail.value = defaultContact?.email || ''
  toName.value = defaultContact?.name || ''
  body.value = `Dear Team,\n\n\nPlease kindly find the attached quote ${props.quote?.quoteNumber || ''} for your review.\n\n\nPlease let us know if you need any further information or have any specific requirements. We welcome your feedback and are happy to discuss further.\n\n${buildSignature()}`
  pdfMode.value = 'auto'
  excelMode.value = 'none'
  uploadedPdfFile.value = null
  uploadedExcelFile.value = null
  extraFiles.value = []
  pickedExtraFiles.value = []
  generatedPdfBlob.value = null
  generatedExcelBlob.value = null
  excelPreviewHtml.value = ''
  previewTab.value = 'email'
  generatePdf()
}

watch(model, (open) => {
  if (open) resetForm()
  else setPdfPreview(null)
})

function close() {
  model.value = false
}

async function sendEmail() {
  if (!canSend.value) return
  sending.value = true
  try {
    const form = new FormData()
    form.append('ToEmail', toEmail.value)
    form.append('ToName', toName.value || '')
    form.append('Subject', subject.value)
    form.append('Body', body.value)

    if (pdfMode.value !== 'none') {
      const file = pdfMode.value === 'upload' && uploadedPdfFile.value
        ? uploadedPdfFile.value
        : new File([generatedPdfBlob.value as Blob], generatedPdfFileName.value || 'Quote.pdf', { type: 'application/pdf' })
      form.append('attachment', file)
    }
    if (excelMode.value !== 'none') {
      const file = excelMode.value === 'upload' && uploadedExcelFile.value
        ? uploadedExcelFile.value
        : new File([generatedExcelBlob.value as Blob], generatedExcelFileName.value || 'Quote.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
      form.append('attachmentExcel', file)
    }
    for (const file of extraFiles.value) {
      form.append('extraAttachments', file, file.name)
    }

    const res: any = await $fetch(`${api.baseURL}/quotes/${props.quote.id}/send-email`, {
      method: 'POST',
      body: form,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    emit('sent')
    model.value = false
    // The mail went out either way — only the IMAP archival copy failed.
    if (res?.sentFolderError) emit('sent-folder-warning', res.sentFolderError)
  } catch (err: any) {
    snackbarText.value = err?.data?.message || 'Failed to send email'
    snackbar.value = true
  } finally {
    sending.value = false
  }
}
</script>

<style scoped>
.excel-preview :deep(table) {
  border-collapse: collapse;
  font-size: 12px;
}
.excel-preview :deep(td) {
  border: 1px solid rgba(var(--v-border-color), 0.2);
  padding: 3px 8px;
  white-space: nowrap;
}
</style>
