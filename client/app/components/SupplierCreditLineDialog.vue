<template>
  <v-dialog :model-value="modelValue" max-width="520" persistent @update:model-value="close">
    <v-card class="glass-card">
      <v-form ref="formRef" @submit.prevent="submit">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-credit-card-clock-outline" class="mr-2" color="indigo" />
          {{ account ? 'Change credit limit' : 'Set up a credit line' }}
        </v-card-title>

        <v-card-text class="pt-0">
          <p class="text-body-2 text-medium-emphasis mb-4">
            The supplier lets this company buy up to the limit and pay back later. What is used must be
            paid back through a normal wallet payment (POP and final bank amount).
          </p>

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
            :disabled="!!account"
            :rules="[required]"
            no-filter
            class="mb-2"
          />
          <v-select
            v-model="companyId"
            :items="companies"
            item-title="name"
            item-value="id"
            label="Our company *"
            prepend-inner-icon="mdi-domain"
            variant="outlined"
            :disabled="!!account"
            :rules="[required]"
            class="mb-2"
          />

          <v-alert v-if="account" type="info" variant="tonal" density="compact" class="mb-3">
            Current limit ${{ formatPrice(account.creditLimit ?? 0) }} · owed
            <strong>${{ formatPrice(account.owed) }}</strong> · available ${{ formatPrice(account.balance) }}
          </v-alert>

          <v-text-field
            v-model.number="limit"
            type="number"
            inputmode="decimal"
            min="0"
            step="0.01"
            prefix="$"
            label="Credit limit (USD) *"
            :hint="account && account.owed > 0 ? `Cannot go below what is owed ($${formatPrice(account.owed)})` : 'The most this company may owe the supplier'"
            persistent-hint
            variant="outlined"
            :rules="[limitRule]"
            class="mb-2"
          />
          <v-textarea v-model="notes" label="Notes" variant="outlined" rows="2" auto-grow maxlength="500" counter />

          <div aria-live="polite">
            <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mt-3">{{ errorMessage }}</v-alert>
          </div>
        </v-card-text>

        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
          <v-btn type="submit" color="indigo" variant="flat" :loading="saving">Save limit</v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import type { SupplierCreditAccountRow, SupplierCreditOption, SupplierCreditWriteResult } from '~/utils/supplierCredits'

const props = defineProps<{
  modelValue: boolean
  /** Editing an existing line (supplier and company locked); null = set up a new one. */
  account?: SupplierCreditAccountRow | null
}>()
const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: [result: SupplierCreditWriteResult]
}>()

const api = useApi()
const formRef = ref<any>(null)
const saving = ref(false)
const errorMessage = ref('')
let requestId = newRequestId()

const supplierId = ref<number | null>(null)
const supplierSearch = ref('')
const supplierItems = shallowRef<SupplierCreditOption[]>([])
const suppliersLoading = ref(false)
const companies = shallowRef<SupplierCreditOption[]>([])
const companyId = ref<number | null>(null)
const limit = ref<number | null>(null)
const notes = ref('')

const required = (v: any) => (v !== null && v !== undefined && v !== '') || 'Required'
const limitRule = (v: any) => {
  const n = Number(v)
  if (v === null || v === '' || !(n >= 0)) return 'Enter 0 or more'
  if (Math.round(n * 100) / 100 !== n) return 'At most two decimal places'
  if (props.account && n < props.account.owed) return `At least $${formatPrice(props.account.owed)} (what is owed)`
  if (props.account && n === props.account.creditLimit) return 'This is already the limit'
  return true
}

watch(() => props.modelValue, async (open) => {
  if (!open) return
  requestId = newRequestId()
  errorMessage.value = ''
  saving.value = false
  notes.value = ''
  const a = props.account
  supplierId.value = a?.supplierId ?? null
  supplierItems.value = a ? [{ id: a.supplierId, name: a.supplierName }] : []
  companyId.value = a?.companyPresetId ?? null
  limit.value = a?.creditLimit ?? null
  formRef.value?.resetValidation?.()
  if (!companies.value.length) {
    try { companies.value = await api.get<SupplierCreditOption[]>('/supplier-credits/lookup/companies') } catch { companies.value = [] }
  }
  if (!a) searchSuppliers('')
}, { immediate: true })

let supplierTimer: ReturnType<typeof setTimeout> | null = null
watch(supplierSearch, (q) => {
  if (props.account) return
  if (q && supplierItems.value.some(s => s.name === q && s.id === supplierId.value)) return
  if (supplierTimer) clearTimeout(supplierTimer)
  supplierTimer = setTimeout(() => searchSuppliers(q ?? ''), 250)
})

async function searchSuppliers(q: string) {
  suppliersLoading.value = true
  try {
    const found = await api.get<SupplierCreditOption[]>(`/supplier-credits/lookup/suppliers?q=${encodeURIComponent(q)}`)
    const selected = supplierItems.value.find(s => s.id === supplierId.value)
    supplierItems.value = selected && !found.some(s => s.id === selected.id) ? [selected, ...found] : found
  } catch { /* validated on submit */ }
  finally { suppliersLoading.value = false }
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
    const result = await api.put<SupplierCreditWriteResult>('/supplier-credits/credit-lines', {
      supplierId: supplierId.value,
      companyPresetId: companyId.value,
      creditLimit: limit.value,
      notes: notes.value.trim() || null,
      requestId,
    })
    saving.value = false
    emit('saved', result)
    emit('update:modelValue', false)
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'The credit limit could not be saved.')
    saving.value = false
  }
}
</script>
