import { useEffect, useState, type FormEvent } from 'react'
export async function api(path: string, method = 'GET', body?: unknown) {
 const headers: Record<string,string> = { 'Content-Type': 'application/json' }
 if(method!=='GET') { const r=await fetch('/api/identity/csrf',{cache:'no-store'});if(!r.ok)throw Error('Không lấy được mã xác thực yêu cầu.');headers['X-CSRF-TOKEN']=(await r.json()).token }
 const r=await fetch('/api/'+path,{method,headers,cache:'no-store',body:body===undefined?undefined:JSON.stringify(body)})
 const data=await r.json().catch(()=>({}))
 if(r.status===401){window.dispatchEvent(new Event('rentalpro:session-ended'));throw Error('Phiên đăng nhập đã hết hạn.')}
 if(!r.ok)throw Error(data.error||data.message||data.title||'Không thể thực hiện yêu cầu.')
 return data
}
export function useRecords(path: string) {
 const [rows, setRows] = useState<any[]>([]), [error, setError] = useState('')
 async function reload() { try { setRows(await api(path)); setError('') } catch (e) { setError((e as Error).message) } }
 useEffect(() => { void reload() }, [path])
 return { rows, error, reload }
}
export type Field = { key: string; label: string; type?: string; options?: (string | { value: string; label: string })[]; optional?: boolean }
export function EntryForm({ title, fields, submit }: { title: string; fields: Field[]; submit: (values: any) => Promise<unknown> }) {
 const [message, setMessage] = useState(''), [busy, setBusy] = useState(false)
 async function save(e: FormEvent<HTMLFormElement>) { e.preventDefault(); const form = e.currentTarget; const data = Object.fromEntries(new FormData(form)); for (const f of fields) { if (f.type === 'number') (data as any)[f.key] = data[f.key] === '' && f.optional ? null : Number(data[f.key]); if (f.type === 'datetime-local' && data[f.key]) data[f.key] = new Date(String(data[f.key])).toISOString() }
 setBusy(true); try { await submit(data); setMessage('Đã lưu thành công.'); form.reset() } catch(e) { setMessage((e as Error).message) } finally {setBusy(false)} }
 return <form onSubmit={save} className="bg-white border rounded-xl p-5 my-4"><h2 className="font-semibold mb-3">{title}</h2><div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">{fields.map(f=><label className="text-sm" key={f.key}>{f.label}{f.options?<select name={f.key} className="border rounded p-2 block w-full">{f.options.map(o=><option key={typeof o==='string'?o:o.value} value={typeof o==='string'?o:o.value}>{typeof o==='string'?o:o.label}</option>)}</select>:<input name={f.key} type={f.type||'text'} required={!f.optional} step={f.type==='number'?'0.01':undefined} className="border rounded p-2 block w-full" />}</label>)}</div><button disabled={busy} className="bg-teal-700 text-white rounded px-4 py-2 mt-3">{busy?'Đang lưu…':'Lưu'}</button><p role="status" className="mt-2 text-sm">{message}</p></form>
}
export const money = (v: number) => new Intl.NumberFormat('vi-VN', {style:'currency',currency:'VND'}).format(v)
