<template>
  <v-dialog v-model="model" max-width="820" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center gap-2 pa-4">
        <v-icon icon="mdi-map-marker-path" color="primary" />
        <div>
          <div class="text-h6">Shipping Trace</div>
          <div v-if="trace" class="text-caption text-medium-emphasis">
            {{ trace.partNumberName || '—' }}
            <span v-if="trace.poNumber"> · PO {{ trace.poNumber }}</span>
            <span v-if="trace.customerCode"> · {{ trace.customerCode }}</span>
          </div>
        </div>
        <v-spacer />
        <v-chip
          v-if="trace"
          :color="trace.isComplete ? 'success' : 'amber'"
          size="small"
          variant="tonal"
        >
          <v-icon :icon="trace.isComplete ? 'mdi-check-circle' : 'mdi-progress-clock'" start size="14" />
          {{ trace.isComplete ? 'Completed' : 'In Progress' }}
        </v-chip>
        <v-btn icon="mdi-close" size="small" variant="text" @click="model = false" />
      </v-card-title>
      <v-divider />

      <v-card-text class="pa-4">
        <v-progress-linear v-if="loading" indeterminate color="primary" />

        <div v-else-if="error" class="text-center text-error pa-6">{{ error }}</div>

        <div v-else-if="!trace?.legs?.length" class="text-center text-medium-emphasis pa-8">
          No shipping history recorded for this item yet.
        </div>

        <v-timeline v-else side="end" density="compact" truncate-line="both">
          <v-timeline-item
            v-for="leg in trace.legs"
            :key="leg.sequence"
            :dot-color="legColor(leg)"
            :icon="legIcon(leg)"
            size="small"
            fill-dot
          >
            <template #opposite>
              <span class="text-caption text-medium-emphasis">
                {{ leg.startedAt ? new Date(leg.startedAt).toLocaleDateString() : '—' }}
              </span>
            </template>

            <v-card variant="outlined" class="pa-3">
              <div class="d-flex align-center flex-wrap gap-2 mb-1">
                <v-chip size="x-small" :color="legColor(leg)" variant="tonal">{{ legLabel(leg) }}</v-chip>
                <span class="text-body-2 font-weight-medium">
                  {{ leg.fromName || '—' }}
                  <v-icon icon="mdi-arrow-right" size="14" class="mx-1" />
                  {{ leg.toName || '—' }}
                </span>
                <v-spacer />
                <v-chip size="x-small" :color="statusColor(leg.status)" variant="flat">{{ leg.status }}</v-chip>
              </div>

              <div class="d-flex flex-wrap gap-3 text-caption text-medium-emphasis">
                <span v-if="leg.reference">
                  <v-icon icon="mdi-file-document-outline" size="12" class="mr-1" />{{ leg.reference }}
                </span>
                <span v-if="leg.trackNumber">
                  <v-icon icon="mdi-barcode-scan" size="12" class="mr-1" />{{ leg.trackNumber }}
                </span>
                <span v-if="leg.carrier">
                  <v-icon icon="mdi-truck-outline" size="12" class="mr-1" />{{ leg.carrier }}
                </span>
                <span v-if="leg.qty">
                  <v-icon icon="mdi-package-variant" size="12" class="mr-1" />{{ leg.qty }} unit(s)
                </span>
                <span v-if="leg.actorName">
                  <v-icon icon="mdi-account-outline" size="12" class="mr-1" />{{ leg.actorName }}
                </span>
                <span v-if="leg.completedAt">
                  <v-icon icon="mdi-check" size="12" class="mr-1" />
                  Completed {{ new Date(leg.completedAt).toLocaleDateString() }}
                </span>
              </div>

              <div v-if="leg.notes" class="text-caption text-medium-emphasis mt-2 font-italic">
                {{ leg.notes }}
              </div>
            </v-card>
          </v-timeline-item>
        </v-timeline>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue: boolean
  poItemId: number | null
}>()

const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const api = useApi()

const model = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
})

const trace = ref<any>(null)
const loading = ref(false)
const error = ref('')

const LEG_LABELS: Record<string, string> = {
  Inbound: 'Supplier Delivery',
  Transfer: 'Warehouse Transfer',
  Shipment: 'Shipment Note',
}

const LEG_ICONS: Record<string, string> = {
  Inbound: 'mdi-truck-delivery-outline',
  Transfer: 'mdi-transfer',
  Shipment: 'mdi-airplane',
}

function legLabel(leg: any) { return LEG_LABELS[leg.legType] ?? leg.legType }
function legIcon(leg: any) {
  if (leg.isTerminal) return 'mdi-flag-checkered'
  return LEG_ICONS[leg.legType] ?? 'mdi-circle-small'
}

function legColor(leg: any) {
  if (leg.isTerminal) return 'success'
  if (leg.legType === 'Transfer') return 'deep-purple'
  if (leg.legType === 'Shipment') return 'blue'
  return 'blue-grey'
}

function statusColor(status: string) {
  const map: Record<string, string> = {
    'Ship to Warehouse': 'blue-grey',
    'Received in Warehouse': 'orange',
    'Waiting for Packing': 'amber',
    'Ship To USA': 'blue',
    'Clearing Customs': 'deep-purple',
    'Received in Office': 'teal',
    'Delivered to Customer': 'success',
    'Rejected': 'error',
    'In Transit': 'blue-grey',
    'Partially Received': 'amber',
    'Received': 'success',
    'Cancelled': 'error',
    'Draft': 'default',
  }
  return map[status] ?? 'default'
}

async function load() {
  if (!props.poItemId) return
  loading.value = true
  error.value = ''
  trace.value = null
  try {
    trace.value = await api.get(`/shipping/trace/${props.poItemId}`)
  } catch (e: any) {
    error.value = e?.data?.message || 'Failed to load the shipping trace.'
  } finally {
    loading.value = false
  }
}

watch(() => [props.modelValue, props.poItemId], ([open]) => {
  if (open) load()
})
</script>
