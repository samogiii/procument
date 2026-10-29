<template>
  <!--
    New Procurement Plus — ILS tab: upload ILS CSV / Excel files; every supplier listing one of my parts shows with its
    conditions and alternatives as chips. Picked ones are added under every part they answer (warning rows on the
    Parts tab until the price is typed). view="preferred" = the Preferred suppliers tab: the same files, shown per
    preferred supplier (PpePreferredTab).
  -->
  <div>
    <v-card class="glass-card mb-4">
      <v-card-text>
        <div class="d-flex flex-wrap align-stretch gap-4">
          <div
            class="ppe-drop"
            :class="{ 'ppe-drop--over': dragDepth > 0, 'ppe-drop--busy': uploading }"
            role="button"
            tabindex="0"
            @click="fileInput?.click()"
            @keydown.enter.prevent="fileInput?.click()"
            @dragenter.prevent="dragDepth++"
            @dragover.prevent
            @dragleave.prevent="dragDepth = Math.max(0, dragDepth - 1)"
            @drop.prevent="onDrop"
          >
            <v-progress-circular v-if="uploading" indeterminate color="primary" size="30" />
            <v-icon v-else :icon="dragDepth ? 'mdi-tray-arrow-down' : 'mdi-file-upload-outline'" size="32" :color="dragDepth ? 'primary' : undefined" />
            <div class="font-weight-medium mt-1">{{ uploading ? 'Reading files…' : dragDepth ? 'Drop to upload' : 'Upload ILS files' }}</div>
            <div class="text-caption text-medium-emphasis">CSV or Excel · several at once</div>
            <input ref="fileInput" type="file" multiple accept=".csv,.txt,.xlsx,.xlsm,.xls" class="d-none" @change="onPick">
          </div>

          <div class="flex-grow-1 min-w-0">
            <div class="d-flex align-center mb-2">
              <div class="text-subtitle-2 font-weight-bold">Current files</div>
              <span class="text-caption text-medium-emphasis ml-2">used for {{ data?.freshDays ?? 7 }} days</span>
              <v-spacer />
              <v-btn size="x-small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
            </div>
            <div v-if="!data?.uploads.length" class="text-body-2 text-medium-emphasis py-2">
              No files yet. Upload the ILS lists you got for your parts.
            </div>
            <div class="d-flex flex-wrap gap-2">
              <v-chip
                v-for="u in data?.uploads ?? []"
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
            <v-alert v-for="e in uploadErrors" :key="e.fileName" type="error" variant="tonal" density="compact" closable class="mt-2">
              <strong>{{ e.fileName }}</strong>: {{ e.message }}
            </v-alert>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <PpePreferredTab
      v-if="view === 'preferred'"
      :data="data"
      :lines="lines"
      :loading="loading"
      :busy="busy"
      :asked-conditions="askedConditions"
      @add="add"
      @copy-pns="copySupplierPns"
      @mail="openMailFor"
      @create="preferredDialog?.openForm()"
      @manage="manageOpen = true"
      @edit="p => preferredDialog?.openForm(p)"
      @remove="removePreferred"
      @toast="toast"
    />

    <v-card v-else class="glass-card">
      <v-card-text>
        <div v-if="loading && !data" class="d-flex justify-center py-8"><v-progress-circular indeterminate color="primary" /></div>
        <p v-else-if="!data?.rows.length" class="text-body-2 text-medium-emphasis text-center py-6 mb-0">
          {{ data?.uploads.length ? 'Your current files list none of your parts.' : 'Upload files to see which suppliers list your parts.' }}
        </p>
        <template v-else>
          <p class="text-caption text-medium-emphasis mb-2">
            Pick a supplier's chips (part / alternative · condition · qty) and Add — the supplier goes under every part they
            answer as one warning row (no condition / alt yet) until you type the price, or its quote mail is accepted in
            Email Quotes. Bold conditions are the ones your part asks for.
          </p>
          <ProcurementPlusFileOffers :rows="data.rows" :lines="lines" :busy="busy" :asked-conditions="askedConditions" match-part @add="add">
            <template #supplier-actions="{ rows, first }">
              <v-btn
                v-if="!first.preferredId"
                size="x-small"
                variant="text"
                color="amber"
                icon="mdi-star-plus-outline"
                title="Add to my preferred suppliers"
                @click="addPreferredFrom(rows, first)"
              />
              <v-btn
                size="x-small"
                variant="text"
                icon="mdi-content-copy"
                title="Copy this supplier's P/Ns (parts and alternatives), one per line"
                @click="copySupplierPns(rows)"
              />
              <v-btn
                size="x-small"
                variant="text"
                color="blue"
                icon="mdi-email-edit-outline"
                title="Quotation mail for this supplier — pick the parts, copy the mail, then open it in your mail app"
                @click="openMail(rows, first)"
              />
            </template>
          </ProcurementPlusFileOffers>
        </template>
      </v-card-text>
    </v-card>

    <PpeSupplierMailDialog
      v-model="mail.open"
      :supplier-name="mail.supplierName"
      :emails="mail.emails"
      :rows="mail.rows"
      :parts="data?.parts ?? []"
    />

    <!-- My preferred suppliers — the same list as Procurement ProMax -->
    <ProcurementPlusPreferredDialog
      ref="preferredDialog"
      v-model="manageOpen"
      :url="(p: string) => api.url(`/preferred-suppliers${p}`)"
      supplier-search-path="/ppe/suppliers"
      @changed="load"
    />

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="5000">{{ snackText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import { toCsvFile } from '~/utils/procurementPlus'
import { asOfferLines, supplierMailAddresses } from '~/utils/ppe'
import type { PpeIls } from '~/utils/ppe'
import type { PpPreferredSupplier } from '~/components/ProcurementPlusPreferredDialog.vue'

/** all = ILS tab (every supplier); preferred = Preferred suppliers tab. */
withDefaults(defineProps<{ view?: 'all' | 'preferred' }>(), { view: 'all' })

/** Lines were added — the Parts tab reloads. */
const emit = defineEmits<{ changed: [] }>()

const api = usePpeApi()
const data = ref<PpeIls | null>(null)
const lines = computed(() => asOfferLines(data.value?.lines ?? []))
const loading = ref(false)
const busy = ref<number | null>(null)

const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

async function load() {
  loading.value = true
  try {
    data.value = await api.get<PpeIls>('/ils')
  } catch (e: any) {
    toast(e?.data?.message || 'Could not load the files', 'error')
  } finally {
    loading.value = false
  }
}
defineExpose({ load })
onMounted(load)
watch(api.asUser, load)

// ── Upload ──
const fileInput = ref<HTMLInputElement | null>(null)
const dragDepth = ref(0)
const uploading = ref(false)
const uploadErrors = ref<{ fileName: string; message: string }[]>([])

function onPick(e: Event) {
  const input = e.target as HTMLInputElement
  const files = [...(input.files ?? [])]
  input.value = ''
  if (files.length) upload(files)
}
function onDrop(e: DragEvent) {
  dragDepth.value = 0
  const files = [...(e.dataTransfer?.files ?? [])]
  if (files.length) upload(files)
}

async function upload(files: File[]) {
  uploading.value = true
  uploadErrors.value = []
  try {
    const form = new FormData()
    for (const f of files) {
      try {
        form.append('files', await toCsvFile(f))
      } catch {
        uploadErrors.value.push({ fileName: f.name, message: 'Could not read this Excel file.' })
      }
    }
    if (!form.has('files')) return
    const res = await api.post<{ uploads: { rowCount: number; matchedRows: number }[]; errors: { fileName: string; message: string }[] }>('/uploads', form)
    uploadErrors.value.push(...res.errors)
    if (res.uploads.length) {
      const rows = res.uploads.reduce((n, u) => n + u.rowCount, 0)
      const matched = res.uploads.reduce((n, u) => n + u.matchedRows, 0)
      toast(`${res.uploads.length} file${res.uploads.length === 1 ? '' : 's'} read · ${rows} rows · ${matched} for your parts`)
    }
    await load()
    // The Preferred suppliers tab shows what they list
    const prefs = new Set((data.value?.rows ?? []).map(r => r.preferredId).filter(Boolean)).size
    if (res.uploads.length && prefs) toast(`${snackText.value} · ${prefs} preferred supplier${prefs === 1 ? '' : 's'} list them — see Preferred suppliers`)
  } catch (e: any) {
    toast(e?.data?.message || 'Upload failed', 'error')
  } finally {
    uploading.value = false
  }
}

async function deleteUpload(id: number) {
  await api.del(`/uploads/${id}`)
  await load()
}

// ── Per supplier: copy P/Ns, quotation mail ──
/** The P/Ns a supplier lists (as in the file: the part or an alternative), each once, in file order. */
const supplierPns = (rows: PpOfferRow[]) => [...new Set(rows.map(r => r.filePartNo.trim()).filter(Boolean))]

function copySupplierPns(rows: PpOfferRow[]) {
  const pns = supplierPns(rows)
  const ok = copyText(pns.join('\n'))
  toast(ok ? `${pns.length} P/N${pns.length === 1 ? '' : 's'} copied` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
}

const mail = ref<{ open: boolean; supplierName: string; emails: string[]; rows: PpOfferRow[] }>({ open: false, supplierName: '', emails: [], rows: [] })
function openMail(rows: PpOfferRow[], first: PpOfferRow) {
  openMailFor(rows, first.preferredName || first.supplierName || first.companyRaw, supplierMailAddresses(rows))
}
function openMailFor(rows: PpOfferRow[], supplierName: string, emails: string[]) {
  mail.value = { open: true, supplierName, emails, rows }
}

// ── Preferred suppliers (each user's own list, shared with ProMax) ──
const preferredDialog = ref<{ openForm: (r?: Partial<PpPreferredSupplier>) => void } | null>(null)
const manageOpen = ref(false)

/** Star on the ILS tab: the add form filled from the file rows (name, emails, catalog supplier). */
function addPreferredFrom(rows: PpOfferRow[], first: PpOfferRow) {
  preferredDialog.value?.openForm({
    name: first.supplierName || first.companyRaw,
    emails: supplierMailAddresses(rows),
    phone: rows.find(r => r.phone?.trim())?.phone ?? undefined,
    supplierId: rows.find(r => r.supplierId)?.supplierId ?? undefined,
  })
}

async function removePreferred(p: PpPreferredSupplier) {
  if (!confirm(`Remove ${p.name} from your preferred suppliers? It is also removed from Procurement ProMax (the same list).`)) return
  try {
    await api.del(`/preferred-suppliers/${p.id}`)
    toast(`${p.name} removed from your preferred suppliers`)
    await load()
  } catch (e: any) {
    toast(e?.data?.message || 'Could not remove it', 'error')
  }
}

/** Conditions my parts ask for (their own and their RFQs') — bold on the suppliers' chips. */
const askedConditions = computed(() => Object.fromEntries((data.value?.parts ?? []).map(p => [p.id, p.conditions])))

// ── Picked rows → lines on every part they answer ──
async function add(picked: PpOfferRow[], methods: string[]) {
  if (!picked.length) return
  busy.value = picked[0]!.offerId
  try {
    const res = await api.post<{ added: number; existing: number }>('/lines/from-offers', { offerIds: picked.map(r => r.offerId), methods })
    toast(res.added
      ? `${res.added} supplier line${res.added === 1 ? '' : 's'} added to your parts — fill the price${res.added === 1 ? '' : 's'} on the Parts tab`
      : 'Already under those parts', res.added ? 'success' : 'info')
    await load()
    emit('changed')
  } catch (e: any) {
    toast(e?.data?.message || 'Could not add them', 'error')
  } finally {
    busy.value = null
  }
}
</script>

<style scoped>
.ppe-drop {
  flex: 0 0 260px; min-height: 120px; border-radius: 12px; cursor: pointer; text-align: center;
  display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 14px;
  border: 2px dashed rgba(var(--v-theme-primary), 0.35); background: rgba(var(--v-theme-primary), 0.03);
  transition: background 0.15s, border-color 0.15s;
}
.ppe-drop:hover, .ppe-drop--over { background: rgba(var(--v-theme-primary), 0.08); border-color: rgb(var(--v-theme-primary)); }
.ppe-drop--busy { pointer-events: none; opacity: 0.8; }
@media (max-width: 700px) { .ppe-drop { flex-basis: 100%; } }
</style>
