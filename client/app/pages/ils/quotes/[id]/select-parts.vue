<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" size="small" @click="navigateTo(`/ils/quotes/${id}`)" />
      <div>
        <h1 class="text-h5 font-weight-bold">{{ quote?.quoteNumber || 'ILS Quote' }}</h1>
        <p class="text-caption text-medium-emphasis mb-0">Step 2 of 3 — Select parts</p>
      </div>
      <v-spacer />
      <v-chip size="small" variant="tonal" :color="selected.length ? 'primary' : 'grey'">
        {{ selected.length }} selected
      </v-chip>
      <v-btn
        color="primary"
        variant="flat"
        append-icon="mdi-arrow-right"
        :disabled="!selected.length"
        @click="next"
      >
        Next: Serials
      </v-btn>
    </div>

    <v-stepper :model-value="2" :items="['Details', 'Select Parts', 'Serials & Pricing']" hide-actions class="mb-4 glass-card" flat />

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search parts..."
            single-line
            hide-details
            class="flex-grow-1"
            style="min-width: 200px;"
          />
          <v-select
            v-model="conditionFilter"
            :items="conditionOptions"
            label="Condition"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            style="min-width: 160px; max-width: 240px;"
          />
        </div>

        <v-data-table
          v-model="selected"
          :headers="headers"
          :items="filteredItems"
          :search="search"
          :loading="loading"
          :items-per-page="50"
          show-select
          item-value="id"
          return-object
          hover
          density="comfortable"
        >
          <template #item.price="{ item }">
            <span class="font-weight-medium" style="font-family: monospace; color: #4ade80;">${{ formatPrice(item.price) }}</span>
          </template>
          <template #item.condition="{ item }">
            <v-chip v-if="item.condition" size="small" variant="tonal" :color="conditionColor(item.condition)">{{ item.condition }}</v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.serialCount="{ item }">
            <v-chip size="small" variant="tonal" :color="item.serialCount > 0 ? 'primary' : 'grey'" prepend-icon="mdi-barcode">
              {{ item.serialCount || 0 }}
            </v-chip>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2500" location="bottom right">{{ snackbarText }}</v-snackbar>
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const api = useApi()
const id = route.params.id as string

const loading = ref(false)
const quote = ref<any>(null)
const allItems = ref<any[]>([])
const selected = ref<any[]>([])
const search = ref('')
const conditionFilter = ref<string[]>([])

const conditionOptions = ['NE', 'OH', 'SV', 'AR', 'RP', 'NS', 'FN', 'IN']

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
function showSnack(text: string, color = 'success') {
  snackbarText.value = text; snackbarColor.value = color; snackbar.value = true
}

function conditionColor(cond: string) {
  const map: Record<string, string> = {
    NE: 'success', OH: 'info', SV: 'warning', AR: 'error', RP: 'secondary', NS: 'grey', FN: 'primary', IN: 'cyan',
  }
  return map[cond] || 'grey'
}

const headers = [
  { title: 'Part Number', key: 'partNumberName', width: '160px' },
  { title: 'Description', key: 'description' },
  { title: 'Alt P/N', key: 'altPartNumber', width: '130px' },
  { title: 'Condition', key: 'condition', width: '110px' },
  { title: 'Price', key: 'price', width: '110px' },
  { title: 'Serials', key: 'serialCount', width: '100px', sortable: false },
]

const filteredItems = computed(() => {
  let result = allItems.value
  if (conditionFilter.value.length) result = result.filter(i => conditionFilter.value.includes(i.condition))
  return result
})

async function load() {
  loading.value = true
  try {
    const [items, q] = await Promise.all([
      api.get<any[]>('/ils'),
      api.get<any>(`/ils-quotes/${id}`),
    ])
    allItems.value = items
    quote.value = q
    // Pre-select parts already on the quote (supports Back navigation)
    const existingIds = new Set((q.items || []).map((it: any) => it.ilsItemId).filter(Boolean))
    selected.value = items.filter(i => existingIds.has(i.id))
  } catch {
    showSnack('Failed to load parts', 'error')
  } finally {
    loading.value = false
  }
}

function next() {
  if (!selected.value.length) return
  const ids = selected.value.map(i => i.id).join(',')
  navigateTo(`/ils/quotes/${id}/serials?items=${ids}`)
}

onMounted(load)
</script>
