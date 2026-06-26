/**
 * Encapsulates server-side sort state for v-data-table-server.
 *
 * Usage:
 *   const sort = useServerSort()
 *   // In @update:options handler: sort.capture(opts)
 *   // When building API params:   sort.appendTo(params)
 *
 * Pass persisted refs (e.g. from usePageFilters) to remember the last sort
 * across refreshes:
 *   const sort = useServerSort({ sortKey: pf.sortKey, sortDesc: pf.sortDesc })
 *
 * `sortByModel` is a computed in v-data-table-server's `:sort-by` shape so the
 * restored sort is reflected in the table header on first render.
 */
export function useServerSort(opts?: { sortKey?: Ref<string>; sortDesc?: Ref<boolean> }) {
  const sortKey = opts?.sortKey ?? ref('')
  const sortDesc = opts?.sortDesc ?? ref(false)

  const sortByModel = computed(() =>
    sortKey.value ? [{ key: sortKey.value, order: sortDesc.value ? 'desc' : 'asc' }] : []
  )

  /** Call with the options object emitted by @update:options */
  function capture(opts: any) {
    const sb = opts?.sortBy?.[0]
    sortKey.value = sb?.key ?? ''
    sortDesc.value = sb?.order === 'desc'
  }

  /** Appends sortBy + sortDesc to a URLSearchParams if a sort is active */
  function appendTo(params: URLSearchParams) {
    if (sortKey.value) {
      params.set('sortBy', sortKey.value)
      params.set('sortDesc', String(sortDesc.value))
    }
  }

  return { sortKey, sortDesc, sortByModel, capture, appendTo }
}
