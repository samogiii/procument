<template>
  <!-- "By supplier": each supplier with every part it is pricing on this board (paged by supplier). -->
  <div>
    <v-progress-linear v-if="loading" indeterminate color="primary" height="2" class="mb-2" />
    <div v-if="!loading && !groups.length" class="sv-empty">
      <v-icon icon="mdi-domain-off" size="44" class="mb-2" />
      <div class="text-body-1 font-weight-medium">{{ search ? 'No supplier matches' : 'No suppliers yet' }}</div>
      <div class="text-body-2 text-medium-emphasis">Add suppliers under the parts (or Source from Suppliers available).</div>
    </div>

    <section v-for="g in groups" :key="g.key" class="sv">
      <header class="sv__head" @click="toggleOpen(g.key)">
        <v-avatar :color="g.preferredId ? 'amber' : 'primary'" variant="tonal" size="34">
          <v-icon :icon="g.preferredId ? 'mdi-star' : 'mdi-domain'" size="18" />
        </v-avatar>
        <div class="min-w-0 flex-grow-1">
          <div class="d-flex align-center flex-wrap gap-2">
            <span class="sv__name">{{ g.name }}</span>
            <v-chip v-if="!g.supplierId" size="x-small" color="warning" variant="tonal">not in catalog</v-chip>
            <v-chip v-if="SOURCE_METHODS.some(m => m.value === g.sourceMethod)" size="x-small" variant="tonal" :color="methodMeta(g.sourceMethod).color" :prepend-icon="methodMeta(g.sourceMethod).icon">
              {{ methodMeta(g.sourceMethod).title }}
            </v-chip>
          </div>
          <div class="text-caption text-medium-emphasis">
            {{ g.lines.length }} part{{ g.lines.length === 1 ? '' : 's' }} ·
            {{ g.pricedCount }}/{{ g.pendingCount }} priced<span v-if="g.doneCount"> · {{ g.doneCount }} done</span>
          </div>
        </div>
        <v-progress-circular
          :model-value="g.pendingCount ? (g.pricedCount / g.pendingCount) * 100 : 100"
          :color="g.pendingCount && g.pricedCount === g.pendingCount ? 'success' : 'primary'"
          size="34"
          width="4"
        >
          <span class="text-caption">{{ g.pendingCount ? Math.round((g.pricedCount / g.pendingCount) * 100) : 100 }}</span>
        </v-progress-circular>
        <v-btn
          size="small"
          variant="flat"
          color="success"
          prepend-icon="mdi-check-bold"
          :disabled="!readyLines(g).length"
          :loading="finishing === g.key"
          title="Put this supplier's priced lines on the RFQs"
          @click.stop="finish(g)"
        >
          Done {{ readyLines(g).length || '' }}
        </v-btn>
        <v-btn :icon="isOpen(g.key) ? 'mdi-chevron-up' : 'mdi-chevron-down'" size="small" variant="text" @click.stop="toggleOpen(g.key)" />
      </header>

      <v-expand-transition>
        <div v-if="isOpen(g.key)" class="grid-scroll">
          <table class="sup-table">
            <thead>
              <tr>
                <th>Part</th>
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
              <tr v-for="l in g.lines" :key="l.line.id" :class="{ 'row--done': l.status === 'Accepted' }" title="Click to edit" @click="openEdit(l)">
                <td class="nowrap font-weight-bold">
                  {{ l.line.partNumber }}
                  <v-chip v-if="l.status === 'Accepted'" size="x-small" color="success" variant="flat" class="ml-1">Done</v-chip>
                </td>
                <td class="nowrap">{{ l.line.alt }}</td>
                <td>{{ l.line.condition }}</td>
                <td class="num">{{ l.line.qty ?? '' }}</td>
                <td class="num font-weight-bold" :class="{ 'text-error': l.line.price == null }">{{ l.line.price != null ? fmtMoney(l.line.price) : '—' }}</td>
                <td class="nowrap">{{ l.line.certName }}</td>
                <td class="nowrap">{{ l.line.tagDate ? fmtDate(l.line.tagDate) : (l.line.tagDateRaw || '') }}</td>
                <td class="nowrap">{{ l.line.taggedBy }}</td>
                <td class="nowrap">{{ l.line.leadTime }}</td>
                <td @click.stop>
                  <v-btn v-if="l.status !== 'Accepted'" icon="mdi-close" size="x-small" variant="text" color="error" title="No quote — remove" @click="removeLine(l)" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </v-expand-transition>
    </section>

    <div v-if="total > pageSize || page > 1" class="d-flex align-center justify-center flex-wrap gap-3 mt-2">
      <v-pagination v-model="page" :length="Math.max(1, Math.ceil(total / pageSize))" :total-visible="7" density="comfortable" rounded="circle" />
      <v-select v-model="pageSize" :items="[10, 25, 50, 100]" label="Per page" density="compact" variant="outlined" hide-details style="max-width: 120px" />
    </div>

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
import type { PpQuoteLine, PpSupplierGroup } from '~/utils/procurementPlus'

const props = defineProps<{ track: string; search: string }>()
const emit = defineEmits<{ changed: [] }>()

const api = useProcurementPlusApi()
const groups = ref<PpSupplierGroup[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(25)
const loading = ref(false)
const opened = ref(new Set<string>())
const isOpen = (key: string) => opened.value.has(key)
function toggleOpen(key: string) {
  const next = new Set(opened.value)
  next.has(key) ? next.delete(key) : next.add(key)
  opened.value = next
}

async function load() {
  loading.value = true
  try {
    const params = new URLSearchParams({ track: props.track, page: String(page.value), pageSize: String(pageSize.value) })
    if (props.search) params.set('search', props.search)
    const res = await api.get<{ items: PpSupplierGroup[]; total: number; page: number }>(`/procurement-plus/board/suppliers?${params}`)
    groups.value = res.items
    total.value = res.total
    if (res.page !== page.value) page.value = res.page
    // Open the ones still being priced.
    if (!opened.value.size) opened.value = new Set(res.items.filter(g => g.pendingCount > g.pricedCount).map(g => g.key))
  } finally {
    loading.value = false
  }
}
watch(() => [props.search, pageSize.value, api.asUser.value], () => { if (page.value !== 1) page.value = 1; else load() })
watch(page, load)

/** Pending lines that can go on RFQs now: priced and pointing at a catalog supplier. */
const readyLines = (g: PpSupplierGroup) => g.lines.filter(l => l.status !== 'Accepted' && l.line.price != null && l.line.supplierId)

const finishing = ref<string | null>(null)
async function finish(g: PpSupplierGroup) {
  finishing.value = g.key
  try {
    const res = await api.post<any>('/procurement-plus/quotes/done', { ids: readyLines(g).map(l => l.line.id) })
    const s = res.summary
    toast(s.failed.length
      ? `${s.accepted} done · ${s.failed.map((f: any) => `${f.partNumber}: ${f.reason}`).join(' · ')}`
      : `${g.name}: ${s.accepted} price${s.accepted === 1 ? '' : 's'} set · ${s.quotesWritten} RFQ quote${s.quotesWritten === 1 ? '' : 's'} written`,
    s.failed.length ? 'warning' : 'success')
    await load()
    emit('changed')
  } catch (e: any) {
    toast(e?.data?.message || 'Could not finish', 'error')
  } finally {
    finishing.value = null
  }
}

async function removeLine(l: PpQuoteLine) {
  await api.post('/procurement-plus/quotes/remove', { ids: [l.line.id] })
  await load()
  emit('changed')
}

const edit = reactive<{ open: boolean; row: any; accepted: boolean }>({ open: false, row: null, accepted: false })
function openEdit(l: PpQuoteLine) {
  edit.row = l.line
  edit.accepted = l.status === 'Accepted'
  edit.open = true
}
function patchLine(row: any) {
  for (const g of groups.value) {
    const l = g.lines.find(x => x.line.id === row.id)
    if (!l) continue
    const wasPriced = l.line.price != null
    l.line = { ...l.line, ...row }
    if (l.status !== 'Accepted' && wasPriced !== (l.line.price != null)) g.pricedCount += l.line.price != null ? 1 : -1
    break
  }
}

const snack = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') { snackText.value = text; snackColor.value = color; snack.value = true }

onMounted(load)
</script>

<style scoped>
.sv-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center;
  padding: 48px 16px; border-radius: 12px; border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
}
.sv {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px; background: rgb(var(--v-theme-surface)); margin-bottom: 12px; overflow: hidden;
}
.sv__head { display: flex; align-items: center; gap: 12px; padding: 10px 12px; cursor: pointer; }
.sv__name { font-size: 15px; font-weight: 800; }
.grid-scroll { overflow-x: auto; border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); }
.sup-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.sup-table th {
  text-align: left; font-size: 11.5px; font-weight: 600; opacity: 0.65; padding: 8px 10px; white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.08);
}
.sup-table td { padding: 7px 10px; border-bottom: 1px solid rgba(var(--v-border-color), 0.05); vertical-align: top; }
.sup-table tbody tr { cursor: pointer; }
.sup-table tbody tr:hover { background: rgba(var(--v-theme-primary), 0.05); }
.row--done { background: rgba(var(--v-theme-success), 0.05); }
.num { text-align: right; white-space: nowrap; }
.nowrap { white-space: nowrap; }
.min-w-0 { min-width: 0; }
</style>
