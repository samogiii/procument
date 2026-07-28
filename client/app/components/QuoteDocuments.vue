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
        <v-col v-for="cat in categories" :key="cat.key" cols="12" sm="6" md="6">
          <v-card variant="outlined" class="pa-3 h-100 d-flex flex-column">
            <div class="d-flex align-center mb-2">
              <v-icon :icon="cat.icon" size="18" class="mr-1" :color="cat.color" />
              <span class="text-body-2 font-weight-medium">{{ cat.label }}</span>
              <v-spacer />
              <v-btn
                size="x-small"
                variant="tonal"
                color="primary"
                icon="mdi-plus"
                @click="triggerUpload(cat.key)"
                :loading="uploading === cat.key"
                title="Upload files"
              />
            </div>

            <div class="flex-grow-1 overflow-y-auto" style="max-height: 180px;">
              <div v-if="filesByCategory(cat.key).length === 0" class="text-caption text-medium-emphasis italic pa-2">
                No files yet.
              </div>
              <div v-for="f in filesByCategory(cat.key)" :key="f.name" class="d-flex align-center mb-1 pa-1 rounded hover-bg">
                <v-icon icon="mdi-file-check" color="success" size="14" class="mr-1" />
                <div class="flex-grow-1 min-width-0">
                  <span class="text-caption text-truncate d-block" :title="f.name">{{ f.name }}</span>
                  <span class="text-caption text-medium-emphasis">{{ formatDate(f.modifiedAt) }}</span>
                </div>
                <v-btn size="x-small" variant="text" color="info" icon="mdi-download" @click="download(f)" />
                <v-btn size="x-small" variant="text" color="error" icon="mdi-delete" @click="remove(f)" />
              </div>
            </div>
          </v-card>
        </v-col>
      </v-row>
    </v-card-text>

    <input ref="fileInputRef" type="file" multiple class="d-none" @change="onFileSelected" />

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000">
      {{ snackbarText }}
    </v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
const props = defineProps<{ quoteId: number | string }>()

const authStore = useAuthStore()
const api = useApi()

type FileInfo = { name: string; category: string; size: number; modifiedAt: string }

const loading = ref(false)
const uploading = ref<string | null>(null)
const files = ref<FileInfo[]>([])

const categories = [
  { key: 'pdf', label: 'Quote PDF', icon: 'mdi-file-pdf-box', color: 'error' },
  { key: 'excel', label: 'Quote Excel', icon: 'mdi-file-excel-box', color: 'success' },
  // { key: 'uploaded', label: 'Uploaded', icon: 'mdi-file-upload-outline', color: 'primary' },
]

const fileInputRef = ref<HTMLInputElement | null>(null)
const pendingCategory = ref<string | null>(null)

function filesByCategory(category: string): FileInfo[] {
  return files.value.filter(f => f.category === category)
}

function formatDate(iso: string) {
  const d = new Date(iso)
  return isNaN(d.getTime()) ? '' : d.toLocaleString()
}

async function loadDocuments() {
  loading.value = true
  try {
    const data = await api.get<any>(`/quotes/${props.quoteId}/documents`)
    files.value = data.files || []
  } catch {
    showSnack('Failed to load documents', 'error')
  } finally {
    loading.value = false
  }
}

function triggerUpload(category: string) {
  pendingCategory.value = category
  fileInputRef.value?.click()
}

async function onFileSelected(e: Event) {
  const target = e.target as HTMLInputElement
  const selected = Array.from(target.files || [])
  if (!selected.length || !pendingCategory.value) return

  const category = pendingCategory.value
  uploading.value = category
  try {
    for (const file of selected) {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('category', category)
      await $fetch(`${api.baseURL}/quotes/${props.quoteId}/documents/upload`, {
        method: 'POST',
        body: formData,
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
    }
    showSnack(selected.length === 1 ? 'File uploaded successfully' : `${selected.length} files uploaded successfully`, 'success')
    await loadDocuments()
  } catch (err: any) {
    showSnack(err?.data?.message || 'Upload failed', 'error')
  } finally {
    uploading.value = null
    pendingCategory.value = null
    if (target) target.value = ''
  }
}

async function download(f: FileInfo) {
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/quotes/${props.quoteId}/documents/file`, {
      method: 'GET',
      query: { name: f.name, category: f.category },
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    saveBlob(blob, f.name)
  } catch { showSnack('Download failed', 'error') }
}

async function remove(f: FileInfo) {
  if (!confirm(`Delete "${f.name}"?`)) return
  try {
    await $fetch(`${api.baseURL}/quotes/${props.quoteId}/documents/file`, {
      method: 'DELETE',
      query: { name: f.name, category: f.category },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Deleted', 'success')
    await loadDocuments()
  } catch { showSnack('Delete failed', 'error') }
}

function saveBlob(blob: Blob, filename: string) {
  const url = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.setAttribute('download', filename)
  document.body.appendChild(link)
  link.click()
  link.parentNode?.removeChild(link)
  window.URL.revokeObjectURL(url)
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

onMounted(loadDocuments)
defineExpose({ loadDocuments })
</script>

<style scoped>
.hover-bg:hover {
  background: rgba(var(--v-theme-surface-variant), 0.1);
}
</style>
