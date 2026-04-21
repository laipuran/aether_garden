import { NavLink, Outlet } from 'react-router-dom'

const navItems = [
  { to: '/', label: '主页', end: true },
  { to: '/blog', label: '博客' },
  { to: '/notes', label: '随笔' },
  { to: '/about', label: '个人介绍' },
]

export function Layout() {
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
      </header>

      <main className="page">
        <Outlet />
      </main>

      <footer className="site-footer">
        <span className="mono">laipuran</span> · Built with React + TypeScript
      </footer>
    </div>
  )
}
