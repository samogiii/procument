<template>
  <v-card class="glass-card mb-4">
    <v-card-title class="d-flex align-center flex-wrap gap-2 pa-4">
      <v-icon icon="mdi-email-arrow-right-outline" color="primary" />
      <span class="text-subtitle-1 font-weight-bold">Customer RFQs from Email</span>
      <v-chip size="small" :color="totalLines ? 'warning' : 'grey'" variant="tonal">
        {{ totalLines }} {{ searchTerm ? 'found' : 'pending' }} · {{ totalEmails }} email{{ totalEmails === 1 ? '' : 's' }}
      </v-chip>
      <v-spacer />
      <v-text-field
        v-model="search"
        placeholder="Search part, RFQ name, customer"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        prepend-inner-icon="mdi-magnify"
        title="Searches every page"
        style="max-width: 300px; min-width: 200px;"
      />
      <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
      <v-btn
        size="small"
        color="error"
        variant="tonal"
        prepend-icon="mdi-delete-sweep-outline"
        :disabled="!pendingTotal || loading"
        title="Remove every pending line on every page"
        @click="askRemoveAll"
      >
        Remove all
      </v-btn>
    </v-card-title>

    <v-card-text class="pt-0">
      <p v-if="!loading && !rows.length" class="text-caption text-medium-emphasis text-center py-4 mb-0">
        <template v-if="searchTerm">No pending lines for "{{ searchTerm }}".</template>
        <template v-else>No pending RFQ emails. MailFlow's CSV Export sends new ones here automatically.</template>
      </p>

      <div v-for="group in groups" :key="group.key" class="email-group mb-4">
        <!-- Customer + email -->
        <div class="d-flex align-center flex-wrap gap-2 mb-2">
          <v-autocomplete
            :model-value="group.customerId"
            :items="customerOptionsFor(group)"
            :item-title="customerTitle"
            item-value="id"
            label="Customer"
            density="compact"
            variant="outlined"
            hide-details
            style="max-width: 300px; min-width: 240px;"
            :color="group.customerId ? undefined : 'warning'"
            :loading="customerSearchLoading"
            no-filter
            @update:search="searchCustomers"
            @update:model-value="(id: number | null) => setGroupCustomer(group, id)"
          />
          <v-chip v-if="!group.customerId" size="small" color="warning" variant="tonal" prepend-icon="mdi-alert">
            No customer found for "{{ group.customerNameRaw || group.customerEmail || 'this email' }}" — pick one
          </v-chip>
          <v-chip v-if="group.mailbox" size="small" variant="outlined" prepend-icon="mdi-inbox-arrow-down-outline" :title="`Read from the mailbox ${group.mailbox}`">
            {{ group.mailboxLabel || group.mailbox }}
          </v-chip>
          <div class="text-caption text-medium-emphasis">
            <span v-if="group.customerEmail">{{ group.customerEmail }} · </span>
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
            prepend-icon="mdi-file-document-plus-outline"
            :disabled="!group.customerId"
            :title="group.customerId ? 'Create the RFQ for this customer' : 'Pick a customer first'"
            :loading="busyKey === group.key"
            @click="accept(acceptIds(group), group.key)"
          >
            {{ selectedIn(group).length ? `Create RFQ (${selectedIn(group).length} selected)` : 'Create RFQ' }}
          </v-btn>
          <v-btn size="small" color="error" variant="text" prepend-icon="mdi-delete-outline" @click="askRemove(group.rows.map((r: PendingRfqRow) => r.id))">
            Remove
          </v-btn>
        </div>

        <!-- The RFQ that will be created -->
        <div class="d-flex align-center flex-wrap gap-2 mb-2">
          <v-text-field
            :model-value="group.rfqName"
            label="RFQ name"
            density="compact"
            variant="outlined"
            hide-details
            style="max-width: 320px; min-width: 220px;"
            :color="group.rfqNameTaken ? 'error' : undefined"
            @change="(e: Event) => saveGroup(group, { rfqName: (e.target as HTMLInputElement).value })"
          />
          <v-text-field
            :model-value="group.sameDeadline"
            type="date"
            :label="group.sameDeadline ? 'Deadline' : 'Deadline (all parts)'"
            density="compact"
            variant="outlined"
            hide-details
            style="max-width: 190px;"
            :title="group.sameDeadline
              ? (group.deadlineRaw ? `In the email: ${group.deadlineRaw}` : 'Not in the email — 3 days after it arrived')
              : 'The parts have different deadlines — setting one here gives every part this deadline'"
            @change="(e: Event) => saveGroup(group, { deadline: (e.target as HTMLInputElement).value || null })"
          />
          <span v-if="group.mixedNames" class="text-caption text-medium-emphasis">
            Lines carry different RFQ names — each name becomes its own RFQ.
          </span>
        </div>

        <!-- One RFQ per name + deadline: ABA(1), ABA(2)... in deadline order, like the RFQ bulk import -->
        <div v-if="group.rfqs.length > 1 || group.rfqNameTaken" class="d-flex align-center flex-wrap gap-1 mb-2">
          <span class="text-caption text-medium-emphasis mr-1">
            {{ group.rfqs.length > 1 ? `Parts have different deadlines — creates ${group.rfqs.length} RFQs:` : 'Creates:' }}
          </span>
          <v-chip
            v-for="rfq in group.rfqs"
            :key="rfq.name"
            size="small"
            variant="tonal"
            :color="rfq.taken ? 'error' : 'primary'"
            :prepend-icon="rfq.taken ? 'mdi-alert-circle-outline' : 'mdi-file-document-outline'"
            :title="rfq.taken ? `An RFQ named ${rfq.name} already exists — rename it` : undefined"
          >
            <strong>{{ rfq.name }}</strong>&nbsp;· {{ fmtDate(rfq.deadline) }} · {{ rfq.count }} part{{ rfq.count === 1 ? '' : 's' }}
          </v-chip>
          <v-chip v-if="group.rfqNameTaken" size="small" color="error" variant="text">
            Name already exists — rename it
          </v-chip>
        </div>

        <!-- Files attached to the email — click to read next to the email -->
        <div v-if="group.attachmentNames.length" class="d-flex flex-wrap align-center gap-1 mb-2">
          <v-icon icon="mdi-paperclip" size="16" class="text-medium-emphasis" />
          <v-chip
            v-for="name in group.attachmentNames"
            :key="name"
            size="small"
            variant="outlined"
            :prepend-icon="/\.pdf$/i.test(name) ? 'mdi-file-pdf-box' : (/\.(xlsx?|csv)$/i.test(name) ? 'mdi-file-excel-outline' : 'mdi-file-outline')"
            :color="/\.pdf$/i.test(name) ? 'red' : (/\.(xlsx?|csv)$/i.test(name) ? 'green' : undefined)"
            @click="openEmail(group, name)"
          >
            {{ name }}
          </v-chip>
        </div>

        <div class="pending-grid-scroll">
          <table class="pending-grid">
            <thead>
              <tr>
                <th style="width: 32px;">
                  <v-checkbox-btn
                    density="compact"
                    :model-value="group.rows.every((r: PendingRfqRow) => selected.has(r.id))"
                    :indeterminate="selectedIn(group).length > 0 && selectedIn(group).length < group.rows.length"
                    @update:model-value="(on: boolean) => toggleGroup(group, on)"
                  />
                </th>
                <th style="min-width: 150px;">Part Number</th>
                <th style="min-width: 140px;">Alternatives</th>
                <th style="min-width: 180px;">Description</th>
                <th style="min-width: 60px;">Qty</th>
                <th style="min-width: 60px;">Cond</th>
                <th style="min-width: 80px;">Priority</th>
                <th style="min-width: 160px;">Remark</th>
                <th style="width: 130px;">Deadline</th>
                <th v-if="group.mixedNames || group.rfqs.length > 1" style="min-width: 140px;">RFQ</th>
                <th style="width: 80px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in group.rows" :key="row.id">
                <td>
                  <v-checkbox-btn density="compact" :model-value="selected.has(row.id)" @update:model-value="(on: boolean) => toggleRow(row.id, on)" />
                </td>
                <td class="font-weight-medium">{{ row.partNumber }}</td>
                <td>
                  <div v-if="row.alternatives.length" class="d-flex flex-wrap gap-1">
                    <v-chip v-for="a in row.alternatives" :key="a" size="x-small" variant="tonal" color="amber-darken-2">{{ a }}</v-chip>
                  </div>
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td class="text-caption">{{ row.description || '—' }}</td>
                <td>{{ row.qty ?? 1 }}</td>
                <td>{{ row.condition || '—' }}</td>
                <td>{{ row.priority || '—' }}</td>
                <td class="text-caption">{{ row.remark || '—' }}</td>
                <td>
                  <input
                    type="date"
                    class="row-date"
                    :value="toDateInput(row.plannedDeadline)"
                    :title="row.deadline ? (row.deadlineRaw ? `In the email: ${row.deadlineRaw}` : '') : 'Not in the email — 3 days after it arrived'"
                    @change="(e: Event) => saveRowDeadline(row, (e.target as HTMLInputElement).value)"
                  >
                </td>
                <td v-if="group.mixedNames || group.rfqs.length > 1" class="text-caption" :class="{ 'text-error': row.rfqNameTaken }">
                  {{ row.plannedRfqName || row.rfqName }}
                </td>
                <td class="text-right" style="white-space: nowrap;">
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

    <!-- Edit one line -->
    <v-dialog v-model="showEdit" max-width="560">
      <v-card v-if="editForm">
        <v-card-title class="pa-4">Edit RFQ line</v-card-title>
        <v-card-text class="pt-0">
          <v-row dense>
            <v-col cols="12" sm="6"><v-text-field v-model="editForm.partNumber" label="Part number *" density="compact" variant="outlined" /></v-col>
            <v-col cols="12" sm="6">
              <v-combobox v-model="editForm.alternatives" label="Alternatives" multiple chips closable-chips density="compact" variant="outlined" hide-details />
            </v-col>
            <v-col cols="12"><v-text-field v-model="editForm.description" label="Description" density="compact" variant="outlined" /></v-col>
            <v-col cols="4"><v-text-field v-model.number="editForm.qty" type="number" min="0" label="Qty" density="compact" variant="outlined" /></v-col>
            <v-col cols="4"><v-text-field v-model="editForm.condition" label="Condition" density="compact" variant="outlined" /></v-col>
            <v-col cols="4"><v-text-field v-model="editForm.priority" label="Priority" density="compact" variant="outlined" /></v-col>
            <v-col cols="12"><v-textarea v-model="editForm.remark" label="Remark" rows="2" auto-grow density="compact" variant="outlined" /></v-col>
            <v-col cols="12" sm="8"><v-text-field v-model="editForm.rfqName" label="RFQ name *" density="compact" variant="outlined" /></v-col>
            <v-col cols="12" sm="4"><v-text-field v-model="editForm.deadline" type="date" label="Deadline" density="compact" variant="outlined" /></v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="showEdit = false">Cancel</v-btn>
          <v-btn color="primary" :loading="savingEdit" :disabled="!editForm.partNumber?.trim() || !editForm.rfqName?.trim()" @click="saveEdit">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <QuoteEmailViewerDialog
      v-model="showEmail"
      api-base="/pending-rfqs"
      edit-title="RFQ lines"
      :email-id="emailViewId"
      :initial-attachment="emailViewAttachment"
      :rows="emailViewRows"
    >
      <template #edit="{ row }">
        <EmailRfqEditForm :row="row" @updated="onViewerRowUpdated" />
      </template>
    </QuoteEmailViewerDialog>

    <ConfirmDialog
      v-model="showConfirmRemove"
      title="Remove RFQ lines?"
      :message="`Remove ${removeAll ? `all ${pendingTotal}` : removeIds.length} pending line(s)${removeAll ? ' on every page' : ''}? No RFQ is created for them, and these emails will never be imported again.`"
      confirm-text="Remove"
      confirm-color="error"
      @confirm="doRemove"
    />

    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="6000">{{ snackText }}</v-snackbar>
  </v-card>
</template>

<script setup lang="ts">
type PendingRfqRow = {
  id: number
  messageId?: string
  emailSubject?: string
  emailDate?: string
  customerEmail?: string
  customerNameRaw?: string
  customerId: number | null
  mailbox?: string | null
  mailboxLabel?: string | null
  customerName?: string
  customerCode?: string
  rfqName: string
  deadline?: string | null
  deadlineRaw?: string
  partNumber: string
  description?: string
  qty: number | null
  condition?: string
  priority?: string
  alternatives: string[]
  remark?: string
  emailId?: number | null
  attachmentNames: string[]
  /** RFQ this line lands in — ABA(1), ABA(2)... when the email's parts carry different deadlines. */
  plannedRfqName?: string | null
  /** The line's deadline, or 3 days after the email arrived. */
  plannedDeadline?: string | null
  rfqNameTaken: boolean
}
type CustomerOption = { id: number; name: string; customerCode?: string | null; email?: string | null }

/** Page's mailbox dropdown: only lines read from this MailFlow mailbox (null = all, '-' = unknown). */
const props = defineProps<{ mailbox?: string | null }>()
const emit = defineEmits<{ (e: 'accepted'): void; (e: 'loaded', count: number): void }>()

const api = useApi()
const rows = ref<PendingRfqRow[]>([])
const loading = ref(false)
const busyKey = ref('')

const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

// Paged by email on the server.
const page = ref(1)
const pageSize = ref(20)
const totalEmails = ref(0)
const totalLines = ref(0)
/** Every pending line, even while a search narrows the list ("Remove all", tab badge). */
const pendingTotal = ref(0)
const search = ref('')
const searchTerm = computed(() => search.value?.trim() || '')
let loadId = 0
/** Set when load() itself moved `page` — that page is already loaded. */
let clampedPage = false

async function load() {
  const id = ++loadId
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize.value) })
    if (searchTerm.value) params.set('search', searchTerm.value)
    if (props.mailbox) params.set('mailbox', props.mailbox)
    const res = await api.get<{ items: PendingRfqRow[]; page: number; totalEmails: number; totalLines: number; pendingTotal: number }>(
      `/pending-rfqs?${params}`)
    if (id !== loadId) return
    rows.value = res.items
    totalEmails.value = res.totalEmails
    totalLines.value = res.totalLines
    if (res.page !== page.value) {
      clampedPage = true
      page.value = res.page
    }
    pendingTotal.value = res.pendingTotal
    const ids = new Set(res.items.map(r => r.id))
    selected.value = new Set([...selected.value].filter(i => ids.has(i)))
    emit('loaded', res.pendingTotal)
  } catch {
    if (id === loadId) showSnack('Could not load RFQs from email', 'error')
  } finally {
    if (id === loadId) loading.value = false
  }
}
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

// One group per customer email
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
        mailbox: r.mailbox,
        mailboxLabel: r.mailboxLabel,
        customerId: r.customerId,
        customerName: r.customerName,
        customerCode: r.customerCode,
        rfqName: r.rfqName,
        deadline: r.deadline,
        deadlineRaw: r.deadlineRaw,
        emailId: r.emailId ?? null,
        attachmentNames: r.attachmentNames || [],
        rows: [] as PendingRfqRow[],
      })
    }
    map.get(key).rows.push(r)
  }
  for (const g of map.values()) {
    const rows = g.rows as PendingRfqRow[]
    g.mixedNames = new Set(rows.map(r => r.rfqName)).size > 1
    g.rfqNameTaken = rows.some(r => r.rfqNameTaken)
    const deadlines = new Set(rows.map(r => toDateInput(r.plannedDeadline)))
    g.sameDeadline = deadlines.size === 1 ? [...deadlines][0] : ''
    // The RFQs Create RFQ makes, in deadline order (the server names them).
    const rfqs = new Map<string, { name: string; deadline: string; count: number; taken: boolean }>()
    for (const r of rows) {
      const name = r.plannedRfqName || r.rfqName
      const rfq = rfqs.get(name) ?? { name, deadline: toDateInput(r.plannedDeadline), count: 0, taken: r.rfqNameTaken }
      rfq.count++
      rfqs.set(name, rfq)
    }
    g.rfqs = [...rfqs.values()].sort((a, b) => a.deadline.localeCompare(b.deadline) || a.name.localeCompare(b.name))
  }
  return [...map.values()]
})

/** "yyyy-MM-dd" for a date input, from the API's ISO date (no timezone shift). */
function toDateInput(iso?: string | null) {
  return iso ? iso.slice(0, 10) : ''
}
/** "yyyy-MM-dd" → "MM/DD/YYYY" without a timezone shift. */
function fmtDate(ymd: string) {
  const [y, m, d] = ymd.split('-')
  return y && m && d ? `${m}/${d}/${y}` : '—'
}

// ── Selection: tick lines to create the RFQ from some of them only ──
const selected = ref(new Set<number>())
function toggleRow(id: number, on: boolean) {
  const next = new Set(selected.value)
  if (on) next.add(id)
  else next.delete(id)
  selected.value = next
}
function toggleGroup(group: any, on: boolean) {
  const next = new Set(selected.value)
  for (const r of group.rows as PendingRfqRow[]) {
    if (on) next.add(r.id)
    else next.delete(r.id)
  }
  selected.value = next
}
function selectedIn(group: any): number[] {
  return (group.rows as PendingRfqRow[]).filter(r => selected.value.has(r.id)).map(r => r.id)
}
/** Ticked lines of the email, or all of them when none are ticked. */
function acceptIds(group: any): number[] {
  const ticked = selectedIn(group)
  return ticked.length ? ticked : (group.rows as PendingRfqRow[]).map(r => r.id)
}

// ── Customer lookup ──
const customerOptions = ref<CustomerOption[]>([])
const customerSearchLoading = ref(false)
let customerSearchTimer: ReturnType<typeof setTimeout> | undefined
function searchCustomers(q: string) {
  clearTimeout(customerSearchTimer)
  customerSearchTimer = setTimeout(async () => {
    customerSearchLoading.value = true
    try {
      customerOptions.value = await api.get<CustomerOption[]>(`/pending-rfqs/customers?q=${encodeURIComponent(q || '')}`)
    } catch {
      customerOptions.value = []
    } finally {
      customerSearchLoading.value = false
    }
  }, 250)
}
function customerTitle(c: CustomerOption) {
  return c.customerCode ? `${c.name} (${c.customerCode})` : c.name
}
/** Search results plus the group's current customer, so the selected name always renders. */
function customerOptionsFor(group: any): CustomerOption[] {
  const current = group.customerId ? [{ id: group.customerId, name: group.customerName, customerCode: group.customerCode }] : []
  return [...current, ...customerOptions.value.filter(c => c.id !== group.customerId)]
}

// One call for the whole email: the server also adds the sender's address to the customer's
// contacts (when missing) and links other pending emails from the same address.
async function setGroupCustomer(group: any, customerId: number | null) {
  try {
    const res = await api.post<{
      emailAdded?: string | null
      emailTarget?: string | null
      otherLinesLinked: number
      emailOnOtherCustomer?: string | null
    }>('/pending-rfqs/customer', { ids: group.rows.map((r: PendingRfqRow) => r.id), customerId })
    if (res.emailOnOtherCustomer) {
      showSnack(`${group.customerEmail} is already a contact of "${res.emailOnOtherCustomer}" — not added here`, 'warning')
    } else if (res.emailAdded) {
      const more = res.otherLinesLinked ? ` · ${res.otherLinesLinked} more line(s) from this address linked` : ''
      showSnack(`${res.emailAdded} added to the customer's contacts (${res.emailTarget})${more}`)
    }
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not set customer', 'error')
  }
}

// RFQ name / deadline on the email card apply to all its lines.
async function saveGroup(group: any, change: { rfqName?: string; deadline?: string | null }) {
  if (change.rfqName !== undefined && !change.rfqName.trim()) {
    showSnack('The RFQ name cannot be empty', 'error')
    return
  }
  try {
    await api.post('/pending-rfqs/email', { ids: group.rows.map((r: PendingRfqRow) => r.id), ...change })
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not save', 'error')
  }
}

// One part's deadline — re-splits the email's RFQs, so the list reloads for the new names.
async function saveRowDeadline(row: PendingRfqRow, value: string) {
  if (!value || value === toDateInput(row.plannedDeadline)) return
  try {
    await api.post('/pending-rfqs/email', { ids: [row.id], deadline: value })
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not save the deadline', 'error')
  }
}

// ── Edit one line ──
const showEdit = ref(false)
const savingEdit = ref(false)
const editForm = ref<any>(null)
function openEdit(row: PendingRfqRow) {
  editForm.value = {
    id: row.id,
    partNumber: row.partNumber,
    alternatives: [...row.alternatives],
    description: row.description || '',
    qty: row.qty,
    condition: row.condition || '',
    priority: row.priority || '',
    remark: row.remark || '',
    rfqName: row.rfqName,
    deadline: toDateInput(row.deadline),
  }
  showEdit.value = true
}
async function saveEdit() {
  const f = editForm.value
  savingEdit.value = true
  try {
    await api.put<PendingRfqRow>(`/pending-rfqs/${f.id}`, {
      ...f,
      qty: f.qty === '' || f.qty == null ? null : Number(f.qty),
      deadline: f.deadline || null,
    })
    showEdit.value = false
    await load() // a new name or deadline can re-number the email's ABA(1), ABA(2)...
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not save the line', 'error')
  } finally {
    savingEdit.value = false
  }
}

// ── Accept = create the RFQ ──
async function accept(ids: number[], key: string) {
  if (!ids.length) return
  busyKey.value = key
  try {
    const res = await api.post<{
      accepted: number
      rfqs: { id: number; name: string; customerName: string; itemCount: number }[]
      failed: { partNumber: string; reason: string }[]
    }>('/pending-rfqs/accept', { ids })
    if (res.rfqs?.length) {
      showSnack(`Created ${res.rfqs.map(r => `${r.name} (${r.itemCount} item${r.itemCount === 1 ? '' : 's'})`).join(', ')} for ${res.rfqs[0]!.customerName}`)
      emit('accepted')
    }
    if (res.failed?.length) {
      const reasons = [...new Set(res.failed.map(f => f.reason))]
      showSnack(reasons.join(' · '), res.accepted ? 'warning' : 'error')
    }
    await load()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Could not create the RFQ', 'error')
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
  emailViewId.value = group.emailId
  emailViewAttachment.value = attachmentName
  showEmail.value = true
}

/** Autosaved line from the viewer: patch it in place; reload on close, since a new name / deadline can re-number ABA(1), ABA(2)... */
let viewerEdited = false
function onViewerRowUpdated(updated: PendingRfqRow) {
  const row = rows.value.find(r => r.id === updated?.id)
  if (row) Object.assign(row, updated)
  if (showEmail.value) viewerEdited = true
  else load() // the last save landed after the viewer closed
}
watch(showEmail, (open) => {
  if (!open && viewerEdited) {
    viewerEdited = false
    load()
  }
})

// ── Remove ──
const showConfirmRemove = ref(false)
const removeIds = ref<number[]>([])
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
    if (removeAll.value) await api.post('/pending-rfqs/remove-all', {})
    else await api.post('/pending-rfqs/remove', { ids: removeIds.value })
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
.row-date {
  font: inherit;
  color: inherit;
  padding: 2px 4px;
  border: 1px solid var(--card-border);
  border-radius: 4px;
  background: transparent;
  color-scheme: light dark;
}
</style>
