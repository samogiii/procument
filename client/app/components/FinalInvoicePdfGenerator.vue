<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" color="background" style="overflow:hidden;">
      <v-toolbar color="surface" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">Final Invoice PDF — {{ pdfData.invoiceNumber || '' }}</v-toolbar-title>
        <v-spacer />
        <v-btn variant="tonal" color="info" prepend-icon="mdi-package-variant" :loading="generatingPacking" :disabled="!selectedItemsList.length" class="mr-2" @click="openPackingListDialog">Packing List</v-btn>
        <v-select
          v-model="pdfTemplate"
          :items="PDF_TEMPLATE_OPTIONS"
          label="Template"
          variant="outlined"
          density="compact"
          hide-details
          class="mr-3"
          style="max-width:210px;"
          prepend-inner-icon="mdi-file-document-outline"
        />
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-download" :loading="generating" :disabled="!selectedItemsList.length" @click="downloadPdf">Download PDF</v-btn>
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

      <!-- Side-by-side layout -->
      <div class="d-flex flex-grow-1" style="overflow:hidden; min-height:0;">

        <!-- ── Left panel: collapsible sections ── -->
        <div class="overflow-y-auto flex-shrink-0 pa-4" style="width:480px; border-right:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">

          <!-- COMPANY -->
          <template v-if="sections[0].open">
            <div class="section-label">Company</div>
            <v-row dense align="center">
              <v-col cols="12">
                <v-select v-model="selectedPreset" :items="companyPresetOptions" label="Company Preset" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-domain" :loading="presetsLoading" />
              </v-col>
              <v-col cols="12">
                <div class="d-flex align-center gap-2">
                  <v-file-input label="Company Logo" variant="outlined" density="compact" hide-details accept="image/*" prepend-icon="mdi-image" class="flex-grow-1" @update:model-value="onLogoUpload" />
                  <v-btn v-if="logoDataUrl" icon="mdi-image-remove" size="small" variant="tonal" color="error" density="compact" title="Remove Logo" @click="logoDataUrl = ''" />
                </div>
              </v-col>
              <v-col cols="12"><v-text-field v-model="companyName" label="Company Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-textarea v-model="companyLocation" label="Company Address" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
              <v-col cols="6"><v-text-field v-model="companyPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="companyWebsite" label="Website" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-text-field v-model="companyEmail" label="Contact Email" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- INVOICE DETAILS -->
          <template v-if="sections[1].open">
            <div class="section-label">Invoice Details</div>
            <v-row dense align="center">
              <v-col cols="6">
                <v-select v-model="currency" :items="['Dollar (USD)', 'Euro (EUR)', 'GBP', 'MYR', 'HKD', 'China Yuan (CNY)']" label="Currency" variant="outlined" density="compact" hide-details :disabled="currencyLocked" />
              </v-col>
              <v-col v-if="currency === 'China Yuan (CNY)'" cols="6">
                <v-text-field v-model.number="exchangeRate" label="Exchange Rate" variant="outlined" density="compact" hide-details type="number" step="0.0001" />
              </v-col>
              <!-- Tax %, Tax amount and the final total stay in sync: type any one and
                   the other two follow. Tax applies to the items after discount. -->
              <v-col cols="4">
                <v-text-field v-model.number="taxPercent" label="Tax %" variant="outlined" density="compact" hide-details type="number" step="0.01" suffix="%" prepend-inner-icon="mdi-percent-outline" @update:model-value="onTaxPercentInput" />
              </v-col>
              <v-col cols="4">
                <v-text-field v-model.number="taxAmount" label="Tax" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" @update:model-value="onTaxAmountInput" />
              </v-col>
              <v-col cols="4">
                <v-text-field v-model.number="otherAmount" label="Other" variant="outlined" density="compact" hide-details type="number" :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'" />
              </v-col>
              <v-col cols="12">
                <v-text-field
                  :model-value="targetTotalInput"
                  label="Final Total (override — derives the tax %)"
                  variant="outlined"
                  density="compact"
                  type="number"
                  :prefix="currency === 'China Yuan (CNY)' ? '¥' : '$'"
                  persistent-hint
                  :hint="`Current total: ${currency === 'China Yuan (CNY)' ? '¥' : '$'}${formatPrice(grandTotalDisplay)}`"
                  @update:model-value="onTargetTotalInput"
                />
              </v-col>
              <v-col cols="12"><v-textarea v-model="comments" label="Comments" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
              <v-col cols="12"><v-textarea v-model="companyTerms" label="Terms & Conditions" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
              <v-col cols="12"><v-text-field v-model="footerText" label="Footer Text" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- BILL TO / SHIP TO -->
          <template v-if="sections[2].open">
            <div class="section-label">Bill To</div>
            <v-row dense align="center">
              <v-col v-if="customerContacts.length" cols="12">
                <v-select
                  v-model="selectedCustomerContact"
                  :items="customerContacts"
                  item-title="name"
                  item-value="email"
                  label="Select Contact Person"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  prepend-inner-icon="mdi-account-outline"
                  @update:model-value="applyCustomerContact"
                />
              </v-col>
              <v-col cols="12"><v-text-field v-model="billToName" label="Bill To Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-textarea v-model="billTo" label="Bill To Address" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
              <v-col cols="12"><v-text-field v-model="billToContactPerson" label="Contact Person" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="billToEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="billToPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <div class="section-label mt-3">Ship To</div>
            <v-row dense align="center">
              <v-col cols="12"><v-text-field v-model="shipToName" label="Ship To Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-textarea v-model="shipTo" label="Ship To Address" variant="outlined" density="compact" hide-details rows="2" auto-grow /></v-col>
              <v-col cols="12"><v-text-field v-model="shipToContactPerson" label="Contact Person" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="shipToEmail" label="Email" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="shipToPhone" label="Phone" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- BANK DETAILS -->
          <template v-if="sections[3].open">
            <div class="section-label">Bank Details</div>
            <v-row dense align="center">
              <v-col cols="12">
                <v-select
                  v-model="selectedBankAccountId"
                  :items="presetBankAccounts"
                  item-title="accountName"
                  item-value="id"
                  label="Select Bank Account"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  prepend-inner-icon="mdi-bank-outline"
                  :no-data-text="presetBankAccounts.length ? 'No accounts' : 'Select a company preset first'"
                  @update:model-value="applyBankAccount"
                />
              </v-col>
              <v-col cols="12"><v-text-field v-model="beneficiaryName" label="Beneficiary Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-text-field v-model="beneficiaryAddress" label="Beneficiary Address" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-text-field v-model="bankName" label="Bank Name" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="12"><v-text-field v-model="bankAddress" label="Bank Address" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="bankAccount" label="Bank Account" variant="outlined" density="compact" hide-details /></v-col>
              <v-col cols="6"><v-text-field v-model="swiftCode" label="Swift Code" variant="outlined" density="compact" hide-details /></v-col>
            </v-row>
            <v-divider class="my-3" />
          </template>

          <!-- ITEMS (pick which parts appear in the PDF / packing list) -->
          <template v-if="sections[4].open">
            <div class="d-flex align-center mb-2 gap-2">
              <span class="section-label mb-0">Items</span>
              <v-spacer />
              <v-btn size="x-small" variant="tonal" @click="selectAllItems">Select All</v-btn>
              <v-btn size="x-small" variant="tonal" color="error" @click="selectedItems = []">Clear</v-btn>
            </div>
            <div class="d-flex flex-column gap-1">
              <v-checkbox
                v-for="it in itemRows"
                :key="it._key"
                v-model="selectedItems"
                :value="it._key"
                :label="`${it.alt || it.partNumber || '—'} × ${it.qty}`"
                density="compact"
                hide-details
                color="primary"
              />
            </div>
            <div v-if="!itemRows.length" class="text-caption text-medium-emphasis">No items on this invoice.</div>
            <v-divider class="my-3" />
          </template>

        </div>

        <!-- ── Right panel: PDF preview ── -->
        <div class="flex-grow-1 overflow-y-auto d-flex justify-center pa-6" style="background: rgb(var(--v-theme-surface-variant));">
          <div v-if="loadingData" class="d-flex justify-center align-center" style="width:210mm;">
            <v-progress-circular indeterminate color="primary" size="48" />
          </div>
          <div v-else ref="pdfContent" class="pdf-page" v-html="renderedHtml" />
        </div>

      </div>
    </v-card>

    <!-- ── Packing List: Packages Dialog ── -->
    <v-dialog v-model="showPackingDialog" max-width="600" persistent scrollable>
      <v-card>
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-package-variant" class="mr-2" color="info" />
          Packing List — Shipping Details
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showPackingDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text style="max-height: 60vh;">
          <p class="text-caption text-medium-emphasis mb-4">
            Add one or more packages with their total weight and dimensions. These will appear in the Shipping Details section of the packing list.
          </p>

          <v-alert
            type="info"
            variant="tonal"
            density="compact"
            class="mb-4 text-caption"
            :text="`Includes ${selectedItemsList.length} of ${itemRows.length} item(s) — same selection as the invoice PDF.`"
          />

          <div v-for="(pkg, i) in packages" :key="i" class="d-flex align-center gap-2 mb-2">
            <span class="text-caption text-medium-emphasis" style="min-width:24px;">{{ i + 1 }}.</span>
            <v-text-field
              v-model="pkg.weight"
              density="compact"
              variant="outlined"
              hide-details
              label="Weight"
              placeholder="e.g. 5 kg"
              style="flex:1;"
            />
            <v-text-field
              v-model="pkg.dimensions"
              density="compact"
              variant="outlined"
              hide-details
              label="Dimensions"
              placeholder="e.g. 40×30×20 cm"
              style="flex:1.5;"
            />
            <v-btn
              icon="mdi-close"
              size="x-small"
              variant="text"
              color="error"
              :disabled="packages.length === 1"
              @click="packages.splice(i, 1)"
            />
          </div>

          <v-btn
            prepend-icon="mdi-plus"
            variant="tonal"
            size="small"
            color="primary"
            class="mt-2"
            @click="packages.push({ weight: '', dimensions: '' })"
          >
            Add Package
          </v-btn>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showPackingDialog = false">Cancel</v-btn>
          <v-btn variant="tonal" color="info" prepend-icon="mdi-download" :loading="generatingPacking" :disabled="!selectedItemsList.length" @click="downloadPackingList">Download Packing List</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{ invoiceId: number | string }>()
const model = defineModel<boolean>({ default: false })
const api = useApi()

// ── Section toggle chips ──
const sections = reactive([
  { key: 'company',  label: 'Company',         open: true },
  { key: 'details',  label: 'Invoice Details',  open: true },
  { key: 'addresses',label: 'Bill To / Ship To',open: true },
  { key: 'bank',     label: 'Bank Details',     open: true },
  { key: 'items',    label: 'Items',            open: true },
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

watch(selectedPreset, async (val) => {
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
    companyTerms.value = pdfData.value?.customerTermsAndConditions || preset.termsAndConditions || ''
    // Load bank accounts for this preset; try to restore saved selection from PI
    presetBankAccounts.value = preset.bankAccounts || []
    const savedId = pdfData.value?.defaultBankAccountId ?? null
    const savedInThisPreset = savedId && presetBankAccounts.value.some((b: any) => b.id === savedId)
    if (savedInThisPreset) {
      selectedBankAccountId.value = savedId
      applyBankAccount(savedId)
    } else if (presetBankAccounts.value.length === 1) {
      selectedBankAccountId.value = presetBankAccounts.value[0].id
      applyBankAccount(selectedBankAccountId.value)
    } else {
      selectedBankAccountId.value = null
      beneficiaryName.value = ''
      bankName.value = ''
      bankAddress.value = ''
      bankAccount.value = ''
      swiftCode.value = ''
    }
    // DISABLED: wallet bank override — bank details now come from company preset bank accounts
    // const walletId = pdfData.value?.defaultDepositWalletId
    // if (walletId) {
    //   try {
    //     const wallet = await api.get<any>(`/payment-boxes/${walletId}`)
    //     if (wallet.bankName) bankName.value = wallet.bankName
    //     if (wallet.bankAddress) bankAddress.value = wallet.bankAddress
    //     if (wallet.accountNumber) bankAccount.value = wallet.accountNumber
    //     if (wallet.beneficiaryName) beneficiaryName.value = wallet.beneficiaryName
    //     if (wallet.swiftCode) swiftCode.value = wallet.swiftCode
    //   } catch { /* non-critical */ }
    // }
  }
})

const companyName     = ref('Your Company Name')
const companyLocation = ref('')
const companyPhone    = ref('')
const companyWebsite  = ref('')
const companyEmail    = ref('')
const footerText      = ref('If you have any questions about this invoice, please contact')
const logoDataUrl     = ref('')
const generating      = ref(false)
const generatingPacking = ref(false)
const loadingData     = ref(false)
const pdfData         = ref<any>({})
const taxAmount       = ref(0)
const otherAmount     = ref(0)
const currency        = ref('Dollar (USD)')
const exchangeRate    = ref(7.0)
const comments        = ref('No Comments')
const billToName      = ref('')
const shipToName      = ref('')
const contactPerson   = ref('')
const billTo          = ref('')
const shipTo          = ref('')
const companyTerms    = ref('')

const billToContactPerson = ref('')
const billToEmail         = ref('')
const billToPhone         = ref('')
const shipToContactPerson = ref('')
const shipToEmail         = ref('')
const shipToPhone         = ref('')

// Customer contact persons (parsed from customerContacts JSON from API)
const customerContacts = ref<{name: string, email: string, phone?: string, title?: string}[]>([])
const selectedCustomerContact = ref<string | null>(null)

function applyCustomerContact(email: string | null) {
  if (!email) return
  const c = customerContacts.value.find(x => x.email === email)
  if (c) {
    billToContactPerson.value = c.name
    shipToContactPerson.value = c.name
    billToEmail.value = c.email
    shipToEmail.value = c.email
    if (c.phone) { billToPhone.value = c.phone; shipToPhone.value = c.phone }
  }
}
const beneficiaryName     = ref('')
const beneficiaryAddress  = ref('')
const bankName            = ref('')
const bankAddress         = ref('')
const bankAccount         = ref('')
const swiftCode           = ref('')

// ── Preset bank accounts ──────────────────────────────────────────────────────
const presetBankAccounts    = ref<any[]>([])
const selectedBankAccountId = ref<number | null>(null)

function applyBankAccount(id: number | null) {
  const ba = presetBankAccounts.value.find(b => b.id === id)
  if (ba) {
    beneficiaryName.value = ba.beneficiaryName || ''
    bankName.value        = ba.bankName || ''
    bankAddress.value     = ba.bankAddress || ''
    bankAccount.value     = ba.accountNumber || ''
    swiftCode.value       = ba.swiftCode || ''
  } else {
    beneficiaryName.value = ''
    bankName.value        = ''
    bankAddress.value     = ''
    bankAccount.value     = ''
    swiftCode.value       = ''
  }
}

// ── Item selection (drives the invoice PDF, its totals, and the packing list) ──
const selectedItems = ref<(number | string)[]>([])

// Stable key per item: the DB id when present, otherwise the position in the list.
const itemRows = computed<any[]>(() =>
  (pdfData.value?.items || []).map((it: any, i: number) => ({ ...it, _key: it.id ?? i })))

const selectedItemsList = computed<any[]>(() =>
  itemRows.value.filter(it => selectedItems.value.includes(it._key)))

function selectAllItems() {
  selectedItems.value = itemRows.value.map(it => it._key)
}

// Subtotal follows the selection; falls back to the stored total when the
// invoice carries no line items.
const subtotalBase = computed(() => {
  if (!itemRows.value.length) return Number(pdfData.value?.totalAmount) || 0
  return selectedItemsList.value.reduce((sum: number, it: any) => sum + (Number(it.totalPrice) || 0), 0)
})

// ── Tax: percentage ⇄ amount ⇄ final total ──────────────────────────────────
// The tax base is the goods after discount; shipping and "Other" sit on top of it.
// Amounts are held in the invoice's base currency and scaled for display, so the
// percentage itself is currency-independent.
const totalDiscountBase = computed(() =>
  selectedItemsList.value.reduce((sum: number, it: any) => sum + (Number(it.discount) || 0), 0))
const taxableBase = computed(() => Math.max(0, subtotalBase.value - totalDiscountBase.value))
const displayRate = computed(() => currency.value === 'China Yuan (CNY)' ? (exchangeRate.value || 1) : 1)
const grandTotalDisplay = computed(() =>
  (taxableBase.value
    + (Number(pdfData.value?.shippingCost) || 0)
    + (Number(taxAmount.value) || 0)
    + (Number(otherAmount.value) || 0)) * displayRate.value)

const taxPercent = ref(0)
const targetTotalInput = ref<number | null>(null)
const round2 = (n: number) => Math.round(n * 100) / 100

/** Tax % typed → tax amount follows. */
function onTaxPercentInput() {
  taxAmount.value = round2(taxableBase.value * (Number(taxPercent.value) || 0) / 100)
  targetTotalInput.value = null
}

/** Tax amount typed → the matching rate is shown. */
function onTaxAmountInput() {
  taxPercent.value = taxableBase.value
    ? round2((Number(taxAmount.value) || 0) / taxableBase.value * 100)
    : 0
  targetTotalInput.value = null
}

/** Final price typed (in the displayed currency) → back out the tax and its rate. */
function onTargetTotalInput(val: string | number | null) {
  const target = Number(val)
  if (val === '' || val === null || Number.isNaN(target)) { targetTotalInput.value = null; return }
  targetTotalInput.value = target

  const rest = taxableBase.value
    + (Number(pdfData.value?.shippingCost) || 0)
    + (Number(otherAmount.value) || 0)
  taxAmount.value = round2(Math.max(0, target / (displayRate.value || 1) - rest))
  taxPercent.value = taxableBase.value ? round2(taxAmount.value / taxableBase.value * 100) : 0
}

// Selecting/deselecting items moves the base — keep the amount true to the rate.
watch(taxableBase, () => { if (taxPercent.value) onTaxPercentInput() })

// ── Packing List dialog ──
const showPackingDialog = ref(false)
interface PackageEntry { weight: string; dimensions: string }
const packages = ref<PackageEntry[]>([{ weight: '', dimensions: '' }])

function openPackingListDialog() {
  // Reset to a single empty package row each time
  packages.value = [{ weight: '', dimensions: '' }]
  showPackingDialog.value = true
}

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
        selectAllItems()
        if (data.notes) comments.value = data.notes
        if (data.customerBase == 3) {
          const currencyType = data.customerCurrencyType || 'Dollar'
          const storedRate = (data.coefYuan ?? 1) * (data.exchangeRateYuan ?? 7)
          if (currencyType === 'Dollar') {
            currency.value = 'Dollar (USD)'
            exchangeRate.value = 1
          } else if (currencyType === 'Yuan') {
            currency.value = 'China Yuan (CNY)'
            exchangeRate.value = storedRate
          } else if (currencyType === 'Both') {
            currency.value = 'Dollar (USD)'
            exchangeRate.value = storedRate
          }
        }
        customerContacts.value = []
        selectedCustomerContact.value = null
        if (data.customerContacts) {
          try { customerContacts.value = JSON.parse(data.customerContacts) } catch {}
        }
        billToName.value           = data.customerName || ''
        shipToName.value           = data.customerName || ''
        contactPerson.value        = data.customerContactPerson || ''
        billTo.value               = data.customerBillTo || ''
        shipTo.value               = data.customerShipTo || data.customerBillTo || ''
        billToContactPerson.value  = data.customerContactPerson || ''
        billToEmail.value          = data.customerBillToEmail || ''
        billToPhone.value          = data.customerBillToPhone || ''
        shipToContactPerson.value  = data.customerContactPerson || ''
        shipToEmail.value          = data.customerShipToEmail || ''
        shipToPhone.value          = data.customerShipToPhone || ''
        if (data.customerTermsAndConditions) {
          companyTerms.value = data.customerTermsAndConditions
        }
      } catch (e) { console.error('[FinalInvoicePdf] Failed to load data', e) }
      finally { loadingData.value = false }
    }
  }
})

const fmt = (n: number) => formatPrice(n)

const currencyLocked = computed(() => {
  if (pdfData.value.customerBase !== 3) return false
  const currencyType = pdfData.value.customerCurrencyType || 'Dollar'
  return currencyType !== 'Both'
})

// Which of the three PDF templates to render — mirrored by the live preview.
const pdfTemplate = ref<PdfTemplateKey>('modern')

/** Template-neutral model — mirrors PdfDocModelBuilders.FromFinalInvoice on the server. */
function buildPreviewModel(): PdfPreviewModel {
  const d = pdfData.value
  const items: any[] = selectedItemsList.value
  const isYuan = currency.value === 'China Yuan (CNY)'
  const sym = isYuan ? '¥' : '$'
  const rate = isYuan ? (exchangeRate.value || 1) : 1
  const money = (n: number) => `${sym}${fmt(n)}`

  const subtotal = subtotalBase.value * rate
  const totalDiscount = items.reduce((s: number, it: any) => s + (Number(it.discount) || 0), 0) * rate
  const tax = (taxAmount.value || 0) * rate
  const shipping = (Number(d.shippingCost) || 0) * rate
  const fee = (otherAmount.value || 0) * rate

  return {
    docTitle: 'Invoice',
    docNumber: d.invoiceNumber,
    logoDataUrl: logoDataUrl.value,
    companyName: companyName.value,
    companyLocation: companyLocation.value,
    companyPhone: companyPhone.value,
    companyWebsite: companyWebsite.value,
    companyEmail: companyEmail.value,
    primary: theme.value.primary,
    accent: theme.value.accent,
    meta: [
      { label: 'Date', value: d.createdAt ? new Date(d.createdAt).toLocaleDateString() : '' },
      { label: 'Customer PO', value: d.customerPONumber },
      { label: 'PI Ref', value: d.proformaInvoiceNumber },
      { label: 'Currency', value: currency.value },
    ],
    addresses: [
      {
        title: 'Bill To',
        name: billToName.value || d.customerName,
        address: billTo.value || d.customerBillTo,
        fields: [
          { label: 'Contact Person', value: billToContactPerson.value || d.customerContactPerson },
          { label: 'Email', value: billToEmail.value || d.customerBillToEmail },
          { label: 'Phone', value: billToPhone.value || d.customerBillToPhone },
        ],
      },
      {
        title: 'Ship To',
        name: shipToName.value || d.customerName,
        address: shipTo.value || d.customerShipTo || d.customerBillTo,
        fields: [
          { label: 'Contact Person', value: shipToContactPerson.value || d.customerContactPerson },
          { label: 'Email', value: shipToEmail.value || d.customerShipToEmail },
          { label: 'Phone', value: shipToPhone.value || d.customerShipToPhone },
          { label: 'Account', value: d.customerShipToAccount },
        ],
      },
    ],
    columns: [
      { header: '#', width: 18 },
      { header: 'Part No.', relative: 1.6 },
      { header: 'Description', relative: 1.8, align: 'left' },
      { header: 'Qty', width: 24 },
      { header: 'CD', width: 24 },
      { header: 'Cert', width: 46 },
      { header: 'Unit Price', width: 50, align: 'right' },
      { header: 'Total', width: 54, align: 'right' },
      { header: 'Discount', width: 50, align: 'right' },
      { header: 'Track #', relative: 1.1 },
      { header: 'Carrier', relative: 1.1 },
    ],
    rows: items.map((it: any, i: number) => ({
      cells: [
        { text: String(i + 1) },
        {
          text: it.alt || it.partNumber,
          subText: it.alt ? `(Alt to: ${it.partNumber || '—'})` : null,
          highlight: !!it.alt,
          bold: true,
        },
        { text: it.description },
        { text: String(it.qty), bold: true },
        { text: it.condition },
        { text: it.certification },
        { text: money((Number(it.unitPrice) || 0) * rate) },
        { text: money((Number(it.totalPrice) || 0) * rate), bold: true },
        it.discount > 0
          ? { text: `-${money(Number(it.discount) * rate)}`, negative: true }
          : { text: '—' },
        { text: it.trackNumber },
        { text: it.carrier },
      ],
    })),
    infoBlocks: [{
      title: 'Bank Information',
      fields: [
        { label: 'Beneficiary Name', value: beneficiaryName.value },
        { label: 'Beneficiary Address', value: beneficiaryAddress.value },
        { label: 'Bank Name', value: bankName.value },
        { label: 'Bank Address', value: bankAddress.value },
        { label: 'Account Number', value: bankAccount.value },
        { label: 'SWIFT Code', value: swiftCode.value },
      ],
    }],
    totals: [
      { label: 'Subtotal', amount: money(subtotal) },
      ...(totalDiscount > 0 ? [{ label: 'Discount', amount: money(totalDiscount), isNegative: true }] : []),
      { label: taxPercent.value > 0 ? `Tax (${taxPercent.value}%)` : 'Tax', amount: money(tax) },
      { label: 'Shipping', amount: money(shipping) },
      { label: 'Processing Fee', amount: money(fee) },
      { label: 'Total', amount: money(subtotal - totalDiscount + tax + shipping + fee), isGrand: true },
    ],
    comments: comments.value,
    terms: companyTerms.value,
    footerText: footerText.value,
    showSignature: true,
  }
}

const renderedHtml = computed(() => {
  const d = pdfData.value
  if (!d.invoiceNumber) return ''

  if (pdfTemplate.value === 'classic') return renderClassicPreview(buildPreviewModel())
  if (pdfTemplate.value === 'standard') return renderStandardPreview(buildPreviewModel())

  const items: any[] = selectedItemsList.value
  const logo = logoDataUrl.value
  const logoImg = logo ? `<img src="${logo}" style="max-height:48px; max-width:160px; object-fit:contain;" />` : ''

  const invDate = d.createdAt ? new Date(d.createdAt).toLocaleDateString() : '—'
  const dueDate = d.dueDate ? new Date(d.dueDate).toLocaleDateString() : '—'
  const isYuan = currency.value === 'China Yuan (CNY)'
  const sym = isYuan ? '¥' : '$'
  const rate = isYuan ? (exchangeRate.value || 1) : 1
  const subtotal = subtotalBase.value * rate
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
      <td style="padding:9px 8px; font-size:11px; color:#6b7280; text-align:center; border-bottom:1px solid #eef0f3;">${i + 1}</td>
      <td style="padding:9px 10px; font-size:11px; font-weight:600; color:#1a2744; border-bottom:1px solid #eef0f3;">${it.alt || it.partNumber || '—'}${it.alt ? `<div style="font-size:9px; font-weight:400; color:#9ca3af;">(Alt to: ${it.partNumber || '—'})</div>` : ''}</td>
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
          <div style="font-size:12px; font-weight:700; color:#1a2744; margin-bottom:3px;">${billToName.value || '—'}</div>
          ${billToContactPerson.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Contact: ${billToContactPerson.value}</div>` : ''}
          ${(billTo.value || d.customerBillTo) ? `<div style="font-size:10.5px; color:#4b5563; line-height:1.5; white-space:pre-wrap;">${billTo.value || d.customerBillTo}</div>` : ''}
          ${billToEmail.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Email: ${billToEmail.value}</div>` : ''}
          ${billToPhone.value ? `<div style="font-size:10.5px; color:#4b5563; margin-bottom:2px;">Phone: ${billToPhone.value}</div>` : ''}
        </div>
        <div style="flex:1; padding:16px 20px;">
          <div style="font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:1.5px; color:#6b7280; margin-bottom:8px;">Ship To</div>
          <div style="font-size:12px; font-weight:700; color:#1a2744; margin-bottom:3px;">${shipToName.value || '—'}</div>
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
              <th style="padding:10px 12px; font-size:9px; font-weight:600; color:#fff; text-transform:uppercase; letter-spacing:0.8px; text-align:center; width:36px;">#</th>
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

      <!-- ═══ Totals + Bank Details ═══ -->
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
            <tr style="background:#f7f8fa;"><td style="padding:8px 14px; color:#4b5563; border-bottom:1px solid #eef0f3;">Tax${taxPercent.value > 0 ? ` (${taxPercent.value}%)` : ''}</td><td style="padding:8px 14px; text-align:right; font-weight:600; border-bottom:1px solid #eef0f3;">${sym}${fmt(tax)}</td></tr>
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
    const authStore = useAuthStore()
    const d = pdfData.value
    const items: any[] = selectedItemsList.value

    const currencyType = pdfData.value.customerCurrencyType || 'Dollar'
    const isBoth = pdfData.value.customerBase === 3 && currencyType === 'Both'

    const yuanRate = (pdfData.value.coefYuan ?? 1) * (pdfData.value.exchangeRateYuan ?? 7)
    const currenciesToGenerate = isBoth
      ? [{ currency: 'Dollar (USD)', symbol: '$', rate: 1 }, { currency: 'China Yuan (CNY)', symbol: '¥', rate: yuanRate }]
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
        customerBillToName: billToName.value || null,
        customerShipToName: shipToName.value || null,
        customerContactPerson: contactPerson.value || d.customerContactPerson || null,
        customerBillTo: billTo.value || d.customerBillTo || null,
        customerBillToEmail: billToEmail.value || d.customerBillToEmail || null,
        customerBillToPhone: billToPhone.value || d.customerBillToPhone || null,
        customerBillToContactPerson: billToContactPerson.value || d.customerContactPerson || null,
        customerShipTo: shipTo.value || d.customerShipTo || d.customerBillTo || null,
        customerShipToContactPerson: shipToContactPerson.value || d.customerContactPerson || null,
        customerShipToEmail: shipToEmail.value || d.customerShipToEmail || null,
        customerShipToPhone: shipToPhone?.value || d?.customerShipToPhone || null,
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
          alt: it.alt || null,
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
        subtotal: subtotalBase.value * curr.rate,
        tax: (taxAmount.value || 0) * curr.rate,
        // Rate is currency-independent — printed as "Tax (13%)" on the PDF.
        taxPercent: taxPercent.value || null,
        other: (otherAmount.value || 0) * curr.rate,
        comments: comments.value || null,
        terms: companyTerms.value || null,
        footerText: footerText.value || null,
        template: pdfTemplate.value,
      }

      const response = await $fetch<Blob>(`${api.baseURL}/pdf/final-invoice`, {
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
    const authStore = useAuthStore()
    const d = pdfData.value

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
      proformaRef: d.proformaInvoiceNumber || null,
      customerName: d.customerName || '—',
      customerBillToName: billToName.value || null,
      customerShipToName: shipToName.value || null,
      customerContactPerson: contactPerson.value || d.customerContactPerson || null,
      customerBillTo: billTo.value || d.customerBillTo || null,
      customerBillToEmail: billToEmail.value || d.customerBillToEmail || null,
      customerBillToPhone: billToPhone.value || d.customerBillToPhone || null,
      customerBillToContactPerson: billToContactPerson.value || d.customerContactPerson || null,
      customerShipTo: shipTo.value || d.customerShipTo || d.customerBillTo || null,
      customerShipToContactPerson: shipToContactPerson.value || d.customerContactPerson || null,
      customerShipToEmail: shipToEmail.value || d.customerShipToEmail || null,
      customerShipToPhone: shipToPhone.value || d?.customerShipToPhone || null,
      customerShipToAccount: d.customerShipToAccount || null,
      // Mirrors the invoice PDF selection — no separate picker needed.
      items: selectedItemsList.value.map((it: any) => ({
        partNumber:    it.partNumber    || null,
        alt:           it.alt           || null,
        description:   it.description   || null,
        qty:           it.qty           || 0,
        condition:     it.condition     || null,
        certification: it.certification || null,
      })),
      packages: packages.value
        .filter((p: PackageEntry) => p.weight || p.dimensions)
        .map((p: PackageEntry) => ({ weight: p.weight || null, dimensions: p.dimensions || null })),
    }

    const response = await $fetch<Blob>(`${api.baseURL}/pdf/packing-list`, {
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
    showPackingDialog.value = false
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
.section-label {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: rgb(var(--v-theme-primary));
  margin-bottom: 8px;
}
</style>
