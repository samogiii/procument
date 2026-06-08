import { defineStore } from 'pinia'

interface User {
    id: number
    name: string
    email: string
    role: string
    token: string
    bases: number[]
}

/** Shape returned by GET /menu-permissions */
interface MenuPermissionGroup {
    feature: string
    userNames: string[]
}

/**
 * All known gated features — keys must match the `feature` strings
 * sent by the backend MenuPermissionsController.
 */
const DEFAULT_FEATURE_PERMISSIONS: Record<string, string[]> = {
    customerMenu:    [],
    isAmir:          [],
    newRFQ:          [],
    ilsUsers:        [],
    isPDFSelection:  [],
    paymentMenu:     [],
    companyPresets:  [],
    syncApp:         [],
    systemActivity:  [],
    supplierRequests:[],
    capList:         [],
    ils:             [],
    shippingMenu:    [],
    actionCenter:    [],
    taskManager:     [],
}

function getTokenExpiry(token: string): number | null {
    try {
        const parts = token.split('.')
        if (parts.length < 2) return null
        const payload = JSON.parse(atob(parts[1]!))
        return payload.exp ? payload.exp * 1000 : null
    } catch {
        return null
    }
}

export const useAuthStore = defineStore('auth', {
    state: () => ({
        user: null as User | null,
        /** Live menu-permission map fetched from the API, keyed by feature name. */
        featurePermissions: { ...DEFAULT_FEATURE_PERMISSIONS } as Record<string, string[]>,
    }),

    getters: {
        tokenExpiry: (state): number | null => {
            if (!state.user?.token) return null
            return getTokenExpiry(state.user.token)
        },
        isTokenExpired(): boolean {
            if (!this.tokenExpiry) return true
            return Date.now() >= this.tokenExpiry
        },
        isAuthenticated(): boolean {
            return !!this.user?.token && !this.isTokenExpired
        },

        // ─── Role Checks ───
        isAdmin: (state) => state.user?.role === 'Admin' || state.user?.role === 'SuperAdmin',
        isSuperAdmin: (state) => state.user?.role === 'SuperAdmin',
        userBases: (state): number[] => state.user?.bases ?? [],
        isPayment: (state) => state.user?.role === 'Payment' || state.user?.role === 'AHM' || state.user?.role === 'SuperAdmin',
        isExpert: (state) => state.user?.role === 'Expert',
        isInventory: (state) => state.user?.role === 'Inventory',

        // ─── Feature/User Permissions ───
        /**
         * Generic helper — checks if the current user's name (or SuperAdmin role)
         * grants access to a specific feature key.
         */
        can: (state) => (feature: string) => {
            if (!state.user?.name) return false
            if (state.user.role === 'SuperAdmin') return true
            const allowed = state.featurePermissions[feature] ?? []
            return allowed.includes(state.user.name)
        },

        // Legacy getters — all delegate to can()
        customerMenu():     boolean { return this.can('customerMenu') },
        isAmir():           boolean { return this.can('isAmir') },
        newRFQ():           boolean { return this.can('newRFQ') },
        ilsUsers():         boolean { return this.can('ilsUsers') },
        isPDFSelection():   boolean { return this.can('isPDFSelection') },

        // Gated menu getters
        paymentMenu():      boolean { return (this as any).isSuperAdmin || this.can('paymentMenu') || (this as any).isPayment },
        companyPresets():   boolean { return (this as any).isSuperAdmin || this.can('companyPresets') },
        syncApp():          boolean { return (this as any).isSuperAdmin || this.can('syncApp') },
        systemActivity():   boolean { return (this as any).isSuperAdmin || this.can('systemActivity') },
        supplierRequests(): boolean { return (this as any).isSuperAdmin || this.can('supplierRequests') },
        capList():          boolean { return (this as any).isSuperAdmin || this.can('capList') },
        ilsMenu():          boolean { return (this as any).isSuperAdmin || this.can('ils') || (this as any).ilsUsers },
        shippingMenu():     boolean { return (this as any).isSuperAdmin || this.can('shippingMenu') },
        actionCenter():     boolean { return (this as any).isSuperAdmin || this.can('actionCenter') },
        taskManager():      boolean { return (this as any).isSuperAdmin || this.can('taskManager') },

        userInitials: (state) => {
            if (!state.user?.name) return '?'
            return state.user.name
                .split(' ')
                .map((n: string) => n[0])
                .join('')
                .toUpperCase()
                .slice(0, 2)
        },
    },

    actions: {
        setUser(user: User) {
            this.user = user
            if (import.meta.client) {
                localStorage.setItem('procument_user', JSON.stringify(user))
            }
        },

        loadFromStorage() {
            if (import.meta.client) {
                const stored = localStorage.getItem('procument_user')
                if (stored) {
                    this.user = JSON.parse(stored)
                }
            }
        },

        logout() {
            this.user = null
            this.featurePermissions = { ...DEFAULT_FEATURE_PERMISSIONS }
            if (import.meta.client) {
                localStorage.removeItem('procument_user')
            }
        },

        /**
         * Fetches the live menu-permission map from the backend.
         * Called once after login and once after loadFromStorage.
         * Non-SuperAdmin users hit this too — the backend returns the full map
         * which the `can()` getter uses locally; non-SuperAdmin just can't
         * modify it via the UI.
         */
        async loadMenuPermissions() {
            if (!this.user?.token || !import.meta.client) return
            try {
                const config = useRuntimeConfig()
                const apiMap = config.public.apiMap as Record<string, string>
                const host = window.location.host
                const baseURL = apiMap[host] || apiMap['default']

                const groups = await $fetch<MenuPermissionGroup[]>('/menu-permissions', {
                    baseURL,
                    headers: { Authorization: `Bearer ${this.user.token}` },
                })

                const map: Record<string, string[]> = { ...DEFAULT_FEATURE_PERMISSIONS }
                for (const g of groups) {
                    map[g.feature] = g.userNames
                }
                this.featurePermissions = map
            } catch {
                // Non-critical — fall back to empty permissions
            }
        },
    },
})
