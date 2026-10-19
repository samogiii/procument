<template>
  <v-dialog :model-value="modelValue" max-width="860" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-paperclip" class="mr-2" color="primary" />
        Shipping Documents
        <span v-if="title" class="text-body-2 text-medium-emphasis ml-2">— {{ title }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-divider />

      <v-card-text>
        <div v-if="loading && !tracks.length" class="d-flex justify-center my-6">
          <v-progress-circular indeterminate color="primary" />
        </div>
        <div v-else-if="!tracks.length" class="text-center text-medium-emphasis pa-6">
          <v-icon icon="mdi-truck-remove-outline" size="40" class="mb-2" />
          <div>This part has no track number yet — documents are attached to a track.</div>
        </div>

        <v-card v-for="t in tracks" :key="t.trackId" variant="outlined" class="mb-4">
          <v-card-title class="text-subtitle-2 d-flex align-center flex-wrap gap-2 py-2">
            <v-icon icon="mdi-truck-outline" size="18" />
            <span class="font-weight-bold">{{ t.trackNumber }}</span>
            <span v-if="t.carrier" class="text-caption text-medium-emphasis">{{ t.carrier }}</span>
            <span v-if="t.warehouseName" class="text-caption text-medium-emphasis">· {{ t.warehouseName }}</span>
            <v-spacer />
            <v-chip size="x-small" variant="tonal">{{ t.status }}</v-chip>
          </v-card-title>
          <v-divider />
          <v-card-text>
            <v-row dense>
              <v-col v-for="kind in kinds" :key="kind.key" cols="12" md="6">
                <div class="d-flex align-center mb-2">
                  <v-icon :icon="kind.icon" size="16" class="mr-1 text-medium-emphasis" />
                  <span class="text-caption font-weight-bold text-medium-emphasis">{{ kind.label }}</span>
                  <v-chip size="x-small" variant="tonal" class="ml-2">{{ t[kind.key].length }}</v-chip>
                </div>
                <FileDropZone
                  compact
                  :label="kind.dropLabel"
                  :on-upload="(files, onProgress) => upload(files, onProgress, t.trackId, kind.key === 'partDocuments' ? poItemId : null)"
                  class="mb-2"
                />
                <div v-if="!t[kind.key].length" class="text-caption text-medium-emphasis">No documents yet.</div>
                <v-list v-else density="compact" class="pa-0">
                  <v-list-item
                    v-for="doc in t[kind.key]"
                    :key="doc.id"
                    :title="doc.originalFileName"
                    :subtitle="`${(doc.fileSizeBytes / 1024).toFixed(1)} KB · ${new Date(doc.uploadedAt).toLocaleDateString()}${doc.uploadedByName ? ' · ' + doc.uploadedByName : ''}`"
                    rounded="lg"
                    class="px-1"
                  >
                    <template #append>
                      <v-btn
                        :icon="docPreview.isPreviewable(doc.originalFileName, doc.mimeType) ? 'mdi-eye-outline' : 'mdi-download'"
                        size="x-small" variant="text" color="primary"
                        @click="docPreview.preview(`/shipping/documents/${doc.id}/file`, doc.originalFileName, doc.mimeType)"
                      />
                      <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error" @click="remove(doc)" />
                    </template>
                  </v-list-item>
                </v-list>
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </v-card-text>
    </v-card>

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />
  </v-dialog>
</template>

<script setup lang="ts">
/**
 * View and add shipping documents for one PO line, at any shipping status: per track, the shipment
 * documents (shared by every part on the same track number + warehouse) and the part's own documents.
 */
const props = defineProps<{
  modelValue: boolean
  poItemId: number | null
  /** Shown in the title, e.g. "PN-123 · PO-45". */
  title?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  /** New document count, so the grid cell can update without a reload. */
  changed: [count: number]
  notify: [message: string, color?: string]
}>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()

const kinds = [
  { key: 'shipmentDocuments', label: 'SHIPMENT DOCUMENTS', icon: 'mdi-file-multiple-outline', dropLabel: 'Drop shipment documents or click' },
  { key: 'partDocuments', label: 'PART DOCUMENTS', icon: 'mdi-paperclip', dropLabel: 'Drop part documents or click' },
] as const

const tracks = ref<any[]>([])
const loading = ref(false)

async function load() {
  if (!props.poItemId) { tracks.value = []; return }
  loading.value = true
  try {
    tracks.value = await api.get<any[]>(`/shipping/po-items/${props.poItemId}/documents`)
    // A shipment document is shared by sibling tracks, so count each one once.
    const ids = new Set<number>()
    for (const t of tracks.value) for (const d of [...t.shipmentDocuments, ...t.partDocuments]) ids.add(d.id)
    emit('changed', ids.size)
  } catch {
    emit('notify', 'Failed to load documents', 'error')
  } finally {
    loading.value = false
  }
}

/** Posts files one at a time (the endpoints take a single file). */
async function upload(files: File[], onProgress: (done: number) => void, trackId: number, poItemId: number | null) {
  const url = poItemId != null
    ? `/shipping/track-numbers/${trackId}/parts/${poItemId}/documents`
    : `/shipping/track-numbers/${trackId}/documents`
  const failed: string[] = []
  let done = 0
  for (const file of files) {
    const formData = new FormData()
    formData.append('file', file)
    try {
      await $fetch(`${api.baseURL}${url}`, {
        method: 'POST',
        body: formData,
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
    } catch {
      failed.push(file.name)
    }
    onProgress(++done)
  }
  await load()
  if (failed.length === files.length) {
    emit('notify', 'Upload failed', 'error')
    throw new Error('upload failed') // keeps the files staged for a retry
  }
  emit('notify', failed.length
    ? `Uploaded ${files.length - failed.length}, failed: ${failed.join(', ')}`
    : `Uploaded ${files.length} file${files.length === 1 ? '' : 's'}`, failed.length ? 'warning' : 'success')
}

async function remove(doc: any) {
  if (!confirm(`Delete ${doc.originalFileName}?`)) return
  try {
    await api.del(`/shipping/documents/${doc.id}`)
    emit('notify', 'Deleted')
    await load()
  } catch {
    emit('notify', 'Delete failed', 'error')
  }
}

watch(() => [props.modelValue, props.poItemId], ([open]) => { if (open) load() }, { immediate: true })
</script>
