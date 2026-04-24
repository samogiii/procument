<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Proforma Invoice PDF PINV — {{ invoice.invoiceNumber || `INV-${invoice.id}` }}</v-toolbar-title>
        <v-spacer />
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" @click="downloadPdf">Download PDF</v-btn>
      </v-toolbar>

      <!-- Controls -->
      <v-container fluid class="flex-shrink-0 py-4">
        <v-row dense align="center">
          <v-col cols="12" md="3"><v-select v-model="selectedPreset" :items="companyPresetOptions" label="Company" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-domain" :loading="presetsLoading" /></v-col>
          <v-col cols="12" md="3"><v-file-input label="Company Logo" variant="outlined" density="compact" hide-details accept="image/*" prepend-icon="mdi-image" @update:model-value="onLogoUpload" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyName" label="Company Name" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="overrideCustomerName" label="Override Customer Name" variant="outlined" density="compact" hide-details placeholder="Leave blank to use original" /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="3"><v-text-field v-model="companyPhone" label="Phone" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyWebsite" label="Website" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyEmail" label="Contact Email" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="2"><v-text-field v-model.number="taxAmount" label="Tax" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model.number="shippingAmount" label="Shipping" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model.number="otherAmount" label="Other" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" /></v-col>
          <v-col cols="12" md="2"><v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD', 'China Yuan (CNY)']" label="Currency" variant="outlined" density="compact" hide-details :disabled="currencyLocked" /></v-col>
          <v-col v-if="currency === 'China Yuan (CNY)'" cols="12" md="2"><v-text-field v-model.number="exchangeRate" label="Exchange Rate" variant="outlined" density="compact" hide-details type="number" step="0.0001" /></v-col>
          <v-col cols="12" :md="currency === 'China Yuan (CNY)' ? 2 : 4"><v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="1" auto-grow /></v-col>
        </v-row>

        <!-- PDF Row Selection -->
        <div class="mt-4 border rounded pa-3 bg-surface">
          <div class="d-flex align-center justify-space-between mb-2">
            <span class="text-caption font-weight-bold uppercase text-medium-emphasis">Select items to include in PDF</span>
            <div class="d-flex gap-2">
              <v-btn size="x-small" variant="tonal" @click="selectAllItems">Select All</v-btn>
              <v-btn size="x-small" variant="tonal" @click="selectedItems.clear()">Clear</v-btn>
            </div>
          </div>
          <div class="d-flex flex-wrap gap-x-6 gap-y-1">
            <v-checkbox
              v-for="it in invoice.items"
              :key="it.id"
              v-model="selectedItems"
              :value="it.id"
              :label="`${it.partNumberName} (${it.qty})`"
              density="compact"
              hide-details
              color="primary"
              class="flex-shrink-0"
            />
          </div>
        </div>

        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="2"><v-text-field v-model="contactPerson" label="Contact Person" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="billToEmail" label="Bill To Email" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="billToPhone" label="Bill To Phone" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="shipToContactPerson" label="Ship To Contact" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="shipToEmail" label="Ship To Email" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="shipToPhone" label="Ship To Phone" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="4"><v-textarea v-model="billTo" label="Bill To (address)" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="4"><v-textarea v-model="shipTo" label="Ship To (address)" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="4"><v-textarea v-model="shipToAccount" label="Ship To Account" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="3"><v-text-field v-model="beneficiaryName" label="Beneficiary Name" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="2"><v-text-field v-model="bankName" label="Bank Name" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="bankAddress" label="Bank Address" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="bankAccount" label="Account Number" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model="swiftCode" label="SWIFT Code" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="4"><v-textarea v-model="companyTerms" label="Terms & Conditions" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
        </v-row>
      </v-container>

      <v-divider />

      <div class="flex-grow-1 overflow-y-auto d-flex justify-center pa-6" style="background: rgb(var(--v-theme-surface-variant));">
        <div ref="pdfContent" class="pdf-page" v-html="renderedHtml" />
      </div>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{ invoice: any }>()
const emit = defineEmits<{ (e: 'pdf-uploaded'): void }>()
const model = defineModel<boolean>({ default: false })

const api = useApi()

// ── Presets ──
const apiPresets = ref<any[]>([])
const presetsLoading = ref(false)
const selectedPreset = ref<string>('Custom')

async function loadPresets() {
  presetsLoading.value = true
  try {
    apiPresets.value = await api.get<any[]>('/companypresets')
  } catch {
    apiPresets.value = []
  } finally {
    presetsLoading.value = false
  }
}

const companyPresetOptions = computed(() => [
  ...apiPresets.value.map((p: any) => p.name),
  'Custom',
])

const theme = computed(() => {
  const preset = apiPresets.value.find((p: any) => p.name === selectedPreset.value)
  return {
    primary: preset?.primaryColor || '#1a2744',
    accent: preset?.accentColor || '#2563eb',
  }
})

watch(apiPresets, (presets) => {
  if (!presets.length) return
  const base = props.invoice?.customerBase
  if (base != null) {
    const match = presets.find((p: any) => p.sortOrder === base)
    if (match) { selectedPreset.value = match.name; return }
  }
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
    companyTerms.value = props.invoice?.customerTermsAndConditions || preset.termsAndConditions || ''
  }
})

watch(model, (open) => {
  if (open) {
    loadPresets()
    selectAllItems()
    overrideCustomerName.value = ''
    // Pre-fill address fields from invoice data
    contactPerson.value = props.invoice?.customerContactPerson || ''
    billTo.value = props.invoice?.customerBillTo || ''
    billToEmail.value = props.invoice?.customerEmail || ''
    billToPhone.value = props.invoice?.customerPhone || ''
    shipTo.value = props.invoice?.customerShipTo || props.invoice?.customerBillTo || ''
    shipToContactPerson.value = props.invoice?.customerContactPerson || ''
    shipToEmail.value = props.invoice?.customerEmail || ''
    shipToPhone.value = props.invoice?.customerPhone || ''
    shipToAccount.value = props.invoice?.customerShippingAccount || ''
    if (props.invoice?.customerBase == 3) {
      const currencyType = props.invoice?.customerCurrencyType || 'Dollar'
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
  }
})

const companyName = ref('')
const companyLocation = ref('')
const companyPhone = ref('')
const companyWebsite = ref('')
const companyEmail = ref('')
const footerText = ref('If you have any questions about this proforma invoice, please contact')
const logoDataUrl = ref('')
const generating = ref(false)
const taxAmount = ref(0)
const shippingAmount = ref(0)
const otherAmount = ref(0)
const currency = ref('Dollar (USD)')
const exchangeRate = ref(7.0)
const comments = ref('No Comments')
const contactPerson = ref('')
const billTo = ref('')
const billToEmail = ref('')
const billToPhone = ref('')
const shipTo = ref('')
const shipToContactPerson = ref('')
const shipToEmail = ref('')
const shipToPhone = ref('')
const shipToAccount = ref('')
const companyTerms = ref('')
const beneficiaryName = ref('')
const beneficiaryAddress = ref('')
const bankName = ref('')
const bankAddress = ref('')
const bankAccount = ref('')
const swiftCode = ref('')

const overrideCustomerName = ref('')
const selectedItems = ref<number[]>([])

function selectAllItems() {
  selectedItems.value = props.invoice?.items?.map((i: any) => i.id) || []
}

function hexToRgb(hex: string) {
  const h = hex.replace('#', '')
  const r = parseInt(h.substring(0, 2), 16)
  const g = parseInt(h.substring(2, 4), 16)
  const b = parseInt(h.substring(4, 6), 16)
  return { r, g, b }
}
function lighten(hex: string, amount = 0.96) {
  const { r, g, b } = hexToRgb(hex)
  return `rgb(${Math.round(r + (255 - r) * amount)},${Math.round(g + (255 - g) * amount)},${Math.round(b + (255 - b) * amount)})`
}

function onLogoUpload(files: File[] | File | null) {
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { logoDataUrl.value = ''; return }
  const reader = new FileReader()
  reader.onload = (e) => { logoDataUrl.value = (e.target?.result as string) || '' }
  reader.readAsDataURL(file)
}

const fmt = (n: number) => formatPrice(n)

// ── Currency lock based on customer settings ──
const currencyLocked = computed(() => {
  if (props.invoice?.customerBase !== 3) return false
  const currencyType = props.invoice?.customerCurrencyType || 'Dollar'
  return currencyType !== 'Both'
})

const renderedHtml = computed(() => {
  const inv = props.invoice
  if (!inv.id) return ''

  const allItems: any[] = inv.items || []
  const items = allItems.filter(it => selectedItems.value.includes(it.id))

  const logo = logoDataUrl.value
  const logoImg = logo ? `<img src="${logo}" style="max-height:48px; max-width:160px; object-fit:contain;" />` : ''
  const invDate = inv.createdAt ? new Date(inv.createdAt).toLocaleDateString() : '—'
  const dueDate = inv.dueDate ? new Date(inv.dueDate).toLocaleDateString() : '—'
  const isYuan = currency.value === 'China Yuan (CNY)'
  const sym = isYuan ? '¥' : '$'
  const rate = isYuan ? (exchangeRate.value || 1) : 1
  const subtotal = items.reduce((sum, it) => sum + (Number(it.totalPrice) || 0), 0) * rate
  const tax = (taxAmount.value || 0) * rate
  const shipping = (shippingAmount.value || 0) * rate
  const other = (otherAmount.value || 0) * rate
  const grandTotal = subtotal + tax + shipping + other

  const primary = theme.value.primary
  const accent = theme.value.accent
  const rowEven = lighten(primary, 0.96)

  const totalDiscount = items.reduce((sum: number, it: any) => sum + (it.discount > 0 ? Number(it.discount) : 0), 0) * rate

  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : rowEven
    const discountCell = it.discount > 0
      ? `<td style="padding:9px 8px; font-size:11px; text-align:right; font-weight:600; color:#e53935; border-bottom:1px solid #eef0f3;">-${sym}${fmt(Number(it.discount) * rate)}</td>`
      : `<td style="padding:9px 8px; font-size:10.5px; text-align:center; color:#9ca3af; border-bottom:1px solid #eef0f3;">—</td>`
    return `
    <tr style="background:${bg};">
      <td style="padding:9px 8px; font-size:10px; color:#9ca3af; text-align:center; border-bottom:1px solid #eef0f3;">${it.rfqReference || '—'}</td>
      <td style="padding:9px 8px; font-size:11px; color:#6b7280; text-align:center; border-bottom:1px solid #eef0f3;">${i + 1}</td>
      <td style="padding:9px 10px; font-size:11px; font-weight:600; color:${primary}; border-bottom:1px solid #eef0f3;">${it.partNumberName || '—'}</td>
      <td style="padding:9px 10px; font-size:10.5px; color:#4b5563; border-bottom:1px solid #eef0f3;">${it.description || '—'}</td>
      <td style="padding:9px 8px; font-size:11px; text-align:center; font-weight:600; color:${primary}; border-bottom:1px solid #eef0f3;">${it.qty}</td>
      <td style="padding:9px 8px; font-size:10.5px; text-align:center; color:${primary}; border-bottom:1px solid #eef0f3;">${it.condition || '—'}</td>
      <td style="padding:9px 8px; font-size:10.5px; text-align:center; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.certName || '—'}</td>
      <td style="padding:9px 10px; font-size:11px; text-align:right; color:${primary}; border-bottom:1px solid #eef0f3;">${sym}${fmt(Number(it.unitPrice) * rate)}</td>
      <td style="padding:9px 10px; font-size:11px; text-align:right; font-weight:700; color:${primary}; border-bottom:1px solid #eef0f3;">${sym}${fmt(Number(it.totalPrice) * rate)}</td>
      ${discountCell}
      <td style="padding:9px 10px; font-size:10.5px; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.leadTime || '—'}</td>
    </tr>`
  }).join('')

  const hasBankDetails = beneficiaryName.value || bankName.value || bankAccount.value || swiftCode.value
  const bankSection = hasBankDetails ? `
    <div style="border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px; max-width:320px;">
      <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:6px;">Bank Information</div>
      ${beneficiaryName.value ? `<div style="font-size:10.5px; color:${primary};"><span style="font-weight:600;">Beneficiary Name:</span> ${beneficiaryName.value}</div>` : ''}
      ${beneficiaryAddress.value ? `<div style="font-size:10.5px; color:${primary}; margin-top:2px;"><span style="font-weight:600;">Beneficiary Address:</span> ${beneficiaryAddress.value}</div>` : ''}
      ${bankName.value ? `<div style="font-size:10.5px; color:${primary}; margin-top:2px;"><span style="font-weight:600;">Bank Name:</span> ${bankName.value}</div>` : ''}
      ${bankAddress.value ? `<div style="font-size:10.5px; color:${primary}; margin-top:2px;"><span style="font-weight:600;">Bank Address:</span> ${bankAddress.value}</div>` : ''}
      ${bankAccount.value ? `<div style="font-size:10.5px; color:${primary}; margin-top:2px;"><span style="font-weight:600;">Account Number:</span> ${bankAccount.value}</div>` : ''}
      ${swiftCode.value ? `<div style="font-size:10.5px; color:${primary}; margin-top:2px;"><span style="font-weight:600;">SWIFT Code:</span> ${swiftCode.value}</div>` : ''}
    </div>
  ` : ''

  return `
    <div style="font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif; color:${primary}; display:flex; flex-direction:column; min-height:297mm;">

      <!-- Header -->
      <div style="padding:28px 40px 20px 40px; display:flex; justify-content:space-between; align-items:flex-start;">
        <div style="display:flex; align-items:center; gap:16px;">
          ${logoImg}
          <div>
            <div style="font-size:20px; font-weight:700; color:${primary}; letter-spacing:0.3px;">${companyName.value}</div>
            <div style="font-size:9px; color:#6b7280; margin-top:4px; line-height:1.7;">
              ${companyLocation.value ? `<div>Add: ${companyLocation.value}</div>` : ''}
              ${companyPhone.value ? `<div>Tel: ${companyPhone.value}</div>` : ''}
              ${companyWebsite.value ? `<div>Website: ${companyWebsite.value}</div>` : ''}
              ${companyEmail.value ? `<div>Email: ${companyEmail.value}</div>` : ''}
            </div>
          </div>
        </div>
        <div style="text-align:right;">
          <div style="font-size:24px; font-weight:700; color:${primary}; letter-spacing:1px;">PROFORMA INVOICE</div>
          <div style="font-size:11px; color:#6b7280; margin-top:4px;">INV-${inv.id}</div>
        </div>
      </div>

      <!-- Accent line -->
      <div style="height:3px; background:linear-gradient(90deg,${primary} 0%,${accent} 40%,#e5e7eb 100%); margin:0 40px;"></div>

      <!-- Meta Row -->
      <div style="padding:16px 40px; display:flex; gap:40px; font-size:11px; color:#4b5563;">
        <div><span style="font-weight:600; color:${primary};">Date:</span> ${invDate}</div>
        <div><span style="font-weight:600; color:${primary};">Due Date:</span> ${dueDate}</div>
        <div><span style="font-weight:600; color:${primary};">Customer PO:</span> ${inv.customerPONumber || '—'}</div>
        <div><span style="font-weight:600; color:${primary};">Status:</span> ${inv.status || '—'}</div>
        <div><span style="font-weight:600; color:${primary};">Currency:</span> ${currency.value}</div>
      </div>

      <!-- Bill To / Ship To -->
      <div style="display:flex; gap:0; margin:0 40px 20px 40px; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Bill To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${inv.customerName || '—'}</div>
          ${(billTo.value || inv.customerBillTo) ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px; line-height:1.5; white-space:pre-wrap;">${billTo.value || inv.customerBillTo}</div>` : ''}
          ${contactPerson.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Contact: ${contactPerson.value}</div>` : ''}
          ${billToEmail.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Email: ${billToEmail.value}</div>` : ''}
          ${billToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Phone: ${billToPhone.value}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Ship To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${inv.customerName || '—'}</div>
          ${(shipTo.value || inv.customerShipTo || inv.customerBillTo) ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px; line-height:1.5; white-space:pre-wrap;">${shipTo.value || inv.customerShipTo || inv.customerBillTo}</div>` : ''}
          ${shipToContactPerson.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Contact: ${shipToContactPerson.value}</div>` : ''}
          ${shipToEmail.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Email: ${shipToEmail.value}</div>` : ''}
          ${shipToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Phone: ${shipToPhone.value}</div>` : ''}
          ${shipToAccount.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Account: ${shipToAccount.value}</div>` : ''}
        </div>
      </div>

      <!-- Items Table -->
      <div style="margin:0 40px 16px 40px;">
        <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
          <thead>
            <tr style="background:${primary};">
              <th style="padding:10px 8px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:32px;">Ref</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">#</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Part No.</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Description</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">Qty</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">CD</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center;">Cert</th>
              <th style="padding:10px 10px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Unit Price</th>
              <th style="padding:10px 10px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Total</th>
              <th style="padding:10px 8px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Discount</th>
              <th style="padding:10px 10px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Delivery</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>

      <!-- Totals + Bank -->
      <div style="display:flex; justify-content:space-between; align-items:flex-start; margin:0 40px 16px 40px;">
        <div>${bankSection}</div>
        <div style="min-width:260px;">
          <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden; font-size:11px;">
            <tr style="background:${rowEven};"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Subtotal</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(subtotal)}</td></tr>
            ${totalDiscount > 0 ? `<tr><td style="padding:8px 14px; color:#e53935; border-bottom:1px solid #eef0f3; font-weight:600;">Discount</td><td style="padding:8px 14px; text-align:right; font-weight:600; color:#e53935; border-bottom:1px solid #eef0f3;">-${sym}${fmt(totalDiscount)}</td></tr>` : ''}
            <tr style="background:${rowEven};"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Tax</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(tax)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Shipping</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(shipping)}</td></tr>
            <tr style="background:${rowEven};"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #e5e7eb;">Other</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #e5e7eb;">${sym}${fmt(other)}</td></tr>
            <tr style="background:${primary};"><td style="padding:10px 14px; color:#fff; font-weight:700;">Total</td><td style="padding:10px 14px; text-align:right; color:#fff; font-weight:800; font-size:14px;">${sym}${fmt(subtotal - totalDiscount + tax + shipping + other)}</td></tr>
          </table>
        </div>
      </div>

      <!-- Comments -->
      <div style="margin:0 40px 16px 40px; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:5px;">Comments</div>
        <div style="font-size:11px; color:#4b5563; white-space:pre-wrap; line-height:1.5;">${comments.value || 'No Comments'}</div>
      </div>

      <!-- Terms & Conditions -->
      ${companyTerms.value ? `
      <div style="margin:0 40px 16px 40px; background:#f8fafc; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:5px;">Terms &amp; Conditions</div>
        <div style="font-size:10px; color:#4b5563; white-space:pre-wrap; line-height:1.6;">${companyTerms.value}</div>
      </div>` : ''}

      <!-- Footer -->
      <div style="margin-top:auto; padding:16px 40px; border-top:2px solid ${primary}; display:flex; justify-content:space-between; align-items:center;">
        <span style="font-size:10px; color:#6b7280;">${footerText.value}</span>
        <span style="font-size:10px; font-weight:600; color:${primary};">${companyEmail.value}</span>
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
    const inv = props.invoice
    const allItems: any[] = inv.items || []
    const items = allItems.filter(it => selectedItems.value.includes(it.id))

    // Check if customerCurrencyType is Both - generate two PDFs
    const currencyType = props.invoice?.customerCurrencyType || 'Dollar'
    const isBoth = props.invoice?.customerBase === 3 && currencyType === 'Both'

    const currenciesToGenerate = isBoth
      ? [
          { currency: 'Dollar (USD)', symbol: '$', rate: 1.0, nameSuffix: ' - Dollar' }, 
          { currency: 'China Yuan (CNY)', symbol: '¥', rate: 7, nameSuffix: ' - Yuan' }
        ]
      : [
          { 
            currency: currency.value, 
            symbol: currency.value === 'China Yuan (CNY)' ? '¥' : '$', 
            rate: currency.value === 'China Yuan (CNY)' ? (exchangeRate.value || 1) : 1,
            nameSuffix: '' 
          }
        ]

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
        invoiceNumber: inv.invoiceNumber || `INV-${inv.id}`,
        invoiceTitle: "PROFORMA INVOICE PINV",
        invoiceDate: inv.createdAt ? new Date(inv.createdAt).toLocaleDateString() : '—',
        dueDate: inv.dueDate ? new Date(inv.dueDate).toLocaleDateString() : '—',
        status: inv.status || '—',
        customerPONumber: inv.customerPONumber || null,
        currency: curr.currency,
        currencySymbol: curr.symbol,
        exchangeRate: curr.rate,
        customerName: overrideCustomerName.value || inv.customerName || '—',
        customerContactPerson: contactPerson.value || null,
        customerBillTo: billTo.value || inv.customerBillTo || null,
        customerBillToEmail: billToEmail.value || null,
        customerBillToPhone: billToPhone.value || null,
        customerShipTo: shipTo.value || inv.customerShipTo || inv.customerBillTo || null,
        customerShipToContactPerson: shipToContactPerson.value || null,
        customerShipToEmail: shipToEmail.value || null,
        customerShipToPhone: shipToPhone.value || null,
        customerShipToAccount: shipToAccount.value || null,
        beneficiaryName: beneficiaryName.value || null,
        beneficiaryAddress: beneficiaryAddress.value || null,
        bankName: bankName.value || null,
        bankAddress: bankAddress.value || null,
        bankAccount: bankAccount.value || null,
        swiftCode: swiftCode.value || null,
        items: items.map((it: any) => ({
          rfqReference: it.rfqReference || null,
          partNumberName: it.partNumberName || null,
          description: it.description || null,
          qty: it.qty || 0,
          condition: it.condition || null,
          certName: it.certName || null,
          unitPrice: (Number(it.unitPrice) || 0) * curr.rate,
          totalPrice: (Number(it.totalPrice) || 0) * curr.rate,
          discount: it.discount > 0 ? Number(it.discount) * curr.rate : null,
          deliveryDate: it.expectedDeliveryDate
            ? new Date(it.expectedDeliveryDate).toLocaleDateString()
            : null,
          leadTime: it.leadTime || null,
        })),
        subtotal: (items.reduce((sum, it) => sum + (Number(it.totalPrice) || 0), 0) + items.reduce((sum: number, it: any) => sum + (it.discount > 0 ? Number(it.discount) : 0), 0)) * curr.rate,
        tax: (taxAmount.value || 0) * curr.rate,
        shipping: (shippingAmount.value || 0) * curr.rate,
        other: (otherAmount.value || 0) * curr.rate,
        comments: comments.value || null,
        terms: companyTerms.value || null,
        footerText: footerText.value || null,
      }

      const response = await $fetch<Blob>(`${config.public.apiBase}/pdf/invoice`, {
        method: 'POST',
        body: payload,
        responseType: 'blob',
        headers: { Authorization: `Bearer ${authStore.user?.token}` },
      })
      const url = window.URL.createObjectURL(response)
      const link = document.createElement('a')
      link.href = url
      const customerNameForFile = overrideCustomerName.value || inv.customerName || 'Customer'
      const sanitizedCustomerName = customerNameForFile.replace(/[^a-zA-Z0-9]/g, '_')
      const fileName = `PI-${inv.id}-${sanitizedCustomerName}${curr.nameSuffix}.pdf`
      link.setAttribute('download', fileName)
      document.body.appendChild(link)
      link.click()
      link.parentNode?.removeChild(link)
      window.URL.revokeObjectURL(url)

      // Auto-upload to the PI document folder (our_pi category)
      try {
        const form = new FormData()
        form.append('file', new File([response], fileName, { type: 'application/pdf' }))
        const category = isBoth
          ? (curr.currency.includes('CNY') ? 'our_pi_yuan' : 'our_pi_dollar')
          : 'our_pi'
        form.append('category', category)
        await $fetch(`${config.public.apiBase}/documents/proforma-invoice/${inv.id}/upload`, {
          method: 'POST',
          body: form,
          headers: { Authorization: `Bearer ${authStore.user?.token}` },
        })
      } catch (uploadErr) { console.warn('PDF auto-upload failed:', uploadErr) }
    }
    emit('pdf-uploaded')
  } catch (err) { console.error('PDF generation failed:', err) }
  finally { generating.value = false }
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
