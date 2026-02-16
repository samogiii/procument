<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div class="d-flex align-center">
        <v-btn icon="mdi-arrow-left" variant="text" to="/catalog" class="mr-2" />
        <h1 class="text-h5 font-weight-bold">Suppliers</h1>
        <v-chip size="small" color="primary" variant="tonal" class="ml-3">{{ items.length }}</v-chip>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openDialog()">Add Supplier</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search suppliers..." single-line hide-details class="mb-4" />
        <v-data-table :headers="headers" :items="filteredItems" :loading="loading" items-per-page="15" hover>
          <template #item.isActive="{ item }">
            <v-chip :color="item.isActive ? 'success' : 'grey'" size="small">{{ item.isActive ? 'Active' : 'Inactive' }}</v-chip>
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" variant="text" size="x-small" @click="openDialog(item)" class="mr-1" />
            <v-btn icon="mdi-delete" variant="text" size="x-small" color="error" @click="deleteItem(item.id)" />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <v-dialog v-model="showDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title>{{ editingId ? 'Edit Supplier' : 'New Supplier' }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="Name" class="mb-2" />
          <v-text-field v-model="form.email" label="Email" class="mb-2" />
          <v-text-field v-model="form.phone" label="Phone" class="mb-2" />
          <v-text-field v-model="form.address" label="Address" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showDialog = false">Cancel</v-btn>
          <v-btn color="primary" @click="save" :loading="saving">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const search = ref('')
const loading = ref(false)
const saving = ref(false)
const items = ref<any[]>([])
const showDialog = ref(false)
const editingId = ref<number | null>(null)
const form = ref({ name: '', email: '', phone: '', address: '' })

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone' },
  { title: 'Address', key: 'address' },
  { title: 'Status', key: 'isActive', width: '100px' },
  { title: '', key: 'actions', sortable: false, width: '100px' },
]

const filteredItems = computed(() => {
  if (!search.value) return items.value
  const q = search.value.toLowerCase()
  return items.value.filter((i: any) =>
    i.name?.toLowerCase().includes(q) ||
    i.email?.toLowerCase().includes(q) ||
    i.phone?.toLowerCase().includes(q)
  )
})

onMounted(() => loadItems())

async function loadItems() {
  loading.value = true
  try {
    items.value = await api.get<any[]>('/suppliers')
  } catch {}
  finally { loading.value = false }
}

function openDialog(item?: any) {
  if (item) {
    editingId.value = item.id
    form.value = { name: item.name, email: item.email || '', phone: item.phone || '', address: item.address || '' }
  } else {
    editingId.value = null
    form.value = { name: '', email: '', phone: '', address: '' }
  }
  showDialog.value = true
}

async function save() {
  saving.value = true
  try {
    if (editingId.value) {
      await api.put(`/suppliers/${editingId.value}`, form.value)
    } else {
      await api.post('/suppliers', form.value)
    }
    showDialog.value = false
    await loadItems()
  } catch {}
  finally { saving.value = false }
}

async function deleteItem(id: number) {
  if (!confirm('Delete this supplier?')) return
  try {
    await api.del(`/suppliers/${id}`)
    await loadItems()
  } catch {}
}
</script>
