import { useState } from 'react'
import { PageHeader, StatCard } from '../components'
import { request, useResource, button, inputClass, statusNames, RecordForm, DataTable, LoadState, dateTime, money } from './fleetApi'

type RecordRow = { id: string; vehicleId: string; vehicleName: string; plate: string; kind: string; start: string; end: string; actualStart: string | null; actualEnd: string | null; content: string; status: string; cost: number }
type Detail = {
  record: RecordRow
  progress: { content: string; created: string; recorder: string }[]
  costs: { item: string; provider: string; amount: number; date: string }[]
  safety: { inspector: string; passed: boolean; note: string | null; created: string }[]
}

export function Maintenance() {
  const [query, setQuery] = useState(''), [status, setStatus] = useState(''), [vehicle, setVehicle] = useState('')
  const [creating, setCreating] = useState(false), [selected, setSelected] = useState('')
  const params = new URLSearchParams(); if (query) params.set('q', query); if (status) params.set('status', status); if (vehicle) params.set('vehicle', vehicle)
  const records = useResource<RecordRow[]>(`maintenance?${params}`)
  const vehicles = useResource<{ id: string; name: string; plate: string; status: string }[]>('maintenance/vehicles')
  const rows = records.data || [], options = (vehicles.data || []).map(v => ({ value: v.id, label: `${v.name} — ${v.plate}` }))
  return <div><PageHeader title="Quản lý bảo trì xe" desc="Lịch bảo dưỡng, tiến độ và kiểm tra an toàn" />
    <div className="mb-5 grid gap-3 sm:grid-cols-4">{[
      ['Chưa thực hiện', String(rows.filter(x => x.status === 'ChuaThucHien').length)],
      ['Đang bảo trì', String(rows.filter(x => x.status === 'DangThucHien').length)],
      ['Hoàn tất', String(rows.filter(x => x.status === 'HoanTat').length)], ['Chi phí', money(rows.reduce((sum, x) => sum + x.cost, 0))],
    ].map(([label, value]) => <StatCard key={label} label={label} value={value} sub="Trong danh sách hiện tại" />)}</div>
    <div className="mb-4 flex flex-wrap gap-3"><input className={`${inputClass} sm:w-64`} aria-label="Tìm phiếu bảo trì" placeholder="Mã phiếu, xe, nội dung…" value={query} onChange={e => setQuery(e.target.value)} />
      <select className={`${inputClass} sm:w-48`} aria-label="Lọc trạng thái bảo trì" value={status} onChange={e => setStatus(e.target.value)}><option value="">Mọi trạng thái</option>{['ChuaThucHien', 'DangThucHien', 'HoanTat'].map(s => <option value={s} key={s}>{statusNames[s]}</option>)}</select>
      <select className={`${inputClass} sm:w-64`} aria-label="Lọc xe" value={vehicle} onChange={e => setVehicle(e.target.value)}><option value="">Tất cả xe</option>{options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}</select>
      <button className={button} onClick={() => setCreating(v => !v)}>{creating ? 'Đóng biểu mẫu' : 'Tạo lịch bảo trì'}</button></div>
    <LoadState loading={records.loading || vehicles.loading} error={records.error || vehicles.error} />
    {creating && <RecordForm title="Lập lịch bảo trì" fields={[
      { key: 'vehicleId', label: 'Xe', options }, { key: 'kind', label: 'Loại bảo trì', options: ['DinhKy', 'SuaChua', 'NangCap'].map(s => ({ value: s, label: statusNames[s] })) },
      { key: 'start', label: 'Bắt đầu dự kiến', type: 'datetime-local' }, { key: 'end', label: 'Kết thúc dự kiến', type: 'datetime-local' },
      { key: 'content', label: 'Nội dung', type: 'textarea', maxLength: 1000, placeholder: 'VD: Bảo dưỡng định kỳ, thay dầu và kiểm tra phanh' },
    ]} save={async data => { await request('maintenance', 'POST', data); records.reload(); setCreating(false) }} />}
    <DataTable headers={['Mã phiếu', 'Xe', 'Nội dung', 'Bắt đầu', 'Kết thúc dự kiến', 'Chi phí', 'Trạng thái', '']} rows={rows.map(row => [
      row.id, `${row.vehicleName} — ${row.plate}`, row.content, dateTime(row.start), dateTime(row.end), money(row.cost), statusNames[row.status],
      <button className="text-teal-700 underline" onClick={() => setSelected(row.id)}>Chi tiết</button>,
    ])} />
    {selected && <MaintenanceDetail key={selected} id={selected} close={() => setSelected('')} changed={records.reload} />}
  </div>
}

function MaintenanceDetail({ id, close, changed }: { id: string; close: () => void; changed: () => void }) {
  const result = useResource<Detail>(`maintenance/${encodeURIComponent(id)}`)
  const [message, setMessage] = useState(''), [busy, setBusy] = useState(false)
  const data = result.data, record = data?.record
  async function update(action: string, body?: unknown) {
    await request(`maintenance/${encodeURIComponent(id)}/${action}`, 'POST', body)
    result.reload(); changed(); setMessage('Đã cập nhật phiếu bảo trì.')
  }
  async function transition(action: string) {
    setBusy(true); setMessage('')
    try { await update(action) } catch (e) { setMessage((e as Error).message) } finally { setBusy(false) }
  }
  return <section className="mt-5 rounded-xl border bg-white p-5"><div className="flex justify-between gap-3"><h2 className="font-semibold">Phiếu {id}</h2><button onClick={close}>Đóng</button></div>
    <LoadState loading={result.loading} error={result.error} /><p role="status" className="my-3 text-sm">{message}</p>
    {record && data && <><p>{record.content}</p><p className="my-2 text-sm">{statusNames[record.kind]} · {statusNames[record.status]}</p>
      <p className="mb-4 text-sm text-zinc-500">Bắt đầu thực tế: {dateTime(record.actualStart)} · Hoàn tất: {dateTime(record.actualEnd)}</p>
      <div className="flex gap-3">
        {record.status === 'ChuaThucHien' && <button className={button} disabled={busy} onClick={() => void transition('start')}>Bắt đầu bảo trì</button>}
        {record.status === 'DangThucHien' && <button className={button} disabled={busy || !data.safety[0]?.passed} onClick={() => void transition('complete')}>Hoàn tất bảo trì</button>}
      </div>
      <h3 className="mb-2 mt-5 font-semibold">Tiến độ</h3><DataTable headers={['Thời gian', 'Nội dung', 'Người cập nhật']} rows={data.progress.map(p => [dateTime(p.created), p.content, p.recorder])} />
      <h3 className="mb-2 mt-5 font-semibold">Chi phí — {money(data.costs.reduce((sum, c) => sum + c.amount, 0))}</h3><DataTable headers={['Ngày', 'Hạng mục', 'Đơn vị thực hiện', 'Số tiền']} rows={data.costs.map(c => [c.date, c.item, c.provider, money(c.amount)])} />
      <h3 className="mb-2 mt-5 font-semibold">Kiểm tra an toàn</h3><DataTable headers={['Thời gian', 'Người kiểm tra', 'Kết quả', 'Ghi chú']} rows={data.safety.map(s => [dateTime(s.created), s.inspector, s.passed ? 'Đạt' : 'Không đạt', s.note || '—'])} />
      {record.status === 'DangThucHien' && <>
        <RecordForm title="Cập nhật tiến độ" fields={[{ key: 'content', label: 'Nội dung', type: 'textarea', maxLength: 1000, placeholder: 'VD: Đã thay dầu, đang kiểm tra phanh' }]} save={body => update('progress', body)} />
        <RecordForm title="Ghi chi phí" fields={[{ key: 'item', label: 'Hạng mục (chọn gợi ý hoặc nhập khác)', maxLength: 255, suggestions: ['Thay dầu động cơ', 'Thay lốp', 'Bảo dưỡng phanh', 'Thay ắc quy', 'Sửa điều hòa', ...data.costs.map(c => c.item)], placeholder: 'VD: Thay dầu động cơ' }, { key: 'provider', label: 'Đơn vị thực hiện', maxLength: 255, suggestions: data.costs.map(c => c.provider), placeholder: 'Tên garage hoặc đơn vị bảo dưỡng' }, { key: 'amount', label: 'Số tiền (đồng)', type: 'number', min: 0 }, { key: 'date', label: 'Ngày ghi nhận', type: 'date' }]} save={body => update('cost', body)} />
        <RecordForm title="Ghi kết quả an toàn" fields={[{ key: 'inspector', label: 'Người kiểm tra', maxLength: 100, suggestions: data.safety.map(s => s.inspector), placeholder: 'Họ tên người trực tiếp kiểm tra' }, { key: 'passed', label: 'Kết quả kiểm tra', type: 'boolean', options: [{ value: 'true', label: 'Đạt' }, { value: 'false', label: 'Không đạt' }] }, { key: 'note', label: 'Ghi chú', type: 'textarea', optional: true, maxLength: 1000, placeholder: 'Kết quả kiểm tra hoặc vấn đề cần khắc phục' }]} save={body => update('safety', body)} />
      </>}
    </>}
  </section>
}
