<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background" style="overflow:hidden;">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Purchase Order PDF — {{ pdfData.poNumber || '' }}</v-toolbar-title>
        <v-spacer />
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" @click="downloadPdf">Download PDF</v-btn>
      </v-toolbar>

      <!-- Section toggle chips -->
      <div class="d-flex flex-wrap gap-2 px-4 py-2" style="border-bottom:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
        <v-chip
          v-for="s in sections"
          :key="s.key"
          :color="s.open ? 'primary' : 'default'"
          :variant="s.open ? 'tonal' : 'outlined'"
          size="small"
          :prepend-icon="s.open ? 'mdi-chevron-up' : 'mdi-chevron-down'"
          class="cursor-pointer"
          @click="s.open = !s.open"
        >{{ s.label }}</v-chip>
      </div>

      <!-- Side-by-side layout: controls (left) + PDF preview (right) -->
      <div class="d-flex flex-grow-1" style="overflow:hidden; min-height:0;">

        <!-- ── Left panel: collapsible form sections ── -->
        <div class="overflow-y-auto flex-shrink-0 pa-4" style="width:480px; border-right:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">

          <!-- COMPANY -->
          <template v-if="sections[0].open">
            <div class="section-label">Company</div>
            <v-row dense align="center">
              <v-col cols="12"><v-select v-model="selectedPreset" :items="companyPresetOptions" label="Company Preset" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-domain" :loading="presetsLoading" /></v-col>
              <v-col cols="12">
                <div class="d-flex align-center gap-2">
                  <v-file-input label="Company Logo" variant="outlined" density="compact" hide-details accept="image/*" prepend-icon="mdi-image" class="flex-grow-1" @update:model-value="onLogoUpload" />
                  <v-btn v-if="logoDataUrl" icon="mdi-image-remove" size="small" variant="tonal" color="error" density="compact" title="Remove Logo" @click="logoDataUrl = ''" />
                </div>
              </v-col>
              <v-col cols="12"><v-text-field v-model="companyName" label="Company Name" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
              <v-col cols="12"><v-textarea v-model="companyLocation" label="Company Address" variant="outlined" density="compact" hide-details rows="2" :disabled="selectedPreset !== 'Custom'" /></v-col>
              <v-col cols="6"><v-text-field v-model="companyPhone" label="Phone" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
              <v-col cols="6"><v-text-field v-model="companyWebsite" label="Website" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
              <v-col cols="12"><v-text-field v-model="companyEmail" label="Contact Email" variant="outlined" density="compact" hide-details :disabled="selectedPreset !== 'Custom'" /></v-col>
              <v-col cols="12"><v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD']" label="Currency" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- VENDOR -->
          <template v-if="sections[1].open">
            <div class="section-label">Vendor (Supplier)</div>
            <v-row dense align="center">
              <v-col cols="12"><v-text-field v-model="purchaseFromName" label="Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-textarea v-model="purchaseFromAddress" label="Address" variant="outlined" rows="2" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="purchaseFromPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="purchaseFromEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- SHIP TO / BILL TO -->
          <template v-if="sections[2].open">
            <!-- Warehouse auto-fill -->
            <v-row dense align="center" class="mb-2">
              <v-col cols="12">
                <v-autocomplete
                  v-model="selectedWarehouseId"
                  :items="presetWarehouses"
                  :item-title="item => item.displayName ? `${item.name} (${item.displayName})` : item.name"
                  item-value="id"
                  label="Warehouse (auto-fills both)"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  prepend-inner-icon="mdi-home-city-outline"
                  @update:model-value="onWarehouseSelected"
                />
              </v-col>
            </v-row>

            <!-- Bill To -->
            <div class="d-flex align-center mb-1 gap-2">
              <span class="section-label mb-0">Bill To</span>
              <v-btn size="x-small" variant="text" color="primary" prepend-icon="mdi-content-copy" @click="syncBillToFromShipTo">Copy from Ship To</v-btn>
            </div>
            <v-row dense align="center">
              <v-col cols="12"><v-text-field v-model="billToName" label="Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-textarea v-model="billToAddress" label="Address" variant="outlined" rows="2" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="billToPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="billToEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>

            <v-divider class="my-3" />

            <!-- Ship To -->
            <div class="section-label">Ship To</div>
            <v-row dense align="center">
              <v-col cols="12"><v-text-field v-model="deliverToName" label="Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-textarea v-model="deliverToAddress" label="Address" variant="outlined" rows="2" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="deliverToPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="deliverToEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- TOTALS -->
          <template v-if="sections[3].open">
            <div class="d-flex align-center mb-2 gap-2">
              <span class="section-label mb-0">PDF Totals</span>
              <v-spacer />
              <v-btn size="x-small" variant="tonal" color="primary" prepend-icon="mdi-content-save" :loading="savingTotals" @click="saveTotalsToPo">Save to PO</v-btn>
            </div>
            <v-row dense align="center">
              <v-col cols="12"><v-text-field v-model="poDate" label="PO Date" variant="outlined" density="compact" hide-details type="date" prepend-inner-icon="mdi-calendar" /></v-col>
              <v-col cols="12"><v-text-field v-model.number="processingFeeAmount" label="Processing Fee" variant="outlined" density="compact" hide-details type="number" prefix="$" prepend-inner-icon="mdi-cog-outline" /></v-col>
              <v-col cols="12"><v-text-field v-model.number="shippingAmount" label="Shipping" variant="outlined" density="compact" hide-details type="number" prefix="$" prepend-inner-icon="mdi-truck-outline" /></v-col>
              <v-col cols="12"><v-text-field v-model.number="taxAmount" label="Tax" variant="outlined" density="compact" hide-details type="number" prefix="$" prepend-inner-icon="mdi-percent-outline" /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- SHIPPING & ACCOUNT -->
          <template v-if="sections[4].open">
            <div class="d-flex align-center mb-2 gap-2">
              <span class="section-label mb-0">Shipping &amp; Account</span>
              <v-spacer />
              <v-btn size="x-small" variant="tonal" color="primary" prepend-icon="mdi-content-save" :loading="savingShipping" @click="saveShippingToPo">Save to PO</v-btn>
            </div>
            <v-row dense align="center">
              <v-col cols="12"><v-text-field v-model="fedExAccount" label="FedEx Account" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-truck-fast" /></v-col>
              <v-col cols="12"><v-text-field v-model="servicePriority" label="Service Priority" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-select v-model="shippingMethod" :items="['Air', 'Sea', 'Ground', 'Express']" label="Shipping Method" variant="outlined" density="compact" hide-details clearable /></v-col>
              <v-col cols="6"><v-select v-model="incoterms" :items="['FOB', 'CIF', 'EXW', 'DDP', 'FCA', 'CPT', 'DAP']" label="Incoterms" variant="outlined" density="compact" hide-details clearable /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- COMMENTS / TERMS -->
          <template v-if="sections[5].open">
            <div class="section-label">Comments &amp; Terms</div>
            <v-row dense align="center">
              <v-col cols="12"><v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
              <v-col cols="12"><v-textarea v-model="companyTerms" label="Terms & Conditions" variant="outlined" density="compact" hide-details rows="3" auto-grow /></v-col>
              <v-col cols="12"><v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
          </template>

        </div>

        <!-- ── Right panel: PDF preview ── -->
        <div class="flex-grow-1 overflow-y-auto d-flex justify-center pa-6" style="background: rgb(var(--v-theme-surface-variant));">
          <div v-if="loadingData" class="d-flex align-center justify-center" style="width:210mm;">
            <v-progress-circular indeterminate color="primary" size="48" />
          </div>
          <div v-else ref="pdfContent" class="pdf-page" v-html="renderedHtml" />
        </div>

      </div>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{ poId: number | string }>()
const model = defineModel<boolean>({ default: false })
const api = useApi()
const authStore = useAuthStore()
const isAdmin = computed(() => authStore.isAdmin)

// ── Section visibility toggles ──
const sections = reactive([
  { key: 'company',  label: 'Company',           open: true  },
  { key: 'vendor',   label: 'Vendor',             open: false },
  { key: 'shipto',   label: 'Bill To / Ship To',  open: false },
  { key: 'totals',   label: 'Totals',             open: false },
  { key: 'shipping', label: 'Shipping & Account', open: false },
  { key: 'comments', label: 'Comments & Terms',   open: false },
])

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

// PO PDF: admins see every preset; non-admins see only the sortOrder=105 preset + Custom.
const companyPresetOptions = computed(() => {
  const list = isAdmin.value
    ? apiPresets.value
    : apiPresets.value.filter((p: any) => p.sortOrder === 105)
  return [...list.map((p: any) => p.name), 'Custom']
})

const theme = computed(() => {
  const preset = apiPresets.value.find((p: any) => p.name === selectedPreset.value)
  return {
    primary: preset?.primaryColor || '#1a2744',
    accent:  preset?.accentColor  || '#2563eb',
  }
})

watch(apiPresets, (presets) => {
  if (!presets.length) return
  const base105 = presets.find((p: any) => p.sortOrder === 105)
  if (!isAdmin.value) {
    // Non-admins are locked to the Base-105 preset (or Custom).
    selectedPreset.value = base105 ? base105.name : 'Custom'
    return
  }
  // Admins: prefer Base 105, otherwise fall back to the first preset.
  if (selectedPreset.value === 'Custom') {
    selectedPreset.value = base105 ? base105.name : presets[0].name
  }
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
    // If delivering to ourselves, default to the preset's ship-to address if available
    if (preset.shipToAddress && (!deliverToAddress.value || selectedPreset.value !== 'Custom')) {
      deliverToAddress.value = preset.shipToAddress
      if (!deliverToName.value || selectedPreset.value !== 'Custom') {
        deliverToName.value = preset.name
      }
      if (preset.shipToPhone) deliverToPhone.value = preset.shipToPhone
    }
    // Load warehouses linked to this preset for the Ship To picker
    loadPresetWarehouses(val)
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
// PO-level cost adjustments — pre-filled from PurchaseOrder.{ProcessingFee,Shipping,Tax}.
// Shipping here is the flat PO-level value (NOT the sum of per-item shippingCost).
const processingFeeAmount = ref(0)
const shippingAmount = ref(0)
const savingTotals = ref(false)
const currency = ref('Dollar (USD)')
const comments = ref('No Comments')
const companyTerms = ref('')
const poDate = ref(new Date().toISOString().substring(0, 10))

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
const selectedWarehouseId = ref<number | null>(null)
const presetWarehouses = ref<any[]>([])

// Bill To — starts same as Ship To but user can edit independently
const billToName = ref('')
const billToAddress = ref('')
const billToPhone = ref('')
const billToEmail = ref('')

/** Sync Bill To from Ship To fields (called on initial load and warehouse select) */
function syncBillToFromShipTo() {
  billToName.value = deliverToName.value
  billToAddress.value = deliverToAddress.value
  billToPhone.value = deliverToPhone.value
  billToEmail.value = deliverToEmail.value
}

async function loadPresetWarehouses(presetName: string) {
  const preset = apiPresets.value.find((p: any) => p.name === presetName)
  if (!preset) { presetWarehouses.value = []; return }
  try {
    presetWarehouses.value = await api.get(`/companypresets/${preset.id}/warehouses`)
  } catch { presetWarehouses.value = [] }
  selectedWarehouseId.value = null
}

function onWarehouseSelected(warehouseId: number | null) {
  if (!warehouseId) return
  const wh = presetWarehouses.value.find((w: any) => w.id === warehouseId)
  if (wh) {
    deliverToName.value = wh.displayName || wh.name || ''
    deliverToAddress.value = wh.address || ''
    deliverToPhone.value = wh.phone || ''
    deliverToEmail.value = wh.email || ''
    // Sync Bill To to match the newly selected warehouse
    syncBillToFromShipTo()
  }
}

async function saveTotalsToPo() {
  savingTotals.value = true
  try {
    const payload = {
      processingFee: processingFeeAmount.value,
      shipping:      shippingAmount.value,
      tax:           taxAmount.value,
      poDate:        poDate.value,
    }
    await api.patch(`/purchase-orders/${props.poId}/totals`, payload)
  } catch (e) { console.error('[PoPdf] Failed to save totals to PO', e) }
  finally { savingTotals.value = false }
}

// Shipping & Account — editable per-print overrides for the FedEx / courier block on the PDF.
// Initialised from the PO's saved Import Detail, used by both the live preview and the
// /pdf/po payload, and can be persisted back to the PO via "Save to PO".
const fedExAccount = ref('')
const servicePriority = ref('')
const shippingMethod = ref<string | null>(null)
const incoterms = ref<string | null>(null)
const savingShipping = ref(false)

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

        // Initialize Deliver To (Ship To)
        const deliver = data.deliverTo || {}
        deliverToName.value = deliver.name || ''
        deliverToAddress.value = deliver.address || ''
        deliverToPhone.value = deliver.phone || ''
        deliverToEmail.value = deliver.email || ''
        // Bill To starts identical to Ship To — user can then edit each independently
        syncBillToFromShipTo()

        // Initialize Shipping & Account from the PO's saved Import Detail.
        // For Customer-ExWork POs, the deliverTo block already carries a fedexAccount that comes
        // from the customer; fall back to that when the PO has no Import Detail of its own.
        const importDtl = data.importDetail || {}
        fedExAccount.value = importDtl.fedExAccount || deliver.fedexAccount || ''
        servicePriority.value = importDtl.servicePriority || ''
        shippingMethod.value = importDtl.shippingMethod || null
        incoterms.value = importDtl.incoterms || null

        // Initialize PDF Totals from the PurchaseOrder row (processingFee / shipping / tax).
        // Shipping here is the flat PO-level number — never derived from per-item shippingCost.
        processingFeeAmount.value = Number(data.processingFee) || 0
        shippingAmount.value = Number(data.shipping) || 0
        taxAmount.value = Number(data.tax) || 0
        if (data.poDate) {
          poDate.value = new Date(data.poDate).toISOString().substring(0, 10)
        } else {
          poDate.value = new Date().toISOString().substring(0, 10)
        }
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
  // FedEx / shipping/incoterms now come from the editable refs (fedExAccount, shippingMethod, etc.)
  // so we don't need to deref data.importDetail here anymore.
  const primary = theme.value.primary
  const accent = theme.value.accent
  const logo = logoDataUrl.value

  const logoImg = logo
    ? `<img src="${logo}" style="max-height:48px; max-width:160px; object-fit:contain;" />`
    : ''

  const poDateVal = poDate.value || '—'
  const subtotal = Number(d.totalAmount) || 0
  // Shipping is the flat PO-level value the user typed — never the sum of item shippingCost.
  const totalShipping = shippingAmount.value || 0
  const tax = taxAmount.value || 0
  const other = otherAmount.value || 0
  const processingFee = processingFeeAmount.value || 0
  const grandTotal = subtotal + totalShipping + tax + other + processingFee

  const rows = items.map((it: any, i: number) => {
    const bg = i % 2 === 0 ? '#ffffff' : '#f7f8fa'
    return `
    <tr style="background:${bg};">
      <td style="padding:9px 12px; font-size:11px; color:#6b7280; text-align:center; border-bottom:1px solid #eef0f3;">${i + 1}</td>
      <td style="padding:9px 12px; font-size:11px; font-weight:600; color:${primary}; border-bottom:1px solid #eef0f3;">${it.partNumber || '—'}</td>
      <td style="padding:9px 12px; font-size:10.5px; color:#4b5563; border-bottom:1px solid #eef0f3; max-width:120px;">${it.description || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:center; font-weight:600; color:${primary}; border-bottom:1px solid #eef0f3;">${it.qty}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:${primary}; border-bottom:1px solid #eef0f3;">${it.condition || '—'}</td>
      <td style="padding:9px 12px; font-size:10.5px; text-align:center; color:#4b5563; border-bottom:1px solid #eef0f3;">${it.certification || '—'}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; color:${primary}; border-bottom:1px solid #eef0f3;">$${fmt(Number(it.unitPrice))}</td>
      <td style="padding:9px 12px; font-size:11px; text-align:right; font-weight:700; color:${primary}; border-bottom:1px solid #eef0f3;">$${fmt(Number(it.totalPrice))}</td>
      <td style="padding:9px 12px; font-size:10.5px; color:#6b7280; border-bottom:1px solid #eef0f3;">${it.note || ''}</td>
    </tr>`
  }).join('')

  return `
    <div style="font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif; color:${primary}; display:flex; flex-direction:column; min-height:297mm;">

      <!-- ═══ Header ═══ -->
      <div style="padding:28px 40px 20px 40px; display:flex; justify-content:space-between; align-items:flex-start;">
        <div style="display:flex; align-items:center; gap:16px;">
          ${logoImg}
          <div>
            <div style="font-size:20px; font-weight:700; color:${primary}; letter-spacing:0.3px;">${companyName.value}</div>
            <div style="font-size:9.5px; color:#6b7280; margin-top:3px; line-height:1.6;">${companyLocation.value}<br/>Tel: ${companyPhone.value} &nbsp;|&nbsp; ${companyWebsite.value}</div>
          </div>
        </div>
        <div style="text-align:right;">
          <div style="font-size:24px; font-weight:700; color:${primary}; letter-spacing:1px;">PURCHASE ORDER</div>
          <div style="font-size:11px; color:#6b7280; margin-top:4px;">${d.poNumber}</div>
        </div>
      </div>

      <!-- Accent line -->
      <div style="height:3px; background:linear-gradient(90deg,${primary} 0%,${accent} 40%,#e5e7eb 100%); margin:0 40px;"></div>

      <!-- ═══ Meta Row ═══ -->
      <div style="padding:16px 40px; display:flex; gap:40px; font-size:11px; color:#4b5563;">
        <div><span style="font-weight:600; color:${primary};">Date:</span> ${poDateVal}</div>

        <div><span style="font-weight:600; color:${primary};">Currency:</span> ${currency.value}</div>
      </div>

      <!-- ═══ Vendor / Bill To / Ship To ═══ -->
      <div style="display:flex; gap:0; margin:0 40px 20px 40px; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Vendor</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${purchaseFromName.value || '—'}</div>
          ${purchaseFromAddress.value ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${purchaseFromAddress.value}</div>` : ''}
          ${purchaseFromPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-top:4px;">Tel: ${purchaseFromPhone.value}</div>` : ''}
          ${purchaseFromEmail.value ? `<div style="font-size:10.5px; color:#4b5563;">Email: ${purchaseFromEmail.value}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px; border-right:1px solid #e5e7eb;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Bill To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${billToName.value || '—'}</div>
          ${billToAddress.value ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${billToAddress.value}</div>` : ''}
          ${billToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-top:4px;">Tel: ${billToPhone.value}</div>` : ''}
          ${billToEmail.value ? `<div style="font-size:10.5px; color:#4b5563;">Email: ${billToEmail.value}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Ship To</div>
          <div style="font-size:12px; font-weight:700; color:${primary}; margin-bottom:3px;">${deliverToName.value || '—'}</div>
          ${deliverToAddress.value ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5;">${deliverToAddress.value}</div>` : ''}
          ${deliverToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-top:4px;">Tel: ${deliverToPhone.value}</div>` : ''}
          ${deliverToEmail.value ? `<div style="font-size:10.5px; color:#4b5563;">Email: ${deliverToEmail.value}</div>` : ''}
        </div>
      </div>

      <!-- ═══ Items Table ═══ -->
      <div style="margin:0 40px 16px 40px;">
        <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden;">
          <thead>
            <tr style="background:${primary};">
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
          ${fedExAccount.value ? `
            <div style="border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px; margin-bottom:8px;">
              <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:6px;">FedEx Account Information</div>
              <div style="color:${primary}; margin-top:2px;"><span style="font-weight:600;">Account Number:</span> ${fedExAccount.value}</div>
              ${servicePriority.value ? `<div style="color:${primary}; margin-top:2px;"><span style="font-weight:600;">Service Priority:</span> ${servicePriority.value}</div>` : ''}
            </div>
          ` : ''}
          ${shippingMethod.value || incoterms.value ? `
            <div style="border:1px solid #e5e7eb; border-radius:6px; padding:12px 16px;">
              <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:6px;">Shipping Information</div>
              ${shippingMethod.value ? `<div style="color:${primary}; margin-top:2px;"><span style="font-weight:600;">Shipping Method:</span> ${shippingMethod.value}</div>` : ''}
              ${incoterms.value ? `<div style="color:${primary}; margin-top:2px;"><span style="font-weight:600;">Incoterms:</span> ${incoterms.value}</div>` : ''}
            </div>
          ` : ''}
        </div>
        <div style="min-width:260px;">
          <table style="width:100%; border-collapse:collapse; border:1px solid #e5e7eb; border-radius:6px; overflow:hidden; font-size:11px;">
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Subtotal</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(subtotal)}</td></tr>
            ${processingFee > 0 ? `<tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Processing Fee</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(processingFee)}</td></tr>` : ''}
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Tax</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(tax)}</td></tr>
            <tr><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Shipping</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">$${fmt(totalShipping)}</td></tr>
            <tr style="background:${primary};"><td style="padding:10px 14px; color:#fff; font-weight:700;">Total</td><td style="padding:10px 14px; text-align:right; color:#fff; font-weight:800; font-size:14px;">$${fmt(grandTotal)}</td></tr>
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
      <div style="margin-top:auto; padding:16px 40px; border-top:2px solid ${primary}; display:flex; justify-content:space-between; align-items:center;">
        <span style="font-size:10px; color:#6b7280;">${footerText.value}</span>
        <span style="font-size:10px; font-weight:600; color:${primary};">${companyEmail.value}</span>
      </div>
    </div>
  `
})

const pdfContent = ref<HTMLElement | null>(null)

// Persist the FedEx Account / shipping overrides back to the PO's Import Detail.
// Reads the current saved detail first (so we don't blow away bank info / notes),
// merges the four shipping/account fields from the dialog, and PUTs the whole record back.
async function saveShippingToPo() {
  savingShipping.value = true
  try {
    let existing: any = null
    try { existing = await api.get<any>(`/purchase-orders/${props.poId}/import-detail`) } catch {}
    const payload = {
      bankName:          existing?.bankName          ?? null,
      bankAccountNumber: existing?.bankAccountNumber ?? null,
      bankAddress:       existing?.bankAddress       ?? null,
      bankCity:          existing?.bankCity          ?? null,
      bankCountry:       existing?.bankCountry       ?? null,
      fedExAccount:      fedExAccount.value          || null,
      courierName:       servicePriority.value       || existing?.courierName || null,
      shippingMethod:    shippingMethod.value        || null,
      incoterms:         incoterms.value             || null,
      notes:             existing?.notes             ?? null,
    }
    const saved = await api.put<any>(`/purchase-orders/${props.poId}/import-detail`, payload)
    // Refresh local pdfData.importDetail so a re-open of the dialog shows the saved values
    if (pdfData.value) {
      pdfData.value.importDetail = {
        ...(pdfData.value.importDetail || {}),
        fedExAccount: saved?.fedExAccount ?? fedExAccount.value,
        servicePriority: saved?.courierName ?? servicePriority.value,
        shippingMethod: saved?.shippingMethod ?? shippingMethod.value,
        incoterms: saved?.incoterms ?? incoterms.value,
      }
    }
  } catch (e) { console.error('[PoPdf] Failed to save shipping to PO', e) }
  finally { savingShipping.value = false }
}

async function downloadPdf() {
  generating.value = true
  try {
    const config = useRuntimeConfig()
    const authStore = useAuthStore()
    const d = pdfData.value
    const items: any[] = d.items || []
    const importDtl = d.importDetail || {}
    const totalShipping = items.reduce((s: number, it: any) => s + (0), 0)


    
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
      poDate: poDate.value || '—',
      orderedBy: d.orderedBy || '—',
      status: d.status || '—',
      currency: currency.value,
      currencySymbol: '$',
      // Purchase From (Supplier)
      purchaseFromName: purchaseFromName.value || null,
      purchaseFromAddress: purchaseFromAddress.value || null,
      purchaseFromPhone: purchaseFromPhone.value || null,
      purchaseFromEmail: purchaseFromEmail.value || null,
      // Vendor (Bill To box in the PDF — backend uses VendorName/Address/Phone/Email for "Bill To")
      vendorName: billToName.value || null,
      vendorAddress: billToAddress.value || null,
      vendorPhone: billToPhone.value || null,
      vendorEmail: billToEmail.value || null,
      // Deliver To / Ship To
      deliverToName: deliverToName.value || null,
      deliverToAddress: deliverToAddress.value || null,
      deliverToPhone: deliverToPhone.value || null,
      deliverToEmail: deliverToEmail.value || null,
      shippingMethod: shippingMethod.value || null,
      incoterms: incoterms.value || null,
      fedExAccount: fedExAccount.value || null,
      servicePriority: servicePriority.value || null,
      items: items.map((it: any) => ({
        partNumber: it.partNumber || null,
        description: it.description || null,
        qty: it.qty || 0,
        condition: it.condition || null,
        certification: it.certification || null,
        unitPrice: Number(it.unitPrice) || 0,
        totalPrice: Number(it.totalPrice) || 0,
        shippingCost: 0,
        note: it.note || null,
      })),
      subtotal: Number(d.totalAmount) || 0,
      tax: taxAmount.value || 0,
      totalShipping:shippingAmount.value || 0,
      // processingFeeAmount: processingFeeAmount.value || 0,
      ProcessingFee: processingFeeAmount.value || 0,
      comments: comments.value || null,
      terms: companyTerms.value || null,
      footerText: footerText.value || null,
    }

    const response = await $fetch<Blob>(`${api.baseURL}/pdf/po`, {
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

.section-label {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
  margin-bottom: 10px;
}
</style>
