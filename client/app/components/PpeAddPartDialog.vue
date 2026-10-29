<template>
  <!-- New Procurement Plus: add a part by hand — picked from the database or typed — and work on it like an assigned one. -->
  <v-dialog :model-value="modelValue" :max-width="mode === 'bulk' ? 820 : 640" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-plus-box-outline" color="primary" class="mr-2" />
        Add part
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-divider />
      <v-card-text>
        <v-btn-toggle v-model="mode" mandatory color="primary" variant="outlined" density="compact" class="mb-4">
          <v-btn value="catalog" prepend-icon="mdi-database-search-outline">From database</v-btn>
          <v-btn value="new" prepend-icon="mdi-pencil-plus-outline">New part</v-btn>
          <v-btn value="bulk" prepend-icon="mdi-table-arrow-down">Bulk import</v-btn>
        </v-btn-toggle>

        <template v-if="mode === 'catalog'">
          <v-autocomplete
            v-model="picked"
            v-model:search="catalogSearch"
            :items="catalogParts"
            :loading="searching"
            item-title="partNumber"
            return-object
            no-filter
            label="Search part number or alternative"
            placeholder="Type at least 2 characters"
            prepend-inner-icon="mdi-magnify"
            density="compact"
            variant="outlined"
            clearable
            autofocus
            :no-data-text="catalogSearch?.trim().length >= 2 && !searching ? 'Not in the database — use New part' : 'Type to search'"
          >
            <template #item="{ props: itemProps, item }">
              <v-list-item v-bind="itemProps" :subtitle="subtitleOf(item.raw)">
                <template #append>
                  <v-chip v-if="item.raw.alreadyAdded" size="x-small" color="success" variant="tonal">on my list</v-chip>
                </template>
              </v-list-item>
            </template>
          </v-autocomplete>
          <div v-if="picked" class="text-caption text-medium-emphasis mt-n2 mb-3">
            {{ picked.description || 'No description' }}
            <span v-if="picked.alternatives.length"> · Alternatives: {{ picked.alternatives.join(', ') }}</span>
          </div>
        </template>

        <template v-else-if="mode === 'bulk'">
          <v-textarea
            v-model="pasteText"
            label="Paste from Excel"
            placeholder="Part Number    Qty    Condition"
            hint="Columns Part Number, Qty, Condition — with or without a header row (a header can name them in any order)"
            persistent-hint
            rows="6"
            density="compact"
            variant="outlined"
            class="mono"
            autofocus
          />
          <div class="d-flex align-center gap-2 mt-3 mb-3">
            <v-btn size="small" color="primary" variant="tonal" prepend-icon="mdi-check-all" :disabled="!pasteText.trim()" @click="readPaste">
              Import &amp; Check
            </v-btn>
            <span v-if="pasteNote" class="text-caption text-medium-emphasis">{{ pasteNote }}</span>
          </div>
          <v-table v-if="bulkRows.length" density="compact" class="bulk-table mb-3">
            <thead>
              <tr>
                <th>Part Number</th>
                <th style="width: 120px">Qty</th>
                <th style="width: 150px">Condition</th>
                <th style="width: 40px" />
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, i) in bulkRows" :key="i">
                <td><v-text-field v-model="row.partNumber" density="compact" variant="plain" hide-details /></td>
                <td><v-text-field v-model.number="row.qty" type="number" min="0" density="compact" variant="plain" hide-details /></td>
                <td><v-combobox v-model="row.condition" :items="QUOTE_CONDITIONS" density="compact" variant="plain" hide-details clearable /></td>
                <td><v-btn icon="mdi-close" size="x-small" variant="text" @click="bulkRows.splice(i, 1)" /></td>
              </tr>
            </tbody>
          </v-table>
        </template>

        <template v-else>
          <v-text-field
            v-model="partNumber"
            label="Part number *"
            density="compact"
            variant="outlined"
            autofocus
          />
          <v-text-field v-model="description" label="Description" density="compact" variant="outlined" />
          <v-combobox
            v-model="alternatives"
            label="Alternatives"
            hint="Press Enter after each one"
            persistent-hint
            multiple
            chips
            closable-chips
            density="compact"
            variant="outlined"
            class="mb-3"
          />
        </template>

        <div v-if="mode !== 'bulk'" class="d-flex gap-3">
          <v-text-field v-model.number="qty" type="number" min="0" label="Qty" density="compact" variant="outlined" style="max-width: 140px" />
          <v-combobox v-model="condition" :items="QUOTE_CONDITIONS" label="Condition" density="compact" variant="outlined" clearable style="max-width: 180px" />
        </div>

        <!-- Which of my folders it goes into; none = Public (a part in a folder is not listed in Public) -->
        <v-select
          v-model="pickedFolders"
          :items="folders"
          item-title="name"
          item-value="id"
          multiple
          chips
          closable-chips
          label="Folders"
          placeholder="Public only"
          persistent-placeholder
          :hint="mode === 'bulk' ? 'Every imported part goes into these folders (not shown in Public)' : 'Leave empty to keep it in Public'"
          persistent-hint
          prepend-inner-icon="mdi-folder-outline"
          density="compact"
          variant="outlined"
          :no-data-text="'No folders yet — make one with New folder on the Parts tab'"
          class="mb-3"
        />

        <v-alert v-if="error" type="error" variant="tonal" density="compact">{{ error }}</v-alert>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-3">
        <span class="text-caption text-medium-emphasis">
          It covers every RFQ asking for it (deadline in the last {{ PPE_DEADLINE_DAYS }} days) — add suppliers and prices under it.
        </span>
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="save">
          {{ mode === 'bulk' ? `Add ${bulkValid.length} part${bulkValid.length === 1 ? '' : 's'}` : 'Add' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { PPE_DEADLINE_DAYS } from '~/utils/ppe'
import type { PpeAddPartResponse, PpeBulkAddPartsResponse, PpeCatalogPart, PpeFolder } from '~/utils/ppe'
import { readPartsFromText } from '~/utils/procurementPlus'
import { QUOTE_CONDITIONS } from '~/utils/supplierQuotes'

/** folders = my folders; folderId = the one open on the Parts tab (picked by default; null = Public). */
const props = withDefaults(defineProps<{ modelValue: boolean; folders?: PpeFolder[]; folderId?: number | null }>(), { folders: () => [], folderId: null })
/** The folders it was put in travel along, so the list can show it where it went. */
const emit = defineEmits<{
  'update:modelValue': [open: boolean]
  added: [res: PpeAddPartResponse, folderIds: number[]]
  bulkAdded: [res: PpeBulkAddPartsResponse, folderIds: number[]]
}>()

const api = usePpeApi()
const mode = ref<'catalog' | 'new' | 'bulk'>('catalog')
const picked = ref<PpeCatalogPart | null>(null)
const partNumber = ref('')
const description = ref('')
const alternatives = ref<string[]>([])
const qty = ref<number | null>(null)
const condition = ref<string | null>(null)
const pickedFolders = ref<number[]>([])
const error = ref('')
const saving = ref(false)

// Fresh form on every open
watch(() => props.modelValue, (open) => {
  if (!open) return
  mode.value = 'catalog'
  pickedFolders.value = props.folderId ? [props.folderId] : []
  picked.value = null
  catalogSearch.value = ''
  catalogParts.value = []
  partNumber.value = ''
  description.value = ''
  alternatives.value = []
  qty.value = null
  condition.value = null
  pasteText.value = ''
  pasteNote.value = ''
  bulkRows.value = []
  error.value = ''
})

// ── Bulk import: rows pasted from Excel, checked and editable before Add ──
type BulkRow = { partNumber: string; qty: number | null; condition: string | null }
const pasteText = ref('')
const pasteNote = ref('')
const bulkRows = ref<BulkRow[]>([])
const bulkValid = computed(() => bulkRows.value.filter(r => r.partNumber?.trim()))
function readPaste() {
  const { parts, skipped } = readPartsFromText(pasteText.value, false, ['partNumber', 'qty', 'condition'])
  const have = new Set(bulkRows.value.map(r => r.partNumber.replace(/\s+/g, '').toUpperCase()))
  const fresh = parts.filter(p => !have.has(p.partNumber.replace(/\s+/g, '').toUpperCase()))
  bulkRows.value.push(...fresh.map(p => ({ partNumber: p.partNumber, qty: p.qty, condition: p.condition })))
  const repeated = skipped + parts.length - fresh.length
  pasteNote.value = `${fresh.length} part${fresh.length === 1 ? '' : 's'} read${repeated ? ` · ${repeated} row${repeated === 1 ? '' : 's'} skipped (empty, header or repeated)` : ''}`
  pasteText.value = ''
}

// ── Database search ──
const catalogSearch = ref('')
const catalogParts = ref<PpeCatalogPart[]>([])
const searching = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | undefined
let searchId = 0
watch(catalogSearch, (q) => {
  clearTimeout(searchTimer)
  const term = q?.trim() || ''
  // Picking an item sets the search text to its P/N — keep the list as it is.
  if (term.length < 2 || term === picked.value?.partNumber) return
  searchTimer = setTimeout(async () => {
    const id = ++searchId
    searching.value = true
    try {
      const res = await api.get<PpeCatalogPart[]>(`/catalog-parts?q=${encodeURIComponent(term)}`)
      if (id === searchId) catalogParts.value = res
    } catch {
      if (id === searchId) catalogParts.value = []
    } finally {
      if (id === searchId) searching.value = false
    }
  }, 300)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

function subtitleOf(p: PpeCatalogPart) {
  const alts = p.alternatives.length ? `Alt: ${p.alternatives.slice(0, 3).join(', ')}${p.alternatives.length > 3 ? '…' : ''}` : ''
  return [p.description, alts].filter(Boolean).join(' · ')
}

const canSave = computed(() => mode.value === 'catalog' ? !!picked.value
  : mode.value === 'bulk' ? bulkValid.value.length > 0
    : !!partNumber.value.trim())

async function save() {
  if (!canSave.value) return
  saving.value = true
  error.value = ''
  try {
    if (mode.value === 'bulk') {
      const items = bulkValid.value.map(r => ({
        partNumber: r.partNumber.trim(),
        qty: r.qty || null,
        condition: r.condition?.trim() || null,
      }))
      const res = await api.post<PpeBulkAddPartsResponse>('/parts/bulk', { items, folderIds: pickedFolders.value })
      emit('bulkAdded', res, pickedFolders.value)
      emit('update:modelValue', false)
      return
    }
    const common = { qty: qty.value || null, condition: condition.value?.trim() || null, folderIds: pickedFolders.value }
    const body = mode.value === 'catalog'
      ? { ...common, partNumberId: picked.value!.id }
      : { ...common, partNumber: partNumber.value.trim(), description: description.value.trim() || null, alternatives: cleanAltList(alternatives.value) }
    const res = await api.post<PpeAddPartResponse>('/parts', body)
    emit('added', res, pickedFolders.value)
    emit('update:modelValue', false)
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not add the part'
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.mono :deep(textarea) {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 12px;
}
.bulk-table {
  max-height: 320px;
  overflow-y: auto;
  border: 1px solid var(--card-border);
  border-radius: 6px;
}
</style>
