<template>
  <!-- Before Done: warns when a (custom) part number is not in the database. -->
  <v-dialog v-model="open" max-width="480" persistent>
    <v-card>
      <v-card-title class="pa-4 d-flex align-center">
        <v-icon icon="mdi-alert-outline" color="warning" class="mr-2" />
        Not in your database
      </v-card-title>
      <v-card-text class="pt-0">
        <p class="mb-3">
          {{ missing.length === 1 ? 'This part is' : 'These parts are' }} not in your database, please check
          {{ missing.length === 1 ? 'it' : 'them' }} carefully:
        </p>
        <div class="d-flex flex-wrap gap-2">
          <v-chip v-for="pn in missing" :key="pn" color="warning" variant="tonal" size="small" class="font-weight-bold">{{ pn }}</v-chip>
        </div>
      </v-card-text>
      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn variant="text" @click="answer(false)">Cancel</v-btn>
        <v-btn color="warning" variant="flat" @click="answer(true)">Done anyway</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const api = useProcurementPlusApi()
const open = ref(false)
const missing = ref<string[]>([])
let resolve: ((ok: boolean) => void) | null = null

function answer(ok: boolean) {
  open.value = false
  resolve?.(ok)
  resolve = null
}

/** Looks the lines' part numbers up; true = go on with Done (all found, or the user confirmed). */
async function confirm(lineIds: number[]): Promise<boolean> {
  const res = await api.post<{ missing: string[] }>('/procurement-plus/quotes/done-check', { ids: lineIds })
  if (!res.missing?.length) return true
  missing.value = res.missing
  open.value = true
  return new Promise(r => { resolve = r })
}

defineExpose({ confirm })
</script>
