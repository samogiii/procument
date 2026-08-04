/**
 * useRangeFilter — numeric from/to column filter, the sibling of useColFilter().
 *
 * Value columns (deposit, withdraw, balance) can't be filtered by a checkbox list,
 * so they get a min/max pair per column key instead. Feeds ColRangeFilterMenu.
 */
export function useRangeFilter() {
  const range = reactive<Record<string, { min: number | null; max: number | null }>>({})

  /** Read-only accessor safe to call from templates/computeds (never mutates). */
  function get(key: string): { min: number | null; max: number | null } {
    return range[key] ?? { min: null, max: null }
  }

  function setMin(key: string, val: number | null) {
    range[key] = { min: val, max: get(key).max }
  }

  function setMax(key: string, val: number | null) {
    range[key] = { min: get(key).min, max: val }
  }

  function setBounds(key: string, lo: number | null, hi: number | null) {
    range[key] = { min: lo, max: hi }
  }

  function clear(key: string) {
    range[key] = { min: null, max: null }
  }

  function isActive(key: string) {
    const r = range[key]
    return !!r && (r.min != null || r.max != null)
  }

  function hasAny() {
    return Object.keys(range).some(isActive)
  }

  function clearAllRanges() {
    for (const key of Object.keys(range)) clear(key)
  }

  /**
   * True when the row passes this column's range.
   * Rows with no value (an empty Deposit cell on a withdrawal, say) drop out
   * once a range is set — asking for "deposits 100–500" shouldn't return blanks.
   */
  function matches(key: string, value: number | null | undefined) {
    const r = range[key]
    if (!r || (r.min == null && r.max == null)) return true
    if (value == null) return false
    if (r.min != null && value < r.min) return false
    if (r.max != null && value > r.max) return false
    return true
  }

  return { range, get, setMin, setMax, setBounds, clear, isActive, hasAny, clearAllRanges, matches }
}
