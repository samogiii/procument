<template>
  <div v-if="suggestion" class="mb-3">
    <!-- 1. The PO's own company can pay with prepaid credit and/or its credit line -->
    <v-alert v-if="ownOptions.length" type="success" variant="tonal" icon="mdi-cash-refund">
      <div v-if="suggestion.usableBalance > 0" class="font-weight-bold">
        {{ suggestion.companyPresetName }} has ${{ formatPrice(suggestion.usableBalance) }} prepaid credit with {{ suggestion.supplierName }}.
      </div>
      <div v-if="suggestion.creditLine && suggestion.creditLine.available > 0" class="font-weight-bold">
        {{ suggestion.companyPresetName }} has a credit line with {{ suggestion.supplierName }}:
        ${{ formatPrice(suggestion.creditLine.available) }} available of ${{ formatPrice(suggestion.creditLine.creditLimit) }}.
      </div>

      <div v-if="!suggestion.canApply" class="text-body-2 mt-1">{{ suggestion.cannotApplyReason }}</div>
      <div v-else-if="!suggestion.prOptions.length" class="text-body-2 mt-1">Nothing is left to pay on this PO.</div>

      <v-form v-else ref="formRef" class="mt-3" @submit.prevent="askConfirm">
        <v-radio-group v-if="ownOptions.length > 1" v-model="kind" label="Pay with" inline hide-details class="mb-2">
          <v-radio v-for="o in ownOptions" :key="o.kind" :value="o.kind"
            :label="`${o.kind === 'CreditLine' ? 'Credit line' : 'Prepaid credit'} ($${formatPrice(o.available)})`" />
        </v-radio-group>
        <div class="d-flex flex-wrap align-start gap-3">
          <v-select
            v-model="paymentRequestId"
            :items="prItems"
            item-title="label"
            item-value="id"
            label="Payment request *"
            density="compact"
            variant="outlined"
            bg-color="surface"
            style="min-width: 220px; max-width: 320px;"
          />
          <v-text-field
            v-model.number="amount"
            type="number"
            inputmode="decimal"
            min="0.01"
            step="0.01"
            prefix="$"
            label="Credit to use (USD) *"
            density="compact"
            variant="outlined"
            bg-color="surface"
            :rules="[amountRule]"
            :hint="`Up to $${formatPrice(maxAmount)}`"
            persistent-hint
            style="min-width: 180px; max-width: 220px;"
          />
          <v-btn type="submit" :color="kind === 'CreditLine' ? 'indigo' : 'success'" variant="flat" prepend-icon="mdi-cash-check" class="mt-1">
            {{ kind === 'CreditLine' ? 'Use credit line' : 'Apply credit' }}
          </v-btn>
        </div>
        <div v-if="kind === 'CreditLine'" class="text-caption mt-2">
          <v-icon icon="mdi-alert-outline" size="14" /> This amount pays the PO now and must be <strong>paid back</strong> to
          {{ suggestion.supplierName }} later (Supplier Credits → Credit lines).
        </div>
        <div v-if="suggestion.walletPaymentInProgress" class="text-caption mt-2">
          A wallet payment is in progress on this PO. The credit is recorded now; the PR status updates when that payment's final amount is saved.
        </div>
      </v-form>
    </v-alert>

    <v-alert v-else-if="suggestion.creditLine && suggestion.creditLine.owed > 0" type="warning" variant="tonal" density="compact"
      icon="mdi-credit-card-clock-outline" class="mb-2">
      {{ suggestion.companyPresetName }}'s credit line with {{ suggestion.supplierName }} is fully used —
      ${{ formatPrice(suggestion.creditLine.owed) }} must be paid back before it can be used again.
    </v-alert>

    <!-- 2. Only another of our companies holds credit with this supplier -->
    <v-alert v-if="!ownOptions.length && suggestion.otherCompanies.length" type="info" variant="tonal" icon="mdi-swap-horizontal">
      <div class="text-body-2">
        <template v-if="suggestion.companyPresetName">This PO is under <strong>{{ suggestion.companyPresetName }}</strong>.</template>
        <template v-else>This PO has no buying company, so no credit can be used.</template>
        <span v-for="(c, i) in suggestion.otherCompanies" :key="`${c.companyPresetId}-${c.kind}`">
          {{ i === 0 ? ' ' : ', ' }}<strong>{{ c.companyPresetName }}</strong> has
          <template v-if="c.kind === 'CreditLine'">a credit line (${{ formatPrice(c.balance) }} available)</template>
          <template v-else>${{ formatPrice(c.balance) }} prepaid credit</template>
        </span>
        with {{ suggestion.supplierName }}.
      </div>
      <div class="mt-2">
        <v-btn v-if="suggestion.canChangeCompany && canChangeCompanyRole" size="small" variant="tonal" color="primary"
          prepend-icon="mdi-domain-switch" :to="`/purchase-orders/${suggestion.poId}?changeCompany=1`">
          Change PO company
        </v-btn>
        <span v-else-if="!suggestion.canChangeCompany" class="text-caption">
          The company cannot be changed: {{ suggestion.changeCompanyBlockedReason }}
        </span>
        <span v-else class="text-caption">Ask an Admin or AHM to change the PO's company to use this credit.</span>
      </div>
    </v-alert>
    <!-- 3. No credit anywhere: render nothing -->

    <v-dialog v-model="confirmOpen" max-width="440" persistent>
      <v-card class="glass-card">
        <v-card-title class="pa-4">{{ kind === 'CreditLine' ? 'Use the credit line?' : 'Use supplier credit?' }}</v-card-title>
        <v-card-text class="pt-0 text-body-2">
          Use <strong>${{ formatPrice(Number(amount || 0)) }}</strong> of {{ suggestion.companyPresetName }}'s
          {{ kind === 'CreditLine' ? 'credit line' : 'credit' }} for <strong>{{ selectedPrLabel }}</strong>?
          <template v-if="kind === 'CreditLine'">
            Still available afterwards: <strong>${{ formatPrice((selectedOption?.available ?? 0) - Number(amount || 0)) }}</strong>.
            Owed to {{ suggestion.supplierName }} afterwards:
            <strong>${{ formatPrice((suggestion.creditLine?.owed ?? 0) + Number(amount || 0)) }}</strong> — to be paid back.
          </template>
          <template v-else>
            Remaining credit: <strong>${{ formatPrice((selectedOption?.available ?? 0) - Number(amount || 0)) }}</strong>.
          </template>
          <div aria-live="polite">
            <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mt-3">{{ errorMessage }}</v-alert>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" :disabled="applying" @click="confirmOpen = false">Cancel</v-btn>
          <v-btn :color="kind === 'CreditLine' ? 'indigo' : 'success'" variant="flat" :loading="applying" @click="apply">
            {{ kind === 'CreditLine' ? 'Use credit line' : 'Use credit' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import type { ApplySupplierCreditResult, SupplierCreditKind, SupplierCreditSuggestion } from '~/utils/supplierCredits'

const props = defineProps<{
  poId: number
  /** Bump after anything that changes what is left to pay (wallet draft saved, POP finished…). */
  refreshKey?: number
}>()
const emit = defineEmits<{ applied: [result: ApplySupplierCreditResult] }>()

const api = useApi()
const authStore = useAuthStore()

const suggestion = ref<SupplierCreditSuggestion | null>(null)
const paymentRequestId = ref<number | null>(null)
const amount = ref<number | null>(null)
const kind = ref<SupplierCreditKind>('Prepaid')
const formRef = ref<any>(null)
const confirmOpen = ref(false)
const applying = ref(false)
const errorMessage = ref('')
let requestId = ''

// Changing a PO's company is limited to these roles on the API (plan §3.3).
const canChangeCompanyRole = computed(() => ['SuperAdmin', 'Admin', 'AHM'].includes(authStore.user?.role ?? ''))

const prItems = computed(() => (suggestion.value?.prOptions ?? []).map(p => ({
  id: p.paymentRequestId,
  label: `PR-${p.prNumber ?? p.paymentRequestId} · $${formatPrice(p.remaining)} left`,
  remaining: p.remaining,
})))
const selectedPr = computed(() => prItems.value.find(p => p.id === paymentRequestId.value))
/** What the PO's own company can pay with: prepaid credit first, then the credit line. */
const ownOptions = computed(() => {
  const s = suggestion.value
  const list: { kind: SupplierCreditKind; available: number }[] = []
  if (!s) return list
  if (s.usableBalance > 0) list.push({ kind: 'Prepaid', available: s.usableBalance })
  if (s.creditLine && s.creditLine.available > 0) list.push({ kind: 'CreditLine', available: s.creditLine.available })
  return list
})
const selectedOption = computed(() => ownOptions.value.find(o => o.kind === kind.value))
const selectedPrLabel = computed(() => selectedPr.value?.label.split(' · ')[0] ?? 'this PR')
const maxAmount = computed(() => {
  const s = suggestion.value
  if (!s || !selectedPr.value) return 0
  return Math.min(selectedOption.value?.available ?? 0, selectedPr.value.remaining, s.poRemaining)
})
const amountRule = (v: any) => {
  const n = Number(v)
  if (!(n > 0)) return 'Enter a positive amount'
  if (Math.round(n * 100) / 100 !== n) return 'At most two decimal places'
  return n <= maxAmount.value + 1e-9 || `At most $${formatPrice(maxAmount.value)}`
}

let loadSeq = 0
async function load() {
  const seq = ++loadSeq
  try {
    const s = await api.get<SupplierCreditSuggestion>(`/supplier-credits/po/${props.poId}/suggestion`)
    if (seq !== loadSeq) return
    suggestion.value = s
    kind.value = s.usableBalance > 0 ? 'Prepaid' : 'CreditLine'
    paymentRequestId.value = s.prOptions[0]?.paymentRequestId ?? null
    const suggested = kind.value === 'Prepaid' ? s.suggestedAmount : (s.creditLine?.suggestedAmount ?? 0)
    amount.value = suggested > 0 ? suggested : null
  } catch {
    // Users without access to Supplier Credits, or an API error: the payment flow works as before.
    if (seq === loadSeq) suggestion.value = null
  }
}
watch(() => [props.poId, props.refreshKey] as const, load, { immediate: true })

// Picking another PR or another way to pay re-suggests the most that fits.
watch([paymentRequestId, kind], () => {
  if (maxAmount.value > 0) amount.value = maxAmount.value
})

async function askConfirm() {
  const { valid } = await formRef.value.validate()
  if (!valid || !paymentRequestId.value) return
  errorMessage.value = ''
  requestId = newRequestId() // one key per confirmation; retries inside the dialog reuse it
  confirmOpen.value = true
}

async function apply() {
  if (applying.value || !paymentRequestId.value) return
  applying.value = true
  errorMessage.value = ''
  try {
    const result = await api.post<ApplySupplierCreditResult>(`/supplier-credits/po/${props.poId}/apply`, {
      paymentRequestId: paymentRequestId.value,
      amount: amount.value,
      requestId,
      kind: kind.value,
    })
    confirmOpen.value = false
    emit('applied', result)
    await load()
  } catch (e: any) {
    errorMessage.value = apiErrorMessage(e, 'The credit could not be applied.')
  } finally {
    applying.value = false
  }
}
</script>
