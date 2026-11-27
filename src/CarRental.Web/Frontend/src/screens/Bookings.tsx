import { useState } from 'react'
import { Badge, PageHeader, SearchBar, Table } from '../components'

export function Bookings() {
  const [query, setQuery] = useState('')
  const [tab, setTab] = useState<'all' | 'pending' | 'active' | 'done'>('all')
  const bookings = [
    { status: 'active', cells: ['HD001', 'Nguyễn Văn An', '0901234567', 'Toyota Camry', '18/09', '22/09', '6,000,000 ₫', <Badge label="Đang thuê" color="teal" />] },
    { status: 'done', cells: ['HD002', 'Trần Thị Bình', '0912345678', 'Honda CR-V', '17/09', '20/09', '5,400,000 ₫', <Badge label="Hoàn thành" color="green" />] },
    { status: 'pending', cells: ['HD003', 'Lê Minh Cường', '0923456789', 'Hyundai Tucson', '19/09', '25/09', '9,600,000 ₫', <Badge label="Chờ xác nhận" color="yellow" />] },
    { status: 'done', cells: ['HD004', 'Phạm Hoàng Dũng', '0934567890', 'Kia Seltos', '15/09', '18/09', '3,600,000 ₫', <Badge label="Hoàn thành" color="green" />] },
    { status: 'active', cells: ['HD005', 'Võ Thị Em', '0945678901', 'Mazda CX-5', '20/09', '24/09', '6,800,000 ₫', <Badge label="Đang thuê" color="teal" />] },
    { status: 'pending', cells: ['HD006', 'Đặng Văn Phúc', '0956789012', 'VinFast VF8', '21/09', '26/09', '10,000,000 ₫', <Badge label="Chờ xác nhận" color="yellow" />] },
  ]

  const tabs = [
    { key: 'all', label: 'Tất cả' },
    { key: 'pending', label: 'Chờ xác nhận' },
    { key: 'active', label: 'Đang thuê' },
    { key: 'done', label: 'Hoàn thành' },
  ] as const

  return (
    <div>
      <PageHeader title="Quản lý đặt xe & thuê xe" desc="Theo dõi toàn bộ hợp đồng thuê xe" action="Tạo hợp đồng mới" />
      <div className="flex flex-wrap gap-1 mb-5 p-1 bg-[#F5F5F3] rounded-lg w-fit border border-[#E4E4E7]">
        {tabs.map(t => (
          <button type="button" key={t.key} aria-pressed={tab === t.key} onClick={() => setTab(t.key)}
            className={`px-4 py-1.5 rounded-md text-sm font-medium transition-all ${tab === t.key ? 'bg-white text-[#0F766E] shadow-sm' : 'text-[#71717A] hover:text-[#18181B]'}`}>
            {t.label}
          </button>
        ))}
      </div>
      <SearchBar placeholder="Tìm theo mã hợp đồng, khách hàng, xe..." query={query} onChange={setQuery} />
      <Table
        headers={['Mã HĐ', 'Khách hàng', 'SĐT', 'Xe thuê', 'Ngày thuê', 'Ngày trả', 'Tổng tiền', 'Trạng thái']}
        rows={bookings.filter(booking => tab === 'all' || booking.status === tab).map(booking => booking.cells)}
        query={query}
      />
    </div>
  )
}
