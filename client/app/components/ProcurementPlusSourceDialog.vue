<template>
  <v-dialog :model-value="modelValue" :fullscreen="mobile" max-width="1500" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card class="src">
      <div class="src__head">
        <div class="d-flex align-center flex-wrap gap-2">
          <v-avatar color="primary" variant="flat" size="40" class="mr-1"><v-icon icon="mdi-send-circle-outline" /></v-avatar>
          <div class="mr-4">
            <div class="text-h6 font-weight-bold">Source from preferred suppliers</div>
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
          No preferred supplier lists your parts in the current files.
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
                :model-value="inLane(m.value).every(s => s.on)"
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
              :key="s.preferredId"
              class="card"
              :class="{ 'card--on': s.on }"
            >
              <div class="d-flex align-start">
                <v-checkbox-btn :model-value="s.on" density="compact" class="flex-grow-0 mr-1" @update:model-value="setSupplier(s, !s.on)" />
                <div class="min-w-0 flex-grow-1" @click="setSupplier(s, !s.on)">
                  <div class="card__name">
                    <v-icon icon="mdi-star" color="amber" size="14" class="mr-1" />{{ s.name }}
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
                      :color="methodMeta(s.method).color"
                      :prepend-icon="methodMeta(s.method).icon"
                      append-icon="mdi-chevron-down"
                      class="ml-1"
                    >
                      {{ methodMeta(s.method).title }}
                    </v-btn>
                  </template>
                  <v-list density="compact">
                    <v-list-item
                      v-for="opt in SOURCE_METHODS"
                      :key="opt.value"
                      :prepend-icon="opt.icon"
                      :title="opt.title"
                      :active="opt.value === s.method"
                      @click="setMethod(s, opt.value)"
                    />
                  </v-list>
                </v-menu>
              </div>

              <ul class="parts">
                <li v-for="r in s.rows" :key="r.offerId" :class="{ 'parts__off': !s.picked.has(r.offerId) }">
                  <v-checkbox-btn
                    :model-value="s.picked.has(r.offerId)"
                    density="compact"
                    class="flex-grow-0 parts__check"
                    @update:model-value="togglePart(s, r.offerId)"
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
        </div>
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="primary" variant="flat" size="large" prepend-icon="mdi-rocket-launch-outline" :disabled="!chosenParts" :loading="starting" @click="start">
          Start sourcing
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import type { PpOfferRow } from './ProcurementPlusAvailabilityDialog.vue'

interface SupplierCard {
  preferredId: number; supplierId?: number; name: string; status?: string; email?: string; phone?: string
  method: string; rows: PpOfferRow[]; on: boolean; picked: Set<number>
}

const props = defineProps<{ modelValue: boolean; rows: PpOfferRow[] }>()
const emit = defineEmits<{ 'update:modelValue': [v: boolean]; started: [lineIds: number[], message: string] }>()

const api = useProcurementPlusApi()
const { mobile } = useDisplay()
const suppliers = ref<SupplierCard[]>([])

// Fresh cards each time the dialog opens: every supplier and part picked.
watch(() => props.modelValue, (open) => {
  if (!open) return
  const map = new Map<number, SupplierCard>()
  for (const r of props.rows) {
    if (!r.preferredId) continue
    let s = map.get(r.preferredId)
    if (!s) {
      s = { preferredId: r.preferredId, supplierId: r.supplierId, name: r.preferredName || r.supplierName || r.companyRaw, status: r.supplierStatus, email: r.email || r.supplierEmail, phone: r.phone, method: r.sourceMethod || 'Email', rows: [], on: true, picked: new Set() }
      map.set(r.preferredId, s)
    }
    if (!s.email && (r.email || r.supplierEmail)) s.email = r.email || r.supplierEmail
    s.rows.push(r)
    s.picked.add(r.offerId)
  }
  suppliers.value = [...map.values()].sort((a, b) => a.name.localeCompare(b.name))
}, { immediate: true })

const inLane = (method: string) => suppliers.value.filter(s => s.method === method)

/** Part numbers of the selected ILS suppliers' picked rows (each once), for the clipboard. */
const ilsPartNumbers = computed(() => [...new Set(
  inLane('ILS').filter(s => s.on).flatMap(s => s.rows.filter(r => s.picked.has(r.offerId)).map(r => r.partNumber)),
)])
const copied = ref('')
function copyIlsParts() {
  const ok = copyText(ilsPartNumbers.value.join('\n'))
  copied.value = ok ? `${ilsPartNumbers.value.length} part number${ilsPartNumbers.value.length === 1 ? '' : 's'} copied` : 'Your browser blocked copying'
}
const chosen = computed(() => suppliers.value.filter(s => s.on && s.picked.size))
const chosenParts = computed(() => chosen.value.reduce((n, s) => n + s.picked.size, 0))
const allOn = computed(() => suppliers.value.length > 0 && suppliers.value.every(s => s.on))
const someOn = computed(() => suppliers.value.some(s => s.on))

function setSupplier(s: SupplierCard, on: boolean) {
  s.on = on
  if (on && !s.picked.size) s.picked = new Set(s.rows.map(r => r.offerId))
}
function setAll(on: boolean) { suppliers.value.forEach(s => setSupplier(s, on)) }
function togglePart(s: SupplierCard, offerId: number) {
  const next = new Set(s.picked)
  next.has(offerId) ? next.delete(offerId) : next.add(offerId)
  s.picked = next
  s.on = next.size > 0
}

/** The method is the supplier's default from now on. */
async function setMethod(s: SupplierCard, method: string) {
  const before = s.method
  s.method = method
  try {
    await api.patch(`/procurement-plus/preferred-suppliers/${s.preferredId}`, { sourceMethod: method })
  } catch {
    s.method = before
  }
}

const starting = ref(false)
async function start() {
  starting.value = true
  try {
    const res = await api.post<{ created: number; existing: number; lineIds: number[] }>('/procurement-plus/sourcing', {
      suppliers: chosen.value.map(s => ({ preferredId: s.preferredId, sourceMethod: s.method, offerIds: [...s.picked] })),
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
.parts { list-style: none; padding: 0; margin: 6px 0 0; border-top: 1px dashed rgba(var(--v-border-color), 0.15); }
.parts li { display: flex; align-items: flex-start; gap: 4px; padding: 4px 0; font-size: 12.5px; border-bottom: 1px dashed rgba(var(--v-border-color), 0.08); }
.parts li:last-child { border-bottom: 0; }
.parts__off { opacity: 0.45; }
.parts__check { margin-top: -4px; }
.min-w-0 { min-width: 0; }
</style>
