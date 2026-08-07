/**
 * useCascadingOptions — keeps two snapshots of a list page's filter-option lists.
 *
 * - `available` is re-fetched whenever the active filters change. The backend builds each
 *   column from the rows that survive the *other* filters, so once one column is filtered
 *   the remaining menus only offer values that would still return rows.
 * - `all` is fetched once with no filters and never changes. ColFilterMenu keeps it behind
 *   the "Show all" toggle so nothing is ever permanently out of reach.
 *
 * `fetcher(cascading)` gets `true` for the filtered call and `false` for the unfiltered one.
 */
export function useCascadingOptions<T extends object>(
  fetcher: (cascading: boolean) => Promise<T>,
  empty: T,
  options: { debounce?: number } = {},
) {
  const available = shallowRef<T>(empty)
  const all = shallowRef<T>(empty)
  const loading = ref(false)

  let timer: any = null
  // Guards against an earlier, slower response overwriting a newer one.
  let seq = 0

  async function loadAll() {
    try {
      all.value = await fetcher(false)
    } catch {}
  }

  async function refresh() {
    const mine = ++seq
    loading.value = true
    try {
      const res = await fetcher(true)
      if (mine === seq) available.value = res
    } catch {}
    finally {
      if (mine === seq) loading.value = false
    }
  }

  function refreshDebounced() {
    clearTimeout(timer)
    timer = setTimeout(refresh, options.debounce ?? 250)
  }

  /** Loads the unfiltered list once, then the cascaded one. */
  async function init() {
    await loadAll()
    await refresh()
  }

  onScopeDispose(() => clearTimeout(timer))

  return { available, all, loading, loadAll, refresh, refreshDebounced, init }
}
