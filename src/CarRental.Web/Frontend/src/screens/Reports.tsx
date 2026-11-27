import { PageHeader, StatCard } from '../components'

export function Reports() {
  const months = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9']
  const values = [85, 92, 110, 98, 125, 140, 158, 165, 186]
  const maxVal = 200

  return (
    <div>
      <PageHeader title="Báo cáo & thống kê" desc="Phân tích hiệu quả kinh doanh — 2026" />

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard label="Doanh thu năm 2026" value="₫1.26B" sub="9 tháng đã qua" accent />
        <StatCard label="Tổng hợp đồng" value="384" sub="tăng 18% YoY" />
        <StatCard label="KH mới 2026" value="127" sub="40% quay lại" />
        <StatCard label="Xe lợi nhuận nhất" value="CR-V" sub="₫28M/tháng" />
      </div>

      {/* Bar chart */}
      <div className="bg-white rounded-xl border border-[#E4E4E7] p-5 mb-5">
        <h3 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mb-4">Doanh thu theo tháng (triệu ₫)</h3>
        <div className="flex items-end gap-1 sm:gap-3 h-48">
          {months.map((m, i) => (
            <div key={i} className="min-w-0 h-full flex-1 flex flex-col justify-end items-center gap-1">
              <span className="text-[10px] sm:text-xs font-mono text-[#71717A]">{values[i]}</span>
              <div
                className={`w-full rounded-t-md transition-all ${i === months.length - 1 ? 'bg-[#0F766E]' : 'bg-[#CCFBF1] border border-[#0F766E]/20'}`}
                style={{ height: `${(values[i] / maxVal) * 100}%` }}
              />
              <span className="text-xs font-mono text-[#71717A]">{m}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Top vehicles */}
      <div className="bg-white rounded-xl border border-[#E4E4E7] p-5">
        <h3 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mb-4">Xe cho thuê nhiều nhất</h3>
        <div className="space-y-3">
          {[
            { car: 'Honda CR-V L', trips: 42, pct: 87 },
            { car: 'Toyota Camry 2.5Q', trips: 38, pct: 79 },
            { car: 'Mazda CX-5', trips: 35, pct: 73 },
            { car: 'Hyundai Tucson', trips: 30, pct: 63 },
            { car: 'Kia Seltos', trips: 27, pct: 56 },
          ].map((v, i) => (
            <div key={i} className="flex items-center gap-3">
              <span className="w-5 text-xs font-mono text-[#71717A] text-right">{i + 1}</span>
              <span className="w-24 sm:w-36 text-sm text-[#18181B] truncate">{v.car}</span>
              <div className="flex-1 bg-[#F5F5F3] rounded-full h-2">
                <div className="bg-[#0F766E] h-2 rounded-full" style={{ width: `${v.pct}%` }} />
              </div>
              <span className="text-xs font-mono text-[#71717A] w-16 text-right">{v.trips} chuyến</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
