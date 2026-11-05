<template>
  <v-card variant="tonal" :color="color" class="pa-3">
    <div class="d-flex align-center gap-2 mb-1">
      <v-icon :icon="icon" size="18" />
      <span class="text-subtitle-2">{{ title }}</span>
      <v-spacer />
      <v-btn
        v-if="reading && reading.status !== 'Waiting'"
        size="x-small" variant="text" prepend-icon="mdi-refresh" :loading="busy" @click="readAgain"
      >Read again</v-btn>
    </div>

    <div v-if="!reading" class="text-body-2 text-medium-emphasis">Not read yet.</div>
    <div v-else-if="reading.status === 'Waiting'" class="text-body-2">
      <v-progress-circular indeterminate size="14" width="2" class="mr-1" />
      The local AI is reading the POP… this page refreshes by itself.
      <span v-if="reading.error" class="text-error d-block mt-1">Last try: {{ reading.error }}</span>
    </div>
    <div v-else-if="reading.status === 'Off'" class="text-body-2">The POP reader is not set up (PopReader:Url). Fill the numbers in from the POP.</div>
    <div v-else-if="reading.status === 'Failed'" class="text-body-2">
      Could not read it: {{ reading.error || 'unknown error' }}. Fill the numbers in from the POP, or read it again.
    </div>
    <template v-else>
      <div class="text-body-2 reading-grid">
        <span class="text-medium-emphasis">Amount</span>
        <strong>{{ money(reading.amount) }} {{ reading.currency ?? '' }}</strong>
        <template v-if="reading.fees">
          <span class="text-medium-emphasis">{{ kind === 'Deposit' ? 'Fees (customer\'s)' : 'Fees on the POP' }}</span>
          <span>{{ money(reading.fees) }} {{ reading.feesCurrency ?? '' }}</span>
        </template>
        <template v-if="reading.totalDebited">
          <span class="text-medium-emphasis">Total debited</span>
          <span>{{ money(reading.totalDebited) }} {{ reading.totalDebitedCurrency ?? '' }}</span>
        </template>
        <template v-if="reading.receivedAmount">
          <span class="text-medium-emphasis">Received</span>
          <span>{{ money(reading.receivedAmount) }} {{ reading.receivedCurrency ?? '' }}</span>
        </template>
        <template v-if="reading.exchangeRate">
          <span class="text-medium-emphasis">Rate on the POP</span><span>{{ reading.exchangeRate }}</span>
        </template>
        <template v-if="reading.chargesOption">
          <span class="text-medium-emphasis">Charges</span><span>{{ reading.chargesOption }}</span>
        </template>
        <template v-if="reading.valueDate">
          <span class="text-medium-emphasis">Date</span><span>{{ reading.valueDate }}</span>
        </template>
        <template v-if="reading.reference">
          <span class="text-medium-emphasis">Reference</span><span class="text-truncate">{{ reading.reference }}</span>
        </template>
        <template v-if="reading.confidence != null">
          <span class="text-medium-emphasis">Sure</span>
          <span :class="reading.confidence < 0.6 ? 'text-error font-weight-bold' : ''">{{ Math.round(reading.confidence * 100) }}%</span>
        </template>
      </div>
      <div v-if="reading.isPaymentProof === false" class="text-body-2 text-error mt-1">The AI says this may not be a payment proof.</div>
      <div v-if="reading.note" class="text-caption mt-1">AI: {{ reading.note }}</div>
    </template>

    <!-- The POP's price against the one set (a yuan POP for a dollar payment goes through the rate) -->
    <v-sheet v-if="check" rounded class="pa-2 mt-2 check" :color="check.matches ? 'success' : 'warning'" variant="tonal">
      <div class="d-flex align-center gap-1 text-body-2 font-weight-medium">
        <v-icon :icon="check.matches ? 'mdi-check-circle-outline' : 'mdi-alert-outline'" size="16" />
        {{ check.matches ? 'POP price matches' : 'POP price is different' }}
      </div>
      <div v-if="check.rate" class="text-body-2">
        POP {{ money(check.popAmount) }} {{ check.popCurrency }} at {{ check.rateText }}
        = <strong>{{ money(check.inSetCurrency) }} {{ check.setCurrency }}</strong>
      </div>
      <div v-else class="text-body-2">POP <strong>{{ money(check.popAmount) }} {{ check.popCurrency }}</strong></div>
      <div class="text-body-2">
        Set {{ money(check.setAmount) }} {{ check.setCurrency }}
        <template v-if="check.difference !== 0">
          · difference <strong>{{ check.difference > 0 ? '+' : '−' }}{{ money(Math.abs(check.difference)) }} {{ check.setCurrency }}</strong>
        </template>
      </div>
      <div v-if="check.popRate" class="text-caption text-medium-emphasis">
        The POP works out at {{ check.popRate }} {{ check.popCurrency }} per 1 {{ check.setCurrency }}.
      </div>
    </v-sheet>

    <ul v-if="notes?.length" class="text-caption mt-2 ml-4">
      <li v-for="(n, i) in notes" :key="i">{{ n }}</li>
    </ul>
  </v-card>
</template>

<script setup lang="ts">
export type PopReading = {
  status: 'Waiting' | 'Done' | 'Failed' | 'Off'
  error?: string | null
  amount?: number | null
  currency?: string | null
  fees?: number | null
  feesCurrency?: string | null
  totalDebited?: number | null
  totalDebitedCurrency?: string | null
  receivedAmount?: number | null
  receivedCurrency?: string | null
  exchangeRate?: number | null
  chargesOption?: string | null
  valueDate?: string | null
  reference?: string | null
  confidence?: number | null
  note?: string | null
  isPaymentProof?: boolean | null
  readAt?: string | null
}

/** PopFees.PopCheck: the POP's price next to the one set, converted with the rate when the POP is in the other currency. */
export type PopCheck = {
  popAmount: number
  popCurrency: string
  rate: number | null
  rateText: string | null
  inSetCurrency: number
  setAmount: number
  setCurrency: string
  difference: number
  popRate: number | null
  matches: boolean
}

const props = defineProps<{
  kind: 'Deposit' | 'Transfer' | 'Withdraw' | 'Payment'
  refId: number
  reading?: PopReading | null
  /** PopFees' remarks on the proposal. */
  notes?: string[] | null
  check?: PopCheck | null
}>()
const emit = defineEmits<{ queued: [] }>()

const api = useApi()
const busy = ref(false)

const title = computed(() => 'Read by the local AI')
const color = computed(() => {
  const r = props.reading
  if (!r || r.status === 'Waiting') return 'info'
  if (r.status !== 'Done') return 'warning'
  return (r.confidence != null && r.confidence < 0.6) || r.isPaymentProof === false || props.notes?.length || props.check?.matches === false
    ? 'warning' : 'success'
})
const icon = computed(() => (props.reading?.status === 'Done' ? 'mdi-robot-outline' : props.reading?.status === 'Waiting' ? 'mdi-robot-happy-outline' : 'mdi-robot-confused-outline'))

function money(v: number | null | undefined) {
  return v == null ? '—' : Number(v).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

async function readAgain() {
  busy.value = true
  try {
    await api.post(`/pop-readings/${props.kind}/${props.refId}/read-again`, {})
    emit('queued')
  } finally {
    busy.value = false
  }
}
</script>

<style scoped>
.reading-grid { display: grid; grid-template-columns: auto 1fr; column-gap: 12px; row-gap: 2px; }
</style>
