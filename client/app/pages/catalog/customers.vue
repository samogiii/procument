<template>
  <div>
    <PageHeader title="Customers" back-to="/catalog" :count="totalItems">
      <template #actions>
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openDialog()">Add Customer</v-btn>
      </template>
    </PageHeader>

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex align-center gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search customers..."
            single-line
            hide-details
            style="flex: 1"
          />
          <v-text-field
            v-if="isAdmin"
            v-model="baseFilter"
            prepend-inner-icon="mdi-filter-outline"
            label="Filter by Base"
            type="number"
            clearable
            hide-details
            style="max-width: 180px"
          />
        </div>
        <v-data-table-server
          :headers="headers"
          :items="serverItems"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="50"
          hover
          @update:options="onTableOptions"
        >
          <template #item.isActive="{ item }">
            <StatusChip :status="item.isActive ? 'Active' : 'Inactive'" />
          </template>
          <template #item.companyType="{ item }">
            <v-chip
              v-if="item.companyType"
              size="x-small"
              variant="flat"
              :color="item.companyType === 'Airline' ? 'primary' : item.companyType === 'MRO' ? 'teal' : 'orange'"
              class="font-weight-medium"
            >
              {{ item.companyType }}
            </v-chip>
            <span v-else>—</span>
          </template>
          <template #item.email="{ item }">
            <div>
              <div v-if="item.email" class="text-body-2">{{ item.email }}</div>
              <div v-if="item.emails" class="d-flex flex-wrap gap-1 mt-1">
                <v-chip
                  v-for="email in item.emails.split(',').map((e: any) => e.trim()).filter((e: any) => e)"
                  :key="email"
                  size="x-small"
                  variant="tonal"
                  color="grey"
                >
                  {{ email }}
                </v-chip>
              </div>
            </div>
          </template>
          <template #item.website="{ item }">
            <a
              v-if="item.website"
              :href="item.website.startsWith('http') ? item.website : 'https://' + item.website"
              target="_blank"
              rel="noopener"
              class="text-primary text-decoration-none text-caption"
            >
              {{ item.website }}
            </a>
            <span v-else>—</span>
          </template>
          <template #item.exWork="{ item }">
            <v-chip v-if="item.exWork != null" size="x-small" variant="tonal" color="primary">
              {{ exWorkOptions.find(o => o.value === item.exWork)?.title || '—' }}
            </v-chip>
            <span v-else>—</span>
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
      :title="isEditing ? 'Edit Customer' : 'New Customer'"
      :loading="saving"
      @save="save"
    >
      <v-text-field v-model="form.name" label="Name" class="mb-2" />
      <v-text-field
        v-model="form.customerCode"
        label="Customer Code"
        class="mb-2"
        :placeholder="suggestedCode || undefined"
        :hint="!isEditing && suggestedCode && !form.customerCode ? `Auto-suggested: ${suggestedCode}` : ''"
        persistent-hint
        :append-inner-icon="!isEditing && suggestedCode && !form.customerCode ? 'mdi-auto-fix' : undefined"
        @click:append-inner="form.customerCode = suggestedCode"
      />
      <v-select
        v-model="form.companyType"
        :items="['Airline', 'MRO', 'Supplier']"
        label="Company Type"
        class="mb-2"
        clearable
      />
      <v-text-field v-model="form.country" label="Country" class="mb-2" />
      <v-text-field v-model="form.email" label="Primary Email" class="mb-2" />
      <v-combobox
        v-model="form.emails"
        label="Additional Email Addresses"
        multiple
        chips
        closable-chips
        class="mb-2"
        placeholder="Type email and press Enter"
        prepend-inner-icon="mdi-email-multiple-outline"
        delimiters="[',', ' ']"
      />
      <v-text-field v-model="form.phone" label="Phone" class="mb-2" />
      <v-text-field v-model="form.website" label="Website" class="mb-2" prepend-inner-icon="mdi-web" placeholder="e.g. www.example.com" />
      <v-textarea v-model="form.contactPerson" label="Contact Person" class="mb-2" />
      <v-textarea v-model="form.shipTo" label="Ship To" class="mb-2" />
      <v-textarea v-model="form.billTo" label="Bill To" class="mb-2" />
      <v-textarea v-model="form.shippingAccount" label="Shipping Account" class="mb-2" />
      <v-textarea v-model="form.description" label="Description" rows="3" auto-grow class="mb-2" />
      <v-textarea v-model="form.termsAndConditions" label="Terms and Conditions (Quotes/RFQs)" rows="3" auto-grow class="mb-2" />
      <v-textarea
        v-model="form.piTermsAndConditions"
        label="PI Terms and Conditions (Invoices)"
        rows="3"
        auto-grow
        class="mb-2"
        hint="Overrides terms in Proforma Invoice PDFs"
        persistent-hint
      />
      <!-- Contact Persons -->
      <div class="text-caption font-weight-bold text-medium-emphasis text-uppercase mt-2 mb-1">Contact Persons</div>
      <div v-for="(c, i) in contactsList" :key="i" class="d-flex gap-2 align-center mb-1">
        <v-text-field v-model="c.name" label="Name" variant="outlined" density="compact" hide-details class="flex-grow-1" />
        <v-text-field v-model="c.email" label="Email" variant="outlined" density="compact" hide-details class="flex-grow-1" />
        <v-text-field v-model="c.phone" label="Phone" variant="outlined" density="compact" hide-details style="max-width:130px;" />
        <v-btn icon="mdi-close" size="x-small" variant="text" color="error" @click="contactsList.splice(i, 1)" />
      </div>
      <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" class="mb-3 mt-1" @click="contactsList.push({ name: '', email: '', phone: '' })">Add Contact</v-btn>

      <v-select
        v-model="form.exWork"
        :items="exWorkSelectOptions"
        item-title="title"
        item-value="value"
        label="Ship To"
        class="mb-2"
        clearable
      />
      <v-text-field
        v-if="isAdmin"
        v-model.number="form.base"
        label="Base"
        type="number"
        class="mb-2"
        :loading="codeLoading"
      />
      <v-select
        v-if="isAdmin && form.base === 3"
        v-model="form.currencyType"
        :items="['Dollar', 'Yuan', 'Both']"
        label="Currency Type"
        class="mb-2"
      />
    </CrudDialog>

    <ConfirmDialog
      v-model="showConfirm"
      title="Delete Customer?"
      message="This action cannot be undone."
      @confirm="doDelete"
    />
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const route = useRoute()
const isAdmin = computed(() => authStore.isAdmin)

// value 2 kept for legacy display; both 1 and 2 show as Vendor/Customer
const exWorkOptions = [
  { title: 'Ex Warehouse', value: 0 },
  { title: 'Vendor/Customer', value: 1 },
  { title: 'Vendor/Customer', value: 2 },
]
// Only 2 choices shown in the edit select
const exWorkSelectOptions = [
  { title: 'Warehouse', value: 0 },
  { title: 'Vendor/Customer', value: 1 },
]

// ─── Server-side data ───
const serverItems = ref<any[]>([])
const totalItems = ref(0)
const loading = ref(false)
const search = ref('')
const debouncedSearch = ref('')
const baseFilter = ref('')
const currentOptions = ref<any>({ page: 1, itemsPerPage: 50 })
const sort = useServerSort()
let searchTimer: ReturnType<typeof setTimeout> | null = null

watch(search, (val) => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    debouncedSearch.value = val
    onTableOptions({ ...currentOptions.value, page: 1 })
  }, 350)
})

watch(baseFilter, () => {
  onTableOptions({ ...currentOptions.value, page: 1 })
})

async function onTableOptions(opts: any) {
  currentOptions.value = opts
  sort.capture(opts)
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(opts.page), pageSize: String(opts.itemsPerPage) })
    if (debouncedSearch.value) params.set('search', debouncedSearch.value)
    if (baseFilter.value && baseFilter.value.trim()) params.set('base', baseFilter.value.trim())
    sort.appendTo(params)
    const res = await api.get<any>(`/customers?${params}`)
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

const defaultForm = () => ({
  name: '', customerCode: '', email: '', phone: '', contactPerson: '',
  shipTo: '', billTo: '', shippingAccount: '', description: '',
  base: null as number | null, termsAndConditions: '', currencyType: '', exWork: null as number | null,
  piTermsAndConditions: '', companyType: null as string | null, country: '', emails: [] as string[],
  website: '',
})
const form = ref(defaultForm())
const contactsList = ref<{name: string, email: string, phone: string}[]>([])

// ─── Auto-suggest customer code by base ───
const suggestedCode = ref('')
const codeLoading = ref(false)

watch(() => form.value.base, async (base) => {
  suggestedCode.value = ''
  if (!base || isEditing.value) return
  codeLoading.value = true
  try {
    const res = await api.get<{ nextCode: string }>(`/customers/next-code?base=${base}`)
    suggestedCode.value = res.nextCode
    // Auto-fill only if user hasn't typed a code yet
    if (!form.value.customerCode) {
      form.value.customerCode = res.nextCode
    }
  } catch { /* silent */ } finally {
    codeLoading.value = false
  }
})

function openDialog(item?: any) {
  if (item) {
    editingId.value = item.id
    const d = defaultForm()
    form.value = { ...d, ...Object.fromEntries(Object.keys(d).map(k => [k, item[k] ?? (d as any)[k]])) }
    form.value.emails = item.emails
      ? item.emails.split(',').map((s: string) => s.trim()).filter((s: string) => s.length > 0)
      : []
    try { contactsList.value = item.contacts ? JSON.parse(item.contacts) : [] } catch { contactsList.value = [] }
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
  suggestedCode.value = ''
}

async function save() {
  saving.value = true
  try {
    const validContacts = contactsList.value.filter(c => c.name.trim())
    const payload = {
      ...form.value,
      emails: form.value.emails && form.value.emails.length > 0 ? form.value.emails.join(', ') : '',
      contacts: validContacts.length ? JSON.stringify(validContacts) : null,
    }
    if (editingId.value) {
      await api.put(`/customers/${editingId.value}`, payload)
    } else {
      await api.post('/customers', payload)
    }
    closeDialog()
    await refreshPage()
  } finally {
    saving.value = false
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
    await api.del(`/customers/${deleteTarget.value}`)
    await refreshPage()
  }
  deleteTarget.value = null
}

const headers = computed(() => {
  const h: any[] = [
    { title: 'Name', key: 'name' },
    { title: 'Code', key: 'customerCode' },
    { title: 'Company Type', key: 'companyType' },
    { title: 'Country', key: 'country' },
    { title: 'Ship To Type', key: 'exWork', width: '130px' },
    { title: 'Email', key: 'email' },
    { title: 'Phone', key: 'phone' },
    { title: 'Website', key: 'website' },
    { title: 'Contact Person', key: 'contactPerson' },
    { title: 'Ship To', key: 'shipTo' },
    { title: 'Bill To', key: 'billTo' },
    { title: 'Shipping Account', key: 'shippingAccount' },
    { title: 'Description', key: 'description' },
    { title: 'Status', key: 'isActive', width: '100px' },
    { title: '', key: 'actions', sortable: false, width: '100px' },
  ]
  if (isAdmin.value) {
    h.splice(9, 0, { title: 'Base', key: 'base', width: '100px' })
  }
  return h
})

onMounted(() => {
  if (route.query.search) {
    search.value = String(route.query.search)
    debouncedSearch.value = String(route.query.search)
  }
})
</script>
