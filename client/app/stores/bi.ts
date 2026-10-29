import { defineStore } from 'pinia'
import { markRaw } from 'vue'
import type { BiCustomerOption, BiSummary } from '~/utils/bi'

/**
 * BI page selection — which customers (all, whole bases, or picked ones) and how many days. Mirrored to
 * localStorage per user, so the user's last BI comes back on reload until they press Clear. Nothing is saved
 * server-side. The last summary is kept in memory only (markRaw, never proxied) so returning to the page is instant.
 */
export interface BiSelection {
    allCustomers: boolean
    /** Whole customer bases, e.g. [5] = every Base 5 customer (resolved by the API on each request). */
    bases: number[]
    customers: BiCustomerOption[]
    days: number | null
}

const EMPTY: BiSelection = { allCustomers: false, bases: [], customers: [], days: null }

/** Keeps exactly one scope: all customers, bases, or picked customers. */
function normalize(s: BiSelection): BiSelection {
    if (s.allCustomers) return { allCustomers: true, bases: [], customers: [], days: s.days }
    if (s.bases.length) return { allCustomers: false, bases: [...new Set(s.bases)].sort((a, b) => a - b), customers: [], days: s.days }
    return { allCustomers: false, bases: [], customers: s.customers.map(c => ({ ...c })), days: s.days }
}

function readStored(key: string): BiSelection {
    if (!import.meta.client) return { ...EMPTY }
    try {
        const parsed = JSON.parse(localStorage.getItem(key) ?? 'null')
        if (!parsed || typeof parsed !== 'object') return { ...EMPTY }
        const customers = Array.isArray(parsed.customers)
            ? parsed.customers
                .filter((c: any) => c && typeof c.id === 'number' && typeof c.name === 'string')
                .map((c: any) => ({ id: c.id, name: c.name, code: c.code ?? null, base: c.base ?? null }))
            : []
        const bases = Array.isArray(parsed.bases) ? parsed.bases.filter((b: unknown) => Number.isInteger(b) && (b as number) > 0) : []
        const days = Number.isInteger(parsed.days) && parsed.days > 0 ? parsed.days : null
        return normalize({ allCustomers: parsed.allCustomers === true, bases, customers, days })
    } catch {
        return { ...EMPTY }
    }
}

export const useBiStore = defineStore('bi', {
    state: () => ({
        loadedKey: '' as string,
        allCustomers: false,
        bases: [] as number[],
        customers: [] as BiCustomerOption[],
        days: null as number | null,
        /** Last summary for `summaryKey` — raw object, not reactive inside. */
        summary: null as BiSummary | null,
        summaryKey: '' as string,
    }),

    getters: {
        isConfigured: (s): boolean => s.days != null && (s.allCustomers || s.bases.length > 0 || s.customers.length > 0),
        /** Ids sent to the API; null = no customer filter (all customers, or whole bases). */
        customerIds: (s): number[] | null => (s.allCustomers || s.bases.length ? null : s.customers.map(c => c.id)),
        /** Bases sent to the API; null = no base filter. */
        baseFilter: (s): number[] | null => (!s.allCustomers && s.bases.length ? [...s.bases] : null),
        /** Identifies one selection — a cached summary is reused only for the same key. */
        selectionKey: (s): string => {
            const who = s.allCustomers ? 'all'
                : s.bases.length ? `bases:${s.bases.join(',')}`
                : s.customers.map(c => c.id).sort((a, b) => a - b).join(',')
            return `${s.days}|${who}`
        },
    },

    actions: {
        storageKey(): string {
            return `bi-selection-v1:${useAuthStore().user?.id ?? 'anonymous'}`
        },

        /** Reads the signed-in user's saved selection once (again after a user switch). */
        load() {
            const key = this.storageKey()
            if (this.loadedKey === key) return
            const stored = readStored(key)
            this.$patch({ ...stored, loadedKey: key, summary: null, summaryKey: '' })
        },

        apply(selection: BiSelection) {
            const next = normalize(selection)
            this.allCustomers = next.allCustomers
            this.bases = next.bases
            this.customers = next.customers
            this.days = next.days
            this.persist()
        },

        setSummary(key: string, summary: BiSummary) {
            this.summary = markRaw(summary)
            this.summaryKey = key
        },

        clear() {
            this.$patch({ ...EMPTY, bases: [], customers: [], summary: null, summaryKey: '' })
            if (!import.meta.client) return
            try { localStorage.removeItem(this.storageKey()) } catch {}
        },

        persist() {
            if (!import.meta.client) return
            const value: BiSelection = { allCustomers: this.allCustomers, bases: this.bases, customers: this.customers, days: this.days }
            try { localStorage.setItem(this.storageKey(), JSON.stringify(value)) } catch {}
        },
    },
})
