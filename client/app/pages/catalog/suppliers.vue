<template>
  <div>
    <PageHeader title="Suppliers" back-to="/catalog" :count="items.length">
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
        <v-data-table :headers="headers" :items="filteredItems" :loading="loading" :items-per-page="50" hover>
          <template #item.isActive="{ item }">
            <StatusChip :status="item.isActive ? 'Active' : 'Inactive'" />
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
const {
  items, loading, saving, search, showDialog,
  isEditing, filteredItems, form,
  loadItems, openDialog, save: saveCrud, deleteItem,
} = useCrud('/suppliers', {
  defaultForm: () => ({ name: '', username: '', dependency: '', description: '', email: '', phone: '', address: '' }),
  searchFields: ['name', 'username', 'email', 'phone'],
})

const dependencyOptions = ['Normal','Certificated', 'NoQuote', 'EndUser']

async function save() {
  if (!dependencyOptions.includes(form.value.dependency)) return
  await saveCrud()
}

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Username', key: 'username' },
  { title: 'Dependency', key: 'dependency' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone' },
  { title: 'Address', key: 'address' },

  { title: 'Status', key: 'isActive', width: '100px' },
  { title: '', key: 'actions', sortable: false, width: '100px' },
]

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

onMounted(() => loadItems())
</script>
