/**
 * Internal / External shipping split — a view-only filter, nothing is stored.
 *
 * The shipping pages are served a second and third time under their own URL prefixes
 * (route aliases in each page's definePageMeta). The prefix decides which customer bases
 * the page shows:
 *   Internal Shipping — Base 1, plus lines with no customer at all (Stock POs).
 *   External Shipping — Base 2 and above. These shipments run without SO# and T-ID.
 * The original /shipping, /total-shipping, … URLs keep showing everything, so existing
 * links (notifications, dashboard) still work.
 */
export type ShippingScope = 'internal' | 'external'

export const SHIPPING_SCOPES = {
  internal: { prefix: '/internal-shipping', title: 'Internal Shipping', feature: 'internalShipping' },
  external: { prefix: '/external-shipping', title: 'External Shipping', feature: 'externalShipping' },
} as const


// Scoped sub-path → the original page it aliases. Most specific first.
const SCOPED_PAGES: ReadonlyArray<readonly [sub: string, original: string]> = [
  ['/control', '/total-shipping'],
  ['/ready-for-sn', '/shipping/ready-for-sn'],
  ['/shipment', '/shipment-notes'],
  ['/track-numbers', '/shipping/track-numbers'],
  ['', '/shipping'],
]

function under(path: string, prefix: string) {
  return path === prefix || path.startsWith(prefix + '/')
}

export function shippingScopeOf(path: string): ShippingScope | null {
  if (under(path, SHIPPING_SCOPES.internal.prefix)) return 'internal'
  if (under(path, SHIPPING_SCOPES.external.prefix)) return 'external'
  return null
}

/** '/external-shipping/control' → '/total-shipping'. Paths outside a scope come back as-is. */
export function unscopedShippingPath(path: string): string {
  const scope = shippingScopeOf(path)
  if (!scope) return path
  const rest = path.slice(SHIPPING_SCOPES[scope].prefix.length)
  for (const [sub, original] of SCOPED_PAGES) {
    if (rest === sub || (sub && rest.startsWith(sub + '/'))) return original + rest.slice(sub.length)
  }
  return path
}

/** '/total-shipping?tab=sn' → '/internal-shipping/control?tab=sn' when inside the internal scope. */
export function scopedShippingPath(scope: ShippingScope | null, path: string): string {
  if (!scope) return path
  const [pathname = '', query] = path.split('?')
  for (const [sub, original] of SCOPED_PAGES) {
    if (!under(pathname, original)) continue
    const scoped = SHIPPING_SCOPES[scope].prefix + sub + pathname.slice(original.length)
    return query ? `${scoped}?${query}` : scoped
  }
  return path
}

/** Whether a line with this Customer.Base belongs in the scope. No scope → everything. */
export function shippingBaseInScope(scope: ShippingScope | null, base: number | null | undefined): boolean {
  if (!scope) return true
  if (scope === 'external') return base != null && base >= 2
  return base == null || base === 1
}

export function useShippingScope() {
  const route = useRoute()
  const scope = computed(() => shippingScopeOf(route.path))

  return {
    scope,
    isExternal: computed(() => scope.value === 'external'),
    scopeTitle: computed(() => (scope.value ? SHIPPING_SCOPES[scope.value].title : null)),
    inScope: (base: number | null | undefined) => shippingBaseInScope(scope.value, base),
    /** A shipment note is in scope when any of its parts is. */
    noteInScope: (note: any) => {
      const tracks: any[] = note?.trackNumbers ?? []
      return tracks.length
        ? tracks.some(t => shippingBaseInScope(scope.value, t.customerBase))
        : shippingBaseInScope(scope.value, null)
    },
    /** Keeps in-page links inside the current scope. */
    link: (path: string) => scopedShippingPath(scope.value, path),
  }
}
