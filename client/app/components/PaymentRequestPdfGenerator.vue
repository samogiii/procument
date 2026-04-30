<template>
  <v-dialog v-model="model" max-width="500">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-file-export-outline" class="mr-3" color="primary" />
        Payment Request (PR)
      </v-card-title>
      <v-card-text class="pa-4">
        <p class="text-body-2 text-medium-emphasis mb-4">
          Generate a Payment Request document. This will create a permanent record in the database with a PR Number (starting from 1501).
        </p>

        <v-select
          v-model="selectedPresetId"
          :items="apiPresets"
          item-title="name"
          item-value="id"
          label="Company Preset"
          variant="outlined"
          density="comfortable"
          hint="Select the company paying from"
          persistent-hint
        />
        
        <v-text-field
          v-if="isAdmin"
          v-model="wireFee"
          label="Wire Fee"
          type="number"
          prefix="$"
          variant="outlined"
          density="comfortable"
          class="mt-4"
        />
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="model = false">Cancel</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :loading="generating"
          prepend-icon="mdi-download"
          @click="generate"
        >Generate & Download</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  poId: string | number
  po?: any
  importDetail?: any
  enriched?: any
}>()

const model = defineModel<boolean>()
const api = useApi()
const authStore = useAuthStore()
const config = useRuntimeConfig()
const generating = ref(false)

const isAdmin = computed(() => authStore.isAdmin)
const wireFee = ref(0)
const selectedPresetId = ref<number | null>(null)
const apiPresets = ref<any[]>([])

async function loadPresets() {
  try {
    apiPresets.value = await api.get('/companypresets')
    // Default to a preset based on customer base if possible
    const match = apiPresets.value.find(p => p.sortOrder === 105)
    if (match) selectedPresetId.value = match.id
  } catch {}
}

onMounted(() => {
  loadPresets()
  if (props.importDetail?.wirefee) {
    wireFee.value = props.importDetail.wirefee
  }
})

watch(() => props.importDetail, (val) => {
  if (val?.wirefee) wireFee.value = val.wirefee
}, { immediate: true })

async function generate() {
  generating.value = true
  try {
    // 1. Create the PR record (or get existing)
    const pr = await api.post<any>(`/paymentrequests/po/${props.poId}`)
    
    // 2. Build the PDF payload
    const preset = apiPresets.value.find(p => p.id === selectedPresetId.value)
    
    // Build items from enriched trail if available, else from props.po.items
    const trailItems = (props.enriched?.items || []).filter((it: any) => 
      it.poSupplier && it.poSupplier === props.po?.supplierName
    )
    const items = (trailItems.length ? trailItems : (props.po?.items || [])).map((it: any) => ({
      partNumber: it.partNumber || it.partNumberName || '—',
      description: it.description || '—',
      qty: it.qty || 0,
      unitPrice: it.poUnitPrice ?? it.unitPrice ?? 0,
      totalPrice: it.poTotalPrice ?? it.totalPrice ?? 0,
    }))
    
    const itemsTotal = items.reduce((s: number, i: any) => s + Number(i.totalPrice || 0), 0)

    const payload = {
      prNumber: `PR${String(pr.prNumber).padStart(5, '0')}`,
      documentDate: new Date().toISOString().slice(0, 10),
      poNumber: props.po?.poNumber,
      supplierName: props.po?.supplierName,
      currency: props.po?.currency || 'USD',
      currencySymbol: '$',
      status: pr.status || 'PENDING APPROVAL',
      
      companyPayingFrom: preset?.name || pr.companyPayingFrom || 'JETRUX',
      companyPayingTo: props.po?.supplierName,
      accountNumber: props.importDetail?.bankAccountNumber,
      bankName: props.importDetail?.bankName,
      swiftCode: props.importDetail?.swiftCode,
      aba: props.importDetail?.aba,
      companyAddress: props.po?.supplier?.address || props.po?.supplierAddress,
      bankAddress: props.importDetail?.bankAddress,
      
      items,
      itemsTotal,
      wireFee: Number(wireFee.value || 0),
      grandTotal: itemsTotal + Number(wireFee.value || 0),
      
      // Theme
      primaryColor: preset?.primaryColor,
      accentColor: preset?.accentColor,
      logoBase64: preset?.logoBase64,
      companyName: preset?.name,
      companyLocation: preset?.location,
    }

    // 3. Generate PDF
    const blob = await $fetch<Blob>(`${config.public.apiBase}/pdf/payment-request`, {
      method: 'POST',
      body: payload,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
      responseType: 'blob',
    })

    // 4. Download
    const fileName = `${payload.prNumber}.pdf`
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', fileName)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)

    // 5. Auto-upload to supplier folder (as requested previously for DP)
    const form = new FormData()
    const file = new File([blob], fileName, { type: 'application/pdf' })
    form.append('file', file)
    form.append('category', 'dp') // Keep category as 'dp' for compatibility or change to 'pr'
    await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${props.po.invoiceId}/supplier/${props.po.supplierId}/upload`, {
      method: 'POST',
      body: form,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })

    model.value = false
  } catch (e: any) {
    console.error('Failed to generate PR', e)
  } finally {
    generating.value = false
  }
}
</script>
