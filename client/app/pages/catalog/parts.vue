<template>
  <div>
    <PageHeader title="Part Numbers" back-to="/catalog" :count="items.length">
      <template #actions>
        <v-btn color="secondary" variant="tonal" prepend-icon="mdi-upload" @click="showBulk = true" class="mr-2">Bulk Upload</v-btn>
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

    <!-- Single Part Dialog -->
    <CrudDialog
      v-model="showDialog"
      :title="isEditing ? 'Edit Part Number' : 'New Part Number'"
      :loading="saving"
      @save="save"
    >
      <v-text-field v-model="form.name" label="Part Number" class="mb-2" />
      <v-text-field v-model="form.description" label="Description" class="mb-2" />
      <v-text-field v-model="form.fleet" label="Fleet" class="mb-2" />
      <v-text-field v-model="form.remark" label="Remark" />
    </CrudDialog>

    <!-- Bulk Upload Dialog -->
    <v-dialog v-model="showBulk" max-width="900" persistent scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-upload" color="secondary" class="mr-2" />
          Bulk Upload Parts
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeBulk" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-alert type="info" variant="tonal" density="compact" class="mb-4">
            Paste rows from Excel. Columns: <strong>Part Number | Description | Fleet | Alternatives</strong> (comma-separated)
          </v-alert>

          <v-textarea
            v-model="bulkPasteText"
            label="Paste from Excel here..."
            placeholder="PN-001&#9;Some description&#9;B737&#9;ALT-001,ALT-002"
            rows="6"
            variant="outlined"
            class="mb-3"
            @paste="onBulkPaste"
          />

          <v-btn color="secondary" variant="tonal" size="small" prepend-icon="mdi-table-arrow-down" @click="parseBulkPaste" class="mb-4">
            Parse Rows
          </v-btn>

          <div v-if="bulkRows.length > 0">
            <p class="text-caption text-medium-emphasis mb-2">{{ bulkRows.length }} rows parsed</p>
            <div style="max-height: 350px; overflow-y: auto;">
              <table class="bulk-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Part Number</th>
                    <th>Description</th>
                    <th>Fleet</th>
                    <th>Alternatives</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, idx) in bulkRows" :key="idx">
                    <td class="text-medium-emphasis">{{ idx + 1 }}</td>
                    <td><input class="bulk-input" v-model="row.name" placeholder="Part Number" /></td>
                    <td><input class="bulk-input" v-model="row.description" placeholder="Description" /></td>
                    <td><input class="bulk-input" v-model="row.fleet" placeholder="Fleet" /></td>
                    <td><input class="bulk-input" v-model="row.alternatives" placeholder="ALT1,ALT2" /></td>
                    <td>
                      <v-btn icon="mdi-close" variant="text" size="x-small" color="error" @click="bulkRows.splice(idx, 1)" />
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <v-alert v-if="bulkResult" :type="bulkResult.type" variant="tonal" density="compact" class="mt-3">
            {{ bulkResult.message }}
          </v-alert>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-btn variant="text" size="small" prepend-icon="mdi-plus" @click="bulkRows.push({ name: '', description: '', fleet: '', alternatives: '' })">
            Add Row
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="closeBulk">Cancel</v-btn>
          <v-btn color="primary" :loading="bulkSaving" :disabled="bulkRows.length === 0" @click="submitBulk">
            Upload {{ bulkRows.length }} Parts
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

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
  defaultForm: () => ({ name: '', description: '', fleet: '', remark: '' }),
  searchFields: ['name', 'description', 'fleet'],
})

const headers = [
  { title: 'Part Number', key: 'name' },
  { title: 'Description', key: 'description' },
  { title: 'Fleet', key: 'fleet' },
  { title: 'Alternatives', key: 'alternatives', sortable: false },
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

// ─── Bulk Upload ───
const showBulk = ref(false)
const bulkPasteText = ref('')
const bulkRows = ref<{ name: string; description: string; fleet: string; alternatives: string }[]>([])
const bulkSaving = ref(false)
const bulkResult = ref<{ type: 'success' | 'error'; message: string } | null>(null)

function onBulkPaste() {
  nextTick(() => parseBulkPaste())
}

function parseBulkPaste() {
  const text = bulkPasteText.value.trim()
  if (!text) return

  const lines = text.split('\n').filter(l => l.trim())
  const parsed: typeof bulkRows.value = []

  for (const line of lines) {
    const cols = line.split('\t')
    const name = (cols[0] || '').trim()
    if (!name) continue
    parsed.push({
      name,
      description: (cols[1] || '').trim(),
      fleet: (cols[2] || '').trim(),
      alternatives: (cols[3] || '').trim(),
    })
  }

  bulkRows.value = parsed
  bulkResult.value = null
}

async function submitBulk() {
  bulkSaving.value = true
  bulkResult.value = null
  try {
    const parts = bulkRows.value
      .filter(r => r.name.trim())
      .map(r => ({
        name: r.name.trim(),
        description: r.description || null,
        fleet: r.fleet || null,
        remark: null,
        alternatives: r.alternatives
          ? r.alternatives.split(',').map(a => a.trim()).filter(Boolean)
          : [],
      }))

    const res = await api.post<{ created: number; skipped: number; total: number }>('/partnumbers/bulk', { parts })
    bulkResult.value = {
      type: 'success',
      message: `Done! ${res.created} created, ${res.skipped} updated/skipped out of ${res.total} total.`,
    }
    await loadItems()
  } catch (e: any) {
    bulkResult.value = { type: 'error', message: e?.data?.message || 'Bulk upload failed.' }
  } finally {
    bulkSaving.value = false
  }
}

function closeBulk() {
  showBulk.value = false
  bulkPasteText.value = ''
  bulkRows.value = []
  bulkResult.value = null
}

onMounted(() => loadItems())
</script>

<style scoped>
.bulk-table {
  width: 100%;
  border-collapse: collapse;
}
.bulk-table th,
.bulk-table td {
  padding: 4px 6px;
  border-bottom: 1px solid var(--card-border);
  font-size: 0.82rem;
}
.bulk-table th {
  text-align: left;
  opacity: 0.6;
  font-weight: 600;
  font-size: 0.72rem;
  text-transform: uppercase;
}
.bulk-input {
  width: 100%;
  background: var(--row-hover);
  border: 1px solid var(--card-border);
  border-radius: 4px;
  padding: 4px 8px;
  color: inherit;
  font-size: 0.82rem;
}
.bulk-input:focus {
  outline: none;
  border-color: rgb(var(--v-theme-primary));
}
</style>
