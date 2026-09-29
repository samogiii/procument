<template>
  <v-dialog :model-value="modelValue" :max-width="mode === 'creditNote' ? 640 : 560" persistent @update:model-value="close">
    <v-card class="glass-card">
      <v-form ref="formRef" @submit.prevent="submit">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon :icon="MODE_META[mode].icon" class="mr-2" :color="MODE_META[mode].color" />
          {{ MODE_META[mode].title }}
        </v-card-title>

        <v-card-text class="pt-0">
          <p class="text-body-2 text-medium-emphasis mb-4">{{ MODE_META[mode].hint }}</p>

          <!-- Supplier: catalog suppliers only (server search) -->
          <v-autocomplete
            v-model="supplierId"
            v-model:search="supplierSearch"
            :items="supplierItems"
            item-title="name"
            item-value="id"
            label="Supplier *"
            prepend-inner-icon="mdi-truck-outline"
            variant="outlined"
            :loading="suppliersLoading"
            :disabled="!!supplier"
            :rules="[required]"
            no-filter
            class="mb-2"
          />

          <!-- Company: radios while there are few, a select when the list is long -->
          <div class="mb-2">
            <v-radio-group
              v-if="companies.length && companies.length <= 5"
              v-model="companyId"
              label="Our company *"
              :rules="[required]"
              :disabled="!!fixedCompanyId"
              inline
            >
              <v-radio v-for="c in companies" :key="c.id" :label="c.name" :value="c.id" />
            </v-radio-group>
            <v-select
              v-else
              v-model="companyId"
              :items="companies"
              item-title="name"
              item-value="id"
              label="Our company *"
              prepend-inner-icon="mdi-domain"
              variant="outlined"
              :loading="companiesLoading"
              :disabled="!!fixedCompanyId"
              :rules="[required]"
            />
          </div>

          <v-radio-group
            v-if="mode === 'adjustment'"
            v-model="direction"
            label="Adjustment *"
            inline
            class="mb-2"
          >
            <v-radio label="Add credit (In)" value="In" color="success" />
            <v-radio label="Remove credit (Out)" value="Out" color="error" />
          </v-radio-group>

          <template v-if="mode === 'walletTopUp'">
            <v-select
              v-model="walletId"
              :items="wallets"
              item-title="label"
              item-value="id"
              label="Paid from wallet *"
              prepend-inner-icon="mdi-bank-outline"
              variant="outlined"
              :loading="walletsLoading"
              :rules="[required]"
              class="mb-2"
            />
          </template>

          <v-text-field
            v-model.number="amount"
            type="number"
            inputmode="decimal"
            min="0.01"
            step="0.01"
            prefix="$"
            :label="mode === 'adjustment' ? 'Amount (USD) *' : 'Credit amount (USD) *'"
            variant="outlined"
            :rules="[positiveMoney]"
            :hint="balanceHint"
            persistent-hint
            class="mb-2"
          />

          <template v-if="mode === 'walletTopUp' && selectedWallet && selectedWallet.currency !== 'USD'">
            <v-text-field
              v-model.number="exchangeRate"
              type="number"
              inputmode="decimal"
              min="0.000001"
              step="0.000001"
              :label="`${selectedWallet.currency} per 1 USD *`"
              variant="outlined"
              :rules="[positiveRate]"
              class="mb-2"
            />
          </template>
          <p v-if="mode === 'walletTopUp' && selectedWallet" class="text-body-2 mb-3">
            Wallet debit: <strong>{{ formatPrice(walletDebit) }} {{ selectedWallet.currency }}</strong>
          </p>

          <!-- Proof of where the credit comes from -->
          <fieldset v-if="mode === 'creditNote'" class="proof-fieldset mb-3">
            <legend class="text-subtitle-2 px-1">Proof</legend>

            <v-radio-group v-model="originKind" label="Where does this credit come from? *" inline hide-details class="mb-2">
              <v-radio label="One of our POs" value="po" />
              <v-radio label="Other (type it)" value="text" />
            </v-radio-group>
            <v-autocomplete
              v-if="originKind === 'po'"
              v-model="sourcePoId"
              v-model:search="poSearch"
              :items="poItems"
              item-title="poNumber"
              item-value="id"
              :item-props="poItemProps"
              label="Our PO *"
              prepend-inner-icon="mdi-package-variant-closed"
              variant="outlined"
              :loading="posLoading"
              :disabled="!supplierId"
              :hint="supplierId ? 'POs with this supplier, newest first' : 'Choose the supplier first'"
              persistent-hint
              :rules="[required]"
              no-filter
              class="mb-2"
            />
            <v-text-field
              v-else
              v-model="sourceText"
              label="Origin *"
              placeholder="e.g. Opening balance 2026, returned parts on their PI 4471"
              variant="outlined"
              maxlength="100"
              counter
              :rules="[required]"
              class="mb-2"
            />

            <v-text-field
              v-model="creditMemo"
              label="Credit memo"
              hint="The supplier's credit memo number or text"
              persistent-hint
              variant="outlined"
              maxlength="200"
              class="mb-2"
            />
            <v-file-input
              v-model="supplierPi"
              label="Supplier PI document"
              :accept="SUPPLIER_CREDIT_FILE_ACCEPT"
              prepend-icon=""
              prepend-inner-icon="mdi-file-document-outline"
              variant="outlined"
              show-size
              clearable
              class="mb-2"
            />
            <v-file-input
              v-model="documents"
              label="Other documents (credit memo scan, e-mails, …)"
              :accept="SUPPLIER_CREDIT_FILE_ACCEPT"
              multiple
              chips
              counter
              :rules="[maxFiles]"
              prepend-icon=""
              prepend-inner-icon="mdi-paperclip"
              variant="outlined"
              show-size
              clearable
            />
          </fieldset>

          <v-textarea
            v-model="notes"
            :label="mode === 'adjustment' ? 'Reason *' : 'Notes'"
            variant="outlined"
            rows="2"
            auto-grow
            maxlength="500"
            counter
            :rules="mode === 'adjustment' ? [required] : []"
          />

          <div aria-live="polite">
            <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mt-3">
              {{ errorMessage }}
            </v-alert>
          </div>
        </v-card-text>

        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
          <v-btn type="submit" :color="MODE_META[mode].color" variant="flat" :loading="saving">
            {{ submitLabel }}
          </v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import type { SupplierCreditDialogMode, SupplierCreditOption, SupplierCreditPoOption, SupplierCreditWriteResult } from '~/utils/supplierCredits'

const props = defineProps<{
  modelValue: boolean
  mode: SupplierCreditDialogMode
  /** Pre-selected, locked supplier (e.g. when opened from a ledger). */
  supplier?: SupplierCreditOption | null
  /** Pre-selected, locked company. */
  fixedCompanyId?: number | null
  /** Current balance of the pre-selected account, shown for Out adjustments. */
  currentBalance?: number | null
}>()
const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: [result: SupplierCreditWriteResult]
}>()

const api = useApi()

const MODE_META: Record<SupplierCreditDialogMode, { title: string; hint: string; icon: string; color: string; action: string }> = {
  creditNote: {
    title: 'Add Credit Note', icon: 'mdi-file-document-plus-outline', color: 'success', action: 'Add credit',
    hint: 'The supplier owes us this amount (returned parts, overpayment, goodwill). It can be used on future POs of this company.',
  },
  walletTopUp: {
    title: 'Prepay from Wallet', icon: 'mdi-bank-transfer-out', color: 'teal', action: 'Prepay',
    hint: 'Money leaves the wallet now and becomes credit with the supplier. It pays no PO until you apply it.',
  },
  adjustment: {
    title: 'Credit Adjustment', icon: 'mdi-scale-balance', color: 'warning', action: 'Save adjustment',
    hint: 'Correct a balance by hand. Every adjustment needs a reason and is kept in the history.',
  },
}

const formRef = ref<any>(null)
const saving = ref(false)
const errorMessage = ref('')
let requestId = newRequestId()

const supplierId = ref<number | null>(null)
const supplierSearch = ref('')
const supplierItems = shallowRef<SupplierCreditOption[]>([])
const suppliersLoading = ref(false)
const companies = shallowRef<SupplierCreditOption[]>([])
const companiesLoading = ref(false)
const companyId = ref<number | null>(null)
const direction = ref<'In' | 'Out'>('In')
const amount = ref<number | null>(null)
// Proof (credit notes): origin PO or text, credit memo, supplier PI and other documents.
const originKind = ref<'po' | 'text'>('po')
const sourcePoId = ref<number | null>(null)
const sourceText = ref('')
const creditMemo = ref('')
const supplierPi = ref<File | File[] | null>(null)
const documents = ref<File[]>([])
const poSearch = ref('')
const poItems = shallowRef<SupplierCreditPoOption[]>([])
const posLoading = ref(false)
const notes = ref('')
type Wallet = { id: number; label: string; currency: string }
const wallets = shallowRef<Wallet[]>([])
const walletsLoading = ref(false)
const walletId = ref<number | null>(null)
const exchangeRate = ref<number | null>(null)

const selectedWallet = computed(() => wallets.value.find(w => w.id === walletId.value))
const walletDebit = computed(() => Number(amount.value || 0) * (selectedWallet.value?.currency === 'USD' ? 1 : Number(exchangeRate.value || 0)))
const submitLabel = computed(() => {
  const value = Number(amount.value || 0)
  return value > 0 ? `${MODE_META[props.mode].action} $${formatPrice(value)}` : MODE_META[props.mode].action
})
const balanceHint = computed(() =>
  props.mode === 'adjustment' && direction.value === 'Out' && props.currentBalance != null
    ? `Current balance: $${formatPrice(props.currentBalance)}`
    : '')

const required = (v: any) => (v !== null && v !== undefined && v !== '') || 'Required'
const positiveMoney = (v: any) => {
  const n = Number(v)
  if (!(n > 0)) return 'Enter a positive amount'
  return Math.round(n * 100) / 100 === n || 'At most two decimal places'
}
const maxFiles = (v: File[] | null) => {
  const count = (v?.length ?? 0) + (asFile(supplierPi.value) ? 1 : 0)
  return count <= SUPPLIER_CREDIT_MAX_FILES || `Attach at most ${SUPPLIER_CREDIT_MAX_FILES} files at a time`
}
function asFile(v: File | File[] | null): File | null {
  return Array.isArray(v) ? (v[0] ?? null) : v
}
function poItemProps(po: SupplierCreditPoOption) {
  const parts = [po.companyPresetName, po.status, po.totalAmount != null ? `$${formatPrice(po.totalAmount)}` : null]
  return { subtitle: parts.filter(Boolean).join(' · ') }
}
const positiveRate = (v: any) => {
  const n = Number(v)
  if (!(n > 0)) return 'Enter a positive rate'
  return Math.round(n * 1e6) / 1e6 === n || 'At most six decimal places'
}

// Fresh state (and a fresh idempotency key) every time the dialog opens.
watch(() => props.modelValue, (open) => {
  if (!open) return
  requestId = newRequestId()
  errorMessage.value = ''
  saving.value = false
  supplierId.value = props.supplier?.id ?? null
  supplierItems.value = props.supplier ? [props.supplier] : []
  companyId.value = props.fixedCompanyId ?? null
  direction.value = 'In'
  amount.value = null
  originKind.value = 'po'
  sourcePoId.value = null
  sourceText.value = ''
  creditMemo.value = ''
  supplierPi.value = null
  documents.value = []
  poItems.value = []
  notes.value = ''
  walletId.value = null
  exchangeRate.value = null
  formRef.value?.resetValidation?.()
  if (!companies.value.length) loadCompanies()
  if (props.mode === 'walletTopUp' && !wallets.value.length) loadWallets()
  if (!props.supplier) searchSuppliers('')
}, { immediate: true })

let supplierTimer: ReturnType<typeof setTimeout> | null = null
watch(supplierSearch, (q) => {
  if (props.supplier) return
  // Selecting an item puts its name in the search box; no need to search for it again.
  if (q && supplierItems.value.some(s => s.name === q && s.id === supplierId.value)) return
  if (supplierTimer) clearTimeout(supplierTimer)
  supplierTimer = setTimeout(() => searchSuppliers(q ?? ''), 250)
})

watch(supplierId, () => {
  sourcePoId.value = null
  poItems.value = []
  if (props.mode === 'creditNote' && supplierId.value) searchPos('')
})

let poTimer: ReturnType<typeof setTimeout> | null = null
watch(poSearch, (q) => {
  if (!supplierId.value) return
  if (q && poItems.value.some(p => p.poNumber === q && p.id === sourcePoId.value)) return
  if (poTimer) clearTimeout(poTimer)
  poTimer = setTimeout(() => searchPos(q ?? ''), 250)
})

async function searchPos(q: string) {
  if (!supplierId.value) return
  posLoading.value = true
  try {
    const found = await api.get<SupplierCreditPoOption[]>(
      `/supplier-credits/lookup/purchase-orders?supplierId=${supplierId.value}&q=${encodeURIComponent(q)}`)
    const selected = poItems.value.find(p => p.id === sourcePoId.value)
    poItems.value = selected && !found.some(p => p.id === selected.id) ? [selected, ...found] : found
  } catch { /* the API validates the origin on submit */ }
  finally { posLoading.value = false }
}

async function searchSuppliers(q: string) {
  suppliersLoading.value = true
  try {
    const found = await api.get<SupplierCreditOption[]>(`/supplier-credits/lookup/suppliers?q=${encodeURIComponent(q)}`)
    // Keep the selected supplier in the list so its name stays visible.
    const selected = supplierItems.value.find(s => s.id === supplierId.value)
    supplierItems.value = selected && !found.some(s => s.id === selected.id) ? [selected, ...found] : found
  } catch { /* the field stays usable; the API validates on submit */ }
  finally { suppliersLoading.value = false }
}

async function loadCompanies() {
  companiesLoading.value = true
  try { companies.value = await api.get<SupplierCreditOption[]>('/supplier-credits/lookup/companies') }
  catch { companies.value = [] }
  finally { companiesLoading.value = false }
}

async function loadWallets() {
  walletsLoading.value = true
  try {
    const boxes = await api.get<any[]>('/supplier-payments/wallets')
    // Wallets are identified by their own name, as on the payment page.
    wallets.value = boxes.map((b: any) => ({ id: b.id, label: `${b.name} (${b.currency})`, currency: b.currency }))
  } catch { wallets.value = [] }
  finally { walletsLoading.value = false }
}

function close(value = false) {
  if (saving.value) return
  emit('update:modelValue', value)
}

async function submit() {
  if (saving.value) return
  const { valid } = await formRef.value.validate()
  if (!valid) return

  saving.value = true
  errorMessage.value = ''
  try {
    let result: SupplierCreditWriteResult
    if (props.mode === 'creditNote') {
      const form = new FormData()
      form.append('supplierId', String(supplierId.value))
      form.append('companyPresetId', String(companyId.value))
      form.append('amount', String(amount.value))
      if (originKind.value === 'po' && sourcePoId.value) form.append('sourcePurchaseOrderId', String(sourcePoId.value))
      if (originKind.value === 'text' && sourceText.value.trim()) form.append('sourcePoReference', sourceText.value.trim())
      if (creditMemo.value.trim()) form.append('creditMemo', creditMemo.value.trim())
      if (notes.value.trim()) form.append('notes', notes.value.trim())
      const pi = asFile(supplierPi.value)
      if (pi) form.append('supplierPi', pi)
      for (const doc of documents.value ?? []) form.append('documents', doc)
      form.append('requestId', requestId)
      result = await api.post<SupplierCreditWriteResult>('/supplier-credits/credit-note', form)
    } else if (props.mode === 'walletTopUp') {
      result = await api.post<SupplierCreditWriteResult>('/supplier-credits/wallet-top-up', {
        supplierId: supplierId.value,
        companyPresetId: companyId.value,
        walletId: walletId.value,
        amount: amount.value,
        exchangeRate: selectedWallet.value?.currency === 'USD' ? null : exchangeRate.value,
        notes: notes.value.trim() || null,
        requestId,
      })
    } else {
      result = await api.post<SupplierCreditWriteResult>('/supplier-credits/adjustment', {
        supplierId: supplierId.value,
        companyPresetId: companyId.value,
        direction: direction.value,
        amount: amount.value,
        notes: notes.value.trim(),
        requestId,
      })
    }
    saving.value = false
    emit('saved', result)
    emit('update:modelValue', false)
  } catch (e: any) {
    // The same requestId is kept, so pressing the button again cannot post twice.
    errorMessage.value = apiErrorMessage(e, 'The credit could not be saved. Try again.')
    saving.value = false
  }
}
</script>

<style scoped>
.proof-fieldset {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  padding: 8px 12px 12px;
}
</style>
