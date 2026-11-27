import { useEffect, useState, type ComponentType, type CSSProperties } from 'react'
import { api, errorText, ApiError, type Admin, type Appearance } from './api'
import { Action, Notice } from './forms'
import { Icon } from './icons'
import { Dashboard } from './screens/Dashboard'
import { Vehicles } from './screens/Vehicles'
import { Bookings } from './screens/Bookings'
import { Customers } from './screens/Customers'
import { Schedule } from './screens/Schedule'
import { Payments } from './screens/Payments'
import { Reports } from './screens/Reports'
import { Maintenance } from './screens/Maintenance'
import { Promotions } from './screens/Promotions'
import { Settings } from './screens/Settings'
import { Login } from './screens/Login'

interface NavigationItem {
  key: string; label: string; shortLabel?: string; icon: ComponentType; screen?: ComponentType;
  permissions: string[]
}

const navItems: NavigationItem[] = [
  { key: 'dashboard', label: 'Tổng quan', icon: Icon.Dashboard, screen: Dashboard, permissions: ['Admin.Access'] },
  { key: 'vehicles', label: 'Quản lý xe', icon: Icon.Car, screen: Vehicles, permissions: ['Fleet.Access'] },
  { key: 'bookings', label: 'Đặt xe & thuê xe', icon: Icon.Booking, screen: Bookings, permissions: ['Bookings.Access'] },
  { key: 'customers', label: 'Khách hàng', icon: Icon.Users, permissions: ['Customers.Read'] },
  { key: 'schedule', label: 'Lịch thuê - trả xe', icon: Icon.Schedule, screen: Schedule, permissions: ['RentalSchedule.Access'] },
  { key: 'payments', label: 'Giá thuê & thanh toán', icon: Icon.Payment, screen: Payments, shortLabel: 'Thanh toán', permissions: ['Billing.Access'] },
  { key: 'reports', label: 'Báo cáo & thống kê', icon: Icon.Chart, screen: Reports, permissions: ['Reports.Access'] },
  { key: 'maintenance', label: 'Bảo trì xe', icon: Icon.Wrench, screen: Maintenance, permissions: ['Maintenance.Access'] },
  { key: 'promotions', label: 'Khuyến mãi & đánh giá', icon: Icon.Tag, screen: Promotions, shortLabel: 'Khuyến mãi',
    permissions: ['Promotions.Access'] },
  { key: 'settings', label: 'Cài đặt', icon: Icon.Settings, permissions: ['Settings.Personal', 'Admin.Security'] },
]

function Sidebar({ active, items, admin, onLogout }: {
  active: string; items: NavigationItem[]; admin: Admin; onLogout: () => void
}) {
  return (
    <aside className="w-14 shrink-0 bg-[#18181B] text-white flex flex-col md:w-56">
      <div className="flex items-center gap-2 justify-center md:justify-start md:px-5 py-5 border-b border-white/10">
        <div className="w-8 h-8 shrink-0 bg-[var(--rental-accent)] rounded-lg flex items-center justify-center"><Icon.Car /></div>
        <div className="hidden md:block">
          <p className="text-sm font-bold font-display leading-tight">RentalPro</p>
          <p className="text-xs text-zinc-500 font-mono">Quản lý thuê xe</p>
        </div>
      </div>
      <nav aria-label="Điều hướng quản trị" className="flex-1 py-3 overflow-y-auto">
        {items.map(item => (
          <a key={item.key} href={'#' + item.key} title={item.label}
            aria-current={active === item.key ? 'page' : undefined}
            className={'relative flex w-full items-center justify-center md:justify-start gap-3 md:px-5 py-2.5 text-sm transition-colors ' +
              (active === item.key ? 'bg-[var(--rental-accent)] text-white' : 'text-zinc-400 hover:bg-white/5 hover:text-white')}>
            <span className="shrink-0"><item.icon /></span>
            <span className="sr-only md:not-sr-only font-medium">{item.label}</span>
          </a>
        ))}
      </nav>
      <div className="px-2 py-4 border-t border-white/10 md:px-5">
        <div className="hidden items-center gap-3 mb-3 md:flex">
          <div className="w-8 h-8 shrink-0 rounded-full bg-[var(--rental-accent)] flex items-center justify-center text-sm font-bold font-display">
            {admin.name.charAt(0).toUpperCase()}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-white truncate">{admin.name}</p>
            <p className="text-xs text-zinc-400 font-mono truncate">{admin.id}</p>
          </div>
        </div>
        <button type="button" onClick={onLogout} title="Đăng xuất"
          className="flex w-full justify-center items-center gap-2 text-zinc-400 hover:text-white text-xs md:justify-start">
          <Icon.Logout /><span className="sr-only md:not-sr-only">Đăng xuất</span>
        </button>
      </div>
    </aside>
  )
}

function Workspace() {
  const [hash, setHash] = useState(location.hash)
  const [admin, setAdmin] = useState<Admin>()
  const [appearance, setAppearance] = useState<Appearance | null>(null)
  const [failure, setFailure] = useState('')
  const [recheck, setRecheck] = useState(0)

  useEffect(() => {
    const sync = () => setHash(location.hash)
    const expired = () => location.replace('/login')
    window.addEventListener('hashchange', sync)
    window.addEventListener('rentalpro:session-ended', expired)
    return () => {
      window.removeEventListener('hashchange', sync)
      window.removeEventListener('rentalpro:session-ended', expired)
    }
  }, [])

  useEffect(() => {
    const abort = new AbortController()
    setFailure('')
    api<Admin>('/api/identity/me', 'GET', undefined, abort.signal).then(async current => {
      setAdmin(current)
      if (current.permissions.includes('Settings.Personal')) {
        const personal = await api<{ appearance: Appearance | null }>('/api/settings', 'GET', undefined, abort.signal)
        setAppearance(personal.appearance)
      }
    }).catch(error => {
      if (abort.signal.aborted) return
      if (error instanceof ApiError && error.status === 401) location.replace('/login')
      else setFailure(errorText(error))
    })
    return () => abort.abort()
  }, [recheck])

  useEffect(() => {
    document.documentElement.style.fontSize = (appearance?.size ?? 100) + '%'
    return () => { document.documentElement.style.fontSize = '' }
  }, [appearance?.size])

  if (!admin) return <main className="min-h-dvh grid place-content-center gap-3 p-4">
    {failure ? <><Notice error={failure} /><Action onClick={() => setRecheck(value => value + 1)}>Thử lại</Action></>
      : <p role="status">Đang kiểm tra phiên đăng nhập…</p>}
  </main>

  const items = navItems.filter(item => item.permissions.some(permission => admin.permissions.includes(permission)))
  const section = items.find(item => '#' + item.key === hash) ?? items[0]
  const Content = section.screen
  const color = appearance?.color === 'blue' ? '#1D4ED8' : appearance?.color === 'purple' ? '#7E22CE' : '#0F766E'
  const fontFamily = appearance?.font === 'serif' ? 'Georgia, serif' :
    appearance?.font === 'mono' ? '"JetBrains Mono", Consolas, monospace' : undefined

  async function logout() {
    try {
      await api('/api/identity/logout', 'POST')
      location.replace('/login')
    } catch (error) { setFailure(errorText(error)) }
  }

  return (
    <div className="flex h-dvh overflow-hidden bg-[#F5F5F3]"
      style={{ '--rental-accent': color, fontFamily } as CSSProperties}>
      <Sidebar active={section.key} items={items} admin={admin} onLogout={() => void logout()} />
      <div className="min-w-0 flex-1 flex flex-col overflow-hidden">
        <header className="h-14 shrink-0 flex items-center justify-between gap-2 border-b border-zinc-200 bg-white px-3 sm:px-6">
          <div className="min-w-0 truncate text-xs font-mono">
            <span className="text-zinc-500 hidden sm:inline">RentalPro / </span>{section.shortLabel ?? section.label}
          </div>
          {Content && <span className="rounded bg-[#CCFBF1] px-2 py-0.5 text-xs text-[#0F766E]">Dữ liệu mẫu</span>}
        </header>
        <main key={section.key} className={'flex-1 overflow-y-auto ' +
          (appearance?.layout === 'compact' ? 'p-2 sm:p-3' : 'p-3 sm:p-6')}>
          <Notice error={failure} />
          {Content && <Content />}
          {section.key === 'customers' && <Customers permissions={admin.permissions} />}
          {section.key === 'settings' && <Settings admin={admin} onProfile={(current, selected) => {
            setAdmin(current); setAppearance(selected)
          }} />}
        </main>
      </div>
    </div>
  )
}

export default function App() {
  return location.pathname === '/login' ? <Login /> : <Workspace />
}
