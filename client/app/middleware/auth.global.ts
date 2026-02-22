export default defineNuxtRouteMiddleware((to) => {
    if (import.meta.server) return

    const authStore = useAuthStore()
    authStore.loadFromStorage()

    if (!authStore.isAuthenticated && to.path !== '/login') {
        return navigateTo('/login')
    }

    if (authStore.isAuthenticated && to.path === '/login') {
        return navigateTo('/dashboard')
    }
})
