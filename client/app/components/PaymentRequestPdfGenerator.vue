<template>
  <v-dialog v-model="model" max-width="520">
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
          label="Company Preset (Paying From)"
          variant="outlined"
          density="comfortable"
          persistent-hint
          hint="Select the company paying from"
          class="mb-3"
        />

        <v-text-field
          v-if="isAdmin"
          v-model="wireFee"
          label="Wire Fee"
          type="number"
          prefix="$"
          variant="outlined"
          density="comfortable"
          class="mb-3"
        />

        <!-- Bank Fee Option as v-select -->
        <v-select
          v-model="bankFeeOption"
          :items="bankFeeItems"
          item-title="label"
          item-value="value"
          label="Bank Charges / 银行费用"
          variant="outlined"
          density="comfortable"
          persistent-hint
          hint="Select who pays bank fees"
        >
          <template #item="{ item, props: itemProps }">
            <v-list-item v-bind="itemProps" :title="undefined">
              <template #title>
                <div>
                  <div class="text-body-2 font-weight-medium">{{ item.raw.chinese }}</div>
                  <div class="text-caption text-medium-emphasis">{{ item.raw.english }}</div>
                </div>
              </template>
            </v-list-item>
          </template>
          <template #selection="{ item }">
            <div>
              <div class="text-body-2 font-weight-medium">{{ item.raw.chinese }}</div>
              <div class="text-caption text-medium-emphasis">{{ item.raw.english }}</div>
            </div>
          </template>
        </v-select>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="model = false">Cancel</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :loading="generating"
          prepend-icon="mdi-download"
          @click="checkAndGenerate"
        >Generate & Download</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- POP Insufficient Warning Dialog -->
  <v-dialog v-model="showPOPWarning" max-width="480" persistent>
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center text-warning">
        <v-icon icon="mdi-alert-outline" class="mr-2" color="warning" />
        Prepayment Not Completed
      </v-card-title>
      <v-card-text>
        <v-alert type="warning" variant="tonal" class="mb-3">
          The customer's Proof of Payment (POP) does not meet the required prepayment percentage.
        </v-alert>
        <v-row dense class="text-body-2">
          <v-col cols="6">Required ({{ popCheck?.prepaymentPercent }}%):</v-col>
          <v-col cols="6" class="font-weight-bold">${{ formatPrice(popCheck?.requiredAmount ?? 0) }}</v-col>
          <v-col cols="6">Total POP Received:</v-col>
          <v-col cols="6" class="font-weight-bold" :class="(popCheck?.totalPaid ?? 0) < (popCheck?.requiredAmount ?? 0) ? 'text-error' : 'text-success'">
            ${{ formatPrice(popCheck?.totalPaid ?? 0) }}
          </v-col>
          <v-col cols="6">Remaining:</v-col>
          <v-col cols="6" class="font-weight-bold text-error">
            ${{ formatPrice(Math.max(0, (popCheck?.requiredAmount ?? 0) - (popCheck?.totalPaid ?? 0))) }}
          </v-col>
        </v-row>
        <div class="mt-3 text-caption text-medium-emphasis">
          Are you sure you want to create a PR for this item? The prepayment is not yet completed.
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="showPOPWarning = false">Cancel</v-btn>
        <v-btn color="warning" variant="flat" :loading="generating" @click="generate">Yes, Proceed Anyway</v-btn>
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
const bankFeeOption = ref<string>('OurCompanyAll')

const showPOPWarning = ref(false)
const popCheck = ref<any>(null)

function formatPrice(val: number) {
  return (val || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

const bankFeeItems = [
  {
    value: 'OurCompanyAll',
    label: '本公司支付所有的银行费用',
    chinese: '本公司支付所有的银行费用',
    english: 'Our company pays all bank fees.',
  },
  {
    value: 'OurCompanyLocal',
    label: '本公司支付本地银行费用，受款人支付海外银行费用。',
    chinese: '本公司支付本地银行费用，受款人支付海外银行费用。',
    english: 'Our company pays local bank fees; the recipient pays overseas bank fees.',
  },
  {
    value: 'RecipientAll',
    label: '受款公司支付所有的银行费用',
    chinese: '受款公司支付所有的银行费用',
    english: 'The recipient company pays all bank fees.',
  },
]

async function loadPresets() {
  try {
    apiPresets.value = await api.get('/companypresets')
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

const selectedPreset = computed(() => apiPresets.value.find(p => p.id === selectedPresetId.value))

// Derive customer code from enriched items (they carry customerCode from RFQ)
const customerCode = computed(() => {
  const items = props.enriched?.items || []
  return items.find((it: any) => it.customerCode)?.customerCode || ''
})

async function checkAndGenerate() {
  // If the PO is linked to a Prepayment invoice, verify POP is sufficient before proceeding
  if (props.po?.invoiceId) {
    try {
      const check = await api.get<any>(`/invoices/${props.po.invoiceId}/prepayment-check`)
      popCheck.value = check
      if (!check.isSufficient) {
        showPOPWarning.value = true
        return
      }
    } catch {
      // If check fails (e.g., not a prepayment invoice), proceed normally
    }
  }
  await generate()
}

async function generate() {
  showPOPWarning.value = false
  generating.value = true
  try {
    // 1. Create / fetch PR record
    const pr = await api.post<any>(`/paymentrequests/po/${props.poId}`, {
      companyPresetId: selectedPresetId.value ?? null,
    })

    // 2. Build PDF payload
    const preset = selectedPreset.value

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

      // Our company bank details (from selected preset)
      companyPayingFrom: preset?.name || pr.companyPayingFrom || '—',
      ourBeneficiaryName: preset?.beneficiaryName,
      ourAccountNumber: preset?.accountNumber,
      ourBankName: preset?.bankName,
      ourSwiftCode: preset?.swiftCode,
      ourBankAddress: preset?.bankAddress,
      ourCompanyAddress: preset?.location,

      // Supplier bank details
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

      bankFeeOption: bankFeeOption.value,

      // Theme
      primaryColor: preset?.primaryColor,
      accentColor: preset?.accentColor,
      logoBase64: preset?.logoBase64,
      companyName: preset?.name,
      companyLocation: preset?.location,
    }

    // 3. Generate PDF
    const blob = await $fetch<Blob>(`${api.baseURL}/pdf/payment-request`, {
      method: 'POST',
      body: payload,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
      responseType: 'blob',
    })

    // 4. Build filename: "Cost Price-{PRNumber}-{CustomerCode}-{SupplierName}.pdf"
    const prNum = String(pr.prNumber).padStart(5, '0')
    const custCode = customerCode.value || 'Unknown'
    const supplierPart = (props.po?.supplierName || 'Supplier').replace(/[/\\?%*:|"<>]/g, '-').trim()
    const grandTotalStr = `$${(itemsTotal + Number(wireFee.value || 0)).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
    const fileName = `${grandTotalStr}-${prNum}-${custCode}-${supplierPart}.pdf`

    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', fileName)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)

    // 5. Auto-upload to supplier folder
    const form = new FormData()
    const file = new File([blob], fileName, { type: 'application/pdf' })
    form.append('file', file)
    form.append('category', 'dp')
    await $fetch(`${api.baseURL}/documents/proforma-invoice/${props.po.invoiceId}/supplier/${props.po.supplierId}/upload`, {
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
