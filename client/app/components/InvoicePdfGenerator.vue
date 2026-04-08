<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Proforma Invoice PDF — INV-{{ invoice.id }}</v-toolbar-title>
        <v-spacer />
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" @click="downloadPdf">Download PDF</v-btn>
      </v-toolbar>

      <!-- Controls -->
      <v-container fluid class="flex-shrink-0 py-4">
        <v-row dense align="center">
          <v-col cols="12" md="3"><v-select v-model="selectedPreset" :items="companyPresetOptions" label="Company" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-domain" :loading="presetsLoading" /></v-col>
          <v-col cols="12" md="3"><v-file-input label="Company Logo" variant="outlined" density="compact" hide-details accept="image/*" prepend-icon="mdi-image" @update:model-value="onLogoUpload" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyName" label="Company Name" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyPhone" label="Phone" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="3"><v-text-field v-model="companyWebsite" label="Website" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="companyEmail" label="Contact Email" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model.number="taxAmount" label="Tax" variant="outlined" density="compact" hide-details type="number" prefix="$" /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model.number="shippingAmount" label="Shipping" variant="outlined" density="compact" hide-details type="number" prefix="$" /></v-col>
          <v-col cols="12" md="2"><v-text-field v-model.number="otherAmount" label="Other" variant="outlined" density="compact" hide-details type="number" prefix="$" /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="2"><v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD']" label="Currency" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="4"><v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="1" auto-grow /></v-col>
          <v-col cols="12" md="6"><v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="3"><v-textarea v-model="billTo" label="Bill To (address)" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="3"><v-textarea v-model="shipTo" label="Ship To (address)" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="beneficiaryName" label="Beneficiary Name" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="beneficiaryAddress" label="Beneficiary Address" variant="outlined" density="compact" hide-details /></v-col>
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
    companyTerms.value = preset.termsAndConditions || ''
  }
})

watch(model, (open) => {
  if (open) {
    loadPresets()
    // Pre-fill address fields from invoice data
    billTo.value = props.invoice?.customerBillTo || ''
    shipTo.value = props.invoice?.customerShipTo || props.invoice?.customerBillTo || ''
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
const comments = ref('No Comments')
const billTo = ref('')
const shipTo = ref('')
const companyTerms = ref('')
const beneficiaryName = ref('')
const beneficiaryAddress = ref('')
const bankName = ref('')
const bankAddress = ref('')
const bankAccount = ref('')
const swiftCode = ref('')

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

const renderedHtml = computed(() => {
  const inv = props.invoice
  if (!inv.id) return ''

  const items: any[] = inv.items || []
  const logo = logoDataUrl.value
  const logoImg = logo ? `<img src="${logo}" style="max-height:48px; max-width:160px; object-fit:contain;" />` : ''
  const invDate = inv.createdAt ? new Date(inv.createdAt).toLocaleDateString() : '—'
  const dueDate = inv.dueDate ? new Date(inv.dueDate).toLocaleDateString() : '—'
  const subtotal = Number(inv.totalAmount) || 0
  const tax = taxAmount.value || 0
  const shipping = shippingAmount.value || 0
  const other = otherAmount.value || 0
  const grandTotal = subtotal + tax + shipping + other

  const primary = theme.value.primary
  const accent = theme.value.accent
  const rowEven = lighten(primary, 0.96)

  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : rowEven
    return `
    <tr style="background:${bg};">\n      <td style="padding:9px 8px; font-size:10px; color:#9ca3af; text-align:center; border-bottom:1px solid #eef0f3;">${it.rfqReference || '—'}</td>\n      <td style="padding:9px 12px; font-size:11px; color:#6b7280; text-align:center; border-bottom:1px solid #eef0f3;">${i + 1}</td>\n      <td style="padding:9px 12px; font-size:11px; font-weight:600; color:${primary}; border-bottom:1px solid #eef0f3;">${it.partNumberName || '—'}</td>\n      <td style="padding:9px 12px; font-size:10.5px; color:#4b5563; border-bottom:1px solid #eef0f3; max-width:120px;">${it.description || '—'}</td>\n      <td style="padding:9px 12px; font-size:11px; text-align:center; font-weight:600; color:${primary}; border-bottom:1px solid #eef0f3;">${it.qty}</td>\n      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:${primary}; border-bottom:1px solid #eef0f3;">${it.condition || '—'}</td>\n      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.certName || '—'}</td>\n      <td style="padding:9px 12px; font-size:11px; text-align:right; color:${primary}; border-bottom:1px solid #eef0f3;">$${fmt(Number(it.unitPrice))}</td>\n      <td style="padding:9px 12px; font-size:11px; text-align:right; font-weight:700; color:${primary}; border-bottom:1px solid #eef0f3;">$${fmt(Number(it.totalPrice))}</td>\n      <td style="padding:9px 12px; font-size:10.5px; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.expectedDeliveryDate ? new Date(it.expectedDeliveryDate).toLocaleDateString() : ''}</td>\n    </tr>`
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
        <div><span style="font-weight:600; color:${primary};">Status:</span> ${inv.status || '—'}</div>
        <div><span style="font-weight:600; color:${primary};">Currency:</span> ${currency.value}</div>
      </div>

      <!-- Bill To / Ship To -->
      <div style="display:flex; gap:0; margin:0 40px 20px 40px; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Bill To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${inv.customerName || '—'}</div>
          ${(billTo.value || inv.customerBillTo) ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${billTo.value || inv.customerBillTo}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Ship To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${inv.customerName || '—'}</div>
          ${(shipTo.value || inv.customerShipTo || inv.customerBillTo) ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${shipTo.value || inv.customerShipTo || inv.customerBillTo}</div>` : ''}
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
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Unit Price</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Total</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Delivery</th>
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
            <tr style="background:${rowEven};"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Subtotal</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(subtotal)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Tax</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(tax)}</td></tr>
            <tr style="background:${rowEven};"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Shipping</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(shipping)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #e5e7eb;">Other</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #e5e7eb;">$${fmt(other)}</td></tr>
            <tr style="background:${primary};"><td style="padding:10px 14px; color:#fff; font-weight:700;">Total</td><td style="padding:10px 14px; text-align:right; color:#fff; font-weight:800; font-size:14px;">$${fmt(grandTotal)}</td></tr>
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
    const items: any[] = inv.items || []

    const payload = {
      companyName: companyName.value,
      companyLocation: companyLocation.value,
      companyPhone: companyPhone.value,
      companyWebsite: companyWebsite.value,
      companyEmail: companyEmail.value,
      logoBase64: logoDataUrl.value || null,
      primaryColor: theme.value.primary,
      accentColor: theme.value.accent,
      invoiceNumber: `INV-${inv.id}`,
      invoiceDate: inv.createdAt ? new Date(inv.createdAt).toLocaleDateString() : '—',
      dueDate: inv.dueDate ? new Date(inv.dueDate).toLocaleDateString() : '—',
      status: inv.status || '—',
      currency: currency.value,
      currencySymbol: '$',
      customerName: inv.customerName || '—',
      customerBillTo: billTo.value || inv.customerBillTo || null,
      customerShipTo: shipTo.value || inv.customerShipTo || inv.customerBillTo || null,
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
        unitPrice: Number(it.unitPrice) || 0,
        totalPrice: Number(it.totalPrice) || 0,
        deliveryDate: it.expectedDeliveryDate
          ? new Date(it.expectedDeliveryDate).toLocaleDateString()
          : null,
      })),
      subtotal: Number(inv.totalAmount) || 0,
      tax: taxAmount.value || 0,
      shipping: shippingAmount.value || 0,
      other: otherAmount.value || 0,
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
    link.setAttribute('download', `INV-${inv.id}.pdf`)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)
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
