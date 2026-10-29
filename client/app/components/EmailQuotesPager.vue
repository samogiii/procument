<template>
  <!-- Email Quotes tabs page by email: one page never splits an email's lines -->
  <div v-if="totalEmails > 0" class="d-flex align-center justify-center flex-wrap gap-3 mt-2">
    <v-pagination v-model="page" :length="pageCount" :total-visible="7" density="comfortable" size="small" />
    <v-select
      v-model="pageSize"
      :items="pageSizes ?? PAGE_SIZES"
      label="Emails per page"
      density="compact"
      variant="outlined"
      hide-details
      style="max-width: 150px; min-width: 130px;"
    />
  </div>
</template>

<script setup lang="ts">
/** pageSizes: the "Emails per page" choices (default 10–100). */
const props = defineProps<{ totalEmails: number; pageSizes?: number[] }>()
const page = defineModel<number>('page', { required: true })
const pageSize = defineModel<number>('pageSize', { required: true })

const PAGE_SIZES = [10, 20, 50, 100]
const pageCount = computed(() => Math.max(1, Math.ceil(props.totalEmails / pageSize.value)))
</script>
