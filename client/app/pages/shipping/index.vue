<template>
  <div>
    <div class="d-flex align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Shipping</h1>
        <p class="text-caption text-medium-emphasis mt-1">
          {{ showAll ? 'Track numbers at your warehouse(s) — pick Ship To USA or later in Shipping Status to see those too' : 'Track numbers on their way to your warehouse(s)' }}
        </p>
      </div>
      <v-spacer />
      <!-- Received / later tracks are hidden by default; "All" brings them back so documents can still be added -->
      <v-btn-toggle v-model="showAll" mandatory density="compact" variant="outlined" color="primary" divided>
        <v-btn :value="false" size="small">Pending</v-btn>
        <v-btn :value="true" size="small">All</v-btn>
      </v-btn-toggle>
      <v-btn
        v-if="hasActiveFilters"
        color="error"
        prepend-icon="mdi-filter-off"
        variant="text"
        size="small"
        @click="clearFilters"
      >
        Clear filters
      </v-btn>
      <v-btn color="primary" prepend-icon="mdi-refresh" variant="tonal" size="small" :loading="loading" @click="loadTracks">Refresh</v-btn>
      <!-- Sends open tracks to the warehouse app and brings back its photos, qty, boxes and notes -->
      <v-btn
        v-if="warehouseSyncEnabled"
        color="primary"
        prepend-icon="mdi-cloud-sync-outline"
        variant="flat"
        size="small"
        :loading="syncing"
        @click="syncWarehouse"
      >
        Sync warehouse
      </v-btn>
    </div>

    <v-alert v-if="syncing" type="info" variant="tonal" density="compact" class="mb-4">
      {{ syncProgress }}
    </v-alert>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <div v-if="!loading && grouped.length === 0" class="text-center pa-12 text-medium-emphasis">
      <v-icon icon="mdi-truck-check-outline" size="64" color="grey" class="mb-3" />
      <p>{{ showAll ? 'No track numbers for your warehouse(s).' : 'No track numbers waiting to arrive at your warehouse(s).' }}</p>
    </div>

    <v-card v-else>
      <v-data-table
        :headers="headers"
        :items="displayed"
        :loading="loading"
        v-model:items-per-page="itemsPerPage"
        :items-per-page-options="pageOptions"
        item-value="key"
        class="elevation-0"
        hover
        @click:row="(_: any, { item }: any) => goToGroup(item)"
      >
        <!-- ── Excel-style filter + sort headers ── -->
        <template #header.warehouseName="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="warehouseName"
            :label="column.title"
            :options="cfWarehouseOptions"
            :selected="colFilter.selected['warehouseName'] || new Set()"
            :search="colFilter.search['warehouseName'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('warehouseName', v)"
            @select-all="() => colFilter.selectAll('warehouseName', cfWarehouseOptions)"
            @clear-all="() => colFilter.clearAll('warehouseName')"
            @update:search="(v) => colFilter.search['warehouseName'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.parts="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="parts"
            :label="column.title"
            :options="cfPartOptions"
            :selected="colFilter.selected['parts'] || new Set()"
            :search="colFilter.search['parts'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('parts', v)"
            @select-all="() => colFilter.selectAll('parts', cfPartOptions)"
            @clear-all="() => colFilter.clearAll('parts')"
            @update:search="(v) => colFilter.search['parts'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.inventoryStatus="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="inventoryStatus"
            :label="column.title"
            :options="cfReviewOptions"
            :selected="colFilter.selected['inventoryStatus'] || new Set()"
            :search="colFilter.search['inventoryStatus'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('inventoryStatus', v)"
            @select-all="() => colFilter.selectAll('inventoryStatus', cfReviewOptions)"
            @clear-all="() => colFilter.clearAll('inventoryStatus')"
            @update:search="(v) => colFilter.search['inventoryStatus'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.status="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="status"
            :label="column.title"
            :options="allShippingStatuses"
            :selected="colFilter.selected['status'] || new Set()"
            :search="colFilter.search['status'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('status', v)"
            @select-all="(vals) => colFilter.selectAll('status', vals)"
            @clear-all="() => colFilter.clearAll('status')"
            @update:search="(v) => colFilter.search['status'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #no-data>
          <div class="text-center pa-8 text-medium-emphasis">
            <v-icon icon="mdi-filter-remove-outline" size="40" color="grey" class="mb-2" />
            <p class="text-body-2">No track numbers match the current filters.</p>
            <v-btn size="small" variant="text" color="primary" class="mt-1" @click="clearFilters">Clear filters</v-btn>
          </div>
        </template>

        <!-- Track number -->
        <template #item.trackNumber="{ item }">
          <span class="font-weight-bold text-pn">{{ item.trackNumber }}</span>
        </template>

        <!-- Supplier(s) the PO(s) behind this track buy from -->
        <template #item.supplierName="{ item }">
          <span v-if="item.supplierNames.length" class="text-body-2">{{ item.supplierText }}</span>
          <span v-else class="text-caption text-medium-emphasis">—</span>
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

        <!-- Users assigned to the PO(s) behind this track -->
        <template #item.people="{ item }">
          <div v-if="item.people.length" class="d-flex gap-1 flex-wrap py-1">
            <v-chip
              v-for="u in item.people"
              :key="u.id"
              size="x-small"
              variant="tonal"
              color="primary"
              prepend-icon="mdi-account"
            >
              {{ u.name }}
            </v-chip>
          </div>
          <span v-else class="text-caption text-medium-emphasis">—</span>
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

        <!-- Track shipping status — one chip per distinct status when the parts are at different stages -->
        <template #item.status="{ item }">
          <div class="d-flex gap-1 flex-wrap py-1">
            <v-chip
              v-for="st in item.statuses"
              :key="st"
              size="x-small"
              variant="tonal"
              :color="trackStatusColor(st)"
            >
              {{ st }}
            </v-chip>
          </div>
        </template>

        <template #item.actions="{ item }">
          <v-btn icon="mdi-chevron-right" size="x-small" variant="text" color="primary" @click.stop="goToGroup(item)" />
        </template>
      </v-data-table>
    </v-card>

    <v-snackbar v-model="snack" :color="snackColor" timeout="3000" location="top right">{{ snackMsg }}</v-snackbar>

    <!-- Warehouse sync result -->
    <v-dialog v-model="syncDialog" max-width="480">
      <v-card v-if="syncResult">
        <v-card-title class="text-h6 pa-4 pb-2">
          <v-icon icon="mdi-cloud-check-outline" color="success" class="mr-2" />
          Warehouse synced
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-list density="compact" class="pa-0">
            <v-list-item prepend-icon="mdi-truck-outline" :title="`${syncResult.tracksSent} open track(s) sent to the warehouse`" />
            <v-list-item prepend-icon="mdi-image-multiple-outline" :title="`${syncResult.files} photo(s) / document(s) received`" />
            <v-list-item prepend-icon="mdi-counter" :title="`${syncResult.quantities} received qty saved`" />
            <v-list-item prepend-icon="mdi-package-variant-closed" :title="`${syncResult.boxes} box(es) saved${syncResult.deletedBoxes ? `, ${syncResult.deletedBoxes} removed` : ''}`" />
            <v-list-item v-if="syncResult.notes" prepend-icon="mdi-note-text-outline" :title="`${syncResult.notes} warehouse note(s) saved`" />
          </v-list>
          <v-alert v-if="syncResult.skipped.length" type="warning" variant="tonal" density="compact" class="mt-3">
            <div v-for="(s, i) in syncResult.skipped" :key="i" class="text-caption">{{ s }}</div>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn color="primary" variant="flat" @click="syncDialog = false">OK</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import type { WarehouseSyncSummary } from '~/composables/useWarehouseSync'

definePageMeta({ layout: 'default' })

const api = useApi()
const router = useRouter()

// Parts and Review Status hold arrays, and Shipping Status lives on `worstStatus`,
// so those three sort through sortRaw on the row rather than on the column key.
const headers = [
  { title: 'Track Number', key: 'trackNumber', sortable: true },
  // A group can span several POs, so Supplier holds a list and sorts through sortRaw.
  {
    title: 'Supplier',
    key: 'supplierName',
    sortable: true,
    sortRaw: (a: any, b: any) => a.supplierText.localeCompare(b.supplierText),
  },
  { title: 'Warehouse', key: 'warehouseName', sortable: true },
  {
    title: 'Parts',
    key: 'parts',
    sortable: true,
    sortRaw: (a: any, b: any) => a.partsText.localeCompare(b.partsText),
  },
  { title: 'Assigned Users', key: 'people', sortable: false },
  {
    title: 'Review Status',
    key: 'inventoryStatus',
    sortable: true,
    sortRaw: (a: any, b: any) => a.reviewText.localeCompare(b.reviewText),
  },
  {
    title: 'Shipping Status',
    key: 'status',
    sortable: true,
    // Ordered along the shipping ladder, not alphabetically — "Ship to Warehouse"
    // before "Delivered to Customer" is the useful order here.
    sortRaw: (a: any, b: any) => (statusRank[a.worstStatus] ?? 99) - (statusRank[b.worstStatus] ?? 99),
  },
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

// Pending lists only tracks still on their way (or rejected on arrival). All adds the
// warehouse stages; tracks from "Ship To USA" on stay hidden until their status is picked
// in the Shipping Status filter. The API already limits the list to the user's customer bases.
const visibleTracks = computed(() => {
  const picked = colFilter.selected['status']
  const limit = showAll.value ? statusRank['Ship To USA']! : statusRank['Received in Warehouse']!
  return tracks.value.filter(t => (statusRank[t.status] ?? 0) < limit || !!picked?.has(t.status))
})

// Every shipping status, in ladder order — the Status filter always offers all of them,
// so the hidden later stages can be picked straight away.
const allShippingStatuses = Object.keys(statusRank).sort((a, b) => statusRank[a]! - statusRank[b]!)

// At least 100 rows per page.
const pageOptions = [
  { value: 100, title: '100' },
  { value: 200, title: '200' },
  { value: 500, title: '500' },
  { value: -1, title: 'All' },
]
const itemsPerPage = ref(100)

// Group tracks that share the same trackNumber + carrier + warehouseId
const grouped = computed(() => {
  const map = new Map<string, any>()
  for (const t of visibleTracks.value) {
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
        // Rows in a group can belong to different POs, so assignees and suppliers
        // are merged across rows rather than taken from the first one.
        peopleById: new Map<number, any>(),
        supplierSet: new Set<string>(),
        statusSet: new Set<string>(),
        worstStatus: t.status,
      })
    }
    const group = map.get(key)!
    group.parts.push({
      trackId: t.id,
      partName: t.partNumberName,
      reviewStatus: t.items?.[0]?.status ?? null,
    })
    for (const u of t.assignedUsers ?? []) {
      if (!group.peopleById.has(u.id)) group.peopleById.set(u.id, u)
    }
    if (t.supplierName) group.supplierSet.add(t.supplierName)
    if (t.status) group.statusSet.add(t.status)
    // Keep worst (lowest rank) shipping status
    if ((statusRank[t.status] ?? 99) < (statusRank[group.worstStatus] ?? 99)) {
      group.worstStatus = t.status
    }
  }
  return [...map.values()].map(g => {
    const partNames = g.parts.map((p: any) => p.partName || '—')
    const reviewStatuses = g.parts.map((p: any) => p.reviewStatus || 'Not submitted')
    const supplierNames = [...g.supplierSet].sort((a: string, b: string) => a.localeCompare(b))
    return {
      ...g,
      people: [...g.peopleById.values()].sort((a: any, b: any) => a.name.localeCompare(b.name)),
      // Normalised values, shared by the filter menus and the sort comparators
      // so a row always filters and sorts on exactly what the cell renders.
      partNames,
      reviewStatuses,
      supplierNames,
      statuses: [...g.statusSet].sort((a: string, b: string) => (statusRank[a] ?? 99) - (statusRank[b] ?? 99)),
      partsText: [...partNames].sort().join(', '),
      reviewText: [...reviewStatuses].sort().join(', '),
      supplierText: supplierNames.join(', '),
    }
  })
})

// ── Excel-style column filters ────────────────────────────────────────────────

const colFilter = useColFilterPersisted('shipping')

function uniq(vals: string[]) {
  return [...new Set(vals)].sort((a, b) => a.localeCompare(b))
}

const cfWarehouseOptions = computed(() => uniq(grouped.value.map(g => g.warehouseName || '—')))
const cfPartOptions = computed(() => uniq(grouped.value.flatMap(g => g.partNames)))
const cfReviewOptions = computed(() => uniq(grouped.value.flatMap(g => g.reviewStatuses)))

const displayed = computed(() => {
  const wh = colFilter.selected['warehouseName']
  const pt = colFilter.selected['parts']
  const rv = colFilter.selected['inventoryStatus']
  const sh = colFilter.selected['status']

  return grouped.value.filter(g => {
    if (wh?.size && !wh.has(g.warehouseName || '—')) return false
    // A row covers several parts, so it survives when any one of them is selected.
    if (pt?.size && !g.partNames.some((n: string) => pt.has(n))) return false
    if (rv?.size && !g.reviewStatuses.some((s: string) => rv.has(s))) return false
    if (sh?.size && !g.statuses.some((s: string) => sh.has(s))) return false
    return true
  })
})

const CF_KEYS = ['warehouseName', 'parts', 'inventoryStatus', 'status']

const hasActiveFilters = computed(() => CF_KEYS.some(k => colFilter.isActive(k)))

function clearFilters() {
  for (const k of CF_KEYS) colFilter.clearAll(k)
}

// Kept in the URL (?view=all) so coming back from a track page keeps the same list.
const route = useRoute()
const showAll = ref(route.query.view === 'all')
watch(showAll, (all) => {
  router.replace({ query: { ...route.query, view: all ? 'all' : undefined } })
  loadTracks()
})

// Picking a status past "Received in Warehouse" only makes sense with every track loaded,
// so the filter switches the page to All by itself.
watch(() => [...(colFilter.selected['status'] ?? [])], (picked) => {
  if (!showAll.value && picked.some(s => (statusRank[s] ?? 0) >= statusRank['Received in Warehouse']!))
    showAll.value = true
}, { immediate: true }) // the filter is remembered, so also check it on page open

function goToGroup(group: any) {
  router.push(`/shipping/track-numbers/${group.primaryId}`)
}

async function loadTracks() {
  loading.value = true
  try {
    tracks.value = await api.get(showAll.value ? '/shipping/track-numbers' : '/shipping/track-numbers?pendingOnly=true')
  } catch {
    notify('Failed to load tracks', 'error')
  } finally {
    loading.value = false
  }
}

// ── Warehouse app sync ────────────────────────────────────────────────────────

const warehouseSync = useWarehouseSync()
const warehouseSyncEnabled = ref(false)
const syncing = ref(false)
const syncProgress = ref('')
const syncDialog = ref(false)
const syncResult = ref<WarehouseSyncSummary | null>(null)

async function syncWarehouse() {
  syncing.value = true
  syncProgress.value = 'Connecting to the warehouse…'
  try {
    syncResult.value = await warehouseSync.sync(text => (syncProgress.value = text))
    syncDialog.value = true
    await loadTracks()
  } catch (e: any) {
    notify(e?.data?.message || e?.message || 'Warehouse sync failed', 'error')
  } finally {
    syncing.value = false
  }
}

onMounted(async () => {
  loadTracks()
  try {
    warehouseSyncEnabled.value = (await warehouseSync.getConfig()).enabled
  } catch {
    // Roles without access to the sync simply don't see the button.
  }
})
</script>
