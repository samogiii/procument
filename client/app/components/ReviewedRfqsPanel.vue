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
          :label="accepted ? 'Accepted by' : 'Removed by'"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          :prepend-inner-icon="accepted ? 'mdi-account-check-outline' : 'mdi-account-remove-outline'"
          style="max-width: 240px; min-width: 200px;"
        />
        <v-select
          v-model="days"
          :items="PERIODS"
          item-title="label"
          item-value="value"
          :label="accepted ? 'Accepted in' : 'Removed in'"
          density="compact"
          variant="outlined"
          hide-details
          style="max-width: 180px; min-width: 160px;"
        />
        <v-text-field
          v-model="search"
          label="Search part, RFQ name, customer or subject"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          style="max-width: 340px; min-width: 220px;"
        />
        <v-spacer />
        <v-chip size="small" variant="tonal" :color="accepted ? 'success' : 'error'">
          {{ totalLines }} line{{ totalLines === 1 ? '' : 's' }} · {{ totalEmails }} email{{ totalEmails === 1 ? '' : 's' }}
        </v-chip>
        <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
      </div>

      <div v-if="loading && !rows.length" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" />
      </div>
      <p v-else-if="!groups.length" class="text-caption text-medium-emphasis text-center py-6 mb-0">
        No {{ accepted ? 'accepted' : 'removed' }} RFQ emails{{ reviewerName ? ` by ${reviewerName}` : '' }} in this period.
      </p>

      <div v-for="group in groups" :key="group.key" class="email-group mb-4">
        <div class="d-flex align-center flex-wrap gap-2 mb-2">
          <v-chip size="small" color="primary" variant="tonal" prepend-icon="mdi-account-tie-outline">
            {{ group.customerName || group.customerNameRaw || group.customerEmail || 'No customer' }}
          </v-chip>
          <div class="text-caption text-medium-emphasis">
            <span v-if="group.customerEmail">{{ group.customerEmail }} · </span>
            <strong>{{ group.subject || '(no subject)' }}</strong>
            <span v-if="group.date"> · {{ new Date(group.date).toLocaleString() }}</span>
          </div>
          <template v-if="accepted">
            <v-chip
              v-for="rfq in group.rfqs"
              :key="rfq.id"
              size="small"
              color="success"
              variant="tonal"
              prepend-icon="mdi-file-document-outline"
              :to="`/rfqs/${rfq.id}`"
              title="Open the RFQ that was created"
            >
              {{ rfq.name }}
            </v-chip>
          </template>
          <span class="text-caption text-medium-emphasis">
            {{ accepted ? 'Accepted' : 'Removed' }} by {{ group.reviewedByName || '—' }}
            <span v-if="group.reviewedAt"> · {{ new Date(group.reviewedAt).toLocaleString() }}</span>
          </span>
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
            v-if="!accepted"
            size="small"
            color="primary"
            variant="text"
            prepend-icon="mdi-restore"
            :loading="busyKey === group.key"
            @click="restore(group.rows, group.key)"
          >
            Restore
          </v-btn>
        </div>

        <div class="grid-scroll">
          <table class="reviewed-grid">
            <thead>
              <tr>
                <th style="min-width: 150px;">Part Number</th>
                <th style="min-width: 140px;">Alternatives</th>
                <th style="min-width: 180px;">Description</th>
                <th style="min-width: 60px;">Qty</th>
                <th style="min-width: 60px;">Cond</th>
                <th style="min-width: 80px;">Priority</th>
                <th style="min-width: 160px;">Remark</th>
                <th style="min-width: 140px;">{{ accepted ? 'RFQ' : 'RFQ name' }}</th>
                <th v-if="!accepted" style="width: 60px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in group.rows" :key="row.id">
                <td class="font-weight-medium">{{ row.partNumber }}</td>
                <td class="text-caption">{{ row.alternatives.join(', ') || '—' }}</td>
                <td class="text-caption">{{ row.description || '—' }}</td>
                <td>{{ row.qty ?? 1 }}</td>
                <td>{{ row.condition || '—' }}</td>
                <td>{{ row.priority || '—' }}</td>
                <td class="text-caption">{{ row.remark || '—' }}</td>
                <td class="text-caption">
                  <NuxtLink v-if="accepted && row.rfqId" :to="`/rfqs/${row.rfqId}`">{{ row.createdRfqName || row.rfqName }}</NuxtLink>
                  <span v-else>{{ row.rfqName }}</span>
                </td>
                <td v-if="!accepted" class="text-right">
                  <v-btn
                    icon="mdi-restore"
                    size="x-small"
                    variant="text"
                    title="Back to Pending"
                    :loading="busyKey === `row-${row.id}`"
                    @click="restore([row], `row-${row.id}`)"
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
      api-base="/pending-rfqs"
      :email-id="emailViewId"
      :initial-attachment="emailViewAttachment"
    />

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="4000">{{ snackText }}</v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
/** Email RFQs: the Accepted tab (with links to the RFQs that were created) or the Removed tab (with Restore). */
type ReviewedRow = {
  id: number
  messageId?: string
  emailSubject?: string
  emailDate?: string
  customerEmail?: string
  customerNameRaw?: string
  customerName?: string
  rfqName: string
  partNumber: string
  description?: string
  qty: number | null
  condition?: string
  priority?: string
  alternatives: string[]
  remark?: string
  emailId?: number | null
  reviewedAt?: string
  reviewedByName?: string
  rfqId?: number | null
  createdRfqName?: string
}

const props = defineProps<{ mode: 'accepted' | 'removed'; mailbox?: string | null }>()
const emit = defineEmits<{ (e: 'restored', count: number): void }>()
const accepted = computed(() => props.mode === 'accepted')

const PERIODS = [
  { label: 'Last 7 days', value: 7 },
  { label: 'Last 30 days', value: 30 },
  { label: 'Last 90 days', value: 90 },
  { label: 'All time', value: 0 },
]

const api = useApi()
const authStore = useAuthStore()
const rows = ref<ReviewedRow[]>([])
const loading = ref(false)
const days = ref(30)
const search = ref('')
// Start on "lines I reviewed" when the user has any; they can clear it to see everyone's.
const reviewer = ref<number | null>(null)
const reviewers = ref<{ id: number; name: string }[]>([])
const reviewerName = computed(() => reviewers.value.find(r => r.id === reviewer.value)?.name)

const page = ref(1)
const pageSize = ref(20)
const totalEmails = ref(0)
const totalLines = ref(0)
let reviewerDefaulted = false
let loadId = 0
/** Set when load() itself moved `page` — that page is already loaded. */
let clampedPage = false

async function load() {
  const id = ++loadId
  loading.value = true
  try {
    const params = new URLSearchParams({ days: String(days.value), page: String(page.value), pageSize: String(pageSize.value) })
    if (reviewer.value != null) params.set('userId', String(reviewer.value))
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (props.mailbox) params.set('mailbox', props.mailbox)
    const res = await api.get<{ items: ReviewedRow[]; page: number; totalEmails: number; totalLines: number; reviewers: { id: number; name: string }[] }>(
      `/pending-rfqs/${props.mode}?${params}`)
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

// One group per customer email, most recently reviewed first
const groups = computed(() => {
  const map = new Map<string, any>()
  for (const r of rows.value) {
    const key = r.messageId || `${r.customerEmail}|${r.emailSubject}|${r.emailDate}`
    if (!map.has(key)) {
      map.set(key, {
        key,
        subject: r.emailSubject,
        date: r.emailDate,
        customerEmail: r.customerEmail,
        customerNameRaw: r.customerNameRaw,
        customerName: r.customerName,
        emailId: r.emailId ?? null,
        reviewedAt: r.reviewedAt,
        reviewedByName: r.reviewedByName,
        rfqs: [] as { id: number; name: string }[],
        rows: [] as ReviewedRow[],
      })
    }
    const g = map.get(key)
    g.rows.push(r)
    if (r.rfqId && !g.rfqs.some((x: { id: number }) => x.id === r.rfqId))
      g.rfqs.push({ id: r.rfqId, name: r.createdRfqName || r.rfqName })
  }
  return [...map.values()]
})

// ── Restore (Removed tab: back to Pending) ──
const busyKey = ref('')
const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}
async function restore(targets: ReviewedRow[], key: string) {
  if (!targets.length) return
  busyKey.value = key
  try {
    const res = await api.post<{ restored: number }>('/pending-rfqs/restore', { ids: targets.map(r => r.id) })
    await load()
    showSnack(`${res.restored} line${res.restored === 1 ? '' : 's'} moved back to Pending`)
    emit('restored', res.restored)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not restore', 'error')
  } finally {
    busyKey.value = ''
  }
}

// ── Source email viewer ──
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
.reviewed-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.reviewed-grid th {
  text-align: left;
  font-weight: 600;
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  white-space: nowrap;
}
.reviewed-grid td {
  padding: 4px 8px;
  border-bottom: 1px solid var(--card-border);
  vertical-align: middle;
}
</style>
