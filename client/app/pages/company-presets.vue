<template>
  <div>
    <div class="d-flex align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Company Presets</h1>
        <p class="text-caption text-medium-emphasis mt-1">Manage company profiles used in PDF quotations</p>
      </div>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">Add Company</v-btn>
    </div>

    <!-- Preset Cards -->
    <v-row v-if="!loading">
      <v-col v-for="preset in presets" :key="preset.id" cols="12" md="6" lg="4">
        <v-card class="preset-card h-100" :class="{ 'border-primary': editingId === preset.id }">
          <v-card-text class="pa-4">
            <!-- Logo + Name -->
            <div class="d-flex align-start gap-3 mb-3">
              <div class="logo-slot flex-shrink-0">
                <img v-if="preset.logoBase64" :src="`data:${preset.logoMimeType};base64,${preset.logoBase64}`" class="preset-logo" />
                <v-icon v-else icon="mdi-domain" size="40" color="primary" />
              </div>
              <div class="min-width-0">
                <div class="text-subtitle-1 font-weight-bold text-truncate">{{ preset.name }}</div>
                <div class="d-flex align-center gap-1 mt-1">
                  <div :style="`width:12px;height:12px;border-radius:3px;background:${preset.primaryColor || '#1a2744'};border:1px solid rgba(0,0,0,.15);`" :title="'Primary: ' + (preset.primaryColor || '#1a2744')" />
                  <div :style="`width:12px;height:12px;border-radius:3px;background:${preset.accentColor || '#2563eb'};border:1px solid rgba(0,0,0,.15);`" :title="'Accent: ' + (preset.accentColor || '#2563eb')" />
                  <span class="text-caption text-medium-emphasis ml-1">Sort: {{ preset.sortOrder }}</span>
                  <v-chip v-if="preset.customPdfHtml" size="x-small" color="secondary" variant="tonal" class="ml-1" prepend-icon="mdi-code-braces">Custom</v-chip>
                </div>
              </div>
              <v-spacer />
              <div class="d-flex gap-1">
                <v-btn icon="mdi-code-braces" size="x-small" variant="tonal" color="secondary" :title="'Custom PDF Template'" @click="openHtmlEditor(preset)" />
                <v-btn icon="mdi-pencil" size="x-small" variant="tonal" color="primary" @click="openEdit(preset)" />
                <v-btn icon="mdi-delete" size="x-small" variant="tonal" color="error" @click="confirmDelete(preset)" />
              </div>
            </div>

            <v-divider class="mb-2" />

            <div class="detail-row"><v-icon icon="mdi-map-marker" size="14" class="mr-1" />{{ preset.location || '—' }}</div>
            <div class="detail-row" title="Ship-To Address"><v-icon icon="mdi-truck-delivery-outline" size="14" class="mr-1" />{{ preset.shipToAddress || '—' }}</div>
            <div class="detail-row" title="Ship-To Phone"><v-icon icon="mdi-phone-outline" size="14" class="mr-1" />{{ preset.shipToPhone || '—' }}</div>
            <div class="detail-row" title="FedEx Account"><v-icon icon="mdi-fedex" size="14" class="mr-1" />{{ preset.fedexAccount || '—' }}</div>
            <div class="detail-row"><v-icon icon="mdi-phone" size="14" class="mr-1" />{{ preset.phone || '—' }}</div>
            <div class="detail-row"><v-icon icon="mdi-web" size="14" class="mr-1" />{{ preset.website || '—' }}</div>
            <div class="detail-row"><v-icon icon="mdi-email" size="14" class="mr-1" />{{ preset.email || '—' }}</div>

            <!-- Bank Accounts Summary -->
            <div v-if="preset.bankAccounts && preset.bankAccounts.length" class="mt-2 pa-2 bg-surface-variant rounded-sm border">
              <div class="text-caption font-weight-bold text-primary mb-1">BANK ACCOUNTS ({{ preset.bankAccounts.length }})</div>
              <div v-for="ba in preset.bankAccounts" :key="ba.id" class="detail-row">
                <v-icon icon="mdi-bank-outline" size="12" class="mr-1 flex-shrink-0" />
                <span class="font-weight-medium">{{ ba.accountName }}</span>
                <span v-if="ba.bankName" class="text-medium-emphasis ml-1">· {{ ba.bankName }}</span>
              </div>
            </div>

            <div v-if="preset.termsAndConditions" class="mt-2">
              <div class="text-caption font-weight-bold text-medium-emphasis mb-1">TERMS & CONDITIONS</div>
              <pre class="terms-preview">{{ preset.termsAndConditions }}</pre>
            </div>
          </v-card-text>
        </v-card>
      </v-col>

      <v-col v-if="presets.length === 0" cols="12" class="text-center pa-12">
        <v-icon icon="mdi-domain-off" size="64" color="grey" class="mb-3" />
        <p class="text-body-1 text-medium-emphasis">No company presets yet. Click "Add Company" to create one.</p>
      </v-col>
    </v-row>

    <div v-else class="d-flex justify-center pa-12">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <!-- Create / Edit Dialog -->
    <v-dialog v-model="showDialog" max-width="720" scrollable>
      <v-card>
        <v-toolbar color="surface" density="compact">
          <v-toolbar-title class="text-body-1 font-weight-bold">
            {{ editingId ? 'Edit Company Preset' : 'Add Company Preset' }}
          </v-toolbar-title>
          <v-spacer />
          <v-btn icon="mdi-close" @click="showDialog = false" />
        </v-toolbar>

        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="12">
              <v-text-field
                v-model="form.name"
                label="Company Name *"
                variant="outlined"
                density="compact"
                hide-details="auto"
                :rules="[v => !!v || 'Required']"
              />
            </v-col>
            <v-col cols="12">
              <v-text-field
                v-model="form.location"
                label="Address / Location"
                variant="outlined"
                density="compact"
                hide-details
              />
            </v-col>
            <v-col cols="12">
              <v-text-field
                v-model="form.shipToAddress"
                label="Ship To Address"
                variant="outlined"
                density="compact"
                hide-details
                prepend-inner-icon="mdi-truck-delivery-outline"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.shipToPhone"
                label="Ship To Phone"
                variant="outlined"
                density="compact"
                hide-details
                prepend-inner-icon="mdi-phone-outline"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.fedexAccount"
                label="FedEx Account"
                variant="outlined"
                density="compact"
                hide-details
                prepend-inner-icon="mdi-fedex"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.phone"
                label="Phone"
                variant="outlined"
                density="compact"
                hide-details
                prepend-inner-icon="mdi-phone"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.email"
                label="Email"
                variant="outlined"
                density="compact"
                hide-details
                prepend-inner-icon="mdi-email"
              />
            </v-col>
            <v-col cols="12" md="8">
              <v-text-field
                v-model="form.website"
                label="Website"
                variant="outlined"
                density="compact"
                hide-details
                prepend-inner-icon="mdi-web"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="form.sortOrder"
                label="Sort Order"
                type="number"
                variant="outlined"
                density="compact"
                hide-details
                min="0"
              />
            </v-col>

            <!-- Bank Accounts Manager -->
            <v-col cols="12">
              <div class="d-flex align-center mb-2">
                <span class="text-caption font-weight-bold text-medium-emphasis">BANK ACCOUNTS</span>
                <v-spacer />
                <v-btn size="x-small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="openBankAccountForm()">Add Account</v-btn>
              </div>

              <div v-if="bankAccounts.length === 0" class="text-caption text-medium-emphasis pa-3 text-center border rounded">
                No bank accounts yet. Click "Add Account" to add one.
              </div>

              <v-list v-else density="compact" class="border rounded pa-0">
                <v-list-item
                  v-for="(ba, idx) in bankAccounts"
                  :key="ba.id ?? `new-${idx}`"
                  :class="{ 'border-b': idx < bankAccounts.length - 1 }"
                  class="px-3 py-2"
                >
                  <template #prepend>
                    <v-icon icon="mdi-bank-outline" size="18" color="primary" class="mr-2" />
                  </template>
                  <v-list-item-title class="text-body-2 font-weight-medium">{{ ba.accountName }}</v-list-item-title>
                  <v-list-item-subtitle class="text-caption">
                    <span v-if="ba.bankName">{{ ba.bankName }}</span>
                    <span v-if="ba.bankName && ba.accountNumber"> · </span>
                    <span v-if="ba.accountNumber">{{ ba.accountNumber }}</span>
                    <span v-if="ba.swiftCode" class="ml-1 text-medium-emphasis">({{ ba.swiftCode }})</span>
                  </v-list-item-subtitle>
                  <template #append>
                    <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openBankAccountForm(ba)" />
                    <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="removeBankAccount(ba, idx)" />
                  </template>
                </v-list-item>
              </v-list>
            </v-col>

            <v-col cols="12">
              <v-textarea
                v-model="form.termsAndConditions"
                label="Terms & Conditions"
                variant="outlined"
                density="compact"
                hide-details
                rows="5"
                auto-grow
                placeholder="PAYMENT METHOD: PREPAYMENT&#10;DELIVERY TERM: EXW VENDOR&#10;ALL QUOTES ARE VALID FOR 7 DAYS, SUBJECT TO PRIOR SALE."
              />
            </v-col>

            <!-- PDF Theme Colors -->
            <v-col cols="12">
              <div class="text-caption font-weight-bold text-medium-emphasis mb-2">PDF THEME COLORS</div>
              <div class="d-flex align-center gap-3 flex-wrap">
                <label class="d-flex align-center gap-2" style="cursor:pointer;">
                  <div :style="`width:32px;height:32px;border-radius:6px;background:${form.primaryColor};border:2px solid rgba(0,0,0,.12);flex-shrink:0;`" />
                  <div>
                    <div class="text-caption font-weight-medium">Primary Color</div>
                    <div class="text-caption text-medium-emphasis">Headers, titles, borders</div>
                  </div>
                  <input type="color" v-model="form.primaryColor" style="opacity:0;width:0;height:0;position:absolute;" />
                </label>
                <label class="d-flex align-center gap-2" style="cursor:pointer;">
                  <div :style="`width:32px;height:32px;border-radius:6px;background:${form.accentColor};border:2px solid rgba(0,0,0,.12);flex-shrink:0;`" />
                  <div>
                    <div class="text-caption font-weight-medium">Accent Color</div>
                    <div class="text-caption text-medium-emphasis">Section labels, gradient</div>
                  </div>
                  <input type="color" v-model="form.accentColor" style="opacity:0;width:0;height:0;position:absolute;" />
                </label>
                <div class="ml-2 d-flex flex-column" style="gap:2px;">
                  <v-text-field v-model="form.primaryColor" label="Primary Hex" variant="outlined" density="compact" hide-details style="max-width:120px;" />
                  <v-text-field v-model="form.accentColor" label="Accent Hex" variant="outlined" density="compact" hide-details style="max-width:120px;" />
                </div>
                <!-- Live PDF preview mini -->
                <div :style="`border-radius:8px;overflow:hidden;border:1px solid #e5e7eb;min-width:160px;font-family:sans-serif;font-size:10px;`">
                  <div :style="`background:${form.primaryColor};color:#fff;padding:6px 10px;font-weight:700;letter-spacing:1px;`">QUOTATION</div>
                  <div :style="`height:3px;background:linear-gradient(90deg,${form.primaryColor} 0%,${form.accentColor} 50%,#e5e7eb 100%);`" />
                  <div style="padding:6px 10px;background:#fff;">
                    <div :style="`color:${form.primaryColor};font-weight:700;font-size:9px;`">Part Number</div>
                    <div style="font-size:8px;color:#6b7280;">$1,234.00</div>
                  </div>
                </div>
              </div>
            </v-col>

            <!-- Logo Upload -->
            <v-col cols="12">
              <div class="logo-upload-area">
                <div class="text-caption font-weight-bold text-medium-emphasis mb-2">COMPANY LOGO</div>
                <div class="d-flex align-center gap-4">
                  <div class="logo-preview-box">
                    <img v-if="logoPreview" :src="logoPreview" class="logo-preview-img" />
                    <v-icon v-else icon="mdi-image-plus" size="40" color="grey" />
                  </div>
                  <div class="flex-grow-1">
                    <v-file-input
                      label="Upload Logo (PNG, JPG, SVG)"
                      variant="outlined"
                      density="compact"
                      hide-details
                      accept="image/*"
                      prepend-icon=""
                      prepend-inner-icon="mdi-image"
                      @update:model-value="onLogoUpload"
                    />
                    <div class="text-caption text-medium-emphasis mt-1">Recommended: transparent PNG, max 500KB</div>
                    <v-btn
                      v-if="logoPreview"
                      size="x-small"
                      variant="text"
                      color="error"
                      class="mt-1"
                      prepend-icon="mdi-delete"
                      @click="clearLogo"
                    >
                      Remove Logo
                    </v-btn>
                  </div>
                </div>
              </div>
            </v-col>
            <!-- Linked Warehouses for Ship To -->
            <v-col cols="12">
              <div class="text-caption font-weight-bold text-medium-emphasis mb-2">LINKED WAREHOUSES (Ship To in PDF)</div>
              <v-autocomplete
                v-model="selectedWarehouseIds"
                :items="allWarehouses"
                item-title="name"
                item-value="id"
                label="Warehouses"
                variant="outlined"
                density="compact"
                hide-details
                multiple
                chips
                closable-chips
                prepend-inner-icon="mdi-home-city-outline"
                placeholder="Select warehouses that can be used as Ship To"
              />
            </v-col>
          </v-row>
        </v-card-text>

        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" @click="savePreset">
            {{ editingId ? 'Update' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Custom HTML Template Editor -->
    <v-dialog v-model="showHtmlEditor" fullscreen transition="dialog-bottom-transition">
      <v-card style="display:flex;flex-direction:column;height:100%;">
        <v-toolbar color="surface" border="b">
          <v-toolbar-title>
            <v-icon icon="mdi-code-braces" class="mr-2" />
            Custom PDF Template — <strong>{{ htmlEditorPreset?.name }}</strong>
          </v-toolbar-title>
          <template #append>
            <v-btn
              v-if="htmlEditorPreset?.customPdfHtml"
              variant="text"
              color="error"
              prepend-icon="mdi-delete"
              class="mr-2"
              @click="clearCustomHtml"
            >Clear Template</v-btn>
            <v-btn variant="text" prepend-icon="mdi-help-circle" class="mr-2" @click="showVarHints = !showVarHints">Variables</v-btn>
            <v-btn variant="text" @click="showHtmlEditor = false">Cancel</v-btn>
            <v-btn color="primary" variant="flat" class="ml-2" :loading="saving" @click="saveCustomHtml">Save Template</v-btn>
          </template>
        </v-toolbar>

        <!-- Variable hints panel -->
        <v-expand-transition>
          <div v-if="showVarHints" style="background:rgba(var(--v-theme-surface-variant),0.6);border-bottom:1px solid rgba(var(--v-border-color),0.2);padding:10px 16px;">
            <div class="text-caption font-weight-bold text-medium-emphasis mb-2">AVAILABLE TEMPLATE VARIABLES — click to insert at cursor</div>
            <div style="display:flex;flex-wrap:wrap;gap:6px;">
              <v-chip
                v-for="v in templateVars" :key="v.key"
                size="x-small" variant="tonal" color="primary" style="cursor:pointer;font-family:monospace;"
                :title="v.desc"
                @click="insertVar(v.key)"
              >{{ v.key }}</v-chip>
            </div>
          </div>
        </v-expand-transition>

        <!-- Split pane: editor | preview -->
        <div style="flex:1;display:flex;overflow:hidden;">
          <!-- Code editor -->
          <div style="flex:1;display:flex;flex-direction:column;min-width:0;border-right:1px solid rgba(var(--v-border-color),0.2);">
            <div style="padding:8px 16px;background:rgba(var(--v-theme-surface-variant),0.3);border-bottom:1px solid rgba(var(--v-border-color),0.1);display:flex;align-items:center;gap:8px;">
              <v-icon icon="mdi-xml" size="16" />
              <span class="text-caption font-weight-medium">HTML / CSS Editor</span>
              <v-spacer />
              <v-btn size="x-small" variant="text" prepend-icon="mdi-lightning-bolt" @click="loadDefaultTemplate">Load Default Template</v-btn>
            </div>
            <textarea
              ref="htmlEditorRef"
              v-model="editingHtml"
              spellcheck="false"
              style="flex:1;width:100%;resize:none;border:none;outline:none;padding:14px 16px;font-family:'Consolas','Fira Mono',monospace;font-size:13px;line-height:1.6;background:rgb(var(--v-theme-surface));color:rgb(var(--v-theme-on-surface));tab-size:2;"
              placeholder="Paste or write your custom HTML/CSS here...&#10;Use template variables like {{QUOTE_NUMBER}}, {{ITEMS_TABLE}}, etc."
              @keydown.tab.prevent="insertTab"
            />
          </div>

          <!-- Live Preview -->
          <div style="flex:1;display:flex;flex-direction:column;min-width:0;">
            <div style="padding:8px 16px;background:rgba(var(--v-theme-surface-variant),0.3);border-bottom:1px solid rgba(var(--v-border-color),0.1);display:flex;align-items:center;gap:8px;">
              <v-icon icon="mdi-eye" size="16" />
              <span class="text-caption font-weight-medium">Live Preview <span class="text-medium-emphasis">(with mock data)</span></span>
            </div>
            <iframe
              :srcdoc="previewHtml"
              sandbox="allow-same-origin"
              style="flex:1;border:none;background:#fff;"
            />
          </div>
        </div>
      </v-card>
    </v-dialog>

    <!-- Bank Account Form Dialog -->
    <v-dialog v-model="showBankForm" max-width="480">
      <v-card>
        <v-toolbar color="surface" density="compact">
          <v-toolbar-title class="text-body-1 font-weight-bold">
            {{ editingBankAccount?.id ? 'Edit Bank Account' : 'Add Bank Account' }}
          </v-toolbar-title>
          <v-spacer />
          <v-btn icon="mdi-close" @click="showBankForm = false" />
        </v-toolbar>
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="12">
              <v-text-field v-model="bankForm.accountName" label="Account Name *" placeholder="e.g. USD Main, EUR Account" variant="outlined" density="compact" hide-details="auto" :rules="[v => !!v || 'Required']" prepend-inner-icon="mdi-tag-outline" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="bankForm.bankName" label="Bank Name" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-bank" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="bankForm.beneficiaryName" label="Beneficiary Name" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-account-cash" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="bankForm.bankAddress" label="Bank Address" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-map-marker-radius" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="bankForm.accountNumber" label="Account Number" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-numeric" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="bankForm.swiftCode" label="SWIFT Code" variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-identifier" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="bankForm.sortOrder" label="Sort Order" type="number" variant="outlined" density="compact" hide-details min="0" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showBankForm = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="savingBank" @click="saveBankAccount">
            {{ editingBankAccount?.id ? 'Update' : 'Add' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirm -->
    <v-dialog v-model="showDeleteConfirm" max-width="400">
      <v-card>
        <v-card-title class="text-h6">Delete Preset?</v-card-title>
        <v-card-text>Are you sure you want to delete <strong>{{ deleteTarget?.name }}</strong>? This cannot be undone.</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDeleteConfirm = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="doDelete">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="3000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>

<script setup lang="ts">
import { consoleError } from 'vuetify/lib/util/console.mjs'

const api = useApi()
const authStore = useAuthStore()

const loading = ref(true)
const saving = ref(false)
const deleting = ref(false)
const presets = ref<any[]>([])
const showDialog = ref(false)
const showDeleteConfirm = ref(false)
const deleteTarget = ref<any>(null)
const editingId = ref<number | null>(null)
const logoPreview = ref<string | null>(null)
const allWarehouses = ref<any[]>([])
const selectedWarehouseIds = ref<number[]>([])
const originalWarehouseIds = ref<number[]>([])

// Bank accounts for the preset currently being edited
const bankAccounts = ref<any[]>([])
const showBankForm = ref(false)
const savingBank = ref(false)
const editingBankAccount = ref<any>(null)
const defaultBankForm = () => ({ accountName: '', bankName: '', bankAddress: '', accountNumber: '', beneficiaryName: '', swiftCode: '', sortOrder: 0 })
const bankForm = ref(defaultBankForm())

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const defaultForm = () => ({
  name: '',
  location: '',
  shipToAddress: '',
  shipToPhone: '',
  fedexAccount: '',
  phone: '',
  website: '',
  email: '',
  termsAndConditions: '',
  sortOrder: 0,
  logoBase64: null as string | null,
  logoMimeType: null as string | null,
  bankName: '',
  bankAddress: '',
  accountNumber: '',
  beneficiaryName: '',
  swiftCode: '',
  primaryColor: '#1a2744',
  accentColor: '#2563eb',
  customPdfHtml: null as string | null,
})

const form = ref(defaultForm())

onMounted(async () => {
  await loadPresets()
  try {
    allWarehouses.value = await api.get('/warehouses')
  } catch { allWarehouses.value = [] }
})

async function loadPresets() {
  loading.value = true
  try {
    presets.value = await api.get<any[]>('/companypresets')
  } catch {
    showSnack('Failed to load presets', 'error')
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  form.value = defaultForm()
  logoPreview.value = null
  bankAccounts.value = []
  selectedWarehouseIds.value = []
  originalWarehouseIds.value = []
  showDialog.value = true
}

async function openEdit(preset: any) {
  editingId.value = preset.id
  form.value = {
    name: preset.name,
    location: preset.location || '',
    shipToAddress: preset.shipToAddress || '',
    shipToPhone: preset.shipToPhone || '',
    fedexAccount: preset.fedexAccount || '',
    bankName: preset.bankName || '',
    bankAddress: preset.bankAddress || '',
    accountNumber: preset.accountNumber || '',
    beneficiaryName: preset.beneficiaryName || '',
    swiftCode: preset.swiftCode || '',
    phone: preset.phone || '',
    website: preset.website || '',
    email: preset.email || '',
    termsAndConditions: preset.termsAndConditions || '',
    sortOrder: preset.sortOrder || 0,
    logoBase64: null, // only sent if changed
    logoMimeType: null,
    primaryColor: preset.primaryColor || '#1a2744',
    accentColor: preset.accentColor || '#2563eb',
    customPdfHtml: preset.customPdfHtml || null,
  }
  logoPreview.value = preset.logoBase64
    ? `data:${preset.logoMimeType};base64,${preset.logoBase64}`
    : null
  // Load bank accounts
  bankAccounts.value = preset.bankAccounts ? [...preset.bankAccounts] : []
  // Load linked warehouses
  selectedWarehouseIds.value = []
  originalWarehouseIds.value = []
  try {
    const linked = await api.get(`/companypresets/${preset.id}/warehouses`)
    const ids = linked.map((w: any) => w.id)
    selectedWarehouseIds.value = [...ids]
    originalWarehouseIds.value = [...ids]
  } catch { /* ignore */ }
  showDialog.value = true
}

async function onLogoUpload(file: File | File[] | null) {
  const f = Array.isArray(file) ? file[0] : file
  if (!f) return
  const reader = new FileReader()
  reader.onload = (e) => {
    const result = e.target?.result as string
    logoPreview.value = result
    const commaIdx = result.indexOf(',')
    const header = result.substring(0, commaIdx)
    const base64 = result.substring(commaIdx + 1)
    const mimeMatch = header.match(/data:([^;]+)/)
    form.value.logoBase64 = base64 || null
    form.value.logoMimeType = mimeMatch?.[1] ?? f.type
  }
  reader.readAsDataURL(f)
}

function clearLogo() {
  logoPreview.value = null
  form.value.logoBase64 = ''
  form.value.logoMimeType = null
}

async function savePreset() {
  if (!form.value.name?.trim()) {
    showSnack('Company name is required', 'error')
    return
  }
  saving.value = true
  try {
    let presetId = editingId.value
    if (editingId.value) {
      await api.put(`/companypresets/${editingId.value}`, form.value)
      showSnack('Preset updated', 'success')
    } else {
      const res = await api.post('/companypresets', form.value)
      presetId = res?.id ?? null
      // Persist any in-memory bank accounts that were added before the preset existed
      if (presetId) {
        const pending = bankAccounts.value.filter(b => !b.id)
        for (const b of pending) {
          const { id: _, ...payload } = b
          await api.post(`/companypresets/${presetId}/bank-accounts`, payload)
        }
      }
      showSnack('Preset created', 'success')
    }
    // Sync warehouse links
    if (presetId) {
      const toAdd = selectedWarehouseIds.value.filter(id => !originalWarehouseIds.value.includes(id))
      const toRemove = originalWarehouseIds.value.filter(id => !selectedWarehouseIds.value.includes(id))
      await Promise.all([
        ...toAdd.map(wid => api.post(`/companypresets/${presetId}/warehouses/${wid}`, {})),
        ...toRemove.map(wid => api.del(`/companypresets/${presetId}/warehouses/${wid}`)),
      ])
    }
    showDialog.value = false
    await loadPresets()
  } catch(e) {
    console.log(e)
    showSnack('Failed to save preset', 'error')
  } finally {
    saving.value = false
  }
}

// ── Bank Account CRUD ──────────────────────────────────────────────────────────

function openBankAccountForm(ba?: any) {
  editingBankAccount.value = ba ?? null
  bankForm.value = ba
    ? { accountName: ba.accountName, bankName: ba.bankName || '', bankAddress: ba.bankAddress || '', accountNumber: ba.accountNumber || '', beneficiaryName: ba.beneficiaryName || '', swiftCode: ba.swiftCode || '', sortOrder: ba.sortOrder ?? 0 }
    : defaultBankForm()
  showBankForm.value = true
}

async function saveBankAccount() {
  if (!bankForm.value.accountName?.trim()) {
    showSnack('Account name is required', 'error')
    return
  }
  savingBank.value = true
  try {
    const payload = { ...bankForm.value }
    if (editingBankAccount.value?.id && editingId.value) {
      // Existing account on saved preset — call API
      const updated = await api.put(`/companypresets/${editingId.value}/bank-accounts/${editingBankAccount.value.id}`, payload)
      const idx = bankAccounts.value.findIndex(b => b.id === editingBankAccount.value.id)
      if (idx >= 0) bankAccounts.value[idx] = { ...editingBankAccount.value, ...payload }
    } else if (editingBankAccount.value && !editingBankAccount.value.id) {
      // Unsaved new account (preset not yet created) — update in-memory
      const idx = bankAccounts.value.indexOf(editingBankAccount.value)
      if (idx >= 0) bankAccounts.value[idx] = { ...editingBankAccount.value, ...payload }
    } else if (editingId.value) {
      // New account on existing preset — call API
      const created = await api.post(`/companypresets/${editingId.value}/bank-accounts`, payload)
      bankAccounts.value.push(created)
    } else {
      // New account on new preset (not saved yet) — add in-memory with temp marker
      bankAccounts.value.push({ id: null, ...payload })
    }
    showBankForm.value = false
    showSnack('Bank account saved', 'success')
  } catch {
    showSnack('Failed to save bank account', 'error')
  } finally {
    savingBank.value = false
  }
}

async function removeBankAccount(ba: any, idx: number) {
  if (ba.id && editingId.value) {
    try {
      await api.del(`/companypresets/${editingId.value}/bank-accounts/${ba.id}`)
    } catch {
      showSnack('Failed to delete bank account', 'error')
      return
    }
  }
  bankAccounts.value.splice(idx, 1)
  showSnack('Bank account removed', 'success')
}

function confirmDelete(preset: any) {
  deleteTarget.value = preset
  showDeleteConfirm.value = true
}

async function doDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  try {
    await api.del(`/companypresets/${deleteTarget.value.id}`)
    showSnack('Preset deleted', 'success')
    showDeleteConfirm.value = false
    await loadPresets()
  } catch {
    showSnack('Failed to delete preset', 'error')
  } finally {
    deleting.value = false
  }
}

// ── HTML Template Editor ──
const showHtmlEditor = ref(false)
const showVarHints = ref(false)
const htmlEditorPreset = ref<any>(null)
const editingHtml = ref('')
const htmlEditorRef = ref<HTMLTextAreaElement | null>(null)

const templateVars = [
  { key: '{{LOGO}}',              desc: 'Company logo <img> tag' },
  { key: '{{COMPANY_NAME}}',      desc: 'Company name' },
  { key: '{{COMPANY_LOCATION}}',  desc: 'Company address' },
  { key: '{{COMPANY_SHIP_TO}}',  desc: 'Company ship-to address' },
  { key: '{{COMPANY_SHIP_TO_PHONE}}', desc: 'Company ship-to phone' },
  { key: '{{FEDEX_ACCOUNT}}',     desc: 'Company FedEx account' },
  { key: '{{COMPANY_PHONE}}',     desc: 'Company phone' },
  { key: '{{COMPANY_EMAIL}}',     desc: 'Company email' },
  { key: '{{COMPANY_WEBSITE}}',   desc: 'Company website' },
  { key: '{{QUOTE_NUMBER}}',      desc: 'Quote number e.g. QT-0001' },
  { key: '{{DATE}}',              desc: 'Quote creation date' },
  { key: '{{VALID_UNTIL}}',       desc: 'Quote validity date' },
  { key: '{{RFQ_NAME}}',          desc: 'RFQ name / reference' },
  { key: '{{CUSTOMER_NAME}}',     desc: 'Customer company name' },
  { key: '{{CUSTOMER_BILL_TO}}',  desc: 'Bill-to address' },
  { key: '{{CUSTOMER_SHIP_TO}}',  desc: 'Ship-to address' },
  { key: '{{ITEMS_TABLE}}',       desc: 'Auto-generated items table (HTML)' },
  { key: '{{SUBTOTAL}}',          desc: 'Sum of all line items' },
  { key: '{{SHIPPING}}',          desc: 'Shipping cost' },
  { key: '{{GRAND_TOTAL}}',       desc: 'Grand total (subtotal + shipping)' },
  { key: '{{COMMENTS}}',          desc: 'Quote comments / notes' },
  { key: '{{TERMS}}',             desc: 'Terms & conditions text' },
  { key: '{{PRIMARY_COLOR}}',     desc: 'Preset primary color' },
  { key: '{{ACCENT_COLOR}}',      desc: 'Preset accent color' },
]

const mockData = computed((): Record<string, string> => {
  const p = htmlEditorPreset.value
  const primary = p?.primaryColor || '#1a2744'
  return {
    '{{LOGO}}':              '<div style="width:80px;height:40px;background:#e2e8f0;border-radius:4px;display:flex;align-items:center;justify-content:center;font-size:10px;color:#94a3b8;">LOGO</div>',
    '{{COMPANY_NAME}}':      p?.name || 'Acme Aviation Ltd',
    '{{COMPANY_LOCATION}}':  p?.location || '123 Business Park, London',
    '{{COMPANY_SHIP_TO}}':    p?.shipToAddress || 'Warehouse A, Dock 4, London',
    '{{COMPANY_SHIP_TO_PHONE}}': p?.shipToPhone || '+44 800 123 4567',
    '{{FEDEX_ACCOUNT}}':     p?.fedexAccount || '1234-5678-9',
    '{{COMPANY_PHONE}}':     p?.phone || '+44 7700 900000',
    '{{COMPANY_EMAIL}}':     p?.email || 'sales@acme.com',
    '{{COMPANY_WEBSITE}}':   p?.website || 'www.acme.com',
    '{{QUOTE_NUMBER}}':      'QT-0042',
    '{{DATE}}':              new Date().toLocaleDateString(),
    '{{VALID_UNTIL}}':       new Date(Date.now() + 7 * 864e5).toLocaleDateString(),
    '{{RFQ_NAME}}':          'RFQ-2024-001',
    '{{CUSTOMER_NAME}}':     'Sample Customer Corp',
    '{{CUSTOMER_BILL_TO}}':  '456 Customer Ave, New York',
    '{{CUSTOMER_SHIP_TO}}':  '456 Customer Ave, New York',
    '{{ITEMS_TABLE}}':       `<table style="width:100%;border-collapse:collapse;font-size:12px;">
    <thead><tr style="background:${primary};color:#fff;">
      <th style="padding:6px 10px;text-align:left;">#</th>
      <th style="padding:6px 10px;text-align:left;">Part Number</th>
      <th style="padding:6px 10px;text-align:center;">Qty</th>
      <th style="padding:6px 10px;text-align:right;">Unit Price</th>
      <th style="padding:6px 10px;text-align:right;">Total</th>
    </tr></thead>
    <tbody>
      <tr><td style="padding:6px 10px;">1</td><td>PN-ABC-123</td><td style="text-align:center;">2</td><td style="text-align:right;">$500.00</td><td style="text-align:right;">$1,000.00</td></tr>
      <tr style="background:#f9fafb;"><td style="padding:6px 10px;">2</td><td>PN-XYZ-456</td><td style="text-align:center;">1</td><td style="text-align:right;">$750.00</td><td style="text-align:right;">$750.00</td></tr>
    </tbody>
  </table>`,
    '{{SUBTOTAL}}':          '$1,750.00',
    '{{SHIPPING}}':          '$50.00',
    '{{GRAND_TOTAL}}':       '$1,800.00',
    '{{COMMENTS}}':          'Delivery within 5 business days.',
    '{{TERMS}}':             'PAYMENT: Prepayment. DELIVERY: EXW. Valid 7 days.',
    '{{PRIMARY_COLOR}}':     primary,
    '{{ACCENT_COLOR}}':      p?.accentColor || '#2563eb',
  }
})

const previewHtml = computed(() => {
  if (!editingHtml.value.trim()) return '<p style="font-family:sans-serif;color:#94a3b8;padding:20px;">Start typing HTML to see the preview…</p>'
  let html = editingHtml.value
  for (const [key, val] of Object.entries(mockData.value)) {
    html = html.split(key).join(val)
  }
  return html
})

const defaultHtmlTemplate = computed(() => {
  const p = htmlEditorPreset.value
  const primary = p?.primaryColor || '#1a2744'
  const accent = p?.accentColor || '#2563eb'
  return `<!DOCTYPE html>
<html><head><meta charset="UTF-8">
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: Arial, sans-serif; font-size: 13px; color: #1f2937; background: #fff; padding: 30px; }
  .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
  .company-info h1 { color: ${primary}; font-size: 22px; font-weight: 800; }
  .company-info p { color: #6b7280; font-size: 11px; margin-top: 2px; }
  .quote-meta { text-align: right; }
  .quote-meta .quote-num { font-size: 18px; font-weight: 700; color: ${primary}; }
  .quote-meta p { color: #6b7280; font-size: 11px; }
  .divider { height: 3px; background: linear-gradient(90deg, ${primary}, ${accent}, #e5e7eb); margin: 16px 0; }
  .addresses { display: flex; gap: 24px; margin-bottom: 20px; }
  .address-box { flex: 1; }
  .address-box .label { font-size: 9px; font-weight: 700; color: ${accent}; letter-spacing: 1px; margin-bottom: 4px; }
  .address-box p { font-size: 11px; color: #374151; }
  table { width: 100%; border-collapse: collapse; margin-bottom: 16px; font-size: 12px; }
  thead tr { background: ${primary}; color: #fff; }
  thead th { padding: 7px 10px; text-align: left; }
  tbody tr:nth-child(even) { background: #f9fafb; }
  tbody td { padding: 6px 10px; border-bottom: 1px solid #f3f4f6; }
  .totals { float: right; width: 220px; margin-bottom: 16px; }
  .totals table { font-size: 12px; }
  .totals td { padding: 4px 8px; }
  .totals .grand td { font-weight: 700; color: ${primary}; border-top: 2px solid ${primary}; }
  .footer { clear: both; border-top: 1px solid #e5e7eb; padding-top: 10px; font-size: 10px; color: #9ca3af; }
</style>
</head><body>
  <div class="header">
    <div class="company-info">
      {{LOGO}}
      <h1>{{COMPANY_NAME}}</h1>
      <p>{{COMPANY_LOCATION}}</p>
      <p>{{COMPANY_PHONE}} | {{COMPANY_EMAIL}} | {{COMPANY_WEBSITE}}</p>
    </div>
    <div class="quote-meta">
      <div class="quote-num">QUOTATION</div>
      <p><strong>{{QUOTE_NUMBER}}</strong></p>
      <p>Date: {{DATE}}</p>
      <p>Valid Until: {{VALID_UNTIL}}</p>
      <p>RFQ: {{RFQ_NAME}}</p>
    </div>
  </div>
  <div class="divider"></div>
  <div class="addresses">
    <div class="address-box"><div class="label">BILL TO</div><p>{{CUSTOMER_NAME}}</p><p>{{CUSTOMER_BILL_TO}}</p></div>
    <div class="address-box"><div class="label">SHIP TO</div><p>{{CUSTOMER_NAME}}</p><p>{{CUSTOMER_SHIP_TO}}</p></div>
  </div>
  {{ITEMS_TABLE}}
  <div class="totals">
    <table>
      <tr><td>Subtotal</td><td style="text-align:right;">{{SUBTOTAL}}</td></tr>
      <tr><td>Shipping</td><td style="text-align:right;">{{SHIPPING}}</td></tr>
      <tr class="grand"><td>TOTAL</td><td style="text-align:right;">{{GRAND_TOTAL}}</td></tr>
    </table>
  </div>
  <div class="footer">
    <p><strong>Comments:</strong> {{COMMENTS}}</p>
    <p style="margin-top:4px;">{{TERMS}}</p>
  </div>
</body></html>`
})

function openHtmlEditor(preset: any) {
  htmlEditorPreset.value = preset
  editingHtml.value = preset.customPdfHtml || ''
  showVarHints.value = false
  showHtmlEditor.value = true
}

function loadDefaultTemplate() {
  editingHtml.value = defaultHtmlTemplate.value
}

function insertVar(varKey: string) {
  const ta = htmlEditorRef.value
  if (!ta) {
    editingHtml.value += varKey
    return
  }
  const start = ta.selectionStart
  const end = ta.selectionEnd
  const val = editingHtml.value
  editingHtml.value = val.substring(0, start) + varKey + val.substring(end)
  nextTick(() => {
    ta.selectionStart = ta.selectionEnd = start + varKey.length
    ta.focus()
  })
}

function insertTab(e: KeyboardEvent) {
  const ta = htmlEditorRef.value
  if (!ta) return
  const start = ta.selectionStart
  const end = ta.selectionEnd
  editingHtml.value = editingHtml.value.substring(0, start) + '  ' + editingHtml.value.substring(end)
  nextTick(() => {
    ta.selectionStart = ta.selectionEnd = start + 2
  })
}

function clearCustomHtml() {
  editingHtml.value = ''
}

async function saveCustomHtml() {
  if (!htmlEditorPreset.value) return
  saving.value = true
  try {
    const preset = htmlEditorPreset.value
    await api.put(`/companypresets/${preset.id}`, {
      name: preset.name,
      location: preset.location,
      shipToAddress: preset.shipToAddress,
      shipToPhone: preset.shipToPhone,
      fedexAccount: preset.fedexAccount,
      bankName: preset.bankName,
      bankAddress: preset.bankAddress,
      accountNumber: preset.accountNumber,
      beneficiaryName: preset.beneficiaryName,
      swiftCode: preset.swiftCode,
      phone: preset.phone,
      website: preset.website,
      email: preset.email,
      termsAndConditions: preset.termsAndConditions,
      sortOrder: preset.sortOrder,
      primaryColor: preset.primaryColor,
      accentColor: preset.accentColor,
      customPdfHtml: editingHtml.value.trim() || null,
    })
    showSnack('Template saved', 'success')
    showHtmlEditor.value = false
    await loadPresets()
  } catch {
    showSnack('Failed to save template', 'error')
  } finally {
    saving.value = false
  }
}

function showSnack(text: string, color: string) {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}
</script>

<style scoped>
.preset-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  transition: border-color 0.2s;
}
.preset-card:hover {
  border-color: rgb(var(--v-theme-primary), 0.4);
}
.border-primary {
  border-color: rgb(var(--v-theme-primary)) !important;
}
.logo-slot {
  width: 52px;
  height: 52px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(var(--v-theme-surface-variant), 0.5);
  border-radius: 8px;
  overflow: hidden;
}
.preset-logo {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.detail-row {
  font-size: 12px;
  color: rgba(var(--v-theme-on-surface), 0.7);
  margin-bottom: 2px;
  display: flex;
  align-items: flex-start;
  gap: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.terms-preview {
  font-size: 11px;
  color: rgba(var(--v-theme-on-surface), 0.6);
  white-space: pre-wrap;
  font-family: inherit;
  background: rgba(var(--v-theme-surface-variant), 0.3);
  border-radius: 4px;
  padding: 6px 8px;
  margin: 0;
  max-height: 80px;
  overflow-y: auto;
}
.logo-upload-area {
  border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  padding: 12px;
}
.logo-preview-box {
  width: 80px;
  height: 80px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  background: rgba(var(--v-theme-surface-variant), 0.3);
  flex-shrink: 0;
}
.logo-preview-img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
</style>

