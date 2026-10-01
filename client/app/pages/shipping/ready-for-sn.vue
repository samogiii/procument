<template>
  <div>
    <div class="d-flex align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Ready for SN#</h1>
        <p class="text-caption text-medium-emphasis mt-1">Accepted parts ready to create a Shipping Number — or, for Base 2–5, to be picked up by the customer</p>
      </div>
      <v-spacer />
      <v-btn
        color="teal"
        variant="tonal"
        prepend-icon="mdi-hand-extended-outline"
        :disabled="!canPickUp"
        @click="openPickup"
      >
        Pick Up ({{ selected.length }})
      </v-btn>
      <v-btn
        color="primary"
        prepend-icon="mdi-note-plus-outline"
        :disabled="!selected.length || baseMismatch"
        @click="openCreateSn"
      >
        Create SN# ({{ selected.length }} selected)
      </v-btn>
    </div>

    <!-- Warehouse filter -->
    <div class="d-flex flex-wrap gap-2 mb-4">
      <v-chip
        :color="filterWarehouse === null ? 'primary' : 'default'"
        variant="tonal"
        size="small"
        @click="filterWarehouse = null"
      >
        All Warehouses
      </v-chip>
      <v-chip
        v-for="wh in warehouses"
        :key="wh.id"
        :color="filterWarehouse === wh.id ? 'primary' : 'default'"
        variant="tonal"
        size="small"
        @click="filterWarehouse = wh.id"
      >
        {{ wh.name }}
      </v-chip>
    </div>

    <!-- Validation alerts -->
    <v-alert v-if="warehouseMismatch" type="error" class="mb-4" density="compact">
      All selected parts must belong to the same warehouse.
    </v-alert>
    <v-alert v-if="baseMismatch" type="error" class="mb-4" density="compact">
      Base 1 parts (shipped with T-ID / SO#) cannot share an SN# with Base 2–5 parts — select one or the other.
    </v-alert>
    <v-alert v-if="customerMismatch" type="warning" class="mb-4" density="compact">
      Selected parts belong to multiple customers — you can only create a <strong>CPT</strong> shipment note or a <strong>Pick Up</strong> for a single customer.
    </v-alert>

    <!-- Table -->
    <v-card>
      <v-data-table
        v-model="selected"
        :headers="headers"
        :items="filteredItems"
        :loading="loading"
        item-value="trackNumberItemId"
        show-select
        class="elevation-0"
      >
        <template #item.customerBase="{ item }">
          <v-chip size="x-small" :color="baseUsesTidSo(item.customerBase) ? 'primary' : 'teal'" variant="tonal">
            Base {{ effectiveBase(item.customerBase) }}
          </v-chip>
        </template>
        <template #item.customerCode="{ item }">
          <v-chip v-if="item.customerCode" size="x-small" color="teal" variant="tonal">{{ item.customerCode }}</v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #item.supplierName="{ item }">
          <span class="text-body-2 text-medium-emphasis">{{ item.supplierName || '—' }}</span>
        </template>
        <template #item.partNumberName="{ item }">
          <div>
            <div class="font-weight-medium">{{ item.partNumberName || '—' }}</div>
            <div v-if="item.partDescription" class="text-caption text-medium-emphasis">{{ item.partDescription }}</div>
          </div>
        </template>
        <template #item.trackNumber="{ item }">
          <span class="font-weight-medium text-pn">{{ item.trackNumber }}</span>
        </template>
        <template #item.warehouseName="{ item }">
          <v-chip size="x-small" color="primary" variant="tonal">{{ item.warehouseName || '—' }}</v-chip>
        </template>
      </v-data-table>
    </v-card>

    <!-- Create SN# Dialog -->
    <v-dialog v-model="createDialog" max-width="700" persistent scrollable>
      <v-card>
        <v-card-title class="d-flex align-center text-h6 pa-4 pb-2">
          <v-icon icon="mdi-note-plus-outline" class="mr-2" color="primary" />
          Create Shipping Number
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <!-- Type: CPT / DDP -->
          <div class="text-caption font-weight-bold text-medium-emphasis mb-1">SHIPMENT TYPE</div>
          <v-btn-toggle
            v-model="snForm.type"
            mandatory
            density="compact"
            variant="tonal"
            class="mb-4"
          >
            <v-btn value="DDP" size="small" class="mx-2">
              <v-icon icon="mdi-package-variant-closed" class="mr-1" size="16" />
              DDP
            </v-btn>
            <v-btn value="CPT" size="small">
              <v-icon icon="mdi-account-group-outline" class="mr-1" size="16" />
              CPT
            </v-btn>
          </v-btn-toggle>

          <!-- CPT customer mismatch warning inside dialog -->
          <v-alert v-if="snForm.type === 'CPT' && customerMismatch" type="error" class="mb-4" density="compact">
            Cannot create CPT — selected parts belong to multiple customers. Switch to DDP or select parts from a single customer only.
          </v-alert>

          <!-- Only Base 1 shipments carry T-ID / SO# -->
          <v-row v-if="selectionUsesTidSo" dense class="mb-1">
            <!-- T-ID -->
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="snForm.tId"
                label="T-ID"
                variant="outlined"
                density="compact"
                prepend-inner-icon="mdi-identifier"
                hint="Local Track# added later by Inventory"
                persistent-hint
              />
            </v-col>
            <!-- SO# -->
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="snForm.soNumber"
                label="SO#"
                variant="outlined"
                density="compact"
                prepend-inner-icon="mdi-file-document-outline"
              />
            </v-col>
          </v-row>

          <!-- Destination: combobox from warehouses or free text -->
          <v-combobox
            v-model="snForm.destination"
            :items="warehouses"
            item-title="name"
            :return-object="false"
            label="Destination (warehouse or free text)"
            variant="outlined"
            density="compact"
            prepend-inner-icon="mdi-map-marker-outline"
            class="mb-3"
            clearable
            hint="Select a warehouse or type a custom destination"
            persistent-hint
          />

          <!-- PDF upload -->
          <v-file-input
            v-model="snPdfFile"
            label="Upload SN# PDF (optional)"
            variant="outlined"
            density="compact"
            accept="application/pdf,.pdf"
            prepend-icon="mdi-file-pdf-box"
            class="mb-3"
          />

          <!-- Selected parts summary with Cert Needed -->
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">SELECTED PARTS</div>
          <v-table density="compact" class="rounded border mb-3">
            <thead>
              <tr>
                <th>Supplier</th>
                <th>Part Number</th>
                <th>Qty</th>
                <th>Track Number(s)</th>
                <th class="text-center">Cert Needed</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in selectedItems" :key="item.trackNumberItemId">
                <td class="text-caption text-medium-emphasis">{{ item.supplierName || '—' }}</td>
                <td>
                  <div class="font-weight-medium">{{ item.partNumberName || '—' }}</div>
                  <div v-if="item.partDescription" class="text-caption text-medium-emphasis">{{ item.partDescription }}</div>
                </td>
                <td>{{ item.actualQty }}</td>
                <td class="font-weight-medium text-pn text-caption">{{ item.trackNumber }}</td>
                <td class="text-center">
                  <v-checkbox
                    v-model="certNeededMap[item.trackNumberItemId]"
                    density="compact"
                    hide-details
                    color="primary"
                  />
                </td>
              </tr>
            </tbody>
          </v-table>

          <div class="text-caption text-medium-emphasis">
            Warehouse: <strong>{{ selectedItems[0]?.warehouseName || '—' }}</strong>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="createDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="creating" :disabled="(snForm.type === 'CPT' && customerMismatch) || baseMismatch" @click="createSn">Create SN#</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Pick Up Dialog (Base 2–5) -->
    <v-dialog v-model="pickupDialog" max-width="700" persistent scrollable>
      <v-card>
        <v-card-title class="d-flex align-center text-h6 pa-4 pb-2">
          <v-icon icon="mdi-hand-extended-outline" class="mr-2" color="teal" />
          Customer Pick Up
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-alert type="info" variant="tonal" density="compact" class="mb-4">
            The customer collects these parts at the warehouse. Upload the documents they handed over —
            the shipping is then <strong>finished</strong> (no packing, AWB or customs steps).
          </v-alert>

          <div class="d-flex flex-wrap gap-4 mb-3 text-body-2">
            <div>Customer: <strong>{{ selectedItems[0]?.customerName || selectedItems[0]?.customerCode || '—' }}</strong></div>
            <div>Warehouse: <strong>{{ selectedItems[0]?.warehouseName || '—' }}</strong></div>
          </div>

          <v-file-input
            v-model="pickupFiles"
            label="Customer documents"
            variant="outlined"
            density="compact"
            prepend-icon="mdi-paperclip"
            multiple
            chips
            show-size
            counter
            class="mb-3"
            hint="Required — at least one document"
            persistent-hint
          />

          <v-textarea
            v-model="pickupNote"
            label="Note (optional) — e.g. who collected the parts"
            variant="outlined"
            density="compact"
            rows="2"
            auto-grow
            class="mb-3"
          />

          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">PARTS PICKED UP</div>
          <v-table density="compact" class="rounded border">
            <thead>
              <tr>
                <th>Supplier</th>
                <th>Part Number</th>
                <th>Qty</th>
                <th>Track Number(s)</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in selectedItems" :key="item.trackNumberItemId">
                <td class="text-caption text-medium-emphasis">{{ item.supplierName || '—' }}</td>
                <td>
                  <div class="font-weight-medium">{{ item.partNumberName || '—' }}</div>
                  <div v-if="item.partDescription" class="text-caption text-medium-emphasis">{{ item.partDescription }}</div>
                </td>
                <td>{{ item.actualQty }}</td>
                <td class="font-weight-medium text-pn text-caption">{{ item.trackNumber }}</td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="pickupDialog = false">Cancel</v-btn>
          <v-btn color="teal" variant="flat" :loading="pickingUp" :disabled="!pickupFiles.length" @click="submitPickup">
            Finish Pick Up
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snack" :color="snackColor" timeout="3000" location="top right">{{ snackMsg }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const api = useApi()
const config = useRuntimeConfig()
const router = useRouter()

const headers = [
  { title: 'Base', key: 'customerBase', sortable: true },
  { title: 'Customer', key: 'customerCode', sortable: true },
  { title: 'Supplier', key: 'supplierName', sortable: true },
  { title: 'Part Number', key: 'partNumberName', sortable: true },
  { title: 'Qty', key: 'actualQty', sortable: true },
  { title: 'Track Number', key: 'trackNumber', sortable: true },
  { title: 'Warehouse', key: 'warehouseName', sortable: true },
]

const items = ref<any[]>([])
const warehouses = ref<any[]>([])
const loading = ref(false)
const selected = ref<number[]>([])
const filterWarehouse = ref<number | null>(null)
const createDialog = ref(false)
const creating = ref(false)
const snPdfFile = ref<File | null>(null)
const certNeededMap = ref<Record<number, boolean>>({})

const snForm = reactive({
  tId: '',
  soNumber: '',
  destination: null as string | null,
  type: 'DDP' as 'CPT' | 'DDP',
})

const snack = ref(false)
const snackMsg = ref('')
const snackColor = ref('success')

function notify(msg: string, color = 'success') {
  snackMsg.value = msg; snackColor.value = color; snack.value = true
}

// The API returns only the customer bases granted to the user.
const filteredItems = computed(() =>
  filterWarehouse.value
    ? items.value.filter(i => i.warehouseId === filterWarehouse.value)
    : items.value
)

const selectedItems = computed(() =>
  items.value.filter(i => selected.value.includes(i.trackNumberItemId))
)

const warehouseMismatch = computed(() => {
  const ids = new Set(selectedItems.value.map(i => i.warehouseId))
  return ids.size > 1
})

const customerMismatch = computed(() => {
  const ids = new Set(selectedItems.value.map(i => i.customerId).filter(id => id != null))
  return ids.size > 1
})

// Base 1 ships with T-ID / SO#; Bases 2–5 do not — the two never share an SN#.
const selectionUsesTidSo = computed(() => selectedItems.value.every(i => baseUsesTidSo(i.customerBase)))
const baseMismatch = computed(() =>
  !selectionUsesTidSo.value && selectedItems.value.some(i => baseUsesTidSo(i.customerBase)))

function openCreateSn() {
  if (warehouseMismatch.value || baseMismatch.value) return
  snForm.tId = ''
  snForm.soNumber = ''
  snForm.destination = null
  snForm.type = 'DDP'
  snPdfFile.value = null
  // Reset cert needed map
  certNeededMap.value = {}
  createDialog.value = true
}

async function createSn() {
  if (warehouseMismatch.value || baseMismatch.value) return
  creating.value = true
  try {
    const warehouseId = selectedItems.value[0]?.warehouseId
    // Resolve destination: if it's a warehouse object-title match, use name; otherwise free text
    const destination = snForm.destination
      ? (typeof snForm.destination === 'string' ? snForm.destination : (snForm.destination as any).name)
      : null

    const res = await api.post<any>('/shipment-notes', {
      warehouseId,
      type: snForm.type,
      tId: selectionUsesTidSo.value ? snForm.tId || null : null,
      soNumber: selectionUsesTidSo.value ? snForm.soNumber || null : null,
      destination: destination || null,
      items: selectedItems.value.map(i => ({
        trackNumberItemId: i.trackNumberItemId,
        certNeeded: certNeededMap.value[i.trackNumberItemId] ?? false,
      })),
    })
    // Upload PDF if provided
    if (snPdfFile.value && res?.id) {
      const fd = new FormData()
      fd.append('file', snPdfFile.value as Blob)
      await $fetch(`${api.baseURL}/shipment-notes/${res.id}/upload-pdf`, {
        method: 'POST',
        body: fd,
        headers: { Authorization: `Bearer ${useAuthStore().user?.token}` },
      }).catch(() => {})
    }
    notify('SN# created successfully')
    createDialog.value = false
    selected.value = []
    router.push('/total-shipping?tab=sn')
  } catch (e: any) {
    notify(e?.data?.message || 'Failed to create SN#', 'error')
  } finally {
    creating.value = false
  }
}

// ── Pick Up (Base 2–5) ────────────────────────────────────────────────────────
// The customer collects the parts at the warehouse and hands over documents; uploading
// them finishes the shipping (a PU-yyyy-### note, straight to Finished).
const pickupDialog = ref(false)
const pickingUp = ref(false)
const pickupFiles = ref<File[]>([])
const pickupNote = ref('')

const canPickUp = computed(() =>
  selectedItems.value.length > 0
  && selectedItems.value.every(i => baseCanPickUp(i.customerBase))
  && !warehouseMismatch.value
  && !customerMismatch.value)

function openPickup() {
  if (!canPickUp.value) return
  pickupFiles.value = []
  pickupNote.value = ''
  pickupDialog.value = true
}

async function submitPickup() {
  if (!canPickUp.value || !pickupFiles.value.length) return
  pickingUp.value = true
  try {
    const fd = new FormData()
    fd.append('warehouseId', String(selectedItems.value[0]!.warehouseId))
    for (const i of selectedItems.value) fd.append('trackNumberItemIds', String(i.trackNumberItemId))
    if (pickupNote.value.trim()) fd.append('note', pickupNote.value.trim())
    for (const f of pickupFiles.value) fd.append('files', f as Blob, f.name)

    const res = await api.post<any>('/shipment-notes/pickup', fd)
    notify(`Pick Up ${res?.snNumber ?? ''} finished`)
    pickupDialog.value = false
    selected.value = []
    // The whole track leaves the queue (as with an SN#), so reload rather than drop rows.
    items.value = await api.get('/shipping/ready-for-sn')
  } catch (e: any) {
    notify(e?.data?.message || 'Failed to save the pick up', 'error')
  } finally {
    pickingUp.value = false
  }
}

onMounted(async () => {
  loading.value = true
  try {
    [items.value, warehouses.value] = await Promise.all([
      api.get('/shipping/ready-for-sn'),
      api.get('/warehouses'),
    ])
  } finally {
    loading.value = false
  }
})
</script>
