<template>
  <!-- Final price of a supplier payment: wallet debit (USD amount × rate) + handling + wire fee -->
  <div class="pa-3 rounded calc">
    <div class="d-flex justify-space-between">
      <span class="text-medium-emphasis">{{ rate ? `$${fmt(amount)} × ${rate}` : 'Amount paid' }}</span>
      <span>{{ sym }}{{ fmt(debit) }}</span>
    </div>
    <div class="d-flex justify-space-between mt-1">
      <span class="text-medium-emphasis">+ Handling fee</span>
      <span>{{ sym }}{{ fmt(handling) }}</span>
    </div>
    <div class="d-flex justify-space-between mt-1">
      <span class="text-medium-emphasis">+ Wire fee</span>
      <span>{{ sym }}{{ fmt(wire) }}</span>
    </div>
    <v-divider class="my-2" />
    <div class="d-flex justify-space-between font-weight-bold">
      <span>Final price</span>
      <span class="text-success">{{ sym }}{{ fmt(final) }} {{ currency }}</span>
    </div>
    <div v-if="rate" class="d-flex justify-space-between text-body-2 mt-1">
      <span class="text-medium-emphasis">{{ sym }}{{ fmt(final) }} × (1 / {{ rate }})</span>
      <span>= ${{ fmt(final / rate) }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  amount: number
  currency: string
  /** Wallet currency per 1 USD; null/1 for USD wallets. */
  rate?: number | null
  /** The wallet debit (amount × rate, rounded to cents) as recorded by the server. */
  debit: number
  handling?: number | null
  wire?: number | null
}>()

const SYMBOLS: Record<string, string> = { USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' }
const sym = computed(() => SYMBOLS[props.currency] ?? `${props.currency} `)
const rate = computed(() => (props.rate && props.rate !== 1 ? props.rate : null))
const final = computed(() => Math.round((Number(props.debit || 0) + Number(props.handling || 0) + Number(props.wire || 0)) * 100) / 100)
const fmt = (v: unknown) => Number(v || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
</script>

<style scoped>
.calc {
  background-color: rgba(var(--v-theme-on-surface), 0.04);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  font-variant-numeric: tabular-nums;
}
</style>
