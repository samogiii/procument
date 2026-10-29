<template>
  <v-combobox
    :model-value="modelValue"
    :items="tagNames"
    multiple
    chips
    closable-chips
    hide-selected
    density="compact"
    variant="outlined"
    :hide-details="true"
    :autofocus="autofocus"
    placeholder="Pick a tag or type a new one + Enter"
    prepend-inner-icon="mdi-tag-outline"
    :menu-props="{ maxHeight: 260 }"
    @update:model-value="onChange"
  >
    <template #chip="{ props: chipProps, item }">
      <v-chip v-bind="chipProps" size="small" :color="colorOf(item.title)" variant="flat" label>{{ item.title }}</v-chip>
    </template>
    <template #item="{ props: itemProps, item }">
      <v-list-item v-bind="itemProps" density="compact">
        <template #prepend>
          <v-icon icon="mdi-circle" size="12" :color="colorOf(item.title)" class="mr-2" />
        </template>
        <template #append>
          <span class="text-caption text-medium-emphasis">{{ usageOf(item.title) }}</span>
        </template>
      </v-list-item>
    </template>
  </v-combobox>
</template>

<script setup lang="ts">
import type { TaskTag } from '~/utils/taskBoard'

/**
 * Tags of one card: pick an existing tag of the board or type a new name and press Enter.
 * The server creates new tags and drops tags no card uses any more.
 */
const props = defineProps<{
  modelValue: string[]
  /** Every tag of the board. */
  tags: TaskTag[]
  autofocus?: boolean
}>()

const emit = defineEmits<{ 'update:modelValue': [names: string[]] }>()

const byName = computed(() => new Map(props.tags.map(t => [t.name.toLowerCase(), t])))
const tagNames = computed(() => props.tags.map(t => t.name))

function colorOf(name: string) {
  return byName.value.get(name.toLowerCase())?.color ?? 'grey'
}

function usageOf(name: string) {
  const usage = byName.value.get(name.toLowerCase())?.usage ?? 0
  return usage === 1 ? '1 card' : `${usage} cards`
}

function onChange(value: unknown) {
  const names: string[] = []
  for (const raw of Array.isArray(value) ? value : []) {
    const name = String(typeof raw === 'object' && raw ? (raw as any).title ?? '' : raw ?? '').trim().replace(/\s+/g, ' ')
    if (!name) continue
    // Typing an existing tag in other casing reuses that tag.
    const existing = byName.value.get(name.toLowerCase())?.name ?? name
    if (!names.some(n => n.toLowerCase() === existing.toLowerCase())) names.push(existing)
  }
  emit('update:modelValue', names)
}
</script>
