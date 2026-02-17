<template>
  <DataListPage
    title="Invoices"
    :headers="headers"
    api-url="/invoices"
    :status-options="['All', 'Pending', 'Paid', 'Overdue']"
    detail-route="/invoices"
    server-side
  >
    <template #item.status="{ item }">
      <StatusChip :status="item.status" />
    </template>

    <template #item.totalAmount="{ item }">
      ${{ item.totalAmount?.toLocaleString() || '0' }}
    </template>

    <template #item.actions="{ item }">
      <v-btn icon="mdi-eye" variant="text" size="small" :to="`/invoices/${item.id}`" />
    </template>
  </DataListPage>
</template>

<script setup lang="ts">
const headers = [
  { title: 'Invoice #', key: 'invoiceNumber' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Total', key: 'totalAmount' },
  { title: 'Status', key: 'status' },
  { title: '', key: 'actions', sortable: false, width: '60px' },
]
</script>
