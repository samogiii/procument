<template>
  <div>
    <PageHeader :title="title">
      <template #actions>
        <v-chip
          v-if="showTotalSum && tableTotalAmountSum"
          color="success"
          variant="tonal"
          size="small"
          prepend-icon="mdi-sigma"
          class="mr-2"
        >
          Total: ${{ formatPrice(tableTotalAmountSum) }}
        </v-chip>
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
            :items="statusSelectItems"
            item-title="label"
            item-value="value"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 120px; max-width: 260px;"
          >
            <template #item="{ props: iProps, item }">
              <v-list-item v-bind="iProps" :class="{ 'text-disabled': !item.raw.available }" density="compact">
                <template #prepend="{ isSelected }">
                  <v-checkbox-btn :model-value="isSelected" density="compact" class="mr-1" />
                </template>
                <template #append>
                  <v-icon v-if="!item.raw.available" icon="mdi-eye-off-outline" size="14" class="text-disabled ml-1" />
                </template>
              </v-list-item>
            </template>
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllStatuses ? 'Show available only' : 'Show all statuses'"
                :prepend-icon="showAllStatuses ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllStatuses = !showAllStatuses"
              />
            </template>
          </v-select>
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

        <!-- Optional summary row between filters and table — receives a pre-computed scalar only (no raw arrays) -->
        <slot v-if="$slots['before-table']" name="before-table" :total-amount-sum="tableTotalAmountSum" />

        <!-- Server-side data table -->
        <v-data-table-server
          v-if="serverSide"
          :headers="headers"
          :items="filteredClientItems"
          :items-length="totalItems"
          :loading="loading"
          v-model:page="currentPage"
          v-model:items-per-page="currentItemsPerPage"
          :items-per-page-options="pageOptions"
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
          v-model:page="currentPage"
          v-model:items-per-page="currentItemsPerPage"
          :items-per-page-options="pageOptions"
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

const pageOptions = [
  { value: 50, title: '50' },
  { value: 75, title: '75' },
  { value: 100, title: '100' },
  { value: 150, title: '150' },
  { value: 200, title: '200' },
  { value: 300, title: '300' },
  { value: 500, title: '500' },
  { value: 1000, title: '1000' },
  { value: -1, title: 'All' },
]

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
  /** Show a live total-amount sum chip in the page header. Reads from frontend (current page for server-side, all items for client-side). */
  showTotalSum?: boolean
}>(), {
  serverSide: false,
  itemsPerPage: 50,
  searchPlaceholder: 'Search...',
  showSelect: false,
  modelValue: () => [],
  showTotalSum: false,
})

const emit = defineEmits(['update:modelValue'])
const selected = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

// If pageKey is provided, persist search + status + page to localStorage
const _pf = props.pageKey
  ? usePageFilters(props.pageKey, { search: '', status: [] as string[], page: 1, itemsPerPage: props.itemsPerPage })
  : null

const search = _pf ? _pf.filters.search : ref('')
const statusFilter = _pf ? _pf.filters.status : ref<string[]>([])
const currentPage = _pf ? _pf.filters.page : ref(1)
const currentItemsPerPage = _pf ? _pf.filters.itemsPerPage : ref(props.itemsPerPage)
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
// Stores only scalar/metadata fields from the server response — items array is excluded to avoid storing data twice
const serverMeta = ref<any>({})

// Pre-computed totalAmount sum — scalar only (no raw arrays passed around).
// Priority: backend-provided sum (all pages) → else sum from current page items.
const tableTotalAmountSum = computed<number>(() => {
  if (serverMeta.value.totalAmountSum != null) return serverMeta.value.totalAmountSum
  return filteredClientItems.value.reduce((s: number, item: any) => s + (Number(item.totalAmount) || 0), 0)
})


// Statuses that exist in items AFTER customFilter but BEFORE statusFilter.
// This makes the status dropdown cascade: pick a customer → only their statuses appear.
const availableStatuses = computed(() => {
  const set = new Set<string>()
  let source = internalItems.value
  if (props.customFilter) source = props.customFilter(source)
  source.forEach((item: any) => { if (item.status) set.add(item.status) })
  return set
})

const showAllStatuses = ref(false)

const statusSelectItems = computed(() => {
  const declared = (props.statusOptions || []).filter(s => s !== 'All')
  if (showAllStatuses.value) {
    return declared.map(s => ({ label: s, value: s, available: availableStatuses.value.size === 0 || availableStatuses.value.has(s) }))
  }
  // Default: show only available + already-selected (so selections don't vanish)
  return declared
    .filter(s => availableStatuses.value.size === 0 || availableStatuses.value.has(s) || (statusFilter.value as string[]).includes(s))
    .map(s => ({ label: s, value: s, available: true }))
})

// keep for backwards compat (used nowhere else now, but safe to leave)
const filteredStatusOptions = statusSelectItems

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
const sort = useServerSort()

// ─── Server-side loading ───
async function loadServerItems(options: any) {
  lastServerOptions.value = options
  currentPage.value = options.page
  currentItemsPerPage.value = options.itemsPerPage
  sort.capture(options)
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
    sort.appendTo(params)

    const url = `${props.apiUrl}?${params.toString()}`
    const res = await api.get<any>(url)

    internalItems.value = res.items || res.Items || []
    totalItems.value = res.totalCount || res.TotalCount || 0
    // Store only scalar fields — exclude the items array to avoid keeping data in memory twice
    const { items: _i, Items: _I, ...meta } = res
    serverMeta.value = meta
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
      currentPage.value = 1
      loadServerItems({ ...lastServerOptions.value, page: 1 })
    }, 350)
  })
  watch(statusFilter, () => {
    currentPage.value = 1
    loadServerItems({ ...lastServerOptions.value, page: 1 })
  })
  watch(() => props.extraParams, () => {
    currentPage.value = 1
    loadServerItems({ ...lastServerOptions.value, page: 1 })
  }, { deep: true })
}

// ─── Lifecycle ───
if (!props.serverSide) {
  onMounted(() => loadClientItems())
}
</script>
