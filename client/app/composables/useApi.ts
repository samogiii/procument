export function useApi() {
    const config = useRuntimeConfig()
    const authStore = useAuthStore()

    async function apiFetch<T>(path: string, options: any = {}): Promise<T> {
        const headers: Record<string, string> = {
            'Content-Type': 'application/json',
            ...options.headers,
        }

        if (authStore.user?.token) {
            headers.Authorization = `Bearer ${authStore.user.token}`
        }

        return await $fetch<T>(`${config.public.apiBase}${path}`, {
            ...options,
            headers,
        })
    }

    return {
        get: <T>(path: string) => apiFetch<T>(path),
        post: <T>(path: string, body: any) => apiFetch<T>(path, { method: 'POST', body }),
        put: <T>(path: string, body: any) => apiFetch<T>(path, { method: 'PUT', body }),
        patch: <T>(path: string, body?: any) => apiFetch<T>(path, { method: 'PATCH', body }),
        del: <T>(path: string) => apiFetch<T>(path, { method: 'DELETE' }),
    }
}
