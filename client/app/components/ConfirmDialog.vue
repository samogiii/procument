<template>
  <v-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" :max-width="maxWidth">
    <v-card class="glass-card">
      <v-card-title class="pa-4">{{ title }}</v-card-title>
      <v-card-text class="text-body-2 pt-0">
        {{ message }}
      </v-card-text>
      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn variant="text" @click="$emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn :color="confirmColor" variant="flat" @click="onConfirm">
          {{ confirmText }}
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
  title?: string
  /** Confirmation message */
  message?: string
  /** Confirm button text */
  confirmText?: string
  /** Confirm button color */
  confirmColor?: string
  /** Dialog max width */
  maxWidth?: number | string
}>(), {
  title: 'Confirm',
  message: 'Are you sure you want to proceed?',
  confirmText: 'Confirm',
  confirmColor: 'error',
  maxWidth: 400,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  'confirm': []
}>()

function onConfirm() {
  emit('confirm')
  emit('update:modelValue', false)
}
</script>
