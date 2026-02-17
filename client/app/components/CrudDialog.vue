<template>
  <v-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" :max-width="maxWidth" persistent>
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <span>{{ title }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="$emit('update:modelValue', false)" />
      </v-card-title>

      <v-divider />

      <v-card-text class="pa-4">
        <slot />
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="$emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" :loading="loading" @click="$emit('save')">
          {{ saveText }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  /** v-model for dialog visibility */
  modelValue: boolean
  /** Dialog title */
  title: string
  /** Show loading spinner on Save button */
  loading?: boolean
  /** Custom text for the save button */
  saveText?: string
  /** Dialog max width */
  maxWidth?: number | string
}>(), {
  loading: false,
  saveText: 'Save',
  maxWidth: 500,
})

defineEmits<{
  'update:modelValue': [value: boolean]
  'save': []
}>()
</script>
