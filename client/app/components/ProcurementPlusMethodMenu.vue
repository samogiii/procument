<template>
  <!--
    "How are you searching this supplier?" — ILS / Send Email / Search, one or more. Opens from its activator
    (an Add button, or a line's method chips) and hands the picked methods back on confirm.
  -->
  <v-menu v-model="open" :close-on-content-click="false" location="bottom end">
    <template #activator="{ props: menuProps }">
      <slot name="activator" :props="menuProps" />
    </template>
    <v-card class="pa-3 mm">
      <div class="text-caption font-weight-bold mb-2">{{ title }}</div>
      <v-chip-group v-model="picked" multiple column selected-class="mm__on">
        <v-chip
          v-for="m in LINE_METHODS"
          :key="m.value"
          :value="m.value"
          :color="m.color"
          :prepend-icon="m.icon"
          variant="outlined"
          size="small"
          filter
        >
          {{ m.title }}
        </v-chip>
      </v-chip-group>
      <div class="d-flex align-center mt-2">
        <span v-if="!picked.length" class="text-caption text-error">Pick at least one</span>
        <v-spacer />
        <v-btn size="small" variant="text" @click="open = false">Cancel</v-btn>
        <v-btn size="small" color="primary" variant="flat" :disabled="!picked.length" @click="confirm">{{ confirmText }}</v-btn>
      </div>
    </v-card>
  </v-menu>
</template>

<script setup lang="ts">
import { LINE_METHODS } from '~/utils/procurementPlus'

const props = withDefaults(defineProps<{
  /** Methods ticked when it opens. */
  initial?: string[]
  title?: string
  confirmText?: string
}>(), { initial: () => ['ILS'], title: 'How are you searching this supplier?', confirmText: 'Add' })
const emit = defineEmits<{ confirm: [methods: string[]] }>()

const open = ref(false)
const picked = ref<string[]>([])
watch(open, (v) => { if (v) picked.value = [...props.initial] })

function confirm() {
  open.value = false
  emit('confirm', [...picked.value])
}
</script>

<style scoped>
.mm { min-width: 300px; }
.mm__on { font-weight: 700; }
</style>
