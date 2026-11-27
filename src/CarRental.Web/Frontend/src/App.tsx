import { useEffect, useState, type ComponentType } from 'react'
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

type Module =
  | 'dashboard'
  | 'vehicles'
  | 'bookings'
  | 'customers'
  | 'schedule'
  | 'payments'
  | 'reports'
  | 'maintenance'
  | 'promotions'
  | 'settings'

const navItems: { key: Module; label: string; shortLabel?: string; icon: ComponentType; screen: ComponentType }[] = [
  { key: 'dashboard', label: 'Tổng quan', icon: Icon.Dashboard, screen: Dashboard },
  { key: 'vehicles', label: 'Quản lý xe', icon: Icon.Car, screen: Vehicles },
  { key: 'bookings', label: 'Đặt xe & thuê xe', icon: Icon.Booking, screen: Bookings },
  { key: 'customers', label: 'Khách hàng', icon: Icon.Users, screen: Customers },
  { key: 'schedule', label: 'Lịch thuê - trả xe', icon: Icon.Schedule, screen: Schedule },
  { key: 'payments', label: 'Giá thuê & thanh toán', icon: Icon.Payment, screen: Payments, shortLabel: 'Thanh toán' },
  { key: 'reports', label: 'Báo cáo & thống kê', icon: Icon.Chart, screen: Reports },
  { key: 'maintenance', label: 'Bảo trì xe', icon: Icon.Wrench, screen: Maintenance },
  { key: 'promotions', label: 'Khuyến mãi & đánh giá', icon: Icon.Tag, screen: Promotions, shortLabel: 'Khuyến mãi' },
  { key: 'settings', label: 'Cài đặt', icon: Icon.Settings, screen: Settings },
]

function readSection() {
  return navItems.find(item => '#' + item.key === window.location.hash) ?? navItems[0]
}

function Sidebar({ active }: { active: Module }) {
  return (
    <aside className="w-14 shrink-0 bg-[#18181B] text-white flex flex-col md:w-56">
      <div className="flex items-center gap-2 justify-center md:justify-start md:px-5 py-5 border-b border-white/10">
        <div className="w-8 h-8 shrink-0 bg-[#0F766E] rounded-lg flex items-center justify-center">
          <Icon.Car />
        </div>
        <div className="hidden md:block">
          <p className="text-sm font-bold font-display leading-tight">RentalPro</p>
          <p className="text-xs text-zinc-500 font-mono">Quản lý thuê xe</p>
        </div>
      </div>
      <nav aria-label="Điều hướng quản trị" className="flex-1 py-3 overflow-y-auto">
        {navItems.map(item => (
          <a key={item.key} href={'#' + item.key} title={item.label}
            aria-current={active === item.key ? 'page' : undefined}
            className={'relative flex w-full items-center justify-center md:justify-start gap-3 md:px-5 py-2.5 text-sm transition-colors ' +
              (active === item.key ? 'bg-[#0F766E] text-white' : 'text-zinc-400 hover:bg-white/5 hover:text-white')}>
            <span className="shrink-0"><item.icon /></span>
            <span className="sr-only md:not-sr-only font-medium">{item.label}</span>
          </a>
        ))}
      </nav>
      <div className="hidden md:block px-5 py-4 border-t border-white/10">
        <div className="flex items-center gap-3 mb-3">
          <div className="w-8 h-8 shrink-0 rounded-full bg-[#0F766E] flex items-center justify-center text-sm font-bold font-display">A</div>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-white truncate">Admin</p>
            <p className="text-xs text-zinc-500 font-mono truncate">admin@phuongnam.vn</p>
          </div>
        </div>
        <button type="button" disabled title="Cần kết nối xác thực"
          className="flex items-center gap-2 text-zinc-400 text-xs">
          <Icon.Logout /><span>Đăng xuất</span>
        </button>
      </div>
    </aside>
  )
}

function Topbar({ label }: { label: string }) {
  return (
    <header className="h-14 gap-2 border-b border-[#E4E4E7] bg-white flex items-center justify-between px-3 sm:px-6 shrink-0">
      <div className="min-w-0 flex items-center gap-2 text-xs text-[#71717A] font-mono">
        <span className="hidden sm:inline">RentalPro</span>
        <span className="hidden sm:inline">/</span>
        <span className="truncate text-[#18181B]">{label}</span>
      </div>
      <div className="flex shrink-0 items-center gap-2 sm:gap-3">
        <span className="rounded bg-[#CCFBF1] px-2 py-0.5 text-xs text-[#0F766E]">Dữ liệu mẫu</span>
        <span className="hidden lg:inline text-xs font-mono text-[#71717A]">Thứ Bảy, 19/09/2026</span>
        <button type="button" disabled aria-label="Thông báo" title="Cần kết nối thông báo"
          className="relative p-2 text-[#71717A]">
          <Icon.Bell />
          <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full" />
        </button>
      </div>
    </header>
  )
}

export default function App() {
  const [section, setSection] = useState(readSection)
  useEffect(() => {
    const syncSection = () => setSection(readSection())
    window.addEventListener('hashchange', syncSection)
    return () => window.removeEventListener('hashchange', syncSection)
  }, [])
  const Content = section.screen

  return (
    <div className="flex h-dvh overflow-hidden bg-[#F5F5F3]">
      <Sidebar active={section.key} />
      <div className="min-w-0 flex-1 flex flex-col overflow-hidden">
        <Topbar label={section.shortLabel ?? section.label} />
        <main key={section.key} className="flex-1 overflow-y-auto p-3 sm:p-6">
          <Content />
        </main>
      </div>
    </div>
  )
}
