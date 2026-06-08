import { useTheme } from 'vuetify'

const STORAGE_KEY = 'procument_theme'

export type AppThemeName = 'procumentDark' | 'procumentLight' | 'procumentFrost'

const THEMES: AppThemeName[] = ['procumentDark', 'procumentLight', 'procumentFrost']

export function useAppTheme() {
  const vuetifyTheme = useTheme()

  const currentTheme = computed<AppThemeName>(
    () => vuetifyTheme.global.name.value as AppThemeName
  )

  const isDark = computed(() => vuetifyTheme.global.current.value.dark)

  function setTheme(name: AppThemeName) {
    vuetifyTheme.global.name.value = name
    if (import.meta.client) {
      localStorage.setItem(STORAGE_KEY, name)
    }
  }

  /** Cycles Dark → Light → Frost → Dark */
  function toggle() {
    const idx = THEMES.indexOf(currentTheme.value)
    const next = THEMES[(idx + 1) % THEMES.length]
    setTheme(next)
  }

  function init() {
    if (import.meta.client) {
      const stored = localStorage.getItem(STORAGE_KEY) as AppThemeName | null
      if (stored && THEMES.includes(stored)) {
        try {
          // Guard: Vuetify crashes if the theme name is not registered in vuetify.config.ts.
          // This can happen if localStorage has a theme from a newer build running on an old server.
          vuetifyTheme.global.name.value = stored
        } catch {
          localStorage.removeItem(STORAGE_KEY)
        }
      }
    }
  }

  return { isDark, currentTheme, toggle, setTheme, init }
}
