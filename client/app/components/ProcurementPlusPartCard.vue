<template>
  <!--
    One part: a compact header (part, status, average price per condition) and, when opened, its suppliers
    table (click a row to edit — autosaves) plus, for Project / WeChat, the tracing timeline.
    Used by the boards (Parts / Project / WeChat) and the RFQ view; tinted by the tab the part comes from.
  -->
  <section class="pc" :class="[`pc--${origin.color}`, `pc--o-${origin.value}`, { 'pc--hidden': part.hidden, 'pc--on': selected }]">
    <header class="pc__head" @click="open = !open">
      <v-checkbox-btn
        v-if="selectable && !standIn"
        :model-value="selected"
        density="compact"
        class="flex-grow-0 pc__check"
        @click.stop
        @update:model-value="emit('toggle')"
      />

      <!-- Left: the part -->
      <div class="pc__part">
        <div class="d-flex align-center flex-wrap gap-1">
          <!-- History: supplier quotes for the part on RFQs + its timeline (loaded on click) -->
          <v-badge :model-value="historyCount > 0" :content="historyCount" color="primary" floating offset-x="2" offset-y="2" @click.stop>
            <v-btn
              icon="mdi-history"
              size="x-small"
              density="comfortable"
              :variant="historyCount ? 'tonal' : 'text'"
              :color="historyCount ? 'primary' : undefined"
              :title="historyCount ? `History — ${historyCount} price${historyCount === 1 ? '' : 's'} known for this part` : 'History'"
              @click.stop="historyOpen = true"
            />
          </v-badge>
          <v-icon :icon="origin.icon" :color="origin.color" size="16" :title="`From ${origin.title}`" />
          <span class="pc__pn" title="Click to copy" @click.stop="copyPn">{{ part.partNumber }}<v-icon icon="mdi-content-copy" size="12" class="ml-1 pc__pn-icon" /></span>
          <v-chip v-if="part.qty" size="x-small" variant="outlined">Qty {{ part.qty }}</v-chip>
          <v-chip v-if="part.condition" size="x-small" variant="outlined">{{ part.condition }}</v-chip>
          <v-chip v-if="part.hidden" size="x-small" variant="outlined">removed</v-chip>
          <v-chip v-if="showOwner && part.ownerName" size="x-small" color="primary" variant="tonal" prepend-icon="mdi-account">{{ part.ownerName }}</v-chip>
        </div>
        <div class="text-caption text-medium-emphasis text-truncate" :title="part.description">{{ part.description || '—' }}</div>
        <!-- Not on the list yet: the saved RFQs asking for it -->
        <div v-if="standIn && part.fromRfqs.length" class="d-flex flex-wrap align-center gap-1 mt-1" @click.stop>
          <span class="text-caption font-weight-bold" :class="`text-${origin.color}`">From</span>
          <v-chip v-for="r in part.fromRfqs.slice(0, 3)" :key="r.rfqId" size="x-small" :color="origin.color" variant="tonal" :to="`/rfqs/${r.rfqId}`">
            {{ r.rfqName }}
          </v-chip>
          <span v-if="part.fromRfqs.length > 3" class="text-caption">+{{ part.fromRfqs.length - 3 }}</span>
        </div>
        <div v-if="part.targetPrice || part.alternatives.length" class="d-flex flex-wrap align-center gap-1 mt-1" @click.stop>
          <v-chip v-if="part.targetPrice" size="x-small" color="amber-darken-2" variant="flat" prepend-icon="mdi-target">{{ fmtMoney(part.targetPrice) }}</v-chip>
          <template v-if="part.alternatives.length">
            <span class="text-caption text-info font-weight-bold">alt</span>
            <v-chip
              v-for="a in part.alternatives.slice(0, ALT_SHOWN)"
              :key="a"
              size="x-small"
              color="info"
              variant="tonal"
              append-icon="mdi-content-copy"
              :title="`Copy ${a}`"
              @click="copyAlt(a)"
            >
              {{ a }}
            </v-chip>
            <v-menu v-if="part.alternatives.length > ALT_SHOWN" :close-on-content-click="false">
              <template #activator="{ props: altProps }">
                <v-chip v-bind="altProps" size="x-small" variant="outlined" color="info">+{{ part.alternatives.length - ALT_SHOWN }}</v-chip>
              </template>
              <v-card class="pa-2 pc__alt-menu">
                <div class="text-caption text-medium-emphasis mb-1">Click one to copy it</div>
                <div class="d-flex flex-wrap gap-1">
                  <v-chip
                    v-for="a in part.alternatives.slice(ALT_SHOWN)"
                    :key="a"
                    size="x-small"
                    color="info"
                    variant="tonal"
                    append-icon="mdi-content-copy"
                    @click="copyAlt(a)"
                  >
                    {{ a }}
                  </v-chip>
                </div>
              </v-card>
            </v-menu>
          </template>
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
            v-if="!standIn"
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
            <!-- An RFQ item not on the list: put it on the list, or look it up -->
            <v-list v-if="standIn" density="compact">
              <v-list-item prepend-icon="mdi-playlist-plus" title="Add to my Parts list" @click="emit('add-to-list', board)" />
              <v-list-item prepend-icon="mdi-web" title="Search the web" :href="searchHref(part.partNumber)" target="_blank" rel="noopener" />
            </v-list>
            <v-list v-else density="compact">
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
          <v-btn v-if="board.offers?.length" :icon="open ? 'mdi-chevron-up' : 'mdi-chevron-down'" size="small" variant="text" :title="open ? 'Hide what your files list' : 'Show what your files list'" @click="open = !open" />
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
      <template v-if="part.offerCount || hasFiles">
        <span class="pc__sep" />
        <template v-if="part.offerCount">
          <v-chip size="x-small" color="amber-darken-2" variant="tonal" prepend-icon="mdi-star" title="Preferred suppliers in your files" @click="open = true">{{ part.preferredSupplierCount }} preferred</v-chip>
          <v-chip size="x-small" variant="tonal" prepend-icon="mdi-domain" title="Other suppliers in your files" @click="open = true">{{ part.otherSupplierCount }} other</v-chip>
        </template>
        <v-chip v-else-if="hasFiles" size="x-small" color="error" variant="tonal">not in your files</v-chip>
      </template>
      <template v-for="q in liveQuotes" :key="q.quoteId">
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

    <!-- Suppliers (always shown — editable), then what the files list for the part (collapses with the header) -->
    <div class="pc__body" :class="{ 'pc__body--timeline': hasTimeline }">
        <div class="min-w-0">
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
                :class="{
                  'row--done': l.status === 'Accepted',
                  'row--email': l.status !== 'Accepted' && l.emailQuote,
                  'row--file': l.status !== 'Accepted' && !l.emailQuote && l.fromFile,
                  'row--rfq': l.status !== 'Accepted' && !l.emailQuote && l.fromRfq,
                }"
                title="Click to edit"
                @click="emit('edit-line', l)"
              >
                <td class="nowrap">
                  <div class="d-flex align-center gap-1">
                    <v-icon v-if="l.preferredId" icon="mdi-star" color="amber" size="13" />
                    <span class="font-weight-medium">{{ supplierName(l) }}</span>
                    <v-chip v-if="l.status === 'Accepted'" size="x-small" color="success" variant="flat" prepend-icon="mdi-check">Done</v-chip>
                    <v-chip v-else-if="!l.line.supplierId" size="x-small" color="warning" variant="tonal" title="Pick a catalog supplier on the line to finish">not in catalog</v-chip>
                    <v-icon v-if="l.status !== 'Accepted' && !l.emailQuote && l.fromFile" icon="mdi-file-delimited-outline" color="warning" size="13" title="Added from your files" />
                  </div>
                  <!-- How the part is searched with this supplier: ILS / Send Email / Search (click to change) -->
                  <div v-if="lineMethods(l.sourceMethod).length || (l.status !== 'Accepted' && !l.fromRfq)" class="mt-1" @click.stop>
                    <ProcurementPlusMethodMenu
                      v-if="l.status !== 'Accepted'"
                      :initial="methodValues(l)"
                      confirm-text="Save"
                      @confirm="(m: string[]) => setMethods(l, m)"
                    >
                      <template #activator="{ props: menuProps }">
                        <span v-bind="menuProps" class="pc__methods" title="How you search this supplier — click to change">
                          <v-chip
                            v-for="m in lineMethods(l.sourceMethod)"
                            :key="m.value"
                            size="x-small"
                            variant="tonal"
                            :color="m.color"
                            :prepend-icon="m.icon"
                          >
                            {{ m.title }}
                          </v-chip>
                          <v-chip v-if="!lineMethods(l.sourceMethod).length" size="x-small" variant="outlined" prepend-icon="mdi-help-circle-outline">How searched?</v-chip>
                        </span>
                      </template>
                    </ProcurementPlusMethodMenu>
                    <span v-else class="pc__methods">
                      <v-chip v-for="m in lineMethods(l.sourceMethod)" :key="m.value" size="x-small" variant="tonal" :color="m.color" :prepend-icon="m.icon">{{ m.title }}</v-chip>
                    </span>
                  </div>
                  <!-- Not in the catalog yet: its emails, to find or add it -->
                  <div v-if="l.status !== 'Accepted' && !l.line.supplierId && lineEmails(l).length" class="text-caption text-medium-emphasis pc__emails">
                    <v-icon icon="mdi-email-outline" size="12" /> {{ lineEmails(l).join(', ') }}
                  </div>
                  <!-- Copied from my own quote on an RFQ (last 2 weeks): edit it here, Done puts it on the other RFQs -->
                  <div
                    v-if="l.fromRfq"
                    class="text-caption d-flex align-center gap-1 pc__from-rfq"
                    :title="`Your quote on ${l.fromRfq.rfqName}${l.fromRfq.customerCode ? ` (${l.fromRfq.customerCode})` : ''}, deadline ${fmtDate(l.fromRfq.deadline)} — Done puts it on the other RFQs for this part, not back on this one`"
                  >
                    <v-icon icon="mdi-file-document-arrow-right-outline" size="13" />
                    From
                    <NuxtLink :to="`/rfqs/${l.fromRfq.rfqId}`" class="pc__from-rfq-link" @click.stop>{{ l.fromRfq.rfqName }}</NuxtLink>
                    <span v-if="l.fromRfqQuotedAt" class="text-medium-emphasis">· {{ fmtDate(l.fromRfqQuotedAt) }}</span>
                  </div>
                  <div v-if="l.status === 'Accepted' && l.writtenRfqs.length" class="text-caption text-success">
                    → {{ l.writtenRfqs.map(r => r.rfqName).join(', ') }}
                  </div>
                  <!-- The supplier quoted this by email: the price came from that mail -->
                  <div
                    v-if="l.status !== 'Accepted' && l.emailQuote"
                    class="text-caption text-blue d-flex align-center gap-1"
                    :title="[l.emailQuote.subject, l.emailQuote.supplierEmail].filter(Boolean).join(' · ')"
                  >
                    <v-icon icon="mdi-email-check-outline" size="13" />
                    Quoted by email {{ fmtDate(l.emailQuote.date) }} — price from the mail.
                    <a href="#" class="text-error" @click.stop.prevent="removeLine(l)">Remove this line</a>
                    <span class="text-medium-emphasis">if you keep the email quote.</span>
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
              <!-- Quotes read from email (Email Quotes) for this part — edit them here too -->
              <tr
                v-for="l in emailLines"
                :key="`e${l.line.id}`"
                class="row--email"
                title="From Email Quotes — click to edit"
                @click="emit('edit-line', l)"
              >
                <td class="nowrap">
                  <div class="d-flex align-center gap-1">
                    <v-icon icon="mdi-email-outline" color="blue" size="13" />
                    <span class="font-weight-medium">{{ supplierName(l) }}</span>
                    <v-chip
                      size="x-small"
                      :color="l.status === 'Accepted' ? 'success' : 'blue'"
                      variant="tonal"
                      :title="l.status === 'Accepted' ? 'Accepted on Email Quotes' : 'Waiting on Email Quotes'"
                    >
                      {{ l.status === 'Accepted' ? 'Email · accepted' : 'Email' }}
                    </v-chip>
                  </div>
                  <div class="text-caption text-medium-emphasis text-truncate pc__email-subj" :title="l.line.emailSubject">
                    {{ fmtDate(l.line.emailDate) }} · {{ l.line.supplierEmail || l.line.emailSubject }}
                  </div>
                </td>
                <td class="nowrap">{{ l.line.alt || (pnKey(l.line.partNumber) !== pnKey(part.partNumber) ? l.line.partNumber : '') }}</td>
                <td>{{ l.line.condition }}</td>
                <td class="num">{{ l.line.qty ?? '' }}</td>
                <td class="num font-weight-bold" :class="l.line.price == null ? 'text-error' : targetClass(l.line.price)">
                  {{ l.line.price != null ? fmtMoney(l.line.price) : (l.line.priceRaw || '—') }}
                </td>
                <td class="nowrap">{{ l.line.certName }}</td>
                <td class="nowrap">{{ l.line.tagDate ? fmtDate(l.line.tagDate) : (l.line.tagDateRaw || '') }}</td>
                <td class="nowrap">{{ l.line.taggedBy }}</td>
                <td class="nowrap">{{ l.line.leadTime }}</td>
                <td />
              </tr>
              <tr v-if="!board.lines.length && !emailLines.length">
                <td colspan="10" class="text-center text-medium-emphasis py-3">
                  No suppliers yet — <a href="#" @click.prevent="emit('add-line', board)">add one</a>.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <template v-if="board.offers?.length">
          <button v-if="!open" type="button" class="pc__files-toggle" @click="open = true">
            <v-icon icon="mdi-chevron-down" size="16" />
            Available in your files · {{ board.offers.length }} listing{{ board.offers.length === 1 ? '' : 's' }}
          </button>
          <v-expand-transition>
            <ProcurementPlusFileOffers
              v-if="open"
              :rows="board.offers"
              :lines="board.lines"
              @add="(rows: PpOfferRow[], m: string[]) => emit('add-offer', board, rows, m)"
            />
          </v-expand-transition>
        </template>
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
    <slot />
    <ProcurementPlusDoneCheck ref="doneCheck" />
    <ProcurementPlusPartHistoryDialog
      v-if="historyOpen"
      v-model="historyOpen"
      :part-id="part.id"
      :part-number="part.partNumber"
      :lines="board.lines"
      @add-supplier="addHistorySupplier"
    />
  </section>
</template>

<script setup lang="ts">
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import { lineMethods, originMeta } from '~/utils/procurementPlus'
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
  /** A file row → a supplier line under the part, searched by these methods (ILS / Email / Search). */
  'add-offer': [board: PpBoardPart, rows: PpOfferRow[], methods: string[]]
  /** A saved-RFQ item not on the list (Parts tab) → put it on the list. */
  'add-to-list': [board: PpBoardPart]
}>()

const api = useProcurementPlusApi()
const part = computed(() => props.board.part)
/** Which tab it comes from — the card is tinted very lightly in its colour. */
const origin = computed(() => originMeta(part.value.track))
/** An item of a saved RFQ that is not on the list yet (Parts tab): no Done / edit until it is added. */
const standIn = computed(() => part.value.id < 0)

// ── History (clock button) ──
const historyOpen = ref(false)
/** Prices known for the part (RFQ supplier quotes + prices typed here) — from the price stats already loaded. */
const historyCount = computed(() => part.value.priceStats.reduce((n, s) => n + s.count, 0))
const hasTimeline = computed(() => part.value.track === 'Project' || part.value.track === 'WeChat')
/**
 * Opens / closes only "Available in your files" — the supplier rows (RFQ quotes, email quotes, added suppliers)
 * always stay visible to edit. Closed at first, so a fresh upload does not fill every card.
 */
const open = ref(props.startOpen ?? false)
/** Quotes read from email (Email Quotes) for the part — editable rows under it. */
const emailLines = computed(() => props.board.emailLines ?? [])
/** Accepted quotes still filling new RFQs — email ones are rows in the table already. */
const liveQuotes = computed(() => part.value.liveQuotes.filter(q => q.source !== 'Email'))
const pnKey = (s?: string | null) => (s || '').replace(/\s+/g, '').toUpperCase()

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
const hasInfo = computed(() => part.value.openRfqs.length > 0 || liveQuotes.value.length > 0 || part.value.offerCount > 0 || props.hasFiles)

const supplierName = (l: PpQuoteLine) => l.preferredName || l.line.supplierName || l.line.supplierNameRaw || '—'
/** The line's address and its preferred supplier's — shown under suppliers not in the catalog. */
const lineEmails = (l: PpQuoteLine) =>
  [...new Set([l.line.supplierEmail, ...(l.preferredEmails ?? [])].filter(Boolean).map((e: string) => e.toLowerCase()))]

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

const doneCheck = ref<{ confirm: (ids: number[]) => Promise<boolean> } | null>(null)
const finishing = ref(false)
async function finish() {
  finishing.value = true
  try {
    const ids = pending.value.map(l => l.line.id)
    // Custom parts: warn when the part number is not in the database.
    if (!(await doneCheck.value?.confirm(ids) ?? true)) return
    const res = await api.post<any>('/procurement-plus/quotes/done', { ids })
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

// ── How each supplier line is searched (ILS / Email / Search) ──
const methodValues = (l: PpQuoteLine) => {
  const now = lineMethods(l.sourceMethod).map(m => m.value as string)
  return now.length ? now : ['ILS']
}
async function setMethods(l: PpQuoteLine, methods: string[]) {
  try {
    const res = await api.put<PpQuoteLine>(`/procurement-plus/quotes/${l.line.id}/methods`, { methods })
    l.sourceMethod = res.sourceMethod
  } catch (e: any) {
    emit('toast', e?.data?.message || 'Could not save', 'error')
  }
}

// ── History → add a supplier under the part (the name only) ──
async function addHistorySupplier(q: PpRfqQuote, methods: string[]) {
  try {
    let id = part.value.id
    // A saved-RFQ item not on the list: put it on the list (Parts) first.
    if (id < 0) {
      const res = await api.post<{ ids: number[] }>('/procurement-plus/parts', { track: 'ILS', items: [{ rfqItemId: part.value.rfqItemId }] })
      id = res.ids?.[0] ?? 0
      if (!id) throw new Error('Could not add the part')
    }
    await api.post(`/procurement-plus/parts/${id}/lines`, { supplierId: q.supplierId ?? null, supplierName: q.supplierName, methods, nameOnly: true })
    emit('toast', `${q.supplierName} added under ${part.value.partNumber} — fill its condition, qty and price`, 'success')
    emit('changed')
  } catch (e: any) {
    emit('toast', e?.data?.message || e?.message || 'Could not add the supplier', 'error')
  }
}

function copyPn() {
  const pn = part.value.partNumber
  const ok = copyText(pn)
  emit('toast', ok ? `${pn} copied` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
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

// ── Alternatives: each one copies on click ──
const ALT_SHOWN = 4
function copyAlt(alt: string) {
  const ok = copyText(alt)
  emit('toast', ok ? `${alt} copied` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
}

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
.pc--blue { border-left-color: #2196f3; }
/* Very light tint of the tab the part comes from (Parts / Project / WeChat / RFQs) */
.pc--o-parts { background: linear-gradient(rgba(103, 58, 183, 0.035), rgba(103, 58, 183, 0.035)), rgb(var(--v-theme-surface)); }
.pc--o-project { background: linear-gradient(rgba(0, 150, 136, 0.045), rgba(0, 150, 136, 0.045)), rgb(var(--v-theme-surface)); }
.pc--o-wechat { background: linear-gradient(rgba(76, 175, 80, 0.05), rgba(76, 175, 80, 0.05)), rgb(var(--v-theme-surface)); }
.pc--o-rfq { background: linear-gradient(rgba(33, 150, 243, 0.05), rgba(33, 150, 243, 0.05)), rgb(var(--v-theme-surface)); }
.min-w-0 { min-width: 0; }
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
.pc__pn { font-size: 15.5px; font-weight: 800; letter-spacing: 0.01em; cursor: copy; }
.pc__pn-icon { opacity: 0; transition: opacity 0.15s; }
.pc__pn:hover .pc__pn-icon { opacity: 0.6; }
.pc__methods { display: inline-flex; flex-wrap: wrap; gap: 3px; cursor: pointer; }
.pc__alt-menu { max-width: 360px; }

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
/* Added from "Available in your files" */
.row--file { background: rgba(var(--v-theme-warning), 0.1); }
/* Copied from my own quote on a recent RFQ */
.row--rfq { background: rgba(var(--v-theme-primary), 0.06); }
.sup-table tbody tr.row--rfq:hover { background: rgba(var(--v-theme-primary), 0.11); }
.pc__from-rfq { color: rgb(var(--v-theme-primary)); }
.pc__from-rfq-link { color: inherit; font-weight: 600; text-decoration: none; }
.pc__from-rfq-link:hover { text-decoration: underline; }
.sup-table tbody tr.row--file:hover { background: rgba(var(--v-theme-warning), 0.16); }
/* Read from email (Email Quotes colour) — an email row, or a line the supplier also quoted by email */
.row--email { background: rgba(33, 150, 243, 0.08); }
.sup-table tbody tr.row--email:hover { background: rgba(33, 150, 243, 0.14); }
.pc__email-subj { max-width: 260px; }
.pc__emails { max-width: 320px; white-space: normal; word-break: break-all; line-height: 1.3; }
.pc__files-toggle {
  display: flex; align-items: center; gap: 4px; width: 100%; padding: 6px 12px;
  font-size: 12px; font-weight: 700; opacity: 0.75; text-align: left;
  border-top: 1px dashed rgba(var(--v-border-color), 0.18);
}
.pc__files-toggle:hover { opacity: 1; background: rgba(var(--v-theme-primary), 0.04); }
.num { text-align: right; white-space: nowrap; }
.nowrap { white-space: nowrap; }
.timeline { border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); padding: 10px 12px; }
@media (max-width: 1100px) { .timeline { border-left: 0; border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); } }
.timeline__scroll { max-height: 280px; overflow-y: auto; }
.timeline__text { font-size: 13px; line-height: 1.35; white-space: pre-wrap; }
</style>
