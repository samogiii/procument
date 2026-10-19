<template>
  <!-- Supplier quotes for one RFQ item: the ones on this RFQ, and older ones for the same part on other RFQs. -->
  <div class="qh">
    <div class="qh__bar" @click="open = !open">
      <v-icon icon="mdi-history" size="16" class="mr-1 text-medium-emphasis" />
      <span class="text-caption font-weight-bold mr-2">Supplier quotes</span>
      <v-btn-toggle v-model="view" mandatory density="compact" variant="text" divided class="qh__toggle" @click.stop>
        <v-btn value="older" size="x-small">Older RFQs ({{ olderCount }})</v-btn>
        <v-btn value="this" size="x-small">This RFQ ({{ rfqQuotes.length }})</v-btn>
      </v-btn-toggle>
      <!-- Collapsed: best (lowest) older price per condition -->
      <div v-if="!open && best.length" class="d-flex flex-wrap gap-1 ml-2">
        <v-chip v-for="b in best" :key="b.condition" size="x-small" variant="tonal" color="primary" :title="`Lowest ${b.condition || '—'} price on older RFQs: ${b.supplierName}, ${fmtDate(b.date)}`">
          {{ b.condition || '—' }} best {{ fmtMoney(b.price) }}
        </v-chip>
      </div>
      <v-spacer />
      <v-btn :icon="open ? 'mdi-chevron-up' : 'mdi-chevron-down'" size="x-small" variant="text" />
    </div>

    <v-expand-transition>
      <div v-if="open">
        <div v-if="conditions.length > 1" class="d-flex flex-wrap gap-1 px-3 pb-2">
          <v-chip
            v-for="c in ['all', ...conditions]"
            :key="c"
            size="x-small"
            :variant="cond === c ? 'flat' : 'outlined'"
            :color="cond === c ? 'primary' : undefined"
            @click="cond = c"
          >
            {{ c === 'all' ? 'All conditions' : c || '—' }}
          </v-chip>
        </div>
        <div class="grid-scroll">
          <table class="qh__table">
            <thead>
              <tr>
                <th>Date</th>
                <th v-if="view === 'older'">RFQ</th>
                <th>Supplier</th>
                <th>P/N · Alt</th>
                <th>Cond</th>
                <th class="num">Qty</th>
                <th class="num">Price</th>
                <th>Cert</th>
                <th>Tag Date</th>
                <th>Lead Time</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(q, i) in rows" :key="i">
                <td class="nowrap">{{ fmtDate(q.date) }}</td>
                <td v-if="view === 'older'" class="nowrap">
                  <NuxtLink v-if="q.rfqId" :to="`/rfqs/${q.rfqId}`" class="text-primary">{{ q.rfqName }}</NuxtLink>
                  <span v-if="q.customerCode" class="text-caption text-medium-emphasis"> · {{ q.customerCode }}</span>
                </td>
                <td class="nowrap font-weight-medium">{{ q.supplierName }}</td>
                <td class="nowrap">
                  <span v-if="q.partNumber && q.partNumber !== partNumber" class="text-info">{{ q.partNumber }}</span>
                  <span v-if="q.alt" class="text-medium-emphasis"> · {{ q.alt }}</span>
                </td>
                <td>{{ q.condition }}</td>
                <td class="num">{{ q.qty }}</td>
                <td class="num font-weight-bold" :class="{ 'text-success': isBest(q) }">{{ fmtMoney(q.price) }}</td>
                <td class="nowrap">{{ q.certName }}</td>
                <td class="nowrap">{{ q.tagDate ? fmtDate(q.tagDate) : '' }}</td>
                <td class="nowrap">{{ q.leadTime }}</td>
              </tr>
              <tr v-if="!rows.length">
                <td :colspan="view === 'older' ? 10 : 9" class="text-center text-medium-emphasis py-3">
                  {{ view === 'older' ? 'No quotes for this part on older RFQs.' : 'No supplier quotes on this RFQ yet.' }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="view === 'older' && olderCount > olderQuotes.length" class="text-caption text-medium-emphasis px-3 py-1">
          Newest {{ olderQuotes.length }} of {{ olderCount }} shown.
        </div>
      </div>
    </v-expand-transition>
  </div>
</template>

<script setup lang="ts">
import type { PpRfqQuote } from '~/utils/procurementPlus'

const props = withDefaults(defineProps<{
  partNumber: string
  rfqQuotes?: PpRfqQuote[]
  olderQuotes?: PpRfqQuote[]
  olderCount?: number
  startOpen?: boolean
}>(), { rfqQuotes: () => [], olderQuotes: () => [], olderCount: 0, startOpen: false })

const open = ref(props.startOpen)
const view = ref<'older' | 'this'>(props.olderQuotes.length || !props.rfqQuotes.length ? 'older' : 'this')
const cond = ref('all')

const source = computed(() => (view.value === 'older' ? props.olderQuotes : props.rfqQuotes))
const conditions = computed(() => [...new Set(source.value.map(q => (q.condition || '').toUpperCase()))].sort())
const rows = computed(() => cond.value === 'all' ? source.value : source.value.filter(q => (q.condition || '').toUpperCase() === cond.value))
watch(view, () => { cond.value = 'all' })

/** Lowest older price per condition. */
const best = computed(() => {
  const map = new Map<string, PpRfqQuote>()
  for (const q of props.olderQuotes) {
    const c = (q.condition || '').toUpperCase()
    const cur = map.get(c)
    if (!cur || Number(q.price) < Number(cur.price)) map.set(c, q)
  }
  return [...map.entries()].map(([condition, q]) => ({ condition, price: q.price, supplierName: q.supplierName, date: q.date }))
})
const isBest = (q: PpRfqQuote) => view.value === 'older'
  && best.value.some(b => b.condition === (q.condition || '').toUpperCase() && Number(b.price) === Number(q.price))
</script>

<style scoped>
.qh { border-top: 1px dashed rgba(var(--v-border-color), 0.15); background: rgba(var(--v-theme-on-surface), 0.015); }
.qh__bar { display: flex; align-items: center; flex-wrap: wrap; gap: 4px; padding: 6px 12px; cursor: pointer; }
.qh__toggle { height: 24px !important; }
.grid-scroll { overflow-x: auto; max-height: 320px; overflow-y: auto; }
.qh__table { width: 100%; border-collapse: collapse; font-size: 12.5px; }
.qh__table th {
  text-align: left; font-size: 11px; font-weight: 600; opacity: 0.65; padding: 6px 10px; white-space: nowrap;
  position: sticky; top: 0; background: rgb(var(--v-theme-surface)); z-index: 1;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
}
.qh__table td { padding: 5px 10px; border-bottom: 1px solid rgba(var(--v-border-color), 0.05); }
.num { text-align: right; white-space: nowrap; }
.nowrap { white-space: nowrap; }
</style>
