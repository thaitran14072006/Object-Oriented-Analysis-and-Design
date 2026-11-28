import { useState } from 'react'
import { PageHeader, StatCard } from '../components'
import { request, useResource, download, button, inputClass, statusNames, RecordForm, DataTable, LoadState, type Field } from './fleetApi'

type Vehicle = { id: string; typeId: string; name: string; brand: string; plate: string; color: string; year: number; seats: number; transmission: string; parking: string | null; status: string; archived: boolean }
type VehicleType = { id: string; name: string }
type Detail = { vehicle: Vehicle; images: { url: string }[] }

export function Vehicles() {
  const [query, setQuery] = useState(''), [status, setStatus] = useState(''), [type, setType] = useState(''), [archived, setArchived] = useState(false)
  const [editor, setEditor] = useState<Record<string, unknown>>(), [creating, setCreating] = useState(false), [detail, setDetail] = useState<Detail>()
  const [message, setMessage] = useState(''), [busy, setBusy] = useState(false)
  const params = new URLSearchParams({ q: query, status, type, archived: String(archived) })
  const records = useResource<Vehicle[]>(`fleet?${params}`), types = useResource<VehicleType[]>('fleet/types')
  const rows = records.data || [], typeOptions = (types.data || []).map(t => ({ value: t.id, label: t.name }))
  const parkingOptions = ['Bãi A', 'Bãi B', 'Bãi C'].map(value => ({ value, label: value }))
  const oldParking = editor?.parking && !parkingOptions.some(p => p.value === editor.parking) ? String(editor.parking) : ''
  async function act(work: () => Promise<void>) { setBusy(true); setMessage(''); try { await work() } catch (e) { setMessage((e as Error).message) } finally { setBusy(false) } }
  async function open(id: string, edit: boolean) {
    await act(async () => {
      const result = await request<Detail>(`fleet/${encodeURIComponent(id)}`)
      if (edit) { setDetail(undefined); setCreating(false); setEditor({ ...result.vehicle, images: result.images.map(i => i.url).join('\n') }) }
      else { setEditor(undefined); setDetail(result) }
    })
  }
  const fields: Field[] = [
    { key: 'id', label: 'Mã xe', maxLength: 20, readOnly: !creating }, { key: 'typeId', label: 'Loại xe', options: typeOptions },
    { key: 'name', label: 'Tên xe', maxLength: 100 }, { key: 'brand', label: 'Hãng xe', maxLength: 80 },
    { key: 'plate', label: 'Biển số', maxLength: 20 }, { key: 'color', label: 'Màu sắc', maxLength: 40 },
    { key: 'year', label: `Năm sản xuất (2000–${new Date().getFullYear()})`, type: 'number', min: 2000, max: new Date().getFullYear() },
    { key: 'seats', label: 'Số chỗ (4–16)', type: 'number', min: 4, max: 16 },
    { key: 'transmission', label: 'Hộp số', options: ['Số sàn', 'Tự động'].map(value => ({ value, label: value })) },
    { key: 'parking', label: 'Vị trí đỗ', optional: !oldParking, options: parkingOptions },
    { key: 'status', label: 'Tình trạng', options: ['SanSang', 'NgungKhaiThac'].map(s => ({ value: s, label: statusNames[s] })) },
    { key: 'images', label: 'Đường dẫn ảnh (mỗi dòng một ảnh)', type: 'textarea', optional: true, maxLength: 6012 },
  ]
  return <div><PageHeader title="Quản lý xe" desc="Hồ sơ phương tiện và tình trạng khai thác" />
    <div className="mb-5 grid gap-3 sm:grid-cols-4">{[
      ['Kết quả lọc', rows.length], ['Sẵn sàng', rows.filter(x => x.status === 'SanSang').length],
      ['Đang cho thuê', rows.filter(x => x.status === 'DangChoThue').length], ['Đang bảo trì', rows.filter(x => x.status === 'DangBaoTri').length],
    ].map(([label, value]) => <StatCard key={label} label={String(label)} value={String(value)} sub="Trong danh sách hiện tại" />)}</div>
    <div className="mb-4 flex flex-wrap gap-3">
      <input className={`${inputClass} sm:w-64`} aria-label="Tìm xe" placeholder="Tên xe, biển số, loại xe…" value={query} onChange={e => setQuery(e.target.value)} />
      <select className={`${inputClass} sm:w-44`} aria-label="Lọc tình trạng" value={status} onChange={e => setStatus(e.target.value)}><option value="">Mọi tình trạng</option>{['SanSang', 'DangChoThue', 'DangBaoTri', 'NgungKhaiThac'].map(s => <option value={s} key={s}>{statusNames[s]}</option>)}</select>
      <select className={`${inputClass} sm:w-44`} aria-label="Lọc loại xe" value={type} onChange={e => setType(e.target.value)}><option value="">Mọi loại xe</option>{typeOptions.map(t => <option value={t.value} key={t.value}>{t.label}</option>)}</select>
      <label className="self-center text-sm"><input type="checkbox" checked={archived} onChange={e => setArchived(e.target.checked)} /> Đã lưu trữ</label>
      <button className={button} onClick={() => { setCreating(true); setDetail(undefined); setEditor({ status: 'SanSang', year: new Date().getFullYear(), seats: 5 }) }}>Thêm xe</button>
      <button className={button} disabled={busy || records.loading || !!records.error} onClick={() => void act(() => download(`fleet/export?${params}`, 'Danh-sach-xe.xlsx'))}>Xuất Excel</button>
    </div>
    <LoadState loading={records.loading || types.loading} error={records.error || types.error} /><p role="status" className="mb-3 text-sm">{message}</p>
    <DataTable headers={['Mã xe', 'Tên xe', 'Biển số', 'Loại xe', 'Tình trạng', 'Thao tác']} rows={rows.map(v => [v.id, v.name, v.plate, types.data?.find(t => t.id === v.typeId)?.name || v.typeId, statusNames[v.status],
      <div className="flex flex-wrap gap-3"><button className="underline" disabled={busy} onClick={() => void open(v.id, false)}>Chi tiết</button>
        {!v.archived && <><button className="underline" disabled={busy || !['SanSang', 'NgungKhaiThac'].includes(v.status)} onClick={() => void open(v.id, true)}>Sửa</button>
          <button className="text-red-700 underline" disabled={busy} onClick={() => { if (window.confirm(`Xóa xe ${v.plate}? Xe có lịch sử sẽ được lưu trữ.`)) void act(async () => { const result = await request<{ archived: boolean }>(`fleet/${encodeURIComponent(v.id)}`, 'DELETE'); records.reload(); setDetail(undefined); setEditor(undefined); setMessage(result.archived ? 'Đã lưu trữ hồ sơ xe.' : 'Đã xóa xe.') }) }}>Xóa</button></>}
      </div>])} />
    {oldParking && <p className="mt-4 text-sm text-amber-700">Vị trí đỗ cũ: {oldParking}. Chọn lại bãi trước khi lưu hồ sơ.</p>}
    {editor && <RecordForm key={creating ? 'new' : String(editor.id)} title={creating ? 'Thêm xe' : 'Cập nhật xe'} fields={fields} values={editor} cancel={() => setEditor(undefined)} save={async data => {
      const body = { ...data, images: String(data.images || '').split('\n').map(s => s.trim()).filter(Boolean) }
      await request(creating ? 'fleet' : `fleet/${encodeURIComponent(String(data.id))}`, creating ? 'POST' : 'PUT', body)
      setEditor(undefined); records.reload(); setMessage('Đã lưu hồ sơ xe.')
    }} />}
    {detail && <section className="mt-5 rounded-xl border bg-white p-5"><div className="flex justify-between"><h2 className="font-semibold">{detail.vehicle.name} — {detail.vehicle.plate}</h2><button onClick={() => setDetail(undefined)}>Đóng</button></div>
      <dl className="my-4 grid gap-3 sm:grid-cols-3">{[
        ['Hãng xe', detail.vehicle.brand], ['Màu', detail.vehicle.color], ['Năm sản xuất', detail.vehicle.year], ['Số chỗ', detail.vehicle.seats], ['Hộp số', detail.vehicle.transmission], ['Vị trí đỗ', detail.vehicle.parking || '—'],
      ].map(([label, value]) => <div key={label}><dt className="text-sm text-zinc-500">{label}</dt><dd>{value}</dd></div>)}</dl>
      <div className="grid gap-3 sm:grid-cols-3">{detail.images.map((image, i) => <img key={i} src={image.url} alt={`${detail.vehicle.name} - ảnh ${i + 1}`} loading="lazy" className="h-40 w-full rounded-lg object-cover" />)}</div>
    </section>}
  </div>
}
