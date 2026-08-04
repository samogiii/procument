<template>
  <v-breadcrumbs
    v-if="items.length > 1"
    :items="items"
    density="compact"
    class="app-breadcrumbs px-0 pt-0 pb-2"
  >
    <template #divider>
      <v-icon icon="mdi-chevron-right" size="16" />
    </template>
    <template #title="{ item }">
      <span :class="item.disabled ? 'text-medium-emphasis' : 'text-primary'">{{ item.title }}</span>
    </template>
  </v-breadcrumbs>
</template>

<script setup lang="ts">
/**
 * Route-derived breadcrumb trail, rendered by the default layout on every page.
 * Only shows up on nested routes (`/purchase-orders/12`), never on a top-level list page.
 * A detail page can rename its own crumb via `useBreadcrumb().setBreadcrumbLabel(...)`.
 */
const route = useRoute()
const router = useRouter()
const { breadcrumbLabel } = useBreadcrumb()

/** Segments whose Title Case guess would be wrong or ugly. */
const SEGMENT_TITLES: Record<string, string> = {
  rfqs: 'RFQs',
  'rfq-items': 'RFQ Items',
  ils: 'ILS',
  pi: 'Proforma Invoices',
  po: 'PO',
  'total-pn': 'Total P/N',
  'ready-for-sn': 'Ready for SN',
  caplist: 'CAP List',
  procument: 'Procument',
  'menu-access': 'Menu Access',
  'company-presets': 'Company Presets',
  'payment-control': 'Payment Control',
  'sync-management': 'Sync Management',
  'satellite-sync': 'Satellite Sync',
}

function titleize(segment: string) {
  return SEGMENT_TITLES[segment]
    ?? segment.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')
}

/** Intermediate paths are not always real pages (e.g. /ils/quotes) — don't link those. */
function isRoutable(path: string) {
  try { return router.resolve(path).matched.length > 0 }
  catch { return false }
}

const items = computed(() => {
  const segments = route.path.split('/').filter(Boolean)
  if (segments.length < 2) return []

  const crumbs: { title: string; to?: string; disabled: boolean }[] = [
    { title: 'Home', to: '/', disabled: false },
  ]

  let path = ''
  segments.forEach((segment, i) => {
    path += `/${segment}`
    const isLast = i === segments.length - 1
    const isId = /^\d+$/.test(segment)

    let title = isId ? `#${segment}` : titleize(segment)
    // A page that knows its real name (PO number, invoice number…) overrides its own crumb.
    if (isLast && breadcrumbLabel.value?.path === route.path) {
      title = breadcrumbLabel.value.label
    }

    crumbs.push({
      title,
      to: !isLast && isRoutable(path) ? path : undefined,
      disabled: isLast || !isRoutable(path),
    })
  })

  return crumbs
})
</script>

<style scoped>
.app-breadcrumbs :deep(.v-breadcrumbs-item) {
  font-size: 0.8125rem;
  padding: 0;
}
.app-breadcrumbs :deep(.v-breadcrumbs-divider) {
  padding: 0 4px;
}
</style>
