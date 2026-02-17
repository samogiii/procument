<template>
  <div>
    <PageHeader title="Part Numbers" back-to="/catalog" :count="items.length">
      <template #actions>
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openDialog()">Add Part</v-btn>
      </template>
    </PageHeader>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search parts..."
          single-line
          hide-details
          class="mb-4"
        />
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
            <v-btn icon="mdi-delete" variant="text" size="x-small" color="error" @click="confirmDelete(item.id)" />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <CrudDialog
      v-model="showDialog"
      :title="isEditing ? 'Edit Part Number' : 'New Part Number'"
      :loading="saving"
      @save="save"
    >
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
    </CrudDialog>

    <ConfirmDialog
      v-model="showConfirm"
      title="Delete Part Number?"
      message="This action cannot be undone."
      @confirm="doDelete"
    />
  </div>
</template>

<script setup lang="ts">
const api = useApi()

const {
  items, loading, saving, search, showDialog,
  isEditing, filteredItems, form,
  loadItems, openDialog, save, deleteItem,
} = useCrud('/partnumbers', {
  defaultForm: () => ({ name: '', description: '', supplierId: null as number | null }),
  searchFields: ['name', 'description', 'supplierName'],
})

const headers = [
  { title: 'Part Number', key: 'name' },
  { title: 'Description', key: 'description' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Alternatives', key: 'alternatives', sortable: false },
  { title: '', key: 'actions', sortable: false, width: '100px' },
]

// ─── Supplier options for the autocomplete ───
const supplierOptions = ref<any[]>([])

async function loadSuppliers() {
  try {
    supplierOptions.value = await api.get<any[]>('/suppliers')
  } catch { /* silently fail */ }
}

// ─── Delete with confirmation ───
const showConfirm = ref(false)
const deleteTarget = ref<number | null>(null)

function confirmDelete(id: number) {
  deleteTarget.value = id
  showConfirm.value = true
}

async function doDelete() {
  if (deleteTarget.value) await deleteItem(deleteTarget.value)
  deleteTarget.value = null
}

onMounted(async () => {
  await Promise.all([loadItems(), loadSuppliers()])
})
</script>
