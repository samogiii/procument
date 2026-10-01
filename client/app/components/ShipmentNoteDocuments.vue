<template>
  <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="220">
    <div class="d-flex align-center mb-1">
      <div class="text-caption font-weight-bold text-medium-emphasis">CUSTOMER DOCUMENTS</div>
      <v-spacer />
      <v-btn
        v-if="canUpload"
        size="x-small"
        variant="text"
        color="teal"
        prepend-icon="mdi-paperclip-plus"
        :loading="uploading"
        @click="fileInput?.click()"
      >
        Add
      </v-btn>
      <input ref="fileInput" type="file" multiple hidden @change="upload">
    </div>

    <div v-if="note.pickupNote" class="text-body-2 mb-2">{{ note.pickupNote }}</div>

    <div v-if="note.documents?.length" class="d-flex flex-wrap gap-1">
      <v-chip
        v-for="doc in note.documents"
        :key="doc.id"
        size="small"
        color="teal"
        variant="tonal"
        prepend-icon="mdi-file-document-outline"
        :title="`${doc.uploadedByName ?? ''} · ${new Date(doc.uploadedAt).toLocaleString()}`"
        @click="open(doc)"
      >
        {{ doc.originalFileName }}
      </v-chip>
    </div>
    <div v-else class="text-caption text-medium-emphasis">No documents</div>

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
/** Documents attached to a shipment note — the papers the customer handed over at a PickUp. */
const props = defineProps<{ note: any; canUpload?: boolean }>()
const emit = defineEmits<{ updated: [note: any] }>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)

const path = (doc: any) => `/shipment-notes/${props.note.id}/documents/${doc.id}/file`

async function open(doc: any) {
  if (docPreview.isPreviewable(doc.originalFileName, doc.mimeType ?? undefined)) {
    await docPreview.preview(path(doc), doc.originalFileName, doc.mimeType ?? undefined)
    return
  }
  // Not previewable (e.g. Word / Excel) — download it with the auth header.
  const blob = await $fetch<Blob>(`${api.baseURL}${path(doc)}`, {
    headers: { Authorization: `Bearer ${authStore.user?.token}` },
    responseType: 'blob',
  })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = doc.originalFileName
  a.click()
  URL.revokeObjectURL(url)
}

async function upload(e: Event) {
  const input = e.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  if (!files.length) return
  uploading.value = true
  try {
    const fd = new FormData()
    for (const f of files) fd.append('files', f, f.name)
    emit('updated', await api.post(`/shipment-notes/${props.note.id}/documents`, fd))
  } finally {
    uploading.value = false
  }
}
</script>
