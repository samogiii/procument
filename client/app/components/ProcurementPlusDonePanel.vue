<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-select
        v-model="userId"
        :items="users"
        item-title="name"
        item-value="id"
        label="Done by"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        prepend-inner-icon="mdi-account-check-outline"
        style="max-width: 220px; min-width: 180px"
      />
      <v-select
        v-model="days"
        :items="PERIODS"
        item-title="label"
        item-value="value"
        label="Done in"
        density="compact"
        variant="outlined"
        hide-details
        style="max-width: 170px; min-width: 150px"
      />
      <v-text-field
        v-model="search"
        label="Search part or supplier"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        prepend-inner-icon="mdi-magnify"
        style="max-width: 300px; min-width: 200px"
      />
      <v-spacer />
      <v-chip size="small" color="success" variant="tonal">{{ lines.length }} line{{ lines.length === 1 ? '' : 's' }}</v-chip>
      <v-btn size="small" variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
    </div>

    <div v-if="loading && !lines.length" class="d-flex justify-center py-8"><v-progress-circular indeterminate color="primary" /></div>
    <div v-else-if="!groups.length" class="text-center text-medium-emphasis py-10">
      <v-icon icon="mdi-check-circle-outline" size="44" class="mb-2" />
      <div>Nothing done in this period yet.</div>
    </div>

    <div v-for="g in groups" :key="g.key" class="done-group mb-4">
      <div class="done-group__head">
        <!-- A line can be searched several ways ("ILS,Email"): the first one's icon, every title -->
        <v-avatar :color="methodMeta(lineMethods(g.method)[0]?.value ?? g.method).color" variant="tonal" size="28" class="mr-2">
          <v-icon :icon="methodMeta(lineMethods(g.method)[0]?.value ?? g.method).icon" size="16" />
        </v-avatar>
        <span class="font-weight-bold">{{ g.supplierName }}</span>
        <span class="text-caption text-medium-emphasis ml-2">{{ lineMethods(g.method).map(m => m.title).join(' · ') || methodMeta(g.method).title }}</span>
        <v-spacer />
        <v-btn
          v-if="g.lines.some(isAutoFillOn)"
          size="x-small"
          color="warning"
          variant="tonal"
          prepend-icon="mdi-autorenew-off"
          :loading="busyKey === g.key"
          title="New RFQs stop receiving these prices (e.g. the supplier sold the parts)"
          @click="setAutoFill(g.lines.filter(isAutoFillOn), false, g.key)"
        >
          Stop auto-fill
        </v-btn>
      </div>
      <div class="grid-scroll">
        <table class="done-table">
          <thead>
            <tr>
              <th>Part</th>
              <th>Cond</th>
              <th class="num">Qty</th>
              <th class="num">Price</th>
              <th>Lead time</th>
              <th>Set on RFQs</th>
              <th>Done</th>
              <th>Auto-fill new RFQs</th>
              <th style="width: 44px" />
            </tr>
          </thead>
          <tbody>
            <tr v-for="l in g.lines" :key="l.line.id">
              <td class="font-weight-medium nowrap">
                {{ l.line.partNumber }}
                <div v-if="l.line.alt" class="text-caption text-medium-emphasis">alt {{ l.line.alt }}</div>
              </td>
              <td>{{ l.line.condition }}</td>
              <td class="num">{{ l.line.qty ?? '' }}</td>
              <td class="num font-weight-bold">{{ fmtMoney(l.line.price) }}</td>
              <td class="nowrap">{{ l.line.leadTime }}</td>
              <td>
                <div class="d-flex flex-wrap gap-1">
                  <v-chip
                    v-for="r in l.writtenRfqs"
                    :key="r.rfqId"
                    size="x-small"
                    color="success"
                    variant="tonal"
                    :to="`/rfqs/${r.rfqId}`"
                    :title="`${r.customerCode || ''} · deadline ${fmtDate(r.deadline)}`"
                  >
                    {{ r.rfqName }}
                  </v-chip>
                  <span v-if="!l.writtenRfqs.length" class="text-caption text-medium-emphasis">— none yet</span>
                </div>
              </td>
              <td class="nowrap text-caption">
                {{ fmtDate(l.line.reviewedAt) }}
                <div class="text-medium-emphasis">{{ l.line.reviewedByName }}</div>
              </td>
              <td class="nowrap">
                <v-btn
                  v-if="autoFillEnded(l)"
                  size="x-small"
                  variant="text"
                  disabled
                >Ended</v-btn>
                <v-btn
                  v-else
                  size="x-small"
                  :color="l.line.autoApplyDisabled ? undefined : 'success'"
                  :variant="l.line.autoApplyDisabled ? 'outlined' : 'tonal'"
                  :prepend-icon="l.line.autoApplyDisabled ? 'mdi-autorenew-off' : 'mdi-autorenew'"
                  :loading="busyKey === `l${l.line.id}`"
                  @click="setAutoFill([l], l.line.autoApplyDisabled, `l${l.line.id}`)"
                >
                  {{ l.line.autoApplyDisabled ? 'Off' : `On until ${fmtDate(l.line.autoApplyUntil)}` }}
                </v-btn>
              </td>
              <td>
                <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" title="Edit — changes reach new RFQs only" @click="edit(l)" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <EmailQuoteEditDialog
      v-model="editOpen"
      :row="editRow"
      accepted
      api-base="/procurement-plus/quotes"
      hide-supplier
      @updated="patch"
    />
  </div>
</template>

<script setup lang="ts">
import type { PpQuoteLine } from '~/utils/procurementPlus'

const PERIODS = [
  { label: 'Last 7 days', value: 7 },
  { label: 'Last 30 days', value: 30 },
  { label: 'Last 90 days', value: 90 },
  { label: 'All time', value: 0 },
]

const api = useApi()
const authStore = useAuthStore()
const lines = ref<PpQuoteLine[]>([])
const users = ref<{ id: number; name: string }[]>([])
const loading = ref(false)
const days = ref(7)
const userId = ref<number | null>(authStore.user?.id ? Number(authStore.user.id) : null)
const search = ref('')
const busyKey = ref<string | null>(null)

const groups = computed(() => {
  const map = new Map<string, { key: string; supplierName: string; method?: string; lines: PpQuoteLine[] }>()
  for (const l of lines.value) {
    const key = l.preferredId ? `p${l.preferredId}` : String(l.line.supplierId ?? l.line.supplierNameRaw)
    let g = map.get(key)
    if (!g) map.set(key, (g = { key, supplierName: l.preferredName || l.line.supplierName || l.line.supplierNameRaw || '—', method: l.sourceMethod, lines: [] }))
    g.lines.push(l)
  }
  return [...map.values()]
})

const autoFillEnded = (l: PpQuoteLine) => !!l.line.autoApplyUntil && new Date(l.line.autoApplyUntil) < new Date()
const isAutoFillOn = (l: PpQuoteLine) => !l.line.autoApplyDisabled && !autoFillEnded(l)

let timer: ReturnType<typeof setTimeout> | null = null
watch([days, userId], () => load())
watch(search, () => {
  if (timer) clearTimeout(timer)
  timer = setTimeout(load, 350)
})

async function load() {
  loading.value = true
  try {
    const params = new URLSearchParams({ days: String(days.value) })
    if (userId.value) params.set('userId', String(userId.value))
    if (search.value?.trim()) params.set('search', search.value.trim())
    const res = await api.get<{ lines: PpQuoteLine[]; users: { id: number; name: string }[] }>(`/procurement-plus/done?${params}`)
    lines.value = res.lines
    users.value = res.users
    // The current user has nothing here yet: show everyone's.
    if (userId.value && !res.users.some(u => u.id === userId.value) && !search.value) userId.value = null
  } finally {
    loading.value = false
  }
}

async function setAutoFill(list: PpQuoteLine[], enabled: boolean, key: string) {
  busyKey.value = key
  try {
    for (const l of list) {
      await api.patch(`/procurement-plus/quotes/${l.line.id}/auto-apply`, { enabled })
      l.line.autoApplyDisabled = !enabled
    }
  } finally {
    busyKey.value = null
  }
}

const editOpen = ref(false)
const editRow = ref<any>(null)
function edit(l: PpQuoteLine) {
  editRow.value = l.line
  editOpen.value = true
}
function patch(row: any) {
  const l = lines.value.find(x => x.line.id === row.id)
  if (l) l.line = { ...l.line, ...row }
}

onMounted(load)
</script>

<style scoped>
.done-group { border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); border-radius: 10px; overflow: hidden; }
.done-group__head {
  display: flex; align-items: center; padding: 8px 12px;
  background: rgba(var(--v-theme-success), 0.06);
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.grid-scroll { overflow-x: auto; }
.done-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.done-table th { text-align: left; font-size: 11.5px; font-weight: 600; opacity: 0.65; padding: 6px 10px; white-space: nowrap; }
.done-table td { padding: 6px 10px; border-top: 1px solid rgba(var(--v-border-color), 0.06); vertical-align: top; }
.num { text-align: right; white-space: nowrap; }
.nowrap { white-space: nowrap; }
</style>
