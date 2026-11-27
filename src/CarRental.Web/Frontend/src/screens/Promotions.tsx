import { Badge, PageHeader, Table } from '../components'

export function Promotions() {
  const promos = [
    ['KM001', 'Khuyến mãi tháng 9', 'Giảm 15% cho khách thuê từ 5 ngày', '01/09/2026', '30/09/2026', '15%', <Badge label="Đang áp dụng" color="teal" />],
    ['KM002', 'Ưu đãi VIP', 'Giảm 20% cho khách VIP', 'Không giới hạn', '—', '20%', <Badge label="Đang áp dụng" color="teal" />],
    ['KM003', 'Cuối tuần giảm giá', 'Giảm 10% thuê T7, CN', '01/01/2026', '31/12/2026', '10%', <Badge label="Đang áp dụng" color="teal" />],
    ['KM004', 'Flash sale Q4', 'Giảm 25% tháng 10–12', '01/10/2026', '31/12/2026', '25%', <Badge label="Sắp bắt đầu" color="blue" />],
    ['KM005', 'Tết Dương lịch', 'Giảm 30% 28/12–02/01', '28/12/2026', '02/01/2027', '30%', <Badge label="Sắp bắt đầu" color="blue" />],
  ]

  const reviews = [
    ['DG001', 'Nguyễn Văn An', 'Toyota Camry', '5/5 ⭐', 'Xe rất sạch, dịch vụ tốt, sẽ quay lại!', '20/09/2026'],
    ['DG002', 'Trần Thị Bình', 'Honda CR-V', '4/5 ⭐', 'Nhân viên nhiệt tình, xe chạy êm.', '18/09/2026'],
    ['DG003', 'Lê Minh Cường', 'Mazda CX-5', '5/5 ⭐', 'Giá hợp lý, xe đẹp, thủ tục nhanh.', '15/09/2026'],
  ]

  return (
    <div>
      <PageHeader title="Quản lý khuyến mãi & đánh giá" desc="Chương trình ưu đãi và phản hồi khách hàng" action="Tạo khuyến mãi" />
      <Table
        headers={['Mã KM', 'Tên chương trình', 'Mô tả', 'Bắt đầu', 'Kết thúc', 'Giảm giá', 'Trạng thái']}
        rows={promos}
      />
      <h2 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mt-6 mb-3">Đánh giá gần đây</h2>
      <div className="grid grid-cols-1 gap-3">
        {reviews.map((r, i) => (
          <div key={i} className="bg-white rounded-xl border border-[#E4E4E7] p-4 flex gap-4">
            <div className="w-8 h-8 rounded-full bg-[#CCFBF1] flex items-center justify-center text-[#0F766E] font-bold text-sm font-display flex-shrink-0">
              {r[1].charAt(0)}
            </div>
            <div className="flex-1">
              <div className="flex flex-wrap items-center gap-2 mb-1">
                <span className="font-medium text-sm text-[#18181B]">{r[1]}</span>
                <span className="text-xs text-[#71717A] font-mono">{r[2]}</span>
                <span className="text-xs font-mono">{r[3]}</span>
              </div>
              <p className="text-sm text-[#52525B]">{r[4]}</p>
              <p className="text-xs text-[#71717A] font-mono mt-1">{r[5]}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
