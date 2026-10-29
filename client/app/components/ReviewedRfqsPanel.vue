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
        <v-btn
          v-if="accepted"
          size="small"
          color="primary"
          variant="flat"
          prepend-icon="mdi-account-star-outline"
          :disabled="!selected.size"
          title="Assign the ticked parts to a PPE (Procurement Part Expert) — every accepted line with the same P/N goes with them"
          @click="openPpe"
        >
          Assign PPE{{ selected.size ? ` (${selected.size})` : '' }}
        </v-btn>
        <v-chip size="small" variant="tonal" :color="accepted ? 'success' : 'error'">
          {{ totalLines }} line{{ totalLines === 1 ? '' : 's' }} · {{ totalEmails }} email{{ totalEmails === 1 ? '' : 's' }}
        </v-chip>
        <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load(true)">Refresh</v-btn>
      </div>

      <div v-if="loading && !rows.length" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" />
      </div>
      <p v-else-if="!groups.length" class="text-caption text-medium-emphasis text-center py-6 mb-0">
        No {{ accepted ? 'accepted' : 'removed' }} RFQ emails{{ reviewerName ? ` by ${reviewerName}` : '' }} in this period.
      </p>

      <!-- Each email renders when it scrolls near the screen, so a 100-email page shows at once -->
      <v-lazy
        v-for="group in groups"
        :key="group.key"
        :min-height="60 + 26 * group.rows.length"
        :options="{ rootMargin: '600px 0px' }"
        transition=""
      >
      <div class="email-group mb-4">
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
            <div v-for="rfq in group.rfqs" :key="rfq.id" class="d-flex align-center">
              <v-chip
                size="small"
                color="success"
                variant="tonal"
                prepend-icon="mdi-file-document-outline"
                :to="`/rfqs/${rfq.id}`"
                title="Open the RFQ that was created"
              >
                {{ rfq.name }}<span v-if="rfq.deadline" class="ml-1 text-medium-emphasis">· {{ fmtDate(rfq.deadline) }}</span>
              </v-chip>
              <v-btn
                icon="mdi-cancel"
                size="x-small"
                variant="text"
                color="deep-purple"
                :title="`No Quote ${rfq.name} — the RFQ is marked No Quote and its lines leave this tab`"
                @click="openNoQuote(rfq)"
              />
            </div>
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
                <th v-if="accepted" style="width: 32px;">
                  <v-checkbox-btn
                    :model-value="selectedIn(group.rows) === group.rows.length"
                    :indeterminate="selectedIn(group.rows) > 0 && selectedIn(group.rows) < group.rows.length"
                    density="compact"
                    @update:model-value="toggleGroup(group.rows)"
                  />
                </th>
                <th style="min-width: 150px;">Part Number</th>
                <th style="min-width: 140px;">Alternatives</th>
                <th style="min-width: 180px;">Description</th>
                <th style="min-width: 60px;">Qty</th>
                <th style="min-width: 60px;">Cond</th>
                <th style="min-width: 80px;">Priority</th>
                <th style="min-width: 160px;">Remark</th>
                <template v-if="accepted">
                  <th style="min-width: 90px;" title="Average supplier quote price for this part on every RFQ (hover for each condition)">Avg Price</th>
                  <th style="min-width: 150px;" title="Customers whose RFQs asked for this part in the last 7 days — total qty">Customers (7 days)</th>
                  <th style="width: 32px;"></th>
                </template>
                <th style="min-width: 140px;">{{ accepted ? 'RFQ' : 'RFQ name' }}</th>
                <th v-if="accepted" style="min-width: 90px;" title="Deadline of the RFQ this part is on">Deadline</th>
                <th v-if="accepted" style="min-width: 110px;" title="PPE — Procurement Part Expert: the user the part is assigned to on Procurement Plus">PPE</th>
                <th v-if="!accepted" style="width: 60px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in group.rows" :key="row.id" :class="{ 'row--selected': selected.has(row.id) }">
                <td v-if="accepted">
                  <v-checkbox-btn :model-value="selected.has(row.id)" density="compact" @update:model-value="toggleRow(row.id)" />
                </td>
                <td class="font-weight-medium">{{ row.partNumber }}</td>
                <td class="text-caption">{{ row.alternatives.join(', ') || '—' }}</td>
                <td class="text-caption">{{ row.description || '—' }}</td>
                <td>{{ row.qty ?? 1 }}</td>
                <td>{{ row.condition || '—' }}</td>
                <td>{{ row.priority || '—' }}</td>
                <td class="text-caption">{{ row.remark || '—' }}</td>
                <template v-if="accepted">
                  <td class="text-no-wrap">
                    <span
                      v-if="ins(row)?.avgPrice != null"
                      class="font-weight-medium"
                      :title="(ins(row)?.avgPriceByCondition ?? []).map(c => `${c.condition}: ${fmtMoney(c.average)} (${c.count})`).join('\n')"
                    >
                      {{ fmtMoney(ins(row)!.avgPrice!) }}
                      <span class="text-caption text-medium-emphasis">({{ ins(row)?.avgPriceCount }})</span>
                    </span>
                    <span v-else class="text-medium-emphasis">{{ insightsWaiting.has(row.partNumber) ? '…' : '—' }}</span>
                  </td>
                  <td class="text-caption">
                    <span v-if="ins(row)?.weekCustomers?.length">
                      <span
                        v-for="(c, i) in ins(row)!.weekCustomers"
                        :key="c.customerId"
                        :title="`${c.customerCode || c.customerName}: qty ${c.qty} in ${c.count} RFQ line${c.count === 1 ? '' : 's'}`"
                      >
                        {{ c.customerCode || c.customerName }} ({{ c.qty }}){{ i < ins(row)!.weekCustomers!.length - 1 ? ' ' : '' }}
                      </span>
                    </span>
                    <span v-else class="text-medium-emphasis">{{ insightsWaiting.has(row.partNumber) ? '…' : '—' }}</span>
                  </td>
                  <td>
                    <v-btn
                      icon="mdi-information-outline"
                      size="x-small"
                      variant="text"
                      color="info"
                      title="Last 25 customers who asked for this part — qty, condition and our price"
                      @click="openPartCustomers(row.partNumber)"
                    />
                  </td>
                </template>
                <td class="text-caption">
                  <NuxtLink v-if="accepted && row.rfqId" :to="`/rfqs/${row.rfqId}`">{{ row.createdRfqName || row.rfqName }}</NuxtLink>
                  <span v-else>{{ row.rfqName }}</span>
                </td>
                <td v-if="accepted" class="text-no-wrap" :class="{ 'text-error font-weight-medium': isOverdue(row.rfqDeadline) }">
                  {{ fmtDate(row.rfqDeadline) || '—' }}
                </td>
                <td v-if="accepted">
                  <v-chip
                    v-if="row.ppeUserName"
                    size="x-small"
                    color="deep-purple"
                    variant="tonal"
                    prepend-icon="mdi-account-star-outline"
                    :title="row.ppeAssignedAt ? `Assigned ${new Date(row.ppeAssignedAt).toLocaleString()}` : undefined"
                  >
                    {{ row.ppeUserName }}
                  </v-chip>
                  <span v-else class="text-medium-emphasis">—</span>
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
      </v-lazy>

      <EmailQuotesPager v-model:page="page" v-model:page-size="pageSize" :total-emails="totalEmails" :page-sizes="[20, 50, 100, 200]" />
    </v-card-text>

    <!-- Floating Assign PPE bar — stays on screen while ticking parts further down the list -->
    <Teleport to="body">
      <v-slide-y-reverse-transition>
        <div v-if="accepted && selected.size && !ppeDialog" class="ppe-float elevation-8">
          <span class="text-body-2 font-weight-medium">
            {{ selected.size }} line{{ selected.size === 1 ? '' : 's' }} · {{ selectedParts.length }} part{{ selectedParts.length === 1 ? '' : 's' }}
          </span>
          <v-btn size="small" variant="text" prepend-icon="mdi-close" @click="selected = new Set()">Clear</v-btn>
          <v-btn
            size="small"
            color="primary"
            variant="flat"
            prepend-icon="mdi-account-star-outline"
            title="Assign the ticked parts to a PPE (Procurement Part Expert) — every accepted line with the same P/N goes with them"
            @click="openPpe"
          >
            Assign PPE ({{ selected.size }})
          </v-btn>
        </div>
      </v-slide-y-reverse-transition>
    </Teleport>

    <!-- Assign PPE (accepted tab) -->
    <v-dialog v-model="ppeDialog" max-width="440">
      <v-card>
        <v-card-title class="text-subtitle-1">Assign PPE</v-card-title>
        <v-card-text>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ selectedParts.length }} part{{ selectedParts.length === 1 ? '' : 's' }}: {{ selectedParts.slice(0, 6).join(', ') }}{{ selectedParts.length > 6 ? '…' : '' }}.
            Every accepted line with the same part number (any RFQ) goes to this user, and the part shows on their Procurement Plus.
          </p>
          <v-autocomplete
            v-model="ppeUser"
            :items="ppeUsers"
            item-title="name"
            item-value="id"
            label="PPE (Procurement Part Expert)"
            density="compact"
            variant="outlined"
            :loading="ppeUsersLoading"
            no-data-text="No user has Procurement Plus in Menu Access"
            hide-details
            autofocus
          />
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" color="error" :loading="ppeSaving === 'clear'" :disabled="!!ppeSaving" @click="assignPpe(null)">Unassign</v-btn>
          <v-spacer />
          <v-btn variant="text" :disabled="!!ppeSaving" @click="ppeDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :disabled="!ppeUser || !!ppeSaving" :loading="ppeSaving === 'set'" @click="assignPpe(ppeUser)">Assign</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- No Quote one created RFQ (accepted tab) — same rule as the RFQ page: non-admins wait for an admin -->
    <v-dialog v-model="noQuoteDialog" max-width="480">
      <v-card>
        <v-card-title class="d-flex align-center text-subtitle-1">
          <v-icon icon="mdi-cancel" color="deep-purple" class="mr-2" />
          No Quote {{ noQuoteRfq?.name }}?
        </v-card-title>
        <v-card-text>
          <p class="text-caption text-medium-emphasis mb-3">
            The RFQ is marked <strong>No Quote</strong>{{ authStore.isAdmin ? '' : ' (after an admin confirms)' }}
            and its parts no longer show on this tab.
          </p>
          <v-textarea
            v-model="noQuoteReason"
            label="Reason *"
            placeholder="Why is this RFQ not being quoted?"
            variant="outlined"
            density="compact"
            rows="3"
            hide-details
            auto-grow
            autofocus
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" :disabled="noQuoteSaving" @click="noQuoteDialog = false">Cancel</v-btn>
          <v-btn
            color="deep-purple"
            variant="flat"
            :loading="noQuoteSaving"
            :disabled="!noQuoteReason.trim()"
            @click="confirmNoQuote"
          >
            Confirm No Quote
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PartCustomersDialog v-model="showPartCustomers" :part-number="partCustomersPn" />

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
  /** Accepted: the created RFQ's deadline. */
  rfqDeadline?: string | null
  ppeUserId?: number | null
  ppeUserName?: string | null
  ppeAssignedAt?: string | null
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

// Accepted: average price and this week's customers — loaded after the list, so the list never waits for them.
const { insights, waiting: insightsWaiting, load: loadInsights } = usePendingRfqInsights({ weekCustomers: true })
function ins(row: ReviewedRow) {
  return insights.value.get(row.partNumber)
}

const page = ref(1)
const pageSize = ref(100)
const totalEmails = ref(0)
const totalLines = ref(0)
let reviewerDefaulted = false
let loadId = 0
/** Set when load() itself moved `page` — that page is already loaded. */
let clampedPage = false

/** `fresh`: the Refresh button — the part insights are fetched again too. */
async function load(fresh = false) {
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
    const ids = new Set(res.items.map(r => r.id))
    selected.value = new Set([...selected.value].filter(x => ids.has(x)))
    totalEmails.value = res.totalEmails
    totalLines.value = res.totalLines
    if (res.page !== page.value) {
      clampedPage = true
      page.value = res.page
    }
    if (accepted.value) loadInsights(res.items.map(r => r.partNumber), fresh === true)
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

// One group per customer email, in the server's order: Accepted newest email first, Removed most recently removed first
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
        rfqs: [] as { id: number; name: string; deadline?: string | null }[],
        rows: [] as ReviewedRow[],
      })
    }
    const g = map.get(key)
    g.rows.push(r)
    if (r.rfqId && !g.rfqs.some((x: { id: number }) => x.id === r.rfqId))
      g.rfqs.push({ id: r.rfqId, name: r.createdRfqName || r.rfqName, deadline: r.rfqDeadline })
  }
  return [...map.values()]
})

/** Deadline day already gone (today still counts as on time). */
function isOverdue(v?: string | null) {
  if (!v) return false
  const d = new Date(v)
  d.setHours(23, 59, 59, 999)
  return d.getTime() < Date.now()
}

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

// ── PPE (accepted tab): tick lines → assign their parts to a user ──
const selected = ref(new Set<number>())
function toggleRow(id: number) {
  const next = new Set(selected.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  selected.value = next
}
function selectedIn(list: ReviewedRow[]) {
  return list.filter(r => selected.value.has(r.id)).length
}
function toggleGroup(list: ReviewedRow[]) {
  const next = new Set(selected.value)
  const all = selectedIn(list) === list.length
  for (const r of list) {
    if (all) next.delete(r.id)
    else next.add(r.id)
  }
  selected.value = next
}
const selectedParts = computed(() =>
  [...new Set(rows.value.filter(r => selected.value.has(r.id)).map(r => r.partNumber.trim()))])

const ppeDialog = ref(false)
const ppeUser = ref<number | null>(null)
const ppeUsers = ref<{ id: number; name: string }[]>([])
const ppeUsersLoading = ref(false)
const ppeSaving = ref<'' | 'set' | 'clear'>('')
async function openPpe() {
  ppeUser.value = null
  ppeDialog.value = true
  if (ppeUsers.value.length) return
  ppeUsersLoading.value = true
  try {
    ppeUsers.value = await api.get<{ id: number; name: string }[]>('/pending-rfqs/ppe-users')
  } catch {
    ppeUsers.value = []
  } finally {
    ppeUsersLoading.value = false
  }
}
async function assignPpe(userId: number | null) {
  ppeSaving.value = userId == null ? 'clear' : 'set'
  try {
    const res = await api.post<{ lines: number; parts: number; userName?: string; commissionsReset: number }>('/pending-rfqs/ppe', { ids: [...selected.value], userId })
    ppeDialog.value = false
    selected.value = new Set()
    await load()
    showSnack(userId == null
      ? `PPE removed from ${res.lines} line${res.lines === 1 ? '' : 's'}`
      : `${res.parts} part${res.parts === 1 ? '' : 's'} (${res.lines} line${res.lines === 1 ? '' : 's'}) assigned to ${res.userName}`
        + (res.commissionsReset ? ` · old commission taken off ${res.commissionsReset} unquoted RFQ quote${res.commissionsReset === 1 ? '' : 's'}` : ''))
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not assign the PPE', 'error')
  } finally {
    ppeSaving.value = ''
  }
}

// ── No Quote (accepted tab): the RFQ goes No Quote and its lines drop off this tab ──
const noQuoteDialog = ref(false)
const noQuoteRfq = ref<{ id: number; name: string } | null>(null)
const noQuoteReason = ref('')
const noQuoteSaving = ref(false)
function openNoQuote(rfq: { id: number; name: string }) {
  noQuoteRfq.value = rfq
  noQuoteReason.value = ''
  noQuoteDialog.value = true
}
async function confirmNoQuote() {
  const rfq = noQuoteRfq.value
  if (!rfq || !noQuoteReason.value.trim()) return
  noQuoteSaving.value = true
  try {
    await api.patch(`/rfqs/${rfq.id}/status`, { status: 'No Quote', noQuoteReason: noQuoteReason.value.trim() })
    noQuoteDialog.value = false
    await load()
    showSnack(authStore.isAdmin
      ? `${rfq.name} marked No Quote`
      : `No Quote for ${rfq.name} sent to an admin — hidden from Accepted`)
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not mark the RFQ No Quote', 'error')
  } finally {
    noQuoteSaving.value = false
  }
}

// ── Last customers for a part (info icon, accepted tab) ──
const showPartCustomers = ref(false)
const partCustomersPn = ref('')
function openPartCustomers(partNumber: string) {
  partCustomersPn.value = partNumber
  showPartCustomers.value = true
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
.row--selected td {
  background: rgba(var(--v-theme-primary), 0.06);
}
.ppe-float {
  position: fixed;
  bottom: 24px;
  left: 0;
  right: 0;
  margin: 0 auto;
  width: fit-content;
  z-index: 1005;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px 8px 16px;
  border-radius: 999px;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-primary), 0.4);
  white-space: nowrap;
}
</style>
