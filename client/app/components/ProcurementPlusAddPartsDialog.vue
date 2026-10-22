<template>
  <v-dialog :model-value="modelValue" max-width="1100" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card class="pp-add">
      <v-card-title class="d-flex align-center flex-wrap gap-2 pa-4">
        <v-icon icon="mdi-playlist-plus" color="primary" class="mr-1" />
        Add parts
        <v-spacer />
        <!-- Where the parts go -->
        <span class="text-body-2 text-medium-emphasis mr-1">Add as</span>
        <v-btn-toggle v-model="track" mandatory density="compact" variant="outlined" divided>
          <v-btn v-for="t in PART_TRACKS" :key="t.value" :value="t.value" size="small" :color="t.color" :prepend-icon="t.icon" :title="t.hint">
            {{ t.title }}
          </v-btn>
        </v-btn-toggle>
        <v-btn icon="mdi-close" variant="text" size="small" @click="close" />
      </v-card-title>

      <template v-if="step === 'pick'">
        <v-tabs v-model="tab" color="primary" density="compact" class="px-4">
          <v-tab value="rfq" prepend-icon="mdi-file-document-outline">From RFQs</v-tab>
          <v-tab value="item" prepend-icon="mdi-magnify">Search RFQ items</v-tab>
          <v-tab value="custom" prepend-icon="mdi-form-textbox">Custom part</v-tab>
        </v-tabs>
        <v-divider />

        <v-card-text class="pa-0 pp-add__body">
          <!-- From RFQs: pick an RFQ on the left, its items on the right -->
          <div v-if="tab === 'rfq'" class="pp-add__split">
            <div class="pp-add__rfqs">
              <div class="pa-3">
                <v-text-field
                  v-model="rfqSearch"
                  placeholder="RFQ name, id, customer or P/N"
                  prepend-inner-icon="mdi-magnify"
                  density="compact"
                  variant="outlined"
                  hide-details
                  clearable
                />
                <div class="text-caption text-medium-emphasis mt-1">
                  {{ rfqSearch ? 'Matching RFQs' : 'RFQs still open for quotes (deadline at most a week ago)' }}
                </div>
              </div>
              <v-progress-linear v-if="rfqLoading" indeterminate color="primary" height="2" />
              <v-list density="compact" class="py-0" nav>
                <v-list-item
                  v-for="r in rfqs"
                  :key="r.id"
                  :active="r.id === activeRfqId"
                  color="primary"
                  class="mx-2"
                  @click="openRfq(r.id)"
                >
                  <v-list-item-title class="font-weight-medium">{{ r.name }}</v-list-item-title>
                  <v-list-item-subtitle>
                    {{ r.customerCode || '—' }} · {{ r.itemCount }} item{{ r.itemCount === 1 ? '' : 's' }}
                  </v-list-item-subtitle>
                  <template #append>
                    <v-chip size="x-small" :color="deadlineColor(r.deadline)" variant="tonal">{{ deadlineLabel(r.deadline) }}</v-chip>
                  </template>
                </v-list-item>
                <div v-if="!rfqLoading && !rfqs.length" class="text-caption text-medium-emphasis text-center pa-6">No RFQs found.</div>
              </v-list>
            </div>
            <div class="pp-add__items">
              <div v-if="!activeRfqId" class="pp-empty">
                <v-icon icon="mdi-arrow-left-bold-circle-outline" size="40" class="mb-2" />
                <div>Pick an RFQ to see its parts</div>
              </div>
              <ItemTable v-else :items="rfqItems" :loading="itemsLoading" :selected="selectedIds" @toggle="toggle" @toggle-all="toggleAll" />
            </div>
          </div>

          <!-- Search RFQ items by part number -->
          <div v-else-if="tab === 'item'" class="pa-4">
            <v-text-field
              v-model="itemSearch"
              placeholder="Part number (or part of it)"
              prepend-inner-icon="mdi-magnify"
              density="compact"
              variant="outlined"
              hide-details
              clearable
              autofocus
              class="mb-3"
              style="max-width: 420px"
            />
            <div v-if="!itemSearch" class="pp-empty">
              <v-icon icon="mdi-text-search" size="40" class="mb-2" />
              <div>Search every RFQ you can see by part number</div>
            </div>
            <ItemTable v-else :items="searchItems" :loading="searchLoading" :selected="selectedIds" show-rfq @toggle="toggle" @toggle-all="toggleAll" />
          </div>

          <!-- Custom parts: typed, with description, qty, condition (and target price for Project / WeChat) -->
          <div v-else class="pa-4 custom-parts">
            <div class="d-flex align-center flex-wrap gap-2 mb-3">
              <div class="text-body-2 text-medium-emphasis">
                A part that is not on an RFQ — it does not have to be in the parts catalog either.
                Type them, or paste a whole list from Excel and check the rows below.
              </div>
              <v-spacer />
              <v-btn
                size="small"
                color="success"
                variant="tonal"
                prepend-icon="mdi-content-paste"
                @click="showPaste = !showPaste"
              >
                {{ showPaste ? 'Hide paste' : 'Paste from Excel' }}
              </v-btn>
              <v-btn v-if="customFilled.length > 1" size="small" variant="text" prepend-icon="mdi-close-box-multiple-outline" @click="custom = [blankCustom()]">
                Clear rows
              </v-btn>
            </div>

            <!-- Bulk paste, like the RFQs page bulk import -->
            <div v-if="showPaste" class="mb-4">
              <p class="text-caption text-medium-emphasis mb-2">
                Copy the rows in Excel (with or without the header row) and paste them below. Columns:
                <strong>Part Number &nbsp;|&nbsp; Description &nbsp;|&nbsp; Qty &nbsp;|&nbsp; Condition{{ needsTarget ? ' | Target price' : '' }} &nbsp;|&nbsp; Alternatives</strong>
                (several in one cell: ALT1, ALT2) — with a header row any order works (P/N, Quantity, Cond, Alt… are understood too); one column = just part numbers.
              </p>
              <v-textarea
                v-model="pasteText"
                placeholder="Paste rows from Excel here (Ctrl+V)…"
                variant="outlined"
                density="compact"
                rows="6"
                hide-details
                class="mb-2 paste-box"
              />
              <div class="d-flex align-center flex-wrap gap-2">
                <v-btn size="small" color="primary" prepend-icon="mdi-table-arrow-down" :disabled="!pasteText.trim()" @click="importPaste">
                  Import &amp; Check
                </v-btn>
                <v-btn size="small" variant="text" @click="pasteText = ''">Clear</v-btn>
              </div>
            </div>
            <v-alert v-if="importNote" :type="importNote.type" variant="tonal" density="compact" closable class="mb-3" @click:close="importNote = null">
              {{ importNote.text }}
            </v-alert>
            <div v-for="(c, i) in custom" :key="i" class="custom-row">
              <v-text-field v-model="c.partNumber" label="Part number *" density="compact" variant="outlined" hide-details class="custom-row__pn" />
              <v-text-field v-model="c.description" label="Description" density="compact" variant="outlined" hide-details class="custom-row__desc" />
              <v-text-field v-model.number="c.qty" type="number" label="Qty" density="compact" variant="outlined" hide-details class="custom-row__qty" />
              <v-combobox v-model="c.condition" :items="CONDITIONS" label="Condition" density="compact" variant="outlined" hide-details class="custom-row__cond" />
              <v-text-field
                v-if="needsTarget"
                v-model.number="c.targetPrice"
                type="number"
                label="Target price *"
                prefix="$"
                density="compact"
                variant="outlined"
                hide-details
                class="custom-row__target"
              />
              <v-combobox
                :model-value="c.alternatives"
                label="Alternatives"
                multiple
                chips
                closable-chips
                density="compact"
                variant="outlined"
                hide-details
                class="custom-row__alt"
                @update:model-value="v => (c.alternatives = splitAlternatives((v as string[]).join(','), c.partNumber))"
              />
              <v-btn icon="mdi-close" size="small" variant="text" color="error" :disabled="custom.length === 1 && !c.partNumber" @click="removeCustom(i)" />
            </div>
            <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="custom.push(blankCustom())">Another part</v-btn>
          </div>
        </v-card-text>
      </template>

      <!-- Review: target price (and qty / condition) for each picked RFQ part — Project / WeChat -->
      <v-card-text v-else class="pp-add__body pa-4">
        <div class="text-body-2 text-medium-emphasis mb-3">
          {{ trackMeta(track).title }} parts need a target price. Qty and condition come from the RFQ — change them if needed.
        </div>
        <div v-for="it in pickedList" :key="it.rfqItemId" class="custom-row">
          <div class="custom-row__pn">
            <div class="font-weight-bold">{{ it.partNumber }}</div>
            <div class="text-caption text-medium-emphasis">{{ it.rfqName }}</div>
          </div>
          <div class="custom-row__desc text-caption text-medium-emphasis">{{ it.description }}</div>
          <v-text-field v-model.number="it.qty" type="number" label="Qty" density="compact" variant="outlined" hide-details class="custom-row__qty" />
          <v-combobox v-model="it.condition" :items="CONDITIONS" label="Condition" density="compact" variant="outlined" hide-details class="custom-row__cond" />
          <v-text-field
            v-model.number="it.targetPrice"
            type="number"
            label="Target price *"
            prefix="$"
            density="compact"
            variant="outlined"
            hide-details
            class="custom-row__target"
            :error="!(Number(it.targetPrice) > 0)"
          />
        </div>
      </v-card-text>

      <v-divider />
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3 mb-0">{{ error }}</v-alert>
      <v-card-actions class="pa-4">
        <div class="text-body-2">
          <template v-if="totalPicked">
            <v-icon :icon="trackMeta(track).icon" :color="trackMeta(track).color" size="18" class="mr-1" />
            <strong>{{ totalPicked }}</strong> part{{ totalPicked === 1 ? '' : 's' }} → {{ trackMeta(track).title }}
            <v-btn v-if="step === 'pick'" variant="text" size="small" class="ml-1" @click="clearPicks">Clear</v-btn>
          </template>
          <span v-else class="text-medium-emphasis">Nothing picked yet</span>
        </div>
        <v-spacer />
        <v-btn v-if="step === 'review'" variant="text" prepend-icon="mdi-arrow-left" @click="step = 'pick'">Back</v-btn>
        <v-btn v-else variant="text" @click="close">Cancel</v-btn>
        <v-btn
          v-if="step === 'pick' && needsTarget && pickedList.length"
          color="primary"
          variant="flat"
          append-icon="mdi-arrow-right"
          :disabled="!customValid"
          @click="step = 'review'"
        >
          Next: target prices
        </v-btn>
        <v-btn
          v-else
          color="primary"
          variant="flat"
          prepend-icon="mdi-plus"
          :disabled="!canSave"
          :loading="saving"
          @click="save"
        >
          Add {{ totalPicked || '' }} part{{ totalPicked === 1 ? '' : 's' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { defineComponent, h } from 'vue'
import { VCheckboxBtn, VChip, VProgressLinear } from 'vuetify/components'

interface RfqOption { id: number; name: string; status: string; deadline: string; customerCode?: string; itemCount: number }
interface ItemOption {
  rfqItemId: number; rfqId: number; rfqName: string; deadline: string; partNumberId: number
  partNumber: string; description?: string; qty: number; condition?: string; alreadyAdded: boolean
}
type Picked = ItemOption & { targetPrice: number | null }
interface CustomPart { partNumber: string; description: string; qty: number | null; condition: string | null; targetPrice: number | null; alternatives: string[] }

const props = defineProps<{ modelValue: boolean; initialTrack?: string }>()
const emit = defineEmits<{ 'update:modelValue': [v: boolean]; added: [message: string, track: string] }>()

const api = useProcurementPlusApi()
const tab = ref<'rfq' | 'item' | 'custom'>('rfq')
const step = ref<'pick' | 'review'>('pick')
const track = ref<string>('ILS')
const needsTarget = computed(() => needsTargetPrice(track.value))
const error = ref('')

// ── RFQs ──
const rfqSearch = ref('')
const rfqs = ref<RfqOption[]>([])
const rfqLoading = ref(false)
const activeRfqId = ref<number | null>(null)
const rfqItems = ref<ItemOption[]>([])
const itemsLoading = ref(false)

let rfqTimer: ReturnType<typeof setTimeout> | null = null
watch(rfqSearch, () => {
  if (rfqTimer) clearTimeout(rfqTimer)
  rfqTimer = setTimeout(loadRfqs, 300)
})

async function loadRfqs() {
  rfqLoading.value = true
  try {
    rfqs.value = await api.get<RfqOption[]>(`/procurement-plus/rfqs?search=${encodeURIComponent(rfqSearch.value || '')}`)
  } catch {
    rfqs.value = []
  } finally {
    rfqLoading.value = false
  }
}

async function openRfq(id: number) {
  activeRfqId.value = id
  itemsLoading.value = true
  try {
    rfqItems.value = await api.get<ItemOption[]>(`/procurement-plus/rfqs/${id}/items`)
  } catch {
    rfqItems.value = []
  } finally {
    itemsLoading.value = false
  }
}

// ── RFQ item search ──
const itemSearch = ref('')
const searchItems = ref<ItemOption[]>([])
const searchLoading = ref(false)
let itemTimer: ReturnType<typeof setTimeout> | null = null
watch(itemSearch, (q) => {
  if (itemTimer) clearTimeout(itemTimer)
  if (!q?.trim()) { searchItems.value = []; return }
  itemTimer = setTimeout(async () => {
    searchLoading.value = true
    try {
      searchItems.value = await api.get<ItemOption[]>(`/procurement-plus/rfq-items?search=${encodeURIComponent(q.trim())}`)
    } catch {
      searchItems.value = []
    } finally {
      searchLoading.value = false
    }
  }, 300)
})

// ── Picked RFQ items (kept with their details for the target-price review) ──
const picked = ref(new Map<number, Picked>())
const selectedIds = computed(() => new Set(picked.value.keys()))
const pickedList = computed(() => [...picked.value.values()])

function toggle(item: ItemOption) {
  const next = new Map(picked.value)
  next.has(item.rfqItemId) ? next.delete(item.rfqItemId) : next.set(item.rfqItemId, { ...item, targetPrice: null })
  picked.value = next
}
function toggleAll(items: ItemOption[], on: boolean) {
  const next = new Map(picked.value)
  for (const i of items) on ? (next.has(i.rfqItemId) || next.set(i.rfqItemId, { ...i, targetPrice: null })) : next.delete(i.rfqItemId)
  picked.value = next
}

// ── Custom parts ──
const blankCustom = (): CustomPart => ({ partNumber: '', description: '', qty: null, condition: null, targetPrice: null, alternatives: [] })
const custom = ref<CustomPart[]>([blankCustom()])
const customFilled = computed(() => custom.value.filter(c => c.partNumber.trim()))
const customValid = computed(() => !needsTarget.value || customFilled.value.every(c => Number(c.targetPrice) > 0))
function removeCustom(i: number) {
  custom.value.splice(i, 1)
  if (!custom.value.length) custom.value.push(blankCustom())
}

// Bulk paste (rows copied from Excel) → the rows above, to check and edit before adding (nothing is saved until Add).
const showPaste = ref(false)
const pasteText = ref('')
const importNote = ref<{ type: 'success' | 'warning' | 'error'; text: string } | null>(null)
function importPaste() {
  const { parts, skipped } = readPartsFromText(pasteText.value, needsTarget.value)
  if (!parts.length) {
    importNote.value = { type: 'error', text: 'No part numbers found in what was pasted — copy the rows (Part Number first) from Excel and paste again.' }
    return
  }
  // Rows already typed stay; the same part number is not added twice.
  const have = new Set(customFilled.value.map(c => c.partNumber.replace(/\s+/g, '').toUpperCase()))
  const fresh = parts.filter(p => !have.has(p.partNumber.replace(/\s+/g, '').toUpperCase()))
  custom.value = [...customFilled.value, ...fresh.map(p => ({ ...p, alternatives: [...p.alternatives] }))]
  if (!custom.value.length) custom.value = [blankCustom()]
  const notes = [`${fresh.length} part${fresh.length === 1 ? '' : 's'} added below`]
  const withAlts = fresh.filter(p => p.alternatives.length).length
  if (withAlts) notes.push(`${withAlts} with alternatives`)
  if (parts.length > fresh.length) notes.push(`${parts.length - fresh.length} already in the list`)
  if (skipped) notes.push(`${skipped} row${skipped === 1 ? '' : 's'} skipped (no part number or a repeat)`)
  const missingTarget = needsTarget.value ? fresh.filter(p => !(Number(p.targetPrice) > 0)).length : 0
  if (missingTarget) notes.push(`${missingTarget} still need a target price`)
  importNote.value = { type: missingTarget ? 'warning' : 'success', text: notes.join(' · ') + '. Check them, then Add.' }
  pasteText.value = ''
  showPaste.value = false
}

const totalPicked = computed(() => picked.value.size + customFilled.value.length)
const canSave = computed(() => totalPicked.value > 0 && customValid.value
  && (!needsTarget.value || pickedList.value.every(p => Number(p.targetPrice) > 0)))

function clearPicks() {
  picked.value = new Map()
  custom.value = [blankCustom()]
}

const saving = ref(false)
async function save() {
  saving.value = true
  error.value = ''
  try {
    const num = (v: any) => (v === '' || v == null ? null : Number(v))
    const res = await api.post<{ added: number; restored: number; alreadyListed: number }>('/procurement-plus/parts', {
      track: track.value,
      items: [
        ...pickedList.value.map(p => ({
          rfqItemId: p.rfqItemId,
          qty: num(p.qty),
          condition: p.condition || null,
          targetPrice: needsTarget.value ? num(p.targetPrice) : null,
        })),
        ...customFilled.value.map(c => ({
          partNumber: c.partNumber.trim(),
          description: c.description || null,
          qty: num(c.qty),
          condition: c.condition || null,
          targetPrice: needsTarget.value ? num(c.targetPrice) : null,
          alternatives: splitAlternatives(c.alternatives.join(','), c.partNumber),
        })),
      ],
    })
    const bits = [`${res.added} part${res.added === 1 ? '' : 's'} added to ${trackMeta(track.value).title}`]
    if (res.restored) bits.push(`${res.restored} shown again`)
    if (res.alreadyListed) bits.push(`${res.alreadyListed} already on your list (updated)`)
    emit('added', bits.join(' · '), track.value)
    clearPicks()
    close()
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not add the parts'
  } finally {
    saving.value = false
  }
}

function close() { emit('update:modelValue', false) }

function deadlineColor(d: string) {
  const days = (new Date(d).getTime() - Date.now()) / 86_400_000
  return days < -1 ? 'error' : days < 2 ? 'warning' : 'success'
}

watch(() => props.modelValue, (open) => {
  if (!open) return
  step.value = 'pick'
  error.value = ''
  track.value = props.initialTrack || 'ILS'
  if (track.value !== 'ILS' && !picked.value.size) tab.value = 'custom'
  if (!rfqs.value.length) loadRfqs()
}, { immediate: true })

/** Checkbox table of RFQ items, shared by both pick tabs. */
const ItemTable = defineComponent({
  props: {
    items: { type: Array as () => ItemOption[], required: true },
    loading: Boolean,
    selected: { type: Object as () => Set<number>, required: true },
    showRfq: Boolean,
  },
  emits: ['toggle', 'toggle-all'],
  setup(p, { emit: e }) {
    return () => {
      const all = p.items.length > 0 && p.items.every(i => p.selected.has(i.rfqItemId))
      const some = p.items.some(i => p.selected.has(i.rfqItemId))
      return h('div', { class: 'pp-items' }, [
        p.loading ? h(VProgressLinear, { indeterminate: true, color: 'primary', height: 2 }) : null,
        h('table', { class: 'pp-table' }, [
          h('thead', h('tr', [
            h('th', { style: 'width:42px' }, h(VCheckboxBtn, {
              modelValue: all, indeterminate: some && !all, density: 'compact',
              'onUpdate:modelValue': () => e('toggle-all', p.items, !all),
            })),
            h('th', 'Part number'),
            h('th', 'Description'),
            p.showRfq ? h('th', 'RFQ') : null,
            h('th', { class: 'text-right' }, 'Qty'),
            h('th', 'Cond'),
            h('th', ''),
          ])),
          h('tbody', p.items.length
            ? p.items.map(i => h('tr', {
                key: i.rfqItemId,
                class: { 'pp-row--on': p.selected.has(i.rfqItemId) },
                onClick: () => e('toggle', i),
              }, [
                h('td', h(VCheckboxBtn, { modelValue: p.selected.has(i.rfqItemId), density: 'compact' })),
                h('td', { class: 'font-weight-medium' }, i.partNumber),
                h('td', { class: 'text-medium-emphasis' }, i.description || ''),
                p.showRfq ? h('td', `${i.rfqName} · ${deadlineLabel(i.deadline)}`) : null,
                h('td', { class: 'text-right' }, String(i.qty ?? '')),
                h('td', i.condition || ''),
                h('td', i.alreadyAdded ? h(VChip, { size: 'x-small', color: 'primary', variant: 'tonal' }, () => 'On your list') : null),
              ]))
            : [h('tr', h('td', { colspan: 7, class: 'text-center text-medium-emphasis pa-6' }, p.loading ? 'Loading…' : 'No parts.'))]),
        ]),
      ])
    }
  },
})
</script>

<style scoped>
.pp-add__body { height: 60vh; }
.pp-add__split { display: grid; grid-template-columns: 340px 1fr; height: 100%; }
.pp-add__rfqs { border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); overflow-y: auto; }
.pp-add__items { overflow-y: auto; padding: 12px 16px; }
.pp-empty {
  height: 100%; min-height: 220px; display: flex; flex-direction: column; align-items: center; justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.5); font-size: 14px;
}
.custom-parts { height: 100%; overflow-y: auto; }
.paste-box :deep(textarea) { font-family: 'JetBrains Mono', 'Cascadia Code', monospace; font-size: 12px; }
.custom-row { display: flex; align-items: center; gap: 10px; margin-bottom: 10px; }
.custom-row__pn { flex: 0 0 200px; }
.custom-row__desc { flex: 1 1 200px; min-width: 0; }
.custom-row__qty { flex: 0 0 90px; }
.custom-row__cond { flex: 0 0 120px; }
.custom-row__target { flex: 0 0 150px; }
.custom-row__alt { flex: 1 1 200px; min-width: 0; }
@media (max-width: 800px) {
  .pp-add__split { grid-template-columns: 1fr; }
  .pp-add__rfqs { max-height: 40%; border-right: 0; }
  .custom-row { flex-wrap: wrap; }
}
:deep(.pp-table) { width: 100%; border-collapse: collapse; font-size: 13px; }
:deep(.pp-table th) {
  text-align: left; font-weight: 600; font-size: 12px; opacity: 0.7; padding: 6px 8px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); position: sticky; top: 0;
  background: rgb(var(--v-theme-surface)); z-index: 1;
}
:deep(.pp-table td) { padding: 4px 8px; border-bottom: 1px solid rgba(var(--v-border-color), 0.06); }
:deep(.pp-table tbody tr) { cursor: pointer; }
:deep(.pp-table tbody tr:hover) { background: rgba(var(--v-theme-primary), 0.04); }
:deep(.pp-row--on) { background: rgba(var(--v-theme-primary), 0.08); }
</style>
