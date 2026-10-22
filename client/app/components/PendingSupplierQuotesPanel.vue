<template>
  <v-card class="glass-card mb-4">
    <v-card-title class="d-flex align-center flex-wrap gap-2 pa-4">
      <v-icon icon="mdi-email-arrow-left-outline" color="primary" />
      <span class="text-subtitle-1 font-weight-bold">Supplier Quotes from Email</span>
      <v-chip size="small" :color="totalLines ? 'warning' : 'grey'" variant="tonal">
        {{ totalLines }} {{ searchTerm ? 'found' : 'pending' }} · {{ totalEmails }} email{{ totalEmails === 1 ? '' : 's' }}
      </v-chip>
      <v-spacer />
      <v-text-field
        v-model="search"
        placeholder="Search part number / Alt P/N"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        prepend-inner-icon="mdi-magnify"
        title="Searches every page"
        style="max-width: 280px; min-width: 200px;"
      />
      <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
      <!-- <v-btn
        size="small"
        color="error"
        variant="tonal"
        prepend-icon="mdi-delete-sweep-outline"
        :disabled="!pendingTotal || loading"
        title="Remove every pending line on every page"
        @click="askRemoveAll"
      >
        Remove all
      </v-btn> -->
      <v-btn
        size="small"
        variant="text"
        :icon="expanded ? 'mdi-chevron-up' : 'mdi-chevron-down'"
        @click="expanded = !expanded"
      />
    </v-card-title>

    <v-expand-transition>
      <v-card-text v-show="expanded" class="pt-0">
        <p v-if="!loading && !rows.length" class="text-caption text-medium-emphasis text-center py-4 mb-0">
          <template v-if="searchTerm">No pending lines for part "{{ searchTerm }}".</template>
          <template v-else>No pending supplier quotes. MailFlow's Quote-Export sends new ones here automatically.</template>
        </p>

        <div v-for="group in groups" :key="group.key" class="email-group mb-4">
          <!-- One supplier email -->
          <div class="d-flex align-center flex-wrap gap-2 mb-2">
            <v-autocomplete
              :model-value="group.supplierId"
              :items="supplierOptionsFor(group)"
              item-title="name"
              item-value="id"
              label="Supplier"
              density="compact"
              variant="outlined"
              hide-details
              style="max-width: 260px; min-width: 220px;"
              :color="group.supplierId ? undefined : 'warning'"
              :loading="supplierSearchLoading"
              no-filter
              @update:search="searchSuppliers"
              @update:model-value="(id: number | null) => setGroupSupplier(group, id)"
            />
            <v-chip v-if="!group.supplierId" size="small" color="warning" variant="tonal" prepend-icon="mdi-alert">
              No catalog supplier for "{{ group.supplierNameRaw || group.supplierEmail }}"
            </v-chip>
            <v-chip v-if="group.mailbox" size="small" variant="outlined" prepend-icon="mdi-inbox-arrow-down-outline" :title="`Read from the mailbox ${group.mailbox}`">
              {{ group.mailboxLabel || group.mailbox }}
            </v-chip>
            <div class="text-caption text-medium-emphasis">
              <span v-if="group.supplierEmail">{{ group.supplierEmail }} · </span>
              <strong>{{ group.subject || '(no subject)' }}</strong>
              <span v-if="group.date"> · {{ new Date(group.date).toLocaleString() }}</span>
            </div>
            <v-spacer />
            <v-btn
              v-if="group.emailId"
              size="small"
              color="primary"
              variant="tonal"
              prepend-icon="mdi-email-open-outline"
              @click="openEmail(group)"
            >
              View email
            </v-btn>
            <v-btn
              size="small"
              color="success"
              variant="tonal"
              prepend-icon="mdi-check-all"
              :disabled="!group.rows.some(canAccept)"
              :loading="busyKey === group.key"
              @click="accept(group.rows.filter(canAccept).map(r => r.id), group.key)"
            >
              Accept all
            </v-btn>
            <v-btn size="small" color="error" variant="text" prepend-icon="mdi-delete-outline" @click="askRemove(group.rows.map(r => r.id))">
              Remove all
            </v-btn>
          </div>

          <!-- Files attached to the email — click to read next to the email -->
          <div v-if="group.attachmentNames.length" class="d-flex flex-wrap align-center gap-1 mb-2">
            <v-icon icon="mdi-paperclip" size="16" class="text-medium-emphasis" />
            <v-chip
              v-for="name in group.attachmentNames"
              :key="name"
              size="small"
              variant="outlined"
              :prepend-icon="/\.pdf$/i.test(name) ? 'mdi-file-pdf-box' : 'mdi-file-outline'"
              :color="/\.pdf$/i.test(name) ? 'red' : undefined"
              @click="openEmail(group, name)"
            >
              {{ name }}
            </v-chip>
          </div>

          <div class="pending-grid-scroll">
            <table class="pending-grid">
              <thead>
                <tr>
                  <th style="min-width: 150px;">Part Number</th>
                  <th style="min-width: 130px;">Alt P/N</th>
                  <th style="min-width: 90px;">Price ($)</th>
                  <th style="min-width: 60px;">Qty</th>
                  <th style="min-width: 60px;">Cond</th>
                  <th style="min-width: 90px;">Cert</th>
                  <th style="min-width: 95px;">Tag Date</th>
                  <th style="min-width: 90px;">Lead Time</th>
                  <th style="min-width: 90px;" title="Blank = derived per RFQ customer on accept">Sell LT</th>
                  <th style="min-width: 110px;">Shipping</th>
                  <th style="min-width: 160px;" title="Serial Number, Trace To, Tag Type, Price Type, Tagged By, MoQ, Warranty, CR">Details</th>
                  <th style="min-width: 140px;">Note</th>
                  <th style="min-width: 120px;">My Notes</th>
                  <th style="min-width: 120px;">Certs</th>
                  <th style="min-width: 240px;" title="RFQs whose deadline is in the future or passed less than a week ago">RFQs (deadline ≤ 1 week ago)</th>
                  <th style="width: 110px;"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in group.rows" :key="row.id">
                  <td class="font-weight-medium">{{ row.partNumber }}</td>
                  <td>
                    <span v-if="row.alt" class="alt-pn" title="The supplier offers this part number instead">{{ row.alt }}</span>
                    <span v-else class="text-medium-emphasis">—</span>
                  </td>
                  <td :title="row.priceRaw ? `Extracted: ${row.priceRaw}` : ''">
                    <span v-if="row.price != null">${{ formatPrice(row.price) }}</span>
                    <span v-else class="text-error">{{ row.priceRaw || 'missing' }}</span>
                  </td>
                  <td>{{ row.qty ?? '—' }}</td>
                  <td>{{ row.condition || '—' }}</td>
                  <td>{{ row.certName || '—' }}</td>
                  <td :title="row.tagDateRaw ? `In the email: ${row.tagDateRaw}` : ''">
                    <span v-if="row.tagDate">{{ formatTagDate(row.tagDate) }}</span>
                    <span v-else-if="row.tagDateRaw" class="font-italic">{{ row.tagDateRaw }}</span>
                    <span v-else class="text-medium-emphasis">—</span>
                  </td>
                  <td>{{ row.leadTime || '—' }}</td>
                  <td>
                    <span v-if="row.sellLeadTime">{{ row.sellLeadTime }}</span>
                    <span v-else class="text-medium-emphasis" title="Derived per RFQ customer on accept">auto</span>
                  </td>
                  <td class="text-caption">
                    <div v-if="row.shippingCost != null">${{ formatPrice(row.shippingCost) }}</div>
                    <div v-if="row.shippingPoint" class="text-medium-emphasis">{{ row.shippingPoint }}</div>
                    <span v-if="row.shippingCost == null && !row.shippingPoint" class="text-medium-emphasis">—</span>
                  </td>
                  <td class="text-caption">
                    <template v-for="f in QUOTE_DETAIL_FIELDS" :key="f.key">
                      <div v-if="(row as any)[f.key]"><span class="text-medium-emphasis">{{ f.label }}:</span> {{ (row as any)[f.key] }}</div>
                    </template>
                    <span v-if="!QUOTE_DETAIL_FIELDS.some(f => (row as any)[f.key])" class="text-medium-emphasis">—</span>
                  </td>
                  <td class="text-caption">{{ row.remark || '—' }}</td>
                  <td class="text-caption">{{ row.myNotes || '—' }}</td>
                  <td>
                    <div class="d-flex flex-wrap gap-1">
                      <v-chip v-for="certRef in row.customCertReferences" :key="certRef" size="x-small" variant="tonal" color="primary">{{ certRef }}</v-chip>
                      <v-chip
                        v-for="c in row.certificates"
                        :key="c.id"
                        size="x-small"
                        variant="outlined"
                        color="red"
                        prepend-icon="mdi-file-pdf-box"
                        :title="c.fileName"
                        @click="openCertificate(row, c)"
                      >
                        PDF
                      </v-chip>
                      <span v-if="!row.customCertReferences.length && !row.certificates.length" class="text-medium-emphasis">—</span>
                    </div>
                  </td>
                  <td>
                    <div v-if="row.matches.length" class="d-flex flex-wrap gap-1">
                      <v-chip
                        v-for="m in row.matches"
                        :key="m.rfqItemId"
                        size="x-small"
                        :color="m.updatesExisting ? 'info' : (daysPastDeadline(m.deadline) > 0 ? 'warning' : 'success')"
                        variant="tonal"
                        :to="`/rfqs/${m.rfqId}`"
                        :title="`${m.customerCode || ''} · requested ${m.requestedQty}${m.matchedAsAlt ? ' · quoted as alternative ' + m.matchedAsAlt : ''}${m.updatesExisting ? ' · updates this supplier\'s existing quote' : ''}`"
                      >
                        {{ m.rfqName }} · {{ new Date(m.deadline).toLocaleDateString() }}
                        <span v-if="daysPastDeadline(m.deadline) > 0" class="ml-1">({{ daysPastDeadline(m.deadline) }}d late)</span>
                        <v-icon v-if="m.matchedAsAlt" icon="mdi-swap-horizontal" size="12" class="ml-1" />
                        <v-icon v-if="m.updatesExisting" icon="mdi-pencil" size="12" class="ml-1" />
                      </v-chip>
                    </div>
                    <span v-else class="text-caption text-error">No RFQ for this part with a deadline less than a week ago</span>
                  </td>
                  <td class="text-right" style="white-space: nowrap;">
                    <v-btn
                      icon="mdi-check"
                      size="x-small"
                      variant="text"
                      color="success"
                      :disabled="!canAccept(row)"
                      :title="acceptBlocker(row) || `Add to ${row.matches.length} RFQ item(s)`"
                      :loading="busyKey === `row-${row.id}`"
                      @click="accept([row.id], `row-${row.id}`)"
                    />
                    <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" title="Edit" @click="openEdit(row)" />
                    <v-btn icon="mdi-close" size="x-small" variant="text" color="error" title="Remove" @click="askRemove([row.id])" />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <EmailQuotesPager v-model:page="page" v-model:page-size="pageSize" :total-emails="totalEmails" />
      </v-card-text>
    </v-expand-transition>

    <EmailQuoteEditDialog v-model="showEdit" :row="editRow" @updated="patchRow" />

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />

    <QuoteEmailViewerDialog
      v-model="showEmail"
      :email-id="emailViewId"
      :initial-attachment="emailViewAttachment"
      :rows="emailViewRows"
      @row-updated="patchRow"
    />

    <ConfirmDialog
      v-model="showConfirmRemove"
      title="Remove supplier quotes?"
      :message="`Remove ${removeAll ? `all ${pendingTotal}` : removeIds.length} pending line(s)${removeAll ? ' on every page' : ''}? They won't be added to any RFQ, and these emails will never be imported again.`"
      confirm-text="Remove"
      confirm-color="error"
      @confirm="doRemove"
    />

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="4000">{{ snackText }}</v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
type RfqMatch = {
  rfqId: number
  rfqName: string
  rfqItemId: number
  deadline: string
  customerCode?: string
  requestedQty: number
  matchedAsAlt?: string
  updatesExisting: boolean
}
type PendingRow = {
  id: number
  messageId?: string
  emailSubject?: string
  emailDate?: string
  supplierEmail?: string
  supplierNameRaw?: string
  supplierId: number | null
  mailbox?: string | null
  mailboxLabel?: string | null
  supplierName?: string
  partNumber: string
  alt?: string
  tagDate?: string | null
  tagDateRaw?: string
  price: number | null
  priceRaw?: string
  qty: number | null
  condition?: string
  certName?: string
  leadTime?: string
  remark?: string
  shippingCost?: number | null
  shippingPoint?: string
  sellLeadTime?: string
  myNotes?: string
  customCertReferences: string[]
  certificates: PendingCertificate[]
  emailId?: number | null
  attachmentNames: string[]
  matches: RfqMatch[]
}
type PendingCertificate = { id: number; fileName: string; sizeBytes: number }
type SupplierOption = { id: number; name: string }

/** Page's mailbox dropdown: only lines read from this MailFlow mailbox (null = all, '-' = unknown). */
const props = defineProps<{ mailbox?: string | null }>()
const emit = defineEmits<{ (e: 'accepted'): void; (e: 'loaded', count: number): void }>()

const api = useApi()
const rows = ref<PendingRow[]>([])
const loading = ref(false)
const expanded = ref(true)
const busyKey = ref('')

const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

// Paged by email on the server — only this page's lines (and their RFQ matches) are loaded.
const page = ref(1)
const pageSize = ref(20)
const totalEmails = ref(0)
const totalLines = ref(0)
/** Every pending line, even while a search narrows the list ("Remove all", tab badge). */
const pendingTotal = ref(0)
// Part search runs on the server, across every page.
const search = ref('')
const searchTerm = computed(() => search.value?.trim() || '')
let loadId = 0

async function load() {
  const id = ++loadId
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize.value) })
    if (searchTerm.value) params.set('search', searchTerm.value)
    if (props.mailbox) params.set('mailbox', props.mailbox)
    const res = await api.get<{ items: PendingRow[]; page: number; totalEmails: number; totalLines: number; pendingTotal: number }>(
      `/pending-supplier-quotes?${params}`)
    if (id !== loadId) return
    rows.value = res.items
    totalEmails.value = res.totalEmails
    totalLines.value = res.totalLines
    // The server clamps to the last page when this one emptied (e.g. after accepting its last email).
    if (res.page !== page.value) {
      clampedPage = true
      page.value = res.page
    }
    pendingTotal.value = res.pendingTotal
    emit('loaded', res.pendingTotal)
  } catch {
    if (id === loadId) showSnack('Could not load supplier quotes from email', 'error')
  } finally {
    if (id === loadId) loading.value = false
  }
}
/** Set when load() itself moved `page` — that page is already loaded. */
let clampedPage = false
onMounted(load)
watch(page, () => {
  if (clampedPage) clampedPage = false
  else load()
})
function reloadFromFirstPage() {
  if (page.value === 1) load()
  else page.value = 1
}
watch([pageSize, () => props.mailbox], reloadFromFirstPage)
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(searchTerm, () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(reloadFromFirstPage, 400)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

// One group per supplier email
const groups = computed(() => {
  const map = new Map<string, any>()
  for (const r of rows.value) {
    const key = r.messageId || `${r.supplierEmail}|${r.emailSubject}|${r.emailDate}`
    if (!map.has(key)) {
      map.set(key, {
        key,
        subject: r.emailSubject,
        date: r.emailDate,
        supplierEmail: r.supplierEmail,
        supplierNameRaw: r.supplierNameRaw,
        mailbox: r.mailbox,
        mailboxLabel: r.mailboxLabel,
        supplierId: r.supplierId,
        supplierName: r.supplierName,
        emailId: r.emailId ?? null,
        attachmentNames: r.attachmentNames || [],
        rows: [] as PendingRow[],
      })
    }
    map.get(key).rows.push(r)
  }
  return [...map.values()]
})

function acceptBlocker(row: PendingRow): string {
  if (!row.supplierId) return 'Pick a supplier from the catalog first'
  if (row.price == null) return 'Price is missing — edit the line'
  if (!row.matches.length) return 'No RFQ for this part whose deadline is less than a week ago'
  return ''
}
const canAccept = (row: PendingRow) => !acceptBlocker(row)

/** Whole days since the RFQ deadline (0 when it hasn't passed). Accepting stays possible for 7 days. */
function daysPastDeadline(deadline: string) {
  const d = new Date(deadline)
  const today = new Date()
  const ms = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()
    - new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
  return Math.max(0, Math.round(ms / 86400000))
}

function formatTagDate(d: string) {
  // DateOnly comes as "yyyy-MM-dd" — show it without a timezone shift.
  const [y, m, day] = d.split('-').map(Number)
  return new Date(y!, (m || 1) - 1, day || 1).toLocaleDateString()
}

// ── Supplier lookup ──
const supplierOptions = ref<SupplierOption[]>([])
const supplierSearchLoading = ref(false)
let supplierSearchTimer: any = null
function searchSuppliers(q: string) {
  clearTimeout(supplierSearchTimer)
  supplierSearchTimer = setTimeout(async () => {
    supplierSearchLoading.value = true
    try {
      supplierOptions.value = await api.get<SupplierOption[]>(`/pending-supplier-quotes/suppliers?q=${encodeURIComponent(q || '')}`)
    } catch {
      supplierOptions.value = []
    } finally {
      supplierSearchLoading.value = false
    }
  }, 250)
}
/** Search results plus the group's current supplier, so the selected name always renders. */
function supplierOptionsFor(group: any): SupplierOption[] {
  const current = group.supplierId ? [{ id: group.supplierId, name: group.supplierName }] : []
  return [...current, ...supplierOptions.value.filter(s => s.id !== group.supplierId)]
}

// One call for the whole email. Only the lines' value — the sender's address reaches the catalog
// supplier when a user allowed to (Menu Access) accepts.
async function setGroupSupplier(group: any, supplierId: number | null) {
  try {
    await api.post('/pending-supplier-quotes/supplier', { ids: group.rows.map((r: PendingRow) => r.id), supplierId })
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not set supplier', 'error')
  }
}

// ── Edit (shared dialog with the Accepted tab) ──
const showEdit = ref(false)
const editRow = ref<PendingRow | null>(null)
function openEdit(row: PendingRow) {
  editRow.value = row
  showEdit.value = true
}

// Cert PDF chips in the grid open a preview
const docPreview = useDocPreview()
function openCertificate(row: { id: number }, c: PendingCertificate) {
  docPreview.preview(`/pending-supplier-quotes/${row.id}/certificates/${c.id}`, c.fileName, 'application/pdf')
}

// ── Accept ──
async function accept(ids: number[], key: string) {
  if (!ids.length) return
  busyKey.value = key
  try {
    const res = await api.post<any>('/pending-supplier-quotes/accept', { ids })
    if (res.accepted) {
      const added = res.emailsAdded?.length ? ` · ${res.emailsAdded.join(', ')} added to the supplier's contacts` : ''
      showSnack(`${res.accepted} line(s) accepted — ${res.quotesWritten} supplier quote(s) added/updated on RFQs${added}`)
      emit('accepted')
    }
    if (res.emailsOnOtherSupplier?.length) {
      showSnack(`Already a contact of another supplier, not added: ${res.emailsOnOtherSupplier.join(', ')}`, 'warning')
    }
    if (res.failed?.length) {
      showSnack(res.failed.map((f: any) => `${f.partNumber}: ${f.reason}`).join(' · '), res.accepted ? 'warning' : 'error')
    }
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Accept failed', 'error')
  } finally {
    busyKey.value = ''
  }
}

// ── Source email viewer ──
const showEmail = ref(false)
const emailViewId = ref<number | null>(null)
const emailViewAttachment = ref<string | null>(null)
// The viewer edits this email's lines in its side panel; looked up by group key so a reload keeps it in sync.
const emailViewKey = ref<string | null>(null)
const emailViewRows = computed(() => groups.value.find(g => g.key === emailViewKey.value)?.rows ?? [])
function openEmail(group: { key: string; emailId: number }, attachmentName: string | null = null) {
  emailViewKey.value = group.key
  const emailId = group.emailId
  emailViewId.value = emailId
  emailViewAttachment.value = attachmentName
  showEmail.value = true
}

/** Autosaved line from the viewer / edit dialog: patch it in place instead of reloading the whole list. */
function patchRow(updated: any) {
  const row = rows.value.find(r => r.id === updated?.id)
  if (row) Object.assign(row, updated)
}

// ── Remove ──
const showConfirmRemove = ref(false)
const removeIds = ref<number[]>([])
/** Header "Remove all": every pending line on every page, done server-side. */
const removeAll = ref(false)
function askRemove(ids: number[]) {
  removeAll.value = false
  removeIds.value = ids
  showConfirmRemove.value = true
}
function askRemoveAll() {
  removeAll.value = true
  removeIds.value = []
  showConfirmRemove.value = true
}
async function doRemove() {
  showConfirmRemove.value = false
  try {
    if (removeAll.value) await api.post('/pending-supplier-quotes/remove-all', {})
    else await api.post('/pending-supplier-quotes/remove', { ids: removeIds.value })
    await load()
  } catch {
    showSnack('Could not remove', 'error')
  }
}
</script>

<style scoped>
.email-group {
  border: 1px solid var(--card-border);
  border-radius: 8px;
  padding: 10px 12px;
}
.alt-pn {
  color: #f59e0b;
  font-weight: 500;
}
.pending-grid-scroll {
  overflow-x: auto;
}
.pending-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.pending-grid th {
  text-align: left;
  font-weight: 600;
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  white-space: nowrap;
}
.pending-grid td {
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  vertical-align: middle;
}
</style>
