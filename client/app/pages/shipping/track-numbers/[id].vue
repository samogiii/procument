<template>
  <div>
    <!-- Header -->
    <div class="d-flex align-center gap-3 mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="router.back()" />
      <div>
        <h1 class="text-h5 font-weight-bold">
          <v-icon icon="mdi-barcode-scan" color="primary" size="20" class="mr-1" />
          {{ primaryTrack?.trackNumber || 'Loading…' }}
        </h1>
        <p class="text-caption text-medium-emphasis mt-1">
          {{ primaryTrack?.carrier || 'No carrier' }} · {{ primaryTrack?.warehouseName || '—' }}
        </p>
      </div>
      <v-spacer />
      <div class="d-flex gap-2 align-center flex-wrap">
        <v-chip
          v-for="t in groupTracks"
          :key="t.id"
          size="x-small"
          variant="tonal"
          :color="trackStatusColor(t.status)"
        >
          {{ t.status }}
        </v-chip>
      </div>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <template v-if="primaryTrack">
      <!-- ── Track-level Documents (shared for the whole shipment) ── -->
      <v-card class="mb-4">
        <v-card-title class="text-subtitle-2 pa-4 pb-2 d-flex align-center gap-2">
          <v-icon icon="mdi-file-multiple-outline" color="primary" size="18" />
          Shipment Documents
          <v-chip size="x-small" variant="tonal" class="ml-1">{{ trackLevelDocs.length }}</v-chip>
          <v-spacer />
          <v-btn size="x-small" variant="tonal" color="primary" prepend-icon="mdi-upload" @click="openUpload(null)">
            Upload
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-3">
          <div v-if="!trackLevelDocs.length" class="text-caption text-medium-emphasis">
            No shipment-level documents uploaded yet.
          </div>
          <div v-else class="d-flex flex-wrap gap-2">
            <v-chip
              v-for="doc in trackLevelDocs"
              :key="doc.id"
              size="small"
              variant="tonal"
              :prepend-icon="docPreview.isPreviewable(doc.originalFileName, doc.mimeType) ? 'mdi-eye-outline' : 'mdi-file-outline'"
              closable
              class="cursor-pointer"
              @click="downloadDoc(primaryTrack.id, doc.id, doc.originalFileName, doc.mimeType)"
              @click:close="deleteDoc(primaryTrack.id, doc.id)"
            >
              {{ doc.originalFileName }}
              <span class="text-caption ml-1 opacity-60">{{ (doc.fileSizeBytes / 1024).toFixed(0) }}KB</span>
            </v-chip>
          </div>
        </v-card-text>
      </v-card>

      <!-- ── Received Boxes (shared for the whole shipment) ─────── -->
      <v-card class="mb-4">
        <v-card-title class="text-subtitle-2 pa-4 pb-2 d-flex align-center gap-2">
          <v-icon icon="mdi-package-variant-closed" color="orange" size="18" />
          Received Boxes
          <v-chip size="x-small" color="orange" variant="tonal" class="ml-1">{{ allGroupBoxes.length }}</v-chip>
          <v-spacer />
          <v-btn size="x-small" variant="flat" color="orange" prepend-icon="mdi-plus" @click="openAddBox">
            Add Box
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-3">
          <div v-if="!allGroupBoxes.length" class="text-caption  pa-2">
            No boxes recorded yet. Add a box for each physical package received with this shipment.
          </div>
          <v-table v-else density="compact">
            <thead>
              <tr>
                <th>Box #</th>
                <th>Weight (kg)</th>
                <th>H × W × L (cm)</th>
                <th>Notes</th>
                <th class="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="box in allGroupBoxes" :key="box.id">
                <td class="font-weight-bold">{{ box.boxNumber }}</td>
                <td>{{ box.weightKg ?? '—' }}</td>
                <td class="text-caption">{{ box.heightCm ?? '?' }} × {{ box.widthCm ?? '?' }} × {{ box.lengthCm ?? '?' }}</td>
                <td class="text-caption text-medium-emphasis">{{ box.notes || '—' }}</td>
                <td class="text-right">
                  <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openEditBox(box)" />
                  <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error" @click="deleteBox(box._trackId, box.id)" />
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
      </v-card>

      <!-- ── Parts (one card per PO item in this shipment) ─────── -->
      <div class="text-caption font-weight-bold text-medium-emphasis mb-3 pl-1">
        PARTS IN THIS SHIPMENT ({{ groupTracks.length }})
      </div>

      <v-card
        v-for="t in groupTracks"
        :key="t.id"
        class="mb-4"
        :class="{ 'border-error': t.status === 'Rejected' }"
      >
        <v-card-title class="text-subtitle-2 pa-4 pb-2 d-flex align-center gap-2">
          <v-icon icon="mdi-package-variant" color="primary" size="18" />
          <span class="text-pn font-weight-bold">{{ t.partNumberName || '—' }}</span>
          <span class="text-caption text-medium-emphasis">· PO: {{ t.poNumber || '—' }}</span>
          <v-spacer />
          <v-chip size="x-small" variant="tonal" :color="trackStatusColor(t.status)">{{ t.status }}</v-chip>
          <v-btn
            size="x-small"
            variant="tonal"
            color="error"
            prepend-icon="mdi-close-circle"
            :disabled="t.status === 'Rejected'"
            @click="confirmReject(t)"
          >
            Reject
          </v-btn>
          <v-btn
            size="x-small"
            variant="flat"
            color="success"
            prepend-icon="mdi-content-save"
            :loading="savingMap[t.id]"
            @click="submitPart(t)"
          >
            Save
          </v-btn>
        </v-card-title>
        <v-divider />

        <v-card-text class="pa-4">
          <!-- Qty entry -->
          <v-row dense class="mb-2">
            <v-col cols="12" sm="6">
              <v-text-field
                v-model.number="partForms[t.id].expectedQty"
                label="Expected Qty"
                type="number"
                variant="outlined"
                density="compact"
                min="0"
                :hint="siblingActualQty(t) > 0
                  ? `PO total: ${t.poItemQty} · received in other tracks: ${siblingActualQty(t)}`
                  : `PO ordered qty: ${t.poItemQty}`"
                persistent-hint
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field
                v-model.number="partForms[t.id].actualQty"
                label="Actual Qty (received)"
                type="number"
                variant="outlined"
                density="compact"
                min="0"
                :error="hasQtyMismatch(t)"
                :persistent-hint="hasQtyMismatch(t)"
                :hint="hasQtyMismatch(t) ? `⚠ Mismatch — expected ${partForms[t.id].expectedQty}, got ${partForms[t.id].actualQty}` : ''"
              />
            </v-col>
          </v-row>

          <!-- Alerts -->
          <v-alert
            v-if="hasQtyMismatch(t)"
            type="warning"
            density="compact"
            class="mb-2"
            variant="tonal"
          >
            Quantity mismatch: expected <strong>{{ partForms[t.id].expectedQty }}</strong>, received <strong>{{ partForms[t.id].actualQty }}</strong>.
            This will be flagged for admin review.
          </v-alert>

          <v-alert
            v-if="shortfall(t) > 0"
            type="info"
            density="compact"
            class="mb-2"
            variant="tonal"
            icon="mdi-truck-plus-outline"
          >
            <div class="font-weight-medium">{{ shortfall(t) }} unit(s) not received in this shipment.</div>
            <div class="text-caption">
              Ask the admin to add a new track number for the remaining <strong>{{ shortfall(t) }}</strong> unit(s). The new track's Expected Qty will be pre-filled with <strong>{{ shortfall(t) }}</strong>.
            </div>
          </v-alert>

          <!-- Review status from Admin -->
          <div v-if="t.items?.[0]" class="mt-2">
            <v-divider class="mb-2" />
            <div class="text-caption font-weight-bold text-medium-emphasis mb-1">REVIEW STATUS</div>
            <v-alert
              v-if="t.items[0].status === 'Rejected'"
              type="error"
              density="compact"
              variant="tonal"
              class="mb-2"
              :icon="false"
            >
              <div class="d-flex align-center gap-2">
                <v-icon icon="mdi-close-circle" size="18" />
                <div>
                  <div class="font-weight-medium">Part Rejected</div>
                  <div v-if="t.items[0].reviewNote" class="text-caption mt-1">
                    Reason: <strong>{{ t.items[0].reviewNote }}</strong>
                  </div>
                  <div v-if="t.items[0].reviewedAt" class="text-caption opacity-70">
                    {{ new Date(t.items[0].reviewedAt).toLocaleDateString() }}
                  </div>
                </div>
              </div>
            </v-alert>
            <div v-else class="d-flex align-center gap-2 flex-wrap">
              <v-chip
                :color="t.items[0].status === 'Accepted' ? 'success' : 'warning'"
                size="small"
                variant="tonal"
              >
                <v-icon
                  :icon="t.items[0].status === 'Accepted' ? 'mdi-check-circle' : 'mdi-clock-outline'"
                  size="14"
                  class="mr-1"
                />
                {{ t.items[0].status }}
              </v-chip>
              <span v-if="t.items[0].reviewNote" class="text-caption text-medium-emphasis">
                Note: {{ t.items[0].reviewNote }}
              </span>
              <span v-if="t.items[0].reviewedAt" class="text-caption text-medium-emphasis ml-auto">
                Reviewed {{ new Date(t.items[0].reviewedAt).toLocaleDateString() }}
              </span>
            </div>
          </div>

          <!-- Part-level docs -->
          <v-divider class="my-3" />
          <div class="d-flex align-center mb-2">
            <v-icon icon="mdi-paperclip" size="14" class="mr-1 text-medium-emphasis" />
            <span class="text-caption font-weight-bold text-medium-emphasis">PART DOCUMENTS</span>
            <v-spacer />
            <v-btn size="x-small" variant="tonal" color="primary" prepend-icon="mdi-upload" @click="openUpload(t.poItemId, t.id)">
              Upload
            </v-btn>
          </div>
          <div v-if="!partDocs(t).length" class="text-caption text-medium-emphasis">No part documents yet.</div>
          <v-list v-else density="compact" class="pa-0">
            <v-list-item
              v-for="doc in partDocs(t)"
              :key="doc.id"
              :title="doc.originalFileName"
              :subtitle="`${(doc.fileSizeBytes / 1024).toFixed(1)} KB · ${new Date(doc.uploadedAt).toLocaleDateString()}`"
              rounded="lg"
            >
              <template #append>
                <v-btn
                  :icon="docPreview.isPreviewable(doc.originalFileName, doc.mimeType) ? 'mdi-eye-outline' : 'mdi-download'"
                  size="x-small" variant="text" color="primary"
                  @click="downloadDoc(t.id, doc.id, doc.originalFileName, doc.mimeType)"
                />
                <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error" @click="deleteDoc(t.id, doc.id)" />
              </template>
            </v-list-item>
          </v-list>
        </v-card-text>
      </v-card>
    </template>

    <!-- Box Dialog -->
    <v-dialog v-model="boxDialog" max-width="480" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">
          <v-icon icon="mdi-package-variant-closed" color="orange" class="mr-2" />
          {{ editingBox ? 'Edit Box' : 'Add Received Box' }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="4">
              <v-text-field v-model.number="boxForm.boxNumber" label="Box #" type="number" variant="outlined" density="compact" min="1" />
            </v-col>
            <v-col cols="8">
              <v-text-field v-model.number="boxForm.weightKg" label="Weight (kg)" type="number" variant="outlined" density="compact" clearable />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="boxForm.heightCm" label="Height (cm)" type="number" variant="outlined" density="compact" clearable />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="boxForm.widthCm" label="Width (cm)" type="number" variant="outlined" density="compact" clearable />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="boxForm.lengthCm" label="Length (cm)" type="number" variant="outlined" density="compact" clearable />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="boxForm.notes" label="Notes" variant="outlined" density="compact" rows="2" auto-grow />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="boxDialog = false">Cancel</v-btn>
          <v-btn color="orange" variant="flat" :loading="savingBox" @click="saveBox">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Upload Dialog -->
    <v-dialog v-model="uploadDialog" max-width="440" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">
          {{ uploadPoItemId != null ? 'Upload Part Document' : 'Upload Shipment Document' }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-file-input
            v-model="uploadFile"
            label="Select file"
            variant="outlined"
            density="compact"
            prepend-icon="mdi-paperclip"
            accept="image/*,application/pdf,.pdf,.png,.jpg,.jpeg"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="uploadDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="uploading" :disabled="!uploadFile" @click="doUpload">Upload</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Reject Confirm -->
    <v-dialog v-model="rejectDialog" max-width="400" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4">Reject This Part?</v-card-title>
        <v-card-text class="pa-4 pt-0 text-body-2">
          <strong>{{ rejectTarget?.partNumberName }}</strong> will be marked <strong>Rejected</strong> and the PO will be set to <strong>Issue</strong>.
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="rejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="rejecting" @click="doReject">Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snack" :color="snackColor" timeout="3000" location="top right">{{ snackMsg }}</v-snackbar>

    <!-- Document Preview Modal -->
    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const api = useApi()
const docPreview = useDocPreview()
const config = useRuntimeConfig()
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const trackId = computed(() => Number(route.params.id))

const allTracks = ref<any[]>([])
const loading = ref(false)

// The track matching the route param — used as "primary" for shared operations
const primaryTrack = computed(() =>
  allTracks.value.find(t => t.id === trackId.value) ?? null
)

// All tracks in the same shipment group (same trackNumber + warehouseId)
const groupTracks = computed(() => {
  if (!primaryTrack.value) return []
  return allTracks.value.filter(
    t => t.trackNumber === primaryTrack.value.trackNumber &&
         t.warehouseId === primaryTrack.value.warehouseId
  )
})

// Track-level docs come from the primary track (shared for the shipment)
const trackLevelDocs = computed(() =>
  (primaryTrack.value?.documents || []).filter((d: any) => d.poItemId == null)
)

// All boxes from all tracks in the group (same physical shipment)
const allGroupBoxes = computed(() => {
  const boxes: any[] = []
  for (const t of groupTracks.value) {
    for (const b of (t.boxes || [])) {
      boxes.push({ ...b, _trackId: t.id })
    }
  }
  return boxes.sort((a, b) => a.boxNumber - b.boxNumber)
})

// Per-part docs (part-level docs filtered by poItemId)
function partDocs(t: any) {
  return (t.documents || []).filter((d: any) => d.poItemId === t.poItemId)
}

// ── Part forms (one per track in group) ──────────────────────────────────────

const partForms = ref<Record<number, { expectedQty: number | null, actualQty: number | null }>>({})
const savingMap = ref<Record<number, boolean>>({})

function siblingActualQty(t: any): number {
  // Sum actualQty from OTHER track numbers (outside this group) for the same POItem
  return allTracks.value
    .filter(x => x.poItemId === t.poItemId && x.trackNumber !== primaryTrack.value?.trackNumber)
    .reduce((sum: number, x: any) => sum + (x.items?.[0]?.actualQty ?? 0), 0)
}

function defaultExpectedQty(t: any): number | null {
  const remaining = (t.poItemQty ?? 0) - siblingActualQty(t)
  return Math.max(0, remaining)
}

function hasQtyMismatch(t: any): boolean {
  const f = partForms.value[t.id]
  if (!f || f.actualQty == null || f.expectedQty == null) return false
  return f.actualQty !== f.expectedQty
}

function shortfall(t: any): number {
  const f = partForms.value[t.id]
  if (!f || f.actualQty == null || f.expectedQty == null) return 0
  return Math.max(0, f.expectedQty - f.actualQty)
}

function initPartForms() {
  const forms: typeof partForms.value = {}
  const saving: typeof savingMap.value = {}
  for (const t of groupTracks.value) {
    const item = t.items?.[0]
    forms[t.id] = {
      expectedQty: item?.expectedQty ?? defaultExpectedQty(t),
      actualQty: item?.actualQty ?? null,
    }
    saving[t.id] = false
  }
  partForms.value = forms
  savingMap.value = saving
}

// ── Submit a single part ──────────────────────────────────────────────────────

async function submitPart(t: any) {
  const f = partForms.value[t.id]
  if (!f) return
  savingMap.value[t.id] = true
  try {
    await api.post(`/shipping/track-numbers/${t.id}/items`, {
      items: [{
        poItemId: t.poItemId,
        expectedQty: f.expectedQty ?? 0,
        actualQty: f.actualQty,
        isAvailable: (f.actualQty ?? 0) > 0,
      }],
    })
    notify('Saved')
    await loadTracks()
  } catch {
    notify('Failed to save', 'error')
  } finally {
    savingMap.value[t.id] = false
  }
}

// ── Reject ────────────────────────────────────────────────────────────────────

const rejectDialog = ref(false)
const rejectTarget = ref<any>(null)
const rejecting = ref(false)

function confirmReject(t: any) {
  rejectTarget.value = t
  rejectDialog.value = true
}

async function doReject() {
  if (!rejectTarget.value) return
  rejecting.value = true
  try {
    await api.post(`/shipping/track-numbers/${rejectTarget.value.id}/reject`, {})
    notify('Rejected')
    rejectDialog.value = false
    await loadTracks()
  } catch {
    notify('Failed to reject', 'error')
  } finally {
    rejecting.value = false
  }
}

// ── Boxes ─────────────────────────────────────────────────────────────────────

const boxDialog = ref(false)
const editingBox = ref<any>(null)
const savingBox = ref(false)
const boxForm = reactive({
  boxNumber: 1,
  weightKg: null as number | null,
  heightCm: null as number | null,
  widthCm: null as number | null,
  lengthCm: null as number | null,
  notes: '',
})

function openAddBox() {
  editingBox.value = null
  Object.assign(boxForm, {
    boxNumber: allGroupBoxes.value.length + 1,
    weightKg: null, heightCm: null, widthCm: null, lengthCm: null, notes: '',
  })
  boxDialog.value = true
}

function openEditBox(box: any) {
  editingBox.value = box
  Object.assign(boxForm, {
    boxNumber: box.boxNumber,
    weightKg: box.weightKg,
    heightCm: box.heightCm,
    widthCm: box.widthCm,
    lengthCm: box.lengthCm,
    notes: box.notes || '',
  })
  boxDialog.value = true
}

async function saveBox() {
  savingBox.value = true
  // Always save to primary track (shared for the whole shipment)
  const targetId = editingBox.value?._trackId ?? primaryTrack.value?.id
  try {
    if (editingBox.value) {
      await api.put(`/shipping/track-numbers/${targetId}/boxes/${editingBox.value.id}`, { ...boxForm })
      notify('Box updated')
    } else {
      await api.post(`/shipping/track-numbers/${targetId}/boxes`, { ...boxForm })
      notify('Box added')
    }
    boxDialog.value = false
    await loadTracks()
  } catch {
    notify('Failed to save box', 'error')
  } finally {
    savingBox.value = false
  }
}

async function deleteBox(trackIdForBox: number, boxId: number) {
  try {
    await api.delete(`/shipping/track-numbers/${trackIdForBox}/boxes/${boxId}`)
    notify('Box removed')
    await loadTracks()
  } catch {
    notify('Failed to delete box', 'error')
  }
}

// ── Documents ─────────────────────────────────────────────────────────────────

const uploadDialog = ref(false)
const uploadFile = ref<File | null>(null)
const uploading = ref(false)
const uploadPoItemId = ref<number | null>(null)
const uploadTrackId = ref<number | null>(null)

function openUpload(poItemId: number | null, trackIdForUpload?: number) {
  uploadPoItemId.value = poItemId
  uploadTrackId.value = trackIdForUpload ?? primaryTrack.value?.id ?? null
  uploadFile.value = null
  uploadDialog.value = true
}

async function doUpload() {
  if (!uploadFile.value || !uploadTrackId.value) return
  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', uploadFile.value as Blob)
    const url = uploadPoItemId.value != null
      ? `/shipping/track-numbers/${uploadTrackId.value}/parts/${uploadPoItemId.value}/documents`
      : `/shipping/track-numbers/${uploadTrackId.value}/documents`
    await $fetch(`${api.baseURL}${url}`, {
      method: 'POST',
      body: formData,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    notify('Uploaded')
    uploadDialog.value = false
    await loadTracks()
  } catch {
    notify('Upload failed', 'error')
  } finally {
    uploading.value = false
  }
}

function downloadDoc(tid: number, docId: number, fileName = 'document', mimeType?: string) {
  docPreview.preview(`/shipping/documents/${docId}/file`, fileName, mimeType)
}

async function deleteDoc(tid: number, docId: number) {
  try {
    await api.del(`/shipping/documents/${docId}`)
    notify('Deleted')
    await loadTracks()
  } catch {
    notify('Delete failed', 'error')
  }
}

// ── Status color ──────────────────────────────────────────────────────────────

function trackStatusColor(status: string) {
  const map: Record<string, string> = {
    'Ship to Warehouse': 'blue-grey',
    'Received in Warehouse': 'orange',
    'Waiting for Packing': 'amber',
    'Ship To USA': 'blue',
    'Clearing Customs': 'deep-purple',
    'Received in Office': 'teal',
    'Delivered to Customer': 'success',
    'Rejected': 'error',
  }
  return map[status] ?? 'default'
}

// ── Snack ─────────────────────────────────────────────────────────────────────

const snack = ref(false)
const snackMsg = ref('')
const snackColor = ref('success')
function notify(msg: string, color = 'success') {
  snackMsg.value = msg; snackColor.value = color; snack.value = true
}

// ── Load ──────────────────────────────────────────────────────────────────────

async function loadTracks() {
  loading.value = true
  try {
    allTracks.value = await api.get('/shipping/track-numbers')
    if (!primaryTrack.value) {
      notify('Track not found', 'error')
      return
    }
    initPartForms()
  } catch {
    notify('Failed to load', 'error')
  } finally {
    loading.value = false
  }
}

onMounted(loadTracks)
</script>

