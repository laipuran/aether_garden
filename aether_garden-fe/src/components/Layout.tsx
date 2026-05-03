import { useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'

const navItems = [
  { to: '/', label: '主页', end: true },
  { to: '/blog', label: '博客' },
  { to: '/notes', label: '随笔' },
  { to: '/about', label: '个人介绍' },
]

const THEME_STORAGE_KEY = 'theme-preference'

type ThemeMode = 'system' | 'light' | 'dark'

const getStoredTheme = (): ThemeMode => {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY)
    if (stored === 'light' || stored === 'dark' || stored === 'system') {
      return stored
    }
  } catch {
    return 'system'
  }

  return 'system'
}

const getSystemTheme = (): 'light' | 'dark' => {
  if (typeof window === 'undefined') {
    return 'light'
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light'
}

export function Layout() {
  const location = useLocation()
  const [themeMode, setThemeMode] = useState<ThemeMode>(() => getStoredTheme())
  const [systemTheme, setSystemTheme] = useState<'light' | 'dark'>(() =>
    getSystemTheme(),
  )

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    const handleChange = (event: MediaQueryListEvent) => {
      setSystemTheme(event.matches ? 'dark' : 'light')
    }

    setSystemTheme(mediaQuery.matches ? 'dark' : 'light')

    if (typeof mediaQuery.addEventListener === 'function') {
      mediaQuery.addEventListener('change', handleChange)
      return () => mediaQuery.removeEventListener('change', handleChange)
    }

    mediaQuery.addListener(handleChange)
    return () => mediaQuery.removeListener(handleChange)
  }, [])

  const activeTheme = useMemo(() => {
    return themeMode === 'system' ? systemTheme : themeMode
  }, [themeMode, systemTheme])

  const themeLabel = useMemo(() => {
    if (themeMode === 'system') {
      return 'Auto'
    }

    return themeMode === 'light' ? 'Light' : 'Dark'
  }, [themeMode])

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', activeTheme)
    document.documentElement.setAttribute('data-theme-mode', themeMode)

    try {
      localStorage.setItem(THEME_STORAGE_KEY, themeMode)
    } catch {
      // Ignore write errors
    }
  }, [activeTheme, themeMode])

  const handleThemeToggle = () => {
    setThemeMode((current) => {
      if (current === 'system') {
        return 'light'
      }

      if (current === 'light') {
        return 'dark'
      }

      return 'system'
    })
  }

  return (
    <div className="site-shell">
      <header className="site-nav">
        <div className="brand">aether_garden</div>
        <nav className="nav-links" aria-label="主导航">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                isActive ? 'nav-link active' : 'nav-link'
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <button
          className="theme-toggle"
          type="button"
          onClick={handleThemeToggle}
          aria-label={`Theme: ${themeLabel}`}
        >
          <span className="theme-toggle-icon" aria-hidden="true">
            {activeTheme === 'dark' ? (
              <svg viewBox="0 0 24 24" role="img" focusable="false">
                <path d="M14.8 2.1a.8.8 0 0 1 .9.6.8.8 0 0 1-.3.9 8 8 0 1 0 5.4 14.3.8.8 0 0 1 1.3.7 9.6 9.6 0 1 1-7.3-16.5Z" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" role="img" focusable="false">
                <path d="M12 4.2a.8.8 0 0 1 .8.8V7a.8.8 0 1 1-1.6 0V5a.8.8 0 0 1 .8-.8Zm0 12a4.2 4.2 0 1 0 0-8.4 4.2 4.2 0 0 0 0 8.4Zm0 3.8a.8.8 0 0 1 .8.8v2a.8.8 0 1 1-1.6 0v-2a.8.8 0 0 1 .8-.8Zm9.2-8a.8.8 0 0 1-.8.8h-2a.8.8 0 1 1 0-1.6h2a.8.8 0 0 1 .8.8ZM5.6 12a.8.8 0 0 1-.8.8h-2a.8.8 0 1 1 0-1.6h2a.8.8 0 0 1 .8.8Zm12.2-6.4a.8.8 0 0 1 1.1 0l1.4 1.4a.8.8 0 1 1-1.1 1.1l-1.4-1.4a.8.8 0 0 1 0-1.1ZM4.7 17.7a.8.8 0 0 1 1.1 0l1.4 1.4a.8.8 0 0 1-1.1 1.1l-1.4-1.4a.8.8 0 0 1 0-1.1Zm13.1 1.1a.8.8 0 0 1 0-1.1l1.4-1.4a.8.8 0 1 1 1.1 1.1l-1.4 1.4a.8.8 0 0 1-1.1 0ZM4.7 6.5a.8.8 0 0 1 0-1.1L6.1 4a.8.8 0 1 1 1.1 1.1L5.8 6.5a.8.8 0 0 1-1.1 0Z" />
              </svg>
            )}
          </span>
          <span className="theme-toggle-label">{themeLabel}</span>
        </button>
      </header>

      <main className="page">
        <div key={location.pathname} className="page-transition">
          <Outlet />
        </div>
      </main>

      <footer className="site-footer">
        <span className="mono">laipuran</span> · Built with React + TypeScript
      </footer>
    </div>
  )
}
