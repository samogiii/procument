<template>
  <v-container>
    <v-row>
      <v-col cols="12">
        <h1 class="text-h4 mb-4">Satellite Sync Management</h1>
      </v-col>
    </v-row>

    <v-row>
      <v-col cols="12">
        <v-card>
          <v-card-title class="d-flex align-center">
            Satellite Nodes
            <v-spacer></v-spacer>
            <v-btn color="primary" @click="openDialog()">Add Node</v-btn>
          </v-card-title>
          <v-data-table :headers="headers" :items="nodes" :loading="loading">
            <template #[`item.lastSyncAt`]="{ item }">
              {{ item.lastSyncAt ? new Date(item.lastSyncAt).toLocaleString() : 'Never' }}
            </template>
            <template #[`item.actions`]="{ item }">
              <v-btn
                color="success"
                size="small"
                class="mr-2"
                :loading="syncingId === item.id"
                @click="triggerSync(item.id)"
              >
                Sync Base {{ item.baseNumber }}
              </v-btn>
              <v-btn color="info" size="small" class="mr-2" @click="openDialog(item)">Edit</v-btn>
              <v-btn color="error" size="small" @click="deleteNode(item.id)">Delete</v-btn>
            </template>
          </v-data-table>
        </v-card>
      </v-col>
    </v-row>

    <!-- Add/Edit Dialog -->
    <v-dialog v-model="dialog" max-width="600px">
      <v-card>
        <v-card-title>{{ editedItem.id ? 'Edit Node' : 'New Node' }}</v-card-title>
        <v-card-text>
          <v-container>
            <v-row>
              <v-col cols="12" md="6">
                <v-text-field v-model="editedItem.name" label="Node Name"></v-text-field>
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="editedItem.baseNumber"
                  :items="[2, 5]"
                  label="Customer Base"
                ></v-select>
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="editedItem.endpointUrl" label="Endpoint URL"></v-text-field>
              </v-col>
              <v-col cols="12">
                <v-textarea
                  v-model="editedItem.publicKey"
                  label="Satellite RSA Public Key (PEM)"
                  rows="5"
                ></v-textarea>
              </v-col>
            </v-row>
          </v-container>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn color="grey" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" @click="saveNode">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Keys Dialog -->
    <v-card class="mt-4">
      <v-card-title>Main App Keys</v-card-title>
      <v-card-text>
        <p class="text-caption mb-2">
          These keys are used for signing and decrypting sync data. Ensure the Private Key is kept
          secure in your appsettings.json.
        </p>
        <v-btn color="secondary" size="small" @click="generateKeys">Generate New Key Pair</v-btn>
        <div v-if="generatedKeys" class="mt-4">
          <v-textarea
            v-model="generatedKeys.publicKey"
            label="Main App Public Key (Share with Satellites)"
            readonly
            rows="3"
          ></v-textarea>
          <v-textarea
            v-model="generatedKeys.privateKey"
            label="Main App Private Key (Keep Secret)"
            readonly
            rows="5"
          ></v-textarea>
        </div>
      </v-card-text>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="5000">
      {{ snackbarText }}
    </v-snackbar>
  </v-container>
</template>

<script setup lang="ts">
const api = useApi()
const loading = ref(false)
const nodes = ref([])
const syncingId = ref<number | null>(null)

const dialog = ref(false)
const editedItem = ref({
  id: 0,
  name: '',
  baseNumber: 2,
  endpointUrl: '',
  publicKey: '',
})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')

const headers = [
  { title: 'Name', key: 'name' },
  { title: 'Base', key: 'baseNumber' },
  { title: 'Endpoint', key: 'endpointUrl' },
  { title: 'Last Sync', key: 'lastSyncAt' },
  { title: 'Actions', key: 'actions', sortable: false },
]

const generatedKeys = ref<{ privateKey: string; publicKey: string } | null>(null)

async function loadNodes() {
  loading.value = true
  try {
    nodes.value = await api.get('/sync/nodes')
  } catch (e: any) {
    showError(e.message)
  } finally {
    loading.value = false
  }
}

function openDialog(item?: any) {
  if (item) {
    editedItem.value = { ...item }
  } else {
    editedItem.value = { id: 0, name: '', baseNumber: 2, endpointUrl: '', publicKey: '' }
  }
  dialog.value = true
}

async function saveNode() {
  try {
    if (editedItem.value.id) {
      await api.put(`/sync/nodes/${editedItem.value.id}`, editedItem.value)
    } else {
      await api.post('/sync/nodes', editedItem.value)
    }
    dialog.value = false
    loadNodes()
    showSuccess('Node saved successfully')
  } catch (e: any) {
    showError(e.message)
  }
}

async function deleteNode(id: number) {
  if (!confirm('Are you sure you want to delete this node?')) return
  try {
    await api.delete(`/sync/nodes/${id}`)
    loadNodes()
    showSuccess('Node deleted')
  } catch (e: any) {
    showError(e.message)
  }
}

async function triggerSync(nodeId: number) {
  syncingId.value = nodeId
  try {
    const res = await api.post(`/sync/trigger/${nodeId}`)
    showSuccess(res.message)
    loadNodes()
  } catch (e: any) {
    showError(e.message)
  } finally {
    syncingId.value = null
  }
}

async function generateKeys() {
  if (
    !confirm(
      'Generating new keys will break existing syncs until you update your appsettings.json and Satellites. Continue?'
    )
  )
    return
  try {
    generatedKeys.value = await api.get('/sync/generate-keys')
  } catch (e: any) {
    showError(e.message)
  }
}

function showSuccess(msg: string) {
  snackbarText.value = msg
  snackbarColor.value = 'success'
  snackbar.value = true
}

function showError(msg: string) {
  snackbarText.value = msg
  snackbarColor.value = 'error'
  snackbar.value = true
}

onMounted(loadNodes)
</script>
