<template>
  <div v-if="lines.length" class="qse" :class="{ 'qse--roomy': roomy }">
    <div v-for="line in lines" :key="line.kind" class="qse-line" :class="`qse-line--${line.kind}`" :title="line.title">
      <v-icon :icon="line.icon" :size="roomy ? 13 : 11" class="mr-1" />
      <span v-if="line.label" class="qse-label">{{ line.label }}</span>{{ line.email }}
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * The emails under a supplier quote's supplier name: the Email Quotes address it was accepted
 * from, the address the user talks to for this item, and the supplier's main Catalog email.
 * An address already shown above is not repeated.
 */
const props = withDefaults(defineProps<{
  quote: { supplierEmail?: string | null; sourceEmail?: string | null; supplierMainEmail?: string | null }
  /** Leave out the item email — the edit modal already shows it as the picker chip. */
  hideSupplierEmail?: boolean
  roomy?: boolean
}>(), { hideSupplierEmail: false, roomy: false })

const same = (a?: string | null, b?: string | null) => !!a && !!b && a.trim().toLowerCase() === b.trim().toLowerCase()

const lines = computed(() => {
  const { supplierEmail, sourceEmail, supplierMainEmail } = props.quote
  const out: { kind: string; icon: string; label: string; email: string; title: string }[] = []
  if (sourceEmail) {
    out.push({
      kind: 'source',
      icon: 'mdi-email-arrow-left-outline',
      label: 'From ',
      email: sourceEmail,
      title: same(sourceEmail, supplierEmail)
        ? `Imported from an Email Quote sent by ${sourceEmail} — also the email used for this item`
        : `Imported from an Email Quote sent by ${sourceEmail}`,
    })
  }
  if (supplierEmail && !props.hideSupplierEmail && !same(supplierEmail, sourceEmail)) {
    out.push({ kind: 'item', icon: 'mdi-email-check-outline', label: '', email: supplierEmail, title: `Email used for this item: ${supplierEmail}` })
  }
  if (supplierMainEmail && !same(supplierMainEmail, sourceEmail) && !same(supplierMainEmail, supplierEmail)) {
    out.push({ kind: 'main', icon: 'mdi-email-outline', label: 'Main ', email: supplierMainEmail, title: `Supplier's main email (Catalog): ${supplierMainEmail}` })
  }
  return out
})
</script>

<style scoped>
.qse-line {
  font-size: 10px;
  padding: 0 6px 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 220px;
}
.qse--roomy .qse-line {
  font-size: 12px;
  padding: 2px 0 0;
  max-width: none;
}
.qse-line--source { color: #3b82f6; }
.qse-line--item { color: #22c55e; }
.qse-line--main { color: rgba(var(--v-theme-on-surface), 0.6); }
.qse-label { opacity: 0.75; }
</style>
