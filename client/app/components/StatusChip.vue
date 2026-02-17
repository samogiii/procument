<template>
  <v-chip
    :color="color"
    :size="size"
    :variant="variant"
    :label="label"
  >
    {{ status }}
  </v-chip>
</template>

<script setup lang="ts">
const { statusColor } = useStatusColor()

const props = withDefaults(defineProps<{
  /** The status string to display (e.g. "Draft", "Accepted") */
  status: string
  /** Chip size */
  size?: 'x-small' | 'small' | 'default' | 'large'
  /** Chip variant */
  variant?: 'flat' | 'text' | 'elevated' | 'tonal' | 'outlined' | 'plain'
  /** Use label style (rectangular) */
  label?: boolean
}>(), {
  size: 'small',
  variant: 'flat',
  label: false,
})

const color = computed(() => statusColor(props.status))
</script>
