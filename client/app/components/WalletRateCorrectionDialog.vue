<template>
  <v-dialog :model-value="modelValue" max-width="520" @update:model-value="$emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="pa-4 text-h6">Correct exchange rate</v-card-title>
      <v-divider />
      <v-card-text class="pa-4">
        <div v-if="transaction" class="mb-4">
          <div class="text-body-2">
            Transaction amount:
            <strong>{{ currencySymbol(transactionCurrency) }}{{ formatPrice(amount) }} {{ transactionCurrency }}</strong>
          </div>
          <div class="text-caption text-medium-emphasis">
            Current rate: {{ transaction.exchangeRate ?? '1:1' }}
          </div>
        </div>

        <v-alert v-if="sameCurrency" type="info" variant="tonal" density="compact" class="mb-4">
          This transaction already uses {{ walletCurrency }}. Its corrected rate will remain 1:1.
        </v-alert>

        <v-text-field
          v-else
          v-model.number="newRate"
          label="New exchange rate"
          type="number"
          min="0.000001"
          step="0.000001"
          variant="outlined"
          density="comfortable"
          autofocus
        />

        <v-textarea
          v-model="note"
          label="Correction note (optional)"
          rows="2"
          variant="outlined"
          density="comfortable"
        />

        <v-card variant="tonal" class="pa-3 mb-3">
          <div class="text-caption text-medium-emphasis">Wallet debit becomes</div>
          <div class="text-h6">{{ currencySymbol(walletCurrency) }}{{ formatPrice(walletDebit) }}</div>
          <div v-if="linkedBankFee" class="text-body-2 mt-2" :class="previewBankFee < 0 ? 'text-error' : ''">
            Bank fee becomes {{ currencySymbol(walletCurrency) }}{{ formatPrice(previewBankFee) }}
          </div>
        </v-card>

        <v-alert v-if="previewBankFee < 0" type="error" variant="tonal" density="compact">
          This rate would make the linked bank fee negative.
        </v-alert>
        <v-alert v-if="error" type="error" variant="tonal" density="compact">{{ error }}</v-alert>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="$emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" :loading="saving" :disabled="!canSave" @click="save">Save correction</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { formatPrice } from '~/utils/formatPrice'

interface RateTransaction {
  id: number
  deposit: number | null
  withdraw: number | null
  txCurrency: string | null
  exchangeRate: number | null
}

const props = defineProps<{
  modelValue: boolean
  boxId: number
  walletCurrency: string
  transaction: RateTransaction | null
  linkedBankFee?: RateTransaction | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: [response: any]
}>()

const api = useApi()
const newRate = ref<number | null>(null)
const note = ref('')
const saving = ref(false)
const error = ref('')

const amount = computed(() => props.transaction?.deposit ?? props.transaction?.withdraw ?? 0)
const transactionCurrency = computed(() => props.transaction?.txCurrency || props.walletCurrency)
const sameCurrency = computed(() => transactionCurrency.value.toUpperCase() === props.walletCurrency.toUpperCase())
const effectiveRate = computed(() => sameCurrency.value ? 1 : Number(newRate.value || 0))
const walletDebit = computed(() => Math.round(amount.value * effectiveRate.value * 100) / 100)
const previewBankFee = computed(() => {
  if (!props.transaction || !props.linkedBankFee) return 0
  const oldInitial = Math.round(amount.value * (props.transaction.exchangeRate ?? 1) * 100) / 100
  const feeAmount = props.linkedBankFee.withdraw ?? props.linkedBankFee.deposit ?? 0
  const oldFee = Math.round(feeAmount * (props.linkedBankFee.exchangeRate ?? 1) * 100) / 100
  return Math.round((oldInitial + oldFee - walletDebit.value) * 100) / 100
})
const canSave = computed(() => !!props.transaction && (sameCurrency.value || Number(newRate.value) > 0) && previewBankFee.value >= 0)

watch(() => [props.modelValue, props.transaction] as const, ([open, tx]) => {
  if (!open || !tx) return
  newRate.value = sameCurrency.value ? null : tx.exchangeRate
  note.value = ''
  error.value = ''
}, { immediate: true })

function currencySymbol(c: string) {
  return ({ USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' } as Record<string, string>)[c] ?? c
}

async function save() {
  if (!props.transaction || !canSave.value) return
  saving.value = true
  error.value = ''
  try {
    const response = await api.patch(`/payment-boxes/${props.boxId}/transactions/${props.transaction.id}/exchange-rate`, {
      exchangeRate: sameCurrency.value ? null : newRate.value,
      note: note.value.trim() || null,
    })
    emit('saved', response)
    emit('update:modelValue', false)
  } catch (e: any) {
    error.value = e?.data?.message || e?.response?._data?.message || 'Failed to correct the exchange rate.'
  } finally {
    saving.value = false
  }
}
</script>
