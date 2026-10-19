<template>
  <v-dialog :model-value="modelValue" max-width="1100" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-receipt-text-check" class="mr-2" color="primary" />
        Create Final Invoice
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="close" />
      </v-card-title>
      <v-divider />

      <v-card-text>
        <!-- 1 · Sales Order (skipped when opened from a Sales Order) -->
        <template v-if="!proformaId">
          <div class="section-label mb-2">1 · Sales Order</div>
          <div v-if="loadingProformas" class="d-flex justify-center my-4">
            <v-progress-circular indeterminate color="primary" />
          </div>
          <!-- Autocomplete so a Sales Order can be found by its number or its B1 Proforma
               Invoice Number — both are in displayText, which the default filter matches on. -->
          <v-autocomplete
            v-else
            v-model="selectedProformaId"
            :items="eligibleProformas"
            item-title="displayText"
            item-value="id"
            label="Select Sales Order"
            variant="outlined"
            density="comfortable"
            hide-details
            placeholder="Search by Sales Order # or B1 Proforma Invoice Number..."
          >
            <template #item="{ props, item }">
              <v-list-item
                v-bind="props"
                :title="item.raw.invoiceNumber"
                :subtitle="`${item.raw.b1ProformaInvoiceNumber ? item.raw.b1ProformaInvoiceNumber + ' · ' : ''}${item.raw.customerName} ($${formatPrice(item.raw.totalAmount)})`"
              >
                <template #append>
                  <v-chip size="x-small" color="success" variant="tonal">
                    {{ item.raw.eligibleItemCount }} ready
                  </v-chip>
                  <v-chip v-if="item.raw.invoicedItemCount" size="x-small" variant="tonal" class="ml-1">
                    {{ item.raw.invoicedItemCount }}/{{ item.raw.totalItemCount }} invoiced
                  </v-chip>
                </template>
              </v-list-item>
            </template>
          </v-autocomplete>
          <div v-if="!loadingProformas && eligibleProformas.length === 0" class="text-caption text-error mt-2">
            No Sales Order has parts ready. A part needs a finished shipping track (Delivered to Customer) and must not be on a Final Invoice yet.
          </div>
        </template>

        <!-- 2 · Parts -->
        <template v-if="activeProformaId">
          <div class="d-flex flex-wrap align-center gap-2" :class="proformaId ? 'mb-2' : 'mt-5 mb-2'">
            <div class="section-label mb-0">{{ proformaId ? 'Select Parts' : '2 · Select Parts' }}</div>
            <span v-if="candidates" class="text-caption text-medium-emphasis">
              {{ candidates.invoiceNumber }}<template v-if="candidates.b1ProformaInvoiceNumber"> · {{ candidates.b1ProformaInvoiceNumber }}</template>
              · {{ candidates.customerName }}
            </span>
            <v-spacer />
            <v-btn size="x-small" variant="tonal" color="primary" :disabled="loadingCandidates || !eligibleIds.length" @click="selectedIds = [...eligibleIds]">Select all ready</v-btn>
            <v-btn size="x-small" variant="tonal" color="error" :disabled="loadingCandidates || !selectedIds.length" @click="selectedIds = []">Clear</v-btn>
          </div>
          <p class="text-caption text-medium-emphasis mb-2">
            Only parts with a finished shipping track (Delivered to Customer) can be invoiced.
            The other parts stay on the Sales Order for a later Final Invoice.
          </p>

          <v-data-table
            v-model="selectedIds"
            :headers="headers"
            :items="candidates?.items ?? []"
            :loading="loadingCandidates"
            item-value="invoiceItemId"
            :item-selectable="(item: any) => item.eligible"
            show-select
            density="compact"
            :items-per-page="-1"
            hide-default-footer
            class="parts-table"
          >
            <template #item.partNumber="{ item }">
              <div class="font-weight-medium" :class="{ 'text-medium-emphasis': !item.eligible }">{{ item.alt || item.partNumber }}</div>
              <div v-if="item.alt" class="text-caption text-medium-emphasis">PN: {{ item.partNumber }}</div>
              <div v-if="item.description" class="text-caption text-medium-emphasis text-truncate" style="max-width: 240px;">{{ item.description }}</div>
            </template>
            <template #item.unitPrice="{ item }">${{ formatPrice(item.unitPrice) }}</template>
            <template #item.totalPrice="{ item }">${{ formatPrice(item.totalPrice) }}</template>
            <template #item.tracks="{ item }">
              <div class="d-flex flex-wrap gap-1 py-1">
                <v-chip v-for="t in item.finishedTrackNumbers" :key="'f' + t" size="x-small" color="success" variant="tonal" prepend-icon="mdi-check-circle">{{ t }}</v-chip>
                <v-chip v-for="t in item.openTrackNumbers" :key="'o' + t" size="x-small" variant="outlined" prepend-icon="mdi-truck-outline">{{ t }}</v-chip>
                <span v-if="!item.finishedTrackNumbers.length && !item.openTrackNumbers.length" class="text-caption text-medium-emphasis">—</span>
              </div>
            </template>
            <template #item.state="{ item }">
              <v-chip v-if="item.eligible" size="x-small" color="success" variant="flat">Ready</v-chip>
              <v-chip
                v-else-if="item.finalInvoiceId"
                size="x-small"
                color="primary"
                variant="tonal"
                :to="`/final-invoices/${item.finalInvoiceId}`"
                append-icon="mdi-open-in-new"
              >{{ item.finalInvoiceNumber }}</v-chip>
              <span v-else class="text-caption text-medium-emphasis">{{ item.reason }}</span>
            </template>
            <template #no-data>
              <div class="text-caption text-medium-emphasis py-4">This Sales Order has no parts.</div>
            </template>
          </v-data-table>
        </template>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-3">
        <span class="text-body-2 text-medium-emphasis">
          {{ selectedIds.length }} part{{ selectedIds.length !== 1 ? 's' : '' }} selected
          <template v-if="selectedIds.length"> · Total: <strong>${{ formatPrice(selectedTotal) }}</strong></template>
        </span>
        <v-spacer />
        <v-btn variant="text" @click="close">Cancel</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :disabled="!activeProformaId || selectedIds.length === 0"
          :loading="creating"
          @click="create"
        >
          Create
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
/**
 * Create a Final Invoice for some or all parts of a Sales Order. Only parts whose live PO line has a
 * finished shipping track ("Delivered to Customer") and that aren't on a Final Invoice yet can be picked;
 * the rest stay open for a later Final Invoice. Pass `proformaId` to skip the Sales Order picker.
 */
const props = defineProps<{
  modelValue: boolean
  proformaId?: number | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  created: [result: any]
  error: [message: string]
}>()

const api = useApi()

const headers = [
  { title: 'Part Number', key: 'partNumber', sortable: false },
  { title: 'Qty', key: 'qty', sortable: false, width: 60 },
  { title: 'Unit', key: 'unitPrice', sortable: false, align: 'end' as const },
  { title: 'Total', key: 'totalPrice', sortable: false, align: 'end' as const },
  { title: 'Condition', key: 'condition', sortable: false },
  { title: 'Track Numbers', key: 'tracks', sortable: false },
  { title: '', key: 'state', sortable: false },
]

const eligibleProformas = ref<any[]>([])
const loadingProformas = ref(false)
const selectedProformaId = ref<number | null>(null)

const candidates = ref<any | null>(null)
const loadingCandidates = ref(false)
const selectedIds = ref<number[]>([])
const creating = ref(false)

const activeProformaId = computed(() => props.proformaId ?? selectedProformaId.value)

const eligibleIds = computed<number[]>(() =>
  (candidates.value?.items ?? []).filter((i: any) => i.eligible).map((i: any) => i.invoiceItemId))

const selectedTotal = computed(() =>
  (candidates.value?.items ?? [])
    .filter((i: any) => selectedIds.value.includes(i.invoiceItemId))
    .reduce((sum: number, i: any) => sum + Number(i.totalPrice || 0), 0))

function close() {
  emit('update:modelValue', false)
}

async function loadProformas() {
  loadingProformas.value = true
  try {
    const data = await api.get<any[]>('/final-invoices/eligible-proformas')
    eligibleProformas.value = data.map((p: any) => ({
      ...p,
      // The B1 number sits in the title text so typing it filters the list.
      displayText: `${p.invoiceNumber}${p.b1ProformaInvoiceNumber ? ` · ${p.b1ProformaInvoiceNumber}` : ''} - ${p.customerName} ($${formatPrice(p.totalAmount)})`
    }))
  } catch {
    emit('error', 'Failed to load eligible Sales Orders')
  } finally {
    loadingProformas.value = false
  }
}

async function loadCandidates(id: number) {
  loadingCandidates.value = true
  candidates.value = null
  selectedIds.value = []
  try {
    candidates.value = await api.get<any>(`/final-invoices/candidates/${id}`)
    // Every ready part starts selected; untick the ones that shouldn't go on this invoice.
    selectedIds.value = [...eligibleIds.value]
  } catch (e: any) {
    emit('error', e?.data?.message || 'Failed to load Sales Order parts')
  } finally {
    loadingCandidates.value = false
  }
}

watch(() => props.modelValue, (open) => {
  if (!open) return
  selectedProformaId.value = null
  candidates.value = null
  selectedIds.value = []
  if (props.proformaId) loadCandidates(props.proformaId)
  else loadProformas()
}, { immediate: true })

watch(selectedProformaId, (id) => {
  if (props.proformaId) return
  if (id) loadCandidates(id)
  else { candidates.value = null; selectedIds.value = [] }
})

async function create() {
  if (!activeProformaId.value || !selectedIds.value.length) return
  creating.value = true
  try {
    const result = await api.post<any>('/final-invoices', {
      proformaInvoiceId: activeProformaId.value,
      invoiceItemIds: selectedIds.value,
    })
    emit('update:modelValue', false)
    emit('created', result)
  } catch (e: any) {
    emit('error', e?.data?.message || 'Failed to create final invoice')
    // Parts may have been invoiced by someone else meanwhile — show the current state.
    await loadCandidates(activeProformaId.value)
  } finally {
    creating.value = false
  }
}
</script>

<style scoped>
.section-label {
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  opacity: 0.75;
}

.parts-table :deep(td) {
  vertical-align: top;
  padding-top: 6px !important;
  padding-bottom: 6px !important;
}
</style>
