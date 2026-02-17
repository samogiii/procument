<template>
  <v-card class="glass-card pa-4">
    <div class="d-flex align-center gap-3">
      <v-avatar :color="color" variant="tonal" size="40">
        <v-icon :icon="icon" size="20" />
      </v-avatar>
      <div class="flex-grow-1 min-width-0">
        <p class="text-caption text-medium-emphasis mb-0">{{ label }}</p>
        <div class="text-body-2 font-weight-medium mb-0">
          <slot>{{ value ?? '—' }}</slot>
        </div>
      </div>
    </div>
  </v-card>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  /** MDI icon name */
  icon: string
  /** Vuetify color name */
  color?: string
  /** Caption label above the value */
  label: string
  /** Display value (can also use default slot for complex content) */
  value?: string | number | null
}>(), {
  color: 'primary',
})
</script>
