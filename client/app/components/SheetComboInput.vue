<template>
  <div class="sheet-combo">
    <input
      ref="inputEl"
      v-model="text"
      class="sheet-editor-input"
      :placeholder="placeholder"
      @keydown="onKeydown"
      @blur="finish(text, 0)"
    >
    <Teleport to="body">
      <div v-if="listStyle" class="sheet-combo-list elevation-8" :style="listStyle">
        <div
          v-if="canAdd"
          class="sheet-combo-item sheet-combo-add"
          :class="{ active: activeIndex === -1 }"
          @mousedown.prevent="finish(text, 0)"
        >
          <v-icon icon="mdi-plus" size="14" class="mr-1" />Add "{{ text.trim() }}"
        </div>
        <div
          v-for="(item, index) in shownItems"
          :key="item"
          class="sheet-combo-item"
          :class="{ active: index === activeIndex, selected: item === modelValue }"
          @mousedown.prevent="finish(item, 0)"
          @mouseenter="activeIndex = index"
        >
          {{ item }}
        </div>
        <div v-if="!shownItems.length && !canAdd" class="sheet-combo-empty">{{ strict ? 'No match' : 'Type to add a value' }}</div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
/**
 * Dropdown with free entry for one sheet cell: pick an item, or type a value that is not
 * in the list. Picking commits at once; Enter / Tab / leaving the cell commits the typed text;
 * Escape cancels. The list is teleported to <body> so the table's scroll box does not clip it.
 * `strict`: only listed items — typed text commits the matching item (or the only one left), otherwise cancels.
 */
const props = defineProps<{
  modelValue: string | null
  items: readonly string[]
  placeholder?: string
  strict?: boolean
}>()

const emit = defineEmits<{
  /** move: 0 = stay, 1 = next column (Tab), -1 = previous column (Shift+Tab). */
  commit: [value: string, move: 0 | 1 | -1]
  cancel: []
}>()

const inputEl = ref<HTMLInputElement | null>(null)
const text = ref(props.modelValue ?? '')
/** Until the user types, the whole list shows (the current value is only highlighted). */
const typed = ref(false)
const activeIndex = ref(-1)
const listStyle = ref<Record<string, string> | null>(null)

watch(text, () => {
  typed.value = true
  activeIndex.value = -1
})

const shownItems = computed(() => {
  const query = text.value.trim().toLowerCase()
  if (!typed.value || !query) return props.items
  return props.items.filter(item => item.toLowerCase().includes(query))
})

const canAdd = computed(() => {
  const value = text.value.trim().toLowerCase()
  return !props.strict && typed.value && !!value && !props.items.some(item => item.toLowerCase() === value)
})

function place() {
  const rect = inputEl.value?.getBoundingClientRect()
  if (!rect) return
  const below = window.innerHeight - rect.bottom
  const openUp = below < 220 && rect.top > below
  listStyle.value = {
    left: `${rect.left}px`,
    minWidth: `${Math.max(rect.width, 180)}px`,
    maxHeight: `${Math.max(120, Math.min(260, (openUp ? rect.top : below) - 12))}px`,
    ...(openUp ? { bottom: `${window.innerHeight - rect.top + 2}px` } : { top: `${rect.bottom + 2}px` }),
  }
}

/** Commit / cancel fire once: picking an item or Enter is followed by the input's blur. */
let done = false
function finish(value: string, move: 0 | 1 | -1) {
  if (done) return
  done = true
  if (props.strict) {
    const wanted = value.trim().toLowerCase()
    const match = props.items.find(item => item.toLowerCase() === wanted)
      ?? (shownItems.value.length === 1 ? shownItems.value[0] : undefined)
    if (match === undefined) {
      emit('cancel')
      return
    }
    value = match
  }
  emit('commit', value, move)
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    const last = shownItems.value.length - 1
    if (last < 0) return
    const step = event.key === 'ArrowDown' ? 1 : -1
    activeIndex.value = Math.min(last, Math.max(canAdd.value ? -1 : 0, activeIndex.value + step))
  } else if (event.key === 'Enter') {
    event.preventDefault()
    const picked = activeIndex.value >= 0 ? shownItems.value[activeIndex.value] : undefined
    finish(picked ?? text.value, 0)
  } else if (event.key === 'Tab') {
    event.preventDefault()
    finish(text.value, event.shiftKey ? -1 : 1)
  } else if (event.key === 'Escape') {
    event.preventDefault()
    done = true
    emit('cancel')
  }
}

onMounted(() => {
  inputEl.value?.focus()
  inputEl.value?.select()
  activeIndex.value = props.modelValue ? props.items.indexOf(props.modelValue) : -1
  place()
  window.addEventListener('scroll', place, true)
  window.addEventListener('resize', place)
})

onBeforeUnmount(() => {
  window.removeEventListener('scroll', place, true)
  window.removeEventListener('resize', place)
})
</script>

<style scoped>
.sheet-combo { width: 100%; }
</style>

<style>
.sheet-combo-list {
  position: fixed;
  z-index: 3000;
  overflow-y: auto;
  padding: 4px 0;
  border-radius: 6px;
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  border: 1px solid rgba(var(--v-border-color), 0.2);
  font-size: 12px;
}

.sheet-combo-item {
  padding: 5px 10px;
  cursor: pointer;
  white-space: nowrap;
}

.sheet-combo-item.active { background: rgba(var(--v-theme-primary), 0.12); }
.sheet-combo-item.selected { font-weight: 700; color: rgb(var(--v-theme-primary)); }
.sheet-combo-add { color: rgb(var(--v-theme-primary)); font-weight: 600; }

.sheet-combo-empty {
  padding: 5px 10px;
  color: rgba(var(--v-theme-on-surface), 0.55);
}
</style>
