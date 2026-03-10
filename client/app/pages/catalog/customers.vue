<template>
  <div>
    <PageHeader title="Customers" back-to="/catalog" :count="items.length">
      <template #actions>
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openDialog()">Add Customer</v-btn>
      </template>
    </PageHeader>

    <v-card class="glass-card">
      <v-card-text>
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Search customers..."
          single-line
          hide-details
          class="mb-4"
        />
        <v-data-table :headers="headers" :items="filteredItems" :loading="loading" items-per-page="15" hover>
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
      :title="isEditing ? 'Edit Customer' : 'New Customer'"
      :loading="saving"
      @save="save"
    >
      <v-text-field v-model="form.name" label="Name" class="mb-2" />
      <v-text-field v-model="form.customerCode" label="Customer Code" class="mb-2" />
      <v-text-field v-model="form.email" label="Email" class="mb-2" />
      <v-text-field v-model="form.phone" label="Phone" class="mb-2" />
      <v-text-field v-model="form.shipTo" label="Ship To" class="mb-2" />
      <v-text-field v-model="form.billTo" label="Bill To" />
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
const {
  items, loading, saving, search, showDialog, editingId, form,
  isEditing, filteredItems,
  loadItems, openDialog, save, deleteItem,
} = useCrud('/customers', {
  defaultForm: () => ({ name: '', customerCode: '', email: '', phone: '', shipTo: '', billTo: '' }),
  searchFields: ['name', 'customerCode', 'email', 'phone'],
})

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Code', key: 'customerCode' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone' },
  { title: 'Ship To', key: 'shipTo' },
  { title: 'Bill To', key: 'billTo' },
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

// ─── Init ───
onMounted(() => loadItems())
</script>
