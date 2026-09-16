<template>
  <div class="document-category-card pa-3 rounded border h-100">
    <div class="d-flex align-start gap-2 mb-3">
      <v-avatar :color="category.color" variant="tonal" size="34">
        <v-icon :icon="category.icon" size="18" />
      </v-avatar>
      <div class="flex-grow-1" style="min-width: 0;">
        <div class="text-body-2 font-weight-bold">{{ category.title }}</div>
        <div class="text-caption text-medium-emphasis">{{ category.description }}</div>
      </div>
      <v-chip size="x-small" variant="tonal" :color="files.length ? category.color : 'grey'">
        {{ files.length }}
      </v-chip>
    </div>

    <div v-if="files.length" class="d-flex flex-column gap-1 mb-3">
      <div v-for="file in files" :key="`${file.name}-${file.originalInvoiceId || ''}`" class="document-file-row d-flex align-center gap-1 pa-2 rounded">
        <v-icon icon="mdi-file-outline" :color="category.color" size="17" />
        <div class="flex-grow-1" style="min-width: 0;">
          <div class="text-caption font-weight-medium text-truncate" :title="file.displayName || file.name">
            {{ file.displayName || file.name }}
          </div>
          <div class="text-caption text-medium-emphasis file-meta">
            {{ formatBytes(file.size) }}<span v-if="file.modifiedAt"> · {{ formatDate(file.modifiedAt) }}</span>
          </div>
        </div>
        <v-btn icon="mdi-download" size="x-small" variant="text" color="info" title="Download" @click="$emit('download', file)" />
        <v-btn
          v-if="canDelete"
          icon="mdi-delete-outline"
          size="x-small"
          variant="text"
          color="error"
          title="Delete"
          :loading="deletingFile === file.name"
          @click="$emit('delete', file)"
        />
      </div>
    </div>
    <div v-else class="text-caption text-medium-emphasis py-2 mb-2">No files yet.</div>

    <v-btn
      v-if="canUpload"
      block
      size="small"
      variant="tonal"
      :color="category.color"
      prepend-icon="mdi-upload"
      :loading="loading"
      @click="$emit('upload')"
    >
      Upload {{ category.title }}
    </v-btn>
    <div v-else-if="category.readOnlyNote" class="text-caption text-medium-emphasis d-flex align-center gap-1">
      <v-icon icon="mdi-lock-outline" size="14" />
      {{ category.readOnlyNote }}
    </div>
  </div>
</template>

<script setup lang="ts">
type DocumentCategory = {
  key: string
  title: string
  description: string
  icon: string
  color: string
  readOnlyNote?: string
}

type DocumentFile = {
  name: string
  category: string
  size: number
  modifiedAt: string
  originalInvoiceId?: number
  displayName?: string
}

defineProps<{
  category: DocumentCategory
  files: DocumentFile[]
  canUpload: boolean
  canDelete: boolean
  loading?: boolean
  deletingFile?: string | null
}>()

defineEmits<{
  upload: []
  download: [file: DocumentFile]
  delete: [file: DocumentFile]
}>()

function formatBytes(bytes: number) {
  if (!bytes) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  let unit = 0
  let value = bytes
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${value.toFixed(value < 10 && unit > 0 ? 1 : 0)} ${units[unit]}`
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString()
}
</script>

<style scoped>
.document-category-card {
  background: rgba(var(--v-theme-surface-variant), 0.12);
}

.document-file-row {
  background: rgba(var(--v-theme-surface), 0.7);
  border: 1px solid rgba(var(--v-border-color), 0.35);
}

.file-meta {
  font-size: 10px !important;
}
</style>
