<template>
  <div>
    <PageHeader title="Customer Credit Trace" back-to="/payment" :count="customers.length" />

    <v-alert type="info" variant="tonal" class="mb-4">
      Available Credit = Max Credit − unpaid balance of open Credit PIs. Every partial customer POP restores credit immediately.
    </v-alert>

    <v-text-field v-model="search" label="Search customer or code" prepend-inner-icon="mdi-magnify" clearable class="mb-4" />

    <v-card class="glass-card">
      <v-data-table :headers="headers" :items="filtered" :loading="loading" item-value="customerId">
        <template #item.maxCredit="{ item }">${{ money(item.maxCredit) }}</template>
        <template #item.usedCredit="{ item }"><span class="text-warning">${{ money(item.usedCredit) }}</span></template>
        <template #item.availableCredit="{ item }">
          <v-chip :color="item.availableCredit > 0 ? 'success' : 'error'" variant="tonal">${{ money(item.availableCredit) }}</v-chip>
        </template>
        <template #item.openInvoices="{ item }">
          <div v-if="item.openInvoices?.length" class="py-2 d-flex flex-column gap-1">
            <NuxtLink v-for="pi in item.openInvoices" :key="pi.invoiceId" :to="`/invoices/${pi.invoiceId}`" class="text-primary text-decoration-none">
              {{ pi.invoiceNumber }} · ${{ money(pi.outstandingAmount) }} open
              <span v-if="pi.paidAmount" class="text-caption text-medium-emphasis">(${{ money(pi.paidAmount) }} paid)</span>
            </NuxtLink>
          </div>
          <span v-else class="text-success">No open Credit PI</span>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const loading = ref(false)
const search = ref('')
const customers = ref<any[]>([])

const headers = [
  { title: 'Customer', key: 'customerName' },
  { title: 'Code', key: 'customerCode' },
  { title: 'Max Credit', key: 'maxCredit' },
  { title: 'Used Credit', key: 'usedCredit' },
  { title: 'Available Credit', key: 'availableCredit' },
  { title: 'Open Credit PIs', key: 'openInvoices', sortable: false },
]

const filtered = computed(() => {
  const value = search.value.trim().toLowerCase()
  if (!value) return customers.value
  return customers.value.filter(c => `${c.customerName} ${c.customerCode || ''}`.toLowerCase().includes(value))
})

const money = (value: any) => Number(value || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

async function load() {
  loading.value = true
  try { customers.value = await api.get<any[]>('/customer-credits') }
  finally { loading.value = false }
}

onMounted(load)
</script>
