<template>
  <v-dialog v-model="model" max-width="1100" scrollable>
    <v-card class="d-flex flex-column">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Merge Packing Lists</v-toolbar-title>
        <v-spacer />
        <v-select
          v-model="pdfTemplate"
          :items="PDF_TEMPLATE_OPTIONS"
          label="Template"
          variant="outlined"
          density="compact"
          hide-details
          class="mr-3"
          style="max-width:210px;"
          prepend-inner-icon="mdi-file-document-outline"
        />
        <v-btn
          variant="tonal"
          color="primary"
          prepend-icon="mdi-download"
          :loading="generating"
          :disabled="!canDownload"
          @click="downloadMerged"
        >
          Download
        </v-btn>
      </v-toolbar>

      <v-card-text class="pa-0">
        <div class="d-flex" style="min-height:0;">

          <!-- ── Left: invoice picker ── -->
          <div class="flex-shrink-0 pa-4" style="width:400px; border-right:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
            <div class="section-label">1 · Select Final Invoices</div>

            <v-text-field
              v-model="search"
              label="Search invoice #, customer..."
              prepend-inner-icon="mdi-magnify"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              class="mb-3"
            />

            <!-- The one hard rule: every invoice must be for the same customer. -->
            <v-alert
              v-if="lockedCustomerName"
              type="info"
              variant="tonal"
              density="compact"
              class="mb-3 text-caption"
            >
              Locked to <strong>{{ lockedCustomerName }}</strong>. Other customers' invoices are
              disabled — clear the selection to switch customer.
              <div class="mt-2">
                <v-btn size="x-small" variant="tonal" @click="clearSelection">Clear selection</v-btn>
              </div>
            </v-alert>

            <div v-if="loadingInvoices" class="d-flex justify-center pa-6">
              <v-progress-circular indeterminate color="primary" />
            </div>

            <v-list v-else density="compact" class="py-0" style="max-height:52vh; overflow-y:auto;">
              <v-list-item v-if="!filteredInvoices.length" class="text-medium-emphasis text-caption">
                No final invoices found.
              </v-list-item>
              <v-list-item
                v-for="inv in filteredInvoices"
                :key="inv.id"
                :disabled="isBlocked(inv)"
                :class="{ 'bg-surface-variant': isPicked(inv.id) }"
                class="cursor-pointer px-2"
                @click="togglePick(inv)"
              >
                <template #prepend>
                  <v-checkbox-btn
                    :model-value="isPicked(inv.id)"
                    :disabled="isBlocked(inv)"
                    color="primary"
                    @click.stop="togglePick(inv)"
                  />
                </template>
                <v-list-item-title class="text-body-2 font-weight-medium">
                  {{ inv.b1InvoiceNumber || inv.invoiceNumber || `#${inv.id}` }}
                </v-list-item-title>
                <v-list-item-subtitle class="text-caption">
                  {{ inv.customerCode || inv.customerName }} · {{ inv.itemCount }} item(s)
                  · {{ inv.createdAt ? new Date(inv.createdAt).toLocaleDateString() : '—' }}
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
          </div>

          <!-- ── Right: parts, shipping, company ── -->
          <div class="flex-grow-1 pa-4" style="max-height:70vh; overflow-y:auto;">

            <v-alert
              v-if="loadError"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-4 text-caption"
              :text="loadError"
            />

            <div v-if="!pickedIds.length" class="text-medium-emphasis text-body-2 pa-8 text-center">
              Pick two or more final invoices on the left to merge their packing lists.
            </div>

            <template v-else>
              <!-- 2 · Parts -->
              <div class="d-flex align-center mb-2 gap-2">
                <span class="section-label mb-0">2 · Select Parts</span>
                <v-spacer />
                <v-btn size="x-small" variant="tonal" :disabled="loadingItems" @click="selectAllParts">Select All</v-btn>
                <v-btn size="x-small" variant="tonal" color="error" :disabled="loadingItems" @click="selectedParts = []">Clear</v-btn>
              </div>

              <div v-if="loadingItems" class="d-flex justify-center pa-6">
                <v-progress-circular indeterminate color="primary" />
              </div>

              <div v-else>
                <v-card
                  v-for="inv in mergedInvoices"
                  :key="inv.id"
                  variant="outlined"
                  class="mb-3"
                >
                  <v-card-title class="text-body-2 font-weight-bold d-flex align-center py-2">
                    {{ inv.displayNumber }}
                    <v-chip size="x-small" variant="tonal" class="ml-2">
                      {{ pickedCountFor(inv) }} / {{ inv.items.length }}
                    </v-chip>
                    <v-spacer />
                    <v-btn size="x-small" variant="text" @click="toggleInvoiceParts(inv)">
                      {{ allPickedFor(inv) ? 'Deselect all' : 'Select all' }}
                    </v-btn>
                  </v-card-title>
                  <v-divider />
                  <v-card-text class="py-2">
                    <div v-if="!inv.items.length" class="text-caption text-medium-emphasis">
                      This invoice has no items.
                    </div>
                    <v-checkbox
                      v-for="it in inv.items"
                      :key="partKey(inv, it)"
                      v-model="selectedParts"
                      :value="partKey(inv, it)"
                      density="compact"
                      hide-details
                      color="primary"
                    >
                      <template #label>
                        <span class="text-body-2">
                          <strong>{{ it.alt || it.partNumber || '—' }}</strong>
                          <span v-if="it.alt" class="text-caption text-medium-emphasis"> (Alt to: {{ it.partNumber || '—' }})</span>
                          × {{ it.qty }}
                          <span v-if="it.condition" class="text-caption text-medium-emphasis"> · {{ it.condition }}</span>
                        </span>
                      </template>
                    </v-checkbox>
                  </v-card-text>
                </v-card>
              </div>

              <v-divider class="my-4" />

              <!-- 3 · Document -->
              <div class="section-label">3 · Document</div>
              <v-row dense align="center">
                <v-col cols="6">
                  <v-select
                    v-model="selectedPreset"
                    :items="companyPresetOptions"
                    label="Company Preset"
                    variant="outlined"
                    density="compact"
                    hide-details
                    prepend-inner-icon="mdi-domain"
                    :loading="presetsLoading"
                  />
                </v-col>
                <v-col cols="6">
                  <v-text-field
                    v-model="packingDate"
                    label="Packing List Date"
                    type="date"
                    variant="outlined"
                    density="compact"
                    hide-details
                  />
                </v-col>
              </v-row>

              <div class="section-label mt-4">4 · Shipping Details</div>
              <p class="text-caption text-medium-emphasis mb-2">
                One shipment covering every selected part. Leave blank to omit the section.
              </p>
              <div v-for="(pkg, i) in packages" :key="i" class="d-flex align-center gap-2 mb-2">
                <span class="text-caption text-medium-emphasis" style="min-width:24px;">{{ i + 1 }}.</span>
                <v-text-field v-model="pkg.weight" density="compact" variant="outlined" hide-details label="Weight" placeholder="e.g. 5 kg" style="flex:1;" />
                <v-text-field v-model="pkg.dimensions" density="compact" variant="outlined" hide-details label="Dimensions" placeholder="e.g. 40×30×20 cm" style="flex:1.5;" />
                <v-btn icon="mdi-close" size="x-small" variant="text" color="error" :disabled="packages.length === 1" @click="packages.splice(i, 1)" />
              </div>
              <v-btn prepend-icon="mdi-plus" variant="tonal" size="small" color="primary" class="mt-1" @click="packages.push({ weight: '', dimensions: '' })">
                Add Package
              </v-btn>
            </template>
          </div>
        </div>
      </v-card-text>

      <v-divider />
      <v-card-actions class="px-4 py-3">
        <span class="text-caption text-medium-emphasis">
          {{ pickedIds.length }} invoice(s) · {{ selectedParts.length }} part(s) selected
        </span>
        <v-spacer />
        <v-btn variant="text" @click="model = false">Close</v-btn>
        <v-btn
          color="primary"
          variant="tonal"
          prepend-icon="mdi-download"
          :loading="generating"
          :disabled="!canDownload"
          @click="downloadMerged"
        >
          Download Merged Packing List
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </v-dialog>
</template>

<script setup lang="ts">
const model = defineModel<boolean>({ default: false })
const api = useApi()

// ── Invoice picker ──
const invoices = ref<any[]>([])
const loadingInvoices = ref(false)
const search = ref('')
const pickedIds = ref<number[]>([])

// ── Merged data (server-validated: one customer only) ──
const mergedData = ref<any>(null)
const mergedInvoices = computed<any[]>(() => mergedData.value?.invoices || [])
const loadingItems = ref(false)
const loadError = ref('')

// ── Part selection: "<invoiceId>:<itemId>" ──
const selectedParts = ref<string[]>([])

// ── Output options ──
const pdfTemplate = ref<PdfTemplateKey>('modern')
const packingDate = ref(new Date().toISOString().slice(0, 10))
interface PackageEntry { weight: string; dimensions: string }
const packages = ref<PackageEntry[]>([{ weight: '', dimensions: '' }])
const generating = ref(false)

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ── Company presets ──
const apiPresets = ref<any[]>([])
const presetsLoading = ref(false)
const selectedPreset = ref<string>('')
const companyPresetOptions = computed(() => apiPresets.value.map((p: any) => p.name))
const activePreset = computed(() => apiPresets.value.find((p: any) => p.name === selectedPreset.value))

watch(model, (open) => {
  if (!open) return
  pickedIds.value = []
  selectedParts.value = []
  mergedData.value = null
  loadError.value = ''
  packages.value = [{ weight: '', dimensions: '' }]
  packingDate.value = new Date().toISOString().slice(0, 10)
  loadInvoices()
  loadPresets()
})

async function loadInvoices() {
  loadingInvoices.value = true
  try {
    const res = await api.get<any>('/final-invoices?page=1&pageSize=500')
    invoices.value = res.items ?? res.Items ?? []
  } catch {
    invoices.value = []
    showSnack('Failed to load final invoices', 'error')
  } finally {
    loadingInvoices.value = false
  }
}

async function loadPresets() {
  presetsLoading.value = true
  try {
    apiPresets.value = await api.get<any[]>('/companypresets')
    if (!selectedPreset.value && apiPresets.value.length) selectedPreset.value = apiPresets.value[0].name
  } catch {
    apiPresets.value = []
  } finally {
    presetsLoading.value = false
  }
}

// ── Same-customer constraint ──
const lockedCustomerId = computed<number | null>(() => {
  const first = invoices.value.find((i: any) => i.id === pickedIds.value[0])
  return first?.customerId ?? null
})
const lockedCustomerName = computed<string>(() => {
  const first = invoices.value.find((i: any) => i.id === pickedIds.value[0])
  return first?.customerName || ''
})

function isBlocked(inv: any) {
  return lockedCustomerId.value !== null && inv.customerId !== lockedCustomerId.value
}
function isPicked(id: number) {
  return pickedIds.value.includes(id)
}
function togglePick(inv: any) {
  if (isBlocked(inv)) return
  const idx = pickedIds.value.indexOf(inv.id)
  if (idx >= 0) pickedIds.value.splice(idx, 1)
  else pickedIds.value.push(inv.id)
}
function clearSelection() {
  pickedIds.value = []
  selectedParts.value = []
  mergedData.value = null
  loadError.value = ''
}

const filteredInvoices = computed(() => {
  const q = search.value?.trim().toLowerCase()
  if (!q) return invoices.value
  return invoices.value.filter((i: any) =>
    (i.invoiceNumber || '').toLowerCase().includes(q) ||
    (i.b1InvoiceNumber || '').toLowerCase().includes(q) ||
    (i.customerName || '').toLowerCase().includes(q) ||
    (i.customerCode || '').toLowerCase().includes(q))
})

// Reload the merged payload whenever the invoice selection changes. A request token
// guards against a slow earlier response overwriting a newer one.
let loadToken = 0
watch(pickedIds, async (ids) => {
  if (!ids.length) { mergedData.value = null; selectedParts.value = []; return }
  const token = ++loadToken
  loadingItems.value = true
  loadError.value = ''
  try {
    const data = await api.post<any>('/final-invoices/packing-list-data', { invoiceIds: [...ids] })
    if (token !== loadToken) return
    mergedData.value = data
    selectAllParts()
  } catch (e: any) {
    if (token !== loadToken) return
    mergedData.value = null
    selectedParts.value = []
    loadError.value = e?.data?.message || 'Failed to load invoice parts.'
  } finally {
    if (token === loadToken) loadingItems.value = false
  }
}, { deep: true })

// ── Part helpers ──
const partKey = (inv: any, it: any) => `${inv.id}:${it.id}`

function selectAllParts() {
  selectedParts.value = mergedInvoices.value.flatMap((inv: any) =>
    inv.items.map((it: any) => partKey(inv, it)))
}
function pickedCountFor(inv: any) {
  return inv.items.filter((it: any) => selectedParts.value.includes(partKey(inv, it))).length
}
function allPickedFor(inv: any) {
  return inv.items.length > 0 && pickedCountFor(inv) === inv.items.length
}
function toggleInvoiceParts(inv: any) {
  const keys = inv.items.map((it: any) => partKey(inv, it))
  if (allPickedFor(inv)) {
    selectedParts.value = selectedParts.value.filter(k => !keys.includes(k))
  } else {
    const missing = keys.filter((k: string) => !selectedParts.value.includes(k))
    selectedParts.value = [...selectedParts.value, ...missing]
  }
}

/** Flattened, invoice-ordered lines for the PDF — each tagged with its source invoice. */
const mergedItems = computed(() =>
  mergedInvoices.value.flatMap((inv: any) =>
    inv.items
      .filter((it: any) => selectedParts.value.includes(partKey(inv, it)))
      .map((it: any) => ({
        partNumber: it.partNumber || null,
        alt: it.alt || null,
        description: it.description || null,
        qty: it.qty || 0,
        condition: it.condition || null,
        certification: it.certification || null,
        sourceInvoice: inv.displayNumber || null,
        // A merged list may combine several final invoices with different customer POs.
        // Keep the PO on its own line so the customer can identify each shipped part.
        sourceCustomerPONumber: inv.customerPONumber || null,
      }))))

const canDownload = computed(() =>
  pickedIds.value.length > 0 && mergedItems.value.length > 0 && !loadingItems.value && !loadError.value)

/** Distinct, non-empty values joined for the merged document's header. */
function joinDistinct(values: (string | null | undefined)[]) {
  const set = [...new Set(values.filter(v => !!v && String(v).trim()) as string[])]
  return set.length ? set.join(' + ') : null
}

async function downloadMerged() {
  if (!canDownload.value) return
  generating.value = true
  try {
    const authStore = useAuthStore()
    const d = mergedData.value
    const preset = activePreset.value

    const payload = {
      companyName: preset?.name || '',
      companyLocation: preset?.location || '',
      companyPhone: preset?.phone || '',
      companyWebsite: preset?.website || '',
      companyEmail: preset?.email || '',
      logoBase64: preset?.logoBase64 ? `data:${preset.logoMimeType};base64,${preset.logoBase64}` : null,
      primaryColor: preset?.primaryColor || '#312e81',
      accentColor: preset?.accentColor || '#6366f1',
      // Every source invoice named in the header; each line also carries its own.
      invoiceNumber: joinDistinct(mergedInvoices.value.map((i: any) => i.displayNumber)),
      invoiceDate: packingDate.value ? new Date(packingDate.value).toLocaleDateString() : '—',
      customerPONumber: joinDistinct(mergedInvoices.value.map((i: any) => i.customerPONumber)),
      proformaRef: joinDistinct(mergedInvoices.value.map(
        (i: any) => i.b1ProformaInvoiceNumber || i.proformaInvoiceNumber)),
      customerName: d.customerName || '—',
      customerBillToName: d.customerName || null,
      customerShipToName: d.customerName || null,
      customerContactPerson: d.customerContactPerson || null,
      customerBillTo: d.customerBillTo || null,
      customerBillToEmail: d.customerBillToEmail || null,
      customerBillToPhone: d.customerBillToPhone || null,
      customerBillToContactPerson: d.customerBillToContactPerson || d.customerContactPerson || null,
      customerShipTo: d.customerShipTo || d.customerBillTo || null,
      customerShipToContactPerson: d.customerShipToContactPerson || d.customerContactPerson || null,
      customerShipToEmail: d.customerShipToEmail || null,
      customerShipToPhone: d.customerShipToPhone || null,
      customerShipToAccount: d.customerShipToAccount || null,
      items: mergedItems.value,
      packages: packages.value
        .filter(p => p.weight || p.dimensions)
        .map(p => ({ weight: p.weight || null, dimensions: p.dimensions || null })),
      template: pdfTemplate.value,
    }

    const response = await $fetch<Blob>(`${api.baseURL}/pdf/packing-list`, {
      method: 'POST',
      body: payload,
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })

    const url = window.URL.createObjectURL(response)
    const link = document.createElement('a')
    link.href = url
    const safe = (s: string) => (s || '').replace(/[/\\:*?"<>|]/g, '-').trim()
    link.setAttribute('download', `PackingList-Merged-${safe(d.customerCode || d.customerName || 'Customer')}.pdf`)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)
    showSnack(`Merged packing list generated (${mergedItems.value.length} parts).`, 'success')
  } catch (e: any) {
    console.error('Merged packing list generation failed:', e)
    showSnack(e?.data?.message || 'Failed to generate the merged packing list.', 'error')
  } finally {
    generating.value = false
  }
}
</script>

<style scoped>
.section-label {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: rgb(var(--v-theme-primary));
  margin-bottom: 8px;
}
</style>
