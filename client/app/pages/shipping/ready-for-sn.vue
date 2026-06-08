<template>
  <div>
    <div class="d-flex align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Ready for SN#</h1>
        <p class="text-caption text-medium-emphasis mt-1">Accepted parts ready to create a Shipping Number</p>
      </div>
      <v-spacer />
      <v-btn
        color="primary"
        prepend-icon="mdi-note-plus-outline"
        :disabled="!selected.length"
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
    <v-alert v-if="customerMismatch" type="warning" class="mb-4" density="compact">
      Selected parts belong to multiple customers — you can only create a <strong>CPT</strong> shipment note for a single customer.
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

          <v-row dense class="mb-1">
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
          <v-btn color="primary" variant="flat" :loading="creating" :disabled="snForm.type === 'CPT' && customerMismatch" @click="createSn">Create SN#</v-btn>
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

function openCreateSn() {
  if (warehouseMismatch.value) return
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
  if (warehouseMismatch.value) return
  creating.value = true
  try {
    const warehouseId = selectedItems.value[0]?.warehouseId
    // Resolve destination: if it's a warehouse object-title match, use name; otherwise free text
    const destination = snForm.destination
      ? (typeof snForm.destination === 'string' ? snForm.destination : (snForm.destination as any).name)
      : null

    const res = await api.post('/shipment-notes', {
      warehouseId,
      type: snForm.type,
      tId: snForm.tId || null,
      soNumber: snForm.soNumber || null,
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
  } catch {
    notify('Failed to create SN#', 'error')
  } finally {
    creating.value = false
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
