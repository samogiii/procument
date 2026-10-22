<template>
  <v-dialog :model-value="modelValue" :fullscreen="mobile" max-width="1500" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card class="src">
      <div class="src__head">
        <div class="d-flex align-center flex-wrap gap-2">
          <v-avatar color="primary" variant="flat" size="40" class="mr-1"><v-icon icon="mdi-send-circle-outline" /></v-avatar>
          <div class="mr-4">
            <div class="text-h6 font-weight-bold">Source suppliers</div>
            <div class="text-caption text-medium-emphasis">
              Pick the suppliers to ask, check how each one is asked, then start — you'll type their prices next.
            </div>
          </div>
          <v-spacer />
          <v-checkbox-btn
            :model-value="allOn"
            :indeterminate="someOn && !allOn"
            label="Select all"
            density="compact"
            class="flex-grow-0"
            @update:model-value="setAll(!allOn)"
          />
          <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
        </div>
      </div>
      <v-divider />

      <v-card-text class="src__body">
        <div v-if="!suppliers.length" class="text-center text-medium-emphasis pa-10">
          No supplier picked — tick suppliers on Suppliers available first.
        </div>
        <div class="lanes">
          <section v-for="m in SOURCE_METHODS" :key="m.value" class="lane" :class="`lane--${m.color}`">
            <header class="lane__head">
              <v-avatar :color="m.color" variant="tonal" size="30"><v-icon :icon="m.icon" size="18" /></v-avatar>
              <div class="ml-2 min-w-0">
                <div class="font-weight-bold">{{ m.title }}</div>
                <div class="text-caption text-medium-emphasis text-truncate">{{ m.hint }}</div>
              </div>
              <v-spacer />
              <v-chip size="x-small" :color="m.color" variant="flat">{{ inLane(m.value).length }}</v-chip>
              <v-checkbox-btn
                v-if="inLane(m.value).length"
                :model-value="inLane(m.value).every(isOn)"
                density="compact"
                class="flex-grow-0 ml-1"
                title="Select this lane"
                @update:model-value="(v: boolean | null) => inLane(m.value).forEach(s => setSupplier(s, !!v))"
              />
            </header>

            <!-- ILS: copy the picked parts of the selected suppliers, to paste into ILS -->
            <v-btn
              v-if="m.value === 'ILS' && inLane('ILS').length"
              block
              size="small"
              :color="m.color"
              variant="tonal"
              prepend-icon="mdi-content-copy"
              class="mb-3"
              :disabled="!ilsPartNumbers.length"
              @click="copyIlsParts"
            >
              Copy {{ ilsPartNumbers.length }} part{{ ilsPartNumbers.length === 1 ? '' : 's' }}
            </v-btn>

            <div v-if="!inLane(m.value).length" class="lane__empty">No suppliers</div>

            <article
              v-for="s in inLane(m.value)"
              :key="s.key"
              class="card"
              :class="{ 'card--on': isOn(s) }"
            >
              <div class="d-flex align-start">
                <v-checkbox-btn :model-value="isOn(s)" density="compact" class="flex-grow-0 mr-1" @update:model-value="setSupplier(s, !isOn(s))" />
                <div class="min-w-0 flex-grow-1" @click="setSupplier(s, !isOn(s))">
                  <div class="card__name">
                    <v-icon v-if="s.preferredId" icon="mdi-star" color="amber" size="14" class="mr-1" title="Preferred supplier" />
                    <v-icon v-else-if="s.supplierId" icon="mdi-domain" color="primary" size="14" class="mr-1" title="Catalog supplier (not preferred)" />
                    <v-icon v-else icon="mdi-help-rhombus-outline" color="warning" size="14" class="mr-1" title="Not found in the catalog" />
                    {{ s.name }}
                    <v-chip v-if="s.status && s.status !== 'Approved'" size="x-small" color="warning" variant="tonal" class="ml-1">{{ s.status }}</v-chip>
                  </div>
                  <div class="text-caption text-medium-emphasis text-truncate">{{ s.email || s.phone || 'No contact in the file' }}</div>
                </div>
                <v-menu>
                  <template #activator="{ props: menuProps }">
                    <v-btn
                      v-bind="menuProps"
                      size="x-small"
                      variant="tonal"
                      :color="methodMeta(methodOf(s)).color"
                      :prepend-icon="methodMeta(methodOf(s)).icon"
                      append-icon="mdi-chevron-down"
                      class="ml-1"
                    >
                      {{ methodMeta(methodOf(s)).title }}
                    </v-btn>
                  </template>
                  <v-list density="compact">
                    <v-list-item
                      v-for="opt in SOURCE_METHODS"
                      :key="opt.value"
                      :prepend-icon="opt.icon"
                      :title="opt.title"
                      :active="opt.value === methodOf(s)"
                      @click="setMethod(s, opt.value)"
                    />
                  </v-list>
                </v-menu>
              </div>

              <!-- Not in the catalog: check it really is not there, then add it by hand -->
              <div v-if="!s.preferredId && !s.supplierId" class="card__unknown">
                <span class="text-caption">Not in your catalog — needed before Done.</span>
                <v-spacer />
                <v-btn size="x-small" color="warning" variant="flat" prepend-icon="mdi-account-search-outline" @click.stop="openCheck(s)">
                  Check &amp; add
                </v-btn>
              </div>

              <ul class="parts">
                <li v-for="r in s.rows" :key="`${r.offerId}-${r.partId}`" :class="{ 'parts__off': !picked.has(r.offerId) }">
                  <v-checkbox-btn
                    :model-value="picked.has(r.offerId)"
                    density="compact"
                    class="flex-grow-0 parts__check"
                    @update:model-value="togglePart(r.offerId)"
                  />
                  <div class="min-w-0 flex-grow-1">
                    <div class="d-flex align-center gap-1 flex-wrap">
                      <span class="font-weight-medium">{{ r.partNumber }}</span>
                      <span v-if="r.filePartNo.toUpperCase() !== r.partNumber.toUpperCase()" class="text-caption text-info">as {{ r.filePartNo }}</span>
                      <v-chip v-if="r.condition" size="x-small" variant="outlined">{{ r.condition }}</v-chip>
                      <span class="text-caption">× {{ r.qty ?? r.qtyRaw ?? '—' }}</span>
                      <v-spacer />
                      <span v-if="r.price != null" class="text-caption font-weight-bold">{{ fmtMoney(r.price) }}</span>
                      <v-chip v-if="r.sourcingState" size="x-small" :color="r.sourcingState === 'Done' ? 'success' : 'info'" variant="tonal">{{ r.sourcingState }}</v-chip>
                    </div>
                    <div v-if="r.supplierComment" class="text-caption text-medium-emphasis text-truncate" :title="r.supplierComment">{{ r.supplierComment }}</div>
                  </div>
                </li>
              </ul>
            </article>
          </section>
        </div>
      </v-card-text>

      <v-divider />
      <v-snackbar :model-value="!!copied" color="success" :timeout="2500" @update:model-value="v => { if (!v) copied = '' }">{{ copied }}</v-snackbar>
      <v-card-actions class="pa-4">
        <div class="text-body-2">
          <strong>{{ chosen.length }}</strong> supplier{{ chosen.length === 1 ? '' : 's' }} ·
          <strong>{{ chosenParts }}</strong> part listing{{ chosenParts === 1 ? '' : 's' }}
          <span v-if="chosenUnknown" class="text-warning ml-2">
            <v-icon icon="mdi-alert-outline" size="14" /> {{ chosenUnknown }} not in the catalog
          </span>
        </div>
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" size="large" prepend-icon="mdi-rocket-launch-outline" :disabled="!chosenParts" :loading="starting" @click="start">
          Start sourcing
        </v-btn>
      </v-card-actions>
    </v-card>

    <!-- Search the catalog for the company (by email domain / name); link it, or add it as a new supplier -->
    <ProcurementPlusResolveDialog v-model="check.open" :companies="check.companies" @resolved="emit('reload')" />
  </v-dialog>
</template>

<script setup lang="ts">
import type { PpOfferRow } from './ProcurementPlusAvailabilityDialog.vue'
import type { PpUnmatchedCompany } from './ProcurementPlusResolveDialog.vue'

/** One supplier: a preferred one, a catalog supplier, or a company only known by its name in the files. */
interface SupplierCard {
  key: string; preferredId?: number; supplierId?: number; companyKey: string; name: string; status?: string
  email?: string; phone?: string; rows: PpOfferRow[]
}

const props = defineProps<{ modelValue: boolean; rows: PpOfferRow[] }>()
const emit = defineEmits<{
  'update:modelValue': [v: boolean]
  started: [lineIds: number[], message: string]
  /** A company was linked to a catalog supplier — reload the rows. */
  reload: []
}>()

const api = useProcurementPlusApi()
const { mobile } = useDisplay()

/** Picked rows (offer ids) and each supplier's method — kept by id, so a reload keeps them. */
const picked = ref(new Set<number>())
const methods = reactive<Record<string, string>>({})

// Fresh each time the dialog opens: every supplier and part picked.
watch(() => props.modelValue, (open) => {
  if (!open) return
  picked.value = new Set(props.rows.map(r => r.offerId))
  for (const k of Object.keys(methods)) delete methods[k]
}, { immediate: true })

const suppliers = computed<SupplierCard[]>(() => {
  const map = new Map<string, SupplierCard>()
  for (const r of props.rows) {
    const key = r.preferredId ? `p${r.preferredId}` : r.supplierId ? `s${r.supplierId}` : `c${r.companyKey}`
    let s = map.get(key)
    if (!s) {
      s = {
        key, preferredId: r.preferredId, supplierId: r.supplierId, companyKey: r.companyKey,
        name: r.preferredName || r.supplierName || r.companyRaw, status: r.supplierStatus,
        email: r.email || r.supplierEmail, phone: r.phone, rows: [],
      }
      map.set(key, s)
    }
    if (!s.email && (r.email || r.supplierEmail)) s.email = r.email || r.supplierEmail
    s.rows.push(r)
  }
  return [...map.values()].sort((a, b) => Number(!a.preferredId) - Number(!b.preferredId) || a.name.localeCompare(b.name))
})

/** A preferred supplier keeps its own method; a non-preferred one starts on Email. */
const methodKey = (s: SupplierCard) => (s.preferredId ? `p${s.preferredId}` : `c${s.companyKey}`)
const methodOf = (s: SupplierCard) => methods[methodKey(s)] ?? (s.preferredId ? s.rows[0]?.sourceMethod : null) ?? 'Email'
const inLane = (method: string) => suppliers.value.filter(s => methodOf(s) === method)
const isOn = (s: SupplierCard) => s.rows.some(r => picked.value.has(r.offerId))
const pickedRows = (s: SupplierCard) => s.rows.filter(r => picked.value.has(r.offerId))

/** Part numbers of the selected ILS suppliers' picked rows (each once), for the clipboard. */
const ilsPartNumbers = computed(() => [...new Set(inLane('ILS').flatMap(s => pickedRows(s).map(r => r.partNumber)))])
const copied = ref('')
function copyIlsParts() {
  const ok = copyText(ilsPartNumbers.value.join('\n'))
  copied.value = ok ? `${ilsPartNumbers.value.length} part number${ilsPartNumbers.value.length === 1 ? '' : 's'} copied` : 'Your browser blocked copying'
}
const chosen = computed(() => suppliers.value.filter(isOn))
const chosenParts = computed(() => new Set(chosen.value.flatMap(s => pickedRows(s).map(r => r.offerId))).size)
const chosenUnknown = computed(() => chosen.value.filter(s => !s.preferredId && !s.supplierId).length)
const allOn = computed(() => suppliers.value.length > 0 && suppliers.value.every(isOn))
const someOn = computed(() => suppliers.value.some(isOn))

function setSupplier(s: SupplierCard, on: boolean) {
  const next = new Set(picked.value)
  for (const r of s.rows) on ? next.add(r.offerId) : next.delete(r.offerId)
  picked.value = next
}
function setAll(on: boolean) {
  picked.value = on ? new Set(props.rows.map(r => r.offerId)) : new Set()
}
function togglePart(offerId: number) {
  const next = new Set(picked.value)
  next.has(offerId) ? next.delete(offerId) : next.add(offerId)
  picked.value = next
}

/** A preferred supplier's method is its default from now on; a non-preferred one's is for this time only. */
async function setMethod(s: SupplierCard, method: string) {
  const key = methodKey(s)
  const before = methods[key]
  methods[key] = method
  if (!s.preferredId) return
  try {
    await api.patch(`/procurement-plus/preferred-suppliers/${s.preferredId}`, { sourceMethod: method })
  } catch {
    if (before === undefined) delete methods[key]
    else methods[key] = before
  }
}

// ── Not in the catalog: check, then link or add by hand ──
const check = reactive<{ open: boolean; companies: PpUnmatchedCompany[] }>({ open: false, companies: [] })
function openCheck(s: SupplierCard) {
  check.companies = [{
    companyKey: s.companyKey,
    companyRaw: s.rows[0]?.companyRaw ?? s.name,
    emails: [...new Set(s.rows.map(r => r.email).filter((e): e is string => !!e))],
    phones: [...new Set(s.rows.map(r => r.phone).filter((p): p is string => !!p))],
    comment: s.rows.find(r => r.supplierComment)?.supplierComment,
    partNumbers: [...new Set(s.rows.map(r => r.partNumber))],
    files: [...new Set(s.rows.map(r => r.fileName))],
    rowCount: s.rows.length,
  }]
  check.open = true
}

const starting = ref(false)
async function start() {
  starting.value = true
  try {
    const res = await api.post<{ created: number; existing: number; lineIds: number[] }>('/procurement-plus/sourcing', {
      suppliers: chosen.value.map(s => ({
        preferredId: s.preferredId ?? null,
        sourceMethod: methodOf(s),
        offerIds: [...new Set(pickedRows(s).map(r => r.offerId))],
      })),
    })
    const bits = [`${res.created} line${res.created === 1 ? '' : 's'} to price`]
    if (res.existing) bits.push(`${res.existing} already being sourced`)
    emit('started', res.lineIds, bits.join(' · '))
    emit('update:modelValue', false)
  } finally {
    starting.value = false
  }
}
</script>

<style scoped>
.src__head { padding: 16px 20px 12px; }
.src__body { height: 70vh; padding: 16px 20px !important; }
.lanes { display: grid; grid-template-columns: repeat(3, minmax(260px, 1fr)); gap: 14px; align-items: start; }
@media (max-width: 1200px) { .lanes { grid-template-columns: repeat(2, minmax(260px, 1fr)); } }
@media (max-width: 700px) { .lanes { grid-template-columns: 1fr; } }
.lane {
  border-radius: 12px;
  background: rgba(var(--v-theme-on-surface), 0.025);
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  padding: 10px;
  min-height: 120px;
}
.lane__head { display: flex; align-items: center; margin-bottom: 10px; }
.lane--blue .lane__head { color: rgb(var(--v-theme-on-surface)); }
.lane__empty { font-size: 12px; text-align: center; padding: 18px 0; color: rgba(var(--v-theme-on-surface), 0.4); }
.card {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 10px;
  padding: 8px 10px;
  margin-bottom: 10px;
  transition: border-color 0.15s, box-shadow 0.15s, opacity 0.15s;
  opacity: 0.6;
}
.card--on { opacity: 1; border-color: rgba(var(--v-theme-primary), 0.6); box-shadow: 0 1px 6px rgba(var(--v-theme-primary), 0.12); }
.card__name { font-weight: 700; font-size: 14px; display: flex; align-items: center; cursor: pointer; }
.card__unknown {
  display: flex; align-items: center; gap: 6px; margin-top: 6px; padding: 4px 8px; border-radius: 8px;
  background: rgba(var(--v-theme-warning), 0.1);
}
.parts { list-style: none; padding: 0; margin: 6px 0 0; border-top: 1px dashed rgba(var(--v-border-color), 0.15); }
.parts li { display: flex; align-items: flex-start; gap: 4px; padding: 4px 0; font-size: 12.5px; border-bottom: 1px dashed rgba(var(--v-border-color), 0.08); }
.parts li:last-child { border-bottom: 0; }
.parts__off { opacity: 0.45; }
.parts__check { margin-top: -4px; }
.min-w-0 { min-width: 0; }
</style>
