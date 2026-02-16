<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div class="d-flex align-center">
        <v-btn icon="mdi-arrow-left" variant="text" to="/catalog" class="mr-2" />
        <h1 class="text-h5 font-weight-bold">Part Numbers</h1>
        <v-chip size="small" color="primary" variant="tonal" class="ml-3">{{ items.length }}</v-chip>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openDialog()">Add Part</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search parts..." single-line hide-details class="mb-4" />
        <v-data-table :headers="headers" :items="filteredItems" :loading="loading" items-per-page="15" hover>
          <template #item.alternatives="{ item }">
            <div v-if="item.alternatives?.length" class="d-flex flex-wrap gap-1">
              <v-chip v-for="alt in item.alternatives" :key="alt.id" size="x-small" color="warning" variant="tonal">
                {{ alt.name }}
              </v-chip>
            </div>
            <span v-else class="text-medium-emphasis">—</span>
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
        <v-card-title>{{ editingId ? 'Edit Part Number' : 'New Part Number' }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="Part Number" class="mb-2" />
          <v-text-field v-model="form.description" label="Description" class="mb-2" />
          <v-autocomplete
            v-model="form.supplierId"
            :items="supplierOptions"
            item-title="name"
            item-value="id"
            label="Supplier (optional)"
            clearable
          />
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
const supplierOptions = ref<any[]>([])
const showDialog = ref(false)
const editingId = ref<number | null>(null)
const form = ref({ name: '', description: '', supplierId: null as number | null })

const headers = [
  { title: 'Part Number', key: 'name' },
  { title: 'Description', key: 'description' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Alternatives', key: 'alternatives', sortable: false },
  { title: '', key: 'actions', sortable: false, width: '100px' },
]

const filteredItems = computed(() => {
  if (!search.value) return items.value
  const q = search.value.toLowerCase()
  return items.value.filter((i: any) =>
    i.name?.toLowerCase().includes(q) ||
    i.description?.toLowerCase().includes(q) ||
    i.supplierName?.toLowerCase().includes(q)
  )
})

onMounted(async () => {
  await Promise.all([loadItems(), loadSuppliers()])
})

async function loadItems() {
  loading.value = true
  try {
    items.value = await api.get<any[]>('/partnumbers')
  } catch {}
  finally { loading.value = false }
}

async function loadSuppliers() {
  try {
    supplierOptions.value = await api.get<any[]>('/suppliers')
  } catch {}
}

function openDialog(item?: any) {
  if (item) {
    editingId.value = item.id
    form.value = { name: item.name, description: item.description || '', supplierId: item.supplierId || null }
  } else {
    editingId.value = null
    form.value = { name: '', description: '', supplierId: null }
  }
  showDialog.value = true
}

async function save() {
  saving.value = true
  try {
    if (editingId.value) {
      await api.put(`/partnumbers/${editingId.value}`, form.value)
    } else {
      await api.post('/partnumbers', form.value)
    }
    showDialog.value = false
    await loadItems()
  } catch {}
  finally { saving.value = false }
}

async function deleteItem(id: number) {
  if (!confirm('Delete this part number?')) return
  try {
    await api.del(`/partnumbers/${id}`)
    await loadItems()
  } catch {}
}
</script>
