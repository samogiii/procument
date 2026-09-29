<template>
  <v-dialog :model-value="modelValue" max-width="640" scrollable @update:model-value="close">
    <v-card v-if="account" class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-cash-refund" class="mr-2" color="teal" />
        Pay back credit line
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" aria-label="Close" @click="close(false)" />
      </v-card-title>
      <v-divider />

      <v-card-text style="max-height: 75vh">
        <!-- Where we stand with this supplier -->
        <div class="d-flex flex-wrap gap-6 mb-4">
          <div>
            <div class="text-caption text-medium-emphasis">{{ account.supplierName }} · {{ account.companyPresetName }}</div>
            <div class="text-h6 font-weight-bold" :class="account.owed > 0 ? 'text-error' : 'text-success'">
              Owed ${{ formatPrice(account.owed) }}
            </div>
          </div>
          <div>
            <div class="text-caption text-medium-emphasis">Limit</div>
            <div class="text-body-1">${{ formatPrice(account.creditLimit ?? 0) }}</div>
          </div>
          <div>
            <div class="text-caption text-medium-emphasis">Available</div>
            <div class="text-body-1">${{ formatPrice(account.balance) }}</div>
          </div>
        </div>

        <v-progress-linear v-if="loading" indeterminate color="teal" class="mb-3" />

        <!-- Step 3: the POP is saved, the bank's final amount is still missing -->
        <template v-else-if="open?.status === 'WAITING FOR FINAL AMOUNT'">
          <v-alert type="warning" variant="tonal" density="compact" class="mb-3">
            PR-{{ open.prNumber }}: the POP is saved and ${{ formatPrice(open.amount) }} has been credited back.
            Enter the bank's final withdrawn amount to finish.
          </v-alert>
          <v-form ref="finalFormRef" @submit.prevent="saveFinalAmount">
            <v-text-field
              v-model.number="finalAmount"
              type="number"
              inputmode="decimal"
              min="0.01"
              step="0.01"
              :prefix="currencySymbol(open.walletCurrency)"
              :label="`Final withdrawn amount (${open.walletCurrency}) *`"
              :hint="`Original POP debit: ${formatPrice(open.walletAmount ?? 0)} ${open.walletCurrency}`"
              persistent-hint
              variant="outlined"
              :rules="[finalRule]"
              class="mb-3"
            />
            <v-alert v-if="finalFee >= 0" type="info" variant="tonal" density="compact" class="mb-3">
              Bank Fee and Others: <strong>{{ formatPrice(finalFee) }} {{ open.walletCurrency }}</strong>
            </v-alert>
            <v-btn type="submit" color="success" variant="flat" prepend-icon="mdi-check-circle" :loading="saving">
              Save final amount &amp; finish
            </v-btn>
          </v-form>
        </template>

        <v-alert v-else-if="account.owed <= 0 && !open" type="success" variant="tonal" density="compact" class="mb-3">
          Nothing is owed on this credit line.
        </v-alert>

        <!-- Steps 1 and 2: repayment details, then the POP -->
        <template v-else>
          <v-form ref="draftFormRef" @submit.prevent="saveDraft">
            <div class="text-subtitle-2 mb-2">1. Repayment details</div>
            <v-text-field
              v-model.number="amount"
              type="number"
              inputmode="decimal"
              min="0.01"
              step="0.01"
              prefix="$"
              label="Amount to pay back (USD) *"
              :hint="`Owed: $${formatPrice(account.owed)}`"
              persistent-hint
              variant="outlined"
              :rules="[amountRule]"
              class="mb-3"
            />
            <v-select
              v-model="walletId"
              :items="wallets"
              item-title="label"
              item-value="id"
              label="Paid from wallet *"
              prepend-inner-icon="mdi-bank-outline"
              variant="outlined"
              :rules="[required]"
              class="mb-3"
            />
            <v-text-field
              v-if="selectedWallet && selectedWallet.currency !== 'USD'"
              v-model.number="exchangeRate"
              type="number"
              inputmode="decimal"
              min="0.000001"
              step="0.000001"
              :label="`${selectedWallet.currency} per 1 USD *`"
              variant="outlined"
              :rules="[rateRule]"
              class="mb-3"
            />
            <p v-if="selectedWallet" class="text-body-2 mb-3">
              Wallet debit: <strong>{{ currencySymbol(selectedWallet.currency) }}{{ formatPrice(walletDebit) }} {{ selectedWallet.currency }}</strong>
            </p>
            <v-btn type="submit" color="primary" variant="flat" prepend-icon="mdi-content-save" :loading="saving">
              Save repayment details
            </v-btn>
          </v-form>

          <template v-if="open?.status === 'READY FOR POP'">
            <v-divider class="my-4" />
            <div class="text-subtitle-2 mb-2">2. Upload the POP</div>
            <v-alert type="success" variant="tonal" density="compact" class="mb-3">
              Saved as PR-{{ open.prNumber }}: ${{ formatPrice(open.amount) }} from {{ open.walletName }}. You can close this and upload the POP later.
            </v-alert>
            <FileDropZone
              :on-upload="uploadPop"
              label="Drag the POP image here or click to browse"
              accept="image/*,application/pdf,.pdf,.png,.jpg,.jpeg,.webp"
              :multiple="false"
            />
          </template>
        </template>

        <div aria-live="polite">
          <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mt-3">{{ errorMessage }}</v-alert>
        </div>

        <!-- Earlier repayments -->
        <template v-if="finished.length">
          <v-divider class="my-4" />
          <div class="text-subtitle-2 mb-2">Paid back before</div>
          <div v-for="r in finished" :key="r.id" class="d-flex align-center gap-2 mb-2 pa-2 rounded repayment-row">
            <v-chip size="x-small" color="teal" variant="tonal">PR-{{ r.prNumber }}</v-chip>
            <div class="flex-grow-1 text-body-2">
              ${{ formatPrice(r.amount) }} · {{ formatPrice(r.walletAmount ?? 0) }} {{ r.walletCurrency }} from {{ r.walletName }}
              <span v-if="r.bankFee" class="text-medium-emphasis"> · fee {{ formatPrice(r.bankFee) }} {{ r.walletCurrency }}</span>
              <div class="text-caption text-medium-emphasis">{{ new Date(r.createdAt).toLocaleDateString() }}</div>
            </div>
            <v-btn v-if="r.popTransactionId" icon="mdi-eye-outline" size="small" variant="text"
              :aria-label="`Open the POP of PR-${r.prNumber}`" @click="openPop(r)" />
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>

  <DocPreviewModal
    :open="docPreview.open.value"
    :blob-url="docPreview.blobUrl.value"
    :file-name="docPreview.fileName.value"
    :mime-type="docPreview.mimeType.value"
    @close="docPreview.close()"
  />
</template>

<script setup lang="ts">
import type { SupplierCreditAccountRow, SupplierCreditRepaymentRow } from '~/utils/supplierCredits'

const props = defineProps<{ modelValue: boolean; account: SupplierCreditAccountRow | null }>()
const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  /** Something was saved: the list, the owed amount and the ledger should reload. */
  changed: [message: string]
}>()

const api = useApi()
const authStore = useAuthStore()
const docPreview = useDocPreview()

type Wallet = { id: number; label: string; currency: string }
const loading = ref(false)
const saving = ref(false)
const errorMessage = ref('')
const repayments = shallowRef<SupplierCreditRepaymentRow[]>([])
const wallets = shallowRef<Wallet[]>([])
const amount = ref<number | null>(null)
const walletId = ref<number | null>(null)
const exchangeRate = ref<number | null>(null)
const finalAmount = ref<number | null>(null)
const draftFormRef = ref<any>(null)
const finalFormRef = ref<any>(null)

/** The repayment still waiting for its POP or final amount (one per credit line). */
const open = computed(() => repayments.value.find(r => r.status !== 'PAID') ?? null)
const finished = computed(() => repayments.value.filter(r => r.status === 'PAID'))
const selectedWallet = computed(() => wallets.value.find(w => w.id === walletId.value))
const walletDebit = computed(() => Number(amount.value || 0) * (selectedWallet.value?.currency === 'USD' ? 1 : Number(exchangeRate.value || 0)))
const finalFee = computed(() => Number(finalAmount.value || 0) - Number(open.value?.walletAmount || 0))

const required = (v: any) => (v !== null && v !== undefined && v !== '') || 'Required'
const amountRule = (v: any) => {
  const n = Number(v)
  if (!(n > 0)) return 'Enter a positive amount'
  if (Math.round(n * 100) / 100 !== n) return 'At most two decimal places'
  return n <= (props.account?.owed ?? 0) + 1e-9 || `At most $${formatPrice(props.account?.owed ?? 0)} (what is owed)`
}
const rateRule = (v: any) => {
  const n = Number(v)
  if (!(n > 0)) return 'Enter a positive rate'
  return Math.round(n * 1e6) / 1e6 === n || 'At most six decimal places'
}
const finalRule = (v: any) => {
  const n = Number(v)
  if (!(n > 0) || Math.round(n * 100) / 100 !== n) return 'Enter the amount with at most two decimal places'
  return n >= Number(open.value?.walletAmount || 0) || 'Cannot be less than the original POP debit'
}

function currencySymbol(currency: string | null | undefined) {
  return currency === 'USD' ? '$' : ''
}

async function load() {
  if (!props.account) return
  loading.value = true
  errorMessage.value = ''
  try {
    const [rows, boxes] = await Promise.all([
      api.get<SupplierCreditRepaymentRow[]>(`/supplier-credits/${props.account.id}/repayments`),
      wallets.value.length ? Promise.resolve(null) : api.get<any[]>('/supplier-payments/wallets'),
    ])
    repayments.value = rows
    // Wallets are identified by their own name, as on the payment page.
    if (boxes) wallets.value = boxes.map((b: any) => ({ id: b.id, label: `${b.name} (${b.currency})`, currency: b.currency }))
    const o = open.value
    amount.value = o?.amount ?? props.account.owed
    walletId.value = o?.walletId ?? null
    exchangeRate.value = o?.exchangeRate ?? null
    finalAmount.value = o?.walletAmount ?? null
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'The repayments could not be loaded.')
  } finally {
    loading.value = false
  }
}

watch(() => [props.modelValue, props.account?.id] as const, ([isOpen]) => { if (isOpen) load() }, { immediate: true })
watch(walletId, () => {
  if (open.value?.walletId === walletId.value) return
  exchangeRate.value = null
})

function close(value = false) {
  if (saving.value) return
  emit('update:modelValue', value)
}

async function saveDraft() {
  if (!props.account || saving.value) return
  const { valid } = await draftFormRef.value.validate()
  if (!valid) return
  saving.value = true
  errorMessage.value = ''
  try {
    await api.post(`/supplier-credits/${props.account.id}/repayments/draft`, {
      walletId: walletId.value,
      amount: amount.value,
      exchangeRate: selectedWallet.value?.currency === 'USD' ? null : exchangeRate.value,
    })
    await load()
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'The repayment details could not be saved.')
  } finally {
    saving.value = false
  }
}

async function uploadPop(files: File[], onProgress: (done: number) => void) {
  if (!props.account || !open.value || files.length === 0) return
  errorMessage.value = ''
  try {
    const form = new FormData()
    form.append('file', files[0]!)
    const result = await api.post<any>(`/supplier-credits/${props.account.id}/repayments/${open.value.id}/pop`, form)
    onProgress(1)
    emit('changed', `POP saved — owed on this line is now $${formatPrice(Math.max(0, (props.account.creditLimit ?? 0) - result.balance))}. Save the bank's final amount when it is known.`)
    await load()
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'The POP could not be saved. The repayment details were kept.')
    throw e
  }
}

async function saveFinalAmount() {
  if (!props.account || !open.value || saving.value) return
  const { valid } = await finalFormRef.value.validate()
  if (!valid) return
  saving.value = true
  errorMessage.value = ''
  try {
    const row = await api.post<SupplierCreditRepaymentRow>(
      `/supplier-credits/${props.account.id}/repayments/${open.value.id}/final-amount`, { finalWalletAmount: finalAmount.value })
    emit('changed', row.bankFee
      ? `Repayment finished. ${formatPrice(row.bankFee)} ${row.walletCurrency} recorded as Bank Fee and Others.`
      : 'Repayment finished with no additional bank fee.')
    await load()
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'The final bank amount could not be saved.')
  } finally {
    saving.value = false
  }
}

/** Repayment POPs live with the other supplier POPs, so the wallet endpoint serves them. */
async function openPop(row: SupplierCreditRepaymentRow) {
  try {
    const blob = await $fetch<Blob>(`${api.baseURL}/supplier-payments/${row.popTransactionId}/file`, {
      responseType: 'blob', headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    docPreview.previewBlob(blob, row.popFileName || `pop-PR-${row.prNumber}`)
  } catch { errorMessage.value = 'The POP could not be opened.' }
}
</script>

<style scoped>
.repayment-row {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
