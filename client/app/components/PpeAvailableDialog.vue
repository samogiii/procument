<template>
  <!--
    New Procurement Plus: suppliers listing one part in the user's current ILS files; a picked supplier becomes one warning
    row (no price, no condition / alt — its quote mail fills them when accepted in Email Quotes). Per supplier: copy the
    quotation mail for this part, copy its address. Conditions the part asks for are bold.
  -->
  <v-dialog :model-value="modelValue" max-width="1100" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center flex-wrap gap-2 pa-4">
        <v-icon icon="mdi-store-search-outline" color="deep-purple" class="mr-2" />
        Available suppliers · {{ part.partNumber }}
        <template v-if="asked.length">
          <span class="text-caption text-medium-emphasis ml-2">asks for</span>
          <v-chip v-for="c in asked" :key="c" size="small" color="success" variant="tonal" class="font-weight-black">{{ c }}</v-chip>
        </template>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-divider />
      <v-card-text>
        <div v-if="loading" class="d-flex justify-center py-10"><v-progress-circular indeterminate color="primary" /></div>
        <v-alert v-else-if="error" type="error" variant="tonal" density="compact">{{ error }}</v-alert>
        <p v-else-if="!rows.length" class="text-body-2 text-medium-emphasis text-center py-6 mb-0">
          None of your current ILS files list this part (or its alternatives). Upload them on the ILS tab.
        </p>
        <ProcurementPlusFileOffers v-else :rows="rows" :lines="lines" :busy="busy" :asked-conditions="{ [part.id]: asked }" @add="add">
          <template #supplier-actions="{ rows: supplierRows }">
            <v-btn
              size="x-small"
              variant="tonal"
              color="blue"
              prepend-icon="mdi-email-edit-outline"
              class="mr-1"
              title="Copy the quotation mail for this part (the P/Ns this supplier lists) — paste it into your mail"
              @click="copyMail(supplierRows)"
            >
              Copy mail
            </v-btn>
            <v-btn
              v-if="supplierMailAddresses(supplierRows).length"
              size="x-small"
              variant="text"
              icon="mdi-at"
              class="mr-1"
              :title="`Copy the address: ${supplierMailAddresses(supplierRows).join(', ')}`"
              @click="copyAddresses(supplierRows)"
            />
          </template>
        </ProcurementPlusFileOffers>
      </v-card-text>
    </v-card>
    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="4000">{{ snackText }}</v-snackbar>
  </v-dialog>
</template>

<script setup lang="ts">
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import type { PpQuoteLine } from '~/utils/procurementPlus'
import { asOfferLines, buildMailLines, copyHtml, mailHtml, mailText, supplierMailAddresses } from '~/utils/ppe'
import type { PpeLineHead, PpePartRow } from '~/utils/ppe'

const props = defineProps<{ modelValue: boolean; part: PpePartRow }>()
const emit = defineEmits<{ 'update:modelValue': [open: boolean]; added: [count: number] }>()

const api = usePpeApi()
const rows = ref<PpOfferRow[]>([])
const lines = ref<PpQuoteLine[]>([])
const loading = ref(false)
const error = ref('')
const busy = ref<number | null>(null)

/** Conditions the part asks for (its own and its RFQs'). */
const asked = computed(() => [...new Set(props.part.conditions.map(c => c.trim().toUpperCase()).filter(Boolean))])

async function load() {
  loading.value = true
  error.value = ''
  try {
    const res = await api.get<{ rows: PpOfferRow[]; lines: PpeLineHead[] }>(`/parts/${props.part.id}/offers`)
    rows.value = res.rows
    lines.value = asOfferLines(res.lines)
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not load the suppliers'
  } finally {
    loading.value = false
  }
}
watch(() => props.modelValue, (open) => { if (open) load() }, { immediate: true })

async function add(picked: PpOfferRow[], methods: string[]) {
  if (!picked.length) return
  busy.value = picked[0]!.offerId
  try {
    const res = await api.post<{ added: number; existing: number }>('/lines/from-offers', {
      offerIds: picked.map(r => r.offerId),
      partId: props.part.id,
      methods,
    })
    emit('added', res.added)
    await load()
  } catch (e: any) {
    error.value = e?.data?.message || 'Could not add them'
  } finally {
    busy.value = null
  }
}

// ── Per supplier: quotation mail, address ──
const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')
function toast(text: string, color = 'success') {
  snackText.value = text
  snackColor.value = color
  snackbar.value = true
}

function copyMail(supplierRows: PpOfferRow[]) {
  const mailLines = buildMailLines(supplierRows, [{
    id: props.part.id,
    partNumber: props.part.partNumber,
    description: props.part.description,
    qty: props.part.qty,
    condition: props.part.condition,
    conditions: asked.value,
  }])
  const ok = copyHtml(mailHtml(mailLines), mailText(mailLines))
  toast(ok ? `Mail copied (${mailLines.length} P/N${mailLines.length === 1 ? '' : 's'}) — paste it into your mail` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
}

function copyAddresses(supplierRows: PpOfferRow[]) {
  const emails = supplierMailAddresses(supplierRows)
  const ok = copyText(emails.join('; '))
  toast(ok ? `${emails.join('; ')} copied` : 'Could not copy — your browser blocked it', ok ? 'success' : 'warning')
}
</script>
