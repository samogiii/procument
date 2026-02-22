<template>
  <DataListPage
    title="Purchase Orders"
    :headers="headers"
    api-url="/purchase-orders"
    :status-options="['All', 'Draft', 'Sent', 'Received', 'Cancelled']"
    detail-route="/purchase-orders"
  >
    <template #item.status="{ item }">
      <StatusChip :status="item.status" />
    </template>

    <template #item.totalAmount="{ item }">
      ${{ item.totalAmount?.toLocaleString() || '0' }}
    </template>

    <template #item.actions="{ item }">
      <v-btn icon="mdi-eye" variant="text" size="small" :to="`/purchase-orders/${item.id}`" />
    </template>
  </DataListPage>
</template>

<script setup lang="ts">
const headers = [
  { title: 'PO #', key: 'poNumber' },
  { title: 'Supplier', key: 'supplierName' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]
</script>
