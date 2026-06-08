self.addEventListener('push', event => {
  const data = event.data?.json() ?? {}
  event.waitUntil(
    self.registration.showNotification(data.title ?? 'Procument', {
      body: data.body ?? '',
      icon: '/favicon.ico',
      badge: '/favicon.ico',
      tag: data.url,
      data: { url: data.url ?? '/' }
    })
  )
})

self.addEventListener('notificationclick', event => {
  event.notification.close()
  const url = event.notification.data?.url ?? '/'
  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
      for (const c of list) {
        if (c.url.includes(self.location.origin) && 'focus' in c) {
          c.navigate(url)
          return c.focus()
        }
      }
      return clients.openWindow(url)
    })
  )
})
