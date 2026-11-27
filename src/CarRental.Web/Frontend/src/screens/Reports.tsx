import { useState } from 'react'
import { PageHeader } from '../components'
import { request, useResource, download, button, inputClass, statusNames, RecordForm, DataTable, LoadState, dateTime } from './fleetApi'

type ReportRow = { label: string; count: number; amount: number; secondary: number; rate: number }
type Report = { title: string; headers: string[]; rows: ReportRow[] }
type Periodic = { id: number; from: string; to: string; status: string; created: string }
const options = [
  ['usage', 'Mức sử dụng xe'], ['revenue', 'Doanh thu'], ['fleet', 'Tình trạng xe'], ['maintenance', 'Chi phí bảo trì'],
  ['quality', 'Chất lượng dịch vụ'], ['promotions', 'Hiệu quả khuyến mãi'], ['channels', 'Kênh đặt xe'],
]
const localDate = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`

export function Reports() {
  const [type, setType] = useState('revenue'), [from, setFrom] = useState(localDate(new Date(new Date().getFullYear(), new Date().getMonth(), 1))), [to, setTo] = useState(localDate(new Date()))
  const [applied, setApplied] = useState({ type, from, to }), [message, setMessage] = useState(''), [busy, setBusy] = useState(false)
  const params = new URLSearchParams(applied), report = useResource<Report>(`reports?${params}`), periodic = useResource<Periodic[]>('reports/periodic')
  async function exportFile(path: string, name: string) {
    setBusy(true); setMessage(''); try { await download(path, name) } catch (e) { setMessage((e as Error).message) } finally { setBusy(false) }
  }
  const data = report.data, columns = data?.headers.map((label, index) => ({ label, index })).filter(c => c.label) || []
  return <div><PageHeader title="Báo cáo & thống kê" desc="Theo dõi hoạt động thuê xe theo kỳ" />
    <form className="mb-4 flex flex-wrap items-end gap-3" onSubmit={e => { e.preventDefault(); setApplied({ type, from, to }); report.reload() }}>
      <label className="text-sm">Báo cáo<select className={inputClass} value={type} onChange={e => setType(e.target.value)}>{options.map(([value, name]) => <option value={value} key={value}>{name}</option>)}</select></label>
      <label className="text-sm">Từ ngày<input className={inputClass} type="date" required value={from} onChange={e => setFrom(e.target.value)} /></label>
      <label className="text-sm">Đến ngày<input className={inputClass} type="date" required min={from} value={to} onChange={e => setTo(e.target.value)} /></label>
      <button className={button}>Xem báo cáo</button><button type="button" className={button} disabled={busy || report.loading || !data} onClick={() => void exportFile(`reports/export?${params}`, 'Bao-cao.xlsx')}>Xuất Excel</button>
    </form><LoadState loading={report.loading} error={report.error} /><p role="status" className="my-3 text-sm">{message}</p>
    {data && <><h2 className="mb-3 font-semibold">{data.title}</h2>
      <DataTable headers={columns.map(c => c.label)} rows={data.rows.map(row => {
        const values = [statusNames[row.label] || row.label, row.count, row.amount, row.secondary, row.rate]
        return columns.map(c => typeof values[c.index] === 'number' ? Number(values[c.index]).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) : String(values[c.index]))
      })} />
      {applied.type === 'revenue' && <p className="my-3 text-sm">Tổng thực thu: {data.rows.reduce((sum, row) => sum + row.amount, 0).toLocaleString('vi-VN')} ₫</p>}
      {applied.type === 'fleet' && <p className="my-3 text-sm text-zinc-500">Tình trạng tại thời điểm xem báo cáo.</p>}
      {applied.type === 'channels' && <p className="my-3 text-sm text-zinc-500">Thống kê các đơn có lịch nhận dự kiến trong kỳ.</p>}
    </>}
    <h2 className="mb-3 mt-7 font-semibold">Báo cáo khuyến mãi định kỳ</h2><LoadState loading={periodic.loading} error={periodic.error} />
    <DataTable headers={['Từ ngày', 'Đến ngày', 'Tạo lúc', 'Kết quả', '']} rows={(periodic.data || []).map(p => [p.from, p.to, dateTime(p.created), statusNames[p.status],
      <button className="underline" disabled={busy || p.status !== 'ThanhCong'} onClick={() => void exportFile(`reports/periodic/${p.id}/download`, `Khuyen-mai-${p.from}.xlsx`)}>Tải Excel</button>,
    ])} />
    <RecordForm title="Tạo hoặc thử lại một kỳ" fields={[{ key: 'from', label: 'Từ ngày', type: 'date' }, { key: 'to', label: 'Đến ngày', type: 'date' }]} save={async body => { await request('reports/periodic', 'POST', body); periodic.reload() }} />
  </div>
}
