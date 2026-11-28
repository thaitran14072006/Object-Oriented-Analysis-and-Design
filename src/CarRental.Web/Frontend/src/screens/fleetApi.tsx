import { useEffect, useId, useState, type FormEvent, type ReactNode } from 'react'

export async function request<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
  const headers: Record<string,string> = { 'Content-Type': 'application/json' }; if(method!=='GET'){const r=await fetch('/api/identity/csrf',{signal,cache:'no-store'});if(!r.ok)throw Error('Không lấy được mã xác thực yêu cầu.');headers['X-CSRF-TOKEN']=(await r.json()).token}
  const response = await fetch(`/api/${path}`, { method, signal, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  if (response.status === 401) throw Error('Vui lòng đăng nhập tài khoản quản trị.')
  if (response.status === 403) throw Error('Bạn chưa có quyền thực hiện thao tác này.')
  const data = await response.json().catch(() => ({}))
  if (!response.ok) throw Error(data.error || Object.values(data.errors || {}).flat().join(' ') || 'Không thể tải dữ liệu. Vui lòng thử lại.')
  return data as T
}

export function useResource<T>(path: string) {
  const [data, setData] = useState<T>(), [error, setError] = useState(''), [loading, setLoading] = useState(true), [version, setVersion] = useState(0)
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setData(undefined); setError('')
    void request<T>(path, 'GET', undefined, controller.signal).then(setData).catch(e => {
      if (!controller.signal.aborted) setError((e as Error).message)
    }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [path, version])
  return { data, error, loading, reload: () => setVersion(v => v + 1) }
}

export async function download(path: string, filename: string) {
  const response = await fetch(`/api/${path}`)
  if (!response.ok) {
    const data = await response.json().catch(() => ({}))
    throw Error(response.status === 401 ? 'Vui lòng đăng nhập tài khoản quản trị.' : data.error || 'Không thể xuất tệp.')
  }
  const url = URL.createObjectURL(await response.blob()), anchor = document.createElement('a')
  anchor.href = url; anchor.download = filename; document.body.append(anchor); anchor.click(); anchor.remove(); URL.revokeObjectURL(url)
}

export const button = 'rounded-lg bg-teal-700 px-4 py-2 text-sm text-white hover:bg-teal-600 disabled:opacity-50'
export const inputClass = 'w-full rounded-lg border border-zinc-300 bg-white p-2 text-sm'
export const money = (value: number) => value.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' })
export const dateTime = (value?: string | null) => value ? new Date(/Z$|[+-]\d\d:\d\d$/.test(value) ? value : value + 'Z').toLocaleString('vi-VN') : '—'
export const statusNames: Record<string, string> = {
  SanSang: 'Sẵn sàng', DangChoThue: 'Đang cho thuê', DangBaoTri: 'Đang bảo trì', NgungKhaiThac: 'Ngừng khai thác',
  ChuaThucHien: 'Chưa thực hiện', DangThucHien: 'Đang thực hiện', HoanTat: 'Hoàn tất', DinhKy: 'Định kỳ', SuaChua: 'Sửa chữa', NangCap: 'Nâng cấp',
  TrucTiep: 'Trực tiếp', DienThoai: 'Điện thoại', LienKet: 'Nền tảng liên kết', ThanhCong: 'Thành công', ThatBai: 'Thất bại',
}

export function DataTable({ headers, rows }: { headers: string[]; rows: ReactNode[][] }) {
  return <div className="overflow-x-auto rounded-xl border bg-white"><table className="w-full text-sm">
    <thead className="bg-zinc-100 text-left"><tr>{headers.map((h, i) => <th className="p-3 whitespace-nowrap" key={i}>{h}</th>)}</tr></thead>
    <tbody>{rows.map((row, i) => <tr className="border-t" key={i}>{row.map((cell, j) => <td className="p-3" key={j}>{cell}</td>)}</tr>)}
      {!rows.length && <tr><td colSpan={headers.length} className="p-6 text-center text-zinc-500">Chưa có dữ liệu.</td></tr>}</tbody>
  </table></div>
}

export type Field = { key: string; label: string; type?: string; optional?: boolean; min?: number; max?: number; maxLength?: number; options?: { value: string; label: string }[]; suggestions?: string[]; placeholder?: string; readOnly?: boolean }
export function RecordForm({ title, fields, values = {}, save, cancel }: {
  title: string; fields: Field[]; values?: Record<string, unknown>; save: (values: Record<string, unknown>) => Promise<void>; cancel?: () => void
}) {
  const [error, setError] = useState(''), [busy, setBusy] = useState(false)
  const formId = useId()
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget, data: Record<string, unknown> = Object.fromEntries(new FormData(form))
    for (const field of fields) {
      if (field.type === 'number') data[field.key] = Number(data[field.key])
      if (field.type === 'datetime-local' && data[field.key]) data[field.key] = new Date(String(data[field.key])).toISOString()
      if (field.type === 'checkbox') data[field.key] = data[field.key] === 'on'
      if (field.type === 'boolean') data[field.key] = data[field.key] === 'true'
    }
    setBusy(true); setError('')
    try { await save(data); form.reset() } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }
  return <form className="my-5 rounded-xl border bg-white p-5" onSubmit={submit}>
    <h2 className="mb-4 font-semibold">{title}</h2><fieldset disabled={busy} className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {fields.map(field => <label className="text-sm" key={field.key}>{field.label}
        {field.options ? <select name={field.key} required={!field.optional} className={inputClass} defaultValue={String(values[field.key] ?? '')}>
          <option value="">{field.optional ? 'Chưa chọn' : 'Chọn…'}</option>{field.options.map(o => <option value={o.value} key={o.value}>{o.label}</option>)}</select>
          : field.type === 'textarea' ? <textarea name={field.key} placeholder={field.placeholder} required={!field.optional} maxLength={field.maxLength} rows={3} className={inputClass} defaultValue={String(values[field.key] ?? '')} />
          : <input name={field.key} type={field.type || 'text'} required={!field.optional && field.type !== 'checkbox'} readOnly={field.readOnly}
            list={field.suggestions ? `${formId}-${field.key}` : undefined} placeholder={field.placeholder}
            min={field.min} max={field.max} maxLength={field.maxLength} step={field.type === 'number' ? 'any' : undefined}
            className={field.type === 'checkbox' ? 'ml-3' : inputClass} defaultChecked={field.type === 'checkbox' ? Boolean(values[field.key]) : undefined}
            defaultValue={field.type === 'checkbox' ? undefined : String(values[field.key] ?? '')} />}
        {field.suggestions && <datalist id={`${formId}-${field.key}`}>{[...new Set(field.suggestions)].map(value => <option key={value} value={value} />)}</datalist>}
      </label>)}
    </fieldset><p role="alert" className="mt-3 text-sm text-red-700">{error}</p>
    <div className="mt-3 flex gap-3"><button className={button} disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu'}</button>
      {cancel && <button type="button" onClick={cancel} disabled={busy} className="text-sm underline">Đóng</button>}</div>
  </form>
}

export function LoadState({ loading, error }: { loading: boolean; error: string }) {
  return <p role={error ? 'alert' : 'status'} className={`my-3 text-sm ${error ? 'text-red-700' : 'text-zinc-500'}`}>{loading ? 'Đang tải…' : error}</p>
}
