import { useState } from 'react'
import { Badge, PageHeader, SearchBar, Table } from '../components'

export function Vehicles() {
  const [query, setQuery] = useState('')
  const vehicles = [
    ['XE001', 'Toyota Camry 2.5Q', '51A-12345', 'Sedan', '2022', '1,500,000 ₫/ngày', <Badge label="Đang thuê" color="teal" />],
    ['XE002', 'Honda CR-V L', '51B-67890', 'SUV', '2023', '1,800,000 ₫/ngày', <Badge label="Sẵn sàng" color="green" />],
    ['XE003', 'Hyundai Tucson', '51C-11223', 'SUV', '2022', '1,600,000 ₫/ngày', <Badge label="Đang thuê" color="teal" />],
    ['XE004', 'Kia Seltos 1.4T', '51D-44556', 'SUV', '2023', '1,200,000 ₫/ngày', <Badge label="Sẵn sàng" color="green" />],
    ['XE005', 'Mazda CX-5', '51E-77889', 'SUV', '2021', '1,700,000 ₫/ngày', <Badge label="Đang thuê" color="teal" />],
    ['XE006', 'VinFast VF8', '51F-00112', 'SUV Điện', '2024', '2,000,000 ₫/ngày', <Badge label="Bảo trì" color="red" />],
    ['XE007', 'Ford Ranger XLS', '51G-33445', 'Pickup', '2022', '1,400,000 ₫/ngày', <Badge label="Sẵn sàng" color="green" />],
    ['XE008', 'Mitsubishi Xpander', '51H-66778', 'MPV', '2023', '1,100,000 ₫/ngày', <Badge label="Sẵn sàng" color="green" />],
  ]

  return (
    <div>
      <PageHeader title="Quản lý xe" desc="Danh sách toàn bộ phương tiện trong hệ thống" action="Thêm xe mới" />
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
        {[
          { label: 'Tổng xe', value: '48', color: 'border-l-4 border-l-[#0F766E]' },
          { label: 'Đang cho thuê', value: '23', color: 'border-l-4 border-l-sky-400' },
          { label: 'Sẵn sàng', value: '21', color: 'border-l-4 border-l-emerald-400' },
          { label: 'Đang bảo trì', value: '4', color: 'border-l-4 border-l-red-400' },
        ].map((s, i) => (
          <div key={i} className={`bg-white rounded-xl border border-[#E4E4E7] ${s.color} p-4`}>
            <p className="text-xs text-[#71717A] font-mono">{s.label}</p>
            <p className="text-2xl font-bold font-display">{s.value}</p>
          </div>
        ))}
      </div>
      <SearchBar placeholder="Tìm theo biển số, tên xe, loại xe..." query={query} onChange={setQuery} />
      <Table
        headers={['Mã xe', 'Tên xe', 'Biển số', 'Loại xe', 'Năm SX', 'Giá thuê', 'Trạng thái']}
        rows={vehicles}
        query={query}
      />
    </div>
  )
}
