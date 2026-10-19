<template>
  <v-card class="glass-card pa-4 w-100 cursor-pointer" rounded="lg" hover @click="dialog = true">
    <div class="d-flex align-start gap-3 h-100">
      <v-icon icon="mdi-hand-coin-outline" color="teal" size="32" class="mt-1" />
      <div class="flex-1-1 min-width-0">
        <div class="text-caption text-medium-emphasis mb-2">
          Supplier Credits
          <span v-if="accounts.length">({{ accounts.length }})</span>
        </div>
        <div v-if="loading && !accounts.length" class="py-2">
          <v-progress-circular indeterminate size="20" width="2" color="teal" />
        </div>
        <template v-else>
          <div class="d-flex align-center gap-2">
            <v-chip size="x-small" color="teal" variant="tonal">$ USD</v-chip>
            <span class="supplier-credit-total font-weight-bold text-teal">${{ formatPrice(total) }}</span>
          </div>

          <!-- The largest few, so the box answers "with whom?" at a glance -->
          <div v-if="accounts.length" class="mt-2">
            <div v-for="account in topAccounts" :key="account.id" class="supplier-credit-line">
              <span class="supplier-credit-name" :title="`${account.supplierName} · ${account.companyPresetName}`">
                {{ account.supplierName }}
                <span class="text-medium-emphasis">· {{ account.companyPresetName }}</span>
              </span>
              <span class="font-weight-bold text-teal">${{ formatPrice(account.balance) }}</span>
            </div>
            <div class="text-caption text-medium-emphasis mt-1">
              {{ accounts.length > TOP_COUNT ? `+${accounts.length - TOP_COUNT} more — ` : '' }}click for details
            </div>
          </div>
          <div v-else class="text-caption text-medium-emphasis mt-1">No credit held by suppliers</div>
        </template>
      </div>
    </div>
  </v-card>

  <v-dialog v-model="dialog" max-width="860">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-hand-coin-outline" color="teal" class="mr-2" />
        Supplier Credits
        <v-chip size="small" color="teal" variant="tonal" class="ml-3">${{ formatPrice(total) }}</v-chip>
        <v-spacer />
        <v-btn icon="mdi-refresh" variant="text" size="small" :loading="loading" title="Refresh" @click="load" />
        <v-btn icon="mdi-close" variant="text" size="small" @click="dialog = false" />
      </v-card-title>
      <v-card-text class="pa-4 pt-0">
        <div class="text-body-2 text-medium-emphasis mb-3">
          Prepaid credit each supplier holds for us, per company. It can only be used on that company's POs with that supplier.
        </div>
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          placeholder="Search supplier or company..."
          density="compact"
          variant="outlined"
          hide-details
          clearable
          class="mb-3"
        />
        <v-data-table
          :headers="headers"
          :items="accounts"
          :search="search ?? ''"
          :sort-by="[{ key: 'balance', order: 'desc' }]"
          :loading="loading"
          density="compact"
          items-per-page="-1"
          hide-default-footer
          fixed-header
          height="min(55vh, 520px)"
          class="border rounded"
        >
          <template #item.supplierName="{ item }">
            <span class="font-weight-medium">{{ item.supplierName }}</span>
          </template>
          <template #item.companyPresetName="{ item }">
            <v-chip size="small" variant="tonal" color="primary">{{ item.companyPresetName }}</v-chip>
          </template>
          <template #item.balance="{ item }">
            <span class="font-weight-bold text-teal">${{ formatPrice(item.balance) }}</span>
          </template>
          <template #item.lastEntryAt="{ item }">
            <span class="text-medium-emphasis">{{ item.lastEntryAt ? new Date(item.lastEntryAt).toLocaleDateString() : '—' }}</span>
          </template>
          <template #no-data>
            <div class="text-center text-medium-emphasis py-6">No supplier holds credit for us right now.</div>
          </template>
        </v-data-table>
        <div class="d-flex align-center mt-3">
          <span class="text-body-2 text-medium-emphasis">
            {{ search ? 'Shown total' : 'Total' }}:
            <strong class="text-teal">${{ formatPrice(search ? visibleTotal : total) }}</strong>
          </span>
          <v-spacer />
          <v-btn
            v-if="authStore.supplierCredits"
            variant="tonal"
            color="teal"
            append-icon="mdi-arrow-right"
            to="/supplier-credits"
          >
            Open Supplier Credits
          </v-btn>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { formatPrice } from '~/utils/formatPrice'

interface CreditAccount {
  id: number
  supplierName: string
  companyPresetName: string
  balance: number
  lastEntryAt: string | null
}

/** Wallets-page box: prepaid credit suppliers hold for us (USD), the largest few, and a per supplier × company dialog. */
const TOP_COUNT = 3

const api = useApi()
const authStore = useAuthStore()

const accounts = ref<CreditAccount[]>([])
const loading = ref(false)
const dialog = ref(false)
const search = ref<string | null>('')
/** Total of the rows the search leaves (same supplier / company text match as the table). */
const visibleTotal = computed(() => {
  const query = (search.value ?? '').trim().toLowerCase()
  return accounts.value
    .filter(a => !query || a.supplierName.toLowerCase().includes(query) || a.companyPresetName.toLowerCase().includes(query))
    .reduce((sum, a) => sum + a.balance, 0)
})

const headers = [
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Company', key: 'companyPresetName' },
  { title: 'Credit', key: 'balance', align: 'end' as const },
  { title: 'Last activity', key: 'lastEntryAt', align: 'end' as const },
]

const total = computed(() => accounts.value.reduce((sum, account) => sum + account.balance, 0))
const topAccounts = computed(() => accounts.value.slice(0, TOP_COUNT))

async function load() {
  loading.value = true
  try {
    // Prepaid accounts with a balance, largest first (the endpoint's default order).
    const result = await api.get<{ items: CreditAccount[] }>('/supplier-credits?kind=Prepaid&onlyWithBalance=true&pageSize=-1')
    accounts.value = result.items.map(item => ({ ...item, balance: Number(item.balance) || 0 }))
  } catch {
    accounts.value = []
  } finally {
    loading.value = false
  }
}

watch(dialog, (open) => {
  if (open) load()
})

onMounted(load)
</script>

<style scoped>
.min-width-0 { min-width: 0; }

/* Same size as the wallet balances beside it. */
.supplier-credit-total {
  font-size: 1.75rem;
  line-height: 1.2;
}

.supplier-credit-line {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
  font-size: 0.8125rem;
  padding: 1px 0;
}

.supplier-credit-name {
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
