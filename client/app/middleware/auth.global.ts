// Routes any Expert is allowed to access. Anything else → 404.
// Sub-paths (e.g. /rfqs/123) are allowed via prefix match.
const EXPERT_ALLOWED_PREFIXES = [
    '/rfqs',
    '/procument',         // RFQ Items
    '/quotes',
    '/procurements',
    '/purchase-orders',
    '/tasks',
] as const

// Expert SYD gets the full Expert allowlist PLUS /ils.
const EXPERT_SYD_ALLOWED_PREFIXES = [
    ...EXPERT_ALLOWED_PREFIXES,
    '/ils',
] as const

function matchesAnyPrefix(path: string, prefixes: readonly string[]): boolean {
    return prefixes.some(p => path === p || path.startsWith(p + '/'))
}

export default defineNuxtRouteMiddleware((to) => {
    if (import.meta.server) return

    const authStore = useAuthStore()
    authStore.loadFromStorage()

    // If user has a token but it's expired, log them out
    if (authStore.user?.token && authStore.isTokenExpired) {
        authStore.logout()
        if (to.path !== '/login') {
            return navigateTo('/login')
        }
        return
    }

    if (!authStore.isAuthenticated && to.path !== '/login') {
        return navigateTo('/login')
    }

    if (authStore.isAuthenticated && to.path === '/login') {
        return navigateTo('/dashboard')
    }

    // ── Role-scoped route allowlist ────────────────────────────────────
    // Experts (and the special SYD operator) get a tight whitelist of pages.
    // Visiting anything outside the list throws a 404 — typing the URL into
    // the address bar is denied the same way as a hidden nav entry.
    if (authStore.isAuthenticated && authStore.user?.role === 'Expert') {
        const isSyd = authStore.user?.name === 'SYD'
        const allowed = isSyd ? EXPERT_SYD_ALLOWED_PREFIXES : EXPERT_ALLOWED_PREFIXES

        // Always permit /login (handled above) and the auth routes.
        if (to.path === '/login') return

        // Funnel the post-login landing pages (`/` and `/dashboard`) to /rfqs for both
        // regular Experts and SYD — SYD just additionally has /ils available.
        if (to.path === '/' || to.path === '/dashboard') {
            return navigateTo('/rfqs')
        }

        if (!matchesAnyPrefix(to.path, allowed)) {
            throw createError({ statusCode: 404, statusMessage: 'Not Found', fatal: true })
        }
    }
})
