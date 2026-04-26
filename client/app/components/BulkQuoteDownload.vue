<template>
  <v-dialog v-model="model" max-width="800" scrollable>
    <v-card>
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">
          Bulk Download Quotes
        </v-toolbar-title>
        <v-spacer />
        <v-btn
          v-if="selected.length > 0"
          variant="tonal"
          color="primary"
          prepend-icon="mdi-download-multiple"
          :loading="downloading"
          @click="downloadAll"
        >
          Download {{ selected.length }} PDF{{ selected.length > 1 ? 's' : '' }}
        </v-btn>
      </v-toolbar>

      <v-card-text class="pa-0">
        <!-- Search & filters row -->
        <div class="d-flex align-center gap-2 px-4 pt-4 pb-2">
          <v-text-field
            v-model="search"
            label="Search by quote #, customer, RFQ..."
            prepend-inner-icon="mdi-magnify"
            variant="outlined"
            density="compact"
            hide-details
            clearable
            style="flex: 1"
          />
          <v-select
            v-model="statusFilter"
            :items="['Sent', 'Accepted']"
            label="Status"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 140px"
          />
          <v-btn
            variant="text"
            size="small"
            :disabled="filteredQuotes.length === 0"
            @click="toggleSelectAll"
          >
            {{ allSelected ? 'Deselect All' : 'Select All' }}
          </v-btn>
        </div>

        <!-- Progress while downloading -->
        <v-progress-linear
          v-if="downloading"
          :model-value="downloadProgress"
          color="primary"
          height="4"
          class="mx-4"
        />
        <div v-if="downloading" class="text-caption text-center text-medium-emphasis pb-2">
          Downloading {{ currentDownloadIndex + 1 }} of {{ selected.length }}: {{ currentDownloadName }}
        </div>

        <v-divider class="mt-2" />

        <!-- Loading state -->
        <div v-if="loading" class="d-flex justify-center align-center pa-8">
          <v-progress-circular indeterminate color="primary" />
        </div>

        <!-- Quote List -->
        <v-list v-else lines="two" class="py-0" style="max-height: 500px; overflow-y: auto;">
          <v-list-item
            v-if="filteredQuotes.length === 0"
            class="text-center text-medium-emphasis pa-8"
          >
            No quotes found.
          </v-list-item>

          <template v-for="quote in filteredQuotes" :key="quote.id">
            <v-list-item
              :value="quote.id"
              :class="{ 'bg-surface-variant': isSelected(quote.id) }"
              class="cursor-pointer"
              @click="toggleSelect(quote.id)"
            >
              <template #prepend>
                <v-checkbox-btn
                  :model-value="isSelected(quote.id)"
                  color="primary"
                  @click.stop="toggleSelect(quote.id)"
                />
              </template>

              <v-list-item-title class="font-weight-medium">
                {{ quote.quoteNumber }}
                <v-chip
                  size="x-small"
                  :color="statusColor(quote.status)"
                  variant="tonal"
                  class="ml-2"
                >{{ quote.status }}</v-chip>
                <v-chip
                  v-if="downloadedIds.has(quote.id)"
                  size="x-small"
                  color="success"
                  variant="tonal"
                  class="ml-1"
                  prepend-icon="mdi-check"
                >Downloaded</v-chip>
              </v-list-item-title>
              <v-list-item-subtitle>
                {{ quote.customerName }} — {{ quote.rfqName || '—' }}
                <span class="ml-3 text-medium-emphasis">${{ formatPrice(quote.totalAmount) }}</span>
                <span class="ml-3 text-medium-emphasis">{{ quote.createdAt ? new Date(quote.createdAt).toLocaleDateString() : '—' }}</span>
              </v-list-item-subtitle>

              <template #append>
                <v-progress-circular
                  v-if="downloadingId === quote.id"
                  indeterminate
                  size="20"
                  width="2"
                  color="primary"
                />
                <v-icon
                  v-else-if="downloadedIds.has(quote.id)"
                  icon="mdi-check-circle"
                  color="success"
                  size="20"
                />
              </template>
            </v-list-item>
            <v-divider />
          </template>
        </v-list>
      </v-card-text>

      <v-card-actions class="px-4 py-3">
        <span class="text-caption text-medium-emphasis">
          {{ selected.length }} of {{ filteredQuotes.length }} selected
        </span>
        <v-spacer />
        <v-btn variant="text" @click="model = false">Close</v-btn>
        <v-btn
          v-if="selected.length > 0"
          color="primary"
          variant="tonal"
          prepend-icon="mdi-download-multiple"
          :loading="downloading"
          @click="downloadAll"
        >
          Download {{ selected.length }} PDF{{ selected.length > 1 ? 's' : '' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const model = defineModel<boolean>({ default: false })

const api = useApi()
const config = useRuntimeConfig()
const authStore = useAuthStore()

const quotes = ref<any[]>([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref('Accepted')
const selected = ref<number[]>([])
const downloading = ref(false)
const downloadingId = ref<number | null>(null)
const downloadedIds = ref<Set<number>>(new Set())
const downloadProgress = ref(0)
const currentDownloadIndex = ref(0)
const currentDownloadName = ref('')

watch(model, (open) => {
  if (open) {
    selected.value = []
    downloadedIds.value = new Set()
    loadQuotes()
  }
})

async function loadQuotes() {
  loading.value = true
  try {
    const res = await api.get<any>('/quotes?pageSize=9999') as any
    const items = Array.isArray(res) ? res : (res.items || res.Items || [])
    quotes.value = items
  } catch {
    quotes.value = []
  } finally {
    loading.value = false
  }
}

const filteredQuotes = computed(() => {
  let result = quotes.value
  if (statusFilter.value !== 'All') {
    result = result.filter((q: any) => q.status === statusFilter.value)
  }
  if (search.value?.trim()) {
    const q = search.value.toLowerCase()
    result = result.filter((quote: any) =>
      (quote.quoteNumber || '').toLowerCase().includes(q) ||
      (quote.customerName || '').toLowerCase().includes(q) ||
      (quote.rfqName || '').toLowerCase().includes(q) ||
      (quote.id?.toString() || '').includes(q)
    )
  }
  return result
})

const allSelected = computed(() =>
  filteredQuotes.value.length > 0 && filteredQuotes.value.every((q: any) => selected.value.includes(q.id))
)

function isSelected(id: number) {
  return selected.value.includes(id)
}

function toggleSelect(id: number) {
  const idx = selected.value.indexOf(id)
  if (idx >= 0) selected.value.splice(idx, 1)
  else selected.value.push(id)
}

function toggleSelectAll() {
  if (allSelected.value) {
    const ids = filteredQuotes.value.map((q: any) => q.id)
    selected.value = selected.value.filter(id => !ids.includes(id))
  } else {
    filteredQuotes.value.forEach((q: any) => {
      if (!selected.value.includes(q.id)) selected.value.push(q.id)
    })
  }
}

function statusColor(status: string) {
  switch (status) {
    case 'Accepted': return 'success'
    case 'Rejected': return 'error'
    case 'Sent': return 'info'
    case 'Draft': return 'warning'
    default: return 'default'
  }
}

async function downloadAll() {
  if (selected.value.length === 0 || downloading.value) return
  downloading.value = true
  downloadProgress.value = 0
  currentDownloadIndex.value = 0

  const presets = await loadPresets()

  for (let i = 0; i < selected.value.length; i++) {
    const quoteId = selected.value[i] as number
    currentDownloadIndex.value = i
    downloadProgress.value = Math.round((i / selected.value.length) * 100)
    downloadingId.value = quoteId

    try {
      // Fetch full quote details
      const fullQuote = await api.get<any>(`/quotes/${quoteId}`) as any
      currentDownloadName.value = fullQuote.quoteNumber || `Quote #${quoteId}`

      // Select preset based on customer base
      const base = fullQuote?.customerBase
      const preset = presets.find((p: any) => p.sortOrder === base) || presets[0]

      await generateAndDownload(fullQuote, preset)
      const newSet = new Set(downloadedIds.value)
      newSet.add(quoteId)
      downloadedIds.value = newSet
    } catch (err) {
      console.error(`Failed to download quote ${quoteId}:`, err)
    }

    downloadingId.value = null
    // Small delay to avoid overwhelming the server
    await new Promise(resolve => setTimeout(resolve, 300))
  }

  downloadProgress.value = 100
  downloading.value = false
}

async function loadPresets(): Promise<any[]> {
  try {
    const res = await api.get<any[]>('/companypresets') as any
    return Array.isArray(res) ? res : []
  } catch {
    return []
  }
}

async function generateAndDownload(q: any, preset: any) {
  const items: any[] = [...(q.items || [])].sort((a: any, b: any) => {
    // Primary: Group by RFQ item (rfqReference or rfqItemId)
    const aRef = typeof a.rfqRef === 'string' ? a.rfqRef : (typeof a.rfqItemId === 'number' ? a.rfqItemId.toString() : '999')
    const bRef = typeof b.rfqRef === 'string' ? b.rfqRef : (typeof b.rfqItemId === 'number' ? b.rfqItemId.toString() : '999')
    if (aRef !== bRef) return aRef.localeCompare(bRef)
    // Secondary: Sort by ProcumentRecordSortOrder (supplier order within RFQ item)
    const aProcSo = typeof a.procumentRecordSortOrder === 'number' ? a.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
    const bProcSo = typeof b.procumentRecordSortOrder === 'number' ? b.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
    if (aProcSo !== bProcSo) return aProcSo - bProcSo
    // Tertiary: Fallback to quote item sortOrder
    const aSo = typeof a.sortOrder === 'number' ? a.sortOrder : Number.MAX_SAFE_INTEGER
    const bSo = typeof b.sortOrder === 'number' ? b.sortOrder : Number.MAX_SAFE_INTEGER
    return aSo - bSo
  })

  const primary = preset?.primaryColor || '#1a2744'
  const accent = preset?.accentColor || '#2563eb'
  const logoBase64 = preset?.logoBase64
    ? `data:${preset.logoMimeType};base64,${preset.logoBase64}`
    : null

  const payload = {
    companyName: preset?.name || '',
    companyLocation: preset?.location || '',
    companyPhone: preset?.phone || '',
    companyWebsite: preset?.website || '',
    companyEmail: preset?.email || '',
    logoBase64: logoBase64 || null,
    primaryColor: primary,
    accentColor: accent,
    quoteNumber: q.quoteNumber || '',
    quoteDate: q.createdAt ? new Date(q.createdAt).toLocaleDateString() : '—',
    validUntil: q.validUntil ? new Date(q.validUntil).toLocaleDateString() : '—',
    rfqName: q.rfqName || '—',
    currency: 'Dollar (USD)',
    currencySymbol: '$',
    exchangeRate: 1,
    customerName: q.customerName || '—',
    customerBillTo: q.customerBillTo || null,
    customerShipTo: q.customerShipTo || q.customerBillTo || null,
    items: items.map((it: any) => ({
      rfqReference: it.rfqReference || null,
      partNumberName: it.partNumberName || null,
      alt: it.alt || null,
      description: it.description || null,
      qty: it.qty || 0,
      condition: it.condition || null,
      leadTime: it.leadTime || null,
      unitPrice: Number(it.unitPrice) || 0,
      totalPrice: Number(it.totalPrice) || 0,
      certName: it.certName || null,
      tagDate: it.tagDate || null,
      note: it.note || null,
    })),
    subtotal: Number(q.totalAmount) || 0,
    tax: 0,
    shipping: 0,
    other: 0,
    comments: null,
    terms: preset?.termsAndConditions || null,
    footerText: 'If you have any questions about this quotation, please contact',
  }

  const response = await $fetch<Blob>(`${config.public.apiBase}/pdf/generate`, {
    method: 'POST',
    body: payload,
    responseType: 'blob',
    headers: { Authorization: `Bearer ${authStore.user?.token}` },
  })

  const url = window.URL.createObjectURL(response)
  const link = document.createElement('a')
  link.href = url
  const safe = (s: string) => (s || '').replace(/[/\\:*?"<>|]/g, '-').trim()
  link.setAttribute('download', `${safe(q.quoteNumber || 'QT')} - ${safe(q.customerName || '')} - ${safe(q.rfqName || '')}.pdf`)
  document.body.appendChild(link)
  link.click()
  link.parentNode?.removeChild(link)
  window.URL.revokeObjectURL(url)
}
</script>
