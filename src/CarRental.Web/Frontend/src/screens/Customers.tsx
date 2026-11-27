import { useState } from 'react'
import { Badge, PageHeader, SearchBar, Table } from '../components'

export function Customers() {
  const [query, setQuery] = useState('')
  const customers = [
    ['KH001', 'Nguyễn Văn An', '0901234567', 'nguyenvanan@email.com', '012345678901', '5 lần', '32,000,000 ₫', <Badge label="VIP" color="teal" />],
    ['KH002', 'Trần Thị Bình', '0912345678', 'tranthib@email.com', '012345678902', '3 lần', '18,000,000 ₫', <Badge label="Thường" color="gray" />],
    ['KH003', 'Lê Minh Cường', '0923456789', 'leminhcuong@email.com', '012345678903', '8 lần', '65,000,000 ₫', <Badge label="VIP" color="teal" />],
    ['KH004', 'Phạm Hoàng Dũng', '0934567890', 'phamhoangdung@email.com', '012345678904', '1 lần', '6,000,000 ₫', <Badge label="Mới" color="blue" />],
    ['KH005', 'Võ Thị Em', '0945678901', 'vothiem@email.com', '012345678905', '4 lần', '28,000,000 ₫', <Badge label="Thường" color="gray" />],
    ['KH006', 'Đặng Văn Phúc', '0956789012', 'dangvanphuc@email.com', '012345678906', '2 lần', '15,000,000 ₫', <Badge label="Thường" color="gray" />],
  ]

  return (
    <div>
      <PageHeader title="Quản lý khách hàng" desc="Thông tin và lịch sử thuê xe của khách hàng" action="Thêm khách hàng" />
      <SearchBar placeholder="Tìm theo tên, số điện thoại, CCCD..." query={query} onChange={setQuery} />
      <Table
        headers={['Mã KH', 'Họ và tên', 'Điện thoại', 'Email', 'CCCD', 'Số lần thuê', 'Tổng chi tiêu', 'Hạng']}
        rows={customers}
        query={query}
      />
    </div>
  )
}
