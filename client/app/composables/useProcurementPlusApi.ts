/**
 * useApi for Procurement Plus: every user has their own page, and a SuperAdmin can open another
 * user's (or every user's) — the picked user travels as ?asUser=<id|all> on each call.
 */
export function useProcurementPlusApi() {
  const api = useApi()
  /** null = my own page; a user id; or 'all' (SuperAdmin "All users"). Shared by the whole page. */
  const asUser = useState<string | null>('ppAsUser', () => null)
  const withUser = (path: string) => {
    if (!asUser.value) return path
    return `${path}${path.includes('?') ? '&' : '?'}asUser=${encodeURIComponent(asUser.value)}`
  }
  return {
    asUser,
    get: <T>(path: string, options: any = {}) => api.get<T>(withUser(path), options),
    post: <T>(path: string, body: any, options: any = {}) => api.post<T>(withUser(path), body, options),
    put: <T>(path: string, body: any) => api.put<T>(withUser(path), body),
    patch: <T>(path: string, body: any) => api.patch<T>(withUser(path), body),
    del: <T>(path: string) => api.del<T>(withUser(path)),
  }
}
