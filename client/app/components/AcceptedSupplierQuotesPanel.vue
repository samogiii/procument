<template>
  <v-card class="glass-card">
    <v-card-text>
      <!-- Filters -->
      <div class="d-flex flex-wrap align-center gap-3 mb-4">
        <v-select
          v-model="reviewer"
          :items="reviewers"
          item-title="name"
          item-value="id"
          label="Accepted by"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-account-check-outline"
          style="max-width: 240px; min-width: 200px;"
        />
        <v-select
          v-model="days"
          :items="PERIODS"
          item-title="label"
          item-value="value"
          label="Accepted in"
          density="compact"
          variant="outlined"
          hide-details
          style="max-width: 180px; min-width: 160px;"
        />
        <v-text-field
          v-model="search"
          label="Search part, supplier or subject"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          style="max-width: 320px; min-width: 220px;"
        />
        <v-spacer />
        <v-chip size="small" variant="tonal" color="success">
          {{ totalLines }} line{{ totalLines === 1 ? '' : 's' }} · {{ totalEmails }} email{{ totalEmails === 1 ? '' : 's' }}
        </v-chip>
        <v-btn
          size="small"
          color="success"
          variant="tonal"
          prepend-icon="mdi-microsoft-excel"
          :loading="exporting"
          :disabled="!totalLines"
          title="Every accepted line matching these filters, on every page"
          @click="exportExcel"
        >
          Export Excel
        </v-btn>
        <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
      </div>

      <div v-if="loading && !rows.length" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" />
      </div>
      <p v-else-if="!groups.length" class="text-caption text-medium-emphasis text-center py-6 mb-0">
        No accepted email quotes{{ reviewerName ? ` by ${reviewerName}` : '' }} in this period.
      </p>

      <div v-for="group in groups" :key="group.key" class="email-group mb-4">
        <!-- One supplier email -->
        <div class="d-flex align-center flex-wrap gap-2 mb-2">
          <v-chip size="small" color="primary" variant="tonal" prepend-icon="mdi-domain">
            {{ group.supplierName || group.supplierNameRaw || group.supplierEmail }}
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
            color="primary"
            variant="tonal"
            prepend-icon="mdi-sync"
            :loading="busyKey === `reapply-${group.key}`"
            title="Write these lines as they are now to their RFQs — rewrites the quotes they added and adds them to open RFQs that don't have them yet"
            @click="reapply(group.rows, `reapply-${group.key}`)"
          >
            Re-apply to RFQs
          </v-btn>
          <v-btn
            v-if="group.rows.some(isAutoFillOn)"
            size="small"
            color="warning"
            variant="tonal"
            prepend-icon="mdi-autorenew-off"
            :loading="busyKey === group.key"
            title="New RFQs stop receiving these quotes (e.g. the supplier sold the parts)"
            @click="setAutoFill(group.rows.filter(isAutoFillOn), false, group.key)"
          >
            Stop auto-fill
          </v-btn>
        </div>

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

        <div class="grid-scroll">
          <table class="accepted-grid">
            <thead>
              <tr>
                <th style="min-width: 150px;">Part Number</th>
                <th style="min-width: 120px;">Alt P/N</th>
                <th style="min-width: 90px;">Price ($)</th>
                <th style="min-width: 60px;">Qty</th>
                <th style="min-width: 60px;">Cond</th>
                <th style="min-width: 90px;">S/N</th>
                <th style="min-width: 90px;">Cert</th>
                <th style="min-width: 95px;">Tag Date</th>
                <th style="min-width: 90px;">Lead Time</th>
                <th style="min-width: 130px;">Accepted by</th>
                <th style="min-width: 140px;">Accepted at</th>
                <th style="min-width: 90px;" title="RFQ items the quote was written to — on accept, plus new RFQs that picked it up within 7 days">Added to</th>
                <th style="min-width: 150px;" title="New RFQs for this part get the quote automatically for 7 days after accepting — switch it off when the supplier no longer has it">Auto-fill new RFQs</th>
                <th style="width: 72px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in group.rows" :key="row.id">
                <td class="font-weight-medium">{{ row.partNumber }}</td>
                <td>
                  <span v-if="row.alt" class="alt-pn">{{ row.alt }}</span>
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td>{{ row.price != null ? '$' + formatPrice(row.price) : '—' }}</td>
                <td>{{ row.qty ?? '—' }}</td>
                <td>{{ row.condition || '—' }}</td>
                <td>{{ row.serialNumber || '—' }}</td>
                <td>{{ row.certName || '—' }}</td>
                <td :title="row.tagDateRaw ? `In the email: ${row.tagDateRaw}` : ''">
                  {{ row.tagDate ? formatTagDate(row.tagDate) : (row.tagDateRaw || '—') }}
                </td>
                <td>{{ row.leadTime || '—' }}</td>
                <td>
                  <v-chip size="x-small" variant="tonal" prepend-icon="mdi-account-check-outline">
                    {{ row.reviewedByName || 'Unknown' }}
                  </v-chip>
                </td>
                <td class="text-caption">{{ row.reviewedAt ? new Date(row.reviewedAt).toLocaleString() : '—' }}</td>
                <td>
                  <v-chip size="x-small" color="success" variant="tonal">
                    {{ row.appliedCount }} RFQ item{{ row.appliedCount === 1 ? '' : 's' }}
                  </v-chip>
                </td>
                <td style="white-space: nowrap;">
                  <span v-if="isAutoFillExpired(row)" class="text-caption text-medium-emphasis">
                    Ended {{ formatDay(row.autoApplyUntil) }}
                  </span>
                  <template v-else>
                    <v-btn
                      size="x-small"
                      :color="row.autoApplyDisabled ? undefined : 'success'"
                      :variant="row.autoApplyDisabled ? 'outlined' : 'tonal'"
                      :prepend-icon="row.autoApplyDisabled ? 'mdi-autorenew-off' : 'mdi-autorenew'"
                      :loading="busyKey === `row-${row.id}`"
                      :title="row.autoApplyDisabled ? 'Off — click to let new RFQs pick this quote up again' : 'On — click to stop new RFQs picking this quote up'"
                      @click="setAutoFill([row], row.autoApplyDisabled, `row-${row.id}`)"
                    >
                      {{ row.autoApplyDisabled ? 'Off' : 'On' }}
                    </v-btn>
                    <span v-if="!row.autoApplyDisabled" class="text-caption text-medium-emphasis ml-1">until {{ formatDay(row.autoApplyUntil) }}</span>
                  </template>
                </td>
                <td style="white-space: nowrap;">
                  <v-btn
                    icon="mdi-pencil-outline"
                    size="x-small"
                    variant="text"
                    title="Edit — reaches new RFQs; press Re-apply to update RFQs that already have it"
                    @click="openEdit(row)"
                  />
                  <v-btn
                    icon="mdi-sync"
                    size="x-small"
                    variant="text"
                    color="primary"
                    :loading="busyKey === `reapply-row-${row.id}`"
                    title="Re-apply — write this line as it is now to its RFQs (rewrite the quote it added, or add it where missing)"
                    @click="reapply([row], `reapply-row-${row.id}`)"
                  />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <EmailQuotesPager v-model:page="page" v-model:page-size="pageSize" :total-emails="totalEmails" />
    </v-card-text>

    <QuoteEmailViewerDialog
      v-model="showEmail"
      :email-id="emailViewId"
      :initial-attachment="emailViewAttachment"
      :rows="emailViewRows"
      accepted
      @row-updated="patchRow"
    />

    <EmailQuoteEditDialog v-model="showEdit" :row="editRow" accepted @updated="patchRow" />

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="4000">{{ snackText }}</v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
type AcceptedRow = {
  id: number
  messageId?: string
  remark?: string
  shippingCost?: number | null
  shippingPoint?: string
  sellLeadTime?: string
  myNotes?: string
  customCertReferences?: string[]
  emailSubject?: string
  emailDate?: string
  supplierEmail?: string
  supplierNameRaw?: string
  supplierName?: string
  partNumber: string
  alt?: string
  price: number | null
  qty: number | null
  condition?: string
  serialNumber?: string
  certName?: string
  tagDate?: string | null
  tagDateRaw?: string
  leadTime?: string
  emailId?: number | null
  attachmentNames: string[]
  reviewedAt?: string
  reviewedByName?: string
  appliedCount: number
  autoApplyDisabled: boolean
  autoApplyUntil?: string | null
}

const PERIODS = [
  { label: 'Last 7 days', value: 7 },
  { label: 'Last 30 days', value: 30 },
  { label: 'Last 90 days', value: 90 },
  { label: 'All time', value: 0 },
]

/** Page's mailbox dropdown: only lines read from this MailFlow mailbox (null = all, '-' = unknown). */
const props = defineProps<{ mailbox?: string | null }>()
const api = useApi()
const authStore = useAuthStore()
const rows = ref<AcceptedRow[]>([])
const loading = ref(false)
const days = ref(30)
const search = ref('')
// Start on "my accepted quotes" when the user has any; they can clear it to see everyone's.
const reviewer = ref<number | null>(null)
const reviewers = ref<{ id: number; name: string }[]>([])
const reviewerName = computed(() => reviewers.value.find(r => r.id === reviewer.value)?.name)

// Paged by email on the server; reviewer, period and search are applied there too.
const page = ref(1)
const pageSize = ref(20)
const totalEmails = ref(0)
const totalLines = ref(0)
let reviewerDefaulted = false
let loadId = 0

async function load() {
  const id = ++loadId
  loading.value = true
  try {
    const params = new URLSearchParams({ days: String(days.value), page: String(page.value), pageSize: String(pageSize.value) })
    if (reviewer.value != null) params.set('userId', String(reviewer.value))
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (props.mailbox) params.set('mailbox', props.mailbox)
    const res = await api.get<{ items: AcceptedRow[]; page: number; totalEmails: number; totalLines: number; reviewers: { id: number; name: string }[] }>(
      `/pending-supplier-quotes/accepted?${params}`)
    if (id !== loadId) return
    reviewers.value = res.reviewers
    if (!reviewerDefaulted) {
      reviewerDefaulted = true
      const me = authStore.user?.id
      if (me != null && res.reviewers.some(r => r.id === me)) {
        reviewer.value = me // the watcher reloads with the filter
        return
      }
    }
    rows.value = res.items
    totalEmails.value = res.totalEmails
    totalLines.value = res.totalLines
    if (res.page !== page.value) {
      clampedPage = true
      page.value = res.page
    }
  } catch {
    if (id === loadId) rows.value = []
  } finally {
    if (id === loadId) loading.value = false
  }
}

/** Set when load() itself moved `page` — that page is already loaded. */
let clampedPage = false
function reloadFromFirstPage() {
  if (page.value === 1) load()
  else page.value = 1
}
onMounted(load)
watch(page, () => {
  if (clampedPage) clampedPage = false
  else load()
})
watch([days, reviewer, pageSize, () => props.mailbox], reloadFromFirstPage)
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(reloadFromFirstPage, 400)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

// One group per supplier email, newest acceptance first
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
        supplierName: r.supplierName,
        emailId: r.emailId ?? null,
        attachmentNames: r.attachmentNames || [],
        rows: [] as AcceptedRow[],
      })
    }
    map.get(key).rows.push(r)
  }
  return [...map.values()]
})

function formatTagDate(d: string) {
  const [y, m, day] = d.split('-').map(Number)
  return new Date(y!, (m || 1) - 1, day || 1).toLocaleDateString()
}

function formatDay(d?: string | null) {
  return d ? new Date(d).toLocaleDateString() : '—'
}

// ── Auto-fill onto new RFQs (7 days after accepting, unless switched off) ──
const busyKey = ref('')
const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

function isAutoFillExpired(row: AcceptedRow) {
  return !!row.autoApplyUntil && new Date(row.autoApplyUntil).getTime() < Date.now()
}
const isAutoFillOn = (row: AcceptedRow) => !row.autoApplyDisabled && !isAutoFillExpired(row)

async function setAutoFill(targets: AcceptedRow[], enabled: boolean, key: string) {
  if (!targets.length) return
  busyKey.value = key
  try {
    await Promise.all(targets.map(r => api.patch(`/pending-supplier-quotes/${r.id}/auto-apply`, { enabled })))
    for (const r of targets) r.autoApplyDisabled = !enabled
    showSnack(enabled
      ? 'Auto-fill on — new RFQs will pick these quotes up again'
      : `Auto-fill off for ${targets.length} line${targets.length === 1 ? '' : 's'} — new RFQs won't get them`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not change auto-fill', 'error')
  } finally {
    busyKey.value = ''
  }
}

// ── Re-apply: write edited lines to the RFQs again (rewrite the quotes they added, add where missing) ──
async function reapply(targets: AcceptedRow[], key: string) {
  if (!targets.length) return
  busyKey.value = key
  try {
    const res = await api.post<{ lines: number; quotesUpdated: number; quotesAdded: number; failed: { partNumber: string; reason: string }[] }>(
      '/pending-supplier-quotes/reapply', { ids: targets.map(r => r.id) })
    const parts = [
      res.quotesUpdated ? `${res.quotesUpdated} RFQ quote${res.quotesUpdated === 1 ? '' : 's'} updated` : '',
      res.quotesAdded ? `${res.quotesAdded} added` : '',
    ].filter(Boolean)
    if (res.failed.length)
      showSnack(`${parts.length ? parts.join(', ') + ' · ' : ''}${res.failed.map(f => `${f.partNumber}: ${f.reason}`).join(' · ')}`, parts.length ? 'warning' : 'error')
    else showSnack(parts.length ? parts.join(', ') : 'Nothing to write')
    if (res.lines) load() // refresh "Added to"
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not re-apply to RFQs', 'error')
  } finally {
    busyKey.value = ''
  }
}

// ── Excel export: every line the filters select, all pages ──
const exporting = ref(false)
async function exportExcel() {
  exporting.value = true
  try {
    const params = new URLSearchParams({ days: String(days.value) })
    if (reviewer.value != null) params.set('userId', String(reviewer.value))
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (props.mailbox) params.set('mailbox', props.mailbox)
    const all = await api.get<AcceptedRow[]>(`/pending-supplier-quotes/accepted/export?${params}`)
    if (!all.length) {
      showSnack('Nothing to export', 'warning')
      return
    }
    downloadExcel(all.map(r => ({
      'Supplier': r.supplierName || r.supplierNameRaw || '',
      'Supplier Email': r.supplierEmail || '',
      'Email Subject': r.emailSubject || '',
      'Email Date': r.emailDate ? new Date(r.emailDate).toLocaleString() : '',
      'Part Number': r.partNumber,
      'Alt P/N': r.alt || '',
      'Price ($)': r.price ?? '',
      'Qty': r.qty ?? '',
      'Condition': r.condition || '',
      'Cert': r.certName || '',
      'Cert References': (r.customCertReferences || []).join(', '),
      'Tag Date': r.tagDate ? formatTagDate(r.tagDate) : (r.tagDateRaw || ''),
      'Lead Time': r.leadTime || '',
      'Sell Lead Time': r.sellLeadTime || '',
      'Shipping Cost ($)': r.shippingCost ?? '',
      'Shipping Point': r.shippingPoint || '',
      ...Object.fromEntries(QUOTE_DETAIL_FIELDS.map(f => [f.label, (r as any)[f.key] || ''])),
      'Remark': r.remark || '',
      'My Notes': r.myNotes || '',
      'Accepted By': r.reviewedByName || '',
      'Accepted At': r.reviewedAt ? new Date(r.reviewedAt).toLocaleString() : '',
      'Added To (RFQ items)': r.appliedCount,
      'Auto-fill': isAutoFillExpired(r) ? 'Ended' : r.autoApplyDisabled ? 'Off' : 'On',
      'Auto-fill Until': formatDay(r.autoApplyUntil),
    })), `accepted-email-quotes-${new Date().toISOString().slice(0, 10)}`, 'Accepted Quotes')
    showSnack(`Exported ${all.length} line${all.length === 1 ? '' : 's'}`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not export accepted quotes', 'error')
  } finally {
    exporting.value = false
  }
}

// ── Edit (new RFQs; Re-apply pushes it to the RFQs that have it) ──
const showEdit = ref(false)
const editRow = ref<AcceptedRow | null>(null)
function openEdit(row: AcceptedRow) {
  editRow.value = row
  showEdit.value = true
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
</script>

<style scoped>
.email-group {
  border: 1px solid var(--card-border);
  border-radius: 8px;
  padding: 10px 12px;
}
.grid-scroll {
  overflow-x: auto;
}
.accepted-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.accepted-grid th {
  text-align: left;
  font-weight: 600;
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  white-space: nowrap;
}
.accepted-grid td {
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  vertical-align: middle;
}
.alt-pn {
  color: #f59e0b;
  font-weight: 500;
}
</style>
