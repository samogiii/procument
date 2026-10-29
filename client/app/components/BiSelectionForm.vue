<template>
  <v-card class="glass-card bi-form" :class="{ 'bi-form--first': !cancellable }">
    <div v-if="!cancellable" class="bi-form__intro">
      <v-avatar color="primary" variant="tonal" size="48">
        <v-icon icon="mdi-chart-timeline-variant-shimmer" size="26" />
      </v-avatar>
      <div>
        <h2 class="text-h6 font-weight-bold">Build your BI</h2>
        <p class="text-body-2 text-medium-emphasis mb-0">
          Pick the customers (all, whole bases or one by one) and the number of days. Your choice is kept on this device until you press Clear.
        </p>
      </div>
    </div>

    <div class="bi-form__grid">
      <!-- ── Customers ── -->
      <section>
        <div class="bi-form__step"><span class="bi-form__num">1</span>Customers</div>
        <v-btn-toggle v-model="scope" mandatory color="primary" variant="outlined" density="comfortable" divided class="mb-3 bi-form__scope">
          <v-btn value="all" prepend-icon="mdi-account-group-outline">All customers</v-btn>
          <v-btn value="bases" prepend-icon="mdi-map-marker-multiple-outline">By base</v-btn>
          <v-btn value="pick" prepend-icon="mdi-account-multiple-check-outline">Select customers</v-btn>
        </v-btn-toggle>

        <!-- ── Whole bases ── -->
        <v-expand-transition>
          <div v-if="scope === 'bases'">
            <v-skeleton-loader v-if="basesLoading && !baseOptions.length" type="chip@4" />
            <template v-else-if="baseOptions.length">
              <v-chip-group v-model="pickedBases" multiple column selected-class="text-primary">
                <v-chip v-for="b in baseOptions" :key="b.base" :value="b.base" variant="outlined" filter>
                  Base {{ b.base }}
                  <span class="ml-1 text-medium-emphasis bi-form__code">· {{ b.customerCount }}</span>
                </v-chip>
              </v-chip-group>
              <p class="text-caption text-medium-emphasis mt-1 mb-0">
                <template v-if="pickedBases.length">
                  Every customer of {{ pickedBaseLabel }} — {{ pickedBaseCustomerCount }} today; customers added later are included too.
                </template>
                <template v-else>Pick one or more bases.</template>
              </p>
            </template>
            <v-alert v-else-if="basesError" type="error" variant="tonal" density="compact">
              {{ basesError }}
              <template #append><v-btn size="small" variant="text" @click="loadBases">Retry</v-btn></template>
            </v-alert>
            <p v-else class="text-body-2 text-medium-emphasis mb-0">None of your customers has a base.</p>
          </div>
        </v-expand-transition>

        <v-expand-transition>
          <div v-if="scope === 'pick'">
            <v-autocomplete
              v-model="picked"
              v-model:search="search"
              :items="options"
              :item-title="biCustomerLabel"
              item-value="id"
              :loading="searching"
              label="Search by name or code"
              placeholder="Type to search…"
              prepend-inner-icon="mdi-magnify"
              variant="outlined"
              density="comfortable"
              multiple
              chips
              closable-chips
              return-object
              no-filter
              clear-on-select
              hide-selected
              autofocus
              :menu-props="{ maxHeight: 320 }"
              hide-details="auto"
              :error-messages="tooMany ? `At most ${MAX_PICK} customers — choose All customers instead.` : undefined"
            >
              <template #chip="{ props: chipProps, item, index }">
                <v-chip v-if="index < CHIP_LIMIT" v-bind="chipProps" size="small" :text="biCustomerLabel(item.raw)" />
                <span v-else-if="index === CHIP_LIMIT" class="text-caption text-medium-emphasis ml-1">
                  +{{ picked.length - CHIP_LIMIT }} more
                </span>
              </template>
              <template #item="{ props: itemProps, item }">
                <v-list-item v-bind="itemProps" :title="item.raw.name" density="compact">
                  <template #prepend>
                    <v-chip v-if="item.raw.code" size="x-small" variant="tonal" class="mr-2 bi-form__code">{{ item.raw.code }}</v-chip>
                  </template>
                  <template #append>
                    <span v-if="item.raw.base" class="text-caption text-medium-emphasis">Base {{ item.raw.base }}</span>
                  </template>
                </v-list-item>
              </template>
              <template #no-data>
                <div class="px-4 py-3 text-body-2 text-medium-emphasis">
                  {{ searching ? 'Searching…' : search ? 'No customer matches.' : 'Start typing a customer name or code.' }}
                </div>
              </template>
            </v-autocomplete>
            <div class="d-flex align-center flex-wrap gap-2 mt-2">
              <span class="text-caption text-medium-emphasis">{{ picked.length }} selected</span>
              <v-spacer />
              <v-btn v-if="addableResults.length > 1" size="small" variant="text" prepend-icon="mdi-plus-box-multiple-outline" @click="addAllResults">
                Add all {{ addableResults.length }} results
              </v-btn>
              <v-btn v-if="picked.length" size="small" variant="text" prepend-icon="mdi-close" @click="picked = []">Remove all</v-btn>
            </div>
          </div>
        </v-expand-transition>
        <p v-if="scope === 'all'" class="text-body-2 text-medium-emphasis mb-0">
          Every customer you can see, each on its own row, with the totals on top.
        </p>
      </section>

      <!-- ── Period ── -->
      <section>
        <div class="bi-form__step"><span class="bi-form__num">2</span>Number of days</div>
        <v-chip-group v-model="preset" selected-class="text-primary" class="mb-2" column>
          <v-chip v-for="d in BI_DAY_PRESETS" :key="d" :value="d" variant="outlined" filter>{{ d }} days</v-chip>
        </v-chip-group>
        <v-text-field
          v-model.number="days"
          type="number"
          label="Days"
          :min="1"
          :max="BI_MAX_DAYS"
          variant="outlined"
          density="comfortable"
          prepend-inner-icon="mdi-calendar-range"
          suffix="days"
          :rules="[daysRule]"
          hide-details="auto"
          style="max-width: 240px"
          @keydown.enter="submit"
        />
        <p v-if="validDays" class="text-caption text-medium-emphasis mt-2 mb-0">
          <v-icon icon="mdi-calendar-arrow-right" size="14" class="mr-1" />{{ rangeLabel }}
        </p>
      </section>
    </div>

    <v-divider class="my-4" />
    <div class="d-flex align-center flex-wrap gap-2">
      <span class="text-caption text-medium-emphasis">{{ summaryText }}</span>
      <v-spacer />
      <v-btn v-if="cancellable" variant="text" @click="emit('cancel')">Cancel</v-btn>
      <v-btn color="primary" variant="flat" size="large" prepend-icon="mdi-chart-box-outline" :disabled="!canSubmit" @click="submit">
        Show BI
      </v-btn>
    </div>
  </v-card>
</template>

<script setup lang="ts">
import { BI_DAY_PRESETS, BI_MAX_DAYS, biCustomerLabel, type BiBaseOption, type BiCustomerOption } from '~/utils/bi'

const props = withDefaults(defineProps<{
  allCustomers: boolean
  bases: number[]
  customers: BiCustomerOption[]
  days: number | null
  /** Editing an existing BI — shows Cancel. */
  cancellable?: boolean
}>(), { cancellable: false })

const emit = defineEmits<{
  apply: [selection: { allCustomers: boolean; bases: number[]; customers: BiCustomerOption[]; days: number }]
  cancel: []
}>()

const MAX_PICK = 2000
const CHIP_LIMIT = 8

const api = useApi()

const scope = ref<'all' | 'bases' | 'pick'>(
  props.allCustomers ? 'all' : props.bases.length ? 'bases' : props.customers.length ? 'pick' : 'all')
const pickedBases = ref<number[]>([...props.bases])
const picked = ref<BiCustomerOption[]>(props.customers.map(c => ({ ...c })))
const days = ref<number | null>(props.days ?? 30)

// ── Days ──
const validDays = computed(() => Number.isInteger(days.value) && days.value! >= 1 && days.value! <= BI_MAX_DAYS)
const daysRule = () => validDays.value || `Whole number from 1 to ${BI_MAX_DAYS}`
const preset = computed({
  get: () => (BI_DAY_PRESETS as readonly number[]).includes(days.value ?? -1) ? days.value : undefined,
  set: (v: number | undefined) => { if (v != null) days.value = v },
})
const rangeLabel = computed(() => {
  const to = new Date()
  const from = new Date()
  from.setDate(to.getDate() - ((days.value ?? 1) - 1))
  const fmt = (d: Date) => d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
  return days.value === 1 ? `Today, ${fmt(to)}` : `${fmt(from)} → today (${fmt(to)})`
})

// ── Bases (loaded the first time By base is opened) ──
const baseOptions = shallowRef<BiBaseOption[]>([])
const basesLoading = ref(false)
const basesError = ref('')

async function loadBases() {
  basesLoading.value = true
  basesError.value = ''
  try {
    baseOptions.value = await api.get<BiBaseOption[]>('/bi/bases')
  } catch {
    basesError.value = 'Could not load the bases.'
  } finally {
    basesLoading.value = false
  }
}
watch(scope, (s) => { if (s === 'bases' && !baseOptions.value.length && !basesLoading.value) loadBases() }, { immediate: true })

const pickedBaseLabel = computed(() => {
  const names = [...pickedBases.value].sort((a, b) => a - b).map(b => `Base ${b}`)
  return names.length > 1 ? `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}` : names[0] ?? ''
})
const pickedBaseCustomerCount = computed(() => baseOptions.value
  .filter(b => pickedBases.value.includes(b.base))
  .reduce((sum, b) => sum + b.customerCount, 0))

// ── Customer search (lazy, debounced, stale responses dropped) ──
const search = ref('')
const results = shallowRef<BiCustomerOption[]>([])
const searching = ref(false)
let timer: ReturnType<typeof setTimeout> | undefined
let controller: AbortController | null = null

async function runSearch(term: string) {
  controller?.abort()
  const current = new AbortController()
  controller = current
  searching.value = true
  try {
    const res = await api.get<BiCustomerOption[]>(`/bi/customers?take=30&search=${encodeURIComponent(term)}`, { signal: current.signal })
    if (controller === current) results.value = res
  } catch {
    if (controller === current) results.value = []
  } finally {
    if (controller === current) searching.value = false
  }
}

watch(search, (term) => {
  clearTimeout(timer)
  timer = setTimeout(() => runSearch((term ?? '').trim()), 250)
})
watch(scope, (s) => { if (s === 'pick' && !results.value.length) runSearch('') }, { immediate: true })
onBeforeUnmount(() => { clearTimeout(timer); controller?.abort() })

/** Picked customers stay in the item list so their chips keep a title. */
const options = computed(() => {
  const ids = new Set(picked.value.map(c => c.id))
  return [...picked.value, ...results.value.filter(r => !ids.has(r.id))]
})
const addableResults = computed(() => {
  const ids = new Set(picked.value.map(c => c.id))
  return results.value.filter(r => !ids.has(r.id))
})
function addAllResults() {
  picked.value = [...picked.value, ...addableResults.value]
}

// ── Submit ──
const tooMany = computed(() => picked.value.length > MAX_PICK)
const canSubmit = computed(() => validDays.value && (
  scope.value === 'all'
  || (scope.value === 'bases' && pickedBases.value.length > 0)
  || (scope.value === 'pick' && picked.value.length > 0 && !tooMany.value)))
const summaryText = computed(() => {
  const who = scope.value === 'all' ? 'All customers'
    : scope.value === 'bases' ? (pickedBases.value.length ? `All customers of ${pickedBaseLabel.value}` : 'No base picked')
    : `${picked.value.length} customer${picked.value.length === 1 ? '' : 's'}`
  return validDays.value ? `${who} · last ${days.value} day${days.value === 1 ? '' : 's'}` : who
})

function submit() {
  if (!canSubmit.value) return
  emit('apply', {
    allCustomers: scope.value === 'all',
    bases: scope.value === 'bases' ? [...pickedBases.value] : [],
    customers: scope.value !== 'pick' ? [] : picked.value.map(c => ({ id: c.id, name: c.name, code: c.code ?? null, base: c.base ?? null })),
    days: days.value!,
  })
}
</script>

<style scoped>
.bi-form {
  padding: 24px;
}
.bi-form--first {
  max-width: 980px;
  margin: 0 auto;
}
.bi-form__intro {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-bottom: 24px;
}
.bi-form__grid {
  display: grid;
  grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
  gap: 32px;
}
@media (max-width: 860px) {
  .bi-form__grid {
    grid-template-columns: minmax(0, 1fr);
    gap: 24px;
  }
}
.bi-form__step {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  margin-bottom: 12px;
}
.bi-form__num {
  display: inline-grid;
  place-items: center;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  font-size: 12px;
  background: rgba(var(--v-theme-primary), 0.14);
  color: rgb(var(--v-theme-primary));
}
.bi-form__scope {
  flex-wrap: wrap;
  height: auto !important;
}
.bi-form__code {
  font-variant-numeric: tabular-nums;
}
</style>
