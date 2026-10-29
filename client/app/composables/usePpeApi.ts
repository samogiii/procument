/**
 * useApi for the new Procurement Plus page (api/ppe): every user sees their own parts; a SuperAdmin can open another
 * user's — the picked user travels as ?asUser=<id> on each call.
 */
export function usePpeApi() {
  const api = useApi()
  /** null = my own page; else a user id. Shared by the whole page. */
  const asUser = useState<number | null>('ppeAsUser', () => null)
  const withUser = (path: string) => {
    const full = `/ppe${path}`
    if (!asUser.value) return full
    return `${full}${full.includes('?') ? '&' : '?'}asUser=${asUser.value}`
  }
  return {
    asUser,
    url: withUser,
    get: <T>(path: string) => api.get<T>(withUser(path)),
    post: <T>(path: string, body: any) => api.post<T>(withUser(path), body),
    put: <T>(path: string, body: any) => api.put<T>(withUser(path), body),
    del: <T>(path: string) => api.del<T>(withUser(path)),
  }
}
