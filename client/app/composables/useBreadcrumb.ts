/**
 * Lets a detail page name itself in the breadcrumb trail.
 *
 * `AppBreadcrumbs` builds the trail from the route path alone, so a URL like
 * `/purchase-orders/12` renders "Purchase Orders › #12". A page that knows the
 * real name (PO number, invoice number, wallet name…) calls `setBreadcrumbLabel`
 * once its data arrives and the last crumb becomes "PO-12".
 *
 * The label is keyed by path so a stale value from the previous page never leaks
 * into the next one.
 */
export function useBreadcrumb() {
  const state = useState<{ path: string; label: string } | null>('breadcrumb-label', () => null)
  const route = useRoute()

  function setBreadcrumbLabel(label: string | null | undefined) {
    state.value = label ? { path: route.path, label } : null
  }

  return { breadcrumbLabel: state, setBreadcrumbLabel }
}
