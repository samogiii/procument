<template>
  <v-dialog v-model="model" max-width="520">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-file-export-outline" class="mr-3" color="primary" />
        Payment Request (PR)
      </v-card-title>
      <v-card-text class="pa-4">
        <p class="text-body-2 text-medium-emphasis mb-4">
          Create a partial or full payment request for this PO, or edit and download an existing request.
        </p>

        <v-alert v-if="requestError" type="error" variant="tonal" class="mb-3">{{ requestError }}</v-alert>
        <v-select v-model="selectedRequestId" :items="requestOptions" item-title="label" item-value="id"
          label="Payment request" variant="outlined" :loading="loadingRequests" class="mb-3" />
        <p class="text-body-2 mb-3">PO total: ${{ formatPrice(poTotal) }} · Paid to supplier: ${{ formatPrice(totalPaid) }} · Remaining: ${{ formatPrice(Math.max(0, poTotal - totalPaid)) }}</p>
        <v-text-field v-model.number="requestAmount" label="Amount to request (USD)" type="number" min="0.01" step="0.01"
          prefix="$" variant="outlined" :hint="`Available for this request: $${formatPrice(availableAmount)}`" persistent-hint class="mb-3" />
        <p v-if="selectedRequest" class="text-caption mb-3">This PR: paid ${{ formatPrice(selectedRequest.paidAmount) }} · remaining ${{ formatPrice(selectedRequest.remainingAmount) }}</p>
        <v-btn v-if="selectedRequest && props.po?.status === 'PR Rejected'" color="error" variant="tonal"
          prepend-icon="mdi-delete" class="mb-3" :loading="deletingRequest" @click="deleteRejectedRequest">
          Delete rejected PR
        </v-btn>

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
          v-model.number="wireFee"
          label="Wire Fee (USD)"
          type="number"
          min="0"
          step="0.01"
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
          :disabled="loadingRequests || requestAmount <= 0 || requestAmount > availableAmount || requestAmount < (selectedRequest?.paidAmount || 0)"
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
const deletingRequest = ref(false)

const requestError = ref('')
const loadingRequests = ref(false)
const requests = ref<any[]>([])
const selectedRequestId = ref(0)
const requestAmount = ref(0)
const wireFee = ref(0)
const selectedRequest = computed(() => requests.value.find(r => r.id === selectedRequestId.value))
const savedWireFee = computed(() => Number(requests.value[0]?.wireFee ?? props.importDetail?.wirefee ?? 0))
const poTotal = computed(() => {
  const savedTotal = requests.value[0]?.poTotalAmount
  if (savedTotal != null) return Number(savedTotal) - savedWireFee.value + Number(wireFee.value || 0)
  return Number(props.po?.totalAmount || 0) + Number(props.po?.processingFee || 0)
    + Number(props.po?.shipping || 0) + Number(props.po?.tax || 0) + Number(wireFee.value || 0)
})
const totalPaid = computed(() => requests.value.reduce((sum, r) => sum + Number(r.paidAmount || 0), 0))
const availableAmount = computed(() => Math.max(0, poTotal.value - requests.value.filter(r => r.id !== selectedRequestId.value).reduce((sum, r) => sum + Number(r.amount || 0), 0)))
const requestOptions = computed(() => [{ id: 0, label: 'Create new payment request' }, ...requests.value.map(r => ({ id: r.id, label: `PR-${r.prNumber} · $${formatPrice(r.amount)} · paid $${formatPrice(r.paidAmount)}` }))])
watch(selectedRequestId, () => { requestAmount.value = selectedRequest.value?.amount ?? availableAmount.value })
watch(model, async (open) => {
  if (!open) return
  requestError.value = ''
  loadingRequests.value = true
  try {
    const all = await api.get<any[]>('/paymentrequests')
    requests.value = all.filter(r => Number(r.poId) === Number(props.poId))
    wireFee.value = Number(requests.value[0]?.wireFee ?? props.importDetail?.wirefee ?? 0)
    selectedRequestId.value = 0
    requestAmount.value = availableAmount.value
  } catch { requestError.value = 'Could not load payment requests. Reopen this dialog to retry.' }
  finally { loadingRequests.value = false }
})

const selectedPresetId = ref<number | null>(null)
const apiPresets = ref<any[]>([])
const bankFeeOption = ref<string>('OurCompanyAll')

const showPOPWarning = ref(false)
const popCheck = ref<any>(null)

function formatPrice(val: number) {
  return (val || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

async function deleteRejectedRequest() {
  if (!selectedRequest.value) return
  deletingRequest.value = true
  requestError.value = ''
  try {
    await api.del(`/paymentrequests/${selectedRequest.value.id}`)
    requests.value = requests.value.filter(r => r.id !== selectedRequest.value.id)
    selectedRequestId.value = 0
    requestAmount.value = availableAmount.value
  } catch (e: any) {
    requestError.value = e?.data?.message || 'The rejected PR could not be deleted.'
  } finally { deletingRequest.value = false }
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
    applyDefaultPreset()
  } catch {}
}

/**
 * Default the paying company to the preset chosen when the PO was created
 * (PO.PreferredWalletId → PaymentBox → CompanyPreset), else Base 105.
 */
function applyDefaultPreset() {
  if (!apiPresets.value.length) return
  const poPresetId = props.po?.companyPresetId
  const match = (poPresetId ? apiPresets.value.find(p => p.id === poPresetId) : null)
    || apiPresets.value.find(p => p.sortOrder === 105)
  if (match) selectedPresetId.value = match.id
}

// The PO is often loaded after this dialog mounts — re-apply once it arrives.
watch(() => props.po?.companyPresetId, () => applyDefaultPreset())

onMounted(() => {
  loadPresets()
  if (props.importDetail?.wirefee) {
    wireFee.value = props.importDetail.wirefee
  }
})

watch(() => props.importDetail, (val) => {
  if (val?.wirefee != null && !requests.value.length) wireFee.value = Number(val.wirefee)
}, { immediate: true })

watch(wireFee, (next, previous) => {
  const delta = Number(next || 0) - Number(previous || 0)
  if (!delta) return
  const previousAvailable = availableAmount.value - delta
  if (Math.abs(Number(requestAmount.value) - previousAvailable) < 0.01)
    requestAmount.value = Math.max(0, Number(requestAmount.value) + delta)
})

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
    requestError.value = ''
    const pr = selectedRequestId.value
      ? await api.patch<any>(`/paymentrequests/${selectedRequestId.value}/amount`, {
          amount: Number(requestAmount.value),
          wireFee: Number(wireFee.value || 0),
        })
      : await api.post<any>(`/paymentrequests/po/${props.poId}`, {
          companyPresetId: selectedPresetId.value ?? null,
          amount: Number(requestAmount.value),
          wireFee: Number(wireFee.value || 0),
        })
    requests.value = [...requests.value.filter(r => r.id !== pr.id), pr]
    selectedRequestId.value = pr.id
    wireFee.value = Number(pr.wireFee ?? wireFee.value ?? 0)

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
      beneficiary: props.importDetail?.beneficiary,
      reference: props.importDetail?.reference,
      accountNumber: props.importDetail?.bankAccountNumber,
      bankName: props.importDetail?.bankName,
      swiftCode: props.importDetail?.swiftCode,
      aba: props.importDetail?.aba,
      companyAddress: props.po?.supplier?.address || props.po?.supplierAddress,
      bankAddress: props.importDetail?.bankAddress,

      items,
      itemsTotal,
      wireFee: Number(pr.wireFee ?? wireFee.value ?? 0),
      grandTotal: Number(pr.amount),

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
    const grandTotalStr = `$${Number(pr.amount).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
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

    // A successfully generated and downloaded PR enters the payment queue.
    await api.patch(`/purchase-orders/${props.poId}/status`, { status: 'Waiting For Payment' })

    model.value = false
  } catch (e: any) {
    requestError.value = e?.data?.message || 'Failed to generate payment request.'
  } finally {
    generating.value = false
  }
}
</script>
