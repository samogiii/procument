<template>
  <v-card class="glass-card mb-6">
    <v-card-title class="d-flex align-center flex-wrap gap-2">
      <v-icon icon="mdi-folder-multiple-outline" class="mr-1" size="20" color="primary" />
      Supplier Documents
      <v-chip v-if="documents.length" size="x-small" variant="tonal" color="primary">{{ documents.length }} files</v-chip>
      <v-spacer />
      <v-btn v-if="canUpload" size="small" variant="tonal" color="primary" prepend-icon="mdi-upload" @click="openUpload()">
        Upload
      </v-btn>
    </v-card-title>
    <v-card-text>
      <v-alert v-if="!hasSupplierPi" type="info" variant="tonal" density="compact" class="mb-4">
        Upload the supplier's <strong>PI</strong> before the PO can move to <strong>Waiting For PR</strong>.
      </v-alert>
      <v-alert v-if="amountWarning" type="warning" variant="tonal" density="compact" class="mb-4" closable @click:close="amountWarning = ''">
        {{ amountWarning }}
      </v-alert>

      <v-row dense>
        <v-col v-for="cat in CATEGORIES" :key="cat.key" cols="12" sm="6" md="4">
          <div class="doc-cat pa-3 h-100">
            <div class="d-flex align-center mb-2">
              <v-icon :icon="cat.icon" :color="cat.color" size="18" class="mr-2" />
              <span class="text-subtitle-2 font-weight-bold">{{ cat.title }}</span>
              <v-spacer />
              <v-btn v-if="canUpload" icon="mdi-plus" size="x-small" variant="text" :title="`Upload ${cat.title}`" @click="openUpload(cat.key)" />
            </div>
            <div v-if="loading" class="text-caption text-medium-emphasis">Loading…</div>
            <div v-else-if="!byCategory[cat.key]?.length" class="text-caption text-medium-emphasis">No files</div>
            <div v-for="doc in byCategory[cat.key]" :key="doc.id" class="d-flex align-center gap-1 mb-1">
              <v-btn
                variant="text"
                size="small"
                class="text-none px-1 flex-grow-1 justify-start doc-name"
                :prepend-icon="docPreview.isPreviewable(doc.fileName) ? 'mdi-eye-outline' : 'mdi-download'"
                :loading="openingId === doc.id"
                :title="docTooltip(doc)"
                @click="openDoc(doc)"
              >
                <span class="text-truncate">{{ doc.documentNumber ? `${doc.documentNumber} · ` : '' }}{{ doc.fileName }}</span>
              </v-btn>
              <span v-if="doc.amount != null" class="text-caption text-medium-emphasis text-no-wrap">${{ formatPrice(doc.amount) }}</span>
              <v-btn
                v-if="canDelete(doc)"
                icon="mdi-delete-outline"
                size="x-small"
                variant="text"
                color="error"
                :loading="deletingId === doc.id"
                @click="removeDoc(doc)"
              />
            </div>
          </div>
        </v-col>
      </v-row>
    </v-card-text>

    <v-dialog v-model="showUpload" max-width="480" persistent>
      <v-card>
        <v-card-title class="pa-4 pb-2">Upload supplier document</v-card-title>
        <v-card-text class="pa-4">
          <v-select v-model="form.category" :items="CATEGORIES" item-title="title" item-value="key" label="Category"
            variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="form.documentNumber" :label="form.category === 'SupplierPI' ? 'PI number' : 'Document number (optional)'"
            variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-if="form.category === 'SupplierPI' || form.category === 'SupplierInvoice'" v-model.number="form.amount"
            type="number" min="0" step="0.01" prefix="$" label="Amount (optional)" variant="outlined" density="compact"
            :hint="poTotal != null ? `PO total: $${formatPrice(poTotal)}` : undefined" persistent-hint class="mb-3" />
          <v-file-input v-model="form.file" label="File" variant="outlined" density="compact" prepend-icon="" prepend-inner-icon="mdi-paperclip"
            :multiple="false" show-size />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showUpload = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="uploading" :disabled="!selectedFile" @click="upload">Upload</v-btn>
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
  </v-card>
</template>

<script setup lang="ts">
/**
 * Supplier PI / invoice / certificate files stored against a Stock PO
 * (`/purchase-orders/{poId}/documents`). Customer POs keep their invoice-keyed Document Center.
 */
const props = defineProps<{ poId: number | string; poTotal?: number | null }>()
const emit = defineEmits<{ (e: 'changed'): void; (e: 'notify', text: string, color: string): void }>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()

const CATEGORIES = [
  { key: 'SupplierPI', title: 'Supplier PI', icon: 'mdi-file-document-outline', color: 'primary' },
  { key: 'SupplierInvoice', title: 'Supplier Invoice', icon: 'mdi-receipt-text-outline', color: 'success' },
  { key: 'Certificate', title: 'Certificates', icon: 'mdi-certificate-outline', color: 'warning' },
  { key: 'PackingList', title: 'Packing List', icon: 'mdi-package-variant', color: 'info' },
  { key: 'Other', title: 'Other', icon: 'mdi-file-outline', color: 'grey' },
]

type PoDocument = {
  id: number; category: string; fileName: string; documentNumber?: string | null; amount?: number | null
  uploadedBy: number; uploadedByName: string; createdAt: string
}

const documents = ref<PoDocument[]>([])
const loading = ref(false)
const openingId = ref<number | null>(null)
const deletingId = ref<number | null>(null)
const amountWarning = ref('')

// Payment and AHM can read the documents but the API only lets these roles upload.
const canUpload = computed(() => ['Admin', 'SuperAdmin', 'Expert', 'Inventory'].includes(authStore.user?.role ?? ''))
const canDelete = (doc: PoDocument) => authStore.isAdmin || (canUpload.value && doc.uploadedBy === authStore.user?.id)

const byCategory = computed(() => {
  const map: Record<string, PoDocument[]> = {}
  for (const doc of documents.value) (map[doc.category] ||= []).push(doc)
  return map
})
const hasSupplierPi = computed(() => !!byCategory.value.SupplierPI?.length)

const docTooltip = (doc: PoDocument) =>
  `${doc.fileName} — uploaded by ${doc.uploadedByName || 'unknown'} on ${new Date(doc.createdAt).toLocaleString()}`

async function load() {
  loading.value = true
  try { documents.value = await api.get<PoDocument[]>(`/purchase-orders/${props.poId}/documents`) }
  catch { documents.value = [] }
  finally { loading.value = false }
}

// ── Upload ──
const showUpload = ref(false)
const uploading = ref(false)
const form = reactive<{ category: string; documentNumber: string; amount: number | null; file: File | File[] | null }>({
  category: 'SupplierPI', documentNumber: '', amount: null, file: null,
})
const selectedFile = computed<File | null>(() => Array.isArray(form.file) ? (form.file[0] ?? null) : form.file)

function openUpload(category = 'SupplierPI') {
  Object.assign(form, { category, documentNumber: '', amount: null, file: null })
  showUpload.value = true
}

async function upload() {
  if (!selectedFile.value) return
  uploading.value = true
  try {
    const body = new FormData()
    body.append('file', selectedFile.value)
    body.append('category', form.category)
    if (form.documentNumber?.trim()) body.append('documentNumber', form.documentNumber.trim())
    if (form.amount != null && String(form.amount) !== '') body.append('amount', String(form.amount))
    const res = await api.post<{ warning?: string | null }>(`/purchase-orders/${props.poId}/documents`, body)
    amountWarning.value = res?.warning || ''
    showUpload.value = false
    emit('notify', 'Document uploaded', 'success')
    await load()
    emit('changed')
  } catch (e: any) {
    emit('notify', e?.data?.message || 'Upload failed', 'error')
  } finally {
    uploading.value = false
  }
}

// ── Open / download ──
async function openDoc(doc: PoDocument) {
  const path = `/purchase-orders/${props.poId}/documents/${doc.id}/download`
  if (docPreview.isPreviewable(doc.fileName)) {
    await docPreview.preview(path, doc.fileName)
    return
  }
  openingId.value = doc.id
  try {
    const blob = await api.get<Blob>(path, { responseType: 'blob' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = doc.fileName
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    emit('notify', 'Download failed', 'error')
  } finally {
    openingId.value = null
  }
}

async function removeDoc(doc: PoDocument) {
  if (!confirm(`Delete ${doc.fileName}?`)) return
  deletingId.value = doc.id
  try {
    await api.del(`/purchase-orders/${props.poId}/documents/${doc.id}`)
    await load()
    emit('changed')
  } catch (e: any) {
    emit('notify', e?.data?.message || 'Delete failed', 'error')
  } finally {
    deletingId.value = null
  }
}

watch(() => props.poId, load, { immediate: true })
defineExpose({ reload: load })
</script>

<style scoped>
.doc-cat {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
}
.doc-name :deep(.v-btn__content) {
  min-width: 0;
}
</style>
