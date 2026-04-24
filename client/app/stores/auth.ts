import { defineStore } from 'pinia'

interface User {
    id: number
    name: string
    email: string
    role: string
    token: string
}

/**
 * Maps features to specific user names or roles.
 * Easy to update when adding new users or actions.
 */
const FeaturePermissions = {
    customerMenu: ['AMJ', 'KZM', 'System Admin'],
    isAmir: ['AMJ', 'KZM', 'MGH', 'System Admin'], // Management/Supervisor group
    newRFQ: ['AHM','GHS'],
    ilsUsers: ['System Admin', 'SYD', 'MGH'],
    isPDFSelection: ['System Admin', 'AMJ', 'MGH'],
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
        isPayment: (state) => state.user?.role === 'Payment' || state.user?.role === 'SuperAdmin',
        isExpert: (state) => state.user?.role === 'Expert',
        // isPDFSelection:(state) => state.user?.
        // ─── Feature/User Permissions ───
        // Generic helper to check if current user has access to a specific feature key
        can: (state) => (feature: keyof typeof FeaturePermissions) => {
            if (!state.user?.name) return false
            const allowed = FeaturePermissions[feature]
            return allowed.includes(state.user.name) || state.user.role === 'SuperAdmin'
        },

        // Legacy getters refactored to use the central permission map
        customerMenu(): boolean { return this.can('customerMenu') },
        isAmir(): boolean { return this.can('isAmir') },
        newRFQ(): boolean { return this.can('newRFQ') },
        ilsUsers(): boolean { return this.can('ilsUsers') },
        isPDFSelection(): boolean { return this.can('isPDFSelection') },


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
            if (import.meta.client) {
                localStorage.removeItem('procument_user')
            }
        },
    },
})
