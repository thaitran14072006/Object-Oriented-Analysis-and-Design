import { Badge, PageHeader, Table } from '../components'

export function Schedule() {
  const days = ['T4', 'T5', 'T6', 'T7', 'CN', 'T2', 'T3']
  const dates = [16, 17, 18, 19, 20, 21, 22]
  const today = 19

  const events = [
    { day: 0, car: 'Toyota Camry', type: 'pickup', color: 'bg-teal-500' },
    { day: 1, car: 'Honda CR-V', type: 'return', color: 'bg-amber-500' },
    { day: 2, car: 'Mazda CX-5', type: 'pickup', color: 'bg-teal-500' },
    { day: 3, car: 'Kia Seltos', type: 'return', color: 'bg-amber-500' },
    { day: 4, car: 'VinFast VF8', type: 'pickup', color: 'bg-teal-500' },
    { day: 5, car: 'Ford Ranger', type: 'pickup', color: 'bg-teal-500' },
  ]

  const schedule = [
    ['LH001', 'Nguyễn Văn An', 'Toyota Camry — 51A-12345', '18/09/2026 08:00', '22/09/2026 17:00', <Badge label="Đang thuê" color="teal" />],
    ['LH002', 'Trần Thị Bình', 'Honda CR-V — 51B-67890', '17/09/2026 09:00', '20/09/2026 09:00', <Badge label="Đã trả" color="green" />],
    ['LH003', 'Võ Thị Em', 'Mazda CX-5 — 51E-77889', '20/09/2026 10:00', '24/09/2026 10:00', <Badge label="Sắp nhận" color="blue" />],
    ['LH004', 'Đặng Văn Phúc', 'VinFast VF8 — 51F-00112', '21/09/2026 08:00', '26/09/2026 08:00', <Badge label="Sắp nhận" color="blue" />],
  ]

  return (
    <div>
      <PageHeader title="Lịch thuê & trả xe" desc="Tuần 16–22/09/2026" />

      {/* Calendar strip */}
      <div className="bg-white rounded-xl border border-[#E4E4E7] p-5 mb-5 overflow-x-auto" tabIndex={0} aria-label="Lịch thuê và trả xe" role="region">
        <div className="min-w-[540px] grid grid-cols-7 gap-2 mb-4">
          {days.map((d, i) => (
            <div key={i} className={`text-center rounded-lg p-3 ${dates[i] === today ? 'bg-[#0F766E] text-white' : 'hover:bg-[#F5F5F3]'}`}>
              <p className={`text-xs font-mono font-medium ${dates[i] === today ? 'text-teal-200' : 'text-[#71717A]'}`}>{d}</p>
              <p className={`text-xl font-bold font-display mt-1 ${dates[i] === today ? 'text-white' : 'text-[#18181B]'}`}>{dates[i]}</p>
            </div>
          ))}
        </div>
        <div className="min-w-[540px] grid grid-cols-7 gap-2">
          {days.map((_, i) => {
            const ev = events.filter(e => e.day === i)
            return (
              <div key={i} className="min-h-[60px] space-y-1">
                {ev.map((e, j) => (
                  <div key={j} className={`${e.color} text-white text-xs rounded px-2 py-1`}>
                    <p className="font-medium truncate">{e.type === 'pickup' ? '↑ Nhận' : '↓ Trả'}</p>
                    <p className="truncate opacity-80">{e.car}</p>
                  </div>
                ))}
              </div>
            )
          })}
        </div>
      </div>

      <h2 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mb-3">Chi tiết lịch</h2>
      <Table
        headers={['Mã lịch', 'Khách hàng', 'Xe', 'Giờ nhận', 'Giờ trả', 'Trạng thái']}
        rows={schedule}
      />
    </div>
  )
}
