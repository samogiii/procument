<template>
  <v-dialog :model-value="modelValue" :fullscreen="mobile" max-width="1500" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card class="avail">
      <!-- Header -->
      <div class="avail__head">
        <div class="d-flex align-center flex-wrap gap-2">
          <v-avatar color="primary" variant="tonal" size="40" class="mr-1"><v-icon icon="mdi-store-search-outline" /></v-avatar>
          <div class="mr-4">
            <div class="text-h6 font-weight-bold">Suppliers available</div>
            <div class="text-caption text-medium-emphasis">
              {{ data?.uploads.length ?? 0 }} file{{ data?.uploads.length === 1 ? '' : 's' }} from the last {{ data?.freshDays ?? 7 }} days ·
              {{ partCount }} of your parts and RFQ items listed
            </div>
          </div>
          <v-spacer />
          <v-text-field
            v-model="search"
            placeholder="Filter part, supplier, comment…"
            prepend-inner-icon="mdi-magnify"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="max-width: 300px; min-width: 200px"
          />
          <v-btn-toggle v-if="tab === 'preferred'" v-model="groupBy" mandatory density="compact" variant="outlined" divided color="primary">
            <v-btn value="part" size="small" prepend-icon="mdi-cog-outline">By part</v-btn>
            <v-btn value="supplier" size="small" prepend-icon="mdi-domain">By supplier</v-btn>
          </v-btn-toggle>
          <v-btn icon="mdi-refresh" variant="text" size="small" :loading="loading" title="Reload" @click="emit('reload')" />
          <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
        </div>

        <v-tabs v-model="tab" color="primary" density="comfortable" class="mt-2" show-arrows>
          <v-tab value="preferred">
            <v-icon icon="mdi-star" color="amber" start />Preferred suppliers
            <v-chip size="x-small" class="ml-2" color="amber" variant="flat">{{ supplierCount(data?.preferred) }}</v-chip>
          </v-tab>
          <v-tab value="others">
            <v-icon icon="mdi-domain" start />Other suppliers
            <v-chip size="x-small" class="ml-2" variant="tonal">{{ supplierCount(data?.others) }}</v-chip>
            <v-chip v-if="data?.unmatched.length" size="x-small" class="ml-1" color="warning" variant="flat" prepend-icon="mdi-alert">
              {{ data.unmatched.length }} unknown
            </v-chip>
          </v-tab>
          <v-tab value="missing">
            <v-icon icon="mdi-help-circle-outline" start />No supplier
            <v-chip size="x-small" class="ml-2" :color="data?.missing.length ? 'error' : undefined" variant="tonal">{{ data?.missing.length ?? 0 }}</v-chip>
          </v-tab>
          <v-tab value="history">
            <v-icon icon="mdi-history" start />History
            <v-chip size="x-small" class="ml-2" variant="tonal">{{ data?.history.length ?? 0 }}</v-chip>
          </v-tab>
        </v-tabs>
      </div>
      <v-divider />

      <v-card-text class="avail__body">
        <v-progress-linear v-if="loading" indeterminate color="primary" height="2" class="mb-2" />

        <!-- ── Tab 1 / 2: offers ── -->
        <template v-if="tab === 'preferred' || tab === 'others'">
          <div class="d-flex align-center flex-wrap gap-2 mb-3">
            <template v-if="tab === 'preferred'">
              <div class="text-body-2 text-medium-emphasis">
                Preferred suppliers whose stock lists one of your parts.
              </div>
              <v-spacer />
              <v-btn
                color="primary"
                variant="flat"
                size="large"
                prepend-icon="mdi-send-circle-outline"
                :disabled="!data?.preferred.length"
                @click="emit('source')"
              >
                Source
              </v-btn>
            </template>
            <template v-else>
              <div class="text-body-2 text-medium-emphasis">
                Suppliers in your files that are not preferred — tick them to source them too, or star one to move it to Preferred.
              </div>
              <v-spacer />
              <v-btn
                v-if="data?.unmatched.length"
                color="warning"
                variant="tonal"
                prepend-icon="mdi-account-search-outline"
                @click="emit('resolve')"
              >
                Find {{ data.unmatched.length }} unknown compan{{ data.unmatched.length === 1 ? 'y' : 'ies' }}
              </v-btn>
              <v-btn
                color="primary"
                variant="flat"
                size="large"
                prepend-icon="mdi-send-circle-outline"
                :disabled="!picked.size"
                @click="emit('source', [...picked])"
              >
                Source{{ picked.size ? ` ${pickedSupplierCount} selected` : '' }}
              </v-btn>
            </template>
          </div>

          <div v-if="!groups.length" class="avail__empty">
            <v-icon :icon="tab === 'preferred' ? 'mdi-star-outline' : 'mdi-domain-off'" size="44" class="mb-2" />
            <div v-if="tab === 'preferred'">No preferred supplier lists your parts in these files.</div>
            <div v-else>No other suppliers.</div>
          </div>

          <div v-for="g in groups" :key="g.key" class="group">
            <div class="group__head">
              <v-checkbox-btn
                v-if="tab === 'others'"
                :model-value="g.rows.every(r => picked.has(r.offerId))"
                :indeterminate="!g.rows.every(r => picked.has(r.offerId)) && g.rows.some(r => picked.has(r.offerId))"
                density="compact"
                class="flex-grow-0 mr-1"
                title="Select these rows to source"
                @update:model-value="(v: boolean | null) => pickRows(g.rows, !!v)"
              />
              <template v-if="grouping === 'part'">
                <v-icon icon="mdi-cog-outline" size="18" class="mr-2 text-primary" />
                <span class="group__title">{{ g.title }}</span>
                <RfqItemChip :part-id="g.rows[0]!.partId" />
                <span v-if="g.subtitle" class="text-caption text-medium-emphasis ml-2 text-truncate">{{ g.subtitle }}</span>
                <v-spacer />
                <v-chip size="x-small" variant="tonal">{{ g.rows.length }} listing{{ g.rows.length === 1 ? '' : 's' }}</v-chip>
              </template>
              <template v-else>
                <v-icon v-if="tab === 'preferred'" icon="mdi-star" color="amber" size="18" class="mr-2" />
                <v-icon v-else :icon="g.rows[0]!.supplierId ? 'mdi-domain' : 'mdi-help-rhombus-outline'" size="18" class="mr-2" :color="g.rows[0]!.supplierId ? 'primary' : 'warning'" />
                <span class="group__title">{{ g.title }}</span>
                <span v-if="g.title !== g.rows[0]!.companyRaw" class="text-caption text-medium-emphasis ml-2">
                  as “{{ g.rows[0]!.companyRaw }}”
                </span>
                <!-- Not in the catalog yet: its emails from the files, to find or add it -->
                <span v-if="!g.rows[0]!.supplierId && emailsOf(g.rows).length" class="text-caption text-medium-emphasis ml-2 text-truncate" :title="emailsOf(g.rows).join(', ')">
                  <v-icon icon="mdi-email-outline" size="12" /> {{ emailsOf(g.rows).join(', ') }}
                </span>
                <v-chip v-if="g.rows[0]!.sourceMethod" size="x-small" :color="methodMeta(g.rows[0]!.sourceMethod).color" variant="tonal" class="ml-2" :prepend-icon="methodMeta(g.rows[0]!.sourceMethod).icon">
                  {{ methodMeta(g.rows[0]!.sourceMethod).title }}
                </v-chip>
                <v-spacer />
                <template v-if="tab === 'others'">
                  <v-btn
                    v-if="!g.rows[0]!.supplierId"
                    size="x-small"
                    variant="text"
                    color="warning"
                    prepend-icon="mdi-account-search-outline"
                    class="mr-1"
                    @click="emit('resolve')"
                  >
                    Find in catalog
                  </v-btn>
                  <!-- Preferred suppliers don't need the catalog: the company name, emails and phone are kept. -->
                  <v-btn
                    size="x-small"
                    variant="tonal"
                    color="amber-darken-2"
                    prepend-icon="mdi-star-plus-outline"
                    :loading="starring === g.key"
                    @click="prefer(g)"
                  >
                    Make preferred
                  </v-btn>
                </template>
              </template>
            </div>
            <div class="grid-scroll">
              <table class="offers">
                <thead>
                  <tr>
                    <th v-if="tab === 'others'" style="width: 36px" />
                    <th v-if="grouping === 'supplier'">Part</th>
                    <th v-else>Supplier</th>
                    <th>Supplier comment</th>
                    <th class="num">Qty</th>
                    <th>Cond</th>
                    <th>P/N in file · Alt</th>
                    <th>Description</th>
                    <th class="num">Price</th>
                    <th>Contact</th>
                    <th>Distance</th>
                    <th>File</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="r in g.rows" :key="`${r.offerId}-${r.partId}`" :class="{ 'row--picked': picked.has(r.offerId) }">
                    <td v-if="tab === 'others'">
                      <v-checkbox-btn :model-value="picked.has(r.offerId)" density="compact" @update:model-value="(v: boolean | null) => pickRows([r], !!v)" />
                    </td>
                    <td v-if="grouping === 'supplier'" class="font-weight-medium nowrap">
                      {{ r.partNumber }}
                      <RfqItemChip :part-id="r.partId" />
                      <StateChip :state="r.sourcingState" />
                    </td>
                    <td v-else class="nowrap">
                      <div class="d-flex align-center gap-1">
                        <v-icon v-if="tab === 'preferred'" icon="mdi-star" color="amber" size="14" />
                        <span class="font-weight-medium">{{ supplierLabel(r) }}</span>
                        <v-chip v-if="!r.supplierId && tab === 'others'" size="x-small" color="warning" variant="tonal" title="Not found in the catalog">unknown</v-chip>
                        <v-chip v-else-if="r.supplierStatus && r.supplierStatus !== 'Approved'" size="x-small" color="warning" variant="tonal">{{ r.supplierStatus }}</v-chip>
                        <StateChip :state="r.sourcingState" />
                      </div>
                      <div v-if="supplierLabel(r) !== r.companyRaw" class="text-caption text-medium-emphasis">as “{{ r.companyRaw }}”</div>
                    </td>
                    <td class="comment" :title="r.supplierComment || ''">{{ r.supplierComment }}</td>
                    <td class="num">{{ r.qty ?? r.qtyRaw }}</td>
                    <td><v-chip v-if="r.condition" size="x-small" variant="outlined">{{ r.condition }}</v-chip></td>
                    <td class="nowrap">
                      <span :class="{ 'text-info': r.filePartNo.toUpperCase() !== r.partNumber.toUpperCase() }">{{ r.filePartNo }}</span>
                      <span v-if="r.altPartNo" class="text-medium-emphasis"> · {{ r.altPartNo }}</span>
                    </td>
                    <td class="desc" :title="r.description || ''">{{ r.description }}</td>
                    <td class="num font-weight-medium">{{ r.price != null ? fmtMoney(r.price) : (r.priceRaw || '') }}</td>
                    <td class="nowrap text-caption">
                      <div v-if="r.email">{{ r.email }}</div>
                      <div v-if="r.phone" class="text-medium-emphasis">{{ r.phone }}</div>
                    </td>
                    <td class="nowrap text-caption">{{ r.distance }}</td>
                    <td class="nowrap text-caption text-medium-emphasis">{{ r.fileName }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </template>

        <!-- ── Tab 3: parts no supplier lists ── -->
        <template v-else-if="tab === 'missing'">
          <div class="d-flex align-center flex-wrap gap-2 mb-3">
            <div class="text-body-2 text-medium-emphasis">
              Parts you are working on that no row in your files lists (by part number or alternative).
              Select them and move them to Project.
            </div>
            <v-spacer />
            <v-checkbox-btn
              v-if="missing.length"
              :model-value="missing.every(m => missingPicked.has(m.partId))"
              :indeterminate="missingPicked.size > 0 && !missing.every(m => missingPicked.has(m.partId))"
              label="Select all"
              density="compact"
              inline
              class="flex-0-0 pp-select-all"
              @update:model-value="(v: boolean | null) => missingPicked = new Set(v ? missing.map(m => m.partId) : [])"
            />
            <v-btn
              color="teal"
              variant="flat"
              prepend-icon="mdi-briefcase-search-outline"
              :disabled="!missingPicked.size"
              @click="emit('move', [...missingPicked], 'Project')"
            >
              Move {{ missingPicked.size || '' }} to Project
            </v-btn>
            <v-btn
              color="green"
              variant="tonal"
              prepend-icon="mdi-wechat"
              :disabled="!missingPicked.size"
              @click="emit('move', [...missingPicked], 'WeChat')"
            >
              To WeChat
            </v-btn>
          </div>
          <div v-if="!missing.length" class="avail__empty">
            <v-icon icon="mdi-check-decagram" color="success" size="44" class="mb-2" />
            <div>Every part has at least one supplier in your files.</div>
          </div>
          <div class="missing-grid">
            <div
              v-for="m in missing"
              :key="m.partId"
              class="missing-card"
              :class="{ 'missing-card--on': missingPicked.has(m.partId) }"
              @click="toggleMissing(m.partId)"
            >
              <div class="d-flex align-start">
                <v-checkbox-btn :model-value="missingPicked.has(m.partId)" density="compact" class="flex-grow-0 mr-1 mt-n1" />
                <div class="min-w-0">
                  <div class="text-subtitle-2 font-weight-bold">{{ m.partNumber }}</div>
                  <div class="text-caption text-medium-emphasis text-truncate">{{ m.description || '—' }}</div>
                </div>
                <v-spacer />
                <v-btn icon="mdi-eye-off-outline" size="x-small" variant="text" title="Remove from my parts" @click.stop="emit('hide-part', m.partId)" />
              </div>
              <div v-if="m.alternatives.length" class="text-caption mt-1">Alt: {{ m.alternatives.join(', ') }}</div>
              <div class="d-flex gap-1 mt-2">
                <v-chip size="x-small" :color="m.openRfqCount ? 'primary' : undefined" variant="tonal" prepend-icon="mdi-file-document-outline">
                  {{ m.openRfqCount }} open RFQ{{ m.openRfqCount === 1 ? '' : 's' }}
                </v-chip>
                <v-chip size="x-small" variant="outlined" prepend-icon="mdi-web" :href="searchHref(m.partNumber)" target="_blank" rel="noopener" @click.stop>Search web</v-chip>
              </div>
            </div>
          </div>
        </template>

        <!-- ── Tab 4: history ── -->
        <template v-else>
          <div class="d-flex align-center flex-wrap gap-2 mb-3">
            <div class="text-body-2 text-medium-emphasis">
              Suppliers we worked with on these parts that are not in your current files — supplier quotes on RFQs, and
              files older than {{ data?.freshDays ?? 7 }} days.
            </div>
            <v-spacer />
            <v-chip-group v-model="historyKind" selected-class="text-primary" mandatory>
              <v-chip value="all" size="small" variant="outlined" filter>All</v-chip>
              <v-chip value="RFQ quote" size="small" variant="outlined" filter>RFQ quotes</v-chip>
              <v-chip value="File" size="small" variant="outlined" filter>Older files</v-chip>
            </v-chip-group>
          </div>
          <div v-if="!history.length" class="avail__empty">
            <v-icon icon="mdi-history" size="44" class="mb-2" />
            <div>No earlier suppliers for these parts.</div>
          </div>
          <div v-else class="grid-scroll">
            <table class="offers">
              <thead>
                <tr>
                  <th>Part</th>
                  <th>Supplier</th>
                  <th>From</th>
                  <th class="num">Last price</th>
                  <th>Cond</th>
                  <th class="num">Qty</th>
                  <th>Last seen</th>
                  <th class="num">Times</th>
                  <th>RFQ / file</th>
                  <th>Note</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(h, i) in history" :key="i">
                  <td class="font-weight-medium nowrap">{{ h.partNumber }}</td>
                  <td class="nowrap">
                    <v-icon v-if="h.isPreferred" icon="mdi-star" color="amber" size="14" class="mr-1" />{{ h.supplierName }}
                  </td>
                  <td><v-chip size="x-small" :color="h.kind === 'File' ? 'grey' : 'primary'" variant="tonal">{{ h.kind }}</v-chip></td>
                  <td class="num font-weight-medium">{{ fmtMoney(h.lastPrice) }}</td>
                  <td>{{ h.condition }}</td>
                  <td class="num">{{ h.qty ?? '' }}</td>
                  <td class="nowrap">{{ fmtDate(h.lastDate) }}</td>
                  <td class="num">{{ h.times }}</td>
                  <td class="nowrap text-caption">{{ h.lastRfqName }}</td>
                  <td class="comment" :title="h.comment || ''">{{ h.comment }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>
      </v-card-text>
    </v-card>
    <v-snackbar v-model="snack" color="success" :timeout="3000">{{ snackText }}</v-snackbar>
  </v-dialog>
</template>

<script setup lang="ts">
import { defineComponent, h } from 'vue'
import { VChip } from 'vuetify/components'

export interface PpOfferRow {
  offerId: number; uploadId: number; partId: number; partNumber: string; filePartNo: string; altPartNo?: string
  supplierId?: number; supplierName?: string; supplierStatus?: string; supplierEmail?: string
  /** The preferred supplier (Preferreds table) the row matched — not necessarily in the catalog. */
  preferredId?: number; preferredName?: string
  companyRaw: string; companyKey: string; supplierComment?: string; qtyRaw?: string; qty?: number
  condition?: string; description?: string; price?: number; priceRaw?: string; email?: string; phone?: string
  distance?: string; cage?: string; fileName: string; uploadedAt: string; sourceMethod?: string; sourcingState?: string
}
export interface PpAvailability {
  freshDays: number
  uploads: { id: number; fileName: string; uploadedAt: string; rowCount: number; matchedRows: number }[]
  preferred: PpOfferRow[]
  others: PpOfferRow[]
  missing: { partId: number; partNumber: string; description?: string; alternatives: string[]; openRfqCount: number }[]
  history: {
    partId: number; partNumber: string; supplierId?: number; supplierName: string; kind: string; lastPrice?: number
    condition?: string; qty?: number; lastDate: string; times: number; lastRfqName?: string; comment?: string; isPreferred: boolean
  }[]
  unmatched: any[]
}

const props = defineProps<{ modelValue: boolean; data: PpAvailability | null; loading?: boolean; initialTab?: string }>()
const emit = defineEmits<{
  'update:modelValue': [v: boolean]
  /** Preferred tab: no ids (every preferred row). Others tab: the picked rows (offer ids). */
  source: [offerIds?: number[]]
  resolve: []
  reload: []
  'hide-part': [partId: number]
  /** Selected "No supplier" parts → Project / WeChat (the page asks for target prices). */
  move: [partIds: number[], track: string]
}>()

const api = useApi()
const missingPicked = ref(new Set<number>())
function toggleMissing(id: number) {
  const next = new Set(missingPicked.value)
  next.has(id) ? next.delete(id) : next.add(id)
  missingPicked.value = next
}
watch(() => props.data, () => {
  const ids = new Set((props.data?.missing ?? []).map(m => m.partId))
  missingPicked.value = new Set([...missingPicked.value].filter(id => ids.has(id)))
  const offers = new Set((props.data?.others ?? []).map(r => r.offerId))
  picked.value = new Set([...picked.value].filter(id => offers.has(id)))
})

// ── Others tab: rows picked to source (offer ids) ──
const picked = ref(new Set<number>())
function pickRows(rows: PpOfferRow[], on: boolean) {
  const next = new Set(picked.value)
  for (const r of rows) on ? next.add(r.offerId) : next.delete(r.offerId)
  picked.value = next
}
const pickedSupplierCount = computed(() =>
  new Set((props.data?.others ?? []).filter(r => picked.value.has(r.offerId)).map(supplierKey)).size)
const { mobile } = useDisplay()
const tab = ref('preferred')
const groupBy = ref<'part' | 'supplier'>('supplier')
/** Other suppliers are always by supplier (their parts are on the part cards already); Preferred has the toggle. */
const grouping = computed(() => (tab.value === 'others' ? 'supplier' : groupBy.value))
const search = ref('')
const historyKind = ref('all')

watch(() => props.modelValue, (open) => { if (open && props.initialTab) tab.value = props.initialTab })

const matches = (text: string) => {
  const q = (search.value || '').trim().toLowerCase()
  return !q || text.toLowerCase().includes(q)
}
const rowText = (r: PpOfferRow) =>
  [r.partNumber, r.filePartNo, r.altPartNo, r.supplierName, r.companyRaw, r.supplierComment, r.description, r.email].filter(Boolean).join(' ')

const partCount = computed(() =>
  new Set([...(props.data?.preferred ?? []), ...(props.data?.others ?? [])].map(r => r.partId)).size)

function supplierCount(rows?: PpOfferRow[]) {
  return new Set((rows ?? []).map(supplierKey)).size
}

/** One supplier: the preferred entry, else the catalog supplier, else the company as written. */
const supplierKey = (r: PpOfferRow) => (r.preferredId ? `p${r.preferredId}` : r.supplierId ? `s${r.supplierId}` : `c${r.companyKey}`)
const supplierLabel = (r: PpOfferRow) => r.preferredName || r.supplierName || r.companyRaw
const emailsOf = (rows: PpOfferRow[]) =>
  [...new Set(rows.map(r => r.email?.trim().toLowerCase()).filter((e): e is string => !!e))]

const groups = computed(() => {
  const rows = (tab.value === 'preferred' ? props.data?.preferred : props.data?.others) ?? []
  const map = new Map<string, { key: string; title: string; subtitle?: string; rows: PpOfferRow[] }>()
  for (const r of rows) {
    if (!matches(rowText(r))) continue
    const key = grouping.value === 'part' ? `part${r.partId}` : supplierKey(r)
    let g = map.get(key)
    if (!g) {
      g = grouping.value === 'part'
        ? { key, title: r.partNumber, subtitle: r.description, rows: [] }
        : { key, title: supplierLabel(r), rows: [] }
      map.set(key, g)
    }
    g.rows.push(r)
  }
  const list = [...map.values()]
  // Rows with a price first, then most stock.
  for (const g of list) g.rows.sort((a, b) => Number(b.price != null) - Number(a.price != null) || (b.qty ?? 0) - (a.qty ?? 0))
  return list.sort((a, b) => a.title.localeCompare(b.title))
})

const missing = computed(() => (props.data?.missing ?? []).filter(m => matches(`${m.partNumber} ${m.description ?? ''} ${m.alternatives.join(' ')}`)))
const history = computed(() => (props.data?.history ?? [])
  .filter(h => historyKind.value === 'all' || h.kind === historyKind.value)
  .filter(h => matches(`${h.partNumber} ${h.supplierName} ${h.comment ?? ''} ${h.lastRfqName ?? ''}`)))

// ── Make preferred ──
const starring = ref<string | null>(null)
const snack = ref(false)
const snackText = ref('')
/** Adds the company to Preferred suppliers with the name, emails and phone from the file (plus its catalog link if it has one). */
async function prefer(g: { key: string; rows: PpOfferRow[] }) {
  const first = g.rows[0]!
  starring.value = g.key
  try {
    await api.post('/procurement-plus/preferred-suppliers', {
      name: first.supplierName || first.companyRaw,
      emails: [...new Set(g.rows.map(r => r.email).filter(Boolean))],
      phone: g.rows.find(r => r.phone)?.phone ?? null,
      supplierId: first.supplierId ?? null,
    })
    snackText.value = 'Moved to Preferred suppliers'
    snack.value = true
    emit('reload')
  } finally {
    starring.value = null
  }
}

/** An item of one of your RFQs (By RFQ) not on your Parts list yet (partId < 0) — sourcing it adds it to Parts. */
const RfqItemChip = defineComponent({
  props: { partId: { type: Number, required: true } },
  setup(p) {
    return () => p.partId < 0
      ? h(VChip, {
          size: 'x-small',
          color: 'blue',
          variant: 'tonal',
          class: 'ml-1',
          prependIcon: 'mdi-file-document-outline',
          title: 'An item of your RFQs (By RFQ) — sourcing it puts it on your Parts list',
        }, () => 'RFQ item')
      : null
  },
})

const StateChip = defineComponent({
  props: { state: { type: String, default: null } },
  setup(p) {
    return () => p.state
      ? h(VChip, {
          size: 'x-small',
          color: p.state === 'Done' ? 'success' : 'info',
          variant: 'tonal',
          class: 'ml-1',
          prependIcon: p.state === 'Done' ? 'mdi-check' : 'mdi-progress-clock',
        }, () => p.state)
      : null
  },
})
</script>

<style scoped>
/* Vuetify wraps selection-control labels (white-space: normal) — "Select all" stays on one line. */
.pp-select-all :deep(.v-label) { white-space: nowrap; }
.avail__head { padding: 16px 20px 0; }
.avail__body { height: 72vh; padding: 16px 20px !important; }
.avail__empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  padding: 56px 16px; color: rgba(var(--v-theme-on-surface), 0.5);
}
.group {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 10px;
  margin-bottom: 14px;
  overflow: hidden;
}
.group__head {
  display: flex; align-items: center; padding: 8px 12px;
  background: rgba(var(--v-theme-primary), 0.05);
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.group__title { font-weight: 700; font-size: 14px; }
.grid-scroll { overflow-x: auto; }
.offers { width: 100%; border-collapse: collapse; font-size: 12.5px; }
.offers th {
  text-align: left; font-size: 11.5px; font-weight: 600; opacity: 0.65; padding: 6px 10px; white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
}
.offers td { padding: 6px 10px; border-bottom: 1px solid rgba(var(--v-border-color), 0.05); vertical-align: top; }
.offers tbody tr:hover { background: rgba(var(--v-theme-primary), 0.03); }
.offers tbody tr.row--picked { background: rgba(var(--v-theme-primary), 0.06); }
.offers .num { text-align: right; white-space: nowrap; }
.offers .nowrap { white-space: nowrap; }
.offers .comment { max-width: 280px; min-width: 160px; color: rgba(var(--v-theme-on-surface), 0.75); }
.offers .desc { max-width: 200px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.missing-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 12px; }
.missing-card.missing-card--on { background: rgba(var(--v-theme-primary), 0.06); border-color: rgba(var(--v-theme-primary), 0.5); }
.missing-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-left: 3px solid rgb(var(--v-theme-error));
  border-radius: 10px; padding: 10px 12px; cursor: pointer;
}
.min-w-0 { min-width: 0; }
</style>
