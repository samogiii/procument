<template>
  <div>
    <PageHeader :title="item ? `${item.partNumber} · ${item.condition}` : 'Stock item'" back-to="/our-inventory">
      <template #actions>
        <v-chip v-if="item?.isLowStock" color="warning" variant="tonal" size="small" prepend-icon="mdi-alert-outline">Below minimum</v-chip>
        <template v-if="item && authStore.isAdmin">
          <v-btn variant="tonal" size="small" prepend-icon="mdi-plus-minus-variant" color="purple" @click="action = 'adjust'">Adjust</v-btn>
          <v-btn variant="tonal" size="small" prepend-icon="mdi-swap-horizontal" color="info" @click="action = 'transfer'">Move</v-btn>
          <v-btn variant="tonal" size="small" prepend-icon="mdi-pencil" @click="action = 'edit'">Edit</v-btn>
        </template>
        <v-btn color="primary" variant="tonal" size="small" prepend-icon="mdi-cart-arrow-down" to="/our-inventory/purchase-orders/new">Order more</v-btn>
      </template>
    </PageHeader>

    <v-alert v-if="notFound" type="error" variant="tonal">Stock item not found.</v-alert>
    <v-progress-linear v-else-if="loading && !item" indeterminate color="primary" />

    <template v-if="item">
      <p v-if="item.description" class="text-body-2 text-medium-emphasis mb-4">{{ item.description }}</p>

      <v-row class="mb-2">
        <v-col cols="6" md="3"><StatCard icon="mdi-package-variant" color="primary" label="On hand" :value="fmtQty(item.qtyOnHand)" class="h-100" /></v-col>
        <v-col cols="6" md="3"><StatCard icon="mdi-lock-outline" color="warning" label="Reserved" :value="fmtQty(item.qtyReserved)" class="h-100" /></v-col>
        <v-col cols="6" md="3"><StatCard icon="mdi-check-circle-outline" color="success" label="Available" :value="fmtQty(item.qtyAvailable)" class="h-100" /></v-col>
        <v-col cols="6" md="3">
          <StatCard v-if="item.avgUnitCost != null" icon="mdi-currency-usd" color="info" label="Avg cost / value" class="h-100">
            ${{ formatPrice(item.avgUnitCost) }} <span class="text-caption text-medium-emphasis">· ${{ formatPrice(item.totalAmount) }}</span>
          </StatCard>
          <StatCard v-else icon="mdi-truck-delivery-outline" color="info" label="Incoming" :value="fmtQty(incomingQty)" class="h-100" />
        </v-col>
      </v-row>

      <v-card class="glass-card mb-4">
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-information-outline" class="mr-2" size="20" color="primary" />
          Lot
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col v-for="f in lotFields" :key="f.label" cols="6" md="3">
              <div class="text-caption text-medium-emphasis">{{ f.label }}</div>
              <div class="text-body-2 font-weight-medium">{{ f.value || '—' }}</div>
            </v-col>
          </v-row>
          <div v-if="item.notes" class="mt-3 text-body-2">{{ item.notes }}</div>
        </v-card-text>
      </v-card>

      <v-row>
        <v-col cols="12" md="6">
          <v-card class="glass-card mb-4 h-100">
            <v-card-title class="d-flex align-center">
              <v-icon icon="mdi-lock-outline" class="mr-2" size="20" color="warning" />
              Reservations
              <v-chip size="x-small" variant="tonal" class="ml-2">{{ detail.reservations.length }}</v-chip>
            </v-card-title>
            <v-card-text>
              <v-table v-if="detail.reservations.length" density="compact">
                <thead><tr><th>Qty</th><th>For</th><th>By</th><th>Since</th><th v-if="authStore.isAdmin" /></tr></thead>
                <tbody>
                  <tr v-for="r in detail.reservations" :key="r.id">
                    <td class="font-weight-medium">{{ fmtQty(r.qty) }}</td>
                    <td>
                      <NuxtLink v-if="r.invoiceId" :to="`/invoices/${r.invoiceId}`" class="text-primary text-decoration-none">{{ r.invoiceNumber || `Sales order ${r.invoiceId}` }}</NuxtLink>
                      <span v-else>{{ r.invoiceItemId ? `Sales order line ${r.invoiceItemId}` : '—' }}</span>
                      <div v-if="r.customerName" class="text-caption text-medium-emphasis">{{ r.customerName }}</div>
                    </td>
                    <td>{{ r.createdByName }}</td>
                    <td>{{ new Date(r.createdAt).toLocaleDateString() }}</td>
                    <td v-if="authStore.isAdmin" class="text-no-wrap">
                      <v-btn size="x-small" variant="tonal" color="success" prepend-icon="mdi-truck-delivery" :loading="busyReservation === r.id"
                        title="Ship to the customer: takes the stock out at its average cost" @click="issueReservation(r)">Issue</v-btn>
                      <v-btn size="x-small" variant="text" color="warning" class="ml-1" :disabled="busyReservation === r.id"
                        title="Give the stock back to available without shipping it" @click="releaseReservation(r)">Release</v-btn>
                    </td>
                  </tr>
                </tbody>
              </v-table>
              <div v-else class="text-caption text-medium-emphasis">Nothing reserved.</div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card class="glass-card mb-4 h-100">
            <v-card-title class="d-flex align-center">
              <v-icon icon="mdi-truck-delivery-outline" class="mr-2" size="20" color="info" />
              Incoming (all lots of this P/N)
            </v-card-title>
            <v-card-text>
              <v-table v-if="detail.incoming.length" density="compact">
                <thead><tr><th>PO</th><th>Cond.</th><th>Warehouse</th><th class="text-end">Remaining</th><th>Expected</th></tr></thead>
                <tbody>
                  <tr v-for="l in detail.incoming" :key="l.poItemId">
                    <td><NuxtLink :to="`/purchase-orders/${l.poId}`" class="text-primary text-decoration-none">{{ l.poNumber }}</NuxtLink></td>
                    <td>{{ l.condition }}</td>
                    <td>{{ l.destinationWarehouseName }}</td>
                    <td class="text-end">{{ fmtQty(l.qtyRemaining) }}</td>
                    <td>{{ l.expectedDeliveryDate ? new Date(l.expectedDeliveryDate).toLocaleDateString() : '—' }}</td>
                  </tr>
                </tbody>
              </v-table>
              <div v-else class="text-caption text-medium-emphasis">No open Stock PO lines for this part.</div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <v-card v-if="detail.serials.length" class="glass-card mb-4">
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-barcode" class="mr-2" size="20" color="primary" />
          Serial numbers
          <v-chip size="x-small" variant="tonal" class="ml-2">{{ detail.serials.length }}</v-chip>
        </v-card-title>
        <v-card-text class="d-flex flex-wrap gap-2">
          <v-chip v-for="s in detail.serials" :key="s.id" size="small" variant="outlined" :color="s.status === 'Reserved' ? 'warning' : undefined">
            {{ s.serialNumber }}
          </v-chip>
        </v-card-text>
      </v-card>

      <v-card class="glass-card mb-4">
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-history" class="mr-2" size="20" color="primary" />
          Movements
        </v-card-title>
        <v-card-text>
          <StockMovementsTable ref="movementsTable" :stock-item-id="route.params.id as string" />
        </v-card-text>
      </v-card>
    </template>

    <StockLotActionDialog v-model:mode="action" :lot="item" @done="onActionDone" />
    <v-snackbar v-model="snackbar" color="success" :timeout="3000" location="bottom end">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const route = useRoute()
const authStore = useAuthStore()

const action = ref<'adjust' | 'transfer' | 'edit' | null>(null)
const movementsTable = ref<{ reload: () => void } | null>(null)
const snackbar = ref(false)
const snackbarText = ref('')
const busyReservation = ref<number | null>(null)
async function reservationAction(r: any, kind: 'issue' | 'release') {
  const verb = kind === 'issue' ? `Issue ${fmtQty(r.qty)} to ${r.invoiceNumber || 'the customer'}?` : `Release ${fmtQty(r.qty)} back to available?`
  if (!confirm(verb)) return
  busyReservation.value = r.id
  try {
    await api.post(`/our-inventory/reservations/${r.id}/${kind}`, kind === 'issue' ? {} : undefined)
    await onActionDone(kind === 'issue' ? `Issued ${fmtQty(r.qty)}` : `Released ${fmtQty(r.qty)}`)
  } catch (e: any) {
    snackbarText.value = e?.data?.message || 'The change was not saved.'
    snackbar.value = true
  } finally {
    busyReservation.value = null
  }
}
const issueReservation = (r: any) => reservationAction(r, 'issue')
const releaseReservation = (r: any) => reservationAction(r, 'release')

async function onActionDone(message: string) {
  snackbarText.value = message
  snackbar.value = true
  await load()
  movementsTable.value?.reload()
}

const detail = ref<{ item: any; reservations: any[]; incoming: any[]; serials: any[] }>({ item: null, reservations: [], incoming: [], serials: [] })
const loading = ref(false)
const notFound = ref(false)
const item = computed(() => detail.value.item)

// Show the part number instead of the raw id in the breadcrumb trail
const { setBreadcrumbLabel } = useBreadcrumb()
watchEffect(() => setBreadcrumbLabel(item.value ? `${item.value.partNumber} · ${item.value.condition}` : undefined))

const fmtQty = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })
const incomingQty = computed(() => detail.value.incoming.reduce((s, l) => s + Number(l.qtyRemaining || 0), 0))

const lotFields = computed(() => item.value ? [
  { label: 'Warehouse', value: item.value.warehouseName },
  { label: 'Owner company', value: item.value.companyPresetName },
  { label: 'Bin', value: item.value.binLocation },
  { label: 'Certificate', value: item.value.certName },
  { label: 'Tag date', value: item.value.tagDate ? new Date(item.value.tagDate).toLocaleDateString() : '' },
  { label: 'Minimum qty', value: item.value.minQty != null ? fmtQty(item.value.minQty) : '' },
  { label: 'Last change', value: new Date(item.value.updatedAt).toLocaleString() },
] : [])

async function load() {
  loading.value = true
  notFound.value = false
  try { detail.value = await api.get<any>(`/our-inventory/stock/${route.params.id}`) }
  catch { notFound.value = true }
  finally { loading.value = false }
}

onMounted(load)
</script>
