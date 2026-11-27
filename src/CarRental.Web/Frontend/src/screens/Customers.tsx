import { useEffect, useState, type FormEvent } from 'react'
import { api, ApiError, errorText } from '../api'
import { Action, Field, fieldClass, Notice, Panel } from '../forms'
import { Badge, PageHeader, SearchBar, Table } from '../components'

interface Contact { name: string; phone: string; cccd: string; address: string; groupId: string }
interface License { number: string; class: string; expires: string; verification: string }
interface Customer {
  id: string; name: string; phone: string; cccd: string; address: string; groupId: string | null;
  groupName: string | null; licenseNumber: string; licenseClass: string; licenseExpires: string;
  verification: string | null; rating: number | null
}
interface Directory { rows: Customer[]; hasNext: boolean; today: string; maxRating: number }
interface Order { orderId: string; vehicleId: string; pickUp: string; return: string; status: string }
interface History { available: boolean; rows: Order[] }
interface Group { id: string; name: string }
type View = 'contact' | 'license' | 'rating' | 'history' | 'add' | 'group' | null

const emptyContact: Contact = { name: '', phone: '', cccd: '', address: '', groupId: '' }
const emptyLicense: License = { number: '', class: '', expires: '', verification: '' }
const formatDate = (date: string) => date.slice(0, 10).split('-').reverse().join('/')
const statuses: Record<string, string> = { ChoXacNhan: 'Chờ xác nhận', DaXacNhan: 'Đã xác nhận', DaTuChoi: 'Đã từ chối',
  DaHuy: 'Đã hủy', DangThue: 'Đang thuê', QuaHan: 'Quá hạn', DaTraXe: 'Đã trả xe' }

export function Customers({ permissions }: { permissions: string[] }) {
  const canEdit = permissions.includes('Customers.Edit')
  const canRate = permissions.includes('Customers.Rate')
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)
  const [revision, setRevision] = useState(0)
  const [directory, setDirectory] = useState<Directory>()
  const [groups, setGroups] = useState<Group[]>([])
  const [view, setView] = useState<View>(null)
  const [selected, setSelected] = useState<Customer>()
  const [contact, setContact] = useState(emptyContact)
  const [license, setLicense] = useState(emptyLicense)
  const [group, setGroup] = useState<Group>({ id: '', name: '' })
  const [rating, setRating] = useState({ rating: 3, comment: '' })
  const [history, setHistory] = useState<History>()
  const [duplicate, setDuplicate] = useState<string>()
  const [sharedPhone, setSharedPhone] = useState(false)
  const [allowSharedPhone, setAllowSharedPhone] = useState(false)
  const [listError, setListError] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const abort = new AbortController()
    const timer = window.setTimeout(() => {
      setListError('')
      const parameters = new URLSearchParams({ q: query, page: String(page) })
      api<Directory>('/api/customers?' + parameters, 'GET', undefined, abort.signal).then(setDirectory,
        failure => { if (!abort.signal.aborted) setListError(errorText(failure)) })
    }, query ? 200 : 0)
    return () => { window.clearTimeout(timer); abort.abort() }
  }, [query, page, revision])

  useEffect(() => {
    const abort = new AbortController()
    api<Group[]>('/api/customers/groups', 'GET', undefined, abort.signal).then(setGroups,
      failure => { if (!abort.signal.aborted) setListError(errorText(failure)) })
    return () => abort.abort()
  }, [revision])

  function choose(next: View, row?: Customer) {
    setError('')
    setMessage('')
    setDuplicate(undefined)
    setAllowSharedPhone(false)
    setSharedPhone(false)
    setView(next)
    setSelected(row)
    if (next === 'add') { setContact(emptyContact); setLicense(emptyLicense) }
    if (next === 'group') setGroup({ id: '', name: '' })
    if (!row) return
    setContact({ name: row.name, phone: row.phone, cccd: row.cccd, address: row.address, groupId: row.groupId ?? '' })
    setLicense({ number: row.licenseNumber, class: row.licenseClass, expires: row.licenseExpires,
      verification: row.verification ?? '' })
    setRating({ rating: row.rating ?? Math.min(3, directory?.maxRating ?? 5), comment: '' })
    if (next === 'history') {
      setHistory(undefined)
      const id = row.id
      void run(async () => setHistory(await api<History>('/api/customers/' + encodeURIComponent(id) + '/history')))
    }
  }

  async function run(work: () => Promise<void>) {
    setBusy(true); setError('')
    try { await work() }
    catch (failure) {
      setError(errorText(failure))
      if (failure instanceof ApiError) {
        setDuplicate(failure.details?.existingId)
        setSharedPhone(failure.details?.code === 'shared_phone')
      }
    } finally { setBusy(false) }
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    void run(async () => {
      const target = '/api/customers/' + encodeURIComponent(selected?.id ?? '')
      if (view === 'add') {
        await api('/api/customers', 'POST', { contact, license, confirmSharedPhone: allowSharedPhone })
        setQuery('')
        setPage(1)
      } else if (view === 'group') await api('/api/customers/groups', 'PUT', group)
      else if (view === 'contact') await api(target + '/profile', 'PUT', contact)
      else if (view === 'license') await api(target + '/license', 'PUT', license)
      else if (view === 'rating') await api(target + '/rating', 'PUT', rating)
      setRevision(value => value + 1)
      if (view === 'add') setMessage('Đã thêm hồ sơ khách hàng.')
      else if (view === 'rating') setMessage('Đã ghi nhận đánh giá.')
      else setMessage('Đã lưu thay đổi.')
      setView(null)
    })
  }

  const labels: Record<Exclude<View, null>, string> = {
    add: 'Thêm khách hàng', group: 'Quản lý nhóm khách', contact: 'Chỉnh sửa hồ sơ',
    license: 'Cập nhật giấy tờ', rating: 'Đánh giá tín nhiệm', history: 'Lịch sử thuê xe',
  }

  return <div className="space-y-4">
    <PageHeader title="Quản lý khách hàng" desc="Hồ sơ, giấy phép lái xe, lịch sử thuê và tín nhiệm" />
    <Notice error={listError} message={message} />
    <div className="flex flex-wrap gap-2">
      {canEdit && <>
        <Action type="button" disabled={busy} onClick={() => choose('add')}>Thêm khách hàng</Action>
        <Action secondary type="button" disabled={busy} onClick={() => choose('group')}>Quản lý nhóm khách</Action>
      </>}
    </div>
    {view && <Panel title={labels[view] + (selected ? ' · ' + selected.name : '')}>
      <Notice error={error} />
      {view === 'history' ? <>
        {history ? history.available ? <Table headers={['Mã đơn', 'Mã xe', 'Ngày nhận', 'Ngày trả', 'Trạng thái']}
          rows={history.rows.map(order => [order.orderId, order.vehicleId, formatDate(order.pickUp),
            formatDate(order.return), statuses[order.status] ?? order.status])} />
          : <p className="text-sm text-zinc-600">Lịch sử thuê sẽ hiển thị sau khi mô-đun Đặt xe có dữ liệu.</p>
          : !error && <p role="status">Đang tải lịch sử…</p>}
        <Action secondary type="button" disabled={busy} onClick={() => choose(null)}>Đóng</Action>
      </> : <form className="grid gap-3 sm:grid-cols-2" onSubmit={submit}>
        {(view === 'add' || view === 'contact') && <>
          <Field label="Họ và tên"><input required maxLength={100} className={fieldClass} value={contact.name}
            onChange={event => setContact({ ...contact, name: event.target.value })} /></Field>
          <Field label="Số điện thoại"><input required type="tel" maxLength={16} className={fieldClass} value={contact.phone}
            onChange={event => { setContact({ ...contact, phone: event.target.value }); setSharedPhone(false); setAllowSharedPhone(false) }} /></Field>
          <Field label="CCCD"><input required inputMode="numeric" maxLength={12} pattern="[0-9]{12}" className={fieldClass}
            value={contact.cccd} onChange={event => setContact({ ...contact, cccd: event.target.value })} /></Field>
          <Field label="Địa chỉ"><input required maxLength={255} className={fieldClass} value={contact.address}
            onChange={event => setContact({ ...contact, address: event.target.value })} /></Field>
          <Field label="Nhóm khách"><select className={fieldClass} value={contact.groupId}
            onChange={event => setContact({ ...contact, groupId: event.target.value })}>
            <option value="">Chưa phân nhóm</option>
            {groups.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select></Field>
        </>}
        {(view === 'add' || view === 'license') && <>
          <Field label="Số GPLX"><input required maxLength={30} className={fieldClass} value={license.number}
            onChange={event => setLicense({ ...license, number: event.target.value })} /></Field>
          <Field label="Hạng GPLX"><input required maxLength={20} className={fieldClass} value={license.class}
            onChange={event => setLicense({ ...license, class: event.target.value })} /></Field>
          <Field label="Ngày hết hạn GPLX"><input required type="date" className={fieldClass} value={license.expires}
            onChange={event => setLicense({ ...license, expires: event.target.value })} /></Field>
          <Field label="Kết quả đối chiếu giấy tờ gốc"><input maxLength={500} className={fieldClass} value={license.verification}
            onChange={event => setLicense({ ...license, verification: event.target.value })} /></Field>
          {license.expires && directory && license.expires < directory.today &&
            <p className="text-sm text-amber-800 sm:col-span-2">Giấy phép đã hết hạn. Bạn vẫn có thể lưu hồ sơ.</p>}
        </>}
        {view === 'rating' && <>
          <p className="text-sm sm:col-span-2">Mức hiện tại <strong>{selected?.rating ?? 'Chưa đánh giá'}</strong></p>
          <Field label="Mức tín nhiệm"><select className={fieldClass} value={rating.rating}
            onChange={event => setRating({ ...rating, rating: Number(event.target.value) })}>
            {Array.from({ length: directory?.maxRating ?? 5 }, (_, index) => index + 1)
              .map(level => <option key={level}>{level}</option>)}
          </select></Field>
          <Field label="Nhận xét"><input maxLength={1000} className={fieldClass} value={rating.comment}
            required={selected?.rating != null && rating.rating < selected.rating}
            onChange={event => setRating({ ...rating, comment: event.target.value })} /></Field>
        </>}
        {view === 'group' && <>
          <Field label="Nhóm đã có"><select className={fieldClass} value={groups.some(item => item.id === group.id) ? group.id : ''}
            onChange={event => setGroup(groups.find(item => item.id === event.target.value) ?? { id: '', name: '' })}>
            <option value="">Thêm nhóm mới</option>
            {groups.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select></Field>
          <Field label="Mã nhóm"><input required maxLength={20} className={fieldClass} value={group.id}
            onChange={event => setGroup({ ...group, id: event.target.value })} /></Field>
          <Field label="Tên nhóm"><input required maxLength={80} className={fieldClass} value={group.name}
            onChange={event => setGroup({ ...group, name: event.target.value })} /></Field>
        </>}
        {sharedPhone && view === 'add' && <label className="flex items-center gap-2 text-sm sm:col-span-2">
          <input type="checkbox" checked={allowSharedPhone} onChange={event => setAllowSharedPhone(event.target.checked)} />
          Tôi xác nhận đây là khách hàng khác dùng cùng số điện thoại.
        </label>}
        <div className="flex flex-wrap gap-2 sm:col-span-2">
          <Action type="submit" disabled={busy || (sharedPhone && view === 'add' && !allowSharedPhone)}>Lưu</Action>
          <Action secondary type="button" disabled={busy} onClick={() => choose(null)}>Hủy</Action>
          {duplicate && <Action secondary type="button" disabled={busy} onClick={() => void run(async () => {
            const row = await api<Customer>('/api/customers/' + encodeURIComponent(duplicate))
            choose('contact', row)
          })}>Mở hồ sơ đã có</Action>}
        </div>
      </form>}
    </Panel>}
    <SearchBar placeholder="Tìm theo tên, số điện thoại, CCCD hoặc GPLX" query={query} onChange={value => { setQuery(value); setPage(1) }} />
    {directory ? <>
      <Table headers={['Mã KH', 'Họ và tên', 'Điện thoại', 'CCCD', 'GPLX', 'Nhóm', 'Tín nhiệm', 'Thao tác']}
        rows={directory.rows.map(row => [row.id, row.name, row.phone, row.cccd,
          <span>{row.licenseNumber} · {row.licenseClass} {row.licenseExpires < directory.today &&
            <Badge label="Hết hạn" color="red" />}</span>, row.groupName ?? '—', row.rating ?? '—',
          <div className="flex flex-wrap gap-1.5">
            <Action secondary disabled={busy} onClick={() => choose('history', row)}>Lịch sử</Action>
            {canEdit && <>
              <Action secondary disabled={busy} onClick={() => choose('contact', row)}>Sửa</Action>
              <Action secondary disabled={busy} onClick={() => choose('license', row)}>Giấy tờ</Action>
            </>}
            {canRate && <Action secondary disabled={busy} onClick={() => choose('rating', row)}>Tín nhiệm</Action>}
          </div>])} />
      <div className="flex items-center gap-3 text-sm">
        <Action secondary disabled={page === 1} onClick={() => setPage(page - 1)}>Trang trước</Action>
        <span>Trang {page}</span>
        <Action secondary disabled={!directory.hasNext} onClick={() => setPage(page + 1)}>Trang sau</Action>
      </div>
    </> : !listError && <p role="status">Đang tải khách hàng…</p>}
  </div>
}
