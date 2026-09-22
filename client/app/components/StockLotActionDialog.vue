<template>
  <v-dialog :model-value="!!mode" max-width="520" persistent @update:model-value="(v: boolean) => { if (!v) close() }">
    <v-card v-if="lot">
      <v-card-title class="d-flex align-center gap-2 pa-4 pb-1">
        <v-icon :icon="TITLES[mode!].icon" :color="TITLES[mode!].color" />
        {{ TITLES[mode!].title }}
      </v-card-title>
      <v-card-subtitle class="px-4 text-wrap">
        {{ lot.partNumber }} · {{ lot.condition }}{{ lot.certName ? ` · ${lot.certName}` : '' }} — {{ lot.warehouseName }} ·
        on hand {{ fmt(lot.qtyOnHand) }}, reserved {{ fmt(lot.qtyReserved) }}
      </v-card-subtitle>

      <v-card-text class="pa-4">
        <!-- Adjust -->
        <template v-if="mode === 'adjust'">
          <v-btn-toggle v-model="direction" mandatory density="comfortable" color="primary" class="mb-3" divided>
            <v-btn value="add" prepend-icon="mdi-plus">Add</v-btn>
            <v-btn value="remove" prepend-icon="mdi-minus">Remove</v-btn>
          </v-btn-toggle>
          <v-text-field v-model.number="qty" type="number" min="0" step="1" label="Quantity" variant="outlined" density="compact" class="mb-3"
            :hint="direction === 'remove' ? `Up to ${fmt(unreserved)} (reserved stock cannot be removed)` : undefined" persistent-hint />
          <v-combobox v-model="reason" :items="ADJUST_REASONS" label="Reason *" variant="outlined" density="compact" />
        </template>

        <!-- Transfer -->
        <template v-else-if="mode === 'transfer'">
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            Moves the stock record now. For goods that travel between warehouses on a track number, use a warehouse transfer
            in Shipping — the stock follows automatically when the destination accepts it.
          </v-alert>
          <v-select v-model="targetWarehouseId" :items="targetWarehouses" :item-title="(w: any) => w.displayName || w.name" item-value="id"
            label="To warehouse *" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model.number="qty" type="number" min="0" step="1" label="Quantity" variant="outlined" density="compact" class="mb-3"
            :hint="`Up to ${fmt(unreserved)} available`" persistent-hint />
          <v-text-field v-model="reason" label="Note (optional)" variant="outlined" density="compact" />
        </template>

        <!-- Edit lot details -->
        <template v-else-if="mode === 'edit'">
          <v-text-field v-model="edit.binLocation" label="Bin location" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model.number="edit.minQty" type="number" min="0" label="Minimum quantity (re-order point)" variant="outlined" density="compact" class="mb-3" clearable />
          <v-text-field v-model="edit.tagDate" type="date" label="Tag date" variant="outlined" density="compact" class="mb-3" />
          <v-textarea v-model="edit.notes" label="Notes" variant="outlined" density="compact" rows="2" auto-grow />
        </template>

        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">{{ error }}</v-alert>
      </v-card-text>

      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn variant="text" @click="close">Cancel</v-btn>
        <v-btn :color="TITLES[mode!].color" variant="flat" :loading="saving" :disabled="!valid" @click="save">{{ TITLES[mode!].action }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
/** Admin actions on one stock lot: adjust quantity, transfer to another warehouse, edit lot details. */
type Mode = 'adjust' | 'transfer' | 'edit'
const props = defineProps<{ lot: any | null; mode: Mode | null }>()
const emit = defineEmits<{ (e: 'update:mode', v: Mode | null): void; (e: 'done', message: string): void }>()

const api = useApi()
const TITLES: Record<Mode, { title: string; icon: string; color: string; action: string }> = {
  adjust: { title: 'Adjust quantity', icon: 'mdi-plus-minus-variant', color: 'purple', action: 'Adjust' },
  transfer: { title: 'Move to another warehouse', icon: 'mdi-swap-horizontal', color: 'info', action: 'Move' },
  edit: { title: 'Edit lot details', icon: 'mdi-pencil', color: 'primary', action: 'Save' },
}
const ADJUST_REASONS = ['Stock count correction', 'Damaged / scrapped', 'Found in store', 'Returned from customer', 'Sample / test use']

const direction = ref<'add' | 'remove'>('add')
const qty = ref<number | null>(null)
const reason = ref('')
const targetWarehouseId = ref<number | null>(null)
const warehouses = ref<any[]>([])
const edit = reactive({ binLocation: '', minQty: null as number | null, tagDate: '', notes: '' })
const saving = ref(false)
const error = ref('')

const fmt = (q: number) => Number(q || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })
const unreserved = computed(() => Math.max(0, Number(props.lot?.qtyOnHand || 0) - Number(props.lot?.qtyReserved || 0)))
const targetWarehouses = computed(() => warehouses.value.filter(w => w.id !== props.lot?.warehouseId))
const qtyOk = computed(() => Number(qty.value) > 0 && Math.round(Number(qty.value) * 100) === Number(qty.value) * 100)

const valid = computed(() => {
  if (props.mode === 'adjust') return qtyOk.value && !!reason.value?.trim() && (direction.value === 'add' || Number(qty.value) <= unreserved.value)
  if (props.mode === 'transfer') return qtyOk.value && !!targetWarehouseId.value && Number(qty.value) <= unreserved.value
  return edit.minQty == null || String(edit.minQty) === '' || Number(edit.minQty) >= 0
})

watch(() => props.mode, async (m) => {
  error.value = ''
  qty.value = null
  reason.value = ''
  direction.value = 'add'
  targetWarehouseId.value = null
  if (m === 'edit' && props.lot) {
    Object.assign(edit, {
      binLocation: props.lot.binLocation || '', minQty: props.lot.minQty ?? null,
      tagDate: props.lot.tagDate ? String(props.lot.tagDate).slice(0, 10) : '', notes: props.lot.notes || '',
    })
  }
  if (m === 'transfer' && !warehouses.value.length)
    warehouses.value = (await api.get<any[]>('/warehouses').catch(() => [])).filter((w: any) => w.isActive !== false)
})

function close() { emit('update:mode', null) }

async function save() {
  saving.value = true
  error.value = ''
  const id = props.lot.id
  try {
    if (props.mode === 'adjust') {
      const signed = direction.value === 'add' ? Number(qty.value) : -Number(qty.value)
      await api.post(`/our-inventory/stock/${id}/adjust`, { qty: signed, reason: reason.value.trim() })
      emit('done', `Adjusted by ${signed > 0 ? '+' : ''}${fmt(signed)}`)
    } else if (props.mode === 'transfer') {
      await api.post(`/our-inventory/stock/${id}/transfer`, { targetWarehouseId: targetWarehouseId.value, qty: Number(qty.value), reason: reason.value || null })
      emit('done', `Moved ${fmt(Number(qty.value))} to ${targetWarehouses.value.find(w => w.id === targetWarehouseId.value)?.name ?? 'the other warehouse'}`)
    } else {
      await api.patch(`/our-inventory/stock/${id}`, {
        binLocation: edit.binLocation || null,
        minQty: edit.minQty == null || String(edit.minQty) === '' ? null : Number(edit.minQty),
        tagDate: edit.tagDate || null,
        notes: edit.notes || null,
      })
      emit('done', 'Lot details saved')
    }
    close()
  } catch (e: any) {
    error.value = e?.data?.message || 'The change was not saved.'
  } finally {
    saving.value = false
  }
}
</script>
