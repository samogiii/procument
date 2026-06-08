/**
 * Encapsulates server-side sort state for v-data-table-server.
 *
 * Usage:
 *   const sort = useServerSort()
 *   // In @update:options handler: sort.capture(opts)
 *   // When building API params:   sort.appendTo(params)
 */
export function useServerSort() {
  const sortKey = ref('')
  const sortDesc = ref(false)

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

  return { sortKey, sortDesc, capture, appendTo }
}
