export function useApi() {
    const config = useRuntimeConfig()
    const authStore = useAuthStore()
    
    // 1. Get the current request URL to determine the frontend host
    const requestUrl = useRequestURL()
    const hostWithPort = requestUrl.host
    
    // 2. Resolve the correct base URL from your apiMap (with default fallback)
    const apiMap = config.public.apiMap as Record<string, string>
    const baseURL = apiMap[hostWithPort] || apiMap['default']

    async function apiFetch<T>(path: string, options: any = {}): Promise<T> {
        const isSessionEndpoint = path.startsWith('/auth/login') || path.startsWith('/auth/register')
            || path.startsWith('/auth/refresh') || path.startsWith('/auth/logout')

        // Refresh before expiry so direct user actions never hit an expired access token.
        if (!isSessionEndpoint && (!authStore.user?.token || authStore.shouldRefreshToken)) {
            const refreshed = await authStore.refreshSession()
            if (!refreshed) {
                await navigateTo('/login')
                throw new Error('Session expired')
            }
        }

        const execute = () => {
            // Let the browser add the multipart boundary for FormData uploads.
            const headers: Record<string, string> = {
                ...(options.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
                ...options.headers,
            }
            if (authStore.user?.token)
                headers.Authorization = `Bearer ${authStore.user.token}`

            return $fetch<T>(path, {
                baseURL,
                credentials: 'include',
                ...options,
                headers,
            })
        }

        try {
            return await execute()
        } catch (err: any) {
            // Rotate the session and retry the original save exactly once.
            if (!isSessionEndpoint && (err?.response?.status === 401 || err?.status === 401)) {
                if (await authStore.refreshSession()) return await execute()
                await navigateTo('/login')
                throw new Error('Session expired')
            }
            throw err
        }
    }

    return {
        baseURL,
        get: <T>(path: string, options: any = {}) => apiFetch<T>(path, options),
        post: <T>(path: string, body: any, options: any = {}) => apiFetch<T>(path, { method: 'POST', body, ...options }),
        put: <T>(path: string, body: any) => apiFetch<T>(path, { method: 'PUT', body }),
        patch: <T>(path: string, body: any) => apiFetch<T>(path, { method: 'PATCH', body }),
        del: <T>(path: string) => apiFetch<T>(path, { method: 'DELETE' }),
    }
}
