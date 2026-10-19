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
          label="Removed by"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-account-remove-outline"
          style="max-width: 240px; min-width: 200px;"
        />
        <v-select
          v-model="days"
          :items="PERIODS"
          item-title="label"
          item-value="value"
          label="Removed in"
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
        <v-chip size="small" variant="tonal" color="error">
          {{ totalLines }} line{{ totalLines === 1 ? '' : 's' }} · {{ totalEmails }} email{{ totalEmails === 1 ? '' : 's' }}
        </v-chip>
        <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
      </div>

      <div v-if="loading && !rows.length" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" />
      </div>
      <p v-else-if="!groups.length" class="text-caption text-medium-emphasis text-center py-6 mb-0">
        No removed email quotes{{ reviewerName ? ` by ${reviewerName}` : '' }} in this period.
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
            color="success"
            variant="tonal"
            prepend-icon="mdi-restore"
            :loading="busyKey === group.key"
            title="Move every line of this email back to Pending"
            @click="restore(group.rows, group.key)"
          >
            Restore all
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
          <table class="removed-grid">
            <thead>
              <tr>
                <th style="min-width: 150px;">Part Number</th>
                <th style="min-width: 120px;">Alt P/N</th>
                <th style="min-width: 90px;">Price ($)</th>
                <th style="min-width: 60px;">Qty</th>
                <th style="min-width: 60px;">Cond</th>
                <th style="min-width: 90px;">Cert</th>
                <th style="min-width: 95px;">Tag Date</th>
                <th style="min-width: 90px;">Lead Time</th>
                <th style="min-width: 130px;">Removed by</th>
                <th style="min-width: 140px;">Removed at</th>
                <th style="width: 100px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in group.rows" :key="row.id">
                <td class="font-weight-medium">{{ row.partNumber }}</td>
                <td>
                  <span v-if="row.alt" class="alt-pn">{{ row.alt }}</span>
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td>{{ row.price != null ? '$' + formatPrice(row.price) : (row.priceRaw || '—') }}</td>
                <td>{{ row.qty ?? '—' }}</td>
                <td>{{ row.condition || '—' }}</td>
                <td>{{ row.certName || '—' }}</td>
                <td :title="row.tagDateRaw ? `In the email: ${row.tagDateRaw}` : ''">
                  {{ row.tagDate ? formatTagDate(row.tagDate) : (row.tagDateRaw || '—') }}
                </td>
                <td>{{ row.leadTime || '—' }}</td>
                <td>
                  <v-chip size="x-small" variant="tonal" prepend-icon="mdi-account-remove-outline">
                    {{ row.reviewedByName || 'Unknown' }}
                  </v-chip>
                </td>
                <td class="text-caption">{{ row.reviewedAt ? new Date(row.reviewedAt).toLocaleString() : '—' }}</td>
                <td>
                  <v-btn
                    size="x-small"
                    color="success"
                    variant="tonal"
                    prepend-icon="mdi-restore"
                    :loading="busyKey === `row-${row.id}`"
                    title="Move this line back to Pending"
                    @click="restore([row], `row-${row.id}`)"
                  >
                    Restore
                  </v-btn>
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
    />

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="4000">{{ snackText }}</v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
type RemovedRow = {
  id: number
  messageId?: string
  emailSubject?: string
  emailDate?: string
  supplierEmail?: string
  supplierNameRaw?: string
  supplierName?: string
  partNumber: string
  alt?: string
  price: number | null
  priceRaw?: string
  qty: number | null
  condition?: string
  certName?: string
  tagDate?: string | null
  tagDateRaw?: string
  leadTime?: string
  emailId?: number | null
  attachmentNames: string[]
  reviewedAt?: string
  reviewedByName?: string
}

/** Page's mailbox dropdown: only lines read from this MailFlow mailbox (null = all, '-' = unknown). */
const props = defineProps<{ mailbox?: string | null }>()
const emit = defineEmits<{ restored: [count: number] }>()

const PERIODS = [
  { label: 'Last 7 days', value: 7 },
  { label: 'Last 30 days', value: 30 },
  { label: 'Last 90 days', value: 90 },
  { label: 'All time', value: 0 },
]

const api = useApi()
const authStore = useAuthStore()
const rows = ref<RemovedRow[]>([])
const loading = ref(false)
const days = ref(30)
const search = ref('')
// Start on "lines I removed" when the user has any; they can clear it to see everyone's.
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
    const res = await api.get<{ items: RemovedRow[]; page: number; totalEmails: number; totalLines: number; reviewers: { id: number; name: string }[] }>(
      `/pending-supplier-quotes/removed?${params}`)
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

// One group per supplier email, most recently removed first
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
        rows: [] as RemovedRow[],
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

// ── Restore (back to Pending) ──
const busyKey = ref('')
const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

async function restore(targets: RemovedRow[], key: string) {
  if (!targets.length) return
  busyKey.value = key
  try {
    const ids = targets.map(r => r.id)
    const res = await api.post<{ restored: number }>('/pending-supplier-quotes/restore', { ids })
    await load() // refills this page from the next one
    showSnack(`${res.restored} line${res.restored === 1 ? '' : 's'} moved back to Pending`)
    emit('restored', res.restored)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not restore', 'error')
  } finally {
    busyKey.value = ''
  }
}

// ── Source email viewer (read only — removed lines are not editable) ──
const showEmail = ref(false)
const emailViewId = ref<number | null>(null)
const emailViewAttachment = ref<string | null>(null)
function openEmail(group: { emailId: number }, attachmentName: string | null = null) {
  emailViewId.value = group.emailId
  emailViewAttachment.value = attachmentName
  showEmail.value = true
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
.removed-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.removed-grid th {
  text-align: left;
  font-weight: 600;
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  white-space: nowrap;
}
.removed-grid td {
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  vertical-align: middle;
}
.alt-pn {
  color: #f59e0b;
  font-weight: 500;
}
</style>
