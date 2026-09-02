<!--
  Displays and edits a document's Base 1 number.

  Base 1 customers get one generated automatically — B1QuoteNumber on the quote
  (Q101-60701-10), then copied onto the Sales Order as B1ProformaInvoiceNumber (P…) and
  onto the Final Invoice as B1InvoiceNumber (I…). Every other base gets none, so this is
  also how one is added by hand where auto-generation produced nothing. Saving a blank
  value clears it back to null.

  Each document keeps its own copy — editing one here does not rewrite the others.
-->
<template>
  <v-chip
    v-if="modelValue || editable"
    size="small"
    variant="tonal"
    :color="modelValue ? 'primary' : undefined"
    :prepend-icon="modelValue ? 'mdi-identifier' : 'mdi-plus'"
    :class="editable ? 'cursor-pointer' : ''"
    @click="editable && openDialog()"
  >
    {{ modelValue || `Add ${label}` }}
  </v-chip>

  <v-dialog v-model="dialog" max-width="440">
    <v-card class="glass-card">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="mdi-identifier" class="mr-2" />
        {{ label }}
      </v-card-title>
      <v-card-text class="pa-4">
        <v-text-field
          v-model="draft"
          :label="label"
          :placeholder="placeholder"
          variant="outlined"
          density="comfortable"
          autofocus
          :error-messages="error"
          @update:model-value="error = ''"
          @keyup.enter="save"
        />
        <div class="text-caption text-medium-emphasis">
          Leave empty to clear it. Only Base 1 customers are numbered automatically —
          anything set here is kept exactly as typed.
        </div>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-btn v-if="modelValue" variant="text" color="error" :disabled="saving" @click="clearNumber">Clear</v-btn>
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="dialog = false">Cancel</v-btn>
        <v-btn color="primary" variant="tonal" :loading="saving" @click="save">Save</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-snackbar v-model="snackbar" color="success" :timeout="2500" location="bottom end">
    {{ label }} saved
  </v-snackbar>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
  /** Current B1 number, or null when the document has none. */
  modelValue: string | null | undefined
  /** PATCH endpoint that accepts `{ b1Number }` — e.g. `/quotes/12/b1-number`. */
  endpoint: string
  /** Field name as the user knows it, e.g. "B1 Quote Number". */
  label: string
  editable?: boolean
  placeholder?: string
}>(), {
  editable: true,
  placeholder: 'Q101-60701-10',
})

const emit = defineEmits<{ (e: 'update:modelValue', v: string | null): void }>()

const api = useApi()

const dialog = ref(false)
const draft = ref('')
const saving = ref(false)
const error = ref('')
const snackbar = ref(false)

function openDialog() {
  draft.value = props.modelValue || ''
  error.value = ''
  dialog.value = true
}

function clearNumber() {
  draft.value = ''
  save()
}

async function save() {
  saving.value = true
  error.value = ''
  const value = draft.value.trim() || null
  try {
    await api.patch(props.endpoint, { b1Number: value })
    emit('update:modelValue', value)
    dialog.value = false
    snackbar.value = true
  } catch (e: any) {
    // 409 is the uniqueness guard on B1 quote numbers; anything else is unexpected.
    error.value = e?.data?.message || e?.response?._data?.message || `Failed to save the ${props.label}`
  } finally {
    saving.value = false
  }
}
</script>
