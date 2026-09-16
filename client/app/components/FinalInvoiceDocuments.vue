<template>
  <v-card class="glass-card mb-6">
    <v-card-title class="d-flex align-center">
      <v-icon icon="mdi-folder-multiple-outline" class="mr-2" size="20" />
      Documents
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="loadDocuments">Refresh</v-btn>
    </v-card-title>
    <v-card-text>
      <v-row dense>
        <v-col v-for="category in categories" :key="category.key" cols="12" sm="6">
          <v-card variant="outlined" class="pa-3 h-100 d-flex flex-column">
            <div class="d-flex align-center mb-2">
              <v-icon :icon="category.icon" :color="category.color" size="18" class="mr-1" />
              <span class="text-body-2 font-weight-medium">{{ category.label }}</span>
              <v-spacer />
              <v-btn size="x-small" variant="tonal" color="primary" icon="mdi-plus" :loading="uploading === category.key" @click="triggerUpload(category.key)" />
            </div>
            <div class="flex-grow-1 overflow-y-auto" style="max-height:180px;">
              <div v-if="filesByCategory(category.key).length === 0" class="text-caption text-medium-emphasis italic pa-2">No files yet.</div>
              <div v-for="file in filesByCategory(category.key)" :key="file.name" class="d-flex align-center mb-1 pa-1 rounded hover-bg">
                <v-icon icon="mdi-file-check" color="success" size="14" class="mr-1" />
                <div class="flex-grow-1 min-width-0">
                  <span class="text-caption text-truncate d-block" :title="file.name">{{ file.name }}</span>
                  <span class="text-caption text-medium-emphasis">{{ formatDate(file.modifiedAt) }}</span>
                </div>
                <v-btn size="x-small" variant="text" color="info" icon="mdi-download" @click="download(file)" />
                <v-btn size="x-small" variant="text" color="error" icon="mdi-delete" @click="remove(file)" />
              </div>
            </div>
          </v-card>
        </v-col>
      </v-row>
    </v-card-text>
    <input ref="fileInput" type="file" multiple class="d-none" @change="onFilesSelected" />
    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000">{{ snackbarText }}</v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
const props = defineProps<{ invoiceId: number | string }>()

const api = useApi()
const authStore = useAuthStore()
type FileInfo = { name: string; category: string; size: number; modifiedAt: string }

const categories = [
  { key: 'invoice', label: 'Invoice PDF', icon: 'mdi-file-pdf-box', color: 'error' },
  { key: 'packing_list', label: 'Packing List', icon: 'mdi-package-variant-closed', color: 'info' },
]
const files = ref<FileInfo[]>([])
const loading = ref(false)
const uploading = ref<string | null>(null)
const pendingCategory = ref<string | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const filesByCategory = (category: string) => files.value.filter(file => file.category === category)
const formatDate = (value: string) => {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString()
}
const showSnack = (text: string, color: string) => {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

async function loadDocuments() {
  loading.value = true
  try {
    const data = await api.get<any>(`/documents/final-invoice/${props.invoiceId}`)
    files.value = data.files || []
  } catch {
    showSnack('Failed to load documents', 'error')
  } finally {
    loading.value = false
  }
}

function triggerUpload(category: string) {
  pendingCategory.value = category
  fileInput.value?.click()
}

async function onFilesSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const selected = Array.from(input.files || [])
  const category = pendingCategory.value
  if (!selected.length || !category) return

  uploading.value = category
  try {
    for (const file of selected) {
      const form = new FormData()
      form.append('file', file)
      form.append('category', category)
      await $fetch(`${api.baseURL}/documents/final-invoice/${props.invoiceId}/upload`, {
        method: 'POST', body: form, headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
    }
    await loadDocuments()
    showSnack(selected.length === 1 ? 'File uploaded successfully' : `${selected.length} files uploaded successfully`, 'success')
  } catch (error: any) {
    showSnack(error?.data?.message || 'Upload failed', 'error')
  } finally {
    uploading.value = null
    pendingCategory.value = null
    input.value = ''
  }
}

async function download(file: FileInfo) {
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/documents/final-invoice/${props.invoiceId}/file`, {
      query: { name: file.name, category: file.category }, responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url; link.download = file.name; document.body.appendChild(link); link.click(); link.remove()
    URL.revokeObjectURL(url)
  } catch { showSnack('Download failed', 'error') }
}

async function remove(file: FileInfo) {
  if (!confirm(`Delete "${file.name}"?`)) return
  try {
    await $fetch(`${api.baseURL}/documents/final-invoice/${props.invoiceId}/file`, {
      method: 'DELETE', query: { name: file.name, category: file.category },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    await loadDocuments()
    showSnack('Deleted', 'success')
  } catch { showSnack('Delete failed', 'error') }
}

onMounted(loadDocuments)
defineExpose({ loadDocuments })
</script>

<style scoped>
.hover-bg:hover { background: rgba(var(--v-theme-surface-variant), 0.1); }
</style>
