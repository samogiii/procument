export function useApi() {
    const config = useRuntimeConfig()
    const authStore = useAuthStore()

    async function apiFetch<T>(path: string, options: any = {}): Promise<T> {
        // Pre-check: if token is expired, logout immediately
        if (authStore.user?.token && authStore.isTokenExpired) {
            authStore.logout()
            await navigateTo('/login')
            throw new Error('Session expired')
        }

        const headers: Record<string, string> = {
            'Content-Type': 'application/json',
            ...options.headers,
        }

        if (authStore.user?.token) {
            headers.Authorization = `Bearer ${authStore.user.token}`
        }

        try {
            return await $fetch<T>(`${config.public.apiBase}${path}`, {
                ...options,
                headers,
            })
        } catch (err: any) {
            // Handle 401 Unauthorized — token rejected by server
            if (err?.response?.status === 401 || err?.status === 401) {
                authStore.logout()
                await navigateTo('/login')
                throw new Error('Session expired')
            }
            throw err
        }
    }

    return {
        get: <T>(path: string, options: any = {}) => apiFetch<T>(path, options),
        post: <T>(path: string, body: any) => apiFetch<T>(path, { method: 'POST', body }),
        put: <T>(path: string, body: any) => apiFetch<T>(path, { method: 'PUT', body }),
        patch: <T>(path: string, body: any) => apiFetch<T>(path, { method: 'PATCH', body }),
        del: <T>(path: string) => apiFetch<T>(path, { method: 'DELETE' }),
    }
}
