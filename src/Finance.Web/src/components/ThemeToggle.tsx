import { Moon, Sun } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Button } from './ui/button'

const themeStorageKey = 'finance-theme'
const themeChangeEventName = 'finance-theme-change'

type Theme = 'light' | 'dark'

function getPreferredTheme(): Theme {
  if (typeof window === 'undefined') {
    return 'light'
  }

  const storedTheme = window.localStorage.getItem(themeStorageKey)
  if (storedTheme === 'light' || storedTheme === 'dark') {
    return storedTheme
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function applyTheme(theme: Theme) {
  document.documentElement.classList.toggle('dark', theme === 'dark')
}

function isTheme(value: unknown): value is Theme {
  return value === 'light' || value === 'dark'
}

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(() => getPreferredTheme())

  useEffect(() => {
    function updateTheme(event: Event) {
      const theme = (event as CustomEvent<unknown>).detail
      if (isTheme(theme)) {
        setTheme(theme)
      }
    }

    window.addEventListener(themeChangeEventName, updateTheme)
    return () => window.removeEventListener(themeChangeEventName, updateTheme)
  }, [])

  useEffect(() => {
    applyTheme(theme)
    window.localStorage.setItem(themeStorageKey, theme)
  }, [theme])

  const isDark = theme === 'dark'
  const label = isDark ? 'Switch to light mode' : 'Switch to dark mode'

  function toggleTheme() {
    setTheme(x => {
      const nextTheme = x === 'dark' ? 'light' : 'dark'
      window.dispatchEvent(new CustomEvent(themeChangeEventName, { detail: nextTheme }))
      return nextTheme
    })
  }

  return (
    <Button
      aria-label={label}
      onClick={toggleTheme}
      size="icon"
      title={label}
      type="button"
      variant="ghost"
    >
      {isDark ? <Sun className="size-4" /> : <Moon className="size-4" />}
    </Button>
  )
}
