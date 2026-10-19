<template>
  <div class="sp-root">
    <div
      ref="boxEl"
      class="sp-box"
      :class="{ 'sp-box--open': open, 'sp-box--picked': !!supplierId }"
      @mousedown="onBoxMousedown"
    >
      <v-icon :icon="supplierId ? 'mdi-domain' : 'mdi-magnify'" size="14" class="sp-lead" />
      <input
        ref="inputEl"
        type="text"
        class="sp-input"
        :placeholder="placeholder"
        :value="modelValue"
        autocomplete="off"
        spellcheck="false"
        @input="onInput"
        @focus="onFocus"
        @blur="close"
        @keydown="onKeydown"
      />
      <v-progress-circular v-if="loading" indeterminate size="12" width="2" color="primary" class="sp-trail" />
      <v-icon v-else icon="mdi-chevron-down" size="16" class="sp-trail sp-chevron" />
    </div>

    <button
      v-if="showEmailAction && email"
      type="button"
      tabindex="-1"
      class="sp-email-chip"
      :title="`Talking to ${email} for this item — click to change`"
      @mousedown.prevent
      @click="emit('add-email')"
    >
      <v-icon icon="mdi-email-check-outline" size="12" />
      <span class="sp-email-text">{{ email }}</span>
      <v-icon icon="mdi-pencil-outline" size="11" class="sp-email-edit" />
    </button>
    <button
      v-else-if="showEmailAction && modelValue?.trim()"
      type="button"
      tabindex="-1"
      class="sp-email-btn"
      title="Pick or add the supplier email you talk to for this item"
      @mousedown.prevent
      @click="emit('add-email')"
    >
      <v-icon icon="mdi-email-plus-outline" size="12" />
      Email
    </button>

    <v-menu
      v-model="open"
      :target="boxEl"
      location="bottom start"
      :offset="4"
      persistent
      no-click-animation
      :capture-focus="false"
      :scrim="false"
      :close-on-content-click="false"
      :min-width="300"
      max-height="340"
      content-class="sp-menu"
    >
      <div class="sp-panel" @mousedown.prevent>
        <div v-if="!query" class="sp-hint">
          <v-icon icon="mdi-keyboard-outline" size="16" class="mr-2" />
          Type to search the supplier catalog
        </div>

        <template v-else>
          <div
            v-for="(s, i) in results"
            :key="s.id"
            class="sp-item"
            :class="{ 'sp-item--active': i === activeIndex }"
            @mouseenter="activeIndex = i"
            @click="pick(s)"
          >
            <div class="sp-avatar" :style="{ background: avatarColor(s.name) }">{{ initials(s.name) }}</div>
            <div class="sp-main">
              <div class="sp-name">
                <template v-for="(part, pIdx) in highlight(s.name)" :key="pIdx">
                  <mark v-if="part.hit">{{ part.text }}</mark><template v-else>{{ part.text }}</template>
                </template>
              </div>
              <div v-if="s.email || s.username" class="sp-sub">{{ s.email || s.username }}</div>
            </div>
            <span v-if="s.status && s.status !== 'Approved'" class="sp-tag sp-tag--warn">{{ s.status }}</span>
            <span v-if="s.dependency && s.dependency !== 'Normal'" class="sp-tag">{{ s.dependency }}</span>
          </div>

          <div v-if="!loading && !results.length" class="sp-hint">
            <v-icon icon="mdi-database-search-outline" size="16" class="mr-2" />
            No supplier matches “{{ query }}”
          </div>

          <div
            v-if="allowCreate && !hasExactMatch && !loading"
            class="sp-item sp-create"
            :class="{ 'sp-item--active': activeIndex === results.length }"
            @mouseenter="activeIndex = results.length"
            @click="create"
          >
            <div class="sp-avatar sp-avatar--add"><v-icon icon="mdi-plus" size="16" /></div>
            <div class="sp-main">
              <div class="sp-name">Add “{{ query }}” as a new supplier</div>
              <div class="sp-sub">Opens a form for the name and email</div>
            </div>
          </div>
        </template>
      </div>
    </v-menu>
  </div>
</template>

<script setup lang="ts">
/**
 * Boxed, searchable supplier dropdown for the RFQ / procument quote grids.
 * Still a free-text field (repair shops may be one-off names), but suggests Catalog suppliers,
 * reports the picked supplier's id/status/dependency, and offers "Add new supplier" when the
 * typed name has no exact match. The add-supplier / add-email dialogs live once on the page.
 */
interface SupplierOption { id: number; name: string; username?: string; status?: string; dependency?: string; email?: string }

const props = withDefaults(defineProps<{
  modelValue: string
  supplierId?: number | null
  /** The supplier email used for this RFQ item — shown under the box. */
  email?: string | null
  placeholder?: string
  allowCreate?: boolean
  showEmailAction?: boolean
}>(), {
  supplierId: null,
  email: null,
  placeholder: 'Search supplier...',
  allowCreate: true,
  showEmailAction: true,
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'update:supplierId': [value: number | null]
  'update:email': [value: string | null]
  select: [supplier: SupplierOption]
  create: [name: string]
  'add-email': []
}>()

const api = useApi()
const boxEl = ref<HTMLElement>()
const inputEl = ref<HTMLInputElement>()
const open = ref(false)
const loading = ref(false)
const results = ref<SupplierOption[]>([])
const activeIndex = ref(0)
const query = computed(() => (props.modelValue || '').trim())

const normalize = (v: string) => v.replace(/\s+/g, ' ').trim().toLowerCase()
const hasExactMatch = computed(() => results.value.some(s => normalize(s.name) === normalize(query.value)))
const optionCount = computed(() => results.value.length + (props.allowCreate && !hasExactMatch.value && query.value ? 1 : 0))

let debounce: ReturnType<typeof setTimeout> | null = null
let requestSeq = 0
let lastQuery = ''
function search(text: string) {
  if (debounce) clearTimeout(debounce)
  const q = text.trim()
  lastQuery = q
  if (!q) { results.value = []; loading.value = false; return }
  loading.value = true
  debounce = setTimeout(async () => {
    const seq = ++requestSeq
    try {
      const data = await api.get<SupplierOption[]>(`/suppliers/search?q=${encodeURIComponent(q)}`)
      if (seq === requestSeq) results.value = data || []
    } catch {
      if (seq === requestSeq) results.value = []
    } finally {
      if (seq === requestSeq) { loading.value = false; activeIndex.value = 0 }
    }
  }, 250)
}

function onInput(e: Event) {
  const value = (e.target as HTMLInputElement).value
  emit('update:modelValue', value)
  if (props.supplierId) emit('update:supplierId', null)
  if (props.email) emit('update:email', null) // a retyped supplier means a different contact email
  open.value = true
  search(value)
}

function onFocus() {
  open.value = true
  if (query.value && query.value !== lastQuery) search(query.value)
}

function onBoxMousedown(e: MouseEvent) {
  if (e.target !== inputEl.value) { e.preventDefault(); inputEl.value?.focus() }
}

function close() {
  open.value = false
}

function pick(s: SupplierOption) {
  emit('update:modelValue', s.name)
  emit('update:supplierId', s.id)
  emit('select', s)
  close()
  inputEl.value?.blur()
}

function create() {
  const name = query.value
  close()
  inputEl.value?.blur()
  emit('create', name)
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    e.preventDefault()
    e.stopPropagation()
    if (!open.value) { open.value = true; return }
    if (!optionCount.value) return
    const step = e.key === 'ArrowDown' ? 1 : -1
    activeIndex.value = (activeIndex.value + step + optionCount.value) % optionCount.value
  } else if (e.key === 'Enter' && open.value && optionCount.value) {
    e.preventDefault()
    e.stopPropagation()
    if (activeIndex.value < results.value.length) pick(results.value[activeIndex.value]!)
    else create()
  } else if (e.key === 'Escape' && open.value) {
    e.stopPropagation()
    close()
  } else if (e.key === 'Tab') {
    close()
  }
}

function highlight(name: string) {
  const q = query.value.toLowerCase()
  const idx = q ? name.toLowerCase().indexOf(q) : -1
  if (idx < 0) return [{ text: name, hit: false }]
  return [
    { text: name.slice(0, idx), hit: false },
    { text: name.slice(idx, idx + q.length), hit: true },
    { text: name.slice(idx + q.length), hit: false },
  ].filter(p => p.text)
}

function initials(name: string) {
  const words = name.trim().split(/\s+/).filter(Boolean)
  return ((words[0]?.[0] || '') + (words[1]?.[0] || '')).toUpperCase() || '?'
}

const AVATAR_COLORS = ['#5c6bc0', '#26a69a', '#ef6c00', '#8e24aa', '#00897b', '#3949ab', '#c0ca33', '#6d4c41']
function avatarColor(name: string) {
  let h = 0
  for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0
  return AVATAR_COLORS[h % AVATAR_COLORS.length]
}

onBeforeUnmount(() => { if (debounce) clearTimeout(debounce) })
</script>

<style scoped>
.sp-root {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  min-width: 0;
}
.sp-box {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  height: 32px;
  padding: 0 8px;
  border: 1px solid var(--card-border, rgba(var(--v-border-color), var(--v-border-opacity)));
  border-radius: 8px;
  background: rgb(var(--v-theme-surface));
  cursor: text;
  transition: border-color 0.15s, box-shadow 0.15s, background 0.15s;
}
.sp-box:hover {
  border-color: rgba(var(--v-theme-primary), 0.55);
}
.sp-box--open {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 3px rgba(var(--v-theme-primary), 0.15);
}
.sp-box--picked .sp-lead {
  color: rgb(var(--v-theme-primary));
  opacity: 1;
}
.sp-lead {
  opacity: 0.5;
  flex-shrink: 0;
}
.sp-input {
  flex: 1;
  min-width: 0;
  height: 100%;
  border: none;
  outline: none;
  background: transparent;
  color: rgb(var(--v-theme-on-surface));
  font-size: 12px;
  font-weight: 500;
  font-family: 'Inter', sans-serif;
}
.sp-input::placeholder {
  opacity: 0.45;
  font-style: italic;
  font-weight: 400;
}
.sp-trail {
  flex-shrink: 0;
}
.sp-chevron {
  opacity: 0.45;
  transition: transform 0.15s;
}
.sp-box--open .sp-chevron {
  transform: rotate(180deg);
}
.sp-email-btn {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 1px 7px;
  border-radius: 10px;
  border: 1px dashed rgba(var(--v-theme-primary), 0.5);
  background: rgba(var(--v-theme-primary), 0.06);
  color: rgb(var(--v-theme-primary));
  font-size: 10px;
  font-weight: 600;
  line-height: 16px;
  cursor: pointer;
  transition: background 0.15s;
}
.sp-email-btn:hover {
  background: rgba(var(--v-theme-primary), 0.14);
}
.sp-email-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  max-width: 100%;
  padding: 1px 7px;
  border-radius: 10px;
  border: 1px solid rgba(34, 197, 94, 0.45);
  background: rgba(34, 197, 94, 0.1);
  color: #16a34a;
  font-size: 10.5px;
  font-weight: 600;
  line-height: 16px;
  cursor: pointer;
  transition: background 0.15s;
}
.sp-email-chip:hover {
  background: rgba(34, 197, 94, 0.18);
}
.sp-email-text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sp-email-edit {
  opacity: 0;
  transition: opacity 0.15s;
}
.sp-email-chip:hover .sp-email-edit {
  opacity: 0.8;
}
</style>

<style>
/* The menu is teleported to the overlay container, so its styles can't be scoped. */
.sp-menu {
  border-radius: 10px !important;
  overflow: hidden;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.22) !important;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.sp-menu .sp-panel {
  background: rgb(var(--v-theme-surface));
  padding: 4px;
  max-height: 340px;
  overflow-y: auto;
}
.sp-menu .sp-hint {
  display: flex;
  align-items: center;
  padding: 12px;
  font-size: 12px;
  opacity: 0.6;
}
.sp-menu .sp-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 7px 8px;
  border-radius: 7px;
  cursor: pointer;
}
.sp-menu .sp-item--active {
  background: rgba(var(--v-theme-primary), 0.1);
}
.sp-menu .sp-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 11px;
  font-weight: 700;
}
.sp-menu .sp-avatar--add {
  background: rgba(var(--v-theme-primary), 0.15);
  color: rgb(var(--v-theme-primary));
}
.sp-menu .sp-main {
  flex: 1;
  min-width: 0;
}
.sp-menu .sp-name {
  font-size: 13px;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.sp-menu .sp-name mark {
  background: rgba(var(--v-theme-primary), 0.2);
  color: inherit;
  border-radius: 2px;
}
.sp-menu .sp-sub {
  font-size: 11px;
  opacity: 0.6;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.sp-menu .sp-tag {
  flex-shrink: 0;
  font-size: 10px;
  font-weight: 600;
  padding: 1px 6px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.08);
}
.sp-menu .sp-tag--warn {
  background: rgba(245, 158, 11, 0.18);
  color: #d97706;
}
.sp-menu .sp-create {
  margin-top: 2px;
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 0 0 7px 7px;
}
.sp-menu .sp-create .sp-name {
  color: rgb(var(--v-theme-primary));
  font-weight: 600;
}
</style>
