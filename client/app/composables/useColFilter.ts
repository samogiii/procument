/**
 * useColFilter — Excel-style column filter composable.
 *
 * Manages a Set<string> of selected values per column key.
 * Provides: toggle, selectAll, clearAll, isActive, getSelected.
 * The parent page feeds "available options" per column (from the last server
 * response), so the dropdown always reflects only what actually exists in the
 * current filtered result set.
 */
export function useColFilter() {
  // Map of columnKey → Set<string> of selected values
  const selected = reactive<Record<string, Set<string>>>({})
  // Per-column in-dropdown search text
  const search = reactive<Record<string, string>>({})

  function toggle(key: string, val: string) {
    if (!selected[key]) selected[key] = new Set()
    if (selected[key].has(val)) selected[key].delete(val)
    else selected[key].add(val)
  }

  function selectAll(key: string, options: string[]) {
    selected[key] = new Set(options)
  }

  function clearAll(key: string) {
    selected[key] = new Set()
  }

  function isActive(key: string) {
    return (selected[key]?.size ?? 0) > 0
  }

  function getSelected(key: string): string[] {
    return selected[key] ? [...selected[key]] : []
  }

  function hasAny() {
    return Object.values(selected).some(s => s.size > 0)
  }

  function clearAllFilters() {
    for (const key of Object.keys(selected)) {
      selected[key] = new Set()
    }
  }

  /** Build query params from all active column filters. */
  function toParams(params: URLSearchParams, mapping: Record<string, string>) {
    for (const [colKey, paramKey] of Object.entries(mapping)) {
      if (selected[colKey]?.size) {
        for (const v of selected[colKey]) params.append(paramKey, v)
      }
    }
  }

  /** Return filtered list of options for a column, applying in-dropdown search. */
  function filteredOptions(key: string, options: string[]): string[] {
    const s = (search[key] || '').toLowerCase()
    return s ? options.filter(v => v.toLowerCase().includes(s)) : options
  }

  return { selected, search, toggle, selectAll, clearAll, isActive, getSelected, hasAny, clearAllFilters, toParams, filteredOptions }
}
