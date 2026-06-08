<template>
  <div>
    <div class="d-flex align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Shipping</h1>
        <p class="text-caption text-medium-emphasis mt-1">Track numbers arriving at your warehouse(s)</p>
      </div>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-refresh" variant="tonal" size="small" :loading="loading" @click="loadTracks">Refresh</v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <div v-if="!loading && grouped.length === 0" class="text-center pa-12 text-medium-emphasis">
      <v-icon icon="mdi-truck-check-outline" size="64" color="grey" class="mb-3" />
      <p>No track numbers assigned to your warehouse(s) yet.</p>
    </div>

    <v-card v-else>
      <v-data-table
        :headers="headers"
        :items="grouped"
        :loading="loading"
        item-value="key"
        class="elevation-0"
        hover
        @click:row="(_: any, { item }: any) => goToGroup(item)"
      >
        <!-- Track number -->
        <template #item.trackNumber="{ item }">
          <span class="font-weight-bold text-pn">{{ item.trackNumber }}</span>
        </template>

        <!-- Parts chips -->
        <template #item.parts="{ item }">
          <div class="d-flex gap-1 flex-wrap py-1">
            <v-chip
              v-for="p in item.parts"
              :key="p.trackId"
              size="x-small"
              variant="tonal"
              color="default"
            >
              {{ p.partName || '—' }}
            </v-chip>
          </div>
        </template>

        <!-- Overall review status -->
        <template #item.inventoryStatus="{ item }">
          <div class="d-flex gap-1 flex-wrap">
            <v-chip
              v-for="p in item.parts"
              :key="p.trackId"
              size="x-small"
              :color="p.reviewStatus === 'Accepted' ? 'success' : p.reviewStatus === 'Rejected' ? 'error' : p.reviewStatus === 'Pending' ? 'warning' : 'default'"
              variant="tonal"
            >
              {{ p.reviewStatus || 'Not submitted' }}
            </v-chip>
          </div>
        </template>

        <!-- Track shipping status -->
        <template #item.status="{ item }">
          <v-chip
            size="x-small"
            variant="tonal"
            :color="trackStatusColor(item.worstStatus)"
          >
            {{ item.worstStatus }}
          </v-chip>
        </template>

        <template #item.actions="{ item }">
          <v-btn icon="mdi-chevron-right" size="x-small" variant="text" color="primary" @click.stop="goToGroup(item)" />
        </template>
      </v-data-table>
    </v-card>

    <v-snackbar v-model="snack" :color="snackColor" timeout="3000" location="top right">{{ snackMsg }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const api = useApi()
const router = useRouter()

const headers = [
  { title: 'Track Number', key: 'trackNumber', sortable: true },
  { title: 'Carrier', key: 'carrier', sortable: false },
  { title: 'Warehouse', key: 'warehouseName', sortable: false },
  { title: 'Parts', key: 'parts', sortable: false },
  { title: 'Review Status', key: 'inventoryStatus', sortable: false },
  { title: 'Shipping Status', key: 'status', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]

const tracks = ref<any[]>([])
const loading = ref(false)

const snack = ref(false)
const snackMsg = ref('')
const snackColor = ref('success')
function notify(msg: string, color = 'success') {
  snackMsg.value = msg; snackColor.value = color; snack.value = true
}

const statusRank: Record<string, number> = {
  'Rejected': 0,
  'Ship to Warehouse': 1,
  'Received in Warehouse': 2,
  'Waiting for Packing': 3,
  'Ship To USA': 4,
  'Clearing Customs': 5,
  'Received in Office': 6,
  'Delivered to Customer': 7,
}

function trackStatusColor(status: string) {
  const map: Record<string, string> = {
    'Ship to Warehouse': 'blue-grey',
    'Received in Warehouse': 'orange',
    'Waiting for Packing': 'amber',
    'Ship To USA': 'blue',
    'Clearing Customs': 'deep-purple',
    'Received in Office': 'teal',
    'Delivered to Customer': 'success',
    'Rejected': 'error',
  }
  return map[status] ?? 'default'
}

// Group tracks that share the same trackNumber + carrier + warehouseId
const grouped = computed(() => {
  const map = new Map<string, any>()
  for (const t of tracks.value) {
    const key = `${t.trackNumber}||${t.carrier ?? ''}||${t.warehouseId ?? ''}`
    if (!map.has(key)) {
      map.set(key, {
        key,
        primaryId: t.id,
        trackNumber: t.trackNumber,
        carrier: t.carrier,
        warehouseId: t.warehouseId,
        warehouseName: t.warehouseName,
        parts: [],
        worstStatus: t.status,
      })
    }
    const group = map.get(key)!
    group.parts.push({
      trackId: t.id,
      partName: t.partNumberName,
      reviewStatus: t.items?.[0]?.status ?? null,
    })
    // Keep worst (lowest rank) shipping status
    if ((statusRank[t.status] ?? 99) < (statusRank[group.worstStatus] ?? 99)) {
      group.worstStatus = t.status
    }
  }
  return [...map.values()]
})

function goToGroup(group: any) {
  router.push(`/shipping/track-numbers/${group.primaryId}`)
}

async function loadTracks() {
  loading.value = true
  try {
    tracks.value = await api.get('/shipping/track-numbers')
  } catch {
    notify('Failed to load tracks', 'error')
  } finally {
    loading.value = false
  }
}

onMounted(loadTracks)
</script>
