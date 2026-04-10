<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Purchase Order PDF — {{ pdfData.poNumber || '' }}</v-toolbar-title>
        <v-spacer />
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" @click="downloadPdf">Download PDF</v-btn>
      </v-toolbar>

      <!-- Controls -->
      <v-container fluid class="flex-shrink-0 py-4">
        <!-- Company Info -->
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
          <v-col cols="12" md="2"><v-text-field v-model.number="otherAmount" label="Other" variant="outlined" density="compact" hide-details type="number" prefix="$" /></v-col>
          <v-col cols="12" md="2"><v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD']" label="Currency" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>

        <v-divider class="my-3" />

        <!-- Purchase From (Supplier) -->
        <div class="text-caption font-weight-bold text-medium-emphasis mb-2">PURCHASE FROM (Supplier)</div>
        <v-row dense align="center">
          <v-col cols="12" md="3"><v-text-field v-model="purchaseFromName" label="Name" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-textarea v-model="purchaseFromAddress" label="Address" variant="outlined" rows="1" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="purchaseFromPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="purchaseFromEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>

        <v-divider class="my-3" />

        <!-- Vendor -->
        <div class="text-caption font-weight-bold text-medium-emphasis mb-2">BILL TO</div>
        <v-row dense align="center">
          <v-col cols="12" md="3"><v-text-field v-model="vendorName" label="Name" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-textarea v-model="vendorAddress" label="Address" variant="outlined" rows="1" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="vendorPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="vendorEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>

        <v-divider class="my-3" />

        <!-- Deliver To -->
        <div class="text-caption font-weight-bold text-medium-emphasis mb-2">SHIP TO</div>
        <v-row dense align="center">
          <v-col cols="12" md="3"><v-text-field v-model="deliverToName" label="Name" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-textarea v-model="deliverToAddress" label="Address" variant="outlined" rows="1" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="deliverToPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model="deliverToEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>

        <v-divider class="my-3" />

        <!-- Comments, Terms, Footer -->
        <v-row dense align="center" class="mt-1">
          <v-col cols="12" md="4"><v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="1" auto-grow /></v-col>
          <v-col cols="12" md="4"><v-textarea v-model="companyTerms" label="Terms & Conditions" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
          <v-col cols="12" md="4"><v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details /></v-col>
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
const props = defineProps<{ poId: number | string }>()
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
    companyTerms.value = preset.termsAndConditions || ''
  }
})

const companyName = ref('Your Company Name')
const companyLocation = ref('')
const companyPhone = ref('')
const companyWebsite = ref('')
const companyEmail = ref('')
const footerText = ref('If you have any questions about this purchase order, please contact')
const logoDataUrl = ref('')
const generating = ref(false)
const loadingData = ref(false)
const pdfData = ref<any>({})
const taxAmount = ref(0)
const otherAmount = ref(0)
const currency = ref('Dollar (USD)')
const comments = ref('No Comments')
const companyTerms = ref('')

// Purchase From (Supplier)
const purchaseFromName = ref('')
const purchaseFromAddress = ref('')
const purchaseFromPhone = ref('')
const purchaseFromEmail = ref('')

// Vendor
const vendorName = ref('')
const vendorAddress = ref('')
const vendorPhone = ref('')
const vendorEmail = ref('')

// Deliver To
const deliverToName = ref('')
const deliverToAddress = ref('')
const deliverToPhone = ref('')
const deliverToEmail = ref('')

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
    if (!pdfData.value.poNumber) {
      loadingData.value = true
      try {
        const data = await api.get<any>(`/purchase-orders/${props.poId}/pdf-data`)
        pdfData.value = data
        if (data.importDetail?.comments) comments.value = data.importDetail.comments

        // Initialize Purchase From (Supplier) - use supplier data
        const supplier = data.supplier || {}
        purchaseFromName.value = supplier.name || ''
        purchaseFromAddress.value = supplier.address || ''
        purchaseFromPhone.value = supplier.phone || ''
        purchaseFromEmail.value = supplier.email || ''

        // Initialize Vendor
        const vendor = data.vendor || {}
        vendorName.value = vendor.name || ''
        vendorAddress.value = vendor.address || ''
        vendorPhone.value = vendor.phone || ''
        vendorEmail.value = vendor.email || ''

        // Initialize Deliver To
        const deliver = data.deliverTo || {}
        deliverToName.value = deliver.name || ''
        deliverToAddress.value = deliver.address || ''
        deliverToPhone.value = deliver.phone || ''
        deliverToEmail.value = deliver.email || ''
      } catch (e) { console.error('[PoPdf] Failed to load PDF data', e) }
      finally { loadingData.value = false }
    }
  }
})

const fmt = (n: number) => formatPrice(n)

const renderedHtml = computed(() => {
  const d = pdfData.value
  if (!d.poNumber) return ''

  const items: any[] = d.items || []
  const vendor = d.vendor || {}
  const deliver = d.deliverTo || {}
  const importDtl = d.importDetail || {}
  const logo = logoDataUrl.value

  const logoImg = logo
    ? `<img src="${logo}" style="max-height:48px; max-width:160px; object-fit:contain;" />`
    : ''

  const poDate = d.createdAt ? new Date(d.createdAt).toLocaleDateString() : '—'
  const subtotal = Number(d.totalAmount) || 0
  const totalShipping = items.reduce((s: number, it: any) => s + (Number(it.shippingCost) || 0), 0)
  const tax = taxAmount.value || 0
  const other = otherAmount.value || 0
  const grandTotal = subtotal + totalShipping + tax + other

  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : '#f7f8fa'
    return `
    <tr style="background:${bg};">
      <td style="padding:9px 12px; font-size:11px; color:#6b7280; text-align:center; border-bottom:1px solid #eef0f3;">${i + 1}</td>
      <td style="padding:9px 12px; font-size:11px; font-weight:600; color:#1a2744; border-bottom:1px solid #eef0f3;">${it.partNumber || '—'}</td>
      <td style="padding:9px 12px; font-size:10.5px; color:#4b5563; border-bottom:1px solid #eef0f3; max-width:120px;">${it.description || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:center; font-weight:600; color:#1a2744; border-bottom:1px solid #eef0f3;">${it.qty}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:#1a2744; border-bottom:1px solid #eef0f3;">${it.condition || '—'}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:#4b5563; border-bottom:1px solid #eef0f3;">${it.certification || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; color:#1a2744; border-bottom:1px solid #eef0f3;">$${fmt(Number(it.unitPrice))}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; font-weight:700; color:#1a2744; border-bottom:1px solid #eef0f3;">$${fmt(Number(it.totalPrice))}</td>
      <td style="padding:9px 12px; font-size:10.5px; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.note || ''}</td>
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
          <div style="font-size:24px; font-weight:700; color:#1a2744; letter-spacing:1px;">PURCHASE ORDER</div>
          <div style="font-size:11px; color:#6b7280; margin-top:4px;">${d.poNumber}</div>
        </div>
      </div>

      <!-- Accent line -->
      <div style="height:3px; background:linear-gradient(90deg,#1a2744 0%,#2563eb 40%,#e5e7eb 100%); margin:0 40px;"></div>

      <!-- ═══ Meta Row ═══ -->
      <div style="padding:16px 40px; display:flex; gap:40px; font-size:11px; color:#4b5563;">
        <div><span style="font-weight:600; color:#1a2744;">Date:</span> ${poDate}</div>
        <div><span style="font-weight:600; color:#1a2744;">Status:</span> ${d.status || '—'}</div>
        <div><span style="font-weight:600; color:#1a2744;">Currency:</span> ${currency.value}</div>
      </div>

      <!-- ═══ Purchase From / BILL TO / Deliver To ═══ -->
      <div style="display:flex; gap:0; margin:0 40px 20px 40px; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Purchase From</div>
          <div style="font-size:12px; font-weight:700; color:#1a2744; margin-bottom:3px;">${purchaseFromName.value || '—'}</div>
          ${purchaseFromAddress.value ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${purchaseFromAddress.value}</div>` : ''}
          ${purchaseFromPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-top:4px;">Tel: ${purchaseFromPhone.value}</div>` : ''}
          ${purchaseFromEmail.value ? `<div style="font-size:10.5px; color:#4b5563;">Email: ${purchaseFromEmail.value}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">BIll TO</div>
          <div style="font-size:12px; font-weight:700; color:#1a2744; margin-bottom:3px;">${vendorName.value || '—'}</div>
          ${vendorAddress.value ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${vendorAddress.value}</div>` : ''}
          ${vendorPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-top:4px;">Tel: ${vendorPhone.value}</div>` : ''}
          ${vendorEmail.value ? `<div style="font-size:10.5px; color:#4b5563;">Email: ${vendorEmail.value}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Deliver To</div>
          <div style="font-size:12px; font-weight:700; color:#1a2744; margin-bottom:3px;">${deliverToName.value || '—'}</div>
          ${deliverToAddress.value ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${deliverToAddress.value}</div>` : ''}
          ${deliverToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-top:4px;">Tel: ${deliverToPhone.value}</div>` : ''}
          ${deliverToEmail.value ? `<div style="font-size:10.5px; color:#4b5563;">Email: ${deliverToEmail.value}</div>` : ''}
        </div>
      </div>

      <!-- ═══ Items Table ═══ -->
      <div style="margin:0 40px 16px 40px;">
        <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
          <thead>
            <tr style="background:#1a2744;">
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">#</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Part No.</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Description</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">Qty</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">CD</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center;">Cert</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Buy Price</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:right;">Amount</th>
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:left;">Note</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>

      <!-- ═══ Totals + Shipping Info ═══ -->
      <div style="display:flex; justify-content:space-between; align-items:flex-start; margin:0 40px 16px 40px;">
        <div style="font-size:11px; max-width:340px;">
          ${importDtl.fedExAccount || importDtl.shippingMethod || importDtl.incoterms ? `
            <div style="border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
              <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:6px;">Shipping Information</div>
              ${importDtl.shippingMethod ? `<div style="color:#1a2744; margin-top:2px;"><span style="font-weight:600;">Shipping Method:</span> ${importDtl.shippingMethod}</div>` : ''}
              ${importDtl.incoterms ? `<div style="color:#1a2744; margin-top:2px;"><span style="font-weight:600;">Incoterms:</span> ${importDtl.incoterms}</div>` : ''}
              ${importDtl.fedExAccount ? `<div style="color:#1a2744; margin-top:2px;"><span style="font-weight:600;">FedEx Account:</span> ${importDtl.fedExAccount}</div>` : ''}
              ${importDtl.servicePriority ? `<div style="color:#1a2744; margin-top:2px;"><span style="font-weight:600;">Service Priority:</span> ${importDtl.servicePriority}</div>` : ''}
            </div>
          ` : ''}
        </div>
        <div style="min-width:260px;">
          <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden; font-size:11px;">
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Subtotal</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(subtotal)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Tax</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(tax)}</td></tr>
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Shipping</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(totalShipping)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #e5e7eb;">Other</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #e5e7eb;">$${fmt(other)}</td></tr>
            <tr style="background:#1a2744;"><td style="padding:10px 14px; color:#fff; font-weight:700;">Total</td><td style="padding:10px 14px; text-align:right; color:#fff; font-weight:800; font-size:14px;">$${fmt(grandTotal)}</td></tr>
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
    const importDtl = d.importDetail || {}
    const totalShipping = items.reduce((s: number, it: any) => s + (Number(it.shippingCost) || 0), 0)

    const payload = {
      companyName: companyName.value,
      companyLocation: companyLocation.value,
      companyPhone: companyPhone.value,
      companyWebsite: companyWebsite.value,
      companyEmail: companyEmail.value,
      logoBase64: logoDataUrl.value || null,
      primaryColor: theme.value.primary,
      accentColor: theme.value.accent,
      poNumber: d.poNumber || '',
      poDate: d.createdAt ? new Date(d.createdAt).toLocaleDateString() : '—',
      orderedBy: d.orderedBy || '—',
      status: d.status || '—',
      currency: currency.value,
      currencySymbol: '$',
      // Purchase From (Supplier)
      purchaseFromName: purchaseFromName.value || null,
      purchaseFromAddress: purchaseFromAddress.value || null,
      purchaseFromPhone: purchaseFromPhone.value || null,
      purchaseFromEmail: purchaseFromEmail.value || null,
      // Vendor
      vendorName: vendorName.value || null,
      vendorAddress: vendorAddress.value || null,
      vendorPhone: vendorPhone.value || null,
      vendorEmail: vendorEmail.value || null,
      // Deliver To
      deliverToName: deliverToName.value || null,
      deliverToAddress: deliverToAddress.value || null,
      deliverToPhone: deliverToPhone.value || null,
      deliverToEmail: deliverToEmail.value || null,
      shippingMethod: importDtl.shippingMethod || null,
      incoterms: importDtl.incoterms || null,
      fedExAccount: importDtl.fedExAccount || null,
      servicePriority: importDtl.servicePriority || null,
      items: items.map((it: any) => ({
        partNumber: it.partNumber || null,
        description: it.description || null,
        qty: it.qty || 0,
        condition: it.condition || null,
        certification: it.certification || null,
        unitPrice: Number(it.unitPrice) || 0,
        totalPrice: Number(it.totalPrice) || 0,
        shippingCost: Number(it.shippingCost) || 0,
        note: it.note || null,
      })),
      subtotal: Number(d.totalAmount) || 0,
      tax: taxAmount.value || 0,
      totalShipping,
      other: otherAmount.value || 0,
      comments: comments.value || null,
      terms: companyTerms.value || null,
      footerText: footerText.value || null,
    }

    const response = await $fetch<Blob>(`${config.public.apiBase}/pdf/po`, {
      method: 'POST',
      body: payload,
      responseType: 'blob',
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    const url = window.URL.createObjectURL(response)
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', `${d.poNumber || 'PO'}.pdf`)
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
