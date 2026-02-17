<template>
  <div class="d-flex align-center justify-space-between mb-6">
    <div class="d-flex align-center">
      <v-btn
        v-if="backTo"
        icon="mdi-arrow-left"
        variant="text"
        :to="backTo"
        class="mr-2"
      />
      <h1 class="text-h5 font-weight-bold">{{ title }}</h1>
      <v-chip
        v-if="count !== undefined"
        size="small"
        color="primary"
        variant="tonal"
        class="ml-3"
      >
        {{ count }}
      </v-chip>
    </div>
    <div class="d-flex align-center gap-2">
      <slot name="actions" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  /** Page title displayed as h1 */
  title: string
  /** Optional back navigation route */
  backTo?: string
  /** Optional count badge next to the title */
  count?: number | string
}>()
</script>
