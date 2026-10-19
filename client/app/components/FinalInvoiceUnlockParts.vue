<template>
  <template v-if="parts.length">
    <v-btn
      size="small"
      variant="tonal"
      color="warning"
      prepend-icon="mdi-lock-open-variant-outline"
      @click="open"
    >
      Unlock parts ({{ parts.length }})
    </v-btn>

    <v-dialog v-model="show" max-width="900" scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-lock-open-variant-outline" class="mr-2" color="warning" />
          Remove parts from Final Invoice
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="show = false" />
        </v-card-title>
        <v-divider />

        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">
            Selected parts are taken off their Final Invoice. The {{ poId ? 'PO' : 'Sales Order' }} unlocks, and the parts
            can be edited, returned, or put on a new Final Invoice later. A Final Invoice left with no parts is deleted.
          </p>

          <v-data-table
            v-model="selectedIds"
            :headers="headers"
            :items="parts"
            item-value="invoiceItemId"
            show-select
            density="compact"
            :items-per-page="-1"
            hide-default-footer
          >
            <template #item.finalInvoiceNumber="{ item }">
              <NuxtLink :to="`/final-invoices/${item.finalInvoiceId}`" class="text-primary">
                {{ item.b1FinalInvoiceNumber || item.finalInvoiceNumber }}
              </NuxtLink>
              <v-chip size="x-small" variant="tonal" class="ml-1" :color="item.finalInvoiceStatus === 'Paid' ? 'success' : undefined">
                {{ item.finalInvoiceStatus }}
              </v-chip>
            </template>
            <template #item.totalPrice="{ item }">${{ formatPrice(item.totalPrice) }}</template>
            <template #item.trackNumber="{ item }">
              <span class="text-caption">{{ item.trackNumber || '—' }}</span>
            </template>
          </v-data-table>

          <v-alert v-if="emptiedInvoices.length" type="warning" variant="tonal" density="compact" class="mt-3">
            {{ emptiedInvoices.join(', ') }} will have no parts left and will be deleted.
          </v-alert>
          <v-alert v-if="paidSelected" type="error" variant="tonal" density="compact" class="mt-3">
            Some selected parts are on a Final Invoice marked Paid.
          </v-alert>
        </v-card-text>

        <v-divider />
        <v-card-actions class="pa-3">
          <span class="text-body-2 text-medium-emphasis">{{ selectedIds.length }} part{{ selectedIds.length !== 1 ? 's' : '' }} selected</span>
          <v-spacer />
          <v-btn variant="text" @click="show = false">Cancel</v-btn>
          <v-btn color="warning" variant="flat" :disabled="!selectedIds.length" :loading="releasing" @click="release">
            Remove from Final Invoice
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </template>
</template>

<script setup lang="ts">
/**
 * "Unlock parts": lists the parts of a Sales Order (proformaId) or a PO (poId) that sit on a Final
 * Invoice and takes the selected ones off it, which unlocks the Sales Order / PO again.
 * Renders nothing while no part is invoiced. Bump `refreshKey` to reload after the parent changes things.
 */
const props = defineProps<{
  proformaId?: number | null
  poId?: number | null
  refreshKey?: number
}>()

const emit = defineEmits<{
  released: [result: any]
  error: [message: string]
}>()

const api = useApi()

const headers = [
  { title: 'Part Number', key: 'partNumber', sortable: false },
  { title: 'Qty', key: 'qty', sortable: false, width: 60 },
  { title: 'Total', key: 'totalPrice', sortable: false, align: 'end' as const },
  { title: 'Track Numbers', key: 'trackNumber', sortable: false },
  { title: 'Final Invoice', key: 'finalInvoiceNumber', sortable: false },
]

const parts = ref<any[]>([])
const show = ref(false)
const selectedIds = ref<number[]>([])
const releasing = ref(false)

const selectedParts = computed(() => parts.value.filter(p => selectedIds.value.includes(p.invoiceItemId)))

/** Final Invoices whose every part is selected — they get deleted. */
const emptiedInvoices = computed(() => {
  const picked = new Map<number, { count: number, total: number, number: string }>()
  for (const p of selectedParts.value) {
    const e = picked.get(p.finalInvoiceId) ?? { count: 0, total: p.finalInvoiceItemCount, number: p.b1FinalInvoiceNumber || p.finalInvoiceNumber }
    e.count++
    picked.set(p.finalInvoiceId, e)
  }
  return [...picked.values()].filter(e => e.count >= e.total).map(e => e.number)
})

const paidSelected = computed(() => selectedParts.value.some(p => p.finalInvoiceStatus === 'Paid'))

async function load() {
  if (!props.proformaId && !props.poId) { parts.value = []; return }
  const query = props.poId ? `poId=${props.poId}` : `proformaInvoiceId=${props.proformaId}`
  try {
    parts.value = await api.get<any[]>(`/final-invoices/invoiced-parts?${query}`)
  } catch {
    parts.value = []
  }
}

function open() {
  selectedIds.value = []
  show.value = true
}

async function release() {
  if (!selectedIds.value.length) return
  const names = selectedParts.value.map(p => p.partNumber).join(', ')
  if (!confirm(`Remove ${selectedIds.value.length} part(s) from their Final Invoice?\n\n${names}`)) return
  releasing.value = true
  try {
    const result = await api.post<any>('/final-invoices/release-parts', { invoiceItemIds: selectedIds.value })
    show.value = false
    await load()
    emit('released', result)
  } catch (e: any) {
    emit('error', e?.data?.message || 'Failed to remove parts from the Final Invoice')
  } finally {
    releasing.value = false
  }
}

watch(() => [props.proformaId, props.poId, props.refreshKey], load, { immediate: true })
</script>
