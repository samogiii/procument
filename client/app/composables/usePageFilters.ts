/**
 * Composable for persisting page filters to localStorage.
 * Each page gets its own storage key based on `pageKey`.
 *
 * Uses Nuxt's `useState` so that every component sharing the same
 * pageKey (e.g. a page + its child DataListPage) gets the EXACT SAME
 * reactive ref — no status-drift, no stale cascading options.
 *
 * Usage:
 *   const { filters, clearFilters, hasActiveFilters } = usePageFilters('rfqs', {
 *     search: '',
 *     status: [] as string[],
 *     user: [] as number[],
 *     customer: [] as string[],
 *   })
 *   // filters.search, filters.status, etc. are reactive refs synced to localStorage
 */
export function usePageFilters<T extends Record<string, any>>(pageKey: string, defaults: T) {
  const STORAGE_PREFIX = 'pf_'
  const storageKey = STORAGE_PREFIX + pageKey

  // Load saved state from localStorage (client only)
  let saved: Partial<T> = {}
  if (import.meta.client) {
    try {
      const raw = localStorage.getItem(storageKey)
      if (raw) saved = JSON.parse(raw)
    } catch {}
  }

  // Build reactive refs using useState so all components sharing the same
  // pageKey share the IDENTICAL ref objects (singleton per key per app instance).
  const filters = {} as { [K in keyof T]: Ref<T[K]> }

  for (const key of Object.keys(defaults) as (keyof T)[]) {
    const initial = saved[key] !== undefined ? saved[key] : defaults[key]
    // useState key is globally unique per key — if already created (e.g. by parent
    // component), the factory is ignored and the existing ref is returned.
    filters[key] = useState<T[typeof key]>(
      `${storageKey}__${String(key)}`,
      () => initial as any
    ) as any
  }

  // Persist to localStorage whenever any filter in THIS call's defaults changes.
  // Reads existing storage first so keys owned by other calls aren't wiped.
  function persist() {
    if (!import.meta.client) return
    try {
      const raw = localStorage.getItem(storageKey)
      const data: Record<string, any> = raw ? JSON.parse(raw) : {}
      for (const key of Object.keys(defaults)) {
        data[key] = unref(filters[key as keyof T])
      }
      localStorage.setItem(storageKey, JSON.stringify(data))
    } catch {}
  }

  for (const key of Object.keys(defaults) as (keyof T)[]) {
    watch(filters[key], persist, { deep: true })
  }

  // Clear only the keys declared in THIS call's defaults back to their default values.
  function clearFilters() {
    for (const key of Object.keys(defaults) as (keyof T)[]) {
      (filters[key] as Ref<any>).value = Array.isArray(defaults[key])
        ? [...(defaults[key] as any[])]
        : defaults[key]
    }
  }

  // Computed: whether any filter in THIS call's defaults differs from its default
  const hasActiveFilters = computed(() => {
    for (const key of Object.keys(defaults) as (keyof T)[]) {
      const val = unref(filters[key])
      const def = defaults[key]
      if (Array.isArray(def)) {
        if ((val as any[]).length > 0) return true
      } else if (val !== def) {
        return true
      }
    }
    return false
  })

  return { filters, clearFilters, hasActiveFilters }
}
