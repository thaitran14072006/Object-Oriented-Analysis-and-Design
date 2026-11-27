import { PageHeader } from '../components'

export function Settings() {
  return (
    <div>
      <PageHeader title="Quản lý cài đặt" desc="Cấu hình hệ thống và tài khoản" />
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        {/* Company info */}
        <div className="bg-white rounded-xl border border-[#E4E4E7] p-5">
          <h3 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mb-4">Thông tin công ty</h3>
          <div className="space-y-3">
            {[
              { label: 'Tên công ty', value: 'Công ty TNHH Thuê Xe Phương Nam' },
              { label: 'Địa chỉ', value: '123 Nguyễn Văn Linh, Quận 7, TP.HCM' },
              { label: 'Điện thoại', value: '028 1234 5678' },
              { label: 'Email', value: 'contact@phuongnamcar.vn' },
              { label: 'Mã số thuế', value: '0312345678' },
            ].map((f, i) => (
              <div key={i}>
                <label htmlFor={"company-" + i} className="text-xs text-[#71717A] font-mono">{f.label}</label>
                <input id={"company-" + i} readOnly defaultValue={f.value} className="w-full mt-1 px-3 py-2 border border-[#E4E4E7] rounded-lg text-sm bg-[#F5F5F3] focus:outline-none focus:ring-2 focus:ring-[#0F766E]/30 focus:border-[#0F766E]" />
              </div>
            ))}
            <button type="button" disabled title="Cần kết nối nghiệp vụ" className="w-full mt-2 py-2 bg-[#0F766E] text-white rounded-lg text-sm font-medium hover:bg-[#0D9488] transition-colors">Lưu thay đổi</button>
          </div>
        </div>

        {/* System settings */}
        <div className="space-y-5">
          <div className="bg-white rounded-xl border border-[#E4E4E7] p-5">
            <h3 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mb-4">Cài đặt hệ thống</h3>
            <div className="space-y-3">
              {[
                { label: 'Tiền đặt cọc mặc định', value: '30%' },
                { label: 'Phí phát sinh ngoài giờ', value: '50,000 ₫/giờ' },
                { label: 'Số ngày thuê tối thiểu', value: '1 ngày' },
                { label: 'Số xe tối đa/khách', value: '3 xe' },
              ].map((f, i) => (
                <div key={i} className="flex items-center justify-between">
                  <span className="text-sm text-[#52525B]">{f.label}</span>
                  <span className="text-sm font-mono font-medium text-[#18181B] bg-[#F5F5F3] px-3 py-1 rounded">{f.value}</span>
                </div>
              ))}
            </div>
          </div>

          <div className="bg-white rounded-xl border border-[#E4E4E7] p-5">
            <h3 className="text-sm font-semibold font-mono uppercase tracking-wider text-[#18181B] mb-4">Tài khoản quản trị</h3>
            <div className="space-y-3">
              <div>
                <label htmlFor="account-name" className="text-xs text-[#71717A] font-mono">Tên hiển thị</label>
                <input id="account-name" readOnly defaultValue="Admin Phương Nam" className="w-full mt-1 px-3 py-2 border border-[#E4E4E7] rounded-lg text-sm bg-[#F5F5F3] focus:outline-none focus:ring-2 focus:ring-[#0F766E]/30" />
              </div>
              <div>
                <label htmlFor="account-password" className="text-xs text-[#71717A] font-mono">Mật khẩu mới</label>
                <input id="account-password" disabled type="password" placeholder="••••••••" className="w-full mt-1 px-3 py-2 border border-[#E4E4E7] rounded-lg text-sm bg-[#F5F5F3] focus:outline-none focus:ring-2 focus:ring-[#0F766E]/30" />
              </div>
              <button type="button" disabled title="Cần kết nối nghiệp vụ" className="w-full py-2 border border-[#E4E4E7] text-[#18181B] rounded-lg text-sm font-medium hover:bg-[#F5F5F3] transition-colors">Đổi mật khẩu</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
