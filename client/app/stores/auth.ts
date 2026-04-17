import { defineStore } from 'pinia'

interface User {
    id: number
    name: string
    email: string
    role: string
    token: string
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
        isAdmin: (state) => state.user?.role === 'Admin',
        customerMenu: (state) => state.user?.name ===  'AMJ' || state.user?.name ===  'KZM' || state.user?.name ===  'System Admin' , 
        isAmir: (state) => state.user?.name === 'AMJ' || state.user?.name === "KZM" || state.user?.name === 'MGH',
        newRFQ: (state) => state.user?.name === 'AHM',
        ilsUsers: (state) => state.user?.name === 'System Admin'
            || state.user?.name === 'SYD'
            || state.user?.name === 'MGH',
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
