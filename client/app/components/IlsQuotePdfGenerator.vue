<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">{{ docTitle }} PDF — {{ docNumber }}</v-toolbar-title>
        <v-spacer />
        <div class="d-flex align-center gap-2 mr-3" v-if="selectedPreset !== 'Custom'">
          <div :style="`width:14px;height:14px;border-radius:3px;background:${theme.primary};`" />
          <div :style="`width:14px;height:14px;border-radius:3px;background:${theme.accent};`" />
        </div>
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" @click="downloadPdf">Download PDF</v-btn>
      </v-toolbar>

      <div class="d-flex flex-grow-1 overflow-hidden">
        <!-- ── Left panel: Controls ── -->
        <div class="overflow-y-auto flex-shrink-0 pa-4" style="width:480px; border-right:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
          <div class="d-flex flex-column gap-3">
            <v-select v-model="selectedPreset" :items="companyPresetOptions" label="Company" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-domain" :loading="presetsLoading" />
            <v-file-input label="Company Logo" variant="outlined" density="compact" hide-details accept="image/*" prepend-icon="mdi-image" @update:model-value="onLogoUpload" />
            
            <div class="text-caption text-medium-emphasis mt-2 font-weight-bold text-uppercase">Company Information</div>
            <v-text-field v-model="companyName" label="Company Name" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
            <v-text-field v-model="companyLocation" label="Address" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
            <v-text-field v-model="companyPhone" label="Phone" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
            <v-text-field v-model="companyWebsite" label="Website" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
            <v-text-field v-model="companyEmail" label="Contact Email" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />

            <div class="text-caption text-medium-emphasis mt-2 font-weight-bold text-uppercase">Financials</div>
            <v-text-field v-model.number="taxAmount" label="Tax" variant="outlined" density="compact" hide-details type="number" :prefix="currencySymbol" />
            <v-text-field v-model.number="shippingAmount" label="Shipping" variant="outlined" density="compact" hide-details type="number" :prefix="currencySymbol" />
            <v-text-field v-model.number="otherAmount" label="Other" variant="outlined" density="compact" hide-details type="number" :prefix="currencySymbol" />
            <v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD', 'China Yuan (CNY)']" label="Currency" variant="outlined" density="compact" hide-details />
            <v-text-field v-if="currency === 'China Yuan (CNY)'" v-model.number="exchangeRate" label="Exchange Rate" variant="outlined" density="compact" hide-details type="number" step="0.0001" />

            <div class="text-caption text-medium-emphasis mt-2 font-weight-bold text-uppercase">Additional Notes</div>
            <v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="2" auto-grow />
            <v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details />
            <v-textarea v-model="companyTerms" label="Terms & Conditions" variant="outlined" density="compact" hide-details rows="3" auto-grow />
          </div>
        </div>

        <!-- ── Right panel: PDF preview ── -->
        <div class="flex-grow-1 overflow-y-auto d-flex justify-center pa-6" style="background: rgb(var(--v-theme-surface-variant));">
          <div ref="pdfContent" class="pdf-page" v-html="renderedHtml" />
        </div>
      </div>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{ quote: any; title?: string }>()
const model = defineModel<boolean>({ default: false })

const api = useApi()

const docTitle = computed(() => props.title || 'QUOTATION')
const docNumber = computed(() => props.quote?.quoteNumber ?? props.quote?.piNumber ?? '')

// ── Presets ──
const apiPresets = ref<any[]>([])
const presetsLoading = ref(false)

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

const selectedPreset = ref<string>('Custom')

const companyName = ref('')
const companyLocation = ref('')
const companyPhone = ref('')
const companyWebsite = ref('')
const companyEmail = ref('')
const companyTerms = ref('')

const theme = computed(() => {
  const preset = apiPresets.value.find((p: any) => p.name === selectedPreset.value)
  return {
    primary: preset?.primaryColor || '#1a2744',
    accent: preset?.accentColor || '#2563eb',
  }
})

// Default to the first preset once loaded
watch(apiPresets, (presets) => {
  if (presets.length && selectedPreset.value === 'Custom') selectedPreset.value = presets[0].name
})

watch(selectedPreset, (val) => {
  const preset = apiPresets.value.find((p: any) => p.name === val)
  if (preset) {
    companyName.value = preset.name
    companyLocation.value = preset.location || ''
    companyPhone.value = preset.phone || ''
    companyWebsite.value = preset.website || ''
    companyEmail.value = preset.email || ''
    // Prefer the customer's terms over the preset's
    companyTerms.value = props.quote?.customerTermsAndConditions || preset.termsAndConditions || ''
    logoDataUrl.value = preset.logoBase64
      ? `data:${preset.logoMimeType};base64,${preset.logoBase64}`
      : ''
  }
})

watch(model, (open) => {
  if (open) {
    loadPresets()
    footerText.value = `If you have any questions about this ${docTitle.value.toLowerCase()}, please contact`
  }
})

const footerText = ref('If you have any questions about this quotation, please contact')
const logoDataUrl = ref('')
const generating = ref(false)
const taxAmount = ref(0)
const shippingAmount = ref(0)
const otherAmount = ref(0)
const currency = ref('Dollar (USD)')
const exchangeRate = ref(7.0)
const comments = ref('')

const currencySymbol = computed(() => {
  if (currency.value === 'China Yuan (CNY)') return '¥'
  if (currency.value === 'Euro (EUR)') return '€'
  if (currency.value === 'GBP') return '£'
  return '$'
})

function onLogoUpload(files: File[] | File | null) {
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { logoDataUrl.value = ''; return }
  const reader = new FileReader()
  reader.onload = (e) => { logoDataUrl.value = (e.target?.result as string) || '' }
  reader.readAsDataURL(file)
}

const fmt = (n: number) => formatPrice(n)

function hexToRgb(hex: string) {
  const h = hex.replace('#', '')
  return { r: parseInt(h.substring(0, 2), 16), g: parseInt(h.substring(2, 4), 16), b: parseInt(h.substring(4, 6), 16) }
}
function lighten(hex: string, amount = 0.92) {
  const { r, g, b } = hexToRgb(hex)
  const lr = Math.round(r + (255 - r) * amount)
  const lg = Math.round(g + (255 - g) * amount)
  const lb = Math.round(b + (255 - b) * amount)
  return `rgb(${lr},${lg},${lb})`
}

// Map an ILS quote item to a uniform display row (customer-facing: no base/coef)
function displayItems() {
  return (props.quote?.items || []).map((it: any) => ({
    partNumberName: it.partNumberName,
    alt: it.altPartNumber,
    serialNumber: it.serialNumber,
    description: it.serialNumber ? `S/N: ${it.serialNumber}` : (it.notes || ''),
    qty: it.qty,
    condition: it.condition,
    certName: it.certName,
    leadTime: it.leadTime,
    unitPrice: Number(it.sellPrice) || 0,
    totalPrice: Number(it.totalPrice) || 0,
    note: it.serialNumber ? `S/N: ${it.serialNumber}` : (it.notes || ''),
  }))
}

const renderedHtml = computed(() => {
  const q = props.quote
  if (!q) return ''
  const items = displayItems()

  const logo = logoDataUrl.value
  const logoImg = logo ? `<img src="${logo}" style="max-height:150px; max-width:200px; object-fit:contain;" />` : ''
  const quoteDate = q.createdAt ? new Date(q.createdAt).toLocaleDateString() : '—'
  const sym = currencySymbol.value
  const rate = currency.value === 'Dollar (USD)' || currency.value === 'MYR' || currency.value === 'HKD' ? 1 : (exchangeRate.value || 1)

  const subtotal = (Number(q.totalAmount) || 0) * rate
  const tax = (taxAmount.value || 0) * rate
  const shipping = (shippingAmount.value || 0) * rate
  const other = (otherAmount.value || 0) * rate
  const grandTotal = subtotal + tax + shipping + other

  const primary = theme.value.primary
  const accent = theme.value.accent
  const rowEven = lighten(primary, 0.96)

  // Custom preset HTML template override
  const activePreset = apiPresets.value.find((p: any) => p.name === selectedPreset.value)
  if (activePreset?.customPdfHtml) {
    const itemsTable = `<table style="width:100%;border-collapse:collapse;font-size:12px;">
      <thead><tr style="background:${primary};color:#fff;">
        <th style="padding:7px 10px;text-align:left;color:#fff">#</th>
        <th style="padding:7px 10px;text-align:left;color:#fff">Part Number</th>
        <th style="padding:7px 10px;text-align:left;color:#fff">Serial</th>
        <th style="padding:7px 10px;text-align:center;color:#fff">Qty</th>
        <th style="padding:7px 10px;text-align:center;color:#fff">Cond</th>
        <th style="padding:7px 10px;text-align:right;color:#fff">Unit</th>
        <th style="padding:7px 10px;text-align:right;color:#fff">Total</th>
      </tr></thead><tbody>${items.map((it: any, i: number) => `<tr style="background:${i % 2 === 0 ? '#fff' : rowEven};">
        <td style="padding:6px 10px;">${i + 1}</td>
        <td style="padding:6px 10px;font-weight:600;color:${primary};">${it.alt || it.partNumberName || '—'}</td>
        <td style="padding:6px 10px;">${it.serialNumber || '—'}</td>
        <td style="padding:6px 10px;text-align:center;">${it.qty}</td>
        <td style="padding:6px 10px;text-align:center;">${it.condition || '—'}</td>
        <td style="padding:6px 10px;text-align:right;">${sym}${fmt(it.unitPrice * rate)}</td>
        <td style="padding:6px 10px;text-align:right;font-weight:700;">${sym}${fmt(it.totalPrice * rate)}</td>
      </tr>`).join('')}</tbody></table>`
    const vars: Record<string, string> = {
      '{{LOGO}}': logoImg,
      '{{COMPANY_NAME}}': companyName.value,
      '{{COMPANY_LOCATION}}': companyLocation.value,
      '{{COMPANY_PHONE}}': companyPhone.value,
      '{{COMPANY_EMAIL}}': companyEmail.value,
      '{{COMPANY_WEBSITE}}': companyWebsite.value,
      '{{QUOTE_NUMBER}}': docNumber.value || '—',
      '{{DATE}}': quoteDate,
      '{{VALID_UNTIL}}': '—',
      '{{RFQ_NAME}}': q.rfqReference || '—',
      '{{CUSTOMER_NAME}}': q.ilsCustomerName || '—',
      '{{CUSTOMER_BILL_TO}}': q.billTo || '—',
      '{{CUSTOMER_SHIP_TO}}': q.shipTo || q.billTo || '—',
      '{{ITEMS_TABLE}}': itemsTable,
      '{{SUBTOTAL}}': `${sym}${fmt(subtotal)}`,
      '{{SHIPPING}}': `${sym}${fmt(shipping)}`,
      '{{GRAND_TOTAL}}': `${sym}${fmt(grandTotal)}`,
      '{{COMMENTS}}': comments.value || '',
      '{{TERMS}}': companyTerms.value || '',
      '{{PRIMARY_COLOR}}': primary,
      '{{ACCENT_COLOR}}': accent,
    }
    let html = activePreset.customPdfHtml
    for (const [key, val] of Object.entries(vars)) html = html.split(key).join(val)
    return html
  }

  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : rowEven
    const isAlt = !!it.alt
    const displayPN = isAlt ? it.alt : (it.partNumberName || '—')
    const mainPNNote = isAlt ? `<div style="font-size:9px;color:#9ca3af;margin-top:1px;">(Req: ${it.partNumberName})</div>` : ''
    return `
    <tr style="background:${bg};">
      <td style="padding:9px 12px; font-size:11px; color:#6b7280; text-align:center; border-bottom:1px solid #eef0f3;">${i + 1}</td>
      <td style="padding:9px 12px; font-size:11px; font-weight:600; color:${isAlt ? accent : primary}; border-bottom:1px solid #eef0f3;">${displayPN}${mainPNNote}</td>
      <td style="padding:9px 12px; font-size:10.5px; color:#4b5563; border-bottom:1px solid #eef0f3;">${it.serialNumber || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:center; font-weight:600; color:${primary}; border-bottom:1px solid #eef0f3;">${it.qty}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:${primary}; border-bottom:1px solid #eef0f3;">${it.condition || '—'}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:#4b5563; border-bottom:1px solid #eef0f3;">${it.leadTime || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; color:${primary}; border-bottom:1px solid #eef0f3;">${sym}${fmt(it.unitPrice * rate)}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; font-weight:700; color:${primary}; border-bottom:1px solid #eef0f3;">${sym}${fmt(it.totalPrice * rate)}</td>
    </tr>`
  }).join('')

  return `
    <div style="font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif; color:${primary}; display:flex; flex-direction:column; min-height:297mm;">
      <div style="padding:28px 40px 20px 40px; display:flex; justify-content:space-between; align-items:flex-start;">
        <div style="display:flex; align-items:flex-start; gap:16px;">
          ${logoImg}
          <div>
            <div style="font-size:20px; font-weight:700; color:${primary};">${companyName.value}</div>
            <div style="font-size:9px; color:#6b7280; margin-top:4px; line-height:1.7;">
              ${companyLocation.value ? `<div>Add: ${companyLocation.value}</div>` : ''}
              ${companyPhone.value ? `<div>Tel: ${companyPhone.value}</div>` : ''}
              ${companyWebsite.value ? `<div>Website: ${companyWebsite.value}</div>` : ''}
              ${companyEmail.value ? `<div>Email: ${companyEmail.value}</div>` : ''}
            </div>
          </div>
        </div>
        <div style="text-align:right;">
          <div style="font-size:24px; font-weight:700; color:${primary}; letter-spacing:1px;">${docTitle.value}</div>
          <div style="font-size:11px; color:#6b7280; margin-top:4px;">${docNumber.value || '—'}</div>
        </div>
      </div>

      <div style="height:3px; background:linear-gradient(90deg,${primary} 0%,${accent} 40%,#e5e7eb 100%); margin:0 40px;"></div>

      <div style="padding:16px 40px; display:flex; gap:40px; font-size:11px; color:#4b5563;">
        <div><span style="font-weight:600; color:${primary};">Date:</span> ${quoteDate}</div>
        <div><span style="font-weight:600; color:${primary};">RFQ Ref:</span> ${q.rfqReference || '—'}</div>
        <div><span style="font-weight:600; color:${primary};">Currency:</span> ${currency.value}</div>
      </div>

      <div style="display:flex; gap:0; margin:0 40px 20px 40px; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Bill To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${q.ilsCustomerName || '—'}${q.ilsCustomerCode ? ` (${q.ilsCustomerCode})` : ''}</div>
          ${q.billTo ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${q.billTo}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Ship To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${q.ilsCustomerName || '—'}</div>
          ${q.shipTo ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${q.shipTo}</div>` : (q.billTo ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${q.billTo}</div>` : '')}
        </div>
      </div>

      <div style="margin:0 40px 16px 40px;">
        <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
          <thead>
            <tr style="background:${primary};">
              <th style="padding:10px 8px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:center; width:28px;">#</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:left;">Part No.</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:left;">Serial</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:center; width:36px;">Qty</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:center; width:36px;">CD</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:center;">Lead Time</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:right;">Unit Price</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; text-align:right;">Total</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>

      <div style="display:flex; justify-content:flex-end; margin:0 40px 16px 40px;">
        <div style="min-width:260px;">
          <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden; font-size:11px;">
            <tr style="background:${rowEven};"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Subtotal</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(subtotal)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Tax</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(tax)}</td></tr>
            <tr style="background:${rowEven};"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Shipping</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(shipping)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #e5e7eb;">Other</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #e5e7eb;">${sym}${fmt(other)}</td></tr>
            <tr style="background:${primary};"><td style="padding:10px 14px; color:#fff; font-weight:700;">Total</td><td style="padding:10px 14px; text-align:right; color:#fff; font-weight:800; font-size:14px;">${sym}${fmt(grandTotal)}</td></tr>
          </table>
        </div>
      </div>

      ${comments.value ? `<div style="margin:0 40px 16px 40px; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:5px;">Comments</div>
        <div style="font-size:11px; color:#4b5563; white-space:pre-wrap; line-height:1.5;">${comments.value}</div>
      </div>` : ''}

      ${companyTerms.value ? `<div style="margin:0 40px 16px 40px; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px; background:${rowEven};">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:6px;">Terms &amp; Conditions</div>
        <div style="font-size:10px; color:#4b5563; white-space:pre-wrap; line-height:1.6;">${companyTerms.value}</div>
      </div>` : ''}

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
    const authStore = useAuthStore()
    const q = props.quote
    const sym = currencySymbol.value
    const rate = currency.value === 'Dollar (USD)' || currency.value === 'MYR' || currency.value === 'HKD' ? 1 : (exchangeRate.value || 1)
    const items = displayItems()

    const payload = {
      docTitle: docTitle.value,
      companyName: companyName.value,
      companyLocation: companyLocation.value,
      companyPhone: companyPhone.value,
      companyWebsite: companyWebsite.value,
      companyEmail: companyEmail.value,
      logoBase64: logoDataUrl.value || null,
      primaryColor: theme.value.primary,
      accentColor: theme.value.accent,
      quoteNumber: docNumber.value || '',
      quoteDate: q.createdAt ? new Date(q.createdAt).toLocaleDateString() : '—',
      validUntil: '—',
      rfqName: q.rfqReference || '—',
      currency: currency.value,
      currencySymbol: sym,
      exchangeRate: rate,
      customerName: q.ilsCustomerName || '—',
      customerBillTo: q.billTo || null,
      customerShipTo: q.shipTo || q.billTo || null,
      items: items.map((it: any) => ({
        rfqReference: null,
        partNumberName: it.partNumberName || null,
        alt: it.alt || null,
        description: it.description || null,
        qty: Math.round(Number(it.qty) || 0),
        condition: it.condition || null,
        leadTime: it.leadTime || null,
        unitPrice: Number(it.unitPrice) || 0,
        totalPrice: Number(it.totalPrice) || 0,
        certName: it.certName || null,
        tagDate: null,
        note: it.note || null,
      })),
      subtotal: Number(q.totalAmount) || 0,
      tax: taxAmount.value || 0,
      shipping: shippingAmount.value || 0,
      other: otherAmount.value || 0,
      comments: comments.value || null,
      terms: companyTerms.value || null,
      footerText: footerText.value || null,
    }

    const response = await $fetch<Blob>(`${api.baseURL}/pdf/generate`, {
      method: 'POST',
      body: payload,
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    const url = window.URL.createObjectURL(response)
    const link = document.createElement('a')
    link.href = url
    const safeName = (s: string) => (s || '').replace(/[/\\:*?"<>|]/g, '-').trim()
    link.setAttribute('download', `${safeName(docNumber.value || 'ILS')} - ${safeName(q.ilsCustomerName || '')}.pdf`)
    document.body.appendChild(link)
    link.click()
    link.parentNode?.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch (err) {
    console.error('PDF generation failed:', err)
  } finally {
    generating.value = false
  }
}
</script>

<style scoped>
.pdf-page {
  width: 210mm;
  min-height: 297mm;
  padding: 20px;
  background: #fff;
  box-shadow: 0 4px 40px rgba(0,0,0,0.2);
  border-radius: 4px;
  overflow: hidden;
}
</style>
