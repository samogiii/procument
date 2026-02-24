<template>
  <div>
    <PageHeader title="RFQs">
      <template #actions>
        <v-btn
          v-if="isAdmin"
          prepend-icon="mdi-shield-account"
          variant="tonal"
          @click="showBulkPerms = true"
          size="small"
        >
          Perms
        </v-btn>
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreateModal" size="small">
          New RFQ
        </v-btn>
      </template>
    </PageHeader>

    <!-- RFQ List -->
    <v-card class="glass-card">
      <v-card-text>
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search RFQs..."
          single-line
          hide-details
          class="mb-4"
        />
        <v-data-table
          :headers="headers"
          :items="items"
          :search="search"
          :loading="loading"
          :items-per-page="10"
          hover
          :row-props="getRowProps"
          @click:row="goToRfq"
        >
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>
          <template #item.leadTime="{ item }">
            <span :style="isLeadTimeUrgent(item.leadTime) ? 'color: #ef4444; font-weight: 600;' : ''">
              {{ new Date(item.leadTime).toLocaleDateString() }}
              <v-icon v-if="isLeadTimeUrgent(item.leadTime)" icon="mdi-alert" size="14" color="error" class="ml-1" />
            </span>
          </template>
          <!-- <template #item.priority="{ item }">
            <v-chip
              size="small"
              :color="item.priority === 'AOG' ? 'error' : item.priority === 'Urgent' ? 'warning' : 'secondary'"
              variant="tonal"
            >
              {{ item.priority || 'Normal' }}
            </v-chip>
          </template> -->
          <template #item.itemCount="{ item }">
            <v-chip size="small" color="secondary">{{ item.items?.length || 0 }} parts</v-chip>
          </template>
          <!-- <template #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`/rfqs/${item.id}`" />
          </template> -->
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- ═══════════ Create RFQ Modal ═══════════ -->
    <v-dialog v-model="showCreate" max-width="1200" persistent scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-file-document-plus-outline" color="primary" class="mr-2" />
          Create New RFQ
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showCreate = false" />
        </v-card-title>

        <v-divider />

        <v-card-text class="pa-4" style="max-height: 70vh; overflow-y: auto;">
          <v-form ref="formRef">
            <!-- RFQ Name -->
            <v-text-field
              v-model="form.name"
              label="RFQ Name *"
              prepend-inner-icon="mdi-file-document-outline"
              :rules="[rules.required]"
              class="mb-3"
            />

            <!-- Customer Name (autocomplete) -->
            <v-combobox
              v-model="form.customerName"
              :items="customerSuggestions"
              item-title="name"
              item-value="name"
              label="Customer Name *"
              prepend-inner-icon="mdi-domain"
              :rules="[rules.required]"
              :loading="customerLoading"
              no-filter
              clearable
              return-object
              class="mb-3"
              @update:search="searchCustomers"
            >
              <template #no-data>
                <v-list-item>
                  <v-list-item-title v-if="customerSearchText.length < 3" class="text-medium-emphasis">
                    Type at least 3 characters...
                  </v-list-item-title>
                  <v-list-item-title v-else class="text-medium-emphasis">
                    "{{ customerSearchText }}" — will be created automatically
                  </v-list-item-title>
                </v-list-item>
              </template>
            </v-combobox>
            <!-- Date -->
            <v-text-field
              v-model="form.date"
              label="Date *"
              prepend-inner-icon="mdi-calendar"
              type="date"
              :max="today"
              :rules="[rules.required]"
              class="mb-3"
            />
            <!-- Lead Time -->
            <v-text-field
              v-model="form.leadTime"
              label="Lead Time *"
              prepend-inner-icon="mdi-calendar"
              type="date"
              :min="today"
              :rules="[rules.required]"
              class="mb-3"
            />

            <!-- Priority -->
            

            <!-- Ex Type -->
            <v-select
              v-model="form.exType"
              :items="exTypeOptions"
              item-title="label"
              item-value="value"
              label="Ex Type"
              prepend-inner-icon="mdi-swap-horizontal"
              clearable
              class="mb-3"
            />

            <!-- Notes -->
            <v-textarea
              v-model="form.notes"
              label="Notes"
              prepend-inner-icon="mdi-note-text-outline"
              rows="2"
              auto-grow
              class="mb-3"
            />

            <!-- ── Part Numbers Section ── -->
            <v-divider class="mb-4" />

            <div class="d-flex flex-wrap align-center gap-2 mb-3">
              <div class="d-flex align-center">
                <v-icon icon="mdi-cog-outline" color="secondary" class="mr-2" size="20" />
                <span class="text-subtitle-1 font-weight-medium">RFQ Items</span>
              </div>
              <v-spacer />
              <div class="d-flex align-center gap-2">
                <v-btn
                  prepend-icon="mdi-content-paste"
                  variant="tonal"
                  size="small"
                  color="info"
                  @click="showPasteBox = !showPasteBox"
                >
                  {{ showPasteBox ? 'Hide Paste' : 'Paste Excel' }}
                </v-btn>
                <v-text-field
                  v-model.number="partCount"
                  type="number"
                  label="# of fields"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="max-width: 80px"
                  min="1"
                  max="100"
                  @update:model-value="adjustPartFields"
                />
                <v-btn
                  icon="mdi-plus"
                  color="primary"
                  size="small"
                  variant="outlined"
                  @click="addPartRow"
                />
              </div>
            </div>

            <!-- Paste from Excel -->
            <div v-if="showPasteBox" class="mb-4">
              <p class="text-caption text-medium-emphasis mb-2">
                Copy rows from Excel and paste below. Expected columns (tab-separated):
                <strong>PartNumber &nbsp; Description &nbsp; Qty &nbsp; Condition &nbsp; Priority &nbsp; Remark &nbsp; Alt</strong>
              </p>
              <v-textarea
                v-model="pasteText"
                placeholder="Paste rows from Excel here (Ctrl+V)..."
                variant="outlined"
                density="compact"
                rows="5"
                hide-details
                class="mb-2"
                style="font-family: 'JetBrains Mono', 'Cascadia Code', monospace; font-size: 12px;"
              />
              <div class="d-flex align-center gap-2">
                <v-btn
                  prepend-icon="mdi-import"
                  color="primary"
                  size="small"
                  :loading="pasteImporting"
                  @click="importFromPaste"
                >
                  Import &amp; Check
                </v-btn>
                <v-btn
                  variant="text"
                  size="small"
                  @click="pasteText = ''; showPasteBox = false"
                >
                  Cancel
                </v-btn>
                <span v-if="pasteStatus" class="text-caption" :class="pasteStatusColor">{{ pasteStatus }}</span>
              </div>
            </div>

            <p class="text-caption text-medium-emphasis mb-3">
              Search part numbers (3+ chars). If found, description &amp; alternatives auto-fill. Otherwise a new part will be created.
            </p>

            <div
              v-for="(row, index) in form.items"
              :key="index"
              class="d-flex flex-wrap align-center gap-2 mb-3 rfq-item-row"
            >
              <span class="text-caption text-medium-emphasis" style="min-width: 24px">{{ index + 1 }}.</span>

              <!-- Part Number -->
              <v-combobox
                v-model="row.partNumber"
                :items="partSuggestions[index] || []"
                item-title="name"
                item-value="name"
                label="Part Number *"
                density="compact"
                variant="outlined"
                hide-details
                no-filter
                clearable
                return-object
                style="min-width: 160px; flex: 2;"
                :loading="partLoading[index]"
                @update:search="(val: string) => searchParts(val, index)"
                @update:model-value="(val: any) => onPartPicked(val, index)"
              >
                <template #item="{ item: suggestion, props: itemProps }">
                  <v-list-item v-bind="itemProps">
                    <template #subtitle>
                      <span v-if="suggestion.raw.description">{{ suggestion.raw.description }}</span>
                      <span v-else class="font-italic text-medium-emphasis">No description</span>
                    </template>
                  </v-list-item>
                </template>
                <template #no-data>
                  <v-list-item>
                    <v-list-item-title
                      v-if="(partSearchTexts[index]?.length || 0) < 3"
                      class="text-medium-emphasis text-caption"
                    >
                      Type 3+ chars to search...
                    </v-list-item-title>
                    <v-list-item-title v-else class="text-medium-emphasis text-caption">
                      "{{ partSearchTexts[index] }}" — will be created
                    </v-list-item-title>
                  </v-list-item>
                </template>
              </v-combobox>

              <!-- Description -->
              <v-text-field
                v-model="row.description"
                label="Description"
                density="compact"
                variant="outlined"
                hide-details
                style="min-width: 140px; flex: 2;"
              />

              <!-- Qty -->
              <v-text-field
                v-model.number="row.qty"
                label="Qty"
                type="number"
                min="1"
                density="compact"
                variant="outlined"
                hide-details
                style="max-width: 70px;"
              />

              <!-- Condition -->
              <v-select
                v-model="row.condition"
                :items="['NE', 'OH', 'SV', 'AR']"
                label="Cond."
                density="compact"
                variant="outlined"
                hide-details
                clearable
                style="max-width: 140px;"
              />

              <v-select
              v-model="row.priority"
              :items="['Normal', 'Urgent', 'AOG']"
              label="Priority"
              density="compact"
              variant="outlined"
              hide-details

              clearable
            />

              <!-- Remark -->
              <v-text-field
                v-model="row.remark"
                label="Remark"
                density="compact"
                variant="outlined"
                hide-details
                style="min-width: 100px; flex: 1;"
              />

              <!-- Alternatives -->
              <div class="d-flex flex-wrap align-center gap-1" style="min-width: 160px; flex: 2;">
                <v-chip
                  v-for="(alt, aIdx) in row.alternatives"
                  :key="aIdx"
                  size="x-small"
                  color="warning"
                  variant="tonal"
                  closable
                  @click:close="row.alternatives.splice(aIdx, 1)"
                >
                  {{ alt }}
                </v-chip>
                <v-text-field
                  v-model="row.altInput"
                  placeholder="+ alt"
                  density="compact"
                  variant="plain"
                  hide-details
                  single-line
                  style="max-width: 80px; font-size: 11px;"
                  @keydown.enter.prevent="pushAlt(index)"
                />
              </div>

              <!-- Status chip -->
              <v-chip
                v-if="row.isExisting"
                size="x-small" color="success" variant="tonal"
              >DB</v-chip>
              <v-chip
                v-else-if="row.partNumber"
                size="x-small" color="info" variant="tonal"
              >New</v-chip>
              <span v-else style="width: 32px;"></span>

              <!-- Remove -->
              <v-btn
                v-if="form.items.length > 1"
                icon="mdi-close"
                size="x-small"
                variant="text"
                color="error"
                @click="removePartRow(index)"
              />
            </div>
          </v-form>

          <!-- Validation error -->
          <v-alert v-if="submitError" type="error" density="compact" class="mt-3">
            {{ submitError }}
          </v-alert>
        </v-card-text>

        <v-divider />

        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showCreate = false">Cancel</v-btn>
          <v-btn color="primary" :loading="submitting" @click="submitRfq">
            Create RFQ
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Bulk Permission Manager -->
    <BulkPermissionManager v-model="showBulkPerms" entity-name="RFQ" />
  </div>
</template>

<script setup lang="ts">
import { VTextField } from 'vuetify/components'

const api = useApi()
const authStore = useAuthStore()
const router = useRouter()

const today = new Date().toISOString().split('T')[0]

function isLeadTimeUrgent(dateStr: string) {
  if (!dateStr) return false
  const diff = new Date(dateStr).getTime() - Date.now()
  const daysLeft = diff / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 5
}

function getRowProps({ item }: { item: any }) {
  if (isLeadTimeUrgent(item.leadTime)) {
    return { class: 'lead-time-urgent-row' }
  }
  return {}
}

const isAdmin = computed(() => authStore.isAdmin)
const showBulkPerms = ref(false)

// ── List state ──
const search = ref('')
const loading = ref(false)
const items = ref<any[]>([])

const headers = [
  { title: 'ID', key: 'id', width: '80px' },
  { title: 'Name', key: 'name' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Priority', key: 'priority', width: '100px' },
  { title: 'Lead Time', key: 'leadTime' },
  { title: 'Parts', key: 'itemCount', sortable: false },
  { title: 'Created', key: 'createdAt' },
]

onMounted(() => loadItems())

async function loadItems() {
  loading.value = true
  try {
    const res = await api.get<any[]>('/rfqs')
    items.value = res || []
  } catch {}
  finally { loading.value = false }
}

function goToRfq(pointerEvent: Event, rowData: { item: any }) {
  if (rowData && rowData.item && rowData.item.id) {
    navigateTo(`/rfqs/${rowData.item.id}`)
  }
}

// ── Create Modal state ──
const showCreate = ref(false)
const formRef = ref<any>(null)
const submitting = ref(false)
const submitError = ref('')
const partCount = ref(10)

interface ItemRow {
  partNumber: any
  description: string
  qty: number
  condition: string
  remark: string
  priority: string
  alternatives: string[]
  altInput: string
  isExisting: boolean
}

function makeEmptyRow(): ItemRow {
  return { partNumber: null, description: '', qty: 1, condition: '', remark: '',priority: '' as string, alternatives: [], altInput: '', isExisting: false }
}

const exTypeOptions = [
  { label: 'Ex Warehouse', value: 0 },
  { label: 'Ex Vendor', value: 1 },
  { label: 'Ex Customer', value: 2 },
]

const form = ref({
  name: '',
  customerName: null as any,
  leadTime: '',
  date: '',
  notes: '',
  exType: null as number | null,
  items: Array.from({ length: 10 }, makeEmptyRow) as ItemRow[],
})

const rules = {
  required: (v: any) => !!v || 'This field is required',
}

// ── Customer Autocomplete ──
const customerSuggestions = ref<any[]>([])
const customerLoading = ref(false)
const customerSearchText = ref('')
let customerDebounce: any = null

function searchCustomers(val: string) {
  customerSearchText.value = val || ''
  clearTimeout(customerDebounce)
  if (!val || val.length < 1) {
    customerSuggestions.value = []
    return
  }
  customerDebounce = setTimeout(async () => {
    customerLoading.value = true
    try {
      customerSuggestions.value = await api.get<any[]>(`/customers/search?q=${encodeURIComponent(val)}`)
    } catch {}
    finally { customerLoading.value = false }
  }, 300)
}

// ── Part Number Autocomplete ──
const partSuggestions = ref<Record<number, any[]>>({})
const partLoading = ref<Record<number, boolean>>({})
const partSearchTexts = ref<Record<number, string>>({})
const partDebounces: Record<number, any> = {}

function searchParts(val: string, index: number) {
  partSearchTexts.value[index] = val || ''
  clearTimeout(partDebounces[index])
  if (!val || val.length < 1) {
    partSuggestions.value[index] = []
    return
  }
  partDebounces[index] = setTimeout(async () => {
    partLoading.value[index] = true
    try {
      partSuggestions.value[index] = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(val)}`)
    } catch {}
    finally { partLoading.value[index] = false }
  }, 300)
}

function onPartPicked(val: any, index: number) {
  const row = form.value.items[index]
  if (!row) return
  if (!val) {
    row.isExisting = false
    row.description = ''
    row.remark = ''
    row.alternatives = []
    return
  }
  if (typeof val === 'object' && val.id) {
    row.isExisting = true
    row.description = val.description || ''
    row.remark = val.remark || ''
    row.alternatives = (val.alternatives || []).map((a: any) => a.name)
  } else {
    row.isExisting = false
  }
}

function pushAlt(index: number) {
  const row = form.value.items[index]
  if (!row) return
  const val = row.altInput.trim()
  if (!val) return
  if (!row.alternatives.includes(val)) {
    row.alternatives.push(val)
  }
  row.altInput = ''
}

// ── Part row management ──
function adjustPartFields(val: string | number) {
  val = Number(val)
  const count = Math.max(1, Math.min(100, val || 1))
  partCount.value = count
  const current = form.value.items.length
  if (count > current) {
    for (let i = 0; i < count - current; i++) form.value.items.push(makeEmptyRow())
  } else if (count < current) {
    form.value.items.splice(count)
  }
}

function addPartRow() {
  form.value.items.push(makeEmptyRow())
  partCount.value = form.value.items.length
}

function removePartRow(index: number) {
  form.value.items.splice(index, 1)
  partCount.value = form.value.items.length
}

// ── Paste from Excel ──
const showPasteBox = ref(false)
const pasteText = ref('')
const pasteImporting = ref(false)
const pasteStatus = ref('')
const pasteStatusColor = ref('text-medium-emphasis')

async function importFromPaste() {
  const text = pasteText.value.trim()
  if (!text) {
    pasteStatus.value = 'Nothing to import — paste some rows first.'
    pasteStatusColor.value = 'text-warning'
    return
  }

  pasteImporting.value = true
  pasteStatus.value = 'Parsing...'
  pasteStatusColor.value = 'text-medium-emphasis'

  try {
    const lines = text.split(/\r?\n/).filter(l => l.trim())
    if (lines.length === 0) {
      pasteStatus.value = 'No valid rows found.'
      pasteStatusColor.value = 'text-error'
      return
    }

    // Detect header row
    const firstLine = (lines.length > 0 ? (lines[0] || '') : '').toLowerCase()
    const hasHeader = firstLine.includes('partnumber') || firstLine.includes('part number') || firstLine.includes('part_number') || firstLine.includes('p/n')
    const startIdx = hasHeader ? 1 : 0

    const rows: ItemRow[] = []
    for (let i = startIdx; i < lines.length; i++) {
      // Excel copies as tab-separated
      const cols = lines[i].split('\t')
      if (cols.length === 0 || !(cols[0] || '').trim()) continue

      const partNumber = (cols[0] || '').trim()
      const description = (cols[1] || '').trim()
      const qty = parseInt((cols[2] || '').trim()) || 1
      const condition = (cols[3] || '').trim().toUpperCase()
      const priority = (cols[4] || '').trim()
      const remark = (cols[5] || '').trim()
      const alt = (cols[6] || '').trim()

      const alternatives = alt ? alt.split(';').map(a => a.trim()).filter(Boolean) : []

      rows.push({
        partNumber,
        description,
        qty,
        condition,
        remark,
        priority,
        alternatives,
        altInput: '',
        isExisting: false,
      })
    }

    if (rows.length === 0) {
      pasteStatus.value = 'No valid rows found.'
      pasteStatusColor.value = 'text-error'
      return
    }

    // Search each part number in the DB and enrich
    pasteStatus.value = `Checking ${rows.length} part(s) against database...`
    let foundCount = 0

    for (const row of rows) {
      const pn = typeof row.partNumber === 'string' ? row.partNumber : ''
      if (!pn || pn.length < 2) continue

      try {
        const results = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(pn)}`)
        // Find exact match (case-insensitive)
        const match = (results || []).find((r: any) => r.name?.toLowerCase() === pn.toLowerCase())

        if (match) {
          foundCount++
          row.partNumber = { id: match.id, name: match.name, description: match.description, remark: match.remark, alternatives: match.alternatives }
          row.isExisting = true

          // Import description from DB if the pasted description is empty
          if (!row.description && match.description) {
            row.description = match.description
          }


          // Import remark from DB if the pasted remark is empty
          if (!row.remark && match.remark) {
            row.remark = match.remark
          }

          // Merge alternatives: keep pasted ones + add DB ones that aren't already listed
          const existingAlts = (match.alternatives || []).map((a: any) => a.name)
          for (const dbAlt of existingAlts) {
            if (!row.alternatives.some(a => a.toLowerCase() === dbAlt.toLowerCase())) {
              row.alternatives.push(dbAlt)
            }
          }
        }
      } catch {
        // Search failed for this part, leave as new
      }
    }

    form.value.items = rows
    partCount.value = rows.length
    submitError.value = ''

    pasteStatus.value = `Imported ${rows.length} row(s) — ${foundCount} found in DB, ${rows.length - foundCount} new.`
    pasteStatusColor.value = 'text-success'

    // Auto-hide paste box after successful import
    setTimeout(() => { showPasteBox.value = false }, 2000)
  } catch (e) {
    pasteStatus.value = 'Import failed.'
    pasteStatusColor.value = 'text-error'
  } finally {
    pasteImporting.value = false
  }
}

function openCreateModal() {
  form.value = {
    name: '',
    customerName: null,
    leadTime: '',
    date: '',
    notes: '',
    exType: null,
    items: Array.from({ length: 10 }, makeEmptyRow),
  }
  partCount.value = 10
  submitError.value = ''
  partSuggestions.value = {}
  partSearchTexts.value = {}
  showCreate.value = true
}

// ── Submit ──
async function submitRfq() {
  submitError.value = ''

  if (!form.value.name.trim()) {
    submitError.value = 'RFQ Name is required.'
    return
  }

  const customerName = typeof form.value.customerName === 'object'
    ? form.value.customerName?.name
    : form.value.customerName

  if (!customerName?.trim()) {
    submitError.value = 'Customer Name is required.'
    return
  }

  if (!form.value.leadTime) {
    submitError.value = 'Lead Time is required.'
    return
  }

  // Collect non-empty items
  const validItems = form.value.items.filter(row => {
    const name = typeof row.partNumber === 'object' ? row.partNumber?.name : row.partNumber
    return name && name.trim()
  })

  if (validItems.length === 0) {
    submitError.value = 'At least 1 Part Number is required.'
    return
  }

  const partNumbers = validItems.map(row => {
    const name = typeof row.partNumber === 'object' ? row.partNumber.name : row.partNumber
    return name.trim()
  })

  submitting.value = true
  try {
    // 1. Create the RFQ with part numbers
    const payload = {
      name: form.value.name.trim(),
      customerName: customerName.trim(),
      leadTime: new Date(form.value.leadTime).toISOString(),
      createdAt: form.value.date ? new Date(form.value.date).toISOString() : new Date().toISOString(),
      userId: authStore.user?.id || 0,
      notes: form.value.notes || null,
      exType: form.value.exType,
      partNumbers,
    }

    const created = await api.post<any>('/rfqs', payload)

    // 2. Update each item with qty, condition, alternatives
    if (created?.items?.length) {
      const updatePromises = created.items.map((serverItem: any, idx: number) => {
        const localRow = validItems[idx]
        if (!localRow) return Promise.resolve()

        const promises: Promise<any>[] = []

        // Update item fields (qty, condition)
        promises.push(api.put(`/rfqs/items/${serverItem.id}`, {
          alt: null,
          qty: localRow.qty || 1,
          condition: localRow.condition || null,
        }))

        // Update description, remark on part number if any provided
        if (localRow.description ||  localRow.remark) {
          promises.push(api.put(`/partnumbers/${serverItem.partNumberId}`, {
            name: serverItem.partNumberName,
            description: localRow.description || null,
            remark: localRow.remark || null,
            supplierId: null,
          }))
        }

        // Add alternatives
        for (const altName of localRow.alternatives) {
          const existing = (serverItem.alternatives || []).find((a: any) => a.name === altName)
          if (!existing) {
            promises.push(api.post(`/partnumbers/${serverItem.partNumberId}/alternatives`, { name: altName }))
          }
        }

        return Promise.all(promises)
      })
      await Promise.all(updatePromises)
    }

    showCreate.value = false
    await loadItems()
  } catch (e: any) {
    submitError.value = e?.data?.message || 'Failed to create RFQ.'
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
:deep(.lead-time-urgent-row) {
  background-color: rgba(239, 68, 68, 0.12) !important;
  border-left: 3px solid #ef4444;
}
:deep(.lead-time-urgent-row:hover) {
  background-color: rgba(239, 68, 68, 0.2) !important;
}
:deep(.lead-time-urgent-row td) {
  color: #fca5a5 !important;
}
</style>
