<template>
  <!-- A part's history (clock button on a part card): supplier quotes for it on RFQs + its tracing timeline. Loaded on open. -->
  <v-dialog :model-value="modelValue" max-width="1100" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-history" color="primary" class="mr-2" />
        History · {{ partNumber }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-0">
        <div v-if="loading" class="d-flex justify-center py-10"><v-progress-circular indeterminate color="primary" /></div>
        <v-alert v-else-if="error" type="error" variant="tonal" density="compact" class="ma-4">{{ error }}</v-alert>
        <template v-else-if="history">
          <ProcurementPlusQuoteHistory
            :part-number="history.partNumber"
            :older-quotes="history.quotes"
            :older-count="history.quoteCount"
            :lines="lines"
            all-rfqs
            start-open
            addable
            @add-supplier="(q: PpRfqQuote, m: string[]) => emit('add-supplier', q, m)"
          />
          <div v-if="history.events.length" class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-2">Timeline</div>
            <v-timeline density="compact" side="end" truncate-line="both">
              <v-timeline-item v-for="e in history.events" :key="e.id" dot-color="primary" size="x-small">
                <div class="text-body-2" style="white-space: pre-wrap">{{ e.text }}</div>
                <div class="text-caption text-medium-emphasis">{{ e.userName || '—' }} · {{ new Date(e.createdAt).toLocaleString() }}</div>
              </v-timeline-item>
            </v-timeline>
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import type { PpEvent, PpQuoteLine, PpRfqQuote } from '~/utils/procurementPlus'

interface PpPartHistory { partNumber: string; quotes: PpRfqQuote[]; quoteCount: number; events: PpEvent[] }

const props = defineProps<{
  modelValue: boolean
  /** The part's id; negative = a saved-RFQ item not on the list (-RFQ item id). */
  partId: number
  partNumber: string
  /** The part's supplier lines — suppliers already under it show "Added". */
  lines?: PpQuoteLine[]
  /** Full history URL (the new Procurement Plus page: /ppe/parts/{id}/history); default = Procurement ProMax's. */
  url?: string
}>()
const emit = defineEmits<{
  'update:modelValue': [open: boolean]
  /** Add a supplier of the history under the part — the name only. */
  'add-supplier': [quote: PpRfqQuote, methods: string[]]
}>()

const api = useProcurementPlusApi()
const plainApi = useApi()
const history = ref<PpPartHistory | null>(null)
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    history.value = props.url
      ? { events: [], ...await plainApi.get<PpPartHistory>(props.url) }
      : await api.get<PpPartHistory>(`/procurement-plus/parts/${props.partId}/history`)
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not load the history'
  } finally {
    loading.value = false
  }
}
watch(() => props.modelValue, (open) => { if (open) load() }, { immediate: true })
</script>
