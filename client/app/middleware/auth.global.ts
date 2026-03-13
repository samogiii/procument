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
})
