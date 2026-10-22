<template>
  <v-dialog :model-value="modelValue" max-width="520" @update:model-value="v => emit('update:modelValue', v)">
    <v-card>
      <v-card-title class="pa-4 d-flex align-center">
        <v-icon icon="mdi-domain-plus" color="primary" class="mr-2" />
        Add supplier · {{ partNumber }}
      </v-card-title>
      <v-card-text class="pt-0">
        <!-- One box: preferred suppliers (★) and catalog suppliers together -->
        <v-autocomplete
          v-if="!custom"
          v-model="picked"
          :items="options"
          item-title="name"
          item-value="key"
          label="Supplier"
          placeholder="Type a name, email or domain"
          density="compact"
          variant="outlined"
          :loading="loading"
          no-filter
          clearable
          hide-details
          autofocus
          prepend-inner-icon="mdi-domain"
          @update:search="onSearch"
        >
          <template #selection="{ item }">
            <v-icon v-if="item.raw.preferred" icon="mdi-star" color="amber" size="16" class="mr-1" />
            {{ item.raw.name }}
          </template>
          <template #item="{ props: itemProps, item }">
            <v-list-item v-bind="itemProps" :title="undefined" :subtitle="subtitleOf(item.raw)">
              <template #title>
                <v-icon v-if="item.raw.preferred" icon="mdi-star" color="amber" size="15" class="mr-1" />
                <span>{{ item.raw.name }}</span>
              </template>
              <template #append>
                <v-chip v-if="item.raw.domainMatch" size="x-small" color="success" variant="tonal" prepend-icon="mdi-at" class="mr-1">same domain</v-chip>
                <v-chip v-if="item.raw.preferred && !item.raw.supplierId" size="x-small" variant="outlined" class="mr-1">not in catalog</v-chip>
                <v-chip v-if="item.raw.status && item.raw.status !== 'Approved'" size="x-small" color="warning" variant="tonal">{{ item.raw.status }}</v-chip>
              </template>
            </v-list-item>
          </template>
        </v-autocomplete>

        <template v-else>
          <v-text-field v-model="customName" label="Supplier name" density="compact" variant="outlined" hide-details autofocus />
          <div class="text-caption text-medium-emphasis mt-2">
            A supplier that is nowhere yet. Pick its catalog supplier on the line before Done (prices on RFQs need one).
          </div>
        </template>

        <div class="text-caption mt-2">
          <a v-if="!custom" href="#" @click.prevent="custom = true">Not in the list? Type a name</a>
          <a v-else href="#" @click.prevent="custom = false">Back to Preferred / Catalog</a>
        </div>

        <!-- How the part is searched with this supplier — shown on its line, so you know where each price comes from -->
        <div class="text-caption font-weight-bold mt-4 mb-1">How are you searching this supplier? <span class="text-error">*</span></div>
        <v-chip-group v-model="methods" multiple column selected-class="font-weight-bold">
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

        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" :disabled="!ready" :loading="saving" @click="save">Add</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import type { PpSupplierOption } from '~/components/ProcurementPlusSupplierPicker.vue'
import { LINE_METHODS } from '~/utils/procurementPlus'
import type { PpQuoteLine } from '~/utils/procurementPlus'

const props = defineProps<{ modelValue: boolean; partId: number; partNumber: string }>()
const emit = defineEmits<{ 'update:modelValue': [v: boolean]; added: [line: PpQuoteLine] }>()

/** A choice in the box: a preferred supplier (key p{id}) or a catalog supplier (key s{id}). */
interface Option { key: string; name: string; email?: string | null; emails?: string[]; preferred: boolean; preferredId?: number; supplierId?: number; status?: string | null; domainMatch?: boolean }
interface PreferredSupplier { id: number; name: string; emails: string[]; supplierId?: number }

const api = useProcurementPlusApi()
const preferreds = ref<PreferredSupplier[]>([])
const catalog = ref<PpSupplierOption[]>([])
const query = ref('')
const loading = ref(false)
const picked = ref<string | null>(null)
const custom = ref(false)
const customName = ref('')
const saving = ref(false)
const error = ref('')
/** ILS / Email / Search — one or more. */
const methods = ref<string[]>(['ILS'])

const ready = computed(() => methods.value.length > 0 && (custom.value ? !!customName.value.trim() : !!picked.value))

/** Preferred suppliers matching the search first (★), then catalog suppliers not already listed through a preferred. */
const options = computed<Option[]>(() => {
  const q = query.value.trim().toLowerCase()
  const prefs = preferreds.value
    .filter(p => !q || p.name.toLowerCase().includes(q) || p.emails.some(e => e.toLowerCase().includes(q)))
    .map<Option>(p => ({ key: `p${p.id}`, name: p.name, email: p.emails[0], emails: p.emails, preferred: true, preferredId: p.id, supplierId: p.supplierId }))
  const linked = new Set(preferreds.value.map(p => p.supplierId).filter(Boolean))
  const cats = catalog.value
    .filter(s => !linked.has(s.id))
    .map<Option>(s => ({ key: `s${s.id}`, name: s.name, email: s.email, preferred: !!s.isPreferred, supplierId: s.id, status: s.status, domainMatch: s.domainMatch }))
  // Keep the picked one in the list so its name still shows after a new search.
  const current = picked.value ? allSeen.get(picked.value) : null
  const list = [...prefs, ...cats]
  return current && !list.some(o => o.key === current.key) ? [current, ...list] : list
})
/** Under the name: every email of a preferred supplier that is not in the catalog (to find it), else the main email. */
const subtitleOf = (o: Option) =>
  (o.preferred && !o.supplierId && o.emails?.length ? o.emails.join(', ') : o.email) || undefined
const allSeen = new Map<string, Option>()
watch(options, list => list.forEach(o => allSeen.set(o.key, o)))

let timer: ReturnType<typeof setTimeout> | null = null
function onSearch(q: string | null) {
  query.value = q || ''
  if (timer) clearTimeout(timer)
  timer = setTimeout(() => loadCatalog(query.value), 250)
}
async function loadCatalog(q: string) {
  loading.value = true
  try {
    catalog.value = await api.get<PpSupplierOption[]>(`/procurement-plus/suppliers?q=${encodeURIComponent(q)}`)
  } catch {
    catalog.value = []
  } finally {
    loading.value = false
  }
}

watch(() => props.modelValue, async (open) => {
  if (!open) return
  picked.value = null
  customName.value = ''
  custom.value = false
  methods.value = ['ILS']
  error.value = ''
  query.value = ''
  loadCatalog('')
  if (preferreds.value.length) return
  try {
    preferreds.value = await api.get('/procurement-plus/preferred-suppliers')
  } catch {
    preferreds.value = []
  }
})
onBeforeUnmount(() => { if (timer) clearTimeout(timer) })

async function save() {
  saving.value = true
  error.value = ''
  try {
    const o = picked.value ? allSeen.get(picked.value) : null
    const body = custom.value
      ? { supplierName: customName.value.trim() }
      : o?.preferredId ? { preferredId: o.preferredId } : { supplierId: o?.supplierId }
    const line = await api.post<PpQuoteLine>(`/procurement-plus/parts/${props.partId}/lines`, { ...body, methods: methods.value })
    emit('update:modelValue', false)
    emit('added', line)
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not add the supplier'
  } finally {
    saving.value = false
  }
}
</script>
