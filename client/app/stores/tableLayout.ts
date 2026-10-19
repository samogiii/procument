import { defineStore } from 'pinia'

/**
 * Per-user column order and widths for sheet-style tables (e.g. Customer Follow Ups).
 * Each table is keyed by a short id and mirrored to localStorage per user, so a layout
 * survives reloads and does not leak between users who share a browser.
 */
export interface TableLayout {
    /** Column keys in the user's order. Keys the user never moved keep their default place at the end. */
    order: string[]
    /** Column widths in px. Columns without an entry use their default width. */
    widths: Record<string, number>
}

export const MIN_COLUMN_WIDTH = 40

function readStored(key: string): TableLayout {
    const empty: TableLayout = { order: [], widths: {} }
    if (!import.meta.client) return empty
    try {
        const parsed = JSON.parse(localStorage.getItem(key) ?? 'null')
        if (!parsed || typeof parsed !== 'object') return empty
        const order = Array.isArray(parsed.order) ? parsed.order.filter((k: unknown) => typeof k === 'string') : []
        const widths: Record<string, number> = {}
        if (parsed.widths && typeof parsed.widths === 'object') {
            for (const [k, w] of Object.entries(parsed.widths)) {
                if (typeof w === 'number' && Number.isFinite(w) && w >= MIN_COLUMN_WIDTH) widths[k] = Math.round(w)
            }
        }
        return { order, widths }
    } catch {
        return empty
    }
}

export const useTableLayoutStore = defineStore('tableLayout', {
    state: () => ({
        /** Keyed by `table-layout-v1:{tableKey}:{userId}` — the same key used in localStorage. */
        layouts: {} as Record<string, TableLayout>,
    }),

    actions: {
        storageKey(tableKey: string): string {
            return `table-layout-v1:${tableKey}:${useAuthStore().user?.id ?? 'anonymous'}`
        },

        /** The signed-in user's layout for a table, read from localStorage the first time. */
        layout(tableKey: string): TableLayout {
            const key = this.storageKey(tableKey)
            if (!this.layouts[key]) this.layouts[key] = readStored(key)
            return this.layouts[key]
        },

        save(tableKey: string, next: TableLayout) {
            const key = this.storageKey(tableKey)
            this.layouts[key] = next
            if (!import.meta.client) return
            try { localStorage.setItem(key, JSON.stringify(next)) } catch {}
        },

        setOrder(tableKey: string, order: string[]) {
            this.save(tableKey, { ...this.layout(tableKey), order: [...order] })
        },

        setWidth(tableKey: string, columnKey: string, width: number) {
            const current = this.layout(tableKey)
            this.save(tableKey, {
                ...current,
                widths: { ...current.widths, [columnKey]: Math.max(MIN_COLUMN_WIDTH, Math.round(width)) },
            })
        },

        clearWidth(tableKey: string, columnKey: string) {
            const current = this.layout(tableKey)
            const { [columnKey]: _removed, ...widths } = current.widths
            this.save(tableKey, { ...current, widths })
        },

        reset(tableKey: string) {
            this.save(tableKey, { order: [], widths: {} })
        },
    },
})
