<template>
  <div>
    <PageHeader title="Receive Stock">
      <template #actions>
        <v-select v-model="warehouseId" :items="warehouses" :item-title="(w: any) => w.displayName || w.name" item-value="id"
          label="Warehouse" clearable hide-details density="compact" variant="outlined" style="min-width: 220px" />
        <v-btn icon="mdi-refresh" variant="text" :loading="loading" @click="load" />
      </template>
    </PageHeader>

    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      Approved Stock PO lines still waiting to arrive. Goods that come in on a <strong>track number</strong> are added to stock when the track is accepted in
      <NuxtLink to="/shipping" class="text-primary">Warehouse Shippings</NuxtLink>; use <strong>Receive</strong> here only for goods without one.
    </v-alert>

    <v-progress-linear v-if="loading && !groups.length" indeterminate color="primary" />
    <v-card v-else-if="!groups.length" class="glass-card pa-8 text-center text-medium-emphasis">
      <v-icon icon="mdi-check-all" size="40" class="mb-2" />
      <div>Nothing waiting to be received{{ warehouseId ? ' at this warehouse' : '' }}.</div>
    </v-card>

    <v-card v-for="g in groups" :key="g.poId" class="glass-card mb-4">
      <v-card-title class="d-flex align-center flex-wrap gap-2">
        <NuxtLink :to="`/purchase-orders/${g.poId}`" class="text-primary text-decoration-none font-weight-bold">{{ g.poNumber }}</NuxtLink>
        <v-chip size="x-small" variant="tonal" :color="stockPoStatusColor(g.poStatus)">{{ g.poStatus }}</v-chip>
        <span class="text-body-2 text-medium-emphasis">{{ g.supplierName }} → {{ g.destinationWarehouseName || 'no warehouse' }}</span>
        <span v-if="g.companyPresetName" class="text-caption text-medium-emphasis">· {{ g.companyPresetName }}</span>
        <v-spacer />
        <span v-if="g.expectedDeliveryDate" class="text-caption" :class="isLate(g.expectedDeliveryDate) ? 'text-error' : 'text-medium-emphasis'">
          Expected {{ new Date(g.expectedDeliveryDate).toLocaleDateString() }}
        </span>
        <v-btn color="success" variant="flat" size="small" prepend-icon="mdi-truck-check-outline" @click="openReceive(g.poId)">Receive</v-btn>
      </v-card-title>
      <v-card-text>
        <v-table density="compact">
          <thead><tr><th>#</th><th>P/N</th><th>Cond.</th><th class="text-end">Ordered</th><th class="text-end">Received</th><th class="text-end">Remaining</th></tr></thead>
          <tbody>
            <tr v-for="l in g.lines" :key="l.poItemId">
              <td class="text-medium-emphasis">{{ l.poRef }}</td>
              <td>
                <span class="font-weight-medium">{{ l.partNumber }}</span>
                <div v-if="l.description" class="text-caption text-medium-emphasis">{{ l.description }}</div>
              </td>
              <td>{{ l.condition }}</td>
              <td class="text-end">{{ l.qtyOrdered }}</td>
              <td class="text-end">{{ fmtQty(l.qtyReceived) }}</td>
              <td class="text-end font-weight-bold">{{ fmtQty(l.qtyRemaining) }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <StockReceiveDialog v-model="showReceive" :po-id="receivePoId" @received="onReceived" />
    <v-snackbar v-model="snackbar" color="success" :timeout="3000" location="bottom end">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()

// Manual receiving is Admin-only on the API; others receive through track numbers.
if (!authStore.isAdmin) navigateTo('/our-inventory')

const lines = ref<any[]>([])
const warehouses = ref<any[]>([])
const warehouseId = ref<number | null>(null)
const loading = ref(false)
const showReceive = ref(false)
const receivePoId = ref<number | null>(null)
const snackbar = ref(false)
const snackbarText = ref('')

const fmtQty = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })
const isLate = (d: string) => new Date(d).getTime() < new Date().setHours(0, 0, 0, 0)

const groups = computed(() => {
  const map = new Map<number, any>()
  for (const l of lines.value) {
    if (!map.has(l.poId)) map.set(l.poId, { ...l, lines: [] })
    map.get(l.poId).lines.push(l)
  }
  return [...map.values()]
})

async function load() {
  loading.value = true
  try {
    const qs = warehouseId.value ? `?warehouseId=${warehouseId.value}` : ''
    lines.value = await api.get<any[]>(`/our-inventory/incoming${qs}`)
  } catch {
    lines.value = []
  } finally {
    loading.value = false
  }
}

function openReceive(poId: number) {
  receivePoId.value = poId
  showReceive.value = true
}

function onReceived(summary: any) {
  snackbarText.value = summary?.fullyReceived ? `${summary.poNumber} fully received` : 'Stock received'
  snackbar.value = true
  load()
}

watch(warehouseId, load)
onMounted(async () => {
  warehouses.value = (await api.get<any[]>('/warehouses').catch(() => [])).filter((w: any) => w.isActive !== false)
  await load()
})
</script>
