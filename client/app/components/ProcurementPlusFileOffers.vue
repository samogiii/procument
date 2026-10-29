<template>
  <!--
    "Available in your files": the rows of the current ILS / Excel files for one part, one row per supplier
    (preferred first, then cheapest), with its conditions to pick from. Every P/N it lists — the part and its
    alternatives — is a chip (P/N · condition · Qty · price); Add turns each picked one into a supplier line with
    that alt, condition and qty.
  -->
  <div class="fo">
    <div class="fo__title">
      <v-icon icon="mdi-file-delimited-outline" size="15" class="mr-1" />
      Available in your files
      <v-chip size="x-small" variant="tonal" class="ml-1">{{ groups.length }} supplier{{ groups.length === 1 ? '' : 's' }}</v-chip>
      <v-chip v-if="groups.length !== rows.length" size="x-small" variant="text" class="text-medium-emphasis">{{ rows.length }} listings</v-chip>
    </div>
    <div class="grid-scroll">
      <table class="fo__table">
        <thead>
          <tr>
            <th>Supplier</th>
            <th>Condition</th>
            <th>P/N · Cond · Qty · Price <span class="text-medium-emphasis font-weight-regular">— pick the ones to add</span></th>
            <th>File</th>
            <th style="width: 110px" />
          </tr>
        </thead>
        <tbody>
          <tr v-for="g in groups" :key="g.key" :class="{ 'fo__row--added': g.rows.every(isAdded) }">
            <td class="nowrap">
              <div class="d-flex align-center gap-1">
                <v-icon v-if="g.first.preferredId" icon="mdi-star" color="amber" size="13" title="Preferred supplier" />
                <span class="font-weight-medium">{{ g.first.preferredName || g.first.supplierName || g.first.companyRaw }}</span>
                <v-chip v-if="!g.first.supplierId" size="x-small" color="warning" variant="tonal" title="Not matched to a catalog supplier yet">not in catalog</v-chip>
                <!-- It lists a condition my part asks for -->
                <v-chip
                  v-for="c in askedHere(g)"
                  :key="`has-${c}`"
                  size="x-small"
                  color="success"
                  variant="tonal"
                  prepend-icon="mdi-check-decagram"
                  class="font-weight-bold"
                  :title="`Lists ${c} — the condition your part asks for`"
                >
                  has {{ c }}
                </v-chip>
              </div>
              <!-- Not in the catalog yet: its emails from the file, to find or add it -->
              <div v-if="!g.first.supplierId && emailsOf(g.rows).length" class="text-caption text-medium-emphasis fo__emails">
                <v-icon icon="mdi-email-outline" size="12" /> {{ emailsOf(g.rows).join(', ') }}
              </div>
              <div v-if="g.first.supplierComment" class="text-caption text-medium-emphasis text-truncate fo__comment" :title="g.first.supplierComment">{{ g.first.supplierComment }}</div>
            </td>
            <!-- Conditions this supplier lists: pick one to see (and pick) its P/Ns -->
            <td class="fo__conds">
              <div v-if="g.conditions.length > 1" class="fo__chips">
                <v-chip
                  size="x-small"
                  :variant="!condOf(g) ? 'flat' : 'outlined'"
                  :color="!condOf(g) ? 'primary' : undefined"
                  @click="setCond(g, null)"
                >
                  All {{ g.rows.length }}
                </v-chip>
                <v-chip
                  v-for="c in g.conditions"
                  :key="c.value"
                  size="x-small"
                  :variant="condOf(g) === c.value ? 'flat' : 'outlined'"
                  :color="condOf(g) === c.value ? 'primary' : askedIn(g).has(c.value) ? 'success' : undefined"
                  :class="{ 'font-weight-black': askedIn(g).has(c.value) }"
                  :title="`Show the P/Ns listed ${c.value || 'with no condition'}${askedIn(g).has(c.value) ? ' — the condition your part asks for' : ''}`"
                  @click="setCond(g, c.value)"
                >
                  {{ c.value || '—' }} <span class="fo__chip-meta">{{ c.count }}</span>
                </v-chip>
              </div>
              <v-chip
                v-else-if="g.conditions[0]?.value"
                size="x-small"
                variant="outlined"
                :color="askedIn(g).has(g.conditions[0].value) ? 'success' : undefined"
                :class="{ 'font-weight-black': askedIn(g).has(g.conditions[0].value) }"
              >
                {{ g.conditions[0].value }}
              </v-chip>
            </td>
            <td class="fo__pns">
              <div class="fo__chips">
                <v-chip
                  v-for="r in shownRows(g)"
                  :key="r.offerId"
                  size="small"
                  :variant="isAdded(r) ? 'flat' : picked.has(r.offerId) ? 'flat' : 'outlined'"
                  :color="isAdded(r) ? 'warning' : picked.has(r.offerId) ? 'primary' : offerAlt(r) ? 'info' : undefined"
                  :prepend-icon="isAdded(r) ? 'mdi-check' : picked.has(r.offerId) ? 'mdi-checkbox-marked' : undefined"
                  :disabled="busy != null"
                  :title="chipTitle(r)"
                  class="fo__chip"
                  @click="toggle(r)"
                >
                  <strong>{{ r.filePartNo }}</strong>
                  <span class="fo__chip-cond" :class="{ 'fo__chip-cond--asked': isAsked(r) }">{{ r.condition || '—' }}</span>
                  <span class="fo__chip-meta">Qty {{ r.qty ?? r.qtyRaw ?? '—' }}</span>
                  <span v-if="r.price != null || r.priceRaw" class="fo__chip-meta">· {{ r.price != null ? fmtMoney(r.price) : r.priceRaw }}</span>
                  <!-- We already have a valid price from this supplier (RFQ quote / Email Quote, last 14 days) -->
                  <span
                    v-if="r.knownPrice != null"
                    class="fo__chip-known"
                    :title="`${r.knownPriceSource === 'Email' ? 'Email Quote' : `RFQ quote${r.knownPriceRef ? ' · ' + r.knownPriceRef : ''}`} · ${fmtDate(r.knownPriceDate)}`"
                  >
                    {{ r.knownPriceSource === 'Email' ? 'Email' : 'RFQ' }} {{ fmtMoney(r.knownPrice) }}
                  </span>
                </v-chip>
                <v-chip
                  v-if="visibleRows(g).length > CHIPS_SHOWN"
                  size="small"
                  variant="text"
                  color="primary"
                  @click="toggleExpand(g.key)"
                >
                  {{ expanded.has(g.key) ? 'Show less' : `+${visibleRows(g).length - CHIPS_SHOWN} more` }}
                </v-chip>
              </div>
            </td>
            <td class="nowrap text-caption text-medium-emphasis" :title="`${g.first.fileName} · ${fmtDate(g.first.uploadedAt)}`">{{ g.first.fileName }}</td>
            <td class="text-right nowrap">
              <!-- Extra per-supplier buttons from the page (new Procurement Plus ILS tab: copy P/Ns, quotation mail) -->
              <slot name="supplier-actions" :rows="g.rows" :first="g.first" />
              <template v-if="g.rows.every(isAdded)">
                <v-chip size="x-small" color="warning" variant="tonal" prepend-icon="mdi-check">Added</v-chip>
              </template>
              <template v-else>
                <v-btn
                  v-if="openRows(g).length > 1 && !pickedIn(g).length"
                  size="x-small"
                  variant="text"
                  color="primary"
                  :title="condOf(g) != null ? `Pick every ${condOf(g) || 'no-condition'} P/N of this supplier` : 'Pick every P/N of this supplier'"
                  @click="pickAll(g)"
                >
                  All
                </v-btn>
                <!-- Add asks how the part is searched with this supplier: ILS / Send Email / Search (one or more) -->
                <ProcurementPlusMethodMenu @confirm="(m: string[]) => add(g, m)">
                  <template #activator="{ props: menuProps }">
                    <v-btn
                      v-bind="menuProps"
                      size="x-small"
                      variant="tonal"
                      color="primary"
                      prepend-icon="mdi-plus"
                      :loading="busy != null && g.rows.some(r => r.offerId === busy)"
                      :disabled="!toAdd(g).length || busy != null"
                      :title="toAdd(g).length ? '' : 'Pick the P/Ns to add'"
                    >
                      Add{{ toAdd(g).length > 1 ? ` ${toAdd(g).length}` : '' }}
                    </v-btn>
                  </template>
                </ProcurementPlusMethodMenu>
              </template>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import { CONDITIONS, offerAdded, offerAlt } from '~/utils/procurementPlus'
import type { PpQuoteLine } from '~/utils/procurementPlus'

const props = withDefaults(defineProps<{
  rows: PpOfferRow[]
  /** The part's supplier lines — rows already added show "Added". */
  lines?: PpQuoteLine[]
  /** Offer id being added. */
  busy?: number | null
  /** Rows of several parts (new Procurement Plus ILS tab): a row is added only by a line of its own part. */
  matchPart?: boolean
  /** New Procurement Plus: conditions each part asks for (by part id, upper case) — shown bold on the chips. */
  askedConditions?: Record<number, string[]>
}>(), { lines: () => [], busy: null, matchPart: false, askedConditions: () => ({}) })
/**
 * The rows to turn into supplier lines (one or more P/Ns of one supplier — each with its own condition and qty),
 * and how the part is searched with that supplier (ILS / Email / Search).
 */
const emit = defineEmits<{ add: [rows: PpOfferRow[], methods: string[]] }>()

/** Chips shown before "+N more" — a supplier listing 99 alternatives stays one short row. */
const CHIPS_SHOWN = 8

const supplierKey = (r: PpOfferRow) => (r.preferredId ? `p${r.preferredId}` : r.supplierId ? `s${r.supplierId}` : `c${r.companyKey}`)
const cond = (r: PpOfferRow) => (r.condition || '').trim().toUpperCase()

interface Group { key: string; first: PpOfferRow; rows: PpOfferRow[]; conditions: { value: string; count: number }[] }

/**
 * One row per supplier: its conditions (picker), and every P/N it lists — by condition, the part itself
 * before its alternatives, cheapest first.
 */
const groups = computed(() => {
  const map = new Map<string, Group>()
  for (const r of props.rows) {
    const key = supplierKey(r)
    const g = map.get(key)
    if (g) g.rows.push(r)
    else map.set(key, { key, first: r, rows: [r], conditions: [] })
  }
  const byPrice = (a: PpOfferRow, b: PpOfferRow) =>
    Number(a.price == null) - Number(b.price == null) || (a.price ?? 0) - (b.price ?? 0)
  const list = [...map.values()]
  for (const g of list) {
    g.rows.sort((a, b) => condRank(cond(a)) - condRank(cond(b)) || cond(a).localeCompare(cond(b))
      || Number(!!offerAlt(a)) - Number(!!offerAlt(b)) || byPrice(a, b))
    g.first = g.rows[0]!
    const counts = new Map<string, number>()
    for (const r of g.rows) counts.set(cond(r), (counts.get(cond(r)) ?? 0) + 1)
    g.conditions = [...counts].map(([value, count]) => ({ value, count }))
  }
  return list.sort((a, b) =>
    Number(!!b.first.preferredId) - Number(!!a.first.preferredId)
    || byPrice(minPriced(a.rows), minPriced(b.rows)))
})
function minPriced(rows: PpOfferRow[]) {
  return rows.reduce((m, r) => (r.price != null && (m.price == null || r.price < m.price) ? r : m), rows[0]!)
}

/** The row's condition is one its part asks for. */
const isAsked = (r: PpOfferRow) => !!cond(r) && (props.askedConditions[r.partId] ?? []).includes(cond(r))
/** Conditions of a supplier's rows that their parts ask for. */
const askedIn = (g: Group) => new Set(g.rows.filter(isAsked).map(cond))
const askedHere = (g: Group) => [...askedIn(g)]

const isAdded = (r: PpOfferRow) =>
  offerAdded(r, props.matchPart ? props.lines.filter(l => l.preferredPartId === r.partId) : props.lines)
const emailsOf = (rows: PpOfferRow[]) =>
  [...new Set(rows.map(r => r.email?.trim().toLowerCase()).filter((e): e is string => !!e))]

// ── Condition picked per supplier (null = all) ──
/** Usual order of conditions; others after, A→Z; no condition last. */
const condRank = (c: string) => {
  const i = CONDITIONS.indexOf(c)
  return c ? (i < 0 ? CONDITIONS.length : i) : CONDITIONS.length + 1
}
const condPicked = ref(new Map<string, string | null>())
const condOf = (g: Group) => condPicked.value.get(g.key) ?? null
function setCond(g: Group, c: string | null) {
  const next = new Map(condPicked.value)
  next.set(g.key, c)
  condPicked.value = next
}
/** The supplier's P/Ns in the picked condition. */
const visibleRows = (g: Group) => {
  const c = condOf(g)
  return c == null ? g.rows : g.rows.filter(r => cond(r) === c)
}
const openRows = (g: Group) => visibleRows(g).filter(r => !isAdded(r))

// ── Picking P/Ns (each keeps its own condition and qty when added) ──
const picked = ref(new Set<number>())
const pickedIn = (g: Group) => g.rows.filter(r => !isAdded(r) && picked.value.has(r.offerId))
/** Picked P/Ns (any condition), or the only one left in the condition shown. */
function toAdd(g: Group) {
  const sel = pickedIn(g)
  if (sel.length) return sel
  const open = openRows(g)
  return open.length === 1 ? open : []
}
function toggle(r: PpOfferRow) {
  if (isAdded(r)) return
  const next = new Set(picked.value)
  next.has(r.offerId) ? next.delete(r.offerId) : next.add(r.offerId)
  picked.value = next
}
function pickAll(g: Group) {
  const next = new Set(picked.value)
  for (const r of openRows(g)) next.add(r.offerId)
  picked.value = next
}

function add(g: Group, methods: string[]) {
  const rows = toAdd(g)
  if (!rows.length) return
  const next = new Set(picked.value)
  for (const r of rows) next.delete(r.offerId)
  picked.value = next
  emit('add', rows, methods)
}

// ── Long lists ──
const expanded = ref(new Set<string>())
function toggleExpand(key: string) {
  const next = new Set(expanded.value)
  next.has(key) ? next.delete(key) : next.add(key)
  expanded.value = next
}
const shownRows = (g: Group) =>
  expanded.value.has(g.key) ? visibleRows(g) : visibleRows(g).slice(0, CHIPS_SHOWN)

function chipTitle(r: PpOfferRow) {
  const bits = [
    offerAlt(r) ? `Alternative ${r.filePartNo}` : r.filePartNo,
    r.altPartNo ? `alt in file: ${r.altPartNo}` : '',
    r.description || '',
    isAdded(r) ? 'Added under the part' : `Click to pick — adds ${offerAlt(r) ? `alt ${r.filePartNo}` : r.filePartNo}, ${r.condition || 'no condition'}, Qty ${r.qty ?? r.qtyRaw ?? '—'}`,
  ]
  return bits.filter(Boolean).join(' · ')
}
</script>

<style scoped>
.fo { border-top: 1px dashed rgba(var(--v-border-color), 0.18); padding: 6px 0 2px; }
.fo__title { display: flex; align-items: center; font-size: 12px; font-weight: 700; padding: 2px 12px 4px; opacity: 0.8; }
.grid-scroll { overflow-x: auto; }
.fo__table { width: 100%; border-collapse: collapse; font-size: 12.5px; }
.fo__table th {
  text-align: left; font-size: 11px; font-weight: 600; opacity: 0.6; padding: 4px 10px; white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
}
.fo__table td { padding: 5px 10px; border-bottom: 1px solid rgba(var(--v-border-color), 0.05); vertical-align: top; }
.fo__row--added { opacity: 0.6; }
.fo__comment { max-width: 260px; }
.fo__emails { max-width: 280px; white-space: normal; word-break: break-all; line-height: 1.3; }
.fo__pns { min-width: 280px; }
.fo__chips { display: flex; flex-wrap: wrap; gap: 4px; }
.fo__chip { font-size: 12px; }
.fo__chip-meta { margin-left: 4px; opacity: 0.75; font-weight: 500; }
.fo__chip-cond {
  margin-left: 6px; padding: 0 5px; border-radius: 4px; font-size: 10.5px; font-weight: 800; letter-spacing: 0.04em;
  background: rgba(var(--v-theme-on-surface), 0.08);
}
.fo__chip-cond--asked {
  font-weight: 900; font-size: 11.5px;
  background: rgba(var(--v-theme-success), 0.22); color: rgb(var(--v-theme-success));
}
.fo__chip-known {
  margin-left: 6px; padding: 0 5px; border-radius: 4px; font-size: 10.5px; font-weight: 700;
  background: rgba(var(--v-theme-success), 0.18); color: rgb(var(--v-theme-success));
}
.fo__conds { min-width: 90px; max-width: 200px; }
.nowrap { white-space: nowrap; }
</style>
