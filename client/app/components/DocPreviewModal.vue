<template>
  <v-dialog
    :model-value="open"
    width="92vw"
    @update:model-value="v => !v && close()"
  >
    <v-card>
      <!-- Header -->
      <v-card-title class="d-flex align-center pa-3 pb-2">
        <v-icon
          :icon="isPdf(fileName, mimeType) ? 'mdi-file-pdf-box' : isImage ? 'mdi-image-outline' : 'mdi-file-outline'"
          :color="isPdf(fileName, mimeType) ? 'error' : isImage ? 'primary' : 'default'"
          size="22"
          class="mr-2"
        />
        <span class="text-body-2 font-weight-medium" style="overflow:hidden; text-overflow:ellipsis; white-space:nowrap; max-width:60vw;">
          {{ fileName }}
        </span>
        <v-spacer />
        <v-btn icon="mdi-download" variant="text" size="small" title="Download" class="mr-1" @click="download()" />
        <v-btn icon="mdi-close" variant="text" size="small" @click="close()" />
      </v-card-title>

      <v-divider />

      <!-- PDF -->
      <iframe
        v-if="blobUrl && isPdf(fileName, mimeType)"
        :src="blobUrl"
        class="preview-body"
        style="border:none; display:block;"
        allow="fullscreen"
      />

      <!-- Image -->
      <div v-else-if="blobUrl && isImage" class="preview-body" style="background:#111; display:flex; align-items:center; justify-content:center; overflow:auto;">
        <img :src="blobUrl" :alt="fileName" style="max-width:100%; max-height:82vh; object-fit:contain;" />
      </div>

      <!-- Cannot preview -->
      <div v-else-if="blobUrl" class="preview-body d-flex flex-column align-center justify-center pa-8">
        <v-icon icon="mdi-file-alert-outline" size="56" color="grey" class="mb-3" />
        <p class="text-body-2 text-medium-emphasis mb-4">Preview not available for this file type.</p>
        <v-btn color="primary" variant="flat" prepend-icon="mdi-download" @click="download()">Download to view</v-btn>
      </div>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  open: boolean
  blobUrl: string | null
  fileName: string
  mimeType: string
}>()

const emit = defineEmits<{ close: [] }>()

const { isPdf } = useDocPreview()

const isImage = computed(() => {
  const mime = props.mimeType
  const ext = props.fileName.split('.').pop()?.toLowerCase() ?? ''
  return mime.startsWith('image/') || ['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg', 'bmp'].includes(ext)
})

function close() { emit('close') }

function download() {
  if (!props.blobUrl) return
  const a = document.createElement('a')
  a.href = props.blobUrl
  a.download = props.fileName
  a.click()
}
</script>

<style scoped>
.preview-body {
  width: 100%;
  height: 82vh;
}
</style>
