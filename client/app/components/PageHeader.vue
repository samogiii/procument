<template>
  <div class="page-header mb-4 mb-md-6">
    <div class="d-flex align-center min-width-0">
      <v-btn
        v-if="backTo"
        icon="mdi-arrow-left"
        variant="text"
        @click="$router.back()"
        class="mr-1 mr-sm-2 flex-shrink-0"
        size="small"
      />
      <h1 class="page-header__title text-h6 text-sm-h5 font-weight-bold text-truncate">{{ title }}</h1>
      <v-chip
        v-if="count !== undefined"
        size="small"
        color="primary"
        variant="tonal"
        class="ml-2 ml-sm-3 flex-shrink-0"
      >
        {{ count }}
      </v-chip>
    </div>
    <div class="d-flex align-center gap-1 gap-sm-2 flex-shrink-0 mt-2 mt-sm-0">
      <slot name="actions" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  /** Page title displayed as h1 */
  title: string
  /** Shows a browser-history back button when supplied. */
  backTo?: string
  /** Optional count badge next to the title */
  count?: number | string
}>()
</script>
