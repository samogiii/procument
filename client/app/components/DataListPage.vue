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
        <div class="d-flex gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            :label="searchPlaceholder"
            single-line
            hide-details
            class="flex-grow-1"
          />
          <v-select
            v-if="statusOptions?.length"
            v-model="statusFilter"
            :items="statusOptions"
            label="Status"
            hide-details
            style="max-width: 160px"
          />
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
          :items="internalItems"
          :loading="loading"
          :items-per-page="itemsPerPage"
          hover
          @click:row="onRowClick"
          :class="{ 'clickable-rows': !!detailRoute }"
        >
          <template v-for="name in Object.keys($slots).filter(k => k !== 'default' && k !== 'actions')" :key="name" #[name]="slotProps">
            <slot :name="name" v-bind="slotProps ?? {}" />
          </template>

          <template v-if="detailRoute && !$slots['item.actions']" #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`${detailRoute}/${item.id}`" />
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
}>(), {
  serverSide: false,
  itemsPerPage: 10,
  searchPlaceholder: 'Search...',
  showSelect: false,
  modelValue: () => [],
})

const emit = defineEmits(['update:modelValue'])
const selected = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const search = ref('')
const statusFilter = ref('All')
const loading = ref(false)
const internalItems = ref<any[]>([])
const totalItems = ref(0)

// ─── Server-side loading ───
async function loadServerItems(options: any) {
  loading.value = true
  try {
    const params = new URLSearchParams()
    params.append('page', options.page)
    params.append('pageSize', options.itemsPerPage)
    if (search.value) params.append('search', search.value)
    if (statusFilter.value && statusFilter.value !== 'All') params.append('status', statusFilter.value)

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

// ─── Client-side loading ───
async function loadClientItems() {
  loading.value = true
  try {
    internalItems.value = await api.get<any[]>(props.apiUrl)
  } catch (e) {
    console.error(`[DataListPage] Failed to load ${props.apiUrl}`, e)
  } finally {
    loading.value = false
  }
}

// ─── Row click navigation ───
function onRowClick(_event: Event, row: any) {
  if (props.detailRoute && row?.item?.id) {
    navigateTo(`${props.detailRoute}/${row.item.id}`)
  }
}

// ─── Lifecycle ───
if (!props.serverSide) {
  onMounted(() => loadClientItems())
}
</script>
