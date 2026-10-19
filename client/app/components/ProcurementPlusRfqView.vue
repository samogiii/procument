<template>
  <!--
    "By RFQ": load an RFQ and see every item — which already have suppliers (quotes on the RFQ, or lines
    here) and which don't — then add the missing ones to the list and fill their suppliers and prices.
  -->
  <div>
    <!-- My RFQs: saved per user, so they are still here after a refresh -->
    <div v-if="myRfqs.length" class="rv-mine mb-3">
      <span class="text-caption font-weight-bold text-medium-emphasis mr-1">My RFQs</span>
      <v-chip
        v-for="r in myRfqs"
        :key="r.rfqId"
        size="small"
        :variant="r.rfqId === rfqId ? 'flat' : 'outlined'"
        :color="r.rfqId === rfqId ? 'primary' : (r.coveredCount === r.itemCount ? 'success' : undefined)"
        closable
        :title="`${r.customerCode || '—'} · deadline ${fmtDate(r.deadline)} · ${r.coveredCount} of ${r.itemCount} items have a supplier`"
        @click="rfqId = r.rfqId"
        @click:close="removeMine(r.rfqId)"
      >
        {{ r.name }}
        <span class="ml-1 text-caption" :class="r.coveredCount === r.itemCount ? 'text-success' : 'text-error'">{{ r.coveredCount }}/{{ r.itemCount }}</span>
      </v-chip>
    </div>

    <div class="rv-bar mb-3">
      <v-autocomplete
        v-model="rfqId"
        :items="rfqs"
        item-title="name"
        item-value="id"
        label="Load an RFQ"
        placeholder="Search RFQ name, id, customer or P/N"
        density="compact"
        variant="outlined"
        hide-details
        no-filter
        clearable
        :loading="rfqLoading"
        prepend-inner-icon="mdi-file-document-outline"
        style="min-width: 320px; max-width: 460px"
        @update:search="searchRfqs"
      >
        <template #item="{ props: itemProps, item }">
          <v-list-item v-bind="itemProps" :subtitle="`${item.raw.customerCode || '—'} · ${item.raw.itemCount} items · ${deadlineLabel(item.raw.deadline)}`" />
        </template>
      </v-autocomplete>
      <template v-if="coverage">
        <v-chip-group v-model="filter" mandatory selected-class="font-weight-bold" class="py-0">
          <v-chip value="all" size="small" variant="outlined" filter>All <strong class="ml-1">{{ coverage.items.length }}</strong></v-chip>
          <v-chip value="missing" size="small" variant="outlined" color="error" filter>No supplier <strong class="ml-1">{{ missingCount }}</strong></v-chip>
          <v-chip value="has" size="small" variant="outlined" color="success" filter>Has supplier <strong class="ml-1">{{ coverage.items.length - missingCount }}</strong></v-chip>
        </v-chip-group>
        <v-spacer />
        <v-btn
          v-if="notListed.length"
          size="small"
          variant="tonal"
          color="primary"
          prepend-icon="mdi-playlist-plus"
          :loading="adding === 'all'"
          @click="addItems(notListed.map(i => i.rfqItemId), 'ILS', 'all')"
        >
          Add {{ notListed.length }} missing to ILS Parts
        </v-btn>
        <v-btn size="small" variant="text" icon="mdi-refresh" :loading="loading" title="Reload" @click="load" />
      </template>
    </div>

    <div v-if="!rfqId" class="rv-empty">
      <v-icon icon="mdi-file-search-outline" size="48" class="mb-2" />
      <div class="text-body-1 font-weight-medium">Load an RFQ</div>
      <div class="text-body-2 text-medium-emphasis">See which of its parts have suppliers and which don't — and fill them.</div>
    </div>

    <template v-else-if="coverage">
      <!-- RFQ summary -->
      <div class="rv-sum mb-3">
        <div>
          <div class="text-subtitle-1 font-weight-bold">{{ coverage.rfq.name }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ coverage.rfq.customerCode || '—' }} · {{ coverage.rfq.status }} · deadline {{ fmtDate(coverage.rfq.deadline) }} ({{ deadlineLabel(coverage.rfq.deadline) }})
          </div>
        </div>
        <v-spacer />
        <div class="rv-progress">
          <v-progress-linear :model-value="coverage.items.length ? ((coverage.items.length - missingCount) / coverage.items.length) * 100 : 0" color="success" height="10" rounded />
          <div class="text-caption mt-1">
            <strong>{{ coverage.items.length - missingCount }}</strong> of {{ coverage.items.length }} items have a supplier
          </div>
        </div>
        <v-btn size="small" variant="text" :to="`/rfqs/${coverage.rfq.id}`" append-icon="mdi-open-in-new">Open RFQ</v-btn>
      </div>

      <v-progress-linear v-if="loading" indeterminate color="primary" height="2" class="mb-2" />

      <template v-for="it in shown" :key="it.rfqItemId">
        <!-- On my list: the full part card, with the quotes already on this RFQ -->
        <ProcurementPlusPartCard
          v-if="it.board"
          :board="it.board"
          :show-owner="showOwner"
          @add-line="openAddLine"
          @edit-line="openEdit"
          @changed="load"
          @toast="toast"
          @edit-part="(p: PpPart) => emit('edit-part', p)"
          @move="(ids: number[], t: string) => emit('move', ids, t)"
          @hide="(ids: number[]) => emit('hide', ids)"
          @show="(ids: number[]) => emit('show', ids)"
          @delete="(ids: number[]) => emit('delete', ids)"
        >
          <ProcurementPlusQuoteHistory
            :part-number="it.partNumber"
            :rfq-quotes="it.rfqQuotes"
            :older-quotes="it.olderQuotes"
            :older-count="it.olderQuoteCount"
          />
        </ProcurementPlusPartCard>
        <!-- Not on my list yet -->
        <section v-else class="rv-item" :class="{ 'rv-item--missing': !it.hasSupplier }">
          <div class="rv-item__grid">
          <v-icon :icon="it.hasSupplier ? 'mdi-check-circle' : 'mdi-alert-circle-outline'" :color="it.hasSupplier ? 'success' : 'error'" />
          <div class="rv-item__part">
            <div class="font-weight-bold">{{ it.partNumber }}<span v-if="it.alt" class="text-caption text-info"> · alt {{ it.alt }}</span></div>
            <div class="text-caption text-medium-emphasis text-truncate">{{ it.description || '—' }}</div>
            <div class="d-flex gap-1 mt-1">
              <v-chip size="x-small" variant="outlined">Qty {{ it.qty }}</v-chip>
              <v-chip v-if="it.condition" size="x-small" variant="outlined">{{ it.condition }}</v-chip>
            </div>
          </div>
          <div class="rv-item__quotes">
            <template v-if="it.rfqQuotes.length">
              <div class="text-caption font-weight-bold mb-1">On this RFQ</div>
              <v-chip v-for="(q, i) in it.rfqQuotes.slice(0, 4)" :key="i" size="x-small" variant="outlined" color="primary" class="mr-1 mb-1">
                {{ q.supplierName }} {{ fmtMoney(q.price) }} {{ q.condition }}
              </v-chip>
              <span v-if="it.rfqQuotes.length > 4" class="text-caption">+{{ it.rfqQuotes.length - 4 }}</span>
            </template>
            <span v-else class="text-caption text-error">No supplier yet</span>
          </div>
          <div class="rv-item__actions">
            <v-btn size="small" variant="flat" color="primary" prepend-icon="mdi-plus" :loading="adding === `s${it.rfqItemId}`" @click="addAndSupplier(it)">
              Supplier
            </v-btn>
            <v-menu>
              <template #activator="{ props: menuProps }">
                <v-btn v-bind="menuProps" size="small" variant="tonal" append-icon="mdi-chevron-down" :loading="adding === it.rfqItemId">Add to</v-btn>
              </template>
              <v-list density="compact">
                <v-list-item v-for="t in PART_TRACKS" :key="t.value" :prepend-icon="t.icon" :title="t.title" :subtitle="t.hint" @click="startAdd(it, t.value)" />
              </v-list>
            </v-menu>
          </div>
          </div>
          <ProcurementPlusQuoteHistory
            :part-number="it.partNumber"
            :rfq-quotes="it.rfqQuotes"
            :older-quotes="it.olderQuotes"
            :older-count="it.olderQuoteCount"
            :start-open="!it.hasSupplier && it.olderQuoteCount > 0"
          />
        </section>
      </template>
      <div v-if="!shown.length" class="text-center text-medium-emphasis pa-6">No items in this filter.</div>
    </template>
    <div v-else-if="loading" class="d-flex justify-center py-10"><v-progress-circular indeterminate color="primary" /></div>

    <!-- Target price for Project / WeChat -->
    <v-dialog v-model="target.open" max-width="420">
      <v-card>
        <v-card-title class="pa-4">Add {{ target.item?.partNumber }} to {{ trackMeta(target.track).title }}</v-card-title>
        <v-card-text class="pt-0">
          <v-text-field v-model.number="target.price" type="number" label="Target price *" prefix="$" density="compact" variant="outlined" autofocus hide-details />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="target.open = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :disabled="!(Number(target.price) > 0)" @click="confirmTarget">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <ProcurementPlusAddLineDialog v-model="addLine.open" :part-id="addLine.partId" :part-number="addLine.partNumber" @added="onLineAdded" />
    <EmailQuoteEditDialog
      v-model="edit.open"
      :row="edit.row"
      :accepted="edit.accepted"
      api-base="/procurement-plus/quotes"
      suppliers-url="/procurement-plus/suppliers"
      @updated="patchLine"
    />
    <v-snackbar v-model="snack" :color="snackColor" :timeout="5000">{{ snackText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { PpBoardPart, PpPart, PpQuoteLine, PpRfqCoverageItem } from '~/utils/procurementPlus'

defineProps<{ showOwner?: boolean }>()
const emit = defineEmits<{
  changed: []
  'edit-part': [part: PpPart]
  move: [ids: number[], track: string]
  hide: [ids: number[]]
  show: [ids: number[]]
  delete: [ids: number[]]
}>()

interface RfqOption { id: number; name: string; status: string; deadline: string; customerCode?: string; itemCount: number }
interface Coverage { rfq: RfqOption; items: PpRfqCoverageItem[] }

const api = useProcurementPlusApi()
const route = useRoute()
const router = useRouter()

// ── My RFQs (saved on the server, per user) ──
interface MyRfq { rfqId: number; name: string; customerCode?: string; status: string; deadline: string; itemCount: number; coveredCount: number }
const myRfqs = ref<MyRfq[]>([])
async function loadMine() {
  try { myRfqs.value = await api.get<MyRfq[]>('/procurement-plus/my-rfqs') } catch { myRfqs.value = [] }
}
async function removeMine(id: number) {
  await api.del(`/procurement-plus/my-rfqs/${id}`)
  myRfqs.value = myRfqs.value.filter(r => r.rfqId !== id)
  if (rfqId.value === id) rfqId.value = myRfqs.value[0]?.rfqId ?? null
}

// ── RFQ picker ──
const rfqId = ref<number | null>(null)
const rfqs = ref<RfqOption[]>([])
const rfqLoading = ref(false)
let rfqTimer: ReturnType<typeof setTimeout> | null = null
function searchRfqs(q: string) {
  if (rfqTimer) clearTimeout(rfqTimer)
  rfqTimer = setTimeout(async () => {
    rfqLoading.value = true
    try {
      const found = await api.get<RfqOption[]>(`/procurement-plus/rfqs?search=${encodeURIComponent(q || '')}`)
      const current = rfqs.value.find(r => r.id === rfqId.value)
      rfqs.value = current && !found.some(f => f.id === current.id) ? [current, ...found] : found
    } catch {
      rfqs.value = []
    } finally {
      rfqLoading.value = false
    }
  }, 300)
}

// ── Coverage ──
const coverage = ref<Coverage | null>(null)
const loading = ref(false)
const filter = ref<'all' | 'missing' | 'has'>('all')
const missingCount = computed(() => coverage.value?.items.filter(i => !i.hasSupplier).length ?? 0)
const notListed = computed(() => coverage.value?.items.filter(i => !i.board) ?? [])
const shown = computed(() => (coverage.value?.items ?? [])
  .filter(i => filter.value === 'all' || (filter.value === 'missing' ? !i.hasSupplier : i.hasSupplier))
  // Items still missing a supplier first.
  .sort((a, b) => Number(a.hasSupplier) - Number(b.hasSupplier)))

async function load() {
  if (!rfqId.value) { coverage.value = null; return }
  // The picker shows the name even when the RFQ came from My RFQs / the address bar.
  if (!rfqs.value.some(r => r.id === rfqId.value)) {
    const mine = myRfqs.value.find(r => r.rfqId === rfqId.value)
    if (mine) rfqs.value = [{ id: mine.rfqId, name: mine.name, status: mine.status, deadline: mine.deadline, customerCode: mine.customerCode, itemCount: mine.itemCount }, ...rfqs.value]
  }
  loading.value = true
  try {
    coverage.value = await api.get<Coverage>(`/procurement-plus/rfqs/${rfqId.value}/coverage`)
    if (!rfqs.value.some(r => r.id === coverage.value!.rfq.id)) rfqs.value = [coverage.value.rfq, ...rfqs.value]
  } catch (e: any) {
    coverage.value = null
    toast(e?.data?.message || 'Could not load the RFQ', 'error')
  } finally {
    loading.value = false
  }
}
watch(rfqId, async (id) => {
  filter.value = 'all'
  // Keep it in the address bar, so a refresh opens it again.
  const query = { ...route.query }
  if (id) query.rfq = String(id)
  else delete query.rfq
  router.replace({ query })
  await load()
  if (id) {
    // Saved on my list (moved to the top when opened again).
    await api.post('/procurement-plus/my-rfqs', { rfqId: id }).catch(() => {})
    await loadMine()
  }
})
watch(() => api.asUser.value, async () => {
  await loadMine()
  rfqId.value = myRfqs.value[0]?.rfqId ?? null
  await load()
})

// ── Add items to the list ──
const adding = ref<number | string | null>(null)
async function addItems(ids: number[], track: string, busyKey: number | string, targetPrice?: number) {
  adding.value = busyKey
  try {
    await api.post('/procurement-plus/parts', { track, items: ids.map(id => ({ rfqItemId: id, targetPrice: targetPrice ?? null })) })
    await Promise.all([load(), loadMine()])
    emit('changed')
    return true
  } catch (e: any) {
    toast(e?.data?.message || 'Could not add', 'error')
    return false
  } finally {
    adding.value = null
  }
}

const target = reactive<{ open: boolean; item: PpRfqCoverageItem | null; track: string; price: number | null }>({ open: false, item: null, track: 'Project', price: null })
function startAdd(it: PpRfqCoverageItem, track: string) {
  if (needsTargetPrice(track)) Object.assign(target, { open: true, item: it, track, price: null })
  else addItems([it.rfqItemId], track, it.rfqItemId)
}
async function confirmTarget() {
  const it = target.item
  target.open = false
  if (it) await addItems([it.rfqItemId], target.track, it.rfqItemId, Number(target.price))
}

/** "+ Supplier" on an item not on the list: add it as an ILS part, then open the supplier dialog for it. */
async function addAndSupplier(it: PpRfqCoverageItem) {
  if (!(await addItems([it.rfqItemId], 'ILS', `s${it.rfqItemId}`))) return
  const board = coverage.value?.items.find(i => i.rfqItemId === it.rfqItemId)?.board
  if (board) openAddLine(board)
}

// ── Lines ──
const addLine = reactive({ open: false, partId: 0, partNumber: '' })
function openAddLine(b: PpBoardPart) { Object.assign(addLine, { open: true, partId: b.part.id, partNumber: b.part.partNumber }) }
async function onLineAdded(line: PpQuoteLine) {
  await load()
  emit('changed')
  openEdit(line)
}

const edit = reactive<{ open: boolean; row: any; accepted: boolean }>({ open: false, row: null, accepted: false })
function openEdit(l: PpQuoteLine) {
  edit.row = l.line
  edit.accepted = l.status === 'Accepted'
  edit.open = true
}
function patchLine(row: any) {
  for (const it of coverage.value?.items ?? []) {
    const l = it.board?.lines.find(x => x.line.id === row.id)
    if (l) { l.line = { ...l.line, ...row }; break }
  }
}

const snack = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') { snackText.value = text; snackColor.value = color; snack.value = true }

onMounted(async () => {
  searchRfqs('')
  await loadMine()
  // Reopen: the RFQ in the address bar, else the last one opened.
  const fromUrl = Number(route.query.rfq)
  rfqId.value = fromUrl > 0 ? fromUrl : myRfqs.value[0]?.rfqId ?? null
})
defineExpose({ load: () => Promise.all([load(), loadMine()]) })
</script>

<style scoped>
.rv-bar { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; }
.rv-mine { display: flex; align-items: center; flex-wrap: wrap; gap: 6px; }
.rv-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center;
  padding: 56px 16px; border-radius: 12px; border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
}
.rv-sum {
  display: flex; align-items: center; flex-wrap: wrap; gap: 16px; padding: 12px 14px; border-radius: 12px;
  background: rgb(var(--v-theme-surface)); border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.rv-progress { min-width: 220px; }
.rv-item {
  margin-bottom: 12px; border-radius: 12px; overflow: hidden;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-left: 4px solid rgb(var(--v-theme-success));
}
.rv-item--missing { border-left-color: rgb(var(--v-theme-error)); }
.rv-item__grid {
  display: grid; grid-template-columns: auto minmax(200px, 1fr) minmax(0, 1.4fr) auto; gap: 14px; align-items: center;
  padding: 10px 12px;
}
.rv-item__part { min-width: 0; }
.rv-item__actions { display: flex; gap: 6px; }
@media (max-width: 900px) { .rv-item__grid { grid-template-columns: auto 1fr; } .rv-item__quotes, .rv-item__actions { grid-column: 2; } }
</style>
