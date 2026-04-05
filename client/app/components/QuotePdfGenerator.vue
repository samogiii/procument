<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Quotation PDF — {{ quote.quoteNumber }}</v-toolbar-title>
        <v-spacer />
        <!-- Theme preview dots -->
        <div class="d-flex align-center gap-2 mr-3" v-if="selectedPreset !== 'Custom'">
          <div :style="`width:14px;height:14px;border-radius:3px;background:${theme.primary};`" />
          <div :style="`width:14px;height:14px;border-radius:3px;background:${theme.accent};`" />
        </div>
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" @click="downloadPdf">Download PDF</v-btn>
      </v-toolbar>

      <!-- Controls -->
      <v-container fluid class="flex-shrink-0 py-4">
        <v-row dense align="center">
          <v-col cols="12" md="3">
            <v-select v-model="selectedPreset" :items="companyPresetOptions" label="Company" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-domain" :loading="presetsLoading" />
          </v-col>
          <v-col cols="12" md="3">
            <v-file-input label="Company Logo" variant="outlined" density="compact" hide-details accept="image/*" prepend-icon="mdi-image" @update:model-value="onLogoUpload" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="companyName" label="Company Name" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="companyPhone" label="Phone" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
          </v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="4">
            <v-text-field v-model="companyLocation" label="Address" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="companyWebsite" label="Website" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="companyEmail" label="Contact Email" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" />
          </v-col>
          <v-col cols="12" md="2">
            <v-text-field v-model.number="taxAmount" label="Tax" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" />
          </v-col>
          <v-col cols="12" md="2">
            <v-text-field v-model.number="shippingAmount" label="Shipping" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" />
          </v-col>
          <v-col cols="12" md="2">
            <v-text-field v-model.number="otherAmount" label="Other" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" />
          </v-col>
        </v-row>
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="2">
            <v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD', 'China Yuan (CNY)']" label="Currency" variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col v-if="currency === 'China Yuan (CNY)'" cols="12" md="2">
            <v-text-field v-model.number="exchangeRate" label="Exchange Rate" variant="outlined" density="compact" hide-details type="number" step="0.0001" />
          </v-col>
          <v-col cols="12" :md="currency === 'China Yuan (CNY)' ? 2 : 4">
            <v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="1" auto-grow />
          </v-col>
          <v-col cols="12" md="6">
            <v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12">
            <v-textarea v-model="companyTerms" label="Terms & Conditions" variant="outlined" density="compact" hide-details rows="2" auto-grow />
          </v-col>
        </v-row>

        <!-- ── Item Sort Order ── -->
        <div class="px-3 pb-3" v-if="sortedItems.length > 0">
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">ITEM ORDER IN PDF</div>
          <div class="d-flex flex-wrap gap-1">
            <div
              v-for="(it, i) in sortedItems"
              :key="it.id || i"
              class="sort-chip d-flex align-center gap-1"
            >
              <v-btn density="compact" icon size="x-small" variant="text" :disabled="i === 0" @click="moveUp(i)">
                <v-icon size="12">mdi-chevron-up</v-icon>
              </v-btn>
              <span class="text-caption" style="white-space:nowrap; max-width:120px; overflow:hidden; text-overflow:ellipsis;">
                {{ i + 1 }}. <strong>{{ it.alt || it.partNumberName }}</strong>
                <span v-if="it.rfqReference" class="text-medium-emphasis ml-1">[{{ it.rfqReference }}]</span>
              </span>
              <v-btn density="compact" icon size="x-small" variant="text" :disabled="i === sortedItems.length - 1" @click="moveDown(i)">
                <v-icon size="12">mdi-chevron-down</v-icon>
              </v-btn>
            </div>
          </div>
        </div>
      </v-container>

      <v-divider />

      <div class="flex-grow-1 overflow-y-auto d-flex justify-center pa-6" style="background: rgb(var(--v-theme-surface-variant));">
        <div ref="pdfContent" class="pdf-page" v-html="renderedHtml" />
      </div>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{ quote: any }>()
const model = defineModel<boolean>({ default: false })

const api = useApi()

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

// ── Theme derived from selected preset ──
const theme = computed(() => {
  const preset = apiPresets.value.find((p: any) => p.name === selectedPreset.value)
  return {
    primary: preset?.primaryColor || '#1a2744',
    accent: preset?.accentColor || '#2563eb',
  }
})

// Auto-select preset that matches customer's Base
watch(apiPresets, (presets) => {
  if (!presets.length) return
  const base = props.quote?.customerBase
  if (base != null) {
    const match = presets.find((p: any) => p.sortOrder === base)
    if (match) { selectedPreset.value = match.name; return }
  }
  // fallback: first preset
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
    companyTerms.value = preset.termsAndConditions || ''
    logoDataUrl.value = preset.logoBase64
      ? `data:${preset.logoMimeType};base64,${preset.logoBase64}`
      : ''
  }
})

// ── Item sort order ──
const sortedItems = ref<any[]>([])

function initSortedItems() {
  const items = [...(props.quote?.items || [])]
  items.sort((a: any, b: any) => {
    const aRef = typeof a.rfqRef === 'number' ? a.rfqRef : (typeof a.rfqReference === 'string' ? parseInt(a.rfqReference) || 999 : (a.rfqItemId ?? 999))
    const bRef = typeof b.rfqRef === 'number' ? b.rfqRef : (typeof b.rfqReference === 'string' ? parseInt(b.rfqReference) || 999 : (b.rfqItemId ?? 999))
    return aRef - bRef
  })
  sortedItems.value = items
}

function moveUp(i: number) {
  if (i <= 0) return
  const arr = [...sortedItems.value]
  ;[arr[i - 1], arr[i]] = [arr[i], arr[i - 1]]
  sortedItems.value = arr
}

function moveDown(i: number) {
  if (i >= sortedItems.value.length - 1) return
  const arr = [...sortedItems.value]
  ;[arr[i], arr[i + 1]] = [arr[i + 1], arr[i]]
  sortedItems.value = arr
}

watch(model, (open) => { if (open) { loadPresets(); initSortedItems() } })
watch(() => props.quote?.items, initSortedItems, { immediate: true })

const footerText = ref('If you have any questions about this quotation, please contact')
const logoDataUrl = ref('')
const generating = ref(false)
const taxAmount = ref(0)
const shippingAmount = ref(0)
const otherAmount = ref(0)
const currency = ref('Dollar (USD)')
const exchangeRate = ref(7.0)
const comments = ref('No Comments')

function onLogoUpload(files: File[] | File | null) {
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { logoDataUrl.value = ''; return }
  const reader = new FileReader()
  reader.onload = (e) => { logoDataUrl.value = (e.target?.result as string) || '' }
  reader.readAsDataURL(file)
}

const fmt = (n: number) => formatPrice(n)

// ── Hex → slightly-lighter tint for even rows ──
function hexToRgb(hex: string) {
  const h = hex.replace('#', '')
  const r = parseInt(h.substring(0, 2), 16)
  const g = parseInt(h.substring(2, 4), 16)
  const b = parseInt(h.substring(4, 6), 16)
  return { r, g, b }
}
function lighten(hex: string, amount = 0.92) {
  const { r, g, b } = hexToRgb(hex)
  const lr = Math.round(r + (255 - r) * amount)
  const lg = Math.round(g + (255 - g) * amount)
  const lb = Math.round(b + (255 - b) * amount)
  return `rgb(${lr},${lg},${lb})`
}

// ── Build items table HTML for custom templates ──
function buildItemsTable(items: any[], primary: string, sym: string, rate: number): string {
  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : '#f9fafb'
    const isAlt = !!it.alt
    const displayPN = isAlt ? it.alt : (it.partNumberName || '—')
    const altNote = isAlt ? `<div style="font-size:9px;color:#9ca3af;">(Req: ${it.partNumberName})</div>` : ''
    return `<tr style="background:${bg};">
      <td style="padding:6px 10px;">${it.rfqReference || '—'}</td>
      <td style="padding:6px 10px;">${i + 1}</td>
      <td style="padding:6px 10px;font-weight:600;color:${primary};">${displayPN}${altNote}</td>
      <td style="padding:6px 10px;text-align:center;">${it.qty}</td>
      <td style="padding:6px 10px;text-align:center;">${it.condition || '—'}</td>
      <td style="padding:6px 10px;text-align:center;">${it.leadTime || '—'}</td>
      <td style="padding:6px 10px;text-align:right;">${sym}${formatPrice(Number(it.unitPrice) * rate)}</td>
      <td style="padding:6px 10px;text-align:right;font-weight:700;">${sym}${formatPrice(Number(it.totalPrice) * rate)}</td>
    </tr>`
  }).join('')
  return `<table style="width:100%;border-collapse:collapse;font-size:12px;">
    <thead><tr style="background:${primary};color:#fff;">
      <th style="padding:7px 10px;text-align:left;">Ref</th>
      <th style="padding:7px 10px;text-align:left;">#</th>
      <th style="padding:7px 10px;text-align:left;">Part Number</th>
      <th style="padding:7px 10px;text-align:center;">Qty</th>
      <th style="padding:7px 10px;text-align:center;">Cond</th>
      <th style="padding:7px 10px;text-align:center;">Lead Time</th>
      <th style="padding:7px 10px;text-align:right;">Unit Price</th>
      <th style="padding:7px 10px;text-align:right;">Total</th>
    </tr></thead>
    <tbody>${rows}</tbody>
  </table>`
}

const renderedHtml = computed(() => {
  const q = props.quote
  const items: any[] = sortedItems.value.length ? sortedItems.value : (q.items || [])
  const logo = logoDataUrl.value
  const logoImg = logo ? `<img src="${logo}" style="max-height:48px; max-width:160px; object-fit:contain;" />` : ''
  const quoteDate = q.createdAt ? new Date(q.createdAt).toLocaleDateString() : '—'
  const validUntil = q.validUntil ? new Date(q.validUntil).toLocaleDateString() : '—'
  const isYuan = currency.value === 'China Yuan (CNY)'
  const sym = isYuan ? '¥' : '$'
  const rate = isYuan ? (exchangeRate.value || 1) : 1

  const subtotal = (Number(q.totalAmount) || 0) * rate
  const tax = (taxAmount.value || 0) * rate
  const shipping = (shippingAmount.value || 0) * rate
  const other = (otherAmount.value || 0) * rate
  const grandTotal = subtotal + tax + shipping + other

  const primary = theme.value.primary
  const accent = theme.value.accent

  // ── Custom HTML template override ──
  const activePreset = apiPresets.value.find((p: any) => p.name === selectedPreset.value)
  if (activePreset?.customPdfHtml) {
    const vars: Record<string, string> = {
      '{{LOGO}}':             logoImg,
      '{{COMPANY_NAME}}':     companyName.value,
      '{{COMPANY_LOCATION}}': companyLocation.value,
      '{{COMPANY_PHONE}}':    companyPhone.value,
      '{{COMPANY_EMAIL}}':    companyEmail.value,
      '{{COMPANY_WEBSITE}}':  companyWebsite.value,
      '{{QUOTE_NUMBER}}':     q.quoteNumber || '—',
      '{{DATE}}':             quoteDate,
      '{{VALID_UNTIL}}':      validUntil,
      '{{RFQ_NAME}}':         q.rfqName || '—',
      '{{CUSTOMER_NAME}}':    q.customerName || '—',
      '{{CUSTOMER_BILL_TO}}': q.customerBillTo || '—',
      '{{CUSTOMER_SHIP_TO}}': q.customerShipTo || q.customerBillTo || '—',
      '{{ITEMS_TABLE}}':      buildItemsTable(items, primary, sym, rate),
      '{{SUBTOTAL}}':         `${sym}${fmt(subtotal)}`,
      '{{SHIPPING}}':         `${sym}${fmt(shipping)}`,
      '{{GRAND_TOTAL}}':      `${sym}${fmt(grandTotal)}`,
      '{{COMMENTS}}':         comments.value || '',
      '{{TERMS}}':            companyTerms.value || '',
      '{{PRIMARY_COLOR}}':    primary,
      '{{ACCENT_COLOR}}':     accent,
    }
    let html = activePreset.customPdfHtml
    for (const [key, val] of Object.entries(vars)) {
      html = html.split(key).join(val)
    }
    return html
  }
  const rowEven = lighten(primary, 0.96)
  const accentLight = lighten(accent, 0.90)

  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : rowEven
    // If alt PN is set → show alt as the offered part number, original as reference below
    const isAlt = !!it.alt
    const displayPN = isAlt ? it.alt : (it.partNumberName || '—')
    const mainPNNote = isAlt
      ? `<div style="font-size:9px;color:#9ca3af;margin-top:1px;">(Req: ${it.partNumberName})</div>`
      : ''
    const details: string[] = []
    if (it.certName) details.push(`<span style="margin-right:16px;"><strong>Cert:</strong> ${it.certName}</span>`)
    if (it.tagDate) details.push(`<span style="margin-right:16px;"><strong>Tag Date:</strong> ${new Date(it.tagDate).getFullYear()}</span>`)
    if (it.note) details.push(`<span><strong>Note:</strong> ${it.note}</span>`)
    const detailRow = details.length > 0 ? `
    <tr style="background:${bg};">
      <td colspan="9" style="padding:2px 12px 8px 44px; font-size:10px; color:#6b7280; border-bottom:1px solid #e5e7eb;">${details.join('')}</td>
    </tr>` : ''
    return `
    <tr style="background:${bg};">
      <td style="padding:9px 8px; font-size:10px; color:#9ca3af; text-align:center; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${it.rfqReference || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; color:#6b7280; text-align:center; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${i + 1}</td>
      <td style="padding:9px 12px; font-size:11px; font-weight:600; color:${isAlt ? accent : primary}; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${displayPN}${mainPNNote}</td>
      <td style="padding:9px 12px; font-size:10.5px; color:#4b5563; max-width:120px; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${it.description || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:center; font-weight:600; color:${primary}; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${it.qty}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:${primary}; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${it.condition || '—'}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:#4b5563; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${it.leadTime || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; color:${primary}; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${sym}${fmt(Number(it.unitPrice) * rate)}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; font-weight:700; color:${primary}; ${details.length ? '' : 'border-bottom:1px solid #eef0f3;'}">${sym}${fmt(Number(it.totalPrice) * rate)}</td>
    </tr>${detailRow}`
  }).join('')

  return `
    <div style="font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif; color:${primary}; display:flex; flex-direction:column; min-height:297mm;">

      <!-- Header -->
      <div style="padding:28px 40px 20px 40px; display:flex; justify-content:space-between; align-items:flex-start;">
        <div style="display:flex; align-items:flex-start; gap:16px;">
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
          <div style="font-size:24px; font-weight:700; color:${primary}; letter-spacing:1px;">QUOTATION</div>
          <div style="font-size:11px; color:#6b7280; margin-top:4px;">${q.quoteNumber || '—'}</div>
        </div>
      </div>

      <!-- Accent line -->
      <div style="height:3px; background:linear-gradient(90deg,${primary} 0%,${accent} 40%,#e5e7eb 100%); margin:0 40px;"></div>

      <!-- Meta Row -->
      <div style="padding:16px 40px; display:flex; gap:40px; font-size:11px; color:#4b5563;">
        <div><span style="font-weight:600; color:${primary};">Date:</span> ${quoteDate}</div>
        <div><span style="font-weight:600; color:${primary};">Valid Until:</span> ${validUntil}</div>
        <div><span style="font-weight:600; color:${primary};">RFQ:</span> ${q.rfqName || '—'}</div>
        <div><span style="font-weight:600; color:${primary};">Currency:</span> ${currency.value}</div>
      </div>

      <!-- Bill To / Ship To -->
      <div style="display:flex; gap:0; margin:0 40px 20px 40px; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Bill To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${q.customerName || '—'}</div>
          ${q.customerBillTo ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${q.customerBillTo}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:8px;">Ship To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${q.customerName || '—'}</div>
          ${q.customerShipTo ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${q.customerShipTo}</div>` : (q.customerBillTo ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${q.customerBillTo}</div>` : '')}
        </div>
      </div>

      <!-- Items Table -->
      <div style="margin:0 40px 16px 40px;">
        <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
          <thead>
            <tr style="background:${primary};">
              <th style="padding:10px 8px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:32px;">Ref</th>
              <th style="padding:10px 8px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:28px;">#</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Part No.</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Description</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">Qty</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">CD</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center;">Lead Time</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Unit Price</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Total</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>

      <!-- Totals -->
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

      <!-- Comments -->
      <div style="margin:0 40px 16px 40px; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:5px;">Comments</div>
        <div style="font-size:11px; color:#4b5563; white-space:pre-wrap; line-height:1.5;">${comments.value || 'No Comments'}</div>
      </div>

      <!-- Terms & Conditions -->
      ${companyTerms.value ? `
      <div style="margin:0 40px 16px 40px; border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px; background:${rowEven};">
        <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:${accent}; margin-bottom:6px;">Terms &amp; Conditions</div>
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
  if (!renderedHtml.value) return
  generating.value = true
  try {
    const isFullDoc = renderedHtml.value.trimStart().toLowerCase().startsWith('<!doctype')
    const fullHtml = isFullDoc
      ? renderedHtml.value
      : `<!DOCTYPE html><html><head><meta charset="UTF-8"><style>body,html{margin:0;padding:0;-webkit-print-color-adjust:exact;print-color-adjust:exact;}</style></head><body>${renderedHtml.value}</body></html>`
    const response = await $fetch('/api/generate-pdf', { method: 'POST', body: { html: fullHtml }, responseType: 'blob' })
    const url = window.URL.createObjectURL(response as unknown as Blob)
    const link = document.createElement('a')
    link.href = url
    const q = props.quote
    const safeName = (s: string) => (s || '').replace(/[/\\:*?"<>|]/g, '-').trim()
    const fileName = `${safeName(q.quoteNumber || 'QT')} - ${safeName(q.customerName || '')} - ${safeName(q.rfqName || '')}.pdf`
    link.setAttribute('download', fileName)
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
  padding:20px;
  background: #fff;
  box-shadow: 0 4px 40px rgba(0,0,0,0.2);
  border-radius: 4px;
  overflow: hidden;
}

.sort-chip {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 6px;
  padding: 2px 4px;
  user-select: none;
}
</style>
