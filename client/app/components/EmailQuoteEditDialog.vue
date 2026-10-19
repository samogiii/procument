<template>
  <!-- Edit one email quote line — used by the Pending and Accepted tabs on /email-quotes.
       Fields save automatically and cert PDFs upload on pick (see EmailQuoteEditForm). -->
  <v-dialog :model-value="modelValue" max-width="680" scrollable @update:model-value="v => emit('update:modelValue', v)">
    <v-card>
      <v-card-title class="pa-4 d-flex align-center">
        Edit supplier quote
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="emit('update:modelValue', false)" />
      </v-card-title>
      <v-card-text class="pa-4 pt-0">
        <!-- v-if: closing unmounts the form, which flushes any pending save -->
        <EmailQuoteEditForm
          v-if="modelValue && row"
          :row="row"
          :accepted="accepted"
          :api-base="apiBase"
          :hide-supplier="hideSupplier"
          :suppliers-url="suppliersUrl"
          @updated="r => emit('updated', r)"
          @open-certificate="openCertificate"
        />
      </v-card-text>
      <v-card-actions class="pa-4 pt-0">
        <v-spacer />
        <v-btn color="primary" variant="flat" @click="emit('update:modelValue', false)">Done</v-btn>
      </v-card-actions>
    </v-card>

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  modelValue: boolean
  /** The pending/accepted line (PendingSupplierQuoteResponse shape). */
  row: any | null
  /** Accepted line: edits only reach new RFQs. */
  accepted?: boolean
  /** See EmailQuoteEditForm — Procurement Plus passes "/procurement-plus/quotes". */
  apiBase?: string
  hideSupplier?: boolean
  suppliersUrl?: string
}>()
const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void
  /** A save or cert change happened — the refreshed line, for the parent to patch its list. */
  (e: 'updated', row: any): void
}>()

const docPreview = useDocPreview()
function openCertificate(c: { id: number; fileName: string }) {
  if (!props.row) return
  docPreview.preview(`${props.apiBase || '/pending-supplier-quotes'}/${props.row.id}/certificates/${c.id}`, c.fileName, 'application/pdf')
}
</script>
