<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/purchase-orders" class="mr-1 flex-shrink-0" size="small" />
      <h1 class="text-h6 text-sm-h5 font-weight-bold">PO {{ po.poNumber || `#${route.params.id}` }}</h1>
      <v-spacer />
      <v-menu v-if="isAdmin">
        <template #activator="{ props: menuProps }">
          <v-chip
            :color="poStatusColor"
            v-bind="menuProps"
            class="cursor-pointer"
            append-icon="mdi-chevron-down"
            size="default"
          >
            {{ po.status || '—' }}
          </v-chip>
        </template>
        <v-list density="compact" style="min-width: 160px">
          <v-list-subheader>Change Status</v-list-subheader>
          <v-list-item
            v-for="s in poStatuses"
            :key="s.value"
            :value="s.value"
            :active="po.status === s.value"
            @click="onStatusSelect(s.value)"
          >
            <template #prepend>
              <v-icon :icon="s.icon" :color="s.color" size="18" />
            </template>
            <v-list-item-title>{{ s.label }}</v-list-item-title>
          </v-list-item>
        </v-list>
      </v-menu>
      <v-chip v-else :color="poStatusColor" size="default">{{ po.status || '—' }}</v-chip>
      <v-btn prepend-icon="mdi-file-pdf-box" size="small" color="error" @click="showPdf = true">PDF</v-btn>
    </div>

    <v-row class="mb-6">
      <v-col cols="12" md="4">
        <StatCard icon="mdi-truck-delivery" color="primary" label="Supplier" :value="po.supplierName" />
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-currency-usd" color="success" label="Total Amount">
          ${{ po.totalAmount?.toLocaleString() || '0' }}
        </StatCard>
      </v-col>
      <v-col cols="12" md="4">
        <StatCard icon="mdi-file-document-outline" color="info" label="Proforma Invoice" :value="po.invoiceNumber || '—'" />
      </v-col>
    </v-row>

    <!-- ── Import Details ── -->
    <v-card class="glass-card mb-6">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-bank" class="mr-2" size="20" />
        Import Details
        <v-spacer />
        <v-btn
          v-if="!editingImport"
          variant="tonal"
          size="small"
          prepend-icon="mdi-pencil"
          @click="editingImport = true"
        >Edit</v-btn>
        <template v-else>
          <v-btn variant="text" size="small" class="mr-1" @click="cancelImportEdit">Cancel</v-btn>
          <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-content-save" :loading="savingImport" @click="saveImport">Save</v-btn>
        </template>
      </v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="4">
            <v-text-field v-model="importForm.bankName" label="Bank Name" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="importForm.bankAccountNumber" label="Account Number" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="importForm.bankAddress" label="Bank Address" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.bankCity" label="Bank City" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.bankCountry" label="Bank Country" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.fedExAccount" label="FedEx Account" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="importForm.courierName" label="Courier Name" variant="outlined" density="compact" hide-details :readonly="!editingImport" />
          </v-col>
          <v-col cols="12">
            <v-textarea v-model="importForm.notes" label="Notes" variant="outlined" density="compact" hide-details rows="2" auto-grow :readonly="!editingImport" />
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <!-- ── Line Items with Track Numbers ── -->
    <v-card class="glass-card mb-6">
      <v-card-title>
        <v-icon icon="mdi-package-variant-closed" class="mr-2" size="20" />
        Line Items &amp; Tracking
      </v-card-title>
      <v-card-text>
        <v-table density="comfortable">
          <thead>
            <tr>
              <th style="width:40px;"></th>
              <th>Part</th>
              <th>Qty</th>
              <th>Unit Price</th>
              <th>Total</th>
              <th>Condition</th>
              <th style="width:60px;">Tracks</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(item, idx) in (po.items || [])" :key="item.id">
              <tr>
                <td>
                  <v-btn
                    icon
                    size="x-small"
                    variant="text"
                    @click="toggleItemExpand(item.id)"
                  >
                    <v-icon :icon="expandedItems.has(item.id) ? 'mdi-chevron-up' : 'mdi-chevron-down'" />
                  </v-btn>
                </td>
                <td class="font-weight-medium">{{ item.partNumberName || '—' }}</td>
                <td>{{ item.qty }}</td>
                <td>${{ item.unitPrice?.toFixed(2) }}</td>
                <td class="font-weight-bold">${{ item.totalPrice?.toFixed(2) }}</td>
                <td>{{ item.condition || '—' }}</td>
                <td>
                  <v-chip size="x-small" color="primary" variant="tonal">
                    {{ (item.trackNumbers || []).length }}
                  </v-chip>
                </td>
              </tr>
              <!-- Expanded Track Numbers -->
              <tr v-if="expandedItems.has(item.id)">
                <td :colspan="7" class="pa-0">
                  <div style="background: rgba(var(--v-theme-surface-variant), 0.08); padding: 12px 16px 12px 48px;">
                    <div class="d-flex align-center mb-2">
                      <span class="text-caption text-medium-emphasis font-weight-bold">TRACKING NUMBERS</span>
                      <v-spacer />
                      <v-btn size="x-small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="openAddTrack(item.id)">Add</v-btn>
                    </div>
                    <div v-if="!(item.trackNumbers || []).length" class="text-body-2 text-medium-emphasis py-2">
                      No tracking numbers yet.
                    </div>
                    <v-table v-else density="compact" class="bg-transparent">
                      <thead>
                        <tr>
                          <th>Track Number</th>
                          <th>Carrier</th>
                          <th>Notes</th>
                          <th>Added</th>
                          <th style="width:40px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="t in item.trackNumbers" :key="t.id">
                          <td class="font-weight-medium" style="color: #60a5fa;">{{ t.trackNumber }}</td>
                          <td>{{ t.carrier || '—' }}</td>
                          <td class="text-medium-emphasis">{{ t.notes || '—' }}</td>
                          <td class="text-caption">{{ t.createdAt ? new Date(t.createdAt).toLocaleDateString() : '—' }}</td>
                          <td>
                            <v-btn icon size="x-small" variant="text" color="error" @click="deleteTrack(item, t.id)">
                              <v-icon icon="mdi-delete-outline" size="16" />
                            </v-btn>
                          </td>
                        </tr>
                      </tbody>
                    </v-table>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <!-- Add Track Number Dialog -->
    <v-dialog v-model="showAddTrackDialog" max-width="450" persistent>
      <v-card>
        <v-card-title class="text-h6">Add Tracking Number</v-card-title>
        <v-card-text>
          <v-text-field v-model="trackForm.trackNumber" label="Track Number" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="trackForm.carrier" label="Carrier (e.g. FedEx, DHL)" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model="trackForm.notes" label="Notes" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showAddTrackDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="savingTrack" :disabled="!trackForm.trackNumber.trim()" @click="addTrack">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Rejection Note Dialog -->
    <v-dialog v-model="showRejectDialog" max-width="450" persistent>
      <v-card>
        <v-card-title class="text-h6">Reject Purchase Order</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">Please provide a reason for rejecting this PO:</p>
          <v-textarea
            v-model="rejectionNote"
            label="Rejection Reason"
            variant="outlined"
            rows="3"
            auto-grow
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showRejectDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" @click="confirmReject">Reject</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>

    <PoPdfGenerator v-model="showPdf" :po-id="String(route.params.id)" />
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const authStore = useAuthStore()
const po = ref<any>({})
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const poStatuses = [
  { value: 'Draft', label: 'Draft', icon: 'mdi-file-edit-outline', color: 'grey' },
  { value: 'Sent', label: 'Sent', icon: 'mdi-send', color: 'info' },
  { value: 'Accepted', label: 'Accepted', icon: 'mdi-check-circle', color: 'success' },
  { value: 'Completed', label: 'Completed', icon: 'mdi-check-all', color: 'teal' },
  { value: 'Rejected', label: 'Rejected', icon: 'mdi-close-circle-outline', color: 'error' },
  { value: 'Cancelled', label: 'Cancelled', icon: 'mdi-cancel', color: 'grey' },
]

const isAdmin = computed(() => authStore.isAdmin)
const showPdf = ref(false)

const poStatusColor = computed(() => {
  const found = poStatuses.find(s => s.value === po.value.status)
  return found?.color || 'grey'
})

// ── Import Details ──
const editingImport = ref(false)
const savingImport = ref(false)
const importForm = ref<any>({
  bankName: '', bankAccountNumber: '', bankAddress: '',
  bankCity: '', bankCountry: '',
  fedExAccount: '', courierName: '', notes: '',
})
const importOriginal = ref<any>({})

async function loadImportDetail() {
  try {
    const detail = await api.get<any>(`/purchase-orders/${route.params.id}/import-detail`)
    if (detail) {
      importForm.value = { ...detail }
      importOriginal.value = { ...detail }
    }
  } catch {}
}

function cancelImportEdit() {
  importForm.value = { ...importOriginal.value }
  editingImport.value = false
}

async function saveImport() {
  savingImport.value = true
  try {
    const saved = await api.put<any>(`/purchase-orders/${route.params.id}/import-detail`, importForm.value)
    importForm.value = { ...saved }
    importOriginal.value = { ...saved }
    editingImport.value = false
    showSnack('Import details saved', 'success')
  } catch {
    showSnack('Failed to save import details', 'error')
  } finally {
    savingImport.value = false
  }
}

// ── Track Numbers ──
const expandedItems = ref(new Set<number>())
const showAddTrackDialog = ref(false)
const savingTrack = ref(false)
const addTrackItemId = ref<number | null>(null)
const trackForm = ref({ trackNumber: '', carrier: '', notes: '' })

function toggleItemExpand(id: number) {
  if (expandedItems.value.has(id)) {
    expandedItems.value.delete(id)
  } else {
    expandedItems.value.add(id)
  }
  expandedItems.value = new Set(expandedItems.value)
}

function openAddTrack(poItemId: number) {
  addTrackItemId.value = poItemId
  trackForm.value = { trackNumber: '', carrier: '', notes: '' }
  showAddTrackDialog.value = true
}

async function addTrack() {
  if (!addTrackItemId.value || !trackForm.value.trackNumber.trim()) return
  savingTrack.value = true
  try {
    const newTrack = await api.post<any>(`/purchase-orders/items/${addTrackItemId.value}/track-numbers`, trackForm.value)
    const item = (po.value.items || []).find((i: any) => i.id === addTrackItemId.value)
    if (item) {
      if (!item.trackNumbers) item.trackNumbers = []
      item.trackNumbers.unshift(newTrack)
    }
    showAddTrackDialog.value = false
    showSnack('Tracking number added', 'success')
  } catch {
    showSnack('Failed to add tracking number', 'error')
  } finally {
    savingTrack.value = false
  }
}

async function deleteTrack(item: any, trackId: number) {
  try {
    await api.del(`/purchase-orders/track-numbers/${trackId}`)
    item.trackNumbers = (item.trackNumbers || []).filter((t: any) => t.id !== trackId)
    showSnack('Tracking number removed', 'success')
  } catch {
    showSnack('Failed to delete tracking number', 'error')
  }
}

// ── Load Data ──
onMounted(async () => {
  try { po.value = await api.get(`/purchase-orders/${route.params.id}`) } catch {}
  await loadImportDetail()
})

// ── Status ──
const showRejectDialog = ref(false)
const rejectionNote = ref('')

function onStatusSelect(newStatus: string) {
  if (newStatus === po.value.status) return
  if (newStatus === 'Rejected') {
    rejectionNote.value = ''
    showRejectDialog.value = true
    return
  }
  changeStatus(newStatus)
}

async function confirmReject() {
  showRejectDialog.value = false
  await changeStatus('Rejected', rejectionNote.value || undefined)
}

async function changeStatus(newStatus: string, note?: string) {
  try {
    await api.patch(`/purchase-orders/${route.params.id}/status`, { status: newStatus, rejectionNote: note || null })
    po.value.status = newStatus
    showSnack(`Status changed to ${newStatus}`, 'success')
  } catch {
    showSnack('Failed to change status', 'error')
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>
