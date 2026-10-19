<template>
  <v-dialog :model-value="!!row" max-width="700" @update:model-value="(open) => !open && emit('close')">
    <v-card v-if="row">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-file-document-multiple-outline" color="primary" class="mr-2" />
        Documents — row #{{ row.id }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('close')" />
      </v-card-title>
      <v-card-text class="pa-4">
        <p class="text-body-2 mb-4">
          {{ row.customerCode || 'No customer' }}<span v-if="row.partNumber"> · {{ row.partNumber }}</span>
        </p>
        <FileDropZone
          :accept="FOLLOW_UP_FILE_ACCEPT"
          label="Drop photos or PDF files here or click to add"
          :on-upload="upload"
        />

        <v-divider class="my-5" />
        <div class="d-flex align-center mb-2">
          <span class="text-subtitle-2">Saved documents</span>
          <v-spacer />
          <v-progress-circular v-if="loading" indeterminate size="18" width="2" />
        </div>
        <v-list v-if="documents.length" density="compact" class="border rounded">
          <v-list-item v-for="document in documents" :key="document.id">
            <template #prepend>
              <v-icon :icon="isImageMime(document.mimeType) ? 'mdi-file-image' : 'mdi-file-pdf-box'" :color="isImageMime(document.mimeType) ? 'info' : 'error'" />
            </template>
            <v-list-item-title>{{ document.originalFileName }}</v-list-item-title>
            <v-list-item-subtitle>
              {{ formatFileSize(document.fileSizeBytes) }} · {{ new Date(document.uploadedAt).toLocaleDateString() }}
              <span v-if="document.uploadedBy"> · {{ document.uploadedBy }}</span>
            </v-list-item-subtitle>
            <template #append>
              <v-btn icon="mdi-download-outline" size="small" variant="text" title="Download" @click="download(document)" />
              <v-btn icon="mdi-delete-outline" size="small" variant="text" color="error" title="Delete this file" @click="documentToDelete = document" />
            </template>
          </v-list-item>
        </v-list>
        <p v-else-if="!loading" class="text-caption text-medium-emphasis text-center py-4 mb-0">
          No documents have been uploaded for this row.
        </p>
      </v-card-text>
    </v-card>
  </v-dialog>

  <v-dialog :model-value="!!documentToDelete" max-width="420" @update:model-value="(open) => !open && (documentToDelete = null)">
    <v-card v-if="documentToDelete">
      <v-card-title class="text-subtitle-1 font-weight-bold">Delete this file?</v-card-title>
      <v-card-text class="text-body-2">{{ documentToDelete.originalFileName }}</v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="documentToDelete = null">Cancel</v-btn>
        <v-btn color="error" variant="tonal" :loading="deleting" @click="deleteDocument">Delete</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { FOLLOW_UP_FILE_ACCEPT, isImageMime, type FollowUpRow } from '~/utils/customerFollowUps'

interface FollowUpDocument {
  id: number
  originalFileName: string
  mimeType: string
  fileSizeBytes: number
  uploadedAt: string
  uploadedBy: string | null
}

/** Files (PDFs and photos) of one Customer Follow Ups row. Open while `row` is set; reports the new count after each change. */
const props = defineProps<{ row: FollowUpRow | null }>()

const emit = defineEmits<{
  close: []
  count: [rowId: number, count: number]
  notify: [text: string, color: string]
}>()

const api = useApi()
const documents = ref<FollowUpDocument[]>([])
const loading = ref(false)
const deleting = ref(false)
const documentToDelete = ref<FollowUpDocument | null>(null)

function errorMessage(error: any, fallback: string) {
  return error?.data?.message || error?.response?._data?.message || fallback
}

function basePath(rowId: number) {
  return `/customer-follow-ups/${rowId}/documents`
}

function setDocuments(rowId: number, list: FollowUpDocument[]) {
  documents.value = list
  emit('count', rowId, list.length)
}

watch(() => props.row?.id, async (rowId) => {
  documents.value = []
  documentToDelete.value = null
  if (!rowId) return
  loading.value = true
  try {
    const list = await api.get<FollowUpDocument[]>(basePath(rowId))
    if (props.row?.id === rowId) setDocuments(rowId, list)
  } catch (error) {
    emit('notify', errorMessage(error, 'Could not load documents.'), 'error')
  } finally {
    loading.value = false
  }
}, { immediate: true })

async function upload(files: File[], onProgress: (done: number) => void) {
  const rowId = props.row?.id
  if (!rowId) throw new Error('No row selected')
  const data = new FormData()
  files.forEach(file => data.append('files', file))
  try {
    const uploaded = await api.post<FollowUpDocument[]>(basePath(rowId), data)
    onProgress(files.length)
    if (props.row?.id === rowId) setDocuments(rowId, [...uploaded, ...documents.value])
    emit('notify', `${uploaded.length} file${uploaded.length === 1 ? '' : 's'} uploaded`, 'success')
  } catch (error) {
    emit('notify', errorMessage(error, 'Upload failed.'), 'error')
    throw error
  }
}

async function download(document: FollowUpDocument) {
  const rowId = props.row?.id
  if (!rowId) return
  try {
    const blob = await api.get<Blob>(`${basePath(rowId)}/${document.id}/download`, { responseType: 'blob' })
    const url = URL.createObjectURL(blob)
    const link = window.document.createElement('a')
    link.href = url
    link.download = document.originalFileName
    link.click()
    URL.revokeObjectURL(url)
  } catch {
    emit('notify', 'Download failed.', 'error')
  }
}

async function deleteDocument() {
  const rowId = props.row?.id
  const target = documentToDelete.value
  if (!rowId || !target) return
  deleting.value = true
  try {
    await api.delete(`${basePath(rowId)}/${target.id}`)
    setDocuments(rowId, documents.value.filter(d => d.id !== target.id))
    documentToDelete.value = null
    emit('notify', `${target.originalFileName} deleted`, 'success')
  } catch (error) {
    emit('notify', errorMessage(error, 'Could not delete the file.'), 'error')
  } finally {
    deleting.value = false
  }
}

function formatFileSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${Math.max(1, Math.round(bytes / 1024))} KB`
    : `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
</script>
