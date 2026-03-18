/**
 * Composable for persisting page filters to localStorage.
 * Each page gets its own storage key based on `pageKey`.
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

  // Build reactive refs for each filter key
  const filters = {} as { [K in keyof T]: Ref<T[K]> }

  // Try to load saved state from localStorage
  let saved: Partial<T> = {}
  if (import.meta.client) {
    try {
      const raw = localStorage.getItem(storageKey)
      if (raw) saved = JSON.parse(raw)
    } catch {}
  }

  // Create a ref for each filter, restoring from saved or using default
  for (const key of Object.keys(defaults) as (keyof T)[]) {
    const initial = saved[key] !== undefined ? saved[key] : defaults[key]
    filters[key] = ref(initial) as any
  }

  // Persist to localStorage whenever any filter changes
  function persist() {
    if (!import.meta.client) return
    const snapshot: Record<string, any> = {}
    for (const key of Object.keys(defaults)) {
      snapshot[key] = unref(filters[key as keyof T])
    }
    try {
      localStorage.setItem(storageKey, JSON.stringify(snapshot))
    } catch {}
  }

  // Watch all filter refs
  for (const key of Object.keys(defaults) as (keyof T)[]) {
    watch(filters[key], persist, { deep: true })
  }

  // Clear all filters back to defaults
  function clearFilters() {
    for (const key of Object.keys(defaults) as (keyof T)[]) {
      (filters[key] as Ref<any>).value = Array.isArray(defaults[key])
        ? [...defaults[key]]
        : defaults[key]
    }
    if (import.meta.client) {
      try { localStorage.removeItem(storageKey) } catch {}
    }
  }

  // Computed: whether any filter differs from its default
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
