<template>
  <v-container fluid>
    <!-- Header -->
    <div class="d-flex align-center mb-4" style="gap: 12px;">
      <div>
        <h1 class="text-h5 font-weight-bold d-flex align-center" style="gap: 8px;">
          <v-icon icon="mdi-alert-circle-outline" color="warning" size="28" />
          Action Center
        </h1>
        <p class="text-caption text-medium-emphasis mt-1">
          {{ lastRefreshedLabel }}
        </p>
      </div>
      <v-spacer />
      <v-btn
        prepend-icon="mdi-refresh"
        variant="tonal"
        color="primary"
        :loading="refreshing && !loading"
        @click="load(true)"
      >
        Refresh
      </v-btn>
    </div>

    <!-- Background refresh progress bar -->
    <v-progress-linear
      v-if="refreshing && !loading"
      indeterminate
      color="primary"
      height="2"
      class="mb-4"
    />

    <!-- Initial load spinner -->
    <div v-if="loading" class="d-flex justify-center align-center" style="min-height: 320px;">
      <v-progress-circular indeterminate color="primary" size="64" />
    </div>

    <!-- Empty state -->
    <v-card
      v-else-if="groups.length === 0"
      class="pa-10 text-center"
      variant="outlined"
      rounded="xl"
    >
      <v-icon icon="mdi-check-circle-outline" size="72" color="success" class="mb-4 opacity-70" />
      <div class="text-h6 mb-2">Everything looks good!</div>
      <div class="text-body-2 text-medium-emphasis">No items need your attention right now.</div>
    </v-card>

    <!-- Attention groups -->
    <template v-else>
      <!-- Summary chips -->
      <div class="d-flex flex-wrap mb-4" style="gap: 8px;">
        <v-chip
          v-for="sev in summarySeverities"
          :key="sev.label"
          :color="severityColor(sev.label)"
          variant="tonal"
          size="small"
          prepend-icon="mdi-circle-small"
        >
          {{ sev.count }} {{ sev.label }}
        </v-chip>
      </div>

      <v-expansion-panels v-model="expandedGroups" multiple variant="accordion">
        <v-expansion-panel
          v-for="group in groups"
          :key="group.category"
          :value="group.category"
          rounded="lg"
          class="mb-3"
          style="border: 1px solid rgba(var(--v-border-color), 0.12);"
          bg-color="surface"
          elevation="0"
        >
          <v-expansion-panel-title class="py-3">
            <div class="d-flex align-center flex-wrap" style="gap: 10px;">
              <v-avatar
                :color="severityColor(group.severity)"
                size="34"
                variant="tonal"
              >
                <v-icon :icon="group.icon" size="18" />
              </v-avatar>
              <span class="text-body-1 font-weight-semibold">{{ group.category }}</span>
              <v-chip
                :color="severityColor(group.severity)"
                size="x-small"
                variant="flat"
                style="min-width: 28px;"
              >
                {{ group.count }}
              </v-chip>
              <v-chip
                :color="severityColor(group.severity)"
                size="x-small"
                variant="tonal"
                class="text-uppercase font-weight-bold"
                style="font-size: 9px; letter-spacing: 0.6px;"
              >
                {{ group.severity }}
              </v-chip>
            </div>
          </v-expansion-panel-title>

          <v-expansion-panel-text class="pa-0">
            <v-divider />
            <v-data-table
              :headers="buildHeaders(group)"
              :items="group.items"
              density="compact"
              :items-per-page="10"
              :hide-default-footer="group.items.length <= 10"
            >
              <!-- Since column -->
              <template #item.since="{ item }">
                <span class="text-caption text-medium-emphasis">{{ timeAgo(item.since) }}</span>
              </template>

              <!-- Detail column: truncate long rejection notes -->
              <template #item.detail="{ item }">
                <span
                  v-if="item.detail"
                  class="text-caption text-medium-emphasis"
                  :title="item.detail"
                  style="max-width: 200px; display: inline-block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;"
                >
                  {{ item.detail }}
                </span>
              </template>

              <!-- Actions column -->
              <template #item.actions="{ item }">
                <v-btn
                  size="x-small"
                  variant="tonal"
                  color="primary"
                  append-icon="mdi-arrow-right"
                  @click="router.push(item.route)"
                >
                  View
                </v-btn>
              </template>
            </v-data-table>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
    </template>
  </v-container>
</template>

<script setup lang="ts">
interface AttentionItem {
  category: string
  severity: string
  title: string
  detail: string
  partNumbers: string[]
  customerName: string
  supplierName: string
  since: string
  route: string
  entityType: string
  entityId: number
}

interface AttentionGroup {
  category: string
  severity: string
  icon: string
  count: number
  items: AttentionItem[]
}

const api = useApi()
const router = useRouter()

const groups = ref<AttentionGroup[]>([])
const loading = ref(true)
const refreshing = ref(false)
const lastRefreshed = ref<Date | null>(null)

// All group categories currently expanded (start all open)
const expandedGroups = ref<string[]>([])

async function load(isRefresh = false) {
  if (isRefresh) {
    refreshing.value = true
  } else {
    loading.value = true
  }
  try {
    const data = await api.get<AttentionGroup[]>('/dashboard/attention')
    groups.value = data
    expandedGroups.value = data.map(g => g.category)
    lastRefreshed.value = new Date()
  } catch (e) {
    console.warn('[ActionCenter] Load failed', e)
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

// Last refreshed label
const lastRefreshedLabel = computed(() => {
  if (!lastRefreshed.value) return 'Loading…'
  return `Last updated ${timeAgo(lastRefreshed.value.toISOString())}`
})

// Summary bar: count items per severity across all groups
const summarySeverities = computed(() => {
  const counts: Record<string, number> = {}
  for (const g of groups.value) {
    counts[g.severity] = (counts[g.severity] ?? 0) + g.count
  }
  return Object.entries(counts)
    .sort(([a], [b]) => {
      const rank = (s: string) => s === 'urgent' ? 0 : s === 'warning' ? 1 : 2
      return rank(a) - rank(b)
    })
    .map(([label, count]) => ({ label, count }))
})

function severityColor(severity: string): string {
  switch (severity) {
    case 'urgent':  return 'error'
    case 'warning': return 'warning'
    default:        return 'info'
  }
}

function buildHeaders(group: AttentionGroup) {
  const headers: any[] = [
    { title: 'Reference', key: 'title', sortable: true },
  ]
  if (group.items.some(i => i.detail)) {
    headers.push({ title: 'Note', key: 'detail', sortable: false })
  }
  if (group.items.some(i => i.customerName)) {
    headers.push({ title: 'Customer', key: 'customerName', sortable: true })
  }
  if (group.items.some(i => i.supplierName)) {
    headers.push({ title: 'Supplier', key: 'supplierName', sortable: true })
  }
  headers.push({ title: 'Since', key: 'since', sortable: true })
  headers.push({ title: '', key: 'actions', sortable: false, align: 'end' })
  return headers
}

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  const days = Math.floor(hrs / 24)
  return `${days}d ago`
}

let refreshTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  load()
  // Auto-refresh every 60 seconds
  refreshTimer = setInterval(() => load(true), 60_000)
})

onUnmounted(() => {
  if (refreshTimer) clearInterval(refreshTimer)
})
</script>
