<template>
  <!--
    Procurement Plus — Add Commission: the picked suppliers one under another; under each, every customer whose RFQs
    ask for the part (C501 · RFQ 1, RFQ 2) gets one coef (= Coef 1 on the Add Quote page). A unit price typed by
    hand stays as typed (the coef is not changed); typing the coef again brings back the price from the coef.
    No coef rule for now: the 5K$ / 10K$ Total Price conditions, the Trusted supplier + customer MinCoef rule and the
    1.13 minimum are switched off (kept commented in commissionError).
    Saving locks Coef 1 on those quotes and takes the part off the list.
  -->
  <v-dialog :model-value="modelValue" max-width="1200" scrollable persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-percent-circle-outline" color="success" class="mr-2" />
        Add Commission · {{ part.partNumber }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" :disabled="saving" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-divider />
      <v-card-text>
        <!-- 5K$ / 10K$ conditions switched off for now (Sam 2026-10-28) — the old text, to roll back:
          One coef per supplier and customer. A Total Price (Unit Price × Qty) under {{ COMMISSION_MIN_TOTAL.toLocaleString() }}$ cannot get a commission.
          Trusted supplier + customer MinCoef: selling total ≥ {{ COMMISSION_THRESHOLD.toLocaleString() }}$ may
          use the MinCoef, below it the coef must be bigger. Otherwise at least {{ COMMISSION_DEFAULT_MIN }}. …
        -->
        <!-- Switched off for now too: "Trusted supplier + customer MinCoef: at least the MinCoef." and ", at least {{ COMMISSION_DEFAULT_MIN }}" -->
        <p class="text-caption text-medium-emphasis mb-3">
          One coef per supplier and customer. A unit price you type stays as typed. Saving locks Coef 1 on the
          Add Quote page and removes the part from your list.
        </p>

        <div v-for="block in blocks" :key="block.row.key" class="supplier-block mb-4">
          <div class="d-flex align-center flex-wrap gap-2 mb-2">
            <span class="text-subtitle-2 font-weight-bold">{{ block.row.supplierName }}</span>
            <!-- Trusted supplier rule switched off for now (Sam 2026-10-28):
            <v-chip v-if="block.row.supplierTrusted" size="x-small" color="success" variant="tonal" prepend-icon="mdi-shield-check-outline">Trusted</v-chip>
            <v-chip v-else size="x-small" variant="tonal" title="Not Trusted — the coef must be at least 1.13">Not trusted</v-chip>
            -->
            <span class="text-caption text-medium-emphasis">
              {{ block.row.condition || '—' }}{{ block.row.alt ? ` · alt ${block.row.alt}` : '' }}
            </span>
          </div>
          <table class="comm-grid">
            <thead>
              <tr>
                <th style="min-width: 120px;">Customer</th>
                <th style="width: 110px;">Coef</th>
                <th style="width: 130px;">Unit Price</th>
                <th style="min-width: 160px;">RFQ</th>
                <th>Qty</th>
                <th>Buy Price</th>
                <th>Total Buy</th>
                <th style="width: 120px;" title="Shipping cost of this RFQ quote — spread over the qty in the unit price">Shipping Cost</th>
                <th>Total Price</th>
                <th style="min-width: 200px;" />
              </tr>
            </thead>
            <!-- One coef per customer; one line per RFQ, each with its own Total Price (Unit Price × Qty) and check -->
            <tbody v-for="e in block.entries" :key="e.key" class="customer-group" :class="{ 'row--locked': e.locked }">
              <tr v-for="(r, i) in e.refs" :key="r.rfqItemId">
                <template v-if="i === 0">
                <td :rowspan="e.refs.length">
                  <!-- Customer code only — no customer names here (Sam) -->
                  <div class="font-weight-medium">{{ e.customerCode || '—' }}</div>
                  <!-- MinCoef rule switched off for now (Sam 2026-10-28):
                  <div class="text-caption text-medium-emphasis">
                    <span v-if="e.minCoef != null">MinCoef {{ e.minCoef }}</span><span v-else>no MinCoef</span>
                  </div>
                  -->
                </td>
                <td :rowspan="e.refs.length">
                  <input
                    class="comm-input"
                    type="number"
                    step="0.01"
                    min="0"
                    :value="e.coef ?? ''"
                    :readonly="e.locked"
                    placeholder="Coef"
                    @input="(ev: Event) => setCoef(e, (ev.target as HTMLInputElement).value)"
                  >
                </td>
                <td :rowspan="e.refs.length">
                  <input
                    class="comm-input comm-input--price"
                    type="number"
                    step="0.01"
                    min="0"
                    :value="unitPrice(e) ?? ''"
                    :readonly="e.locked"
                    :class="{ 'comm-input--custom': e.unitPrice != null }"
                    :title="e.unitPrice != null ? 'Typed by hand — typing the coef again brings back the price from the coef' : 'Unit Price = (Buy Price + Shipping / Qty) × Coef — type to set it by hand'"
                    @change="(ev: Event) => setUnitPrice(e, (ev.target as HTMLInputElement).value)"
                  >
                </td>
                </template>
                <td class="text-caption">
                  <NuxtLink :to="`/rfqs/${r.rfqId}`">{{ r.rfqName }}</NuxtLink>
                  <v-icon
                    v-if="commissionActive(r.commissionAt)"
                    icon="mdi-lock-outline"
                    size="12"
                    color="deep-purple-lighten-2"
                    class="ml-1"
                    :title="`Commission saved (coef ${r.coef1}) — active until ${commissionEnds(r.commissionAt!).toLocaleDateString()}`"
                  />
                </td>
                <td class="num">{{ r.quoteQty ?? r.qty }}</td>
                <td class="num">{{ fmtMoney(r.quotePrice) }}</td>
                <td class="num">{{ fmtMoney((Number(r.quotePrice) || 0) * (Number(r.quoteQty) || 1)) }}</td>
                <td>
                  <input
                    class="comm-input"
                    type="number"
                    step="0.01"
                    min="0"
                    placeholder="0"
                    :value="r.quoteShippingCost ?? ''"
                    :readonly="e.locked"
                    title="Shipping cost — added to the price as Shipping / Qty per piece, saved on the quote"
                    @input="(ev: Event) => setShipping(r, (ev.target as HTMLInputElement).value)"
                  >
                </td>
                <td class="num font-weight-bold">{{ e.coef ? fmtMoney(commissionTotal([r], e.coef, e.unitPrice)) : '—' }}</td>
                <td>
                  <span v-if="e.locked" class="text-caption text-medium-emphasis">Commission saved — only SuperAdmin can change it</span>
                  <span v-else-if="commissionError(r, block.row.supplierTrusted, e.coef)" class="text-caption text-error">{{ commissionError(r, block.row.supplierTrusted, e.coef) }}</span>
                  <v-icon v-else icon="mdi-check-circle" color="success" size="16" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-2">{{ error }}</v-alert>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <span class="text-caption text-medium-emphasis">{{ openEntries.length }} coef{{ openEntries.length === 1 ? '' : 's' }} to save</span>
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="success" variant="flat" prepend-icon="mdi-content-save" :loading="saving" :disabled="!canSave" @click="save">
          Save commission
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
// COMMISSION_MIN_TOTAL, COMMISSION_THRESHOLD, COMMISSION_DEFAULT_MIN: 5K$ / 10K$ / 1.13 switched off for now (see commissionError).
import { commissionActive, commissionBase, commissionEnds, commissionError, commissionTotal } from '~/utils/ppe'
import type { PpePartRow, PpeQuoteRow, PpeRfqRef } from '~/utils/ppe'

const props = defineProps<{ modelValue: boolean; part: PpePartRow; rows: PpeQuoteRow[] }>()
const emit = defineEmits<{ 'update:modelValue': [open: boolean]; saved: [] }>()

const api = usePpeApi()
const authStore = useAuthStore()
const isSuperAdmin = computed(() => authStore.isSuperAdmin)

interface Entry {
  key: string
  customerId: number
  customerCode?: string | null
  minCoef?: number | null
  refs: PpeRfqRef[]
  coef: number | null
  /** Unit price typed by hand (null = from the coef). */
  unitPrice: number | null
  locked: boolean
}
interface Block { row: PpeQuoteRow; entries: Entry[] }

const blocks = ref<Block[]>([])
const saving = ref(false)
const error = ref('')

/** One entry per customer under each supplier. */
function build() {
  blocks.value = props.rows.map((row) => {
    const byCustomer = new Map<number, PpeRfqRef[]>()
    for (const r of row.rfqs.filter(r => r.quoteId)) {
      const list = byCustomer.get(r.customerId)
      // A copy: shipping typed here changes the dialog only until Save.
      if (list) list.push({ ...r })
      else byCustomer.set(r.customerId, [{ ...r }])
    }
    const entries = [...byCustomer.entries()].map(([customerId, refs]): Entry => {
      const first = refs[0]!
      // A commission is active 7 days; after that the RFQ can get a new one.
      const locked = !isSuperAdmin.value && refs.every(r => commissionActive(r.commissionAt))
      const savedRef = refs.find(r => commissionActive(r.commissionAt))
      const saved = savedRef?.coef1 ?? null
      const coef: number | null = saved
      // Trusted + MinCoef pre-fill switched off for now (Sam 2026-10-28):
      // if (coef == null && row.supplierTrusted && first.minCoef != null && refs.every(r => !commissionError(r, true, Number(first.minCoef))))
      //   coef = Number(first.minCoef)
      // A saved commission's hand-typed unit price (differs from the coef's) comes back as typed.
      const typed = Number(savedRef?.quoteUnitPrice) || 0
      const unitPrice = saved != null && typed > 0 && Math.abs(typed - commissionBase(savedRef!) * saved) > 0.001 ? typed : null
      return { key: `${row.key}|${customerId}`, customerId, customerCode: first.customerCode, minCoef: first.minCoef, refs, coef, unitPrice, locked }
    })
    return { row, entries }
  })
  error.value = ''
}
watch(() => props.modelValue, (open) => { if (open) build() }, { immediate: true })

const unitPrice = (e: Entry) => e.unitPrice ?? (e.coef ? Math.round(commissionBase(e.refs[0]!) * e.coef * 100) / 100 : null)

/** Shipping of one RFQ quote: Unit Price = (Buy Price + Shipping / Qty) × Coef, so the totals follow at once. */
function setShipping(r: PpeRfqRef, value: string) {
  const v = parseFloat(value)
  r.quoteShippingCost = isNaN(v) || v < 0 ? null : v
}

/** Typing the coef brings back the unit price from the coef (as on the Add Quote page). */
function setCoef(e: Entry, value: string) {
  const v = parseFloat(value)
  e.coef = isNaN(v) ? null : v
  e.unitPrice = null
}
/** A typed unit price stays as typed — the coef is not changed to match it (Sam 2026-10-28). */
function setUnitPrice(e: Entry, value: string) {
  const v = parseFloat(value)
  e.unitPrice = !isNaN(v) && v > 0 ? v : null
}

/** First RFQ quote of the entry that the coef breaks ('' = all fine). */
/** First RFQ of the customer that the coef breaks — each RFQ is checked on its own Total Price ('' = all fine). */
function errorOf(block: Block, e: Entry) {
  return e.refs.map(r => commissionError(r, block.row.supplierTrusted, e.coef)).find(Boolean) ?? ''
}

const openEntries = computed(() => blocks.value.flatMap(b => b.entries.filter(e => !e.locked).map(e => ({ b, e }))))
const canSave = computed(() => openEntries.value.length > 0 && openEntries.value.every(({ b, e }) => !errorOf(b, e)))

async function save() {
  saving.value = true
  error.value = ''
  try {
    await api.post(`/parts/${props.part.id}/commission`, {
      items: openEntries.value.map(({ e }) => ({
        quoteIds: e.refs.map(r => r.quoteId),
        coef: e.coef,
        unitPrice: e.unitPrice,
        shipping: e.refs.map(r => ({ quoteId: r.quoteId, shippingCost: r.quoteShippingCost ?? null })),
      })),
    })
    emit('saved')
    emit('update:modelValue', false)
  } catch (err: any) {
    error.value = err?.data?.message || 'Could not save the commission'
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.supplier-block {
  border: 1px solid var(--card-border);
  border-radius: 8px;
  padding: 10px 12px;
}
.comm-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.comm-grid th {
  text-align: left;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  padding: 6px 8px;
  color: rgba(var(--v-theme-on-surface), 0.6);
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  white-space: nowrap;
}
.comm-grid td {
  padding: 4px 8px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.05);
  vertical-align: middle;
}
.num {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  white-space: nowrap;
}
.customer-group tr:first-child td {
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.15);
}
.row--locked td {
  background: rgba(149, 117, 205, 0.08);
}
.comm-input {
  width: 100%;
  height: 30px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.2);
  border-radius: 4px;
  padding: 0 6px;
  font-size: 12px;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  text-align: center;
  background: rgba(var(--v-theme-surface), 0.6);
  outline: none;
}
.comm-input:focus {
  border-color: rgb(var(--v-theme-primary));
}
.comm-input--price {
  color: #60a5fa;
}
.comm-input--custom {
  border-color: rgba(245, 158, 11, 0.6);
  color: #f59e0b;
}
.comm-input[readonly] {
  cursor: not-allowed;
  opacity: 0.7;
}
</style>
