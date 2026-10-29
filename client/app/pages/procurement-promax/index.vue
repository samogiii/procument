<template>
  <div class="pp">
    <PageHeader title="Procurement ProMax">
      <template #actions>
        <!-- Every user has their own page; SuperAdmin can open anyone's, or everyone's at once -->
        <v-select
          v-if="isSuperAdmin"
          v-model="asUser"
          :items="userOptions"
          item-title="title"
          item-value="value"
          density="compact"
          variant="outlined"
          hide-details
          prepend-inner-icon="mdi-account-switch-outline"
          style="min-width: 200px; max-width: 240px"
        />
        <v-btn variant="tonal" color="amber-darken-2" prepend-icon="mdi-star-outline" @click="preferredOpen = true">Preferred suppliers</v-btn>
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-playlist-plus" @click="openAddParts()">Add parts</v-btn>
        <v-scale-transition>
          <v-btn
            v-if="!allMode && availability?.uploads.length"
            color="primary"
            variant="flat"
            prepend-icon="mdi-store-search-outline"
            class="pp-available-btn"
            :class="{ 'pp-available-btn--fresh': freshResults }"
            @click="openAvailability()"
          >
            Suppliers available
            <v-badge :content="availableSupplierCount" color="amber" inline class="ml-1" />
          </v-btn>
        </v-scale-transition>
      </template>
    </PageHeader>

    <v-alert v-if="viewingOther" type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-account-eye-outline">
      <template v-if="allMode">Every user's parts — each shows whose it is. Files and Suppliers available are per user: pick one user to see them.</template>
      <template v-else>You are viewing <strong>{{ viewedName }}</strong>'s page. Changes you make go to their page.</template>
    </v-alert>

    <!-- Overview -->
    <div class="pp-stats mb-4">
      <div class="pp-stat pp-stat--click" @click="tab = 'parts'">
        <v-icon icon="mdi-airplane-search" color="deep-purple" />
        <div><div class="pp-stat__value">{{ boardCounts.All }}</div><div class="pp-stat__label">Parts (every tab + your RFQs)</div></div>
      </div>
      <div class="pp-stat pp-stat--click" @click="!allMode && availability?.uploads.length && openAvailability()">
        <v-icon icon="mdi-star" color="amber" />
        <div>
          <div class="pp-stat__value">{{ preferredSupplierCount }} <span class="pp-stat__sub">+ {{ otherSupplierCount }} other</span></div>
          <div class="pp-stat__label">Suppliers found in files</div>
        </div>
      </div>
      <div class="pp-stat pp-stat--click" @click="tab = 'project'">
        <v-icon icon="mdi-briefcase-search-outline" color="teal" />
        <div><div class="pp-stat__value">{{ trackParts('Project').length }}</div><div class="pp-stat__label">Project parts</div></div>
      </div>
      <div class="pp-stat pp-stat--click" @click="tab = 'wechat'">
        <v-icon icon="mdi-wechat" color="green" />
        <div><div class="pp-stat__value">{{ trackParts('WeChat').length }}</div><div class="pp-stat__label">WeChat parts</div></div>
      </div>
    </div>

    <!-- Work tabs -->
    <v-tabs v-model="tab" color="primary" class="mb-3" show-arrows>
      <v-tab value="parts" prepend-icon="mdi-airplane-search">
        Parts <v-chip size="x-small" class="ml-2" variant="tonal">{{ boardCounts.All }}</v-chip>
      </v-tab>
      <v-tab value="project" prepend-icon="mdi-briefcase-search-outline">
        Project <v-chip v-if="boardCounts.Project" size="x-small" class="ml-2" color="teal" variant="flat">{{ boardCounts.Project }}</v-chip>
      </v-tab>
      <v-tab value="wechat" prepend-icon="mdi-wechat">
        WeChat <v-chip v-if="boardCounts.WeChat" size="x-small" class="ml-2" color="green" variant="flat">{{ boardCounts.WeChat }}</v-chip>
      </v-tab>
      <v-tab value="rfq" prepend-icon="mdi-file-search-outline">By RFQ</v-tab>
      <v-tab value="done" prepend-icon="mdi-check-circle-outline">Done</v-tab>
    </v-tabs>
    <p class="text-body-2 text-medium-emphasis mb-3">
      <v-icon :icon="TAB_HELP[tab].icon" size="16" class="mr-1" />{{ TAB_HELP[tab].text }}
    </p>

    <!-- ── Parts (ILS): files, then each part with its suppliers underneath ── -->
    <div v-show="tab === 'parts'">
      <v-card v-if="!allMode" class="glass-card mb-4">
        <v-card-text>
          <div class="d-flex flex-wrap align-stretch gap-4">
            <div
              class="pp-drop"
              :class="{ 'pp-drop--over': dragDepth > 0, 'pp-drop--busy': uploading }"
              role="button"
              tabindex="0"
              @click="pickFiles"
              @keydown.enter.prevent="pickFiles"
              @dragenter.prevent="dragDepth++"
              @dragover.prevent
              @dragleave.prevent="dragDepth = Math.max(0, dragDepth - 1)"
              @drop.prevent="onDrop"
            >
              <v-progress-circular v-if="uploading" indeterminate color="primary" size="30" />
              <v-icon v-else :icon="dragDepth ? 'mdi-tray-arrow-down' : 'mdi-file-upload-outline'" size="32" :color="dragDepth ? 'primary' : undefined" />
              <div class="font-weight-medium mt-1">{{ uploading ? 'Reading files…' : dragDepth ? 'Drop to upload' : 'Upload availability files' }}</div>
              <div class="text-caption text-medium-emphasis">ILS CSV or Excel · several at once</div>
              <input ref="fileInput" type="file" multiple accept=".csv,.txt,.xlsx,.xlsm,.xls" class="d-none" @change="onPick">
            </div>

            <div class="flex-grow-1 min-w-0">
              <div class="d-flex align-center mb-2">
                <div class="text-subtitle-2 font-weight-bold">Current files</div>
                <span class="text-caption text-medium-emphasis ml-2">used for {{ PP_FRESH_DAYS }} days, then they move to History</span>
                <v-spacer />
                <v-btn v-if="availability?.uploads.length" size="x-small" variant="text" color="error" prepend-icon="mdi-delete-sweep-outline" @click="clearFiles">Clear files</v-btn>
              </div>
              <div v-if="!availability?.uploads.length" class="text-body-2 text-medium-emphasis py-2">
                No files yet. Add the parts you're working on, then upload the availability lists you got for them.
              </div>
              <div class="d-flex flex-wrap gap-2">
                <v-chip
                  v-for="u in availability?.uploads ?? []"
                  :key="u.id"
                  variant="outlined"
                  closable
                  prepend-icon="mdi-file-delimited-outline"
                  @click:close="deleteUpload(u.id)"
                >
                  {{ u.fileName }}
                  <span class="text-caption text-medium-emphasis ml-1">· {{ u.matchedRows }}/{{ u.rowCount }} rows for your parts</span>
                </v-chip>
              </div>
              <v-alert
                v-if="availability?.unmatched.length"
                type="warning"
                variant="tonal"
                density="compact"
                class="mt-3"
                icon="mdi-account-question-outline"
              >
                <div class="d-flex align-center flex-wrap gap-2">
                  <span>
                    <strong>{{ availability.unmatched.length }}</strong> compan{{ availability.unmatched.length === 1 ? 'y' : 'ies' }} in your files
                    {{ availability.unmatched.length === 1 ? 'is' : 'are' }} not in the catalog:
                    {{ availability.unmatched.slice(0, 4).map(c => c.companyRaw).join(', ') }}{{ availability.unmatched.length > 4 ? '…' : '' }}
                  </span>
                  <v-spacer />
                  <v-btn size="small" color="warning" variant="flat" @click="resolveOpen = true">Find them</v-btn>
                </div>
              </v-alert>
              <v-alert v-for="e in uploadErrors" :key="e.fileName" type="error" variant="tonal" density="compact" closable class="mt-2">
                <strong>{{ e.fileName }}</strong>: {{ e.message }}
              </v-alert>
            </div>
          </div>
        </v-card-text>
      </v-card>

    </div>

    <!-- ── Parts (ILS) / Project / WeChat: part → suppliers (→ timeline) ── -->
    <ProcurementPlusBoard
      v-for="b in BOARDS"
      v-show="tab === b.tab"
      :key="b.track"
      :ref="(el: any) => (boards[b.track] = el)"
      :track="b.track"
      :show-owner="allMode"
      :has-files="!!availability?.uploads.length"
      @loaded="(n: number) => (boardCounts[b.track] = n)"
      @changed="onBoardChanged(b.track)"
      @add-part="openAddParts(b.track)"
      @edit-part="editPart"
      @move="openMove"
      @hide="(ids: number[]) => hideParts(ids, true)"
      @show="(ids: number[]) => hideParts(ids, false)"
      @delete="deleteParts"
    />

    <!-- ── By RFQ: which items of an RFQ have suppliers, which don't ── -->
    <ProcurementPlusRfqView
      v-show="tab === 'rfq'"
      ref="rfqView"
      :show-owner="allMode"
      @changed="onRfqChanged"
      @mine-changed="boards.All?.load(); loadAvailability()"
      @edit-part="editPart"
      @move="openMove"
      @hide="(ids: number[]) => hideParts(ids, true)"
      @show="(ids: number[]) => hideParts(ids, false)"
      @delete="deleteParts"
    />


    <!-- ── Done ── -->
    <v-card v-if="tab === 'done'" class="glass-card">
      <v-card-text>
        <p class="text-body-2 text-medium-emphasis mb-4">
          Prices set from Procurement ProMax — written to RFQs whose deadline was at most a week ago, and auto-filled onto new RFQs
          for the same part for 7 days.
        </p>
        <ProcurementPlusDonePanel :key="doneKey" />
      </v-card-text>
    </v-card>

    <!-- Dialogs -->
    <ProcurementPlusAddPartsDialog v-model="addOpen" :initial-track="addTrack" @added="onPartsAdded" />
    <ProcurementPlusPreferredDialog v-model="preferredOpen" @changed="loadAvailability" />
    <ProcurementPlusResolveDialog v-model="resolveOpen" :companies="availability?.unmatched ?? []" @resolved="loadAvailability" />
    <ProcurementPlusAvailabilityDialog
      v-model="availabilityOpen"
      :data="availability"
      :loading="availabilityLoading"
      :initial-tab="availabilityTab"
      @source="openSource"
      @resolve="resolveOpen = true"
      @reload="loadAvailability"
      @hide-part="(id: number) => hideParts([id], true)"
      @move="openMove"
    />
    <ProcurementPlusSourceDialog v-model="sourceOpen" :rows="sourceRows" @started="onSourcingStarted" @reload="loadAvailability" />
    <ProcurementPlusMoveDialog v-model="move.open" :track="move.track" :parts="move.parts" @moved="onMoved" />

    <!-- Edit a preferred part (kept in Preferreds — the catalog is not changed) -->
    <v-dialog v-model="partEdit.open" max-width="560">
      <v-card>
        <v-card-title class="pa-4 d-flex align-center">
          {{ partEdit.partNumber }}
          <v-spacer />
          <v-chip size="small" :color="trackMeta(partEdit.track).color" variant="tonal" :prepend-icon="trackMeta(partEdit.track).icon">
            {{ trackMeta(partEdit.track).title }}
          </v-chip>
        </v-card-title>
        <v-card-text class="pt-0">
          <v-text-field v-model="partEdit.description" label="Description" density="compact" variant="outlined" class="mb-3" hide-details />
          <div class="d-flex gap-3 mb-3">
            <v-text-field v-model.number="partEdit.qty" type="number" label="Qty" density="compact" variant="outlined" hide-details />
            <v-combobox v-model="partEdit.condition" :items="CONDITIONS" label="Condition" density="compact" variant="outlined" hide-details />
            <v-text-field
              v-model.number="partEdit.targetPrice"
              type="number"
              :label="needsTargetPrice(partEdit.track) ? 'Target price *' : 'Target price'"
              prefix="$"
              density="compact"
              variant="outlined"
              hide-details
              :error="needsTargetPrice(partEdit.track) && !(Number(partEdit.targetPrice) > 0)"
            />
          </div>
          <v-btn-toggle v-model="partEdit.track" mandatory density="compact" variant="outlined" divided class="mb-3">
            <v-btn v-for="t in PART_TRACKS" :key="t.value" :value="t.value" size="small" :color="t.color" :prepend-icon="t.icon">{{ t.title }}</v-btn>
          </v-btn-toggle>
          <v-combobox
            v-model="partEdit.alternatives"
            label="Alternative part numbers"
            placeholder="Type a P/N and press Enter"
            multiple
            chips
            closable-chips
            density="compact"
            variant="outlined"
            :delimiters="[',', ';']"
            hint="File rows listing any of these count for this part"
            persistent-hint
          />
          <div v-if="partEdit.catalogAlternatives.length" class="text-caption text-medium-emphasis mt-3">
            Also from the linked catalog part: {{ partEdit.catalogAlternatives.join(', ') }}
          </div>
          <v-alert v-if="partEdit.error" type="error" variant="tonal" density="compact" class="mt-3">{{ partEdit.error }}</v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="partEdit.open = false">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="partEdit.saving"
            :disabled="needsTargetPrice(partEdit.track) && !(Number(partEdit.targetPrice) > 0)"
            @click="savePart"
          >
            Save
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
    <ConfirmDialog v-model="confirm.open" :title="confirm.title" :message="confirm.message" @confirm="confirm.action?.()" />
    <v-snackbar v-model="snack" :color="snackColor" :timeout="4500">{{ snackText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { PpAvailability } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import type { PpPart } from '~/utils/procurementPlus'

// Access: Menu Access "Procurement ProMax" (route gate in middleware/auth.global.ts). Never in the menu (Sam) — opened by URL.

const api = useProcurementPlusApi()
const authStore = useAuthStore()
type PpTab = 'parts' | 'project' | 'wechat' | 'rfq' | 'done'
const PP_TABS: PpTab[] = ['parts', 'project', 'wechat', 'rfq', 'done']
const route = useRoute()
const router = useRouter()
// The open tab lives in the address bar (?tab=rfq), so a refresh comes back to it.
const tab = ref<PpTab>(PP_TABS.includes(route.query.tab as PpTab) ? route.query.tab as PpTab : 'parts')
watch(tab, (t) => {
  const query = { ...route.query }
  if (t === 'parts') delete query.tab
  else query.tab = t
  router.replace({ query })
})

const TAB_HELP: Record<string, { icon: string; text: string }> = {
  parts: { icon: 'mdi-airplane-search', text: 'Every part you work on — Parts, Project, WeChat and the items of your RFQs (By RFQ), tinted by the tab it comes from. Copy all P/Ns, search them on ILS, upload the files: the suppliers they list show under each part on every tab.' },
  project: { icon: 'mdi-briefcase-search-outline', text: 'Parts nobody lists — worked on as a project: add your own suppliers, keep the tracing timeline, aim for the target price.' },
  wechat: { icon: 'mdi-wechat', text: 'Parts sourced over WeChat: copy the request text, add the suppliers who answer, fill their prices.' },
  rfq: { icon: 'mdi-file-search-outline', text: 'Load an RFQ to see which of its parts already have suppliers and which do not — then add and fill the missing ones.' },
  done: { icon: 'mdi-check-circle-outline', text: 'Prices put on RFQs from here — they keep filling new RFQs for the same part for 7 days.' },
}

const BOARDS = [
  { tab: 'parts', track: 'All' },
  { tab: 'project', track: 'Project' },
  { tab: 'wechat', track: 'WeChat' },
] as const

// ── Snackbar / confirm ──
const snack = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') { snackText.value = text; snackColor.value = color; snack.value = true }
const confirm = reactive<{ open: boolean; title: string; message: string; action: (() => void) | null }>({ open: false, title: '', message: '', action: null })
function ask(title: string, message: string, action: () => void) { Object.assign(confirm, { open: true, title, message, action }) }

// ── Whose page (SuperAdmin) ──
const isSuperAdmin = computed(() => authStore.isSuperAdmin)
const asUser = api.asUser
const users = ref<{ id: number; name: string; partCount: number; sourcingCount: number }[]>([])
const userOptions = computed(() => [
  { title: 'My page', value: null },
  ...users.value
    .filter(u => u.id !== Number(authStore.user?.id))
    .map(u => ({ title: `${u.name} · ${u.partCount} part${u.partCount === 1 ? '' : 's'}`, value: String(u.id) })),
  { title: 'All users', value: 'all' },
])
const allMode = computed(() => asUser.value === 'all')
const viewingOther = computed(() => !!asUser.value)
const viewedName = computed(() => users.value.find(u => String(u.id) === asUser.value)?.name ?? '')

async function loadUsers() {
  if (!isSuperAdmin.value) return
  try { users.value = await api.get('/procurement-plus/users') } catch { users.value = [] }
}
// The boards reload themselves when the user changes.
watch(asUser, () => {
  Promise.all([loadParts(), loadAvailability(), loadUsers()])
})

// ── Parts (every track, removed ones too — counts and the move dialog read them) ──
const allParts = ref<PpPart[]>([])
const partsLoading = ref(false)

const trackParts = (track: string) => allParts.value.filter(p => p.track === track && !p.hidden)

async function loadParts() {
  partsLoading.value = true
  try {
    allParts.value = await api.get<PpPart[]>('/procurement-plus/parts?includeHidden=true')
  } finally {
    partsLoading.value = false
  }
}

async function hideParts(ids: number[], hidden: boolean) {
  await api.post(`/procurement-plus/parts/${hidden ? 'hide' : 'show'}`, { ids })
  toast(hidden ? `${ids.length} part${ids.length === 1 ? '' : 's'} removed — "Show removed parts" brings them back` : 'Shown again')
  await reloadAll()
}

function deleteParts(ids: number[]) {
  ask('Delete parts?', `Delete ${ids.length} part${ids.length === 1 ? '' : 's'} from the list for good? Prices already set on RFQs stay.`, async () => {
    await api.post('/procurement-plus/parts/delete', { ids })
    await reloadAll()
  })
}

// ── Edit a part ──
const partEdit = reactive({
  open: false, id: 0, partNumber: '', description: '', alternatives: [] as string[], catalogAlternatives: [] as string[],
  qty: null as number | null, condition: null as string | null, targetPrice: null as number | null, track: 'ILS',
  saving: false, error: '',
})
function editPart(p: PpPart) {
  const own = new Set(p.ownAlternatives.map(a => a.toUpperCase()))
  Object.assign(partEdit, {
    open: true, id: p.id, partNumber: p.partNumber, description: p.description ?? '',
    alternatives: [...p.ownAlternatives],
    catalogAlternatives: p.alternatives.filter(a => !own.has(a.toUpperCase())),
    qty: p.qty ?? null, condition: p.condition ?? null, targetPrice: p.targetPrice ?? null, track: p.track,
    saving: false, error: '',
  })
}
async function savePart() {
  partEdit.saving = true
  partEdit.error = ''
  try {
    await api.patch(`/procurement-plus/parts/${partEdit.id}`, {
      description: partEdit.description,
      alternatives: cleanAltList(partEdit.alternatives),
      qty: Number(partEdit.qty) > 0 ? Number(partEdit.qty) : 0,
      condition: partEdit.condition ?? '',
      targetPrice: Number(partEdit.targetPrice) > 0 ? Number(partEdit.targetPrice) : 0,
      track: partEdit.track,
    })
    partEdit.open = false
    await reloadAll()
  } catch (e: any) {
    partEdit.error = e?.data?.message || 'Could not save'
  } finally {
    partEdit.saving = false
  }
}

// ── Add parts / move between tracks ──
const addOpen = ref(false)
const addTrack = ref('ILS')
function openAddParts(track = tab.value === 'project' ? 'Project' : tab.value === 'wechat' ? 'WeChat' : 'ILS') {
  addTrack.value = track
  addOpen.value = true
}
function onPartsAdded(message: string, track: string) {
  toast(message)
  tab.value = track === 'Project' ? 'project' : track === 'WeChat' ? 'wechat' : 'parts'
  reloadAll()
}

const move = reactive<{ open: boolean; track: string; parts: PpPart[] }>({ open: false, track: 'Project', parts: [] })
function openMove(ids: number[], track: string) {
  move.parts = allParts.value.filter(p => ids.includes(p.id))
  move.track = track
  if (move.parts.length) move.open = true
}
function onMoved(message: string) {
  toast(message)
  tab.value = move.track === 'Project' ? 'project' : move.track === 'WeChat' ? 'wechat' : 'parts'
  reloadAll()
}

// ── Files & availability (one user's page) ──
const availability = ref<PpAvailability | null>(null)
const availabilityLoading = ref(false)
const availabilityOpen = ref(false)
const availabilityTab = ref('preferred')
const freshResults = ref(false)

async function loadAvailability() {
  if (allMode.value) { availability.value = null; return }
  availabilityLoading.value = true
  try {
    availability.value = await api.get<PpAvailability>('/procurement-plus/availability')
  } finally {
    availabilityLoading.value = false
  }
}

const countSuppliers = (rows: { preferredId?: number; supplierId?: number; companyKey: string }[] = []) =>
  new Set(rows.map(r => (r.preferredId ? `p${r.preferredId}` : r.supplierId ? `s${r.supplierId}` : `c:${r.companyKey}`))).size
const preferredSupplierCount = computed(() => countSuppliers(availability.value?.preferred))
const otherSupplierCount = computed(() => countSuppliers(availability.value?.others))
const availableSupplierCount = computed(() => preferredSupplierCount.value + otherSupplierCount.value)

function openAvailability(tabName = 'preferred') {
  availabilityTab.value = tabName
  freshResults.value = false
  availabilityOpen.value = true
}

const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
const dragDepth = ref(0)
const uploadErrors = ref<{ fileName: string; message: string }[]>([])

function pickFiles() { if (!uploading.value) fileInput.value?.click() }
function onPick(e: Event) {
  const input = e.target as HTMLInputElement
  const files = [...(input.files ?? [])]
  input.value = ''
  if (files.length) upload(files)
}
function onDrop(e: DragEvent) {
  dragDepth.value = 0
  const files = [...(e.dataTransfer?.files ?? [])]
  if (files.length && !uploading.value) upload(files)
}

async function upload(files: File[]) {
  uploading.value = true
  uploadErrors.value = []
  try {
    const data = new FormData()
    for (const f of files) {
      try {
        data.append('files', await toCsvFile(f))
      } catch {
        uploadErrors.value.push({ fileName: f.name, message: 'Could not read this Excel file.' })
      }
    }
    if (!data.has('files')) return
    const res = await api.post<{ uploads: { rowCount: number; matchedRows: number }[]; errors: { fileName: string; message: string }[] }>('/procurement-plus/uploads', data)
    uploadErrors.value.push(...res.errors)
    if (res.uploads.length) {
      const rows = res.uploads.reduce((n, u) => n + u.rowCount, 0)
      const matched = res.uploads.reduce((n, u) => n + u.matchedRows, 0)
      toast(`${res.uploads.length} file${res.uploads.length === 1 ? '' : 's'} read · ${rows} rows · ${matched} for your parts`)
      freshResults.value = true
    }
    await Promise.all([loadAvailability(), loadParts(), reloadBoards(), rfqView.value?.load()])
  } catch (e: any) {
    toast(e?.data?.message || 'Upload failed', 'error')
  } finally {
    uploading.value = false
  }
}

async function deleteUpload(id: number) {
  await api.del(`/procurement-plus/uploads/${id}`)
  await Promise.all([loadAvailability(), loadParts(), reloadBoards(), rfqView.value?.load()])
}

function clearFiles() {
  ask('Clear files?', 'Remove every current file? Suppliers being sourced and prices already set are kept.', async () => {
    await api.del('/procurement-plus/uploads')
    await Promise.all([loadAvailability(), loadParts(), reloadBoards(), rfqView.value?.load()])
  })
}

const sourceOpen = ref(false)
/** Rows picked on the Others tab (offer ids); null = every preferred row. Kept as ids so a reload refreshes them. */
const sourceOfferIds = ref<Set<number> | null>(null)
const sourceRows = computed(() => {
  const ids = sourceOfferIds.value
  if (!ids) return availability.value?.preferred ?? []
  return [...(availability.value?.preferred ?? []), ...(availability.value?.others ?? [])].filter(r => ids.has(r.offerId))
})
function openSource(offerIds?: number[]) {
  sourceOfferIds.value = offerIds?.length ? new Set(offerIds) : null
  sourceOpen.value = true
}
const resolveOpen = ref(false)
const preferredOpen = ref(false)

// ── Boards ──
const boards = reactive<Record<string, { load: () => Promise<void> } | null>>({})
const boardCounts = reactive<Record<string, number>>({ All: 0, Project: 0, WeChat: 0 })
const doneKey = ref(0)

function reloadBoards() {
  return Promise.all(Object.values(boards).map(b => b?.load()))
}

async function onSourcingStarted(_lineIds: number[], message: string) {
  toast(message)
  availabilityOpen.value = false
  tab.value = 'parts'
  await Promise.all([reloadBoards(), loadAvailability(), loadParts(), rfqView.value?.load()])
}

const rfqView = ref<{ load: () => Promise<void> } | null>(null)

/** Something changed on the RFQ view — the boards show the same parts. */
async function onRfqChanged() {
  await Promise.all([reloadBoards(), onBoardChanged()])
}

/** A board changed (it reloaded itself) — the Parts tab and the track boards show the same parts. */
async function onBoardChanged(from?: string) {
  doneKey.value++
  await Promise.all([
    loadParts(),
    loadAvailability(),
    ...(from ? Object.entries(boards).filter(([t]) => t !== from).map(([, b]) => b?.load()) : []),
  ])
}

function reloadAll() {
  return Promise.all([loadParts(), loadAvailability(), reloadBoards(), loadUsers(), rfqView.value?.load()])
}

onMounted(() => {
  if (!isSuperAdmin.value) asUser.value = null
  loadParts()
  loadAvailability()
  loadUsers()
})
</script>

<style scoped>
.pp-stats { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
@media (max-width: 960px) { .pp-stats { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
.pp-stat {
  display: flex; align-items: center; gap: 12px; padding: 14px 16px; border-radius: 12px;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.pp-stat--click { cursor: pointer; transition: border-color 0.15s; }
.pp-stat--click:hover { border-color: rgba(var(--v-theme-primary), 0.5); }
.pp-stat__value { font-size: 22px; font-weight: 800; line-height: 1.1; }
.pp-stat__sub { font-size: 13px; font-weight: 500; opacity: 0.6; }
.pp-stat__label { font-size: 12px; opacity: 0.65; }

.pp-drop {
  flex: 0 0 260px; min-height: 120px; border-radius: 12px; cursor: pointer; text-align: center;
  display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 14px;
  border: 2px dashed rgba(var(--v-theme-primary), 0.35); background: rgba(var(--v-theme-primary), 0.03);
  transition: background 0.15s, border-color 0.15s;
}
.pp-drop:hover, .pp-drop--over { background: rgba(var(--v-theme-primary), 0.08); border-color: rgb(var(--v-theme-primary)); }
.pp-drop--busy { pointer-events: none; opacity: 0.8; }
@media (max-width: 700px) { .pp-drop { flex-basis: 100%; } }

.pp-available-btn--fresh { animation: pp-pulse 1.6s ease-out 3; }
@keyframes pp-pulse {
  0% { box-shadow: 0 0 0 0 rgba(var(--v-theme-primary), 0.55); }
  100% { box-shadow: 0 0 0 14px rgba(var(--v-theme-primary), 0); }
}
@media (prefers-reduced-motion: reduce) { .pp-available-btn--fresh { animation: none; } }

.pp-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center;
  padding: 48px 16px; color: rgba(var(--v-theme-on-surface), 0.7);
}
.grid-scroll { overflow-x: auto; }
.pp-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.pp-table th {
  text-align: left; font-size: 12px; font-weight: 600; opacity: 0.65; padding: 6px 10px; white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.pp-table td { padding: 8px 10px; border-bottom: 1px solid rgba(var(--v-border-color), 0.06); vertical-align: top; }
.pp-table tbody tr:hover { background: rgba(var(--v-theme-primary), 0.03); }
.pp-row--on { background: rgba(var(--v-theme-primary), 0.06); }
.pp-row--hidden { opacity: 0.5; }
.pp-part { max-width: 280px; }
.pp-live { font-size: 12.5px; line-height: 1.9; }
.num { text-align: right; white-space: nowrap; }
.nowrap { white-space: nowrap; }
.min-w-0 { min-width: 0; }
</style>
