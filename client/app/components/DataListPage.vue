<template>
  <div>
    <PageHeader :title="title">
      <template #actions>
        <slot name="actions" />
      </template>
    </PageHeader>

    <v-card class="glass-card">
      <v-card-text>
        <!-- Search + optional status filter -->
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            :label="searchPlaceholder"
            single-line
            hide-details
            class="flex-grow-1 mx-2"
            style="min-width: 180px;"
          />
          <v-select
            v-if="statusOptions?.length"
            v-model="statusFilter"
            :items="filteredStatusOptions"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 120px; max-width: 220px;"
          />
          <slot name="filters" />
          <v-btn
            v-if="hasDlpActiveFilters"
            variant="tonal"
            color="error"
            size="small"
            prepend-icon="mdi-filter-off"
            class="align-self-center"
            @click="clearDlpFilters"
          >
            Clear
          </v-btn>
        </div>

        <!-- Server-side data table -->
        <v-data-table-server
          v-if="serverSide"
          :headers="headers"
          :items="internalItems"
          :items-length="totalItems"
          :loading="loading"
          :items-per-page="itemsPerPage"
          hover
          @update:options="loadServerItems"
          @click:row="onRowClick"
          :class="{ 'clickable-rows': !!detailRoute }"
        >
          <!-- Pass-through all column slots from parent, excluding 'default' (dialogs) and 'actions' (header) -->
          <template v-for="name in Object.keys($slots).filter(k => k !== 'default' && k !== 'actions')" :key="name" #[name]="slotProps">
            <slot :name="name" v-bind="slotProps ?? {}" />
          </template>

          <!-- Default action column if detailRoute is set -->
          <template v-if="detailRoute && !$slots['item.actions']" #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`${detailRoute}/${item.id}`" />
          </template>
        </v-data-table-server>

        <!-- Client-side data table -->
        <v-data-table
          v-else
          :headers="headers"
          :items="filteredClientItems"
          :search="search"
          :loading="loading"
          :items-per-page="itemsPerPage"
          hover
          @click:row="onRowClick"
          :class="{ 'clickable-rows': !!detailRoute }"
        >
          <template v-for="name in Object.keys($slots).filter(k => k !== 'default' && k !== 'actions' && k !== 'tfoot')" :key="name" #[name]="slotProps">
            <slot :name="name" v-bind="slotProps ?? {}" />
          </template>

          <template v-if="detailRoute && !$slots['item.actions']" #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`${detailRoute}/${item.id}`" />
          </template>

          <template v-if="$slots.tfoot" #tfoot>
            <slot name="tfoot" :items="searchFilteredItems" />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Default slot for dialogs and other content -->
    <slot />
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const route = useRoute()

const props = withDefaults(defineProps<{
  /** Page title */
  title: string
  /** Table column headers */
  headers: any[]
  /** API path for fetching data (e.g. "/quotes") */
  apiUrl: string
  /** Status filter options (e.g. ["All", "Draft", "Sent"]) */
  statusOptions?: string[]
  /** Route prefix for detail pages. Enables row click navigation. */
  detailRoute?: string
  /** Use server-side pagination */
  serverSide?: boolean
  /** Items per page */
  itemsPerPage?: number
  /** Search field placeholder */
  searchPlaceholder?: string
  /** Show selection checkboxes */
  showSelect?: boolean
  /** Selected items model value */
  modelValue?: any[]
  /** Custom client-side filter function applied after status filter */
  customFilter?: (items: any[]) => any[]
  /** Unique key for persisting filters in localStorage. If not set, filters are not persisted. */
  pageKey?: string
  /** Extra query params to append to server-side API calls (e.g. { customer: 'Acme', status: 'Sent' }) */
  extraParams?: Record<string, string | string[]>
}>(), {
  serverSide: false,
  itemsPerPage: 50,
  searchPlaceholder: 'Search...',
  showSelect: false,
  modelValue: () => [],
})

const emit = defineEmits(['update:modelValue'])
const selected = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

// If pageKey is provided, persist search + status to localStorage
const _pf = props.pageKey
  ? usePageFilters(props.pageKey, { search: '', status: [] as string[] })
  : null

const search = _pf ? _pf.filters.search : ref('')
const statusFilter = _pf ? _pf.filters.status : ref<string[]>([])
const clearDlpFilters = _pf ? _pf.clearFilters : () => {}
const hasDlpActiveFilters = _pf ? _pf.hasActiveFilters : computed(() => false)

// If URL has ?status=X, apply it once on load (overrides saved)
const initialStatus = route.query.status ? [route.query.status as string] : []
if (initialStatus.length && statusFilter.value.length === 0) {
  statusFilter.value = initialStatus
}
const loading = ref(false)
const internalItems = ref<any[]>([])
const totalItems = ref(0)

// Statuses that exist in items AFTER customFilter but BEFORE statusFilter.
// This makes the status dropdown cascade: pick a customer → only their statuses appear.
const availableStatuses = computed(() => {
  const set = new Set<string>()
  let source = internalItems.value
  if (props.customFilter) source = props.customFilter(source)
  source.forEach((item: any) => { if (item.status) set.add(item.status) })
  return set
})

const filteredStatusOptions = computed(() => {
  const declared = (props.statusOptions || []).filter(s => s !== 'All')
  // While loading (no items yet) show all declared options so the dropdown isn't empty.
  if (availableStatuses.value.size === 0) return declared
  // Otherwise narrow to statuses that actually exist given the current non-status filters.
  return declared.length
    ? declared.filter(s => availableStatuses.value.has(s))
    : Array.from(availableStatuses.value).sort()
})

const filteredClientItems = computed(() => {
  let result = internalItems.value
  if (statusFilter.value?.length) {
    result = result.filter((item: any) => statusFilter.value.includes(item.status))
  }
  if (props.customFilter) {
    result = props.customFilter(result)
  }
  return result
})

const searchFilteredItems = computed(() => {
  let result = filteredClientItems.value
  const q = search.value?.toLowerCase().trim()
  if (q) {
    result = result.filter((item: any) =>
      Object.values(item).some((val: any) =>
        val != null && String(val).toLowerCase().includes(q)
      )
    )
  }
  return result
})
const lastServerOptions = ref<any>({ page: 1, itemsPerPage: 50 })

// ─── Server-side loading ───
async function loadServerItems(options: any) {
  lastServerOptions.value = options
  loading.value = true
  try {
    const params = new URLSearchParams()
    params.append('page', options.page)
    params.append('pageSize', options.itemsPerPage)
    if (search.value) params.append('search', search.value)
    if (statusFilter.value?.length) {
      statusFilter.value.forEach((s: string) => params.append('status', s))
    }
    if (props.extraParams) {
      for (const [key, val] of Object.entries(props.extraParams)) {
        if (Array.isArray(val)) val.forEach((v: string) => { if (v) params.append(key, v) })
        else if (val) params.append(key, val)
      }
    }

    const url = `${props.apiUrl}?${params.toString()}`
    const res = await api.get<any>(url)

    internalItems.value = res.items || res.Items || []
    totalItems.value = res.totalCount || res.TotalCount || 0
  } catch (e) {
    console.error(`[DataListPage] Failed to load ${props.apiUrl}`, e)
  } finally {
    loading.value = false
  }
}

// ─── Client-side loading (progressive, race-safe) ───
let _clientLoadId = 0
async function loadClientItems() {
  const id = ++_clientLoadId
  loading.value = true
  internalItems.value = []
  let page = 1
  const batchSize = 200
  try {
    while (true) {
      const res = await api.get<any>(`${props.apiUrl}?page=${page}&pageSize=${batchSize}`)
      if (_clientLoadId !== id) return
      const batch: any[] = Array.isArray(res) ? res : (res.items ?? res.Items ?? [])
      const total: number = (!Array.isArray(res) && res != null)
        ? (res.totalCount ?? res.TotalCount ?? batch.length)
        : batch.length
      internalItems.value = [...internalItems.value, ...batch]
      if (batch.length < batchSize || internalItems.value.length >= total) break
      page++
    }
  } catch (e) {
    console.error(`[DataListPage] Failed to load ${props.apiUrl}`, e)
  } finally {
    if (_clientLoadId === id) loading.value = false
  }
}

// ─── Row click navigation ───
function onRowClick(_event: Event, row: any) {
  if (props.detailRoute && row?.item?.id) {
    navigateTo(`${props.detailRoute}/${row.item.id}`)
  }
}

// ─── Watchers for server-side search/status changes ───
let searchDebounce: any = null
if (props.serverSide) {
  watch(search, () => {
    clearTimeout(searchDebounce)
    searchDebounce = setTimeout(() => {
      loadServerItems({ ...lastServerOptions.value, page: 1 })
    }, 350)
  })
  watch(statusFilter, () => {
    loadServerItems({ ...lastServerOptions.value, page: 1 })
  })
  watch(() => props.extraParams, () => {
    loadServerItems({ ...lastServerOptions.value, page: 1 })
  }, { deep: true })
}

// ─── Lifecycle ───
if (!props.serverSide) {
  onMounted(() => loadClientItems())
}
</script>
