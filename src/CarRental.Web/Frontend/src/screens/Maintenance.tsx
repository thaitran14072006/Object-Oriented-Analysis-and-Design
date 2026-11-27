import { useState } from 'react'
import { Badge, PageHeader, SearchBar, Table } from '../components'

export function Maintenance() {
  const [query, setQuery] = useState('')
  const records = [
    ['BT001', 'Toyota Camry — 51A-12345', 'Thay dầu động cơ', '15/09/2026', '16/09/2026', '450,000 ₫', <Badge label="Hoàn thành" color="green" />],
    ['BT002', 'VinFast VF8 — 51F-00112', 'Sửa hệ thống điện', '18/09/2026', '21/09/2026', '2,800,000 ₫', <Badge label="Đang sửa" color="yellow" />],
    ['BT003', 'Honda CR-V — 51B-67890', 'Kiểm tra định kỳ', '20/09/2026', '20/09/2026', '0 ₫', <Badge label="Lên lịch" color="blue" />],
    ['BT004', 'Ford Ranger — 51G-33445', 'Thay lốp xe', '22/09/2026', '22/09/2026', '3,200,000 ₫', <Badge label="Lên lịch" color="blue" />],
    ['BT005', 'Mazda CX-5 — 51E-77889', 'Vá lốp, rửa xe', '10/09/2026', '10/09/2026', '200,000 ₫', <Badge label="Hoàn thành" color="green" />],
  ]

  return (
    <div>
      <PageHeader title="Quản lý bảo trì xe" desc="Lịch bảo dưỡng và sửa chữa phương tiện" action="Tạo phiếu bảo trì" />
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
        {[
          { label: 'Đang bảo trì', value: '4', color: 'border-l-red-400' },
          { label: 'Lên lịch', value: '8', color: 'border-l-sky-400' },
          { label: 'Hoàn thành T9', value: '12', color: 'border-l-emerald-400' },
          { label: 'Chi phí T9', value: '₫12.4M', color: 'border-l-amber-400' },
        ].map((s, i) => (
          <div key={i} className={`bg-white rounded-xl border border-[#E4E4E7] border-l-4 ${s.color} p-4`}>
            <p className="text-xs text-[#71717A] font-mono">{s.label}</p>
            <p className="text-2xl font-bold font-display">{s.value}</p>
          </div>
        ))}
      </div>
      <SearchBar placeholder="Tìm theo xe, loại bảo trì..." query={query} onChange={setQuery} />
      <Table
        headers={['Mã BT', 'Xe', 'Nội dung', 'Ngày vào', 'Ngày ra dự kiến', 'Chi phí', 'Trạng thái']}
        rows={records}
        query={query}
      />
    </div>
  )
}
