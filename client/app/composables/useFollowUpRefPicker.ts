import type { Ref } from 'vue'

export interface FollowUpRefOption {
    id: number
    label: string
    detail: string | null
    status: string | null
    createdAt: string
}

const PAGE_SIZE = 30
const SEARCH_DEBOUNCE_MS = 300

/**
 * One lazily loaded picker list of the Customer Follow Ups RFQ / Other Refs dialogs (newest first).
 * Nothing loads until `reset()`; after that, `search` changes reload it (debounced) and `loadMore()`
 * appends the next page. Responses of superseded searches are dropped.
 * type: Rfq | PurchaseOrder | Quote | Invoice | FinalInvoice. The Task Manager passes its own endpoint.
 */
export function useFollowUpRefPicker(type: string, search: Ref<string | null>, endpoint = '/customer-follow-ups/ref-options') {
    const api = useApi()
    let generation = 0
    let timer: ReturnType<typeof setTimeout> | null = null
    /** The search the current list was loaded for — a "change" back to it reloads nothing. */
    let loadedSearch: string | null = null

    const state = reactive({
        items: [] as FollowUpRefOption[],
        hasMore: false,
        loading: false,
        /** reset() has been called at least once — the list is "open". */
        active: false,
        error: '',
        reset,
        loadMore,
    })

    async function fetchPage(append: boolean) {
        const current = append ? generation : ++generation
        if (!append) loadedSearch = search.value?.trim() ?? ''
        state.loading = true
        state.error = ''
        try {
            const page = await api.get<{ items: FollowUpRefOption[]; hasMore: boolean }>(endpoint, {
                query: {
                    type,
                    search: search.value?.trim() || undefined,
                    skip: append ? state.items.length : 0,
                    take: PAGE_SIZE,
                },
            })
            if (current !== generation) return
            state.items = append ? [...state.items, ...page.items] : page.items
            state.hasMore = page.hasMore
        } catch (error: any) {
            if (current !== generation) return
            state.hasMore = false
            state.error = error?.data?.message || 'Could not load the list.'
        } finally {
            if (current === generation) state.loading = false
        }
    }

    function reset() {
        state.active = true
        state.items = []
        state.hasMore = false
        fetchPage(false)
    }

    function loadMore() {
        if (state.loading || !state.hasMore) return
        fetchPage(true)
    }

    watch(search, () => {
        if (timer) clearTimeout(timer)
        if (!state.active || (search.value?.trim() ?? '') === loadedSearch) return
        timer = setTimeout(reset, SEARCH_DEBOUNCE_MS)
    })

    onBeforeUnmount(() => {
        if (timer) clearTimeout(timer)
    })

    return state
}
