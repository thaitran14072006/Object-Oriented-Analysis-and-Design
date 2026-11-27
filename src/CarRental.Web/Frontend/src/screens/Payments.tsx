import { useState } from 'react'
import { Badge, PageHeader, SearchBar, StatCard, Table } from '../components'

export function Payments() {
  const [query, setQuery] = useState('')
  const payments = [
    ['TT001', 'HD001', 'Nguyễn Văn An', '6,000,000 ₫', 'Chuyển khoản', '18/09/2026', <Badge label="Đã thanh toán" color="green" />],
    ['TT002', 'HD002', 'Trần Thị Bình', '5,400,000 ₫', 'Tiền mặt', '20/09/2026', <Badge label="Đã thanh toán" color="green" />],
    ['TT003', 'HD003', 'Lê Minh Cường', '9,600,000 ₫', 'Chuyển khoản', '—', <Badge label="Chờ thanh toán" color="yellow" />],
    ['TT004', 'HD005', 'Võ Thị Em', '6,800,000 ₫', 'Ví điện tử', '—', <Badge label="Chờ thanh toán" color="yellow" />],
    ['TT005', 'HD006', 'Đặng Văn Phúc', '10,000,000 ₫', '—', '—', <Badge label="Chờ xác nhận" color="gray" />],
  ]

  return (
    <div>
      <PageHeader title="Quản lý giá thuê & thanh toán" desc="Theo dõi tình trạng thanh toán các hợp đồng" action="Ghi nhận thanh toán" />
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-5">
        <StatCard label="Doanh thu tháng 9" value="₫186M" sub="↑ 12% so với T8" accent />
        <StatCard label="Đã thu" value="₫179M" sub="96.2% hóa đơn" />
        <StatCard label="Chờ thanh toán" value="₫7M" sub="7 hợp đồng" />
      </div>

      {/* Pricing table */}
      <div className="bg-white rounded-xl border border-[#E4E4E7] p-5 mb-5">
        <h3 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mb-3">Bảng giá hiện hành</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          {[
            { type: 'Sedan', price: '1,000,000–1,500,000 ₫' },
            { type: 'SUV', price: '1,200,000–2,000,000 ₫' },
            { type: 'MPV', price: '900,000–1,200,000 ₫' },
            { type: 'Pickup', price: '1,100,000–1,400,000 ₫' },
          ].map((p, i) => (
            <div key={i} className="bg-[#F5F5F3] rounded-lg p-3">
              <p className="text-xs text-[#71717A] font-mono">{p.type}</p>
              <p className="text-sm font-semibold text-[#18181B] mt-1">{p.price}</p>
              <p className="text-xs text-[#71717A]">/ ngày</p>
            </div>
          ))}
        </div>
      </div>

      <SearchBar placeholder="Tìm theo mã thanh toán, hợp đồng, khách hàng..." query={query} onChange={setQuery} />
      <Table
        headers={['Mã TT', 'Mã HĐ', 'Khách hàng', 'Số tiền', 'Phương thức', 'Ngày TT', 'Trạng thái']}
        rows={payments}
        query={query}
      />
    </div>
  )
}
