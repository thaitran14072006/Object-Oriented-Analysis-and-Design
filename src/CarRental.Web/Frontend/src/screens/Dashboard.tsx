import { Badge, PageHeader, StatCard, Table } from '../components'

export function Dashboard() {
  const recentBookings = [
    ['HD001', 'Nguyễn Văn An', 'Toyota Camry — 51A-12345', '18/09/2026', '22/09/2026', <Badge label="Đang thuê" color="teal" />],
    ['HD002', 'Trần Thị Bình', 'Honda CR-V — 51B-67890', '17/09/2026', '20/09/2026', <Badge label="Hoàn thành" color="green" />],
    ['HD003', 'Lê Minh Cường', 'Hyundai Tucson — 51C-11223', '19/09/2026', '25/09/2026', <Badge label="Chờ xác nhận" color="yellow" />],
    ['HD004', 'Phạm Hoàng Dũng', 'Kia Seltos — 51D-44556', '15/09/2026', '18/09/2026', <Badge label="Hoàn thành" color="green" />],
    ['HD005', 'Võ Thị Em', 'Mazda CX-5 — 51E-77889', '20/09/2026', '24/09/2026', <Badge label="Đang thuê" color="teal" />],
  ]

  return (
    <div>
      <PageHeader title="Tổng quan hệ thống" desc="Dữ liệu cập nhật đến 19/09/2026" />
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard label="Tổng xe" value="48" sub="4 xe đang bảo trì" accent />
        <StatCard label="Đang cho thuê" value="23" sub="Công suất 47.9%" />
        <StatCard label="Đặt xe hôm nay" value="12" sub="↑ 3 so với hôm qua" />
        <StatCard label="Doanh thu tháng" value="₫186M" sub="↑ 12% so với T8/2026" />
      </div>

      {/* Mini stats row */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        {[
          { label: 'Khách hàng mới', value: '34', sub: 'tháng này' },
          { label: 'Xe sẵn sàng', value: '21', sub: 'trạng thái tốt' },
          { label: 'Thanh toán chờ', value: '7', sub: 'hợp đồng chờ thanh toán' },
        ].map((s, i) => (
          <div key={i} className="bg-white rounded-xl border border-[#E4E4E7] p-4 flex gap-4 items-center">
            <div>
              <p className="text-xs text-[#71717A] font-mono uppercase tracking-wider">{s.label}</p>
              <p className="text-2xl font-bold font-display text-[#18181B]">{s.value}</p>
              <p className="text-xs text-[#71717A]">{s.sub}</p>
            </div>
          </div>
        ))}
      </div>

      <h2 className="text-sm font-semibold text-[#18181B] mb-3 font-mono uppercase tracking-wider">Hợp đồng gần đây</h2>
      <Table
        headers={['Mã HĐ', 'Khách hàng', 'Xe', 'Ngày thuê', 'Ngày trả', 'Trạng thái']}
        rows={recentBookings}
      />
    </div>
  )
}
