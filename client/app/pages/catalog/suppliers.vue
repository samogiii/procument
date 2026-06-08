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
          <template #item.status="{ item }">
            <StatusChip v-if="item.status" :status="item.status" size="small" />
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
      <v-text-field v-model="form.email" label="Email" class="mb-2" />
      <v-text-field v-model="form.phone" label="Phone" class="mb-2" />
      <v-text-field v-model="form.address" label="Address" class="mb-2" />
      <v-textarea v-model="form.description" label="Description" rows="3" />
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

const defaultForm = () => ({ name: '', username: '', dependency: '', description: '', email: '', phone: '', address: '' })
const form = ref(defaultForm())

function openDialog(item?: any) {
  if (item) {
    editingId.value = item.id
    const d = defaultForm()
    form.value = { ...d, ...Object.fromEntries(Object.keys(d).map(k => [k, item[k] ?? (d as any)[k]])) }
  } else {
    editingId.value = null
    form.value = defaultForm()
  }
  showDialog.value = true
}

function closeDialog() {
  showDialog.value = false
  editingId.value = null
  form.value = defaultForm()
}

async function save() {
  if (!dependencyOptions.includes(form.value.dependency)) return
  saving.value = true
  try {
    if (editingId.value) {
      await api.put(`/suppliers/${editingId.value}`, form.value)
    } else {
      await api.post('/suppliers', form.value)
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
    await api.del(`/suppliers/${deleteTarget.value}`)
    await refreshPage()
  }
  deleteTarget.value = null
}

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Username', key: 'username' },
  { title: 'Dependency', key: 'dependency' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone' },
  { title: 'Address', key: 'address' },
  { title: 'Approval', key: 'status', width: '110px' },
  { title: 'Active', key: 'isActive', width: '100px' },
  { title: '', key: 'actions', sortable: false, width: '100px' },
]
</script>
