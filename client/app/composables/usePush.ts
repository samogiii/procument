export function usePush() {
  const api = useApi()

  function urlBase64ToUint8Array(base64: string) {
    const pad = '='.repeat((4 - (base64.length % 4)) % 4)
    const b64 = (base64 + pad).replace(/-/g, '+').replace(/_/g, '/')
    const raw = atob(b64)
    return Uint8Array.from([...raw].map(c => c.charCodeAt(0)))
  }

  async function subscribe() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) return
    if (Notification.permission === 'denied') return

    try {
      const reg = await navigator.serviceWorker.register('/sw.js', { scope: '/' })
      await navigator.serviceWorker.ready

      const { publicKey } = await api.get<{ publicKey: string }>('/notifications/vapid-public-key')
      if (!publicKey) return

      const existing = await reg.pushManager.getSubscription()
      if (existing) return // already subscribed

      const permission = await Notification.requestPermission()
      if (permission !== 'granted') return

      const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey)
      })

      const json = sub.toJSON()
      await api.post('/notifications/push-subscribe', {
        endpoint: json.endpoint,
        p256dh: (json.keys as any)?.p256dh ?? '',
        auth: (json.keys as any)?.auth ?? ''
      })
    } catch {
      // VAPID not configured or permission denied — silent fail
    }
  }

  return { subscribe }
}
