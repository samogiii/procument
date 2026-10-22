<template>
  <!-- Email RFQs: every pending + accepted line of emails sent between two dates, as a CSV. -->
  <v-menu v-model="open" :close-on-content-click="false" location="bottom end">
    <template #activator="{ props: activator }">
      <v-btn v-bind="activator" size="small" color="success" variant="tonal" prepend-icon="mdi-file-delimited-outline">
        Export CSV
      </v-btn>
    </template>
    <v-card min-width="300">
      <v-card-title class="text-subtitle-1 pa-4 pb-2">Export RFQ emails</v-card-title>
      <v-card-text class="pt-0">
        <p class="text-caption text-medium-emphasis mb-3">
          Parts of the RFQ emails sent in this period (pending and accepted, not removed){{ mailbox ? ', this mailbox only' : '' }}.
        </p>
        <div class="d-flex gap-2">
          <v-text-field v-model="from" type="date" label="From" density="compact" variant="outlined" hide-details />
          <v-text-field v-model="to" type="date" label="To" density="compact" variant="outlined" hide-details />
        </div>
        <p v-if="error" class="text-caption text-error mt-2 mb-0">{{ error }}</p>
      </v-card-text>
      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn variant="text" @click="open = false">Cancel</v-btn>
        <v-btn color="success" variant="flat" :loading="exporting" :disabled="!from || !to" @click="exportCsv">Export</v-btn>
      </v-card-actions>
    </v-card>
  </v-menu>
</template>

<script setup lang="ts">
type ExportRow = {
  emailDate?: string | null
  createdAt: string
  emailSubject?: string
  customerEmail?: string
  customerNameRaw?: string
  customerName?: string
  customerCode?: string
  mailboxLabel?: string | null
  mailbox?: string | null
  rfqName: string
  plannedRfqName?: string | null
  createdRfqName?: string | null
  deadline?: string | null
  plannedDeadline?: string | null
  partNumber: string
  alternatives: string[]
  description?: string
  qty: number | null
  condition?: string
  priority?: string
  remark?: string
  status: string
  reviewedByName?: string
  reviewedAt?: string | null
}

/** Page's mailbox dropdown (null = every mailbox). */
const props = defineProps<{ mailbox?: string | null }>()

const api = useApi()
const open = ref(false)
const exporting = ref(false)
const error = ref('')

/** "yyyy-MM-dd" of a local date. */
function ymd(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
const today = new Date()
const from = ref(ymd(new Date(today.getFullYear(), today.getMonth(), today.getDate() - 7)))
const to = ref(ymd(today))

/** "yyyy-MM-dd…" from the API → "MM/DD/YYYY" with no timezone shift (deadlines are dates). */
function fmtDay(iso?: string | null) {
  if (!iso) return ''
  const [y, m, d] = iso.slice(0, 10).split('-')
  return `${m}/${d}/${y}`
}

async function exportCsv() {
  error.value = ''
  // Whole local days: from 00:00 on "From" up to 00:00 the day after "To".
  const start = new Date(`${from.value}T00:00`)
  const end = new Date(`${to.value}T00:00`)
  end.setDate(end.getDate() + 1)
  if (end <= start) {
    error.value = '"To" must not be before "From".'
    return
  }
  exporting.value = true
  try {
    const params = new URLSearchParams({ from: start.toISOString(), to: end.toISOString() })
    if (props.mailbox) params.set('mailbox', props.mailbox)
    const rows = await api.get<ExportRow[]>(`/pending-rfqs/export?${params}`)
    if (!rows.length) {
      error.value = 'No RFQ emails in this period.'
      return
    }
    downloadCsv(rows.map(r => ({
      'Email Date': new Date(r.emailDate || r.createdAt).toLocaleString(),
      'Customer': r.customerName || r.customerNameRaw || '',
      'Customer Code': r.customerCode || '',
      'Customer Email': r.customerEmail || '',
      'Email Subject': r.emailSubject || '',
      'Mailbox': r.mailboxLabel || r.mailbox || '',
      'RFQ Name': r.createdRfqName || r.plannedRfqName || r.rfqName,
      'Deadline': fmtDay(r.deadline || r.plannedDeadline),
      'Part Number': r.partNumber,
      'Alternatives': r.alternatives.join(', '),
      'Description': r.description || '',
      'Qty': r.qty ?? '',
      'Condition': r.condition || '',
      'Priority': r.priority || '',
      'Remark': r.remark || '',
      'Status': r.status === 'Accepted' ? 'RFQ created' : r.status,
      'Accepted By': r.reviewedByName || '',
      'Accepted At': r.reviewedAt ? new Date(r.reviewedAt).toLocaleString() : '',
    })), `email-rfqs-${from.value}-to-${to.value}`)
    open.value = false
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not export'
  } finally {
    exporting.value = false
  }
}
</script>
