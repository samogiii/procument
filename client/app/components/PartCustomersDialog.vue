<template>
  <v-dialog :model-value="modelValue" max-width="900" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card>
      <v-card-title class="d-flex align-center gap-2 text-subtitle-1">
        <v-icon icon="mdi-account-group-outline" color="info" />
        Last customers for <strong>{{ partNumber }}</strong>
        <v-spacer />
        <v-btn icon="mdi-close" size="small" variant="text" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-card-text>
        <div v-if="loading" class="d-flex justify-center py-8">
          <v-progress-circular indeterminate color="primary" />
        </div>
        <p v-else-if="error" class="text-error text-caption">{{ error }}</p>
        <p v-else-if="!customers.length" class="text-caption text-medium-emphasis text-center py-6 mb-0">
          No customer asked for this part (or its alternatives) yet.
        </p>
        <div v-for="c in customers" :key="c.customerId" class="customer-block mb-3">
          <div class="d-flex align-center gap-2 mb-1">
            <v-chip size="small" color="primary" variant="tonal">{{ c.customerCode || '—' }}</v-chip>
            <span class="font-weight-medium">{{ c.customerName }}</span>
            <span class="text-caption text-medium-emphasis">
              asked {{ c.requestCount }} time{{ c.requestCount === 1 ? '' : 's' }}{{ c.requestCount > c.requests.length ? ` (newest ${c.requests.length} shown)` : '' }}
            </span>
          </div>
          <table class="customers-grid">
            <thead>
              <tr>
                <th>RFQ</th>
                <th>Asked</th>
                <th>P/N</th>
                <th>Qty</th>
                <th>Cond</th>
                <th>Our price</th>
                <th>Quote</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in c.requests" :key="`${r.rfqId}-${r.partNumber}-${r.requestedAt}`">
                <td><NuxtLink :to="`/rfqs/${r.rfqId}`">{{ r.rfqName }}</NuxtLink></td>
                <td class="text-no-wrap">{{ fmtDate(r.requestedAt) }}</td>
                <td>{{ r.partNumber }}</td>
                <td>{{ r.qty }}</td>
                <td>{{ r.condition || '—' }}</td>
                <td class="text-no-wrap">
                  <template v-if="r.quoted">
                    <span class="font-weight-medium">{{ fmtMoney(r.quoted.unitPrice) }}</span>
                    <span class="text-caption text-medium-emphasis"> × {{ r.quoted.qty }}{{ r.quoted.condition ? ` · ${r.quoted.condition}` : '' }}</span>
                  </template>
                  <span v-else class="text-medium-emphasis">not quoted</span>
                </td>
                <td class="text-caption">
                  <template v-if="r.quoted">{{ r.quoted.quoteNumber }} · {{ r.quoted.status }}</template>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
/** Email RFQs Pending tab info icon: the last 25 customers who asked for a part, each request with our quoted price. */
type QuotedPrice = { quoteId: number; quoteNumber: string; status: string; createdAt: string; unitPrice: number; qty: number; condition?: string | null }
type CustomerRequest = { rfqId: number; rfqName: string; requestedAt: string; deadline?: string | null; partNumber: string; qty: number; condition?: string | null; quoted?: QuotedPrice | null }
type CustomerHistory = { customerId: number; customerCode?: string | null; customerName: string; requestCount: number; requests: CustomerRequest[] }

const props = defineProps<{ modelValue: boolean; partNumber: string }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: boolean): void }>()

const api = useApi()
const customers = ref<CustomerHistory[]>([])
const loading = ref(false)
const error = ref('')
let loadId = 0

watch(() => [props.modelValue, props.partNumber] as const, async ([open, pn]) => {
  if (!open || !pn) return
  const id = ++loadId
  loading.value = true
  error.value = ''
  customers.value = []
  try {
    const res = await api.get<{ customers: CustomerHistory[] }>(`/pending-rfqs/part-customers?partNumber=${encodeURIComponent(pn)}`)
    if (id === loadId) customers.value = res.customers
  } catch (e: any) {
    if (id === loadId) error.value = e?.data?.message || 'Could not load the customers'
  } finally {
    if (id === loadId) loading.value = false
  }
}, { immediate: true })
</script>

<style scoped>
.customer-block {
  border: 1px solid var(--card-border);
  border-radius: 8px;
  padding: 8px 10px;
}
.customers-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.customers-grid th {
  text-align: left;
  font-weight: 600;
  padding: 3px 8px;
  border-bottom: 1px solid var(--card-border);
  white-space: nowrap;
}
.customers-grid td {
  padding: 3px 8px;
  border-bottom: 1px solid var(--card-border);
}
</style>
