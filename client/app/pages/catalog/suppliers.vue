<template>
  <div>
    <PageHeader title="Suppliers" back-to="/catalog" :count="totalItems">
      <template #actions>
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openDialog()">Add Supplier</v-btn>
      </template>
    </PageHeader>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search suppliers..."
          single-line
          hide-details
          class="mb-4"
        />
        <v-data-table-server
          :headers="headers"
          :items="serverItems"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="50"
          hover
          @update:options="onTableOptions"
        >
          <template #item.isPreferred="{ item }">
            <v-btn
              :icon="item.isPreferred ? 'mdi-star' : 'mdi-star-outline'"
              :color="item.isPreferred ? 'amber' : undefined"
              variant="text"
              size="x-small"
              :loading="starring === item.id"
              :title="item.isPreferred ? 'Preferred supplier — click to remove' : 'Mark as preferred (Procurement Plus)'"
              @click="togglePreferred(item)"
            />
          </template>
          <template #item.trusted="{ item }">
            <v-chip v-if="item.trusted" size="x-small" color="success" variant="tonal" prepend-icon="mdi-shield-check-outline">Trusted</v-chip>
            <span v-else class="text-medium-emphasis text-caption">Not trusted</span>
          </template>
          <template #item.status="{ item }">
            <StatusChip v-if="item.status" :status="item.status" size="small" />
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.contacts="{ item }">
            <template v-if="contactEmailCount(item)">
              <v-tooltip location="top">
                <template #activator="{ props: tip }">
                  <v-chip v-bind="tip" size="small" variant="tonal" prepend-icon="mdi-account-multiple-outline">
                    {{ parseContacts(item.contacts).length }} · {{ contactEmailCount(item) }} email{{ contactEmailCount(item) === 1 ? '' : 's' }}
                  </v-chip>
                </template>
                <div v-for="(c, i) in parseContacts(item.contacts)" :key="i" class="py-1">
                  <strong>{{ c.name || '—' }}</strong>
                  <div v-for="e in c.emails" :key="e" class="text-caption">{{ e }}</div>
                </div>
              </v-tooltip>
            </template>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.isActive="{ item }">
            <StatusChip :status="item.isActive ? 'Active' : 'Inactive'" />
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" variant="text" size="x-small" @click="openDialog(item)" class="mr-1" />
            <v-btn icon="mdi-delete" variant="text" size="x-small" color="error" @click="confirmDelete(item.id)" />
          </template>
        </v-data-table-server>
      </v-card-text>
    </v-card>

    <CrudDialog
      v-model="showDialog"
      :title="isEditing ? 'Edit Supplier' : 'New Supplier'"
      :loading="saving"
      @save="save"
    >
      <v-text-field v-model="form.name" label="Name" class="mb-2" />
      <v-text-field v-model="form.username" label="Username" class="mb-2" />
      <v-select
        v-model="form.dependency"
        :items="dependencyOptions"
        label="Dependency"
        class="mb-2"
        variant="outlined"
        density="compact"
      />
      <!-- Trusted (Procurement Plus commission rule) — SuperAdmin only -->
      <v-switch
        v-if="isSuperAdmin"
        v-model="form.trusted"
        color="success"
        inset
        density="compact"
        label="Trusted supplier"
        hint="Not used for now — the Procurement Plus commission has no minimum coef."
        persistent-hint
        class="mb-3"
      />
      <v-text-field v-model="form.email" label="Email" class="mb-2" />
      <v-text-field v-model="form.phone" label="Phone" class="mb-2" />
      <v-text-field v-model="form.address" label="Address" class="mb-2" />
      <v-textarea v-model="form.description" label="Description" rows="3" class="mb-2" />
      <!-- Contact Persons -->
      <div class="text-caption font-weight-bold text-medium-emphasis text-uppercase mt-2 mb-1">Contact Persons</div>
      <div v-for="(c, i) in contactsList" :key="i" class="contact-card mb-2">
        <div class="d-flex gap-2 align-center">
          <v-text-field v-model="c.name" label="Name" variant="outlined" density="compact" hide-details class="flex-grow-1" />
          <v-text-field v-model="c.phone" label="Phone" variant="outlined" density="compact" hide-details style="max-width:150px;" />
          <v-btn icon="mdi-close" size="x-small" variant="text" color="error" @click="contactsList.splice(i, 1)" />
        </div>
        <v-combobox
          v-model="c.emails"
          label="Emails"
          placeholder="Type an email and press Enter"
          variant="outlined"
          density="compact"
          multiple
          chips
          closable-chips
          hide-details="auto"
          :rules="[emailsRule]"
          :delimiters="[',', ';', ' ']"
          prepend-inner-icon="mdi-email-multiple-outline"
          class="mt-2"
          @update:model-value="c.emails = cleanEmails($event)"
        />
      </div>
      <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" class="mt-1" @click="contactsList.push(emptyContact())">Add Contact</v-btn>
    </CrudDialog>

    <ConfirmDialog
      v-model="showConfirm"
      title="Delete Supplier?"
      message="This action cannot be undone."
      @confirm="doDelete"
    />
  </div>
</template>

<script setup lang="ts">
const api = useApi()

const dependencyOptions = ['Normal', 'Certificated', 'NoQuote', 'EndUser']

// ─── Server-side data ───
const serverItems = ref<any[]>([])
const totalItems = ref(0)
const loading = ref(false)
const search = ref('')
const debouncedSearch = ref('')
const currentOptions = ref({ page: 1, itemsPerPage: 50 })
let searchTimer: ReturnType<typeof setTimeout> | null = null

watch(search, (val) => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    debouncedSearch.value = val
    onTableOptions({ ...currentOptions.value, page: 1 })
  }, 350)
})

async function onTableOptions(opts: { page: number; itemsPerPage: number }) {
  currentOptions.value = { page: opts.page, itemsPerPage: opts.itemsPerPage }
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(opts.page), pageSize: String(opts.itemsPerPage) })
    if (debouncedSearch.value) params.set('search', debouncedSearch.value)
    const res = await api.get<any>(`/suppliers?${params}`)
    serverItems.value = res.items ?? res.Items ?? []
    totalItems.value = res.totalCount ?? res.TotalCount ?? serverItems.value.length
  } finally {
    loading.value = false
  }
}

async function refreshPage() {
  await onTableOptions(currentOptions.value)
}

// ─── CRUD ───
const saving = ref(false)
const showDialog = ref(false)
const editingId = ref<number | null>(null)
const isEditing = computed(() => editingId.value !== null)

const isSuperAdmin = computed(() => useAuthStore().isSuperAdmin)
const defaultForm = () => ({ name: '', username: '', dependency: '', description: '', email: '', phone: '', address: '', trusted: false })
const form = ref(defaultForm())

// Contacts JSON: [{ name, email, emails[], phone, ...extra }]. `email` stays the first address so
// older readers (PO PDF, MailFlow matching) keep working; `emails` holds all of them.
type ContactRow = { name: string; emails: string[]; phone: string; extra: Record<string, any> }
const contactsList = ref<ContactRow[]>([])
const emptyContact = (): ContactRow => ({ name: '', emails: [], phone: '', extra: {} })
const EMAIL_RE = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/

function cleanEmails(list: any[]): string[] {
  const seen = new Set<string>()
  return (list || [])
    .flatMap(e => String(e ?? '').split(/[,;\s]+/))
    .map(e => e.trim())
    .filter(e => e && !seen.has(e.toLowerCase()) && !!seen.add(e.toLowerCase()))
}
const emailsRule = (list: string[]) =>
  (list || []).every(e => EMAIL_RE.test(e)) || 'One of the emails is not valid'

function parseContacts(raw?: string | null): ContactRow[] {
  let parsed: any[] = []
  try { parsed = raw ? JSON.parse(raw) : [] } catch { parsed = [] }
  if (!Array.isArray(parsed)) return []
  return parsed.map((c: any) => {
    const { name, email, emails, phone, ...extra } = c || {}
    return {
      name: name || '',
      emails: cleanEmails([email, ...(Array.isArray(emails) ? emails : [])]),
      phone: phone || '',
      extra,
    }
  })
}

function serializeContacts(rows: ContactRow[]): string | null {
  const valid = rows.filter(c => c.name.trim())
  if (!valid.length) return null
  return JSON.stringify(valid.map(c => ({
    ...c.extra,
    name: c.name.trim(),
    email: c.emails[0] || '',
    emails: c.emails,
    phone: c.phone,
  })))
}

function contactEmailCount(item: any) {
  return parseContacts(item.contacts).reduce((n, c) => n + c.emails.length, 0)
}

function openDialog(item?: any) {
  if (item) {
    editingId.value = item.id
    const d = defaultForm()
    form.value = { ...d, ...Object.fromEntries(Object.keys(d).map(k => [k, item[k] ?? (d as any)[k]])) }
    contactsList.value = parseContacts(item.contacts)
  } else {
    editingId.value = null
    form.value = defaultForm()
    contactsList.value = []
  }
  showDialog.value = true
}

function closeDialog() {
  showDialog.value = false
  editingId.value = null
  form.value = defaultForm()
  contactsList.value = []
}

async function save() {
  if (!dependencyOptions.includes(form.value.dependency)) return
  if (contactsList.value.some(c => emailsRule(c.emails) !== true)) return
  saving.value = true
  try {
    const payload = {
      ...form.value,
      contacts: serializeContacts(contactsList.value),
      trusted: isSuperAdmin.value ? form.value.trusted : null, // null = keep (SuperAdmin only)
    }
    if (editingId.value) {
      await api.put(`/suppliers/${editingId.value}`, payload)
    } else {
      await api.post('/suppliers', payload)
    }
    closeDialog()
    await refreshPage()
  } finally {
    saving.value = false
  }
}

// ─── Preferred (Procurement Plus) ───
const starring = ref<number | null>(null)
async function togglePreferred(item: any) {
  starring.value = item.id
  try {
    await api.put(`/suppliers/${item.id}/preferred`, { preferred: !item.isPreferred })
    item.isPreferred = !item.isPreferred
  } finally {
    starring.value = null
  }
}

// ─── Delete ───
const showConfirm = ref(false)
const deleteTarget = ref<number | null>(null)

function confirmDelete(id: number) {
  deleteTarget.value = id
  showConfirm.value = true
}

async function doDelete() {
  if (deleteTarget.value) {
    await api.del(`/suppliers/${deleteTarget.value}`)
    await refreshPage()
  }
  deleteTarget.value = null
}

const headers = [
  { title: '', key: 'isPreferred', sortable: false, width: '44px' },
  { title: 'Name', key: 'name' },
  { title: 'Username', key: 'username' },
  { title: 'Dependency', key: 'dependency' },
  { title: 'Trusted', key: 'trusted', width: '110px' },
  { title: 'Email', key: 'email' },
  { title: 'Contacts', key: 'contacts', sortable: false },
  { title: 'Phone', key: 'phone' },
  { title: 'Address', key: 'address' },
  { title: 'Approval', key: 'status', width: '110px' },
  { title: 'Active', key: 'isActive', width: '100px' },
  { title: '', key: 'actions', sortable: false, width: '100px' },
]
</script>

<style scoped>
.contact-card {
  padding: 10px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
}
</style>
