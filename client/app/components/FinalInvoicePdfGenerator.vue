<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Final Invoice PDF — {{ pdfData.invoiceNumber || '' }}</v-toolbar-title>
        <v-spacer />
        <v-btn variant="tonal" color="info" prepend-icon="mdi-package-variant" :loading="generatingPacking" @click="downloadPackingList" class="mr-2">Download Packing List</v-btn>
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" @click="downloadPdf">Download PDF</v-btn>
      </v-toolbar>

      <v-container fluid class="flex-shrink-0 py-4">
        <v-row dense align="center">
          <v-col cols="12" md="3"><v-select v-model="selectedPreset" :items="companyPresetOptions" label="Company" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-domain" :loading="presetsLoading" /></v-col>
          <v-col cols="12" md="3"><v-file-input label="Company Logo" variant="outlined" density="compact" hide-details accept="image/*" prepend-icon="mdi-image" @update:model-value="onLogoUpload" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyName" label="Company Name" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyEmail" label="Contact Email" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="3"><v-text-field v-model="companyPhone" label="Phone" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyWebsite" label="Website" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model.number="taxAmount" label="Tax" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model.number="otherAmount" label="Other" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" /></v-col>
          <v-col cols="12" md="2"><v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD', 'China Yuan (CNY)']" label="Currency" variant="outlined" density="compact" hide-details :disabled="currencyLocked" /></v-col>
          <v-col v-if="currency === 'China Yuan (CNY)'" cols="12" md="2"><v-text-field v-model.number="exchangeRate" label="Exchange Rate" variant="outlined" density="compact" hide-details type="number" step="0.0001" /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="3"><v-text-field v-model="contactPerson" label="Contact Person" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-textarea v-model="billTo" label="Bill To (address)" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="3"><v-textarea v-model="shipTo" label="Ship To (address)" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="3"><v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="4"><v-text-field v-model="billToContactPerson" label="Bill To Contact Person" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="4"><v-text-field v-model="billToEmail" label="Bill To Email" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="4"><v-text-field v-model="billToPhone" label="Bill To Phone" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="4"><v-text-field v-model="shipToContactPerson" label="Ship To Contact Person" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="4"><v-text-field v-model="shipToEmail" label="Ship To Email" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="4"><v-text-field v-model="shipToPhone" label="Ship To Phone" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="6"><v-textarea v-model="companyTerms" label="Terms & Conditions" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="6"><v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="3"><v-text-field v-model="beneficiaryName" label="Beneficiary Name" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="beneficiaryAddress" label="Beneficiary Address" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="bankName" label="Bank Name" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="bankAddress" label="Bank Address" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="6"><v-text-field v-model="bankAccount" label="Bank Account" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="6"><v-text-field v-model="swiftCode" label="Swift Code" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
      </v-container>

      <v-divider />

      <div v-if="loadingData" class="flex-grow-1 d-flex justify-center align-center">
        <v-progress-circular indeterminate color="primary" size="48" />
      </div>
      <div v-else class="flex-grow-1 overflow-y-auto d-flex justify-center pa-6" style="background: rgb(var(--v-theme-surface-variant));">
        <div ref="pdfContent" class="pdf-page" v-html="renderedHtml" />
      </div>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{ invoiceId: number | string }>()
const model = defineModel<boolean>({ default: false })
const api = useApi()

// ── Presets ──
const apiPresets = ref<any[]>([])
const presetsLoading = ref(false)
const selectedPreset = ref<string>('Custom')

async function loadPresets() {
  presetsLoading.value = true
  try { apiPresets.value = await api.get<any[]>('/companypresets') }
  catch { apiPresets.value = [] }
  finally { presetsLoading.value = false }
}

const companyPresetOptions = computed(() => [
  ...apiPresets.value.map((p: any) => p.name),
  'Custom',
])

const theme = computed(() => {
  const preset = apiPresets.value.find((p: any) => p.name === selectedPreset.value)
  return {
    primary: preset?.primaryColor || '#1a2744',
    accent:  preset?.accentColor  || '#2563eb',
  }
})

watch(apiPresets, (presets) => {
  if (!presets.length) return
  if (selectedPreset.value === 'Custom') selectedPreset.value = presets[0].name
})

watch(selectedPreset, (val) => {
  const preset = apiPresets.value.find((p: any) => p.name === val)
  if (preset) {
    companyName.value = preset.name
    companyLocation.value = preset.location || ''
    companyPhone.value = preset.phone || ''
    companyWebsite.value = preset.website || ''
    companyEmail.value = preset.email || ''
    logoDataUrl.value = preset.logoBase64
      ? `data:${preset.logoMimeType};base64,${preset.logoBase64}`
      : ''
    // Prefer customer's terms & conditions over preset's
    companyTerms.value = pdfData.value?.customerTermsAndConditions || preset.termsAndConditions || ''
  }
})

const companyName = ref('Your Company Name')
const companyLocation = ref('')
const companyPhone = ref('')
const companyWebsite = ref('')
const companyEmail = ref('')
const footerText = ref('If you have any questions about this invoice, please contact')
const logoDataUrl = ref('')
const generating = ref(false)
const generatingPacking = ref(false)
const loadingData = ref(false)
const pdfData = ref<any>({})
const taxAmount = ref(0)
const otherAmount = ref(0)
const currency = ref('Dollar (USD)')
const exchangeRate = ref(7.0)
const comments = ref('No Comments')
const contactPerson = ref('')
const billTo = ref('')
const shipTo = ref('')
const companyTerms = ref('')

// Bill To contact fields
const billToContactPerson = ref('')
const billToEmail = ref('')
const billToPhone = ref('')

// Ship To contact fields
const shipToContactPerson = ref('')
const shipToEmail = ref('')
const shipToPhone = ref('')
const beneficiaryName = ref('')
const beneficiaryAddress = ref('')
const bankName = ref('')
const bankAddress = ref('')
const bankAccount = ref('')
const swiftCode = ref('')

function onLogoUpload(files: File[] | File | null) {
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { logoDataUrl.value = ''; return }
  const reader = new FileReader()
  reader.onload = (e) => { logoDataUrl.value = (e.target?.result as string) || '' }
  reader.readAsDataURL(file)
}

watch(model, async (open) => {
  if (open) {
    loadPresets()
    if (!pdfData.value.invoiceNumber) {
      loadingData.value = true
      try {
        const data = await api.get<any>(`/final-invoices/${props.invoiceId}/pdf-data`)
        pdfData.value = data
        if (data.notes) comments.value = data.notes
        if (data.customerBase == 3) {
          const currencyType = data.customerCurrencyType || 'Dollar'
          if (currencyType === 'Dollar') {
            currency.value = 'Dollar (USD)'
            exchangeRate.value = 1
          } else if (currencyType === 'Yuan') {
            currency.value = 'China Yuan (CNY)'
            exchangeRate.value = 7
          } else if (currencyType === 'Both') {
            currency.value = 'Dollar (USD)'
            exchangeRate.value = 7
            // For Both, default to Dollar but allow user to switch
          }
        }
        contactPerson.value = data.customerContactPerson || ''
        billTo.value = data.customerBillTo || ''
        shipTo.value = data.customerShipTo || data.customerBillTo || ''
        // Populate separate contact fields from single Customer fields
        billToContactPerson.value = data.customerContactPerson || ''
        billToEmail.value = data.customerBillToEmail || ''
        billToPhone.value = data.customerBillToPhone || ''
        shipToContactPerson.value = data.customerContactPerson || ''
        shipToEmail.value = data.customerShipToEmail || ''
        shipToPhone.value = data.customerShipToPhone || ''
        // Apply customer terms if present (overrides preset terms)
        if (data.customerTermsAndConditions) {
          companyTerms.value = data.customerTermsAndConditions
        }
      } catch (e) { console.error('[FinalInvoicePdf] Failed to load data', e) }
      finally { loadingData.value = false }
    }
  }
})

const fmt = (n: number) => formatPrice(n)

// ── Currency lock based on customer settings ──
const currencyLocked = computed(() => {
  if (pdfData.value.customerBase !== 3) return false
  const currencyType = pdfData.value.customerCurrencyType || 'Dollar'
  return currencyType !== 'Both'
})

const renderedHtml = computed(() => {
  const d = pdfData.value
  if (!d.invoiceNumber) return ''

  const items: any[] = d.items || []
  const logo = logoDataUrl.value
  const logoImg = logo ? `<img src="${logo}" style="max-height:48px; max-width:160px; object-fit:contain;" />` : ''

  const invDate = d.createdAt ? new Date(d.createdAt).toLocaleDateString() : '—'
  const dueDate = d.dueDate ? new Date(d.dueDate).toLocaleDateString() : '—'
  const isYuan = currency.value === 'China Yuan (CNY)'
  const sym = isYuan ? '¥' : '$'
  const rate = isYuan ? (exchangeRate.value || 1) : 1
  const subtotal = (Number(d.totalAmount) || 0) * rate
  const shippingCost = (Number(d.shippingCost) || 0) * rate
  const tax = (taxAmount.value || 0) * rate
  const other = (otherAmount.value || 0) * rate
  const totalDiscount = items.reduce((sum: number, it: any) => sum + (Number(it.discount) || 0), 0) * rate
  const grandTotal = subtotal - totalDiscount + shippingCost + tax + other

  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : '#f7f8fa'
    const discountCell = it.discount > 0
      ? `<td style="padding:9px 8px; font-size:11px; text-align:right; font-weight:600; color:#e53935; border-bottom:1px solid #eef0f3;">-${sym}${fmt(Number(it.discount) * rate)}</td>`
      : `<td style="padding:9px 8px; font-size:10.5px; text-align:center; color:#9ca3af; border-bottom:1px solid #eef0f3;">—</td>`
    return `
    <tr style="background:${bg};">
      <td style="padding:9px 8px; font-size:10px; color:#9ca3af; text-align:center; border-bottom:1px solid #eef0f3;">${it.rfqReference || '—'}</td>
      <td style="padding:9px 8px; font-size:11px; color:#6b7280; text-align:center; border-bottom:1px solid #eef0f3;">${i + 1}</td>
      <td style="padding:9px 10px; font-size:11px; font-weight:600; color:#1a2744; border-bottom:1px solid #eef0f3;">${it.partNumber || '—'}</td>
      <td style="padding:9px 10px; font-size:10.5px; color:#4b5563; border-bottom:1px solid #eef0f3;">${it.description || '—'}</td>
      <td style="padding:9px 8px; font-size:11px; text-align:center; font-weight:600; color:#1a2744; border-bottom:1px solid #eef0f3;">${it.qty}</td>
      <td style="padding:9px 8px; font-size:10.5px; text-align:center; color:#1a2744; border-bottom:1px solid #eef0f3;">${it.condition || '—'}</td>
      <td style="padding:9px 8px; font-size:10.5px; text-align:center; color:#4b5563; border-bottom:1px solid #eef0f3;">${it.certification || '—'}</td>
      <td style="padding:9px 10px; font-size:11px; text-align:right; color:#1a2744; border-bottom:1px solid #eef0f3;">${sym}${fmt(Number(it.unitPrice) * rate)}</td>
      <td style="padding:9px 10px; font-size:11px; text-align:right; font-weight:700; color:#1a2744; border-bottom:1px solid #eef0f3;">${sym}${fmt(Number(it.totalPrice) * rate)}</td>
      ${discountCell}
      <td style="padding:9px 10px; font-size:10.5px; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.trackNumber || ''}</td>
      <td style="padding:9px 10px; font-size:10.5px; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.carrier || ''}</td>
    </tr>`
  }).join('')

  return `
    <div style="font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif; color:#1a2744; display:flex; flex-direction:column; min-height:297mm;">

      <!-- ═══ Header ═══ -->
      <div style="padding:28px 40px 20px 40px; display:flex; justify-content:space-between; align-items:flex-start;">
        <div style="display:flex; align-items:center; gap:16px;">
          ${logoImg}
          <div>
            <div style="font-size:20px; font-weight:700; color:#1a2744; letter-spacing:0.3px;">${companyName.value}</div>
            <div style="font-size:9.5px; color:#6b7280; margin-top:3px; line-height:1.6;">${companyLocation.value}<br/>Tel: ${companyPhone.value} &nbsp;|&nbsp; ${companyWebsite.value}</div>
          </div>
        </div>
        <div style="text-align:right;">
          <div style="font-size:24px; font-weight:700; color:#1a2744; letter-spacing:1px;">INVOICE</div>
          <div style="font-size:11px; color:#6b7280; margin-top:4px;">${d.invoiceNumber}</div>
        </div>
      </div>

      <div style="height:3px; background:linear-gradient(90deg,#1a2744 0%,#2563eb 40%,#e5e7eb 100%); margin:0 40px;"></div>

      <!-- ═══ Meta Row ═══ -->
      <div style="padding:16px 40px; display:flex; gap:40px; font-size:11px; color:#4b5563;">
        <div><span style="font-weight:600; color:#1a2744;">Date:</span> ${invDate}</div>
        <div><span style="font-weight:600; color:#1a2744;">Due Date:</span> ${dueDate}</div>
        <div><span style="font-weight:600; color:#1a2744;">Customer PO:</span> ${d.customerPONumber || '—'}</div>
        <div><span style="font-weight:600; color:#1a2744;">Proforma Ref:</span> ${d.proformaInvoiceNumber || '—'}</div>
        <div><span style="font-weight:600; color:#1a2744;">Currency:</span> ${currency.value}</div>
      </div>

      <!-- ═══ Bill To / Ship To ═══ -->
      <div style="display:flex; gap:0; margin:0 40px 20px 40px; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Bill To</div>
          <div style="font-size:12px; font-weight:700; color:#1a2744; margin-bottom:3px;">${d.customerName || '—'}</div>
          ${billToContactPerson.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Contact: ${billToContactPerson.value}</div>` : ''}
          ${(billTo.value || d.customerBillTo) ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${billTo.value || d.customerBillTo}</div>` : ''}
          ${billToEmail.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Email: ${billToEmail.value}</div>` : ''}
          ${billToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Phone: ${billToPhone.value}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Ship To</div>
          <div style="font-size:12px; font-weight:700; color:#1a2744; margin-bottom:3px;">${d.customerName || '—'}</div>
          ${shipToContactPerson.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Contact: ${shipToContactPerson.value}</div>` : ''}
          ${(shipTo.value || d.customerShipTo || d.customerBillTo) ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${shipTo.value || d.customerShipTo || d.customerBillTo}</div>` : ''}
          ${shipToEmail.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Email: ${shipToEmail.value}</div>` : ''}
          ${shipToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Phone: ${shipToPhone.value}</div>` : ''}
          ${d.customerShipToAccount ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Account: ${d.customerShipToAccount}</div>` : ''}
        </div>
      </div>

      <!-- ═══ Items Table ═══ -->
      <div style="margin:0 40px 16px 40px;">
        <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
          <thead>
            <tr style="background:#1a2744;">
              <th style="padding:10px 8px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:30px;">Ref</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:30px;">#</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Part No.</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Description</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:30px;">Qty</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:30px;">CD</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center;">Cert</th>
              <th style="padding:10px 10px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Unit Price</th>
              <th style="padding:10px 10px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Total</th>
              <th style="padding:10px 8px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Discount</th>
              <th style="padding:10px 10px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Track #</th>
              <th style="padding:10px 10px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Carrier</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>

      <!-- ═══ Totals + Shipping + Bank Details ═══ -->
      <div style="display:flex; justify-content:space-between; align-items:flex-start; margin:0 40px 16px 40px;">
        <div style="font-size:11px; max-width:340px;">
          ${d.shippingMethod ? `
            <div style="border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px; margin-bottom:12px;">
              <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:6px;">Shipping Information</div>
              <div style="color:#1a2744;"><span style="font-weight:600;">Shipping Method:</span> ${d.shippingMethod}</div>
            </div>
          ` : ''}
          ${bankName.value ? `
            <div style="border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
              <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:6px;">Bank Details</div>
              ${beneficiaryName.value ? `<div style="color:#1a2744; margin-bottom:2px;"><span style="font-weight:600;">Beneficiary:</span> ${beneficiaryName.value}</div>` : ''}
              ${beneficiaryAddress.value ? `<div style="color:#1a2744; margin-bottom:2px;"><span style="font-weight:600;">Beneficiary Address:</span> ${beneficiaryAddress.value}</div>` : ''}
              ${bankName.value ? `<div style="color:#1a2744; margin-bottom:2px;"><span style="font-weight:600;">Bank Name:</span> ${bankName.value}</div>` : ''}
              ${bankAddress.value ? `<div style="color:#1a2744; margin-bottom:2px;"><span style="font-weight:600;">Bank Address:</span> ${bankAddress.value}</div>` : ''}
              ${bankAccount.value ? `<div style="color:#1a2744; margin-bottom:2px;"><span style="font-weight:600;">Bank Account:</span> ${bankAccount.value}</div>` : ''}
              ${swiftCode.value ? `<div style="color:#1a2744; margin-bottom:2px;"><span style="font-weight:600;">Swift Code:</span> ${swiftCode.value}</div>` : ''}
            </div>
          ` : ''}
        </div>
        <div style="min-width:260px;">
          <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden; font-size:11px;">
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Subtotal</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(subtotal)}</td></tr>
            ${totalDiscount > 0 ? `<tr><td style="padding:8px 14px; color:#e53935; font-weight:600; border-bottom:1px solid #eef0f3;">Discount</td><td style="padding:8px 14px; text-align:right; font-weight:600; color:#e53935; border-bottom:1px solid #eef0f3;">-${sym}${fmt(totalDiscount)}</td></tr>` : ''}
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Tax</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(tax)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Shipping</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(shippingCost)}</td></tr>
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #e5e7eb;">Other</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #e5e7eb;">${sym}${fmt(other)}</td></tr>
            <tr style="background:#1a2744;"><td style="padding:10px 14px; color:#fff; font-weight:700;">Total</td><td style="padding:10px 14px; text-align:right; color:#fff; font-weight:800; font-size:14px;">${sym}${fmt(grandTotal)}</td></tr>
          </table>
        </div>
      </div>

      <!-- ═══ Comments ═══ -->
      <div style="margin:0 40px 16px 40px; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:5px;">Comments</div>
        <div style="font-size:11px; color:#4b5563; white-space:pre-wrap; line-height:1.5;">${comments.value || 'No Comments'}</div>
      </div>

      <!-- ═══ Terms & Conditions ═══ -->
      ${companyTerms.value ? `
      <div style="margin:0 40px 16px 40px; background:#f8fafc; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:5px;">Terms &amp; Conditions</div>
        <div style="font-size:10px; color:#4b5563; white-space:pre-wrap; line-height:1.6;">${companyTerms.value}</div>
      </div>` : ''}

      <!-- ═══ Footer ═══ -->
      <div style="margin-top:auto; padding:16px 40px; border-top:2px solid #1a2744; display:flex; justify-content:space-between; align-items:center;">
        <span style="font-size:10px; color:#6b7280;">${footerText.value}</span>
        <span style="font-size:10px; font-weight:600; color:#1a2744;">${companyEmail.value}</span>
      </div>
    </div>
  `
})

const pdfContent = ref<HTMLElement | null>(null)
async function downloadPdf() {
  generating.value = true
  try {
    const config = useRuntimeConfig()
    const authStore = useAuthStore()
    const d = pdfData.value
    const items: any[] = d.items || []

    // Check if customerCurrencyType is Both - generate two PDFs
    const currencyType = pdfData.value.customerCurrencyType || 'Dollar'
    const isBoth = pdfData.value.customerBase === 3 && currencyType === 'Both'

    const currenciesToGenerate = isBoth
      ? [{ currency: 'Dollar (USD)', symbol: '$', rate: 1 }, { currency: 'China Yuan (CNY)', symbol: '¥', rate: 7 }]
      : [{ currency: currency.value, symbol: currency.value === 'China Yuan (CNY)' ? '¥' : '$', rate: currency.value === 'China Yuan (CNY)' ? (exchangeRate.value || 1) : 1 }]

    for (const curr of currenciesToGenerate) {
      const payload = {
        companyName: companyName.value,
        companyLocation: companyLocation.value,
        companyPhone: companyPhone.value,
        companyWebsite: companyWebsite.value,
        companyEmail: companyEmail.value,
        logoBase64: logoDataUrl.value || null,
        primaryColor: theme.value.primary,
        accentColor: theme.value.accent,
        invoiceNumber: d.invoiceNumber || '',
        invoiceDate: d.createdAt ? new Date(d.createdAt).toLocaleDateString() : '—',
        dueDate: d.dueDate ? new Date(d.dueDate).toLocaleDateString() : '—',
        proformaRef: d.proformaInvoiceNumber || null,
        customerPONumber: d.customerPONumber || null,
        currency: curr.currency,
        currencySymbol: curr.symbol,
        exchangeRate: curr.rate,
        customerName: d.customerName || '—',
        customerContactPerson: contactPerson.value || d.customerContactPerson || null,
        customerBillTo: billTo.value || d.customerBillTo || null,
        customerBillToEmail: billToEmail.value || d.customerBillToEmail || null,
        customerBillToPhone: billToPhone.value || d.customerBillToPhone || null,
        customerBillToContactPerson: billToContactPerson.value || d.customerContactPerson || null,
        customerShipTo: shipTo.value || d.customerShipTo || d.customerBillTo || null,
        customerShipToContactPerson: shipToContactPerson.value || d.customerContactPerson || null,
        customerShipToEmail: shipToEmail.value || d.customerShipToEmail || null,
        customerShipToPhone: shipToPhone.value || d.customerShipToPhone || null,
        customerShipToAccount: d.customerShipToAccount || null,
        beneficiaryName: beneficiaryName.value || null,
        beneficiaryAddress: beneficiaryAddress.value || null,
        bankName: bankName.value || null,
        bankAddress: bankAddress.value || null,
        bankAccount: bankAccount.value || null,
        swiftCode: swiftCode.value || null,
        shippingMethod: d.shippingMethod || null,
        shippingCost: Number(d.shippingCost) || 0,
        items: items.map((it: any) => ({
          rfqReference: it.rfqReference || null,
          partNumber: it.partNumber || null,
          description: it.description || null,
          qty: it.qty || 0,
          condition: it.condition || null,
          certification: it.certification || null,
          unitPrice: (Number(it.unitPrice) || 0) * curr.rate,
          totalPrice: (Number(it.totalPrice) || 0) * curr.rate,
          discount: it.discount > 0 ? Number(it.discount) * curr.rate : null,
          trackNumber: it.trackNumber || null,
          carrier: it.carrier || null,
        })),
        subtotal: (Number(d.totalAmount) || 0) * curr.rate,
        tax: (taxAmount.value || 0) * curr.rate,
        other: (otherAmount.value || 0) * curr.rate,
        comments: comments.value || null,
        terms: companyTerms.value || null,
        footerText: footerText.value || null,
      }

      const response = await $fetch<Blob>(`${config.public.apiBase}/pdf/final-invoice`, {
        method: 'POST',
        body: payload,
        responseType: 'blob',
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
      const url = window.URL.createObjectURL(response)
      const link = document.createElement('a')
      link.href = url
      const customerName = (d.customerName || '').replace(/\s+/g, '_')
      const currencySuffix = curr.currency.includes('CNY') ? ' - Yuan' : ' - Dollar'
      link.setAttribute('download', `${d.invoiceNumber || 'FinalInvoice'}-${customerName}${currencySuffix}.pdf`)
      document.body.appendChild(link)
      link.click()
      link.parentNode?.removeChild(link)
      window.URL.revokeObjectURL(url)
    }
  } catch (err) { console.error('PDF generation failed:', err) }
  finally { generating.value = false }
}

async function downloadPackingList() {
  generatingPacking.value = true
  try {
    const config = useRuntimeConfig()
    const authStore = useAuthStore()
    const d = pdfData.value
    const items: any[] = d.items || []

    const payload = {
      companyName: companyName.value,
      companyLocation: companyLocation.value,
      companyPhone: companyPhone.value,
      companyWebsite: companyWebsite.value,
      companyEmail: companyEmail.value,
      logoBase64: logoDataUrl.value || null,
      primaryColor: theme.value.primary,
      accentColor: theme.value.accent,
      invoiceNumber: d.invoiceNumber || '',
      invoiceDate: d.createdAt ? new Date(d.createdAt).toLocaleDateString() : '—',
      customerPONumber: d.customerPONumber || null,
      customerName: d.customerName || '—',
      customerContactPerson: contactPerson.value || d.customerContactPerson || null,
      customerBillTo: billTo.value || d.customerBillTo || null,
      customerBillToEmail: billToEmail.value || d.customerBillToEmail || null,
      customerBillToPhone: billToPhone.value || d.customerBillToPhone || null,
      customerBillToContactPerson: billToContactPerson.value || d.customerContactPerson || null,
      customerShipTo: shipTo.value || d.customerShipTo || d.customerBillTo || null,
      customerShipToContactPerson: shipToContactPerson.value || d.customerContactPerson || null,
      customerShipToEmail: shipToEmail.value || d.customerShipToEmail || null,
      customerShipToPhone: shipToPhone.value || d.customerShipToPhone || null,
      customerShipToAccount: d.customerShipToAccount || null,
      items: items.map((it: any) => ({
        partNumber: it.partNumber || null,
        description: it.description || null,
        qty: it.qty || 0,
        condition: it.condition || null,
        certification: it.certification || null,
      })),
    }

    const response = await $fetch<Blob>(`${config.public.apiBase}/pdf/packing-list`, {
      method: 'POST',
      body: payload,
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    const url = window.URL.createObjectURL(response)
    const link = document.createElement('a')
    link.href = url
    const customerName = (d.customerName || '').replace(/\s+/g, '_')
    link.setAttribute('download', `PackingList-${d.invoiceNumber || 'Invoice'}-${customerName}.pdf`)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch (err) { console.error('Packing List PDF generation failed:', err) }
  finally { generatingPacking.value = false }
}
</script>

<style scoped>
.pdf-page {
  width: 210mm;
  min-height: 297mm;
  background: #fff;
  box-shadow: 0 4px 40px rgba(0,0,0,0.2);
  border-radius: 4px;
  overflow: hidden;
}
</style>
