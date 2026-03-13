import { useTheme } from 'vuetify'

const STORAGE_KEY = 'procument_theme'

export function useAppTheme() {
  const vuetifyTheme = useTheme()

  const isDark = computed(() => vuetifyTheme.global.current.value.dark)

  function toggle() {
    const next = isDark.value ? 'procumentLight' : 'procumentDark'
    vuetifyTheme.global.name.value = next
    if (import.meta.client) {
      localStorage.setItem(STORAGE_KEY, next)
    }
  }

  function init() {
    if (import.meta.client) {
      const stored = localStorage.getItem(STORAGE_KEY)
      if (stored === 'procumentLight' || stored === 'procumentDark') {
        vuetifyTheme.global.name.value = stored
      }
    }
  }

  return { isDark, toggle, init }
}
