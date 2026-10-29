<template>
  <!--
    New Procurement Plus — Preferred suppliers tab: the user's own preferred suppliers (the same list as ProMax), each
    with the items it lists for my parts in the current ILS files (part, P/N in the file, condition, qty, price, a
    price we already have). Pick items → Add puts the supplier under those parts, like the ILS tab.
  -->
  <v-card class="glass-card">
    <v-card-text>
      <div class="d-flex flex-wrap align-center gap-2 mb-3">
        <v-text-field
          v-model="search"
          placeholder="Supplier, email or P/N"
          prepend-inner-icon="mdi-magnify"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          style="max-width: 280px"
        />
        <v-switch v-model="onlyWithItems" color="primary" density="compact" hide-details inset label="Only with items in my files" class="ml-2" />
        <v-spacer />
        <span class="text-caption text-medium-emphasis">
          {{ preferred.length }} preferred · {{ withItemsCount }} list{{ withItemsCount === 1 ? 's' : '' }} your parts
        </span>
        <v-btn size="small" variant="text" prepend-icon="mdi-table-cog" @click="emit('manage')">Manage list</v-btn>
        <v-btn size="small" color="primary" variant="flat" prepend-icon="mdi-star-plus-outline" @click="emit('create')">Add supplier</v-btn>
      </div>

      <div v-if="loading && !data" class="d-flex justify-center py-8"><v-progress-circular indeterminate color="primary" /></div>
      <p v-else-if="!preferred.length" class="text-body-2 text-medium-emphasis text-center py-6 mb-0">
        No preferred suppliers yet. Add your own — or star a supplier on the ILS tab. Files match them by email, email
        domain, catalog supplier or name.
      </p>
      <template v-else>
        <v-alert v-if="!data?.uploads.length" type="info" variant="tonal" density="compact" class="mb-3">
          Upload your ILS files (above) — the items your preferred suppliers list for your parts show here.
        </v-alert>
        <p v-if="!cards.length" class="text-body-2 text-medium-emphasis text-center py-6 mb-0">Nothing matches.</p>

        <div v-for="c in cards" :key="c.pref.id" class="pf" :class="{ 'pf--empty': !c.rows.length }">
          <!-- Supplier -->
          <div class="pf__head">
            <div class="min-w-0">
              <div class="d-flex flex-wrap align-center gap-1">
                <v-icon icon="mdi-star" color="amber" size="16" />
                <span class="font-weight-bold">{{ c.pref.name }}</span>
                <v-chip v-if="c.pref.supplierId" size="x-small" color="success" variant="tonal" prepend-icon="mdi-link-variant" :title="`Catalog: ${c.pref.supplierName}`">
                  {{ c.pref.supplierName }}
                </v-chip>
                <v-chip v-else size="x-small" variant="outlined" prepend-icon="mdi-link-variant-off" title="Needs a catalog supplier before its prices can go on RFQs">not in catalog</v-chip>
                <v-chip v-if="c.pref.supplierStatus && c.pref.supplierStatus !== 'Approved'" size="x-small" color="warning" variant="tonal">{{ c.pref.supplierStatus }}</v-chip>
                <v-chip size="x-small" variant="tonal" :color="methodMeta(c.pref.sourceMethod).color" :prepend-icon="methodMeta(c.pref.sourceMethod).icon">
                  {{ methodMeta(c.pref.sourceMethod).title }}
                </v-chip>
                <v-chip v-if="c.rows.length" size="x-small" color="primary" variant="tonal">
                  {{ c.parts }} part{{ c.parts === 1 ? '' : 's' }} · {{ c.rows.length }} listing{{ c.rows.length === 1 ? '' : 's' }}
                </v-chip>
                <span v-else class="text-caption text-medium-emphasis ml-1">none of your parts in your current files</span>
              </div>
              <!-- Emails: click one to copy it -->
              <div class="d-flex flex-wrap align-center gap-1 mt-1">
                <v-chip
                  v-for="e in c.emails"
                  :key="e"
                  size="x-small"
                  variant="outlined"
                  prepend-icon="mdi-email-outline"
                  title="Copy this address"
                  @click="copy(e, 'Address copied')"
                >
                  {{ e }}
                </v-chip>
                <v-btn
                  v-if="c.emails.length > 1"
                  size="x-small"
                  variant="text"
                  icon="mdi-at"
                  title="Copy every address"
                  @click="copy(c.emails.join('; '), `${c.emails.length} addresses copied`)"
                />
                <span v-if="c.pref.phone" class="text-caption text-medium-emphasis"><v-icon icon="mdi-phone-outline" size="12" /> {{ c.pref.phone }}</span>
                <span v-if="c.pref.note" class="text-caption text-medium-emphasis">· {{ c.pref.note }}</span>
                <span v-if="!c.emails.length && !c.pref.phone" class="text-caption text-medium-emphasis">no email</span>
              </div>
            </div>
            <div class="nowrap">
              <v-btn
                size="x-small"
                variant="text"
                icon="mdi-content-copy"
                :disabled="!c.rows.length"
                title="Copy this supplier's P/Ns (parts and alternatives), one per line"
                @click="emit('copy-pns', c.rows)"
              />
              <v-btn
                size="x-small"
                variant="text"
                color="blue"
                icon="mdi-email-edit-outline"
                :disabled="!c.rows.length"
                title="Quotation mail for this supplier — pick the parts, copy the mail, then open it in your mail app"
                @click="emit('mail', c.rows, c.pref.name, c.emails)"
              />
              <v-btn size="x-small" variant="text" icon="mdi-pencil-outline" title="Edit" @click="emit('edit', c.pref)" />
              <v-btn size="x-small" variant="text" color="error" icon="mdi-star-off-outline" title="Remove from my preferred suppliers" @click="emit('remove', c.pref)" />
            </div>
          </div>

          <!-- Its items for my parts -->
          <div v-if="c.rows.length" class="grid-scroll">
            <table class="pf__table">
              <thead>
                <tr>
                  <th style="width: 34px" />
                  <th>Your part</th>
                  <th>P/N in file</th>
                  <th>Description</th>
                  <th>Cond</th>
                  <th class="text-right">Qty</th>
                  <th class="text-right">Price</th>
                  <th>We have</th>
                  <th>File</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="r in shownRows(c)" :key="`${r.offerId}-${r.partId}`" :class="{ 'pf__row--added': isAdded(r), 'pf__row--picked': picked.has(rowKey(r)) }" @click="toggle(r)">
                  <td>
                    <v-icon v-if="isAdded(r)" icon="mdi-check" color="warning" size="18" title="Already under the part" />
                    <v-icon v-else :icon="picked.has(rowKey(r)) ? 'mdi-checkbox-marked' : 'mdi-checkbox-blank-outline'" :color="picked.has(rowKey(r)) ? 'primary' : undefined" size="18" />
                  </td>
                  <td class="nowrap">
                    <span class="font-weight-medium pf__pn" title="Copy" @click.stop="copy(r.partNumber, 'P/N copied')">{{ r.partNumber }}</span>
                  </td>
                  <td class="nowrap">
                    <span class="pf__pn" title="Copy" @click.stop="copy(r.filePartNo, 'P/N copied')">{{ r.filePartNo }}</span>
                    <v-chip v-if="offerAlt(r)" size="x-small" color="info" variant="tonal" class="ml-1">alt</v-chip>
                  </td>
                  <td class="text-caption pf__desc" :title="r.description || ''">{{ r.description || '—' }}</td>
                  <td>
                    <span :class="{ 'font-weight-black text-success': isAsked(r) }" :title="isAsked(r) ? 'The condition your part asks for' : ''">{{ r.condition || '—' }}</span>
                  </td>
                  <td class="text-right">{{ r.qty ?? r.qtyRaw ?? '—' }}</td>
                  <td class="text-right nowrap">{{ r.price != null ? fmtMoney(r.price) : r.priceRaw || '—' }}</td>
                  <td class="nowrap">
                    <v-chip
                      v-if="r.knownPrice != null"
                      size="x-small"
                      color="success"
                      variant="tonal"
                      :title="`${r.knownPriceSource === 'Email' ? 'Email Quote' : `RFQ quote${r.knownPriceRef ? ' · ' + r.knownPriceRef : ''}`} · ${fmtDate(r.knownPriceDate)}`"
                    >
                      {{ r.knownPriceSource === 'Email' ? 'Email' : 'RFQ' }} {{ fmtMoney(r.knownPrice) }}
                    </v-chip>
                  </td>
                  <td class="text-caption text-medium-emphasis nowrap" :title="fmtDate(r.uploadedAt)">{{ r.fileName }}</td>
                </tr>
              </tbody>
            </table>
            <div class="d-flex align-center gap-2 pt-2">
              <v-btn v-if="c.rows.length > ROWS_SHOWN" size="x-small" variant="text" color="primary" @click="toggleExpand(c.pref.id)">
                {{ expanded.has(c.pref.id) ? 'Show less' : `Show all ${c.rows.length}` }}
              </v-btn>
              <v-spacer />
              <v-btn v-if="openRows(c).length > 1" size="x-small" variant="text" color="primary" @click="pickAll(c)">
                {{ pickedIn(c).length === openRows(c).length ? 'Unpick all' : 'Pick all' }}
              </v-btn>
              <!-- Add asks how the part is searched with this supplier: ILS / Send Email / Search -->
              <ProcurementPlusMethodMenu @confirm="(m: string[]) => add(c, m)">
                <template #activator="{ props: menuProps }">
                  <v-btn
                    v-bind="menuProps"
                    size="x-small"
                    variant="tonal"
                    color="primary"
                    prepend-icon="mdi-plus"
                    :loading="busy != null && c.rows.some(r => r.offerId === busy)"
                    :disabled="!pickedIn(c).length || busy != null"
                    :title="pickedIn(c).length ? '' : 'Pick the items to add'"
                  >
                    Add{{ pickedIn(c).length ? ` ${pickedIn(c).length}` : '' }} to my parts
                  </v-btn>
                </template>
              </ProcurementPlusMethodMenu>
            </div>
          </div>
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import type { PpPreferredSupplier } from '~/components/ProcurementPlusPreferredDialog.vue'
import { CONDITIONS, copyText, fmtDate, fmtMoney, methodMeta, offerAdded, offerAlt } from '~/utils/procurementPlus'
import type { PpQuoteLine } from '~/utils/procurementPlus'
import { supplierMailAddresses } from '~/utils/ppe'
import type { PpeIls } from '~/utils/ppe'

const props = withDefaults(defineProps<{
  data: PpeIls | null
  lines: PpQuoteLine[]
  loading?: boolean
  /** Offer id being added. */
  busy?: number | null
  /** Conditions each part asks for (by part id) — bold. */
  askedConditions?: Record<number, string[]>
}>(), { loading: false, busy: null, askedConditions: () => ({}) })

const emit = defineEmits<{
  add: [rows: PpOfferRow[], methods: string[]]
  'copy-pns': [rows: PpOfferRow[]]
  mail: [rows: PpOfferRow[], supplierName: string, emails: string[]]
  create: []
  manage: []
  edit: [pref: PpPreferredSupplier]
  remove: [pref: PpPreferredSupplier]
  toast: [text: string, color?: string]
}>()

/** Rows shown before "Show all". */
const ROWS_SHOWN = 12

const search = ref('')
const onlyWithItems = ref(false)

const preferred = computed(() => props.data?.preferred ?? [])
const cond = (r: PpOfferRow) => (r.condition || '').trim().toUpperCase()
const condRank = (c: string) => {
  const i = CONDITIONS.indexOf(c)
  return c ? (i < 0 ? CONDITIONS.length : i) : CONDITIONS.length + 1
}
const byPrice = (a: PpOfferRow, b: PpOfferRow) => Number(a.price == null) - Number(b.price == null) || (a.price ?? 0) - (b.price ?? 0)

interface Card { pref: PpPreferredSupplier; rows: PpOfferRow[]; parts: number; emails: string[] }

/** Every preferred supplier with its rows (by part, asked condition first, cheapest first); those with items first. */
const allCards = computed<Card[]>(() => {
  const byPref = new Map<number, PpOfferRow[]>()
  for (const r of props.data?.rows ?? []) {
    if (!r.preferredId) continue
    const list = byPref.get(r.preferredId)
    list ? list.push(r) : byPref.set(r.preferredId, [r])
  }
  return preferred.value.map((pref) => {
    const rows = [...(byPref.get(pref.id) ?? [])].sort((a, b) =>
      a.partNumber.localeCompare(b.partNumber) || Number(!!offerAlt(a)) - Number(!!offerAlt(b))
      || condRank(cond(a)) - condRank(cond(b)) || byPrice(a, b))
    const emails = [...new Set([...pref.emails.map(e => e.toLowerCase()), ...supplierMailAddresses(rows)])]
    return { pref, rows, parts: new Set(rows.map(r => r.partId)).size, emails }
  }).sort((a, b) => Number(!a.rows.length) - Number(!b.rows.length) || a.pref.name.localeCompare(b.pref.name))
})
const withItemsCount = computed(() => allCards.value.filter(c => c.rows.length).length)

const cards = computed(() => {
  const q = (search.value || '').trim().toLowerCase()
  return allCards.value.filter((c) => {
    if (onlyWithItems.value && !c.rows.length) return false
    if (!q) return true
    const hay = [c.pref.name, c.pref.supplierName ?? '', ...c.emails, ...c.rows.flatMap(r => [r.partNumber, r.filePartNo])].join(' ').toLowerCase()
    return hay.includes(q)
  })
})

const expanded = ref(new Set<number>())
function toggleExpand(id: number) {
  const next = new Set(expanded.value)
  next.has(id) ? next.delete(id) : next.add(id)
  expanded.value = next
}
const shownRows = (c: Card) => (expanded.value.has(c.pref.id) ? c.rows : c.rows.slice(0, ROWS_SHOWN))

const isAsked = (r: PpOfferRow) => !!cond(r) && (props.askedConditions[r.partId] ?? []).includes(cond(r))
/** Added only by a line under the row's own part (rows of several parts). */
const isAdded = (r: PpOfferRow) => offerAdded(r, props.lines.filter(l => l.preferredPartId === r.partId))

// ── Picking items (a file row can answer two parts — picked per part) ──
const rowKey = (r: PpOfferRow) => `${r.offerId}-${r.partId}`
const picked = ref(new Set<string>())
const openRows = (c: Card) => c.rows.filter(r => !isAdded(r))
const pickedIn = (c: Card) => openRows(c).filter(r => picked.value.has(rowKey(r)))
function toggle(r: PpOfferRow) {
  if (isAdded(r)) return
  const next = new Set(picked.value)
  next.has(rowKey(r)) ? next.delete(rowKey(r)) : next.add(rowKey(r))
  picked.value = next
}
function pickAll(c: Card) {
  const all = pickedIn(c).length === openRows(c).length
  const next = new Set(picked.value)
  for (const r of openRows(c)) all ? next.delete(rowKey(r)) : next.add(rowKey(r))
  picked.value = next
}
function add(c: Card, methods: string[]) {
  const rows = pickedIn(c)
  if (!rows.length) return
  const next = new Set(picked.value)
  for (const r of rows) next.delete(rowKey(r))
  picked.value = next
  emit('add', rows, methods)
}

function copy(text: string, done: string) {
  const ok = copyText(text)
  emit('toast', ok ? done : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
}
</script>

<style scoped>
.pf { border: 1px solid rgba(var(--v-border-color), 0.12); border-radius: 10px; padding: 10px 12px; margin-bottom: 10px; }
.pf--empty { opacity: 0.75; padding: 8px 12px; }
.pf__head { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; }
.grid-scroll { overflow-x: auto; margin-top: 8px; }
.pf__table { width: 100%; border-collapse: collapse; font-size: 12.5px; }
.pf__table th {
  text-align: left; font-size: 11px; font-weight: 600; opacity: 0.6; padding: 4px 8px; white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
}
.pf__table td { padding: 4px 8px; border-bottom: 1px solid rgba(var(--v-border-color), 0.05); vertical-align: middle; cursor: pointer; }
.pf__table tbody tr:hover { background: rgba(var(--v-theme-primary), 0.04); }
.pf__row--picked { background: rgba(var(--v-theme-primary), 0.08); }
.pf__row--added { opacity: 0.55; cursor: default; }
.pf__pn { cursor: copy; }
.pf__pn:hover { text-decoration: underline dotted; }
.pf__desc { max-width: 260px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.nowrap { white-space: nowrap; }
</style>
