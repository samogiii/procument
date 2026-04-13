<template>
  <div>
    <PageHeader title="Supplier Requests" back-to="/catalog">
      <template #actions>
        <v-btn prepend-icon="mdi-refresh" variant="tonal" size="small" @click="load">Refresh</v-btn>
      </template>
    </PageHeader>

    <v-row class="mb-4" dense>
      <v-col cols="6" md="3">
        <v-card class="glass-card pa-4 text-center">
          <p class="text-caption text-medium-emphasis mb-1">Pending</p>
          <p class="text-h5 font-weight-bold text-warning">{{ pending.length }}</p>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card class="glass-card pa-4 text-center">
          <p class="text-caption text-medium-emphasis mb-1">Rejected (Awaiting Resubmission)</p>
          <p class="text-h5 font-weight-bold text-error">{{ rejected.length }}</p>
        </v-card>
      </v-col>
    </v-row>

    <v-tabs v-model="tab" class="mb-4">
      <v-tab value="pending">
        Pending
        <v-badge v-if="pending.length" :content="String(pending.length)" color="warning" inline class="ml-2" />
      </v-tab>
      <v-tab value="rejected">
        Rejected / Awaiting Resubmission
        <v-badge v-if="rejected.length" :content="String(rejected.length)" color="error" inline class="ml-2" />
      </v-tab>
    </v-tabs>

    <!-- Pending Tab -->
    <v-card v-if="tab === 'pending'" class="glass-card">
      <v-card-text>
        <v-data-table
          :headers="pendingHeaders"
          :items="pending"
          :loading="loading"
          :items-per-page="50"
          hover
          no-data-text="No pending supplier requests"
        >
          <template #item.createdAt="{ item }">
            {{ item.createdAt ? new Date(item.createdAt).toLocaleString() : '—' }}
          </template>
          <template #item.actions="{ item }">
            <v-btn
              size="x-small"
              color="success"
              variant="tonal"
              prepend-icon="mdi-check"
              class="mr-2"
              :loading="actionLoading === item.id + '-approve'"
              @click="approve(item.id)"
            >
              Approve
            </v-btn>
            <v-btn
              size="x-small"
              color="error"
              variant="tonal"
              prepend-icon="mdi-close"
              :loading="actionLoading === item.id + '-reject'"
              @click="reject(item.id)"
            >
              Reject
            </v-btn>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Rejected Tab -->
    <v-card v-else class="glass-card">
      <v-card-text>
        <v-alert type="info" variant="tonal" class="mb-4" density="compact">
          These suppliers were rejected. The user has been notified to correct and resubmit.
        </v-alert>
        <v-data-table
          :headers="rejectedHeaders"
          :items="rejected"
          :loading="loading"
          :items-per-page="50"
          hover
          no-data-text="No rejected suppliers awaiting resubmission"
        >
          <template #item.createdAt="{ item }">
            {{ item.createdAt ? new Date(item.createdAt).toLocaleString() : '—' }}
          </template>
          <template #item.status="{ item }">
            <v-chip color="error" size="x-small">Rejected — Awaiting User Correction</v-chip>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <v-snackbar v-model="snack" :color="snackColor" timeout="3000">{{ snackMsg }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const loading = ref(false)
const tab = ref('pending')
const items = ref<any[]>([])
const actionLoading = ref<string | null>(null)

const pending = computed(() => items.value.filter(i => i.status === 'Pending'))
const rejected = computed(() => items.value.filter(i => i.status === 'Rejected'))

const pendingHeaders = [
  { title: 'Supplier Name', key: 'name' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone' },
  { title: 'Requested At', key: 'createdAt' },
  { title: '', key: 'actions', sortable: false, width: '200px', align: 'end' as const },
]

const rejectedHeaders = [
  { title: 'Supplier Name', key: 'name' },
  { title: 'Email', key: 'email' },
  { title: 'Phone', key: 'phone' },
  { title: 'Rejected At', key: 'createdAt' },
  { title: 'Status', key: 'status', sortable: false },
]

const snack = ref(false)
const snackMsg = ref('')
const snackColor = ref('success')

function showSnack(msg: string, color = 'success') {
  snackMsg.value = msg
  snackColor.value = color
  snack.value = true
}

async function load() {
  loading.value = true
  try {
    items.value = await api.get<any[]>('/suppliers/pending')
  } catch {
    showSnack('Failed to load supplier requests', 'error')
  } finally {
    loading.value = false
  }
}

async function approve(id: number) {
  actionLoading.value = `${id}-approve`
  try {
    await api.post(`/suppliers/${id}/approve`, {})
    showSnack('Supplier approved successfully', 'success')
    await load()
  } catch {
    showSnack('Failed to approve supplier', 'error')
  } finally {
    actionLoading.value = null
  }
}

async function reject(id: number) {
  actionLoading.value = `${id}-reject`
  try {
    await api.post(`/suppliers/${id}/reject`, {})
    showSnack('Supplier rejected — user must correct the name', 'warning')
    await load()
  } catch {
    showSnack('Failed to reject supplier', 'error')
  } finally {
    actionLoading.value = null
  }
}

onMounted(load)
</script>
