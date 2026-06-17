<template>
  <div>
    <!-- Header -->
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="navigateTo('/ils')" />
      <div>
        <h1 class="text-h5 font-weight-bold" style="font-family: monospace;">
          {{ item?.partNumberName || 'ILS Item' }}
        </h1>
        <p class="text-caption text-medium-emphasis mb-0">
          {{ item?.description || 'Serial Numbers' }}
        </p>
      </div>
      <v-chip v-if="item?.condition" size="small" variant="tonal" :color="conditionColor(item.condition)" class="ml-2">
        {{ item.condition }}
      </v-chip>
      <v-spacer />
      <v-chip size="small" variant="tonal" color="primary" prepend-icon="mdi-barcode">
        {{ serials.length }} serial{{ serials.length === 1 ? '' : 's' }}
      </v-chip>
      <v-btn color="primary" prepend-icon="mdi-plus" size="small" @click="openAddDialog">
        Add Serial
      </v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <v-data-table
          :headers="headers"
          :items="serials"
          :loading="loading"
          :items-per-page="50"
          hover
          density="comfortable"
        >
          <template #item.condition="{ item: s }">
            <v-chip v-if="s.condition" size="small" variant="tonal" :color="conditionColor(s.condition)">
              {{ s.condition }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.tagDate="{ item: s }">
            {{ s.tagDate ? new Date(s.tagDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.cert="{ item: s }">
            <div class="d-flex align-center gap-2">
              <span v-if="s.certText">{{ s.certText }}</span>
              <span v-else-if="!s.hasCertImage" class="text-medium-emphasis">—</span>
              <img
                v-if="s.hasCertImage"
                :src="imageUrls[`${s.id}-cert`]"
                class="thumb"
                alt="cert"
                @click="previewImage(s.id, 'cert')"
              />
            </div>
          </template>
          <template #item.price="{ item: s }">
            <span v-if="s.price != null" class="font-weight-medium" style="font-family: monospace; color: #4ade80;">
              ${{ formatPrice(s.price) }}
            </span>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.partImage="{ item: s }">
            <img
              v-if="s.hasPartImage"
              :src="imageUrls[`${s.id}-part`]"
              class="thumb"
              alt="part"
              @click="previewImage(s.id, 'part')"
            />
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.location="{ item: s }">
            <v-chip v-if="s.location" size="x-small" variant="tonal" :color="s.location === 'Shop' ? 'warning' : 'info'">
              {{ s.location }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.actions="{ item: s }">
            <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openEditDialog(s)" />
            <v-btn
              v-if="isAdmin"
              icon="mdi-delete"
              size="x-small"
              variant="text"
              color="error"
              @click="confirmDelete(s)"
            />
          </template>
          <template #no-data>
            <div class="text-center py-6 text-medium-emphasis">
              No serials yet — click "Add Serial".
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Add / Edit Dialog -->
    <v-dialog v-model="showDialog" max-width="680" persistent>
      <v-card>
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon :icon="isEditing ? 'mdi-pencil' : 'mdi-plus'" class="mr-2" size="20" />
          {{ isEditing ? 'Edit Serial' : 'Add Serial' }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.serialNumber" label="Serial Number *" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="form.location" :items="locationOptions" label="Location" variant="outlined" density="compact" hide-details clearable />
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="form.condition" :items="conditionOptions" label="Condition" variant="outlined" density="compact" hide-details clearable />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.tagDate" label="Tag Date" type="date" :max="today" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model.number="form.price" label="Price ($)" type="number" step="0.01" min="0" prefix="$" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.leadTime" label="Lead Time" placeholder="e.g. 5 days" variant="outlined" density="compact" hide-details />
            </v-col>

            <!-- Cert: text and/or image (at least one) -->
            <v-col cols="12">
              <v-divider class="my-2" />
              <p class="text-caption text-medium-emphasis mb-1">
                Cert — provide text and/or an image (at least one required)
              </p>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.certText" label="Cert Text" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-file-input
                v-model="form.certFile"
                label="Cert Image"
                accept="image/*"
                variant="outlined"
                density="compact"
                prepend-icon="mdi-certificate"
                hide-details
              />
              <div v-if="isEditing && editingSerial?.hasCertImage" class="d-flex align-center gap-2 mt-1">
                <img :src="imageUrls[`${editingSerial.id}-cert`]" class="thumb" alt="cert" @click="previewImage(editingSerial.id, 'cert')" />
                <v-btn size="x-small" variant="text" color="error" @click="removeImage(editingSerial.id, 'cert')">Remove</v-btn>
              </div>
            </v-col>

            <!-- Part image -->
            <v-col cols="12">
              <v-divider class="my-2" />
            </v-col>
            <v-col cols="12" md="6">
              <v-file-input
                v-model="form.partFile"
                label="Part Image"
                accept="image/*"
                variant="outlined"
                density="compact"
                prepend-icon="mdi-image"
                hide-details
              />
              <div v-if="isEditing && editingSerial?.hasPartImage" class="d-flex align-center gap-2 mt-1">
                <img :src="imageUrls[`${editingSerial.id}-part`]" class="thumb" alt="part" @click="previewImage(editingSerial.id, 'part')" />
                <v-btn size="x-small" variant="text" color="error" @click="removeImage(editingSerial.id, 'part')">Remove</v-btn>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.notes" label="Notes" variant="outlined" density="compact" hide-details />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-3">
          <span v-if="!certProvided" class="text-caption text-error">Cert text or image required</span>
          <v-spacer />
          <v-btn variant="text" @click="showDialog = false">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="saving"
            :disabled="!form.serialNumber || !certProvided"
            @click="saveSerial"
          >
            {{ isEditing ? 'Update' : 'Add' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirm -->
    <v-dialog v-model="showDeleteConfirm" max-width="400">
      <v-card>
        <v-card-title class="pa-4 pb-2">Delete Serial?</v-card-title>
        <v-card-text>
          Remove serial <strong>{{ deleteTarget?.serialNumber }}</strong>? This cannot be undone.
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showDeleteConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleteSaving" @click="doDelete">Delete</v-btn>
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

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()
const isAdmin = computed(() => authStore.isAdmin)

const ilsItemId = Number(route.params.id)
const today = new Date().toISOString().split('T')[0]

const conditionOptions = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']
const locationOptions = ['Shop', 'Warehouse']

const item = ref<any>(null)
const serials = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)

// blob-url cache for thumbnails / preview, keyed by `${serialId}-${kind}`
const imageUrls = reactive<Record<string, string>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

const headers = [
  { title: 'Serial #', key: 'serialNumber', width: '160px' },
  { title: 'Condition', key: 'condition', width: '100px' },
  { title: 'Tag Date', key: 'tagDate', width: '110px' },
  { title: 'Lead Time', key: 'leadTime', width: '110px' },
  { title: 'Cert', key: 'cert' },
  { title: 'Price', key: 'price', width: '100px' },
  { title: 'Part Image', key: 'partImage', width: '90px', sortable: false },
  { title: 'Location', key: 'location', width: '110px' },
  { title: '', key: 'actions', width: '90px', sortable: false },
]

function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error',
    RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

function formatPrice(v: number) {
  return Number(v || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// ── Image blob helpers (auth header can't ride on <img src>, so fetch + objectURL) ──
async function fetchImageBlob(serialId: number, kind: 'cert' | 'part'): Promise<Blob | null> {
  try {
    return await $fetch<Blob>(`${api.baseURL}/ils/serials/${serialId}/${kind}-image`, {
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
      responseType: 'blob',
    })
  } catch {
    return null
  }
}

async function loadThumb(serialId: number, kind: 'cert' | 'part') {
  const key = `${serialId}-${kind}`
  if (imageUrls[key]) return
  const blob = await fetchImageBlob(serialId, kind)
  if (blob) imageUrls[key] = URL.createObjectURL(blob)
}

function revokeAll() {
  for (const k of Object.keys(imageUrls)) {
    URL.revokeObjectURL(imageUrls[k])
    delete imageUrls[k]
  }
}

async function previewImage(serialId: number, kind: 'cert' | 'part') {
  const blob = await fetchImageBlob(serialId, kind)
  if (!blob) { showSnack('Failed to load image', 'error'); return }
  docPreview.previewBlob(blob, `${kind}-${serialId}.jpg`)
}

// ── Loading ──
async function loadItem() {
  try {
    item.value = await api.get<any>(`/ils/${ilsItemId}`)
  } catch {
    showSnack('Failed to load item', 'error')
  }
}

async function loadSerials() {
  loading.value = true
  try {
    serials.value = await api.get<any[]>(`/ils/${ilsItemId}/serials`)
    revokeAll()
    for (const s of serials.value) {
      if (s.hasCertImage) loadThumb(s.id, 'cert')
      if (s.hasPartImage) loadThumb(s.id, 'part')
    }
  } catch {
    showSnack('Failed to load serials', 'error')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadItem()
  loadSerials()
})
onBeforeUnmount(revokeAll)

// ── Add / Edit ──
const showDialog = ref(false)
const isEditing = ref(false)
const editingSerial = ref<any>(null)

const defaultForm = () => ({
  serialNumber: '',
  condition: '' as string,
  tagDate: '',
  leadTime: '',
  certText: '',
  price: null as number | null,
  location: '' as string,
  notes: '',
  certFile: null as File | null,
  partFile: null as File | null,
})
const form = ref(defaultForm())

const certProvided = computed(() =>
  !!form.value.certText?.trim() ||
  !!form.value.certFile ||
  (isEditing.value && !!editingSerial.value?.hasCertImage)
)

function openAddDialog() {
  isEditing.value = false
  editingSerial.value = null
  form.value = defaultForm()
  showDialog.value = true
}

function openEditDialog(s: any) {
  isEditing.value = true
  editingSerial.value = s
  form.value = {
    serialNumber: s.serialNumber || '',
    condition: s.condition || '',
    tagDate: s.tagDate ? new Date(s.tagDate).toISOString().split('T')[0] : '',
    leadTime: s.leadTime || '',
    certText: s.certText || '',
    price: s.price ?? null,
    location: s.location || '',
    notes: s.notes || '',
    certFile: null,
    partFile: null,
  }
  showDialog.value = true
}

async function uploadImage(serialId: number, kind: 'cert' | 'part', file: File) {
  const fd = new FormData()
  fd.append('file', file)
  await $fetch(`${api.baseURL}/ils/serials/${serialId}/${kind}-image`, {
    method: 'POST',
    body: fd,
    headers: { Authorization: `Bearer ${authStore.user?.token}` },
  })
}

async function saveSerial() {
  if (!form.value.serialNumber || !certProvided.value) return
  saving.value = true
  try {
    const payload = {
      id: isEditing.value ? editingSerial.value.id : null,
      ilsItemId,
      serialNumber: form.value.serialNumber.trim(),
      leadTime: form.value.leadTime || null,
      certText: form.value.certText || null,
      price: form.value.price,
      location: form.value.location || null,
      condition: form.value.condition || null,
      tagDate: form.value.tagDate || null,
      notes: form.value.notes || null,
    }
    const saved = await api.post<any>('/ils/serials', payload)

    if (form.value.certFile) await uploadImage(saved.id, 'cert', form.value.certFile)
    if (form.value.partFile) await uploadImage(saved.id, 'part', form.value.partFile)

    showSnack(isEditing.value ? 'Serial updated' : 'Serial added', 'success')
    showDialog.value = false
    await Promise.all([loadSerials(), loadItem()])
  } catch (e: any) {
    showSnack(e?.data?.error || e?.data?.message || 'Failed to save serial', 'error')
  } finally {
    saving.value = false
  }
}

async function removeImage(serialId: number, kind: 'cert' | 'part') {
  try {
    const updated = await api.del<any>(`/ils/serials/${serialId}/${kind}-image`)
    const key = `${serialId}-${kind}`
    if (imageUrls[key]) { URL.revokeObjectURL(imageUrls[key]); delete imageUrls[key] }
    if (editingSerial.value?.id === serialId) editingSerial.value = updated
    const idx = serials.value.findIndex(s => s.id === serialId)
    if (idx >= 0) serials.value[idx] = updated
    showSnack('Image removed')
  } catch {
    showSnack('Failed to remove image', 'error')
  }
}

// ── Delete ──
const showDeleteConfirm = ref(false)
const deleteTarget = ref<any>(null)
const deleteSaving = ref(false)

function confirmDelete(s: any) {
  deleteTarget.value = s
  showDeleteConfirm.value = true
}

async function doDelete() {
  if (!deleteTarget.value) return
  deleteSaving.value = true
  try {
    await api.del(`/ils/serials/${deleteTarget.value.id}`)
    showSnack('Serial deleted')
    showDeleteConfirm.value = false
    deleteTarget.value = null
    await Promise.all([loadSerials(), loadItem()])
  } catch {
    showSnack('Failed to delete serial', 'error')
  } finally {
    deleteSaving.value = false
  }
}
</script>

<style scoped>
.thumb {
  width: 36px;
  height: 36px;
  object-fit: cover;
  border-radius: 6px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.15);
  cursor: pointer;
  transition: transform 0.12s;
}
.thumb:hover { transform: scale(1.08); }
</style>
