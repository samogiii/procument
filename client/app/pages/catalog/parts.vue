<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div class="d-flex align-center">
        <v-btn icon="mdi-arrow-left" variant="text" to="/catalog" class="mr-2" />
        <h1 class="text-h5 font-weight-bold">Part Numbers</h1>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="showDialog = true">Add Part</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search parts..." single-line hide-details class="mb-4" />
        <v-data-table-server :headers="headers" :items="items" :items-length="totalItems" :loading="loading" :items-per-page="10" @update:options="loadItems">
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" variant="text" size="x-small" />
          </template>
        </v-data-table-server>
      </v-card-text>
    </v-card>

    <v-dialog v-model="showDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title>New Part Number</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="Part Number" class="mb-2" />
          <v-text-field v-model="form.description" label="Description" class="mb-2" />
          <v-text-field v-model="form.supplierId" label="Supplier ID" type="number" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showDialog = false">Cancel</v-btn>
          <v-btn color="primary" @click="save">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const search = ref('')
const loading = ref(false)
const items = ref<any[]>([])
const totalItems = ref(0)
const showDialog = ref(false)
const form = ref({ name: '', description: '', supplierId: '' })

const headers = [
  { title: 'Part Number', key: 'name' },
  { title: 'Description', key: 'description' },
  { title: 'Supplier', key: 'supplierName' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]

async function loadItems(options: any) {
  loading.value = true
  try {
    const res = await api.get<any>(`/parts?page=${options.page}&pageSize=${options.itemsPerPage}`)
    items.value = res.items || []
    totalItems.value = res.totalCount || 0
  } catch {}
  finally { loading.value = false }
}

async function save() {
  try {
    await api.post('/parts', form.value)
    showDialog.value = false
    form.value = { name: '', description: '', supplierId: '' }
  } catch {}
}
</script>
