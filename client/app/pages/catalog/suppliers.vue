<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-6">
      <div class="d-flex align-center">
        <v-btn icon="mdi-arrow-left" variant="text" to="/catalog" class="mr-2" />
        <h1 class="text-h5 font-weight-bold">Suppliers</h1>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="showDialog = true">Add Supplier</v-btn>
    </div>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Search suppliers..." single-line hide-details class="mb-4" />
        <v-data-table-server :headers="headers" :items="items" :items-length="totalItems" :loading="loading" :items-per-page="10" @update:options="loadItems">
          <template #item.isActive="{ item }">
            <v-chip :color="item.isActive ? 'success' : 'grey'" size="small">{{ item.isActive ? 'Active' : 'Inactive' }}</v-chip>
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" variant="text" size="x-small" />
          </template>
        </v-data-table-server>
      </v-card-text>
    </v-card>

    <v-dialog v-model="showDialog" max-width="500">
      <v-card class="glass-card">
        <v-card-title>New Supplier</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="Name" class="mb-2" />
          <v-text-field v-model="form.email" label="Email" class="mb-2" />
          <v-text-field v-model="form.phone" label="Phone" class="mb-2" />
          <v-text-field v-model="form.address" label="Address" />
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
const form = ref({ name: '', email: '', phone: '', address: '' })

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone' },
  { title: 'Status', key: 'isActive' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]

async function loadItems(options: any) {
  loading.value = true
  try {
    const res = await api.get<any>(`/suppliers?page=${options.page}&pageSize=${options.itemsPerPage}`)
    items.value = res.items || []
    totalItems.value = res.totalCount || 0
  } catch {}
  finally { loading.value = false }
}

async function save() {
  try {
    await api.post('/suppliers', form.value)
    showDialog.value = false
    form.value = { name: '', email: '', phone: '', address: '' }
  } catch {}
}
</script>
