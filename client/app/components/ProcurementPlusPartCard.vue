<template>
  <!--
    One part: a compact header (part, status, average price per condition) and, when opened, its suppliers
    table (click a row to edit — autosaves) plus, for Project / WeChat, the tracing timeline.
    Used by the boards (ILS Parts / Project / WeChat) and the RFQ view.
  -->
  <section class="pc" :class="[`pc--${track.color}`, { 'pc--hidden': part.hidden, 'pc--on': selected }]">
    <header class="pc__head" @click="open = !open">
      <v-checkbox-btn
        v-if="selectable"
        :model-value="selected"
        density="compact"
        class="flex-grow-0 pc__check"
        @click.stop
        @update:model-value="emit('toggle')"
      />

      <!-- Left: the part -->
      <div class="pc__part">
        <div class="d-flex align-center flex-wrap gap-1">
          <v-icon :icon="track.icon" :color="track.color" size="16" :title="track.title" />
          <span class="pc__pn">{{ part.partNumber }}</span>
          <v-chip v-if="part.hidden" size="x-small" variant="outlined">removed</v-chip>
          <v-chip v-if="showOwner && part.ownerName" size="x-small" color="primary" variant="tonal" prepend-icon="mdi-account">{{ part.ownerName }}</v-chip>
        </div>
        <div class="text-caption text-medium-emphasis text-truncate" :title="part.description">{{ part.description || '—' }}</div>
        <div class="d-flex flex-wrap gap-1 mt-1">
          <v-chip v-if="part.qty" size="x-small" variant="outlined">Qty {{ part.qty }}</v-chip>
          <v-chip v-if="part.condition" size="x-small" variant="outlined">{{ part.condition }}</v-chip>
          <v-chip v-if="part.targetPrice" size="x-small" color="amber-darken-2" variant="flat" prepend-icon="mdi-target">{{ fmtMoney(part.targetPrice) }}</v-chip>
          <v-chip v-if="part.alternatives.length" size="x-small" variant="text" class="text-info" :title="part.alternatives.join(', ')">
            alt {{ part.alternatives.length > 2 ? `${part.alternatives.slice(0, 2).join(', ')} +${part.alternatives.length - 2}` : part.alternatives.join(', ') }}
          </v-chip>
        </div>
      </div>

      <!-- Middle: average price per condition (older RFQ quotes + prices typed here) -->
      <div class="pc__stats">
        <div
          v-for="st in part.priceStats.slice(0, 4)"
          :key="st.condition"
          class="pc__stat"
          :title="`${st.count} price${st.count === 1 ? '' : 's'} (older RFQ supplier quotes + prices typed here) · min ${fmtMoney(st.min)} · max ${fmtMoney(st.max)} · last ${fmtDate(st.lastDate)}`"
        >
          <div class="pc__stat-cond">{{ st.condition || '—' }}</div>
          <div class="pc__stat-avg" :class="targetClass(st.average)">{{ fmtMoney(st.average) }}</div>
          <div class="pc__stat-sub">avg · {{ st.count }} price{{ st.count === 1 ? '' : 's' }}</div>
        </div>
        <div v-if="!part.priceStats.length" class="text-caption text-medium-emphasis pc__nostats">No price history</div>
      </div>

      <!-- Right: status + actions -->
      <div class="pc__side" @click.stop>
        <v-chip size="small" :color="status.color" variant="tonal" :prepend-icon="status.icon" class="mb-1" @click="open = !open">
          {{ statusText }}
        </v-chip>
        <div class="d-flex align-center gap-1">
          <v-btn v-if="part.track === 'WeChat'" size="x-small" variant="tonal" color="green" icon="mdi-content-copy" title="Copy the WeChat request text" @click="copyRequest" />
          <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="emit('add-line', board)">Supplier</v-btn>
          <v-btn
            size="small"
            variant="flat"
            color="success"
            prepend-icon="mdi-check-bold"
            :disabled="!canFinish"
            :loading="finishing"
            :title="finishHint"
            @click="finish"
          >
            Done
          </v-btn>
          <v-menu>
            <template #activator="{ props: menuProps }">
              <v-btn v-bind="menuProps" icon="mdi-dots-vertical" size="small" variant="text" />
            </template>
            <v-list density="compact">
              <v-list-item prepend-icon="mdi-pencil-outline" title="Edit part" @click="emit('edit-part', part)" />
              <v-list-subheader>Move to</v-list-subheader>
              <v-list-item
                v-for="t in PART_TRACKS.filter(t => t.value !== part.track)"
                :key="t.value"
                :prepend-icon="t.icon"
                :title="t.title"
                @click="emit('move', [part.id], t.value)"
              />
              <v-divider />
              <v-list-item prepend-icon="mdi-web" title="Search the web" :href="searchHref(part.partNumber)" target="_blank" rel="noopener" />
              <v-list-item v-if="part.hidden" prepend-icon="mdi-eye-outline" title="Show again" @click="emit('show', [part.id])" />
              <v-list-item v-else prepend-icon="mdi-eye-off-outline" title="Remove from my list" @click="emit('hide', [part.id])" />
              <v-list-item prepend-icon="mdi-delete-outline" title="Delete" base-color="error" @click="emit('delete', [part.id])" />
            </v-list>
          </v-menu>
          <v-btn :icon="open ? 'mdi-chevron-up' : 'mdi-chevron-down'" size="small" variant="text" @click="open = !open" />
        </div>
      </div>
    </header>

    <!-- Info strip: open RFQs, what the files list, prices still auto-filling -->
    <div v-if="hasInfo" class="pc__info">
      <template v-if="part.openRfqs.length">
        <v-icon icon="mdi-file-document-outline" size="14" class="text-medium-emphasis" />
        <v-chip
          v-for="r in part.openRfqs.slice(0, 3)"
          :key="r.rfqId"
          size="x-small"
          :color="new Date(r.deadline) < new Date() ? 'orange' : 'primary'"
          variant="tonal"
          :to="`/rfqs/${r.rfqId}`"
        >
          {{ r.rfqName }} · {{ deadlineLabel(r.deadline) }}
        </v-chip>
        <span v-if="part.openRfqs.length > 3" class="text-caption">+{{ part.openRfqs.length - 3 }}</span>
      </template>
      <template v-if="part.track === 'ILS'">
        <span class="pc__sep" />
        <template v-if="part.offerCount">
          <v-chip size="x-small" color="amber-darken-2" variant="tonal" prepend-icon="mdi-star" title="Preferred suppliers in your files">{{ part.preferredSupplierCount }} preferred</v-chip>
          <v-chip size="x-small" variant="tonal" prepend-icon="mdi-domain" title="Other suppliers in your files">{{ part.otherSupplierCount }} other</v-chip>
        </template>
        <v-chip v-else-if="hasFiles" size="x-small" color="error" variant="tonal">not in your files</v-chip>
      </template>
      <template v-for="q in part.liveQuotes" :key="q.quoteId">
        <span class="pc__sep" />
        <span class="text-caption" :title="q.source === 'Email' ? 'Accepted on Email Quotes' : 'Done here'">
          <v-icon :icon="q.source === 'Email' ? 'mdi-email-check-outline' : 'mdi-rocket-launch-outline'" size="13" :color="q.source === 'Email' ? 'blue' : 'success'" />
          <strong>{{ q.supplierName }}</strong> {{ fmtMoney(q.price) }} {{ q.condition }} → {{ q.rfqs.length ? q.rfqs.map(r => r.rfqName).join(', ') : 'new RFQs' }}
          <span class="text-medium-emphasis">· until {{ fmtDate(q.autoFillUntil) }}</span>
        </span>
      </template>
    </div>

    <!-- RFQ view: quotes already on this RFQ item -->
    <div v-if="rfqQuotes?.length" class="pc__rfqquotes">
      <span class="text-caption font-weight-bold mr-1">On this RFQ:</span>
      <v-chip v-for="(q, i) in rfqQuotes.slice(0, 5)" :key="i" size="x-small" variant="outlined" color="primary">
        {{ q.supplierName }} {{ fmtMoney(q.price) }} {{ q.condition }}
      </v-chip>
      <span v-if="rfqQuotes.length > 5" class="text-caption">+{{ rfqQuotes.length - 5 }}</span>
    </div>

    <v-expand-transition>
      <div v-if="open" class="pc__body" :class="{ 'pc__body--timeline': hasTimeline }">
        <!-- Suppliers -->
        <div class="grid-scroll">
          <table class="sup-table">
            <thead>
              <tr>
                <th>Supplier</th>
                <th>Alt</th>
                <th>Condition</th>
                <th class="num">Qty</th>
                <th class="num">Price</th>
                <th>Cert Type</th>
                <th>Tag Date</th>
                <th>Tagged By</th>
                <th>Lead Time</th>
                <th style="width: 36px" />
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="l in board.lines"
                :key="l.line.id"
                :class="{ 'row--done': l.status === 'Accepted' }"
                title="Click to edit"
                @click="emit('edit-line', l)"
              >
                <td class="nowrap">
                  <div class="d-flex align-center gap-1">
                    <v-icon v-if="l.preferredId" icon="mdi-star" color="amber" size="13" />
                    <span class="font-weight-medium">{{ supplierName(l) }}</span>
                    <v-chip v-if="l.status === 'Accepted'" size="x-small" color="success" variant="flat" prepend-icon="mdi-check">Done</v-chip>
                    <v-chip v-else-if="!l.line.supplierId" size="x-small" color="warning" variant="tonal" title="Pick a catalog supplier on the line to finish">not in catalog</v-chip>
                  </div>
                  <div v-if="l.status === 'Accepted' && l.writtenRfqs.length" class="text-caption text-success">
                    → {{ l.writtenRfqs.map(r => r.rfqName).join(', ') }}
                  </div>
                </td>
                <td class="nowrap">{{ l.line.alt }}</td>
                <td>{{ l.line.condition }}</td>
                <td class="num">{{ l.line.qty ?? '' }}</td>
                <td class="num font-weight-bold" :class="l.line.price == null ? 'text-error' : targetClass(l.line.price)">
                  {{ l.line.price != null ? fmtMoney(l.line.price) : '—' }}
                </td>
                <td class="nowrap">{{ l.line.certName }}</td>
                <td class="nowrap">{{ l.line.tagDate ? fmtDate(l.line.tagDate) : (l.line.tagDateRaw || '') }}</td>
                <td class="nowrap">{{ l.line.taggedBy }}</td>
                <td class="nowrap">{{ l.line.leadTime }}</td>
                <td @click.stop>
                  <v-btn
                    v-if="l.status !== 'Accepted'"
                    icon="mdi-close"
                    size="x-small"
                    variant="text"
                    color="error"
                    title="No quote — remove"
                    @click="removeLine(l)"
                  />
                </td>
              </tr>
              <tr v-if="!board.lines.length">
                <td colspan="10" class="text-center text-medium-emphasis py-4">
                  No suppliers yet — <a href="#" @click.prevent="emit('add-line', board)">add one</a>.
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Tracing timeline (Project / WeChat) -->
        <aside v-if="hasTimeline" class="timeline">
          <v-text-field
            v-model="note"
            placeholder="What happened? Press Enter"
            density="compact"
            variant="outlined"
            hide-details
            prepend-inner-icon="mdi-message-plus-outline"
            :loading="noting"
            @keydown.enter.prevent="addNote"
          />
          <div class="timeline__scroll">
            <v-timeline v-if="board.events.length" density="compact" side="end" truncate-line="both" class="mt-2">
              <v-timeline-item
                v-for="e in board.events"
                :key="e.id"
                :dot-color="eventMeta(e.kind).color"
                :icon="eventMeta(e.kind).icon"
                size="x-small"
              >
                <div class="timeline__text">{{ e.text }}</div>
                <div class="text-caption text-medium-emphasis">{{ e.userName || '—' }} · {{ new Date(e.createdAt).toLocaleString() }}</div>
              </v-timeline-item>
            </v-timeline>
            <div v-else class="text-caption text-medium-emphasis text-center pa-4">Nothing yet — notes and changes show here.</div>
          </div>
        </aside>
      </div>
    </v-expand-transition>
    <slot />
  </section>
</template>

<script setup lang="ts">
import type { PpBoardPart, PpEvent, PpPart, PpQuoteLine, PpRfqQuote } from '~/utils/procurementPlus'

const props = withDefaults(defineProps<{
  board: PpBoardPart
  showOwner?: boolean
  /** Files are uploaded: ILS parts with no row in them are flagged. */
  hasFiles?: boolean
  selectable?: boolean
  selected?: boolean
  /** RFQ view: supplier quotes already on the RFQ item. */
  rfqQuotes?: PpRfqQuote[]
  /** Start opened (else: opened when it has suppliers). */
  startOpen?: boolean | null
}>(), { showOwner: false, hasFiles: false, selectable: false, selected: false, rfqQuotes: () => [], startOpen: null })

const emit = defineEmits<{
  toggle: []
  'add-line': [board: PpBoardPart]
  'edit-line': [line: PpQuoteLine]
  /** Something changed on the server (Done, line removed) — reload. */
  changed: []
  toast: [text: string, color: string]
  'edit-part': [part: PpPart]
  move: [ids: number[], track: string]
  hide: [ids: number[]]
  show: [ids: number[]]
  delete: [ids: number[]]
}>()

const api = useProcurementPlusApi()
const part = computed(() => props.board.part)
const track = computed(() => trackMeta(part.value.track))
const hasTimeline = computed(() => part.value.track !== 'ILS')
const open = ref(props.startOpen ?? props.board.lines.length > 0)

const statusKey = computed(() => partStatus(props.board))
const status = computed(() => PART_STATUS[statusKey.value])
const pending = computed(() => props.board.lines.filter(l => l.status !== 'Accepted'))
const statusText = computed(() => {
  if (statusKey.value === 'sourcing') {
    const priced = pending.value.filter(l => l.line.price != null).length
    return `${priced}/${pending.value.length} priced`
  }
  if (statusKey.value === 'none') return 'No supplier'
  return status.value.title
})
const hasInfo = computed(() => part.value.openRfqs.length > 0 || part.value.liveQuotes.length > 0 || part.value.track === 'ILS')

const supplierName = (l: PpQuoteLine) => l.preferredName || l.line.supplierName || l.line.supplierNameRaw || '—'

/** At / under target → green, above → orange. */
function targetClass(price: number | string) {
  if (!part.value.targetPrice) return ''
  return Number(price) <= Number(part.value.targetPrice) ? 'text-success' : 'text-orange'
}

// ── Done: every supplier line priced and pointing at a catalog supplier ──
const canFinish = computed(() => pending.value.length > 0 && pending.value.every(l => l.line.price != null && l.line.supplierId))
const finishHint = computed(() => {
  if (!pending.value.length) return 'Add a supplier first'
  if (pending.value.some(l => l.line.price == null)) return 'Every supplier needs a price (or remove it)'
  if (pending.value.some(l => !l.line.supplierId)) return 'Pick a catalog supplier on the lines marked "not in catalog"'
  return 'Put these prices on the RFQs'
})

const finishing = ref(false)
async function finish() {
  finishing.value = true
  try {
    const res = await api.post<any>('/procurement-plus/quotes/done', { ids: pending.value.map(l => l.line.id) })
    const s = res.summary
    emit('toast', s.failed.length
      ? `${s.accepted} done · ${s.failed.map((f: any) => `${f.partNumber}: ${f.reason}`).join(' · ')}`
      : `${part.value.partNumber}: ${s.accepted} price${s.accepted === 1 ? '' : 's'} set · ${s.quotesWritten} RFQ quote${s.quotesWritten === 1 ? '' : 's'} written`,
    s.failed.length ? 'warning' : 'success')
    emit('changed')
  } catch (e: any) {
    emit('toast', e?.data?.message || 'Could not finish', 'error')
  } finally {
    finishing.value = false
  }
}

async function removeLine(l: PpQuoteLine) {
  await api.post('/procurement-plus/quotes/remove', { ids: [l.line.id] })
  emit('changed')
}

// ── Timeline ──
const note = ref('')
const noting = ref(false)
async function addNote() {
  const text = note.value.trim()
  if (!text) return
  noting.value = true
  try {
    const ev = await api.post<PpEvent>(`/procurement-plus/parts/${part.value.id}/notes`, { text })
    props.board.events = [ev, ...props.board.events]
    note.value = ''
  } catch (e: any) {
    emit('toast', e?.data?.message || 'Could not add the note', 'error')
  } finally {
    noting.value = false
  }
}

const EVENT_META: Record<string, { icon: string; color: string }> = {
  Note: { icon: 'mdi-message-text-outline', color: 'primary' },
  Added: { icon: 'mdi-plus', color: 'grey' },
  Track: { icon: 'mdi-swap-horizontal', color: 'teal' },
  Target: { icon: 'mdi-target', color: 'amber-darken-2' },
  Supplier: { icon: 'mdi-domain-plus', color: 'info' },
  Sourcing: { icon: 'mdi-send-outline', color: 'info' },
  Removed: { icon: 'mdi-close', color: 'error' },
  Done: { icon: 'mdi-check-bold', color: 'success' },
}
const eventMeta = (kind: string) => EVENT_META[kind] ?? EVENT_META.Note!

// ── WeChat request text ──
function copyRequest() {
  const p = part.value
  const ok = copyText(buildRequestText('', [{ partNumber: p.partNumber, qty: p.qty, condition: p.condition, description: p.description }]))
  emit('toast', ok ? 'Request text copied — paste it into WeChat' : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
}
</script>

<style scoped>
.pc {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-left-width: 4px;
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
  margin-bottom: 12px;
  overflow: hidden;
  transition: box-shadow 0.15s;
}
.pc:hover { box-shadow: 0 2px 10px rgba(0, 0, 0, 0.06); }
.pc--deep-purple { border-left-color: rgb(var(--v-theme-primary)); }
.pc--teal { border-left-color: #009688; }
.pc--green { border-left-color: #4caf50; }
.pc--hidden { opacity: 0.55; }
.pc--on { box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.45); }

.pc__head {
  display: grid;
  grid-template-columns: auto minmax(200px, 1.1fr) minmax(0, 1.6fr) auto;
  gap: 14px;
  align-items: center;
  padding: 10px 12px;
  cursor: pointer;
}
.pc__check { align-self: start; margin-top: -2px; }
.pc__part { min-width: 0; }
.pc__pn { font-size: 15.5px; font-weight: 800; letter-spacing: 0.01em; }

.pc__stats { display: flex; gap: 8px; flex-wrap: wrap; align-items: stretch; min-width: 0; }
.pc__stat {
  min-width: 92px; padding: 4px 10px; border-radius: 8px; text-align: center;
  background: rgba(var(--v-theme-on-surface), 0.035);
  border: 1px solid rgba(var(--v-border-color), 0.08);
}
.pc__stat-cond { font-size: 10.5px; font-weight: 800; letter-spacing: 0.06em; opacity: 0.6; }
.pc__stat-avg { font-size: 14px; font-weight: 800; line-height: 1.2; }
.pc__stat-sub { font-size: 10.5px; opacity: 0.6; white-space: nowrap; }
.pc__nostats { align-self: center; opacity: 0.6; }

.pc__side { display: flex; flex-direction: column; align-items: flex-end; }

.pc__info, .pc__rfqquotes {
  display: flex; align-items: center; flex-wrap: wrap; gap: 6px;
  padding: 6px 14px 8px 46px; font-size: 12px;
  border-top: 1px dashed rgba(var(--v-border-color), 0.12);
}
.pc__rfqquotes { background: rgba(var(--v-theme-primary), 0.03); }
.pc__sep { width: 1px; height: 14px; background: rgba(var(--v-border-color), 0.25); margin: 0 4px; }

.pc__body { display: grid; grid-template-columns: 1fr; border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); }
.pc__body--timeline { grid-template-columns: minmax(0, 1fr) 320px; }

@media (max-width: 1100px) {
  .pc__head { grid-template-columns: auto 1fr auto; }
  .pc__stats { grid-column: 2 / -1; grid-row: 2; }
  .pc__body--timeline { grid-template-columns: 1fr; }
}
@media (max-width: 700px) {
  .pc__head { grid-template-columns: auto 1fr; }
  .pc__side { grid-column: 1 / -1; align-items: flex-start; }
  .pc__info, .pc__rfqquotes { padding-left: 14px; }
}

.grid-scroll { overflow-x: auto; }
.sup-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.sup-table th {
  text-align: left; font-size: 11.5px; font-weight: 600; opacity: 0.65; padding: 8px 10px; white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
}
.sup-table td { padding: 7px 10px; border-bottom: 1px solid rgba(var(--v-border-color), 0.05); vertical-align: top; }
.sup-table tbody tr { cursor: pointer; transition: background 0.1s; }
.sup-table tbody tr:hover { background: rgba(var(--v-theme-primary), 0.05); }
.row--done { background: rgba(var(--v-theme-success), 0.05); }
.num { text-align: right; white-space: nowrap; }
.nowrap { white-space: nowrap; }
.timeline { border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); padding: 10px 12px; }
@media (max-width: 1100px) { .timeline { border-left: 0; border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); } }
.timeline__scroll { max-height: 280px; overflow-y: auto; }
.timeline__text { font-size: 13px; line-height: 1.35; white-space: pre-wrap; }
</style>
