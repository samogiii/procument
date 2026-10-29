<template>
  <v-dialog :model-value="modelValue" max-width="1200" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-playlist-plus" class="mr-2" color="primary" />
        Add Items to Sales Order
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="close" />
      </v-card-title>
      <v-divider />

      <v-card-text>
        <p class="text-caption text-medium-emphasis mb-2">
          Only lines of this Sales Order's quotes can be added, at the quoted price and condition.
          A part that is already on the Sales Order with the same condition and price can't be added again.
        </p>

        <!-- Bring in another quote of this customer (Sent / Accepted only) so its lines show below -->
        <div class="d-flex flex-wrap align-center gap-2 mb-3">
          <v-autocomplete
            v-model="quoteToLink"
            :items="linkableQuotes"
            item-title="displayText"
            item-value="id"
            label="Add a quote of this customer"
            placeholder="Search by Quote # or B1 Quote #..."
            variant="outlined"
            density="compact"
            hide-details
            clearable
            :loading="loadingQuotes"
            no-data-text="No other sent quote for this customer"
            prepend-inner-icon="mdi-file-document-plus-outline"
            style="min-width: 320px; max-width: 520px;"
          >
            <template #item="{ props: itemProps, item }">
              <v-list-item
                v-bind="itemProps"
                :title="item.raw.quoteNumber"
                :subtitle="quoteSubtitle(item.raw)"
              >
                <template #append>
                  <v-chip size="x-small" variant="tonal" :color="item.raw.status === 'Accepted' ? 'success' : 'info'">{{ item.raw.status }}</v-chip>
                </template>
              </v-list-item>
            </template>
          </v-autocomplete>
          <v-btn
            color="primary"
            variant="tonal"
            prepend-icon="mdi-plus"
            :disabled="!quoteToLink"
            :loading="linkingQuote"
            @click="linkQuote"
          >Add Quote</v-btn>
        </div>

        <v-data-table
          v-model="selectedIds"
          :headers="headers"
          :items="items"
          :loading="loading"
          item-value="quoteItemId"
          :item-selectable="isSelectable"
          show-select
          density="compact"
          :items-per-page="-1"
          hide-default-footer
        >
          <template #item.quoteNumber="{ item }">
            <span class="text-caption font-weight-bold text-primary">{{ item.quoteNumber }}</span>
          </template>
          <template #item.partNumberName="{ item }">
            <div class="font-weight-medium" style="font-family: monospace;" :class="{ 'text-medium-emphasis': item.isDuplicate }">
              {{ item.alt || item.partNumberName || '—' }}
            </div>
            <div v-if="item.alt" class="text-caption text-medium-emphasis">orig: {{ item.partNumberName }}</div>
            <div v-if="item.description" class="text-caption text-medium-emphasis text-truncate" style="max-width: 240px;">{{ item.description }}</div>
          </template>
          <template #item.condition="{ item }">{{ item.condition || '—' }}</template>
          <template #item.certName="{ item }">
            <span class="text-caption">{{ item.certName || '—' }}</span>
          </template>
          <template #item.unitPrice="{ item }">
            <span style="font-family: monospace;">${{ formatPrice(item.unitPrice) }}</span>
          </template>
          <template #item.invQty="{ item }">
            <v-text-field
              v-model.number="qtyById[item.quoteItemId]"
              type="number"
              density="compact"
              hide-details
              variant="outlined"
              min="1"
              step="1"
              :disabled="!selectedIds.includes(item.quoteItemId)"
              style="min-width: 80px; max-width: 100px;"
            />
          </template>
          <template #item.delivery="{ item }">
            <v-text-field
              v-model="deliveryById[item.quoteItemId]"
              type="date"
              density="compact"
              hide-details
              variant="outlined"
              :min="today"
              :disabled="!selectedIds.includes(item.quoteItemId)"
              style="min-width: 140px; max-width: 160px;"
            />
          </template>
          <template #item.total="{ item }">
            <span style="font-family: monospace; color: rgb(var(--v-theme-success));">
              ${{ formatPrice(lineQty(item) * Number(item.unitPrice || 0)) }}
            </span>
          </template>
          <template #item.state="{ item }">
            <v-chip
              v-if="item.isDuplicate"
              size="x-small"
              variant="tonal"
              color="warning"
              title="This part is already on the Sales Order with the same condition and price"
            >Already on SO</v-chip>
            <span
              v-else-if="conflictsWithSelection(item)"
              class="text-caption text-medium-emphasis"
            >Same as a selected line</span>
          </template>
          <template #no-data>
            <div class="text-caption text-medium-emphasis py-4">This Sales Order's quotes have no lines.</div>
          </template>
        </v-data-table>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-3">
        <span class="text-body-2 text-medium-emphasis">
          {{ selectedIds.length }} item{{ selectedIds.length !== 1 ? 's' : '' }} selected
          <template v-if="selectedIds.length"> · Total: <strong>${{ formatPrice(selectedTotal) }}</strong></template>
        </span>
        <v-spacer />
        <v-btn variant="text" @click="close">Cancel</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          prepend-icon="mdi-plus"
          :disabled="selectedIds.length === 0"
          :loading="adding"
          @click="add"
        >
          Add
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
/**
 * Add lines to an existing Sales Order. Only lines of the order's quotes are listed — the ones it was created
 * from plus any Sent/Accepted quote of the same customer added here. A part already on the order with the same
 * condition and unit price can't be added (the server enforces it too).
 */
const props = defineProps<{
  modelValue: boolean
  invoiceId: number
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  added: [count: number]
  error: [message: string]
}>()

const api = useApi()
const today = new Date().toISOString().split('T')[0]

const headers = [
  { title: 'Quote #', key: 'quoteNumber', sortable: false, width: 110 },
  { title: 'Part Number', key: 'partNumberName', sortable: false },
  { title: 'CD', key: 'condition', sortable: false, width: 80 },
  { title: 'Cert', key: 'certName', sortable: false, width: 90 },
  { title: 'Quote Qty', key: 'qty', sortable: false, width: 90 },
  { title: 'Unit Price', key: 'unitPrice', sortable: false, width: 110 },
  { title: 'Inv Qty', key: 'invQty', sortable: false, width: 110 },
  { title: 'Delivery', key: 'delivery', sortable: false, width: 170 },
  { title: 'Total', key: 'total', sortable: false, width: 110 },
  { title: '', key: 'state', sortable: false, width: 150 },
]

const items = ref<any[]>([])
const loading = ref(false)
const adding = ref(false)
const selectedIds = ref<number[]>([])
const qtyById = ref<Record<number, number>>({})
const deliveryById = ref<Record<number, string>>({})

const linkableQuotes = ref<any[]>([])
const loadingQuotes = ref(false)
const quoteToLink = ref<number | null>(null)
const linkingQuote = ref(false)

function quoteSubtitle(q: any) {
  const parts = [q.b1QuoteNumber, `${q.itemCount} line${q.itemCount !== 1 ? 's' : ''}`]
  if (q.totalAmount != null) parts.push(`$${formatPrice(q.totalAmount)}`)
  return parts.filter(Boolean).join(' · ')
}

async function loadLinkableQuotes() {
  loadingQuotes.value = true
  quoteToLink.value = null
  try {
    const data = await api.get<any[]>(`/invoices/${props.invoiceId}/linkable-quotes`)
    // The B1 number sits in the title text so typing it filters the list.
    linkableQuotes.value = data.map(q => ({
      ...q,
      displayText: `${q.quoteNumber}${q.b1QuoteNumber ? ` · ${q.b1QuoteNumber}` : ''}`,
    }))
  } catch (e: any) {
    linkableQuotes.value = []
    emit('error', e?.data?.message || 'Failed to load quotes')
  } finally {
    loadingQuotes.value = false
  }
}

async function linkQuote() {
  if (!quoteToLink.value) return
  linkingQuote.value = true
  try {
    await api.post(`/invoices/${props.invoiceId}/source-quotes`, { quoteId: quoteToLink.value })
    // Keep what is already ticked; the new quote's lines join the list unticked.
    const keepSelected = [...selectedIds.value]
    const keepQty = { ...qtyById.value }
    const keepDelivery = { ...deliveryById.value }
    await Promise.all([load(), loadLinkableQuotes()])
    qtyById.value = { ...qtyById.value, ...keepQty }
    deliveryById.value = { ...deliveryById.value, ...keepDelivery }
    selectedIds.value = keepSelected.filter(id => items.value.some(item => item.quoteItemId === id))
  } catch (e: any) {
    emit('error', e?.data?.message || 'Failed to add quote')
  } finally {
    linkingQuote.value = false
  }
}

// Same part (Alt when set) + condition + unit price — mirrors the server's duplicate rule.
function lineKey(item: any) {
  const part = String((item.alt && item.alt.trim()) || item.partNumberName || '').trim().toUpperCase()
  const condition = String(item.condition || '').trim().toUpperCase()
  return `${part}|${condition}|${Number(item.unitPrice || 0).toFixed(2)}`
}

function conflictsWithSelection(item: any) {
  if (selectedIds.value.includes(item.quoteItemId)) return false
  const key = lineKey(item)
  return items.value.some(other => other.quoteItemId !== item.quoteItemId
    && selectedIds.value.includes(other.quoteItemId)
    && lineKey(other) === key)
}

function isSelectable(item: any) {
  return !item.isDuplicate && !conflictsWithSelection(item)
}

function lineQty(item: any) {
  return Math.max(1, Math.floor(Number(qtyById.value[item.quoteItemId]) || 1))
}

const selectedTotal = computed(() =>
  items.value
    .filter(item => selectedIds.value.includes(item.quoteItemId))
    .reduce((sum, item) => sum + lineQty(item) * Number(item.unitPrice || 0), 0))

function close() {
  emit('update:modelValue', false)
}

async function load() {
  loading.value = true
  items.value = []
  selectedIds.value = []
  try {
    items.value = await api.get<any[]>(`/invoices/${props.invoiceId}/addable-items`)
    qtyById.value = Object.fromEntries(items.value.map(item => [item.quoteItemId, item.qty]))
    deliveryById.value = Object.fromEntries(items.value.map(item => [item.quoteItemId, '']))
  } catch (e: any) {
    emit('error', e?.data?.message || 'Failed to load quote items')
  } finally {
    loading.value = false
  }
}

watch(() => props.modelValue, (open) => {
  if (!open) return
  load()
  loadLinkableQuotes()
}, { immediate: true })

async function add() {
  if (!selectedIds.value.length) return
  adding.value = true
  try {
    const selected = items.value.filter(item => selectedIds.value.includes(item.quoteItemId))
    await api.post(`/invoices/${props.invoiceId}/items`, {
      items: selected.map(item => ({
        quoteItemId: item.quoteItemId,
        qty: lineQty(item),
        expectedDeliveryDate: deliveryById.value[item.quoteItemId] || null,
      })),
    })
    emit('update:modelValue', false)
    emit('added', selected.length)
  } catch (e: any) {
    emit('error', e?.data?.message || 'Failed to add items')
    // Someone may have added the same part meanwhile — show the current state.
    await load()
  } finally {
    adding.value = false
  }
}
</script>
