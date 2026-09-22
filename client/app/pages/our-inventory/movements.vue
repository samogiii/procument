<template>
  <div>
    <PageHeader title="Stock Movements" />

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field v-model="searchInput" label="Search P/N, reference or reason" prepend-inner-icon="mdi-magnify" hide-details clearable
            density="compact" variant="outlined" style="min-width: 240px; max-width: 360px" @keyup.enter="search = searchInput || ''" @click:clear="search = ''" />
          <v-select v-model="types" :items="TYPE_ITEMS" label="Type" multiple chips closable-chips clearable hide-details
            density="compact" variant="outlined" style="min-width: 200px; max-width: 420px" />
          <v-text-field v-model="from" type="date" label="From" clearable hide-details density="compact" variant="outlined" style="max-width: 180px" />
          <v-text-field v-model="to" type="date" label="To" clearable hide-details density="compact" variant="outlined" style="max-width: 180px" />
        </div>
        <StockMovementsTable show-part :types="types" :from="from || undefined" :to="to || undefined" :search="search || undefined" />
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
/** Read-only ledger across all stock: every receipt, issue, adjustment and transfer. */
const TYPE_ITEMS = [
  { title: 'Receipt', value: 'Receipt' }, { title: 'Opening', value: 'Opening' }, { title: 'Issue', value: 'Issue' },
  { title: 'Adjust', value: 'Adjust' }, { title: 'Transfer in', value: 'TransferIn' }, { title: 'Transfer out', value: 'TransferOut' },
  { title: 'Return to supplier', value: 'ReturnToSupplier' }, { title: 'Customer return', value: 'CustomerReturn' },
]

const { filters } = usePageFilters('stock-movements', { types: [] as string[], from: '', to: '', search: '' })
const types = filters.types
const from = filters.from
const to = filters.to
const search = filters.search
const searchInput = ref(search.value)
</script>
