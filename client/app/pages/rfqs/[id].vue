<template>
  <div>
    <div class="d-flex align-center mb-6">
      <v-btn icon="mdi-arrow-left" variant="text" to="/rfqs" class="mr-2" />
      <h1 class="text-h5 font-weight-bold">RFQ #{{ route.params.id }}</h1>
      <v-spacer />
      <v-chip :color="statusColor" class="ml-2">{{ rfq.status || 'Open' }}</v-chip>
    </div>

    <!-- Header Info -->
    <v-row class="mb-6">
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Customer</p>
          <p class="text-body-1 font-weight-medium">{{ rfq.customerName || '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Lead Time</p>
          <p class="text-body-1 font-weight-medium">{{ rfq.leadTime ? new Date(rfq.leadTime).toLocaleDateString() : '—' }}</p>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card class="glass-card pa-4">
          <p class="text-caption text-medium-emphasis">Assigned To</p>
          <p class="text-body-1 font-weight-medium">{{ rfq.userName || '—' }}</p>
        </v-card>
      </v-col>
    </v-row>

    <!-- Line Items -->
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-format-list-bulleted" class="mr-2" size="20" />
        Line Items
        <v-spacer />
        <v-btn color="primary" size="small" prepend-icon="mdi-plus" variant="outlined">Add Item</v-btn>
      </v-card-title>
      <v-card-text>
        <v-data-table
          :headers="itemHeaders"
          :items="rfq.items || []"
          density="comfortable"
        >
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" variant="text" size="x-small" />
            <v-btn icon="mdi-delete" variant="text" size="x-small" color="error" />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()

const rfq = ref<any>({})

const statusColor = computed(() => {
  const s = rfq.value.status?.toLowerCase()
  if (s === 'closed') return 'success'
  if (s === 'cancelled') return 'error'
  return 'primary'
})

const itemHeaders = [
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Alt', key: 'alt' },
  { title: 'Qty', key: 'qty' },
  { title: 'Condition', key: 'condition' },
  { title: '', key: 'actions', sortable: false, width: '100px' },
]

onMounted(async () => {
  try {
    rfq.value = await api.get(`/rfqs/${route.params.id}`)
  } catch { /* API not connected */ }
})
</script>
