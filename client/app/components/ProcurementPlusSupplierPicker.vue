<template>
  <!-- Catalog supplier search for Procurement Plus (GET /procurement-plus/suppliers — not role-limited). -->
  <v-autocomplete
    :model-value="modelValue"
    :items="options"
    item-title="name"
    item-value="id"
    :label="label"
    :placeholder="placeholder"
    density="compact"
    variant="outlined"
    :loading="loading"
    no-filter
    clearable
    hide-details
    prepend-inner-icon="mdi-domain"
    @update:search="search"
    @update:model-value="pick"
  >
    <template #item="{ props: itemProps, item }">
      <v-list-item v-bind="itemProps" :subtitle="item.raw.email || undefined">
        <template #append>
          <v-chip v-if="item.raw.domainMatch" size="x-small" color="success" variant="tonal" prepend-icon="mdi-at" class="mr-1">same domain</v-chip>
          <v-icon v-if="item.raw.isPreferred" icon="mdi-star" color="amber" size="16" class="mr-1" />
          <v-chip v-if="item.raw.status && item.raw.status !== 'Approved'" size="x-small" color="warning" variant="tonal">{{ item.raw.status }}</v-chip>
        </template>
      </v-list-item>
    </template>
  </v-autocomplete>
</template>

<script setup lang="ts">
export interface PpSupplierOption { id: number; name: string; email?: string | null; status?: string | null; isPreferred?: boolean; domainMatch?: boolean }

const props = withDefaults(defineProps<{
  modelValue: number | null
  label?: string
  placeholder?: string
  /** Search text to start with (the company's email — searched by its domain — or its name). */
  initialSearch?: string
  /** Pick the first supplier using the searched email's domain when nothing is picked yet. */
  autoPickDomain?: boolean
  /** Search endpoint (new Procurement Plus: /ppe/suppliers). */
  searchPath?: string
}>(), { label: 'Catalog supplier', placeholder: 'Type a name, email or domain', initialSearch: '', autoPickDomain: false, searchPath: '/procurement-plus/suppliers' })

const emit = defineEmits<{
  'update:modelValue': [id: number | null]
  select: [supplier: PpSupplierOption | null]
}>()

const api = useApi()
const options = ref<PpSupplierOption[]>([])
const loading = ref(false)
let timer: ReturnType<typeof setTimeout> | null = null

function search(q: string) {
  if (timer) clearTimeout(timer)
  timer = setTimeout(() => load(q), 250)
}

async function load(q: string) {
  loading.value = true
  try {
    const found = await api.get<PpSupplierOption[]>(`${props.searchPath}?q=${encodeURIComponent(q || '')}`)
    // Keep the picked one in the list so its name still shows.
    const current = options.value.find(o => o.id === props.modelValue)
    options.value = current && !found.some(f => f.id === current.id) ? [current, ...found] : found
    const byDomain = found.find(f => f.domainMatch)
    if (props.autoPickDomain && props.modelValue == null && byDomain) pick(byDomain.id)
  } catch {
    options.value = []
  } finally {
    loading.value = false
  }
}

function pick(id: number | null) {
  emit('update:modelValue', id)
  emit('select', options.value.find(o => o.id === id) ?? null)
}

onMounted(() => load(props.initialSearch))
onBeforeUnmount(() => { if (timer) clearTimeout(timer) })
</script>
