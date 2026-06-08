/**
 * Drop-in replacement for useColFilter() that persists selections to localStorage.
 * Uses the same Set-based API so ColFilterMenu components work unchanged.
 */
export function useColFilterPersisted(pageKey: string) {
  const STORAGE_KEY = `col-filter-${pageKey}`

  const selected = reactive<Record<string, Set<string>>>({})
  const search = reactive<Record<string, string>>({})

  // Restore from localStorage immediately (runs at composable init, not just on mount)
  if (import.meta.client) {
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (raw) {
        const parsed: Record<string, string[]> = JSON.parse(raw)
        for (const [key, vals] of Object.entries(parsed)) {
          if (Array.isArray(vals) && vals.length) selected[key] = new Set(vals)
        }
      }
    } catch {}
  }

  function persist() {
    if (!import.meta.client) return
    const toStore: Record<string, string[]> = {}
    for (const [key, set] of Object.entries(selected)) {
      if (set && set.size > 0) toStore[key] = [...set]
    }
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(toStore)) } catch {}
  }

  function toggle(key: string, val: string) {
    if (!selected[key]) selected[key] = new Set()
    if (selected[key].has(val)) selected[key].delete(val)
    else selected[key].add(val)
    selected[key] = new Set(selected[key]) // trigger reactivity
    persist()
  }

  function selectAll(key: string, options: string[]) {
    selected[key] = new Set(options)
    persist()
  }

  function clearAll(key: string) {
    selected[key] = new Set()
    persist()
  }

  function isActive(key: string) { return (selected[key]?.size ?? 0) > 0 }
  function getSelected(key: string): string[] { return selected[key] ? [...selected[key]] : [] }
  function hasAny() { return Object.values(selected).some(s => s && s.size > 0) }

  function clearAllFilters() {
    for (const key of Object.keys(selected)) selected[key] = new Set()
    persist()
  }

  function toParams(params: URLSearchParams, mapping: Record<string, string>) {
    for (const [colKey, paramKey] of Object.entries(mapping)) {
      getSelected(colKey).forEach(v => params.append(paramKey, v))
    }
  }

  function filteredOptions(key: string, options: string[]): string[] {
    const s = (search[key] || '').toLowerCase()
    return s ? options.filter(o => String(o).toLowerCase().includes(s)) : options
  }

  return { selected, search, toggle, selectAll, clearAll, isActive, getSelected, hasAny, clearAllFilters, toParams, filteredOptions }
}
