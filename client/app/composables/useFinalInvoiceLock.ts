export function useFinalInvoiceLock(entityType: string, entityId: Ref<string | number>) {
  const api = useApi()
  const isLocked = ref(false)

  async function checkLock() {
    try {
      const res = await api.get<any>(`/final-invoices/is-locked?entityType=${entityType}&entityId=${entityId.value}`)
      isLocked.value = res?.locked === true
    } catch {
      isLocked.value = false
    }
  }

  return { isLocked, checkLock }
}
