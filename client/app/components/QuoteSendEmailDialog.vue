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
        <div class="pa-4" style="width:33%; border-right:1px solid rgba(var(--v-border-color),0.15); overflow-y:auto;">
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

        <!-- Column 2: Attachment -->
        <div class="pa-4" style="width:33%; border-right:1px solid rgba(var(--v-border-color),0.15); overflow-y:auto;">
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">ATTACHMENT</div>
          <v-btn-toggle v-model="attachmentMode" mandatory color="primary" density="compact" class="mb-4 d-flex">
            <v-btn value="auto" size="small" class="flex-grow-1">Auto-generate</v-btn>
            <v-btn value="upload" size="small" class="flex-grow-1">Upload File</v-btn>
          </v-btn-toggle>

          <div v-if="attachmentMode === 'auto'">
            <div v-if="generatingPdf" class="d-flex align-center gap-2 text-body-2 text-medium-emphasis">
              <v-progress-circular indeterminate size="18" width="2" />
              Generating quote PDF…
            </div>
            <div v-else-if="generatedPdfBlob" class="d-flex align-center gap-2 text-body-2">
              <v-icon icon="mdi-file-pdf-box" color="error" />
              {{ generatedFileName }}
              <v-chip size="x-small" variant="tonal">{{ formatSize(generatedPdfBlob.size) }}</v-chip>
            </div>
            <div v-else class="text-body-2 text-error">
              Failed to generate PDF. <a href="#" @click.prevent="generatePdf">Retry</a>
            </div>
          </div>
          <div v-else>
            <v-file-input
              v-model="uploadedFile"
              label="Upload PDF"
              variant="outlined"
              density="compact"
              hide-details
              accept="application/pdf"
              prepend-icon=""
              prepend-inner-icon="mdi-paperclip"
            />
          </div>
        </div>

        <!-- Column 3: Live preview -->
        <div class="pa-4" style="width:34%; overflow-y:auto;">
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">PREVIEW</div>
          <v-card variant="outlined" class="pa-4">
            <div class="mb-2"><span class="font-weight-bold">Subject:</span> {{ subject || '—' }}</div>
            <div class="mb-2"><span class="font-weight-bold">To:</span> {{ toName ? `${toName} <${toEmail}>` : (toEmail || '—') }}</div>
            <v-divider class="my-3" />
            <div style="white-space: pre-wrap;" class="text-body-2">{{ body || '—' }}</div>
            <v-divider class="my-3" />
            <div class="d-flex align-center gap-2 text-body-2">
              <v-icon icon="mdi-paperclip" size="16" />
              <span v-if="attachmentMode === 'auto'">{{ generatedFileName || 'No attachment yet' }}</span>
              <span v-else>{{ uploadedFile?.name || 'No file selected' }}</span>
            </div>
          </v-card>
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

const props = defineProps<{ quote: any, preset: any }>()
const model = defineModel<boolean>({ default: false })
const emit = defineEmits<{ (e: 'sent'): void }>()

const api = useApi()
const authStore = useAuthStore()

const subject = ref('')
const toEmail = ref('')
const toName = ref('')
const body = ref('')
const sending = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')

const attachmentMode = ref<'auto' | 'upload'>('auto')
const generatingPdf = ref(false)
const generatedPdfBlob = ref<Blob | null>(null)
const generatedFileName = ref('')
const uploadedFile = ref<File | null>(null)

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

const canSend = computed(() => {
  if (!toEmail.value || !subject.value) return false
  if (attachmentMode.value === 'auto') return !!generatedPdfBlob.value && !generatingPdf.value
  return !!uploadedFile.value
})

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

async function generatePdf() {
  generatingPdf.value = true
  generatedPdfBlob.value = null
  try {
    const payload = buildQuotePdfPayload(props.quote, props.preset)
    generatedPdfBlob.value = await fetchQuotePdfBlob(payload)
    generatedFileName.value = buildQuotePdfFileName(props.quote)
  } catch {
    generatedPdfBlob.value = null
  } finally {
    generatingPdf.value = false
  }
}

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
  attachmentMode.value = 'auto'
  uploadedFile.value = null
  generatedPdfBlob.value = null
  generatePdf()
}

watch(model, (open) => {
  if (open) resetForm()
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

    const file = attachmentMode.value === 'upload' && uploadedFile.value
      ? uploadedFile.value
      : new File([generatedPdfBlob.value as Blob], generatedFileName.value || 'Quote.pdf', { type: 'application/pdf' })
    form.append('attachment', file)

    await $fetch(`${api.baseURL}/quotes/${props.quote.id}/send-email`, {
      method: 'POST',
      body: form,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    emit('sent')
    model.value = false
  } catch (err: any) {
    snackbarText.value = err?.data?.message || 'Failed to send email'
    snackbar.value = true
  } finally {
    sending.value = false
  }
}
</script>
