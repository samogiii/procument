<template>
  <v-dialog :model-value="modelValue" max-width="560" persistent @update:model-value="close">
    <v-card class="glass-card">
      <v-form ref="formRef" @submit.prevent="submit">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-domain-switch" class="mr-2" color="primary" />
          Change buying company
        </v-card-title>

        <v-card-text class="pt-0">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ po.poNumber }} · {{ po.supplierName }}. Choose which of our companies places this PO.
            Credit a company holds with {{ po.supplierName }} can only be used on its own POs.
          </p>

          <v-radio-group v-model="companyId" label="Company *" :rules="[required]" :disabled="saving" class="mb-1">
            <v-radio v-for="c in companyRows" :key="c.id" :value="c.id">
              <template #label>
                <span>{{ c.name }}</span>
                <span v-if="c.id === po.companyPresetId" class="text-caption text-medium-emphasis ml-2">(current)</span>
                <v-chip v-if="c.credit > 0" size="x-small" color="success" variant="tonal" prepend-icon="mdi-cash-refund" class="ml-2">
                  ${{ formatPrice(c.credit) }} credit
                </v-chip>
                <v-chip v-if="c.line > 0" size="x-small" color="indigo" variant="tonal" prepend-icon="mdi-credit-card-clock-outline" class="ml-2">
                  ${{ formatPrice(c.line) }} credit line
                </v-chip>
              </template>
            </v-radio>
          </v-radio-group>
          <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

          <!-- Only when the new company ships to its own warehouses and the current one is not among them -->
          <v-select
            v-if="needsWarehouse"
            v-model="warehouseId"
            :items="warehouses"
            :item-title="(w: any) => w.displayName || w.name"
            item-value="id"
            label="Destination warehouse *"
            :hint="`The current warehouse (${po.destinationWarehouseName || 'none'}) is not linked to this company.`"
            persistent-hint
            prepend-inner-icon="mdi-warehouse"
            variant="outlined"
            :loading="warehousesLoading"
            :rules="[required]"
            class="mb-3"
          />

          <v-alert type="warning" variant="tonal" density="compact" icon="mdi-file-alert-outline">
            The PO and payment-request PDFs switch to the new company's branding. If the PO PDF was already
            sent to the supplier, send the new one.
          </v-alert>

          <div aria-live="polite">
            <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mt-3">{{ errorMessage }}</v-alert>
          </div>
        </v-card-text>

        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" :disabled="saving" @click="close(false)">Cancel</v-btn>
          <v-btn type="submit" color="primary" variant="flat" :loading="saving" :disabled="!changed">
            {{ selectedName ? `Move to ${selectedName}` : 'Change company' }}
          </v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import type { SupplierCreditCompanyBalance, SupplierCreditOption } from '~/utils/supplierCredits'

type PoLike = {
  id: number
  poNumber: string
  supplierId: number
  supplierName: string
  companyPresetId: number | null
  destinationWarehouseId: number | null
  destinationWarehouseName?: string | null
}

const props = defineProps<{ modelValue: boolean; po: PoLike }>()
const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  /** The refreshed PO returned by PATCH /purchase-orders/{id}/company-preset. */
  changed: [po: any]
}>()

const api = useApi()

const formRef = ref<any>(null)
const loading = ref(false)
const saving = ref(false)
const errorMessage = ref('')
const companies = shallowRef<SupplierCreditOption[]>([])
const balances = shallowRef<SupplierCreditCompanyBalance[]>([])
const companyId = ref<number | null>(null)
const warehouses = shallowRef<any[]>([])
const warehousesLoading = ref(false)
const warehouseId = ref<number | null>(null)

const companyRows = computed(() => companies.value.map(c => ({
  ...c,
  credit: balances.value.find(b => b.companyPresetId === c.id && b.kind !== 'CreditLine')?.balance ?? 0,
  line: balances.value.find(b => b.companyPresetId === c.id && b.kind === 'CreditLine')?.balance ?? 0,
})))
const selectedName = computed(() => companies.value.find(c => c.id === companyId.value)?.name ?? '')
const changed = computed(() => companyId.value != null && companyId.value !== props.po.companyPresetId)
// A company with linked warehouses may only ship to them (same rule as the API); none linked = any warehouse.
const needsWarehouse = computed(() => changed.value && warehouses.value.length > 0
  && !warehouses.value.some(w => w.id === props.po.destinationWarehouseId))

const required = (v: any) => (v !== null && v !== undefined && v !== '') || 'Required'

watch(() => props.modelValue, async (open) => {
  if (!open) return
  errorMessage.value = ''
  saving.value = false
  warehouses.value = []
  warehouseId.value = null
  companyId.value = props.po.companyPresetId
  formRef.value?.resetValidation?.()
  loading.value = true
  try {
    const [list, credit, lines] = await Promise.all([
      api.get<SupplierCreditOption[]>('/supplier-credits/lookup/companies'),
      // Balances are a hint only; without access the dialog still works.
      api.get<SupplierCreditCompanyBalance[]>(`/supplier-credits/balances?supplierId=${props.po.supplierId}`).catch(() => []),
      api.get<SupplierCreditCompanyBalance[]>(`/supplier-credits/balances?supplierId=${props.po.supplierId}&kind=CreditLine`).catch(() => []),
    ])
    companies.value = list
    balances.value = [...credit, ...lines]
    // Pre-select the company that can use the most (prepaid + credit line), when it is not the current one.
    const usable = new Map<number, number>()
    for (const b of balances.value) usable.set(b.companyPresetId, (usable.get(b.companyPresetId) ?? 0) + b.balance)
    const best = [...usable.entries()].sort((a, b) => b[1] - a[1])[0]
    if (best && best[0] !== props.po.companyPresetId) companyId.value = best[0]
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'Our companies could not be loaded.')
  } finally {
    loading.value = false
  }
}, { immediate: true })

watch(companyId, async (id) => {
  warehouseId.value = null
  warehouses.value = []
  if (!id || id === props.po.companyPresetId) return
  warehousesLoading.value = true
  try { warehouses.value = await api.get<any[]>(`/companypresets/${id}/warehouses`) }
  catch { warehouses.value = [] } // the API still enforces the rule and explains it on save
  finally { warehousesLoading.value = false }
})

function close(value = false) {
  if (saving.value) return
  emit('update:modelValue', value)
}

async function submit() {
  if (saving.value || !changed.value) return
  const { valid } = await formRef.value.validate()
  if (!valid) return
  saving.value = true
  errorMessage.value = ''
  try {
    const result = await api.patch<any>(`/purchase-orders/${props.po.id}/company-preset`, {
      companyPresetId: companyId.value,
      destinationWarehouseId: needsWarehouse.value ? warehouseId.value : null,
    })
    saving.value = false
    emit('changed', result)
    emit('update:modelValue', false)
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'The company could not be changed.')
    saving.value = false
  }
}
</script>
