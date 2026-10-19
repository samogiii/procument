<template>
  <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="220">
    <div class="text-caption font-weight-bold text-medium-emphasis mb-1">CUSTOMER DOCUMENTS</div>
    <input ref="fileInput" type="file" multiple hidden @change="upload">

    <div v-if="note.pickupNote" class="text-body-2 mb-2">{{ note.pickupNote }}</div>

    <!-- One section per part: each part keeps its own files -->
    <div v-for="(group, i) in groups" :key="group.poItemId ?? 'shared'" :class="{ 'mt-2 pt-2 border-t': i > 0 }">
      <div class="d-flex align-center mb-1">
        <v-icon :icon="group.poItemId == null ? 'mdi-file-multiple-outline' : 'mdi-cog-outline'" size="14" class="mr-1 text-medium-emphasis" />
        <span class="text-caption font-weight-medium">{{ group.label }}</span>
        <v-chip size="x-small" variant="tonal" class="ml-1">{{ group.docs.length }}</v-chip>
        <v-spacer />
        <v-btn
          v-if="canUpload && group.poItemId != null"
          size="x-small"
          variant="text"
          color="teal"
          prepend-icon="mdi-paperclip-plus"
          :loading="uploadingFor === group.poItemId"
          :disabled="uploadingFor != null"
          @click="pick(group.poItemId)"
        >
          Add
        </v-btn>
      </div>
      <div v-if="group.docs.length" class="d-flex flex-wrap gap-1">
        <v-chip
          v-for="doc in group.docs"
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
    </div>

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
/** The part whose Add button opened the file picker / is uploading. */
const uploadingFor = ref<number | null>(null)
let pickFor: number | null = null

/**
 * Documents grouped per part on the note. Files uploaded with the pickup itself (no part)
 * are shown on their own as "Whole pickup", only when there are any.
 */
const groups = computed(() => {
  const docs: any[] = props.note.documents ?? []
  const parts = new Map<number, string>()
  for (const t of props.note.trackNumbers ?? [])
    if (t.poItemId && !parts.has(t.poItemId)) parts.set(t.poItemId, t.partNumberName || `Part #${t.poItemId}`)

  const result = [...parts].map(([poItemId, label]) => ({
    poItemId: poItemId as number | null,
    label,
    docs: docs.filter(d => d.poItemId === poItemId),
  }))
  const shared = docs.filter(d => d.poItemId == null || !parts.has(d.poItemId))
  if (shared.length) result.unshift({ poItemId: null, label: 'Whole pickup', docs: shared })
  return result
})

function pick(poItemId: number | null) {
  pickFor = poItemId
  fileInput.value?.click()
}

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
  uploadingFor.value = pickFor
  try {
    const fd = new FormData()
    for (const f of files) fd.append('files', f, f.name)
    if (pickFor != null) fd.append('poItemId', String(pickFor))
    emit('updated', await api.post(`/shipment-notes/${props.note.id}/documents`, fd))
  } finally {
    uploadingFor.value = null
  }
}
</script>
