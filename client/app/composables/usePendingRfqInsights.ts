/** Part insights of an Email RFQs line (POST /pending-rfqs/part-insights), keyed by the P/N as the line has it. */
export interface PendingRfqInsight {
    partNumber: string
    /** Pending: this P/N already has a PPE on Procurement Plus — accepting assigns the line to them. */
    knownPpeName?: string | null
    /** Average supplier quote price for the part on every RFQ, and per condition. */
    avgPrice?: number | null
    avgPriceCount?: number
    avgPriceByCondition?: { condition: string; average: number; count: number }[]
    /** Pending: last 5 customers who asked for the part — "C501 (2) C502 (4)". */
    recentCustomers?: { customerId: number; customerCode?: string | null; customerName: string; count: number }[]
    /** Accepted: customers asking for the part in the last 7 days — "C501 (4)" = qty 4. */
    weekCustomers?: { customerId: number; customerCode?: string | null; customerName: string; qty: number; count: number }[]
}

const BATCH_SIZE = 40
const PARALLEL = 3

/**
 * Email RFQs tabs: the list shows at once and these slower insights fill in after, a few batches of P/Ns at a
 * time. Loaded P/Ns stay cached until `load(..., true)`; responses of a superseded load are dropped.
 */
export function usePendingRfqInsights(what: { recentCustomers?: boolean; weekCustomers?: boolean; knownPpe?: boolean }) {
    const api = useApi()
    const insights = shallowRef(new Map<string, PendingRfqInsight>())
    /** P/Ns being fetched right now — their cells show a placeholder instead of "—". */
    const waiting = shallowRef(new Set<string>())
    let generation = 0

    async function load(partNumbers: string[], fresh = false) {
        const gen = ++generation
        if (fresh) insights.value = new Map()
        const wanted = [...new Set(partNumbers.filter(p => p?.trim()))].filter(p => !insights.value.has(p))
        waiting.value = new Set(wanted)
        const batches: string[][] = []
        for (let i = 0; i < wanted.length; i += BATCH_SIZE) batches.push(wanted.slice(i, i + BATCH_SIZE))

        async function worker() {
            for (let batch = batches.shift(); batch; batch = batches.shift()) {
                let found: PendingRfqInsight[] = []
                try {
                    found = await api.post<PendingRfqInsight[]>('/pending-rfqs/part-insights', { partNumbers: batch, ...what })
                } catch {
                    // Leave these cells empty; the list itself is already on screen.
                }
                if (gen !== generation) return
                const next = new Map(insights.value)
                for (const pn of batch) next.set(pn, found.find(f => f.partNumber === pn) ?? { partNumber: pn })
                insights.value = next
                const still = new Set(waiting.value)
                for (const pn of batch) still.delete(pn)
                waiting.value = still
            }
        }
        await Promise.all(Array.from({ length: Math.min(PARALLEL, batches.length) }, worker))
    }

    return { insights, waiting, load }
}
