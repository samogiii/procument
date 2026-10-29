<template>
  <div class="bi-pi">
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-2">
      {{ error }}
      <template #append>
        <v-btn size="small" variant="text" @click="load()">Retry</v-btn>
      </template>
    </v-alert>

    <v-data-table-server
      v-model:items-per-page="itemsPerPage"
      v-model:page="page"
      v-model:sort-by="sortBy"
      :headers="headers"
      :items="items"
      :items-length="total"
      :loading="loading"
      :items-per-page-options="[10, 25, 50, 100]"
      item-value="id"
      density="compact"
      class="bi-pi__table"
      :class="{ 'bi-pi__table--nested': nested }"
      must-sort
      hover
    >
      <template #loading>
        <v-skeleton-loader type="table-row@4" />
      </template>

      <template #no-data>
        <div class="py-6 text-center text-medium-emphasis">
          <v-icon icon="mdi-receipt-text-outline" size="28" class="mb-1" />
          <div>No proforma invoices in this period.</div>
        </div>
      </template>

      <template #item.invoiceNumber="{ item }">
        <NuxtLink :to="`/invoices/${item.id}`" class="bi-pi__link" target="_blank">
          {{ item.b1ProformaInvoiceNumber || item.invoiceNumber }}
        </NuxtLink>
        <div v-if="item.quoteNumber" class="text-caption text-medium-emphasis">{{ item.quoteNumber }}</div>
      </template>

      <template #item.customer="{ item }">
        <span class="text-truncate d-inline-block" style="max-width: 220px">{{ biCustomerLabel(item) }}</span>
      </template>

      <template #item.createdAt="{ item }">{{ biDate(item.createdAt) }}</template>

      <template #item.boughtValue="{ item }">
        <template v-if="item.boughtValue != null">
          <span class="bi-num">{{ biMoney(item.boughtValue) }}</span>
          <div class="text-caption text-medium-emphasis bi-num">
            {{ item.boughtLineCount }}/{{ item.lineCount }} lines · PI {{ biMoney(item.totalAmount, true) }}
            <v-tooltip v-if="item.boughtLineCount < item.lineCount" location="top" text="Only some lines are in a PO — the rest are not counted until they are bought.">
              <template #activator="{ props }">
                <v-icon v-bind="props" icon="mdi-alert-outline" size="14" color="warning" class="ml-1" aria-label="Partly bought" />
              </template>
            </v-tooltip>
          </div>
        </template>
        <template v-else>
          <v-chip size="x-small" variant="tonal" prepend-icon="mdi-clock-outline">No PO yet</v-chip>
          <div class="text-caption text-medium-emphasis bi-num">PI {{ biMoney(item.totalAmount, true) }}</div>
        </template>
      </template>

      <template #item.supplierCost="{ item }">
        <template v-if="item.supplierCost != null">
          <span class="bi-num">{{ biMoney(item.supplierCost) }}</span>
          <div class="text-caption text-medium-emphasis">{{ item.poCount }} PO{{ item.poCount === 1 ? '' : 's' }}</div>
        </template>
        <span v-else class="text-medium-emphasis">—</span>
      </template>

      <template #item.fees="{ item }">
        <template v-if="item.fees != null">
          <span class="bi-num">{{ biMoney(item.fees) }}</span>
          <div v-if="item.unpaidPoCount" class="text-caption text-medium-emphasis">
            <v-icon icon="mdi-timer-sand" size="12" class="mr-1" />{{ item.unpaidPoCount }} PO{{ item.unpaidPoCount === 1 ? '' : 's' }} unpaid · estimated
          </div>
          <div v-else class="text-caption text-medium-emphasis">from withdraws</div>
        </template>
        <span v-else class="text-medium-emphasis">—</span>
      </template>

      <template #item.profit="{ item }">
        <template v-if="item.profit != null">
          <span class="bi-num font-weight-medium" :class="item.profit < 0 ? 'text-error' : 'text-success'">
            <v-icon :icon="item.profit < 0 ? 'mdi-trending-down' : 'mdi-trending-up'" size="14" class="mr-1" />{{ biMoney(item.profit) }}
          </span>
          <div v-if="item.marginPercent != null" class="text-caption text-medium-emphasis">{{ item.marginPercent }}% margin</div>
        </template>
        <span v-else class="text-medium-emphasis">—</span>
      </template>

      <template #item.daysToFinal="{ item }">
        <template v-if="item.daysToFinal != null">
          <v-chip size="small" variant="tonal" :color="speedColor(item.daysToFinal)" class="bi-num">
            {{ biDays(item.daysToFinal) }}
          </v-chip>
          <div class="text-caption text-medium-emphasis">
            {{ biDate(item.firstFinalAt) }}<template v-if="item.finalCount > 1"> · {{ item.finalCount }} invoices</template>
          </div>
        </template>
        <v-chip v-else size="x-small" variant="outlined" prepend-icon="mdi-timer-sand">Not invoiced</v-chip>
      </template>
    </v-data-table-server>
  </div>
</template>

<script setup lang="ts">
import { biCustomerLabel, biDate, biDays, biMoney, type BiProformaPage, type BiProformaRow, type BiProformaSort } from '~/utils/bi'

const props = withDefaults(defineProps<{
  /** null = every customer the user may see. */
  customerIds: number[] | null
  /** Whole customer bases; null = no base filter. */
  bases?: number[] | null
  days: number
  /** Inside an expanded customer row — hides the customer column. */
  nested?: boolean
}>(), { nested: false })

const api = useApi()

const items = shallowRef<BiProformaRow[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const page = ref(1)
const itemsPerPage = ref(props.nested ? 10 : 25)
const sortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([{ key: 'createdAt', order: 'desc' }])

const headers = computed(() => [
  { title: 'Proforma #', key: 'invoiceNumber', sortable: false },
  ...(props.nested ? [] : [{ title: 'Customer', key: 'customer', sortable: true }]),
  { title: 'Created', key: 'createdAt', sortable: true },
  { title: 'Bought value', key: 'boughtValue', align: 'end' as const, sortable: true },
  { title: 'Supplier cost', key: 'supplierCost', align: 'end' as const, sortable: true },
  { title: 'Shipping, wire fee & others', key: 'fees', align: 'end' as const, sortable: true },
  { title: 'Profit', key: 'profit', align: 'end' as const, sortable: true },
  { title: 'PI → Final invoice', key: 'daysToFinal', align: 'end' as const, sortable: true },
])

/** Table column → API sort key. */
const SORT_KEYS: Record<string, BiProformaSort> = {
  customer: 'customer', createdAt: 'createdAt', boughtValue: 'boughtValue', supplierCost: 'supplierCost',
  fees: 'fees', profit: 'profit', daysToFinal: 'daysToFinal',
}

function speedColor(days: number) {
  if (days <= 7) return 'success'
  if (days <= 30) return 'info'
  if (days <= 60) return 'warning'
  return 'error'
}

let controller: AbortController | null = null

async function load() {
  controller?.abort()
  const current = new AbortController()
  controller = current
  loading.value = true
  error.value = ''
  const sort = sortBy.value[0]
  try {
    const res = await api.post<BiProformaPage>('/bi/proformas', {
      customerIds: props.customerIds,
      bases: props.bases ?? null,
      days: props.days,
      skip: (page.value - 1) * itemsPerPage.value,
      take: itemsPerPage.value,
      sortBy: SORT_KEYS[sort?.key ?? 'createdAt'] ?? 'createdAt',
      sortDesc: (sort?.order ?? 'desc') === 'desc',
    }, { signal: current.signal })
    if (controller !== current) return
    items.value = res.items
    total.value = res.total
  } catch (e: any) {
    if (current.signal.aborted) return
    error.value = e?.data?.message || e?.data || 'Could not load proforma invoices.'
  } finally {
    if (controller === current) loading.value = false
  }
}

watch([page, itemsPerPage, sortBy], load, { deep: true })
watch(() => [props.days, props.customerIds?.join(','), props.bases?.join(',')], () => {
  if (page.value !== 1) page.value = 1 // the page watcher reloads
  else load()
})

onMounted(load)
onBeforeUnmount(() => controller?.abort())
</script>

<style scoped>
.bi-pi__table :deep(th) {
  white-space: nowrap;
}
.bi-pi__table--nested {
  background: transparent !important;
}
.bi-pi__link {
  color: rgb(var(--v-theme-primary));
  font-weight: 600;
  text-decoration: none;
}
.bi-pi__link:hover {
  text-decoration: underline;
}
.bi-num {
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
</style>
