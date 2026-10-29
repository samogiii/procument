<template>
  <!--
    New Procurement Plus — ILS tab: a quotation request mail for one supplier. The supplier's P/Ns (as their file lists
    them) are a table; pick the ones to ask for (Qty / Condition editable), copy the mail (keeps the table when pasted
    into Outlook / Gmail), then open it in the mail app with the supplier's address. Nothing is sent from here.
  -->
  <v-dialog :model-value="modelValue" max-width="980" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-email-edit-outline" color="blue" class="mr-2" />
        Quotation mail · {{ supplierName }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-divider />
      <v-card-text>
        <v-combobox
          v-model="to"
          :items="emails"
          label="To"
          multiple
          chips
          closable-chips
          density="compact"
          variant="outlined"
          hide-details
          class="mb-3"
          prepend-inner-icon="mdi-email-outline"
        />

        <table class="mail-grid mb-4">
          <thead>
            <tr>
              <th style="width: 32px;">
                <v-checkbox-btn
                  :model-value="selectedCount === lines.length && lines.length > 0"
                  :indeterminate="selectedCount > 0 && selectedCount < lines.length"
                  density="compact"
                  @update:model-value="toggleAll"
                />
              </th>
              <th>Part number</th>
              <th>Description</th>
              <th style="width: 90px;">Qty</th>
              <th style="width: 110px;">Condition</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="l in lines" :key="l.key" :class="{ 'row--off': !l.selected }">
              <td><v-checkbox-btn v-model="l.selected" density="compact" /></td>
              <td class="font-weight-medium">
                {{ l.partNumber }}
                <span v-if="l.isAlt" class="text-caption text-info ml-1" :title="`Alternative of ${l.ourPart}`">alt of {{ l.ourPart }}</span>
              </td>
              <td><input v-model="l.description" class="mail-input"></td>
              <td><input v-model.number="l.qty" type="number" min="1" class="mail-input mail-input--num"></td>
              <td><input v-model="l.condition" class="mail-input"></td>
            </tr>
          </tbody>
        </table>

        <div class="text-caption text-medium-emphasis mb-1">Preview</div>
        <!-- Values are escaped in buildHtml — file text never becomes markup -->
        <div class="mail-preview" v-html="html" />
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <span class="text-caption text-medium-emphasis">{{ selectedCount }} part{{ selectedCount === 1 ? '' : 's' }}</span>
        <v-spacer />
        <v-btn variant="tonal" prepend-icon="mdi-content-copy" :disabled="!selectedCount" @click="copyMail(false)">Copy mail</v-btn>
        <v-btn
          color="blue"
          variant="flat"
          prepend-icon="mdi-email-fast-outline"
          :disabled="!selectedCount || !to.length"
          :title="to.length ? 'Copies the mail, then opens your mail app to this supplier — paste it in' : 'Add the supplier email first'"
          @click="copyMail(true)"
        >
          Copy & open mail
        </v-btn>
      </v-card-actions>
    </v-card>
    <v-snackbar v-model="snackbar" :color="snackColor" :timeout="5000">{{ snackText }}</v-snackbar>
  </v-dialog>
</template>

<script setup lang="ts">
import type { PpOfferRow } from '~/components/ProcurementPlusAvailabilityDialog.vue'
import { buildMailLines, copyHtml, mailHtml, mailText } from '~/utils/ppe'
import type { PpeMailLine, PpePartBrief } from '~/utils/ppe'

const props = defineProps<{ modelValue: boolean; supplierName: string; emails: string[]; rows: PpOfferRow[]; parts: PpePartBrief[] }>()
const emit = defineEmits<{ 'update:modelValue': [open: boolean] }>()

const to = ref<string[]>([])
const lines = ref<PpeMailLine[]>([])

function build() {
  lines.value = buildMailLines(props.rows, props.parts)
  to.value = [...props.emails]
}
watch(() => props.modelValue, (open) => { if (open) build() }, { immediate: true })

const selectedCount = computed(() => lines.value.filter(l => l.selected).length)
function toggleAll(on: boolean) {
  for (const l of lines.value) l.selected = on
}

const html = computed(() => mailHtml(lines.value))
const text = computed(() => mailText(lines.value))

const snackbar = ref(false)
const snackText = ref('')
const snackColor = ref('success')

function copyMail(open: boolean) {
  const ok = copyHtml(html.value, text.value)
  snackText.value = ok ? (open ? 'Mail copied — paste it into the new message' : 'Mail copied') : 'Could not copy — your browser blocked it'
  snackColor.value = ok ? 'success' : 'warning'
  snackbar.value = true
  if (!open || !to.value.length) return
  const pns = lines.value.filter(l => l.selected).map(l => l.partNumber)
  const subject = `Quotation request - ${pns.slice(0, 5).join(', ')}${pns.length > 5 ? ' …' : ''}`
  window.location.href = `mailto:${to.value.map(encodeURIComponent).join(',')}?subject=${encodeURIComponent(subject)}`
}
</script>

<style scoped>
.mail-grid {
  width: 100%;
  border-collapse: collapse;
  font-size: 12.5px;
}
.mail-grid th {
  text-align: left;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  padding: 6px 8px;
  color: rgba(var(--v-theme-on-surface), 0.6);
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}
.mail-grid td {
  padding: 3px 8px;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.05);
}
.row--off td {
  opacity: 0.45;
}
.mail-input {
  width: 100%;
  height: 28px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.15);
  border-radius: 4px;
  padding: 0 6px;
  font-size: 12px;
  background: rgba(var(--v-theme-surface), 0.6);
  color: inherit;
  outline: none;
}
.mail-input:focus {
  border-color: rgb(var(--v-theme-primary));
}
.mail-input--num {
  text-align: center;
}
.mail-preview {
  background: #fff;
  color: #222;
  border-radius: 6px;
  padding: 12px 14px;
  font-family: Arial, sans-serif;
  font-size: 13px;
}
</style>
