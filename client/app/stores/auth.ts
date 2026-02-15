import { defineStore } from 'pinia'

interface User {
    id: number
    name: string
    email: string
    role: string
    token: string
}

export const useAuthStore = defineStore('auth', {
    state: () => ({
        user: null as User | null,
    }),

    getters: {
        isAuthenticated: (state) => !!state.user?.token,
        isAdmin: (state) => state.user?.role === 'Admin',
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
