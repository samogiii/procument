<template>
  <!--
    New Procurement Plus — Parts tab: the parts assigned to me (PPE), one row each across all their RFQs; click to expand.
    Folders (each user's own) are chips above the list: Public = the parts in no folder; a part can be in several folders.
  -->
  <v-card class="glass-card">
    <v-card-text class="pb-0">
      <!-- Folders -->
      <div class="d-flex flex-wrap align-center gap-2 mb-3">
        <v-chip
          :variant="folderId == null ? 'flat' : 'outlined'"
          :color="folderId == null ? 'primary' : undefined"
          prepend-icon="mdi-folder-open-outline"
          title="The parts on your list that are in no folder"
          @click="folderId = null"
        >
          Public <span class="folder-count">{{ publicCount }}</span>
        </v-chip>
        <v-chip
          v-for="f in folders"
          :key="f.id"
          :variant="folderId === f.id ? 'flat' : 'outlined'"
          :color="folderId === f.id ? 'deep-purple' : undefined"
          :prepend-icon="folderId === f.id ? 'mdi-folder-open' : 'mdi-folder-outline'"
          @click="folderId = f.id"
        >
          {{ f.name }} <span class="folder-count">{{ f.partCount }}</span>
        </v-chip>
        <v-chip variant="text" color="primary" prepend-icon="mdi-folder-plus-outline" @click="openFolderDialog(null)">New folder</v-chip>
        <template v-if="currentFolder">
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" title="Rename this folder" @click="openFolderDialog(currentFolder)" />
          <v-btn
            icon="mdi-delete-outline"
            size="x-small"
            variant="text"
            color="error"
            title="Delete this folder — its parts stay in their other folders, or go back to Public"
            @click="confirmDelete = currentFolder"
          />
        </template>
      </div>

      <div class="d-flex flex-wrap align-center gap-3 mb-3">
        <v-text-field
          v-model="search"
          label="Search part, alternative or description"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          style="max-width: 340px; min-width: 220px;"
        />
        <v-switch
          v-model="showOld"
          color="primary"
          density="compact"
          hide-details
          inset
          :label="`Show older${oldCount ? ` (${oldCount})` : ''}`"
          :title="`Parts with no RFQ deadline in the last ${PPE_DEADLINE_DAYS} days, assigned more than ${PPE_DEADLINE_DAYS} days ago`"
        />
        <v-btn
          size="small"
          variant="flat"
          color="primary"
          prepend-icon="mdi-plus"
          title="Add a part from the database or a new one, and work on it here"
          @click="showAdd = true"
        >
          Add part
        </v-btn>
        <v-btn
          size="small"
          variant="tonal"
          color="primary"
          prepend-icon="mdi-content-copy"
          :loading="copying"
          title="Copy every part number on this list (folder, search and Show older apply), one per line"
          @click="copyAll"
        >
          Copy all P/N
        </v-btn>
        <v-spacer />
        <span class="legend"><span class="legend__dot legend__dot--draft" /> supplier without a price</span>
        <span class="legend"><span class="legend__dot legend__dot--email" /> accepted Email Quote</span>
        <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
      </div>

      <!-- Ticked parts → folders -->
      <div v-if="selected.length" class="bulk-bar d-flex flex-wrap align-center gap-2 mb-3">
        <span class="text-body-2 font-weight-medium">{{ selected.length }} part{{ selected.length === 1 ? '' : 's' }} ticked</span>
        <v-menu location="bottom start">
          <template #activator="{ props: menu }">
            <v-btn v-bind="menu" size="small" variant="flat" color="deep-purple" prepend-icon="mdi-folder-move-outline" :loading="moving">
              Add to folder
            </v-btn>
          </template>
          <v-list density="compact" min-width="220">
            <v-list-item v-for="f in folders" :key="f.id" prepend-icon="mdi-folder-outline" :title="f.name" @click="setFolders(selected, [f.id], [])" />
            <v-divider v-if="folders.length" />
            <v-list-item prepend-icon="mdi-folder-plus-outline" title="New folder…" @click="openFolderDialog(null, [...selected])" />
          </v-list>
        </v-menu>
        <v-btn
          v-if="currentFolder"
          size="small"
          variant="tonal"
          prepend-icon="mdi-folder-remove-outline"
          :loading="moving"
          :title="`Take them out of ${currentFolder.name} (in no other folder = back to Public)`"
          @click="setFolders(selected, [], [currentFolder.id])"
        >
          Remove from {{ currentFolder.name }}
        </v-btn>
        <v-btn size="small" variant="text" @click="selected = []">Clear</v-btn>
      </div>
    </v-card-text>

    <v-data-table-server
      v-model="selected"
      v-model:expanded="expanded"
      v-model:page="page"
      v-model:items-per-page="pageSize"
      :headers="headers"
      :items="items"
      :items-length="total"
      :loading="loading"
      item-value="id"
      show-select
      show-expand
      density="comfortable"
      hover
      :items-per-page-options="[25, 50, 100, 200]"
      :row-props="rowProps"
      :no-data-text="noDataText"
      @click:row="(_: any, { item }: any) => toggle(item.id)"
    >
      <template #item.partNumber="{ item }">
        <div class="py-1">
          <span class="font-weight-bold pn-copy" title="Click to copy" @click.stop="copy(item.partNumber)">{{ item.partNumber }}</span>
          <v-chip v-if="item.draftCount" size="x-small" color="warning" variant="tonal" class="ml-2" title="Suppliers added without a price yet">
            {{ item.draftCount }} to price
          </v-chip>
          <v-chip v-if="item.pricedCount" size="x-small" color="success" variant="tonal" class="ml-1" title="Suppliers with a price for this part">
            {{ item.pricedCount }} priced
          </v-chip>
          <!-- The folders it is in (the one open now is left out) — click to open that folder -->
          <v-chip
            v-for="f in otherFolders(item)"
            :key="`f${f.id}`"
            size="x-small"
            variant="outlined"
            color="deep-purple"
            prepend-icon="mdi-folder-outline"
            class="ml-1"
            :title="`In folder ${f.name} — click to open it`"
            @click.stop="folderId = f.id"
          >
            {{ f.name }}
          </v-chip>
          <div v-if="item.alternatives.length" class="d-flex flex-wrap gap-1 mt-1">
            <v-chip
              v-for="a in item.alternatives.slice(0, 4)"
              :key="a"
              size="x-small"
              variant="tonal"
              color="amber-darken-2"
              title="Click to copy"
              @click.stop="copy(a)"
            >
              {{ a }}
            </v-chip>
            <!-- The rest of the alternatives: a menu, each one copies on click -->
            <v-menu v-if="item.alternatives.length > 4" location="bottom">
              <template #activator="{ props: menu }">
                <v-chip v-bind="menu" size="x-small" variant="text" @click.stop>+{{ item.alternatives.length - 4 }}</v-chip>
              </template>
              <v-list density="compact">
                <v-list-item v-for="a in item.alternatives.slice(4)" :key="a" :title="a" prepend-icon="mdi-content-copy" @click="copy(a)" />
              </v-list>
            </v-menu>
          </div>
        </div>
      </template>
      <template #item.description="{ item }">
        <span class="text-caption">{{ item.description || '—' }}</span>
      </template>
      <template #item.qty="{ item }">{{ item.qty ?? '—' }}</template>
      <template #item.condition="{ item }">
        <div class="d-flex flex-wrap gap-1">
          <v-chip v-for="c in item.conditions" :key="c" size="x-small" variant="tonal" :color="c === (item.condition || '').toUpperCase() ? 'primary' : undefined">{{ c }}</v-chip>
          <span v-if="!item.conditions.length" class="text-medium-emphasis">—</span>
        </div>
      </template>
      <template #item.avgPrice="{ item }">
        <span
          v-if="item.avgPrice != null"
          class="price-text"
          :title="item.priceStats.map(s => `${s.condition || '—'}: avg ${fmtMoney(s.average)} · min ${fmtMoney(s.min)} · max ${fmtMoney(s.max)} (${s.count})`).join('\n')"
        >
          {{ fmtMoney(item.avgPrice) }}
          <span class="text-caption text-medium-emphasis">{{ item.avgPriceCondition || 'all' }}</span>
        </span>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.rfqCount="{ item }">
        <v-chip size="small" variant="tonal" :color="item.rfqCount ? 'primary' : 'grey'">{{ item.rfqCount }}</v-chip>
      </template>
      <template #item.nearestDeadline="{ item }">
        <v-chip v-if="item.nearestDeadline" size="small" variant="tonal" :color="deadlineColor(item.nearestDeadline)">
          {{ fmtDate(item.nearestDeadline) }} · {{ deadlineLabel(item.nearestDeadline) }}
        </v-chip>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.quoteCount="{ item }">{{ item.quoteCount }}</template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end">
          <!-- Folders this part is in: tick / untick -->
          <v-menu :close-on-content-click="false" location="bottom end">
            <template #activator="{ props: menu }">
              <v-btn
                v-bind="menu"
                :icon="item.folderIds.length ? 'mdi-folder' : 'mdi-folder-plus-outline'"
                size="x-small"
                variant="text"
                :color="item.folderIds.length ? 'deep-purple' : undefined"
                title="Folders"
                @click.stop
              />
            </template>
            <v-list density="compact" min-width="230">
              <v-list-subheader>Put {{ item.partNumber }} in</v-list-subheader>
              <v-list-item v-for="f in folders" :key="f.id" :disabled="moving" @click="toggleFolder(item, f.id)">
                <template #prepend>
                  <v-checkbox-btn :model-value="item.folderIds.includes(f.id)" density="compact" color="deep-purple" style="pointer-events: none;" />
                </template>
                <v-list-item-title>{{ f.name }}</v-list-item-title>
              </v-list-item>
              <v-list-item v-if="!folders.length" class="text-medium-emphasis" title="No folders yet" />
              <v-divider />
              <v-list-item prepend-icon="mdi-folder-plus-outline" title="New folder…" @click="openFolderDialog(null, [item.id])" />
            </v-list>
          </v-menu>
          <v-btn
            icon="mdi-eye-off-outline"
            size="x-small"
            variant="text"
            title="Remove from my list (assigning it again brings it back)"
            @click.stop="hide(item)"
          />
        </div>
      </template>
      <template #expanded-row="{ item, columns }">
        <tr>
          <td :colspan="columns.length" class="pa-0">
            <PpePartPanel :part="item" @changed="row => patch(row)" @removed="onRemoved(item)" />
          </td>
        </tr>
      </template>
    </v-data-table-server>

    <PpeAddPartDialog v-model="showAdd" :folders="folders" :folder-id="folderId" @added="onAdded" @bulk-added="onBulkAdded" />

    <!-- New folder / rename -->
    <v-dialog v-model="folderDialog.open" max-width="420">
      <v-card>
        <v-card-title class="pa-4">{{ folderDialog.id ? 'Rename folder' : 'New folder' }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="folderDialog.name"
            label="Folder name"
            density="compact"
            variant="outlined"
            autofocus
            counter="100"
            maxlength="100"
            :error-messages="folderDialog.error"
            @keydown.enter.prevent="saveFolder"
          />
          <p v-if="folderDialog.assign.length" class="text-caption text-medium-emphasis mb-0">
            {{ folderDialog.assign.length }} part{{ folderDialog.assign.length === 1 ? '' : 's' }} will go into it.
          </p>
        </v-card-text>
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="folderDialog.open = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="folderDialog.saving" :disabled="!folderDialog.name.trim()" @click="saveFolder">
            {{ folderDialog.id ? 'Rename' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="!!confirmDelete" max-width="440" @update:model-value="(v: boolean) => { if (!v) confirmDelete = null }">
      <v-card>
        <v-card-title class="pa-4">Delete folder {{ confirmDelete?.name }}?</v-card-title>
        <v-card-text>Only the folder goes — its parts stay in their other folders, or go back to Public.</v-card-text>
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="confirmDelete = null">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="deleteFolder">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="4000">{{ snackText }}</v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
import { PPE_DEADLINE_DAYS } from '~/utils/ppe'
import type { PpeAddPartResponse, PpeBulkAddPartsResponse, PpeFolder, PpePartPage, PpePartRow } from '~/utils/ppe'

const api = usePpeApi()
const route = useRoute()
const router = useRouter()
const items = ref<PpePartRow[]>([])
const total = ref(0)
const oldCount = ref(0)
const loading = ref(false)
const page = ref(1)
const pageSize = ref(50)
const search = ref('')
const showOld = ref(false)
const expanded = ref<number[]>([])
const selected = ref<number[]>([])

// ── Folders: null = Public (parts in no folder); the open one is kept in the URL (?folder=id) ──
const folders = ref<PpeFolder[]>([])
const publicCount = ref(0)
const folderId = ref<number | null>(Number(route.query.folder) > 0 ? Number(route.query.folder) : null)
const currentFolder = computed(() => folders.value.find(f => f.id === folderId.value) ?? null)
const folderById = computed(() => new Map(folders.value.map(f => [f.id, f])))
/** The folders a part is in, except the one open now. */
const otherFolders = (item: PpePartRow) =>
  item.folderIds.filter(id => id !== folderId.value).map(id => folderById.value.get(id)).filter((f): f is PpeFolder => !!f)
const noDataText = computed(() => currentFolder.value
  ? `No parts in ${currentFolder.value.name} — put them in with the folder button on a part, or Add part.`
  : 'No parts yet — a reviewer assigns them from Email RFQs (Accepted tab → Assign PPE), or use Add part.')

const headers = [
  { title: 'Part Number', key: 'partNumber', sortable: false },
  { title: 'Description', key: 'description', sortable: false },
  { title: 'QTY', key: 'qty', sortable: false, width: 70 },
  { title: 'Condition', key: 'condition', sortable: false },
  { title: 'Avg Price', key: 'avgPrice', sortable: false },
  { title: 'RFQs', key: 'rfqCount', sortable: false, width: 70 },
  { title: 'Nearest deadline', key: 'nearestDeadline', sortable: false },
  { title: 'Quotes', key: 'quoteCount', sortable: false, width: 70 },
  { title: '', key: 'actions', sortable: false, width: 80 },
]

let loadId = 0
async function load() {
  const id = ++loadId
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize.value), showOld: String(showOld.value) })
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (folderId.value) params.set('folder', String(folderId.value))
    const res = await api.get<PpePartPage>(`/parts?${params}`)
    if (id !== loadId) return
    folders.value = res.folders
    publicCount.value = res.publicCount
    // The open folder is gone (deleted, or another user's page): back to Public — the watcher reloads.
    if (folderId.value && !res.folders.some(f => f.id === folderId.value)) {
      folderId.value = null
      return
    }
    items.value = res.items
    total.value = res.total
    oldCount.value = res.oldCount
    if (res.page !== page.value) page.value = res.page
    const ids = new Set(res.items.map(i => i.id))
    expanded.value = expanded.value.filter(x => ids.has(x))
    if (openAfterLoad && ids.has(openAfterLoad)) {
      if (!expanded.value.includes(openAfterLoad)) expanded.value = [...expanded.value, openAfterLoad]
      openAfterLoad = null
    }
  } catch {
    if (id === loadId) items.value = []
  } finally {
    if (id === loadId) loading.value = false
  }
}
defineExpose({ load })
onMounted(load)
watch([page, pageSize], load)
const reloadFirstPage = () => (page.value === 1 ? load() : (page.value = 1))
watch(showOld, reloadFirstPage)
watch(folderId, (id) => {
  selected.value = []
  const query = { ...route.query }
  if (id) query.folder = String(id)
  else delete query.folder
  router.replace({ query })
  reloadFirstPage()
})
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(reloadFirstPage, 400)
})
onBeforeUnmount(() => clearTimeout(searchTimer))
// The page's user switcher (SuperAdmin) shows another user's parts — and their own folders.
watch(api.asUser, () => {
  expanded.value = []
  selected.value = []
  if (folderId.value) folderId.value = null
  else reloadFirstPage()
})

function toggle(id: number) {
  expanded.value = expanded.value.includes(id) ? expanded.value.filter(x => x !== id) : [...expanded.value, id]
}

/** The expanded panel reloaded the part: counts / average change in place (no table reload). */
function patch(row: PpePartRow) {
  const item = items.value.find(i => i.id === row.id)
  if (item) Object.assign(item, row)
}

function rowProps({ item }: { item: PpePartRow }) {
  return { class: [item.draftCount ? 'ppe-row--draft' : '', expanded.value.includes(item.id) ? 'ppe-row--open' : ''] }
}

const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

// ── Parts into / out of folders ──
const moving = ref(false)
async function setFolders(ids: number[], add: number[], remove: number[]) {
  if (!ids.length) return
  moving.value = true
  try {
    await api.post('/parts/folders', { ids, add, remove })
    const name = (id?: number) => (id != null && folderById.value.get(id)?.name) || 'the folder'
    const n = `${ids.length} part${ids.length === 1 ? '' : 's'}`
    toast(add.length ? `${n} put in ${name(add[0])}` : `${n} taken out of ${name(remove[0])}`)
    selected.value = []
    await load()
  } catch (e: any) {
    toast(e?.data?.message || 'Could not move them', 'error')
  } finally {
    moving.value = false
  }
}

/** Row folder menu: tick / untick one folder — the row and the counts change in place. */
async function toggleFolder(item: PpePartRow, id: number) {
  const inIt = item.folderIds.includes(id)
  moving.value = true
  try {
    await api.post('/parts/folders', { ids: [item.id], add: inIt ? [] : [id], remove: inIt ? [id] : [] })
    item.folderIds = inIt ? item.folderIds.filter(f => f !== id) : [...item.folderIds, id]
    const folder = folderById.value.get(id)
    if (folder) folder.partCount += inIt ? -1 : 1
    // Taken out of the folder open now, or put in a folder while Public is open: it leaves this list.
    if ((inIt && folderId.value === id) || (!inIt && folderId.value == null)) await load()
  } catch (e: any) {
    toast(e?.data?.message || 'Could not change its folders', 'error')
  } finally {
    moving.value = false
  }
}

// ── New folder / rename / delete ──
const folderDialog = ref({ open: false, id: null as number | null, name: '', error: '', saving: false, assign: [] as number[] })
/** assign = parts to put into the new folder once it is made. */
function openFolderDialog(folder: PpeFolder | null, assign: number[] = []) {
  folderDialog.value = { open: true, id: folder?.id ?? null, name: folder?.name ?? '', error: '', saving: false, assign }
}
async function saveFolder() {
  const d = folderDialog.value
  const name = d.name.trim()
  if (!name || d.saving) return
  d.saving = true
  d.error = ''
  try {
    const saved = d.id
      ? await api.put<PpeFolder>(`/folders/${d.id}`, { name })
      : await api.post<PpeFolder>('/folders', { name })
    if (d.assign.length) await api.post('/parts/folders', { ids: d.assign, add: [saved.id], remove: [] })
    const withParts = d.assign.length ? ` with ${d.assign.length} part${d.assign.length === 1 ? '' : 's'}` : ''
    toast(d.id ? `Folder renamed to ${saved.name}` : `Folder ${saved.name} created${withParts}`)
    d.open = false
    if (d.assign.length) selected.value = []
    await load()
  } catch (e: any) {
    d.error = e?.data?.message || 'Could not save the folder'
  } finally {
    d.saving = false
  }
}

const confirmDelete = ref<PpeFolder | null>(null)
const deleting = ref(false)
async function deleteFolder() {
  const folder = confirmDelete.value
  if (!folder) return
  deleting.value = true
  try {
    await api.del(`/folders/${folder.id}`)
    toast(`Folder ${folder.name} deleted — its parts are still in Public`)
    confirmDelete.value = null
    if (folderId.value === folder.id) folderId.value = null
    else await load()
  } catch (e: any) {
    toast(e?.data?.message || 'Could not delete the folder', 'error')
  } finally {
    deleting.value = false
  }
}

// ── Copy (P/N or alternative click, Copy all P/N) ──
function copy(text: string) {
  const ok = copyText(text)
  toast(ok ? `${text} copied` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
}
const copying = ref(false)
async function copyAll() {
  copying.value = true
  try {
    const params = new URLSearchParams({ showOld: String(showOld.value) })
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (folderId.value) params.set('folder', String(folderId.value))
    const names = await api.get<string[]>(`/parts/part-numbers?${params}`)
    if (!names.length) { toast('No part numbers to copy', 'warning'); return }
    const ok = copyText(names.join('\n'))
    toast(ok ? `${names.length} part number${names.length === 1 ? '' : 's'} copied` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
  } catch (e: any) {
    toast(e?.data?.message || 'Could not load the part numbers', 'error')
  } finally {
    copying.value = false
  }
}
// ── Add part (by hand) ──
const showAdd = ref(false)
/** Part to expand once it is on the loaded page (just added). */
let openAfterLoad: number | null = null
/**
 * Search for the added part so it is on screen whatever its place in the list, and open it to work on it.
 * Not put in the folder open now: it is shown in its first folder, or in Public when it is in none.
 */
function showFolderOf(inFolders: number[]) {
  if (folderId.value != null && inFolders.includes(folderId.value)) return
  if (folderId.value == null && !inFolders.length) return
  folderId.value = inFolders[0] ?? null
}

function onAdded(res: PpeAddPartResponse, inFolders: number[]) {
  toast(res.alreadyListed ? `${res.partNumber} is on your list already` : `${res.partNumber} added${res.restored ? ' back' : ''} to your list`)
  openAfterLoad = res.id
  clearTimeout(searchTimer)
  showFolderOf(inFolders)
  if (search.value === res.partNumber) load()
  else search.value = res.partNumber
}

/** Bulk import: the whole list reloads (search cleared so every new part can show). */
function onBulkAdded(res: PpeBulkAddPartsResponse, inFolders: number[]) {
  const parts = [
    res.added && `${res.added} added`,
    res.restored && `${res.restored} back on your list`,
    res.alreadyListed && `${res.alreadyListed} already on your list (qty / condition updated)`,
  ].filter(Boolean)
  toast(parts.join(' · ') || 'Nothing to add')
  clearTimeout(searchTimer)
  showFolderOf(inFolders)
  if (search.value) search.value = ''
  else reloadFirstPage()
}

/** Commission saved: the part left the list. */
async function onRemoved(item: PpePartRow) {
  toast(`Commission saved — ${item.partNumber} left your list`)
  await load()
}

async function hide(item: PpePartRow) {
  try {
    await api.post('/parts/hide', { ids: [item.id] })
    toast(`${item.partNumber} removed from your list`)
    await load()
  } catch (e: any) {
    toast(e?.data?.message || 'Could not remove it', 'error')
  }
}
</script>

<style scoped>
:deep(.ppe-row--draft) {
  background: rgba(var(--v-theme-warning), 0.07);
}
:deep(.ppe-row--open) {
  background: rgba(var(--v-theme-primary), 0.06) !important;
}
:deep(tbody tr) {
  cursor: pointer;
}
.pn-copy {
  cursor: copy;
}
.pn-copy:hover {
  text-decoration: underline dotted;
}
.price-text {
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
}
.folder-count {
  margin-left: 6px;
  font-size: 11px;
  font-weight: 700;
  opacity: 0.75;
}
.bulk-bar {
  padding: 6px 10px;
  border-radius: 8px;
  background: rgba(103, 58, 183, 0.08);
  border: 1px solid rgba(103, 58, 183, 0.25);
}
.legend {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: rgba(var(--v-theme-on-surface), 0.6);
}
.legend__dot {
  width: 10px;
  height: 10px;
  border-radius: 2px;
}
.legend__dot--draft {
  background: rgba(var(--v-theme-warning), 0.5);
}
.legend__dot--email {
  background: rgba(var(--v-theme-info), 0.5);
}
</style>
