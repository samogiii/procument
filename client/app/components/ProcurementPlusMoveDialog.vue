<template>
  <v-dialog :model-value="modelValue" max-width="560" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card>
      <v-card-title class="pa-4 d-flex align-center">
        <v-icon :icon="meta.icon" :color="meta.color" class="mr-2" />
        Move {{ rows.length }} part{{ rows.length === 1 ? '' : 's' }} to {{ meta.title }}
      </v-card-title>
      <v-card-subtitle class="px-4 pb-2 text-wrap">{{ meta.hint }}<template v-if="needsTarget"> — every part needs a target price.</template></v-card-subtitle>
      <v-card-text class="pt-2">
        <div v-for="r in rows" :key="r.id" class="d-flex align-center gap-3 mb-2">
          <div class="flex-grow-1 min-w-0">
            <div class="font-weight-bold">{{ r.partNumber }}</div>
            <div class="text-caption text-medium-emphasis text-truncate">{{ r.description || '—' }}</div>
          </div>
          <v-text-field
            v-if="needsTarget"
            v-model.number="r.targetPrice"
            type="number"
            label="Target price"
            prefix="$"
            density="compact"
            variant="outlined"
            hide-details
            style="max-width: 170px"
            :error="!(Number(r.targetPrice) > 0)"
          />
        </div>
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-2">{{ error }}</v-alert>
      </v-card-text>
      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn :color="meta.color" variant="flat" :disabled="!ready" :loading="saving" @click="save">Move</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue: boolean
  track: string
  parts: { id: number; partNumber: string; description?: string; targetPrice?: number | null }[]
}>()
const emit = defineEmits<{ 'update:modelValue': [v: boolean]; moved: [message: string] }>()

const api = useProcurementPlusApi()
const rows = ref<{ id: number; partNumber: string; description?: string; targetPrice: number | null }[]>([])
const saving = ref(false)
const error = ref('')
const meta = computed(() => trackMeta(props.track))
const needsTarget = computed(() => needsTargetPrice(props.track))
const ready = computed(() => rows.value.length > 0 && (!needsTarget.value || rows.value.every(r => Number(r.targetPrice) > 0)))

watch(() => props.modelValue, (open) => {
  if (!open) return
  error.value = ''
  rows.value = props.parts.map(p => ({ id: p.id, partNumber: p.partNumber, description: p.description, targetPrice: p.targetPrice ?? null }))
})

async function save() {
  saving.value = true
  error.value = ''
  try {
    await api.post('/procurement-plus/parts/track', {
      track: props.track,
      items: rows.value.map(r => ({ id: r.id, targetPrice: Number(r.targetPrice) > 0 ? Number(r.targetPrice) : null })),
    })
    emit('update:modelValue', false)
    emit('moved', `${rows.value.length} part${rows.value.length === 1 ? '' : 's'} moved to ${meta.value.title}`)
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not move'
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.min-w-0 { min-width: 0; }
</style>
