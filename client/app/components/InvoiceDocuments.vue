<template>
  <v-card class="glass-card mb-6">
    <v-card-title class="d-flex align-center">
      <v-icon icon="mdi-folder-multiple-outline" class="mr-2" size="20" />
      Documents
      <v-spacer />
      <v-btn variant="text" size="small" prepend-icon="mdi-refresh" :loading="loading" @click="loadDocuments">Refresh</v-btn>
    </v-card-title>
    <v-card-text>
      <!-- PI-level documents -->
      <div class="mb-4">
        <div class="text-subtitle-2 mb-2 d-flex align-center">
          <v-icon icon="mdi-file-document-outline" size="18" class="mr-1" />
          Proforma Invoice Documents
        </div>
        <v-row dense>
          <v-col v-for="cat in piCategories" :key="cat.key" cols="12" sm="6" md="3">
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
                  @click="triggerUpload(cat.key, null)"
                  :loading="uploading === cat.key"
                  title="Upload New"
                />
              </div>

              <div class="flex-grow-1 overflow-y-auto" style="max-height: 150px;">
                <div v-if="piFilesByCategory(cat.key).length === 0" class="text-caption text-medium-emphasis italic pa-2">
                  No files uploaded.
                </div>
                <div v-for="f in piFilesByCategory(cat.key)" :key="f.name" class="d-flex align-center mb-1 pa-1 rounded hover-bg">
                  <v-icon icon="mdi-file-check" color="success" size="14" class="mr-1" />
                  <span class="text-caption text-truncate flex-grow-1" :title="f.name">
                    {{ f.name }}
                  </span>
                  <v-btn size="x-small" variant="text" color="info" icon="mdi-download" @click="downloadPI(f.name)" />
                  <v-btn size="x-small" variant="text" color="error" icon="mdi-delete" @click="deletePI(f.name)" />
                </div>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </div>

      <!-- Supplier-level documents -->
      <v-divider class="my-4" />
      <div class="text-subtitle-2 mb-2 d-flex align-center">
        <v-icon icon="mdi-truck-outline" size="18" class="mr-1" />
        Supplier Documents
        <v-chip v-if="suppliers.length" size="x-small" class="ml-2">{{ suppliers.length }}</v-chip>
      </div>

      <div v-if="!suppliers.length" class="text-caption text-medium-emphasis">
        No suppliers yet. Suppliers appear after POs are created for this Proforma Invoice.
      </div>

      <v-expansion-panels v-else multiple variant="accordion">
        <v-expansion-panel v-for="s in suppliers" :key="s.supplierId">
          <v-expansion-panel-title>
            <div class="d-flex align-center">
              <v-icon icon="mdi-account-tie" size="18" class="mr-2" />
              <span class="font-weight-medium">{{ s.supplierName }}</span>
              <v-chip size="x-small" class="ml-2">{{ s.files.length }} files</v-chip>
            </div>
          </v-expansion-panel-title>
          <v-expansion-panel-text>
            <v-row dense>
              <v-col v-for="cat in supplierCategories" :key="cat.key" cols="12" md="6">
                <v-card variant="outlined" class="pa-3">
                  <div class="d-flex align-center mb-2">
                    <v-icon :icon="cat.icon" size="18" class="mr-1" :color="cat.color" />
                    <span class="text-body-2 font-weight-medium">{{ cat.label }}</span>
                    <v-spacer />
                    <v-btn
                      size="x-small"
                      variant="tonal"
                      color="primary"
                      icon="mdi-plus"
                      @click="triggerUpload(cat.key, s.supplierId)"
                      :loading="uploading === `${cat.key}-${s.supplierId}`"
                      title="Upload New"
                    />
                  </div>

                  <div class="overflow-y-auto" style="max-height: 150px;">
                    <div v-if="supplierFilesByCategory(s, cat.key).length === 0" class="text-caption text-medium-emphasis italic pa-2">
                      No files uploaded.
                    </div>
                    <div v-for="f in supplierFilesByCategory(s, cat.key)" :key="f.name" class="d-flex align-center mb-1 pa-1 rounded hover-bg">
                      <v-icon :icon="f.name.includes('_final') ? 'mdi-file-star' : 'mdi-file-check'" :color="f.name.includes('_final') ? 'amber-darken-2' : 'success'" size="14" class="mr-1" />
                      <span class="text-caption text-truncate flex-grow-1" :title="f.name">
                        {{ f.name }}
                      </span>
                      <v-btn size="x-small" variant="text" color="info" icon="mdi-download" @click="downloadSupplier(s.supplierId, f.name)" />
                      <v-btn size="x-small" variant="text" color="error" icon="mdi-delete" @click="deleteSupplier(s.supplierId, f.name)" />
                    </div>
                  </div>
                </v-card>
              </v-col>
            </v-row>

            <!-- Any other files -->
            <div v-if="otherSupplierFiles(s).length" class="mt-3">
              <div class="text-caption text-medium-emphasis mb-1">Other files:</div>
              <v-chip
                v-for="f in otherSupplierFiles(s)"
                :key="f.name"
                size="small"
                class="mr-1 mb-1"
                closable
                @click:close="deleteSupplier(s.supplierId, f.name)"
                @click="downloadSupplier(s.supplierId, f.name)"
              >
                <v-icon icon="mdi-file-outline" size="14" class="mr-1" />
                {{ f.name }}
              </v-chip>
            </div>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>

      <!-- Other PI-level files -->
      <div v-if="otherPiFiles.length" class="mt-4">
        <v-divider class="mb-3" />
        <div class="text-caption text-medium-emphasis mb-1">Other PI files:</div>
        <v-chip
          v-for="f in otherPiFiles"
          :key="f.name"
          size="small"
          class="mr-1 mb-1"
          closable
          @click:close="deletePI(f.name)"
          @click="downloadPI(f.name)"
        >
          <v-icon icon="mdi-file-outline" size="14" class="mr-1" />
          {{ f.name }}
        </v-chip>
      </div>
    </v-card-text>

    <!-- Hidden file input used for all uploads -->
    <input
      ref="fileInputRef"
      type="file"
      class="d-none"
      @change="onFileSelected"
    />

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000">
      {{ snackbarText }}
    </v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

const props = defineProps<{ invoiceId: number | string }>()

const config = useRuntimeConfig()
const authStore = useAuthStore()
const api = useApi()

type FileInfo = { name: string; size: number; modifiedAt: string }
type SupplierSection = { supplierId: number; supplierName: string; files: FileInfo[] }

const loading = ref(false)
const uploading = ref<string | null>(null)
const piFiles = ref<FileInfo[]>([])
const suppliers = ref<SupplierSection[]>([])

const piCategories = [
  { key: 'customer_pop', label: 'Customer POP', icon: 'mdi-receipt-text-check', color: 'success' },
  { key: 'customer_po', label: 'Customer PO', icon: 'mdi-file-document', color: 'primary' },
  { key: 'our_pi', label: 'Our PI', icon: 'mdi-file-pdf-box', color: 'error' },
  { key: 'quote', label: 'Quote', icon: 'mdi-file-sign', color: 'warning' },
]
const supplierCategories = [
  { key: 'our_pop', label: 'Our POP to Supplier', icon: 'mdi-cash-check', color: 'success' },
  { key: 'supplier_invoice', label: 'Supplier Invoice', icon: 'mdi-invoice-text', color: 'primary' },
]

const categoryToDisplayName: Record<string, string> = {
  'our_pop': 'Our POP',
  'supplier_invoice': 'Supplier Invoice',
  'customer_pop': 'Customer POP',
  'customer_po': 'Customer PO',
  'our_pi': 'Our PI',
  'quote': 'Quote',
}

// ── Pending upload state ──
const fileInputRef = ref<HTMLInputElement | null>(null)
const pendingCategory = ref<string | null>(null)
const pendingSupplierId = ref<number | null>(null)

function piFilesByCategory(category: string): FileInfo[] {
  const displayName = categoryToDisplayName[category] || category.replace('_', ' ')
  const prefix = `${displayName} number`
  return piFiles.value
    .filter(f => f.name.startsWith(prefix))
    .sort((a, b) => sortNumberedFiles(a.name, b.name))
}

function supplierFilesByCategory(s: SupplierSection, category: string): FileInfo[] {
  const displayName = categoryToDisplayName[category] || category.replace('_', ' ')
  const prefix = `${displayName} number`
  return s.files
    .filter(f => f.name.startsWith(prefix))
    .sort((a, b) => sortNumberedFiles(a.name, b.name))
}

function sortNumberedFiles(nameA: string, nameB: string) {
  const nA = parseInt(nameA.match(/\d+/)?. [0] || '0')
  const nB = parseInt(nameB.match(/\d+/)?. [0] || '0')
  return nA - nB
}

const otherPiFiles = computed(() => {
  return piFiles.value.filter(f => !piCategories.some(c => {
    const displayName = categoryToDisplayName[c.key] || c.key.replace('_', ' ')
    return f.name.startsWith(`${displayName} number`)
  }))
})

function otherSupplierFiles(s: SupplierSection) {
  return s.files.filter(f => !supplierCategories.some(c => {
    const displayName = categoryToDisplayName[c.key] || c.key.replace('_', ' ')
    return f.name.startsWith(`${displayName} number`)
  }))
}

async function loadDocuments() {
  loading.value = true
  try {
    const data = await api.get<any>(`/documents/proforma-invoice/${props.invoiceId}`)
    piFiles.value = data.piFiles || []
    suppliers.value = data.suppliers || []
  } catch (e) {
    showSnack('Failed to load documents', 'error')
  } finally {
    loading.value = false
  }
}

function triggerUpload(category: string, supplierId: number | null) {
  pendingCategory.value = category
  pendingSupplierId.value = supplierId
  fileInputRef.value?.click()
}

async function onFileSelected(e: Event) {
  const target = e.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file || !pendingCategory.value) return

  const category = pendingCategory.value
  const supplierId = pendingSupplierId.value
  uploading.value = supplierId ? `${category}-${supplierId}` : category

  try {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('category', category)

    const url = supplierId != null
      ? `/documents/proforma-invoice/${props.invoiceId}/supplier/${supplierId}/upload`
      : `/documents/proforma-invoice/${props.invoiceId}/upload`

    await $fetch(`${config.public.apiBase}${url}`, {
      method: 'POST',
      body: formData,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Uploaded successfully', 'success')
    await loadDocuments()
  } catch (err: any) {
    showSnack(err?.data?.message || 'Upload failed', 'error')
  } finally {
    uploading.value = null
    pendingCategory.value = null
    pendingSupplierId.value = null
    if (target) target.value = ''
  }
}

async function downloadPI(name: string) {
  try {
    const blob = await $fetch<Blob>(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/file`, {
      method: 'GET',
      query: { name },
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    saveBlob(blob, name)
  } catch { showSnack('Download failed', 'error') }
}

async function downloadSupplier(supplierId: number, name: string) {
  try {
    const blob = await $fetch<Blob>(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/supplier/${supplierId}/file`, {
      method: 'GET',
      query: { name },
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    saveBlob(blob, name)
  } catch { showSnack('Download failed', 'error') }
}

async function deletePI(name: string) {
  if (!confirm(`Delete "${name}"?`)) return
  try {
    await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/file`, {
      method: 'DELETE',
      query: { name },
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    showSnack('Deleted', 'success')
    await loadDocuments()
  } catch { showSnack('Delete failed', 'error') }
}

async function deleteSupplier(supplierId: number, name: string) {
  if (!confirm(`Delete "${name}"?`)) return
  try {
    await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${props.invoiceId}/supplier/${supplierId}/file`, {
      method: 'DELETE',
      query: { name },
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
