import { useRef, useState, type ReactNode } from 'react'
import { Icon } from './icons'

export function Badge({ label, color }: { label: string; color: 'green' | 'red' | 'yellow' | 'blue' | 'gray' | 'teal' }) {
  const cls = {
    green: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
    red: 'bg-red-50 text-red-700 border border-red-200',
    yellow: 'bg-amber-50 text-amber-700 border border-amber-200',
    blue: 'bg-sky-50 text-sky-700 border border-sky-200',
    gray: 'bg-zinc-100 text-zinc-600 border border-zinc-200',
    teal: 'bg-teal-50 text-teal-700 border border-teal-200',
  }[color]
  return <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium font-mono ${cls}`}>{label}</span>
}

export function StatCard({ label, value, sub, accent }: { label: string; value: string; sub: string; accent?: boolean }) {
  return (
    <div className={`rounded-xl p-5 border ${accent ? 'bg-[#0F766E] border-transparent text-white' : 'bg-white border-[#E4E4E7]'}`}>
      <p className={`text-xs font-medium tracking-wider uppercase font-mono ${accent ? 'text-teal-200' : 'text-[#71717A]'}`}>{label}</p>
      <p className={`text-3xl font-bold mt-2 font-display ${accent ? 'text-white' : 'text-[#18181B]'}`}>{value}</p>
      <p className={`text-xs mt-1 ${accent ? 'text-teal-200' : 'text-[#71717A]'}`}>{sub}</p>
    </div>
  )
}

function normalizeSearch(text: string) {
  return text.toLowerCase().normalize('NFD').replace(/\p{M}/gu, '').replaceAll('đ', 'd')
}

export function Table({ headers, rows, query = '' }: {
  headers: string[]
  rows: ReactNode[][]
  query?: string
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const [selected, setSelected] = useState<ReactNode[]>()
  const search = normalizeSearch(query.trim())
  const visibleRows = rows.filter(row => !search || row.some(cell =>
    typeof cell === 'string' && normalizeSearch(cell).includes(search)))

  return (
    <>
      <div className="overflow-auto rounded-xl border border-[#E4E4E7] bg-white"
        role="region" aria-label="Bảng dữ liệu" tabIndex={0}>
        <table className="w-full min-w-[660px] text-sm">
          <thead>
            <tr className="border-b border-[#E4E4E7] bg-[#F5F5F3]">
              {headers.map(header => (
                <th key={header} scope="col" className="px-4 py-3 text-left text-xs font-semibold text-[#71717A] uppercase tracking-wider font-mono">
                  {header}
                </th>
              ))}
              <th scope="col" className="relative"><span className="sr-only">Thao tác</span></th>
            </tr>
          </thead>
          <tbody>
            {visibleRows.map(row => (
              <tr key={String(row[0])} className="border-b border-[#F4F4F5] last:border-b-0 hover:bg-[#FAFAFA] transition-colors">
                {row.map((cell, index) => (
                  <td key={headers[index]} className="px-4 py-3 text-[#18181B]">{cell}</td>
                ))}
                <td className="px-4 py-3">
                  <div className="flex gap-1">
                    <button type="button" aria-label={'Xem chi tiết ' + row[0]} title="Xem chi tiết"
                      className="p-1.5 rounded hover:bg-teal-50 text-[#71717A] hover:text-[#0F766E] transition-colors"
                      onClick={() => { setSelected(row); dialog.current?.showModal() }}>
                      <Icon.Eye />
                    </button>
                    <button type="button" disabled aria-label="Chỉnh sửa" title="Cần kết nối nghiệp vụ"
                      className="p-1.5 rounded text-[#71717A]"><Icon.Edit /></button>
                    <button type="button" disabled aria-label="Xóa" title="Cần kết nối nghiệp vụ"
                      className="p-1.5 rounded text-[#71717A]"><Icon.Trash /></button>
                  </div>
                </td>
              </tr>
            ))}
            {visibleRows.length === 0 && (
              <tr><td colSpan={headers.length + 1} className="px-4 py-6 text-center text-[#71717A]">
                Không tìm thấy kết quả.
              </td></tr>
            )}
          </tbody>
        </table>
      </div>
      <dialog ref={dialog} aria-labelledby="record-title"
        className="m-auto w-[calc(100%-2rem)] max-w-lg rounded-xl bg-white p-5 text-[#18181B] shadow-xl sm:p-6">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 id="record-title" className="font-display text-lg font-bold">Chi tiết {selected?.[0]}</h2>
          <form method="dialog">
            <button type="submit" autoFocus className="rounded-lg border border-[#E4E4E7] px-3 py-1.5 text-sm hover:bg-[#F5F5F3]">Đóng</button>
          </form>
        </div>
        <dl className="divide-y divide-[#E4E4E7]">
          {selected?.map((cell, index) => (
            <div key={headers[index]} className="grid grid-cols-2 items-start gap-2 py-2 text-sm">
              <dt className="text-[#71717A]">{headers[index]}</dt>
              <dd className="break-words">{cell}</dd>
            </div>
          ))}
        </dl>
      </dialog>
    </>
  )
}

export function PageHeader({ title, desc, action }: { title: string; desc: string; action?: string }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
      <div>
        <h1 className="text-xl font-bold text-[#18181B] font-display">{title}</h1>
        <p className="text-sm text-[#71717A] mt-0.5">{desc}</p>
      </div>
      {action && (
        <button type="button" disabled title="Cần kết nối nghiệp vụ" className="flex items-center gap-2 px-4 py-2 bg-[#0F766E] text-white rounded-lg text-sm font-medium hover:bg-[#0D9488] transition-colors">
          <Icon.Plus />{action}
        </button>
      )}
    </div>
  )
}

export function SearchBar({ placeholder, query, onChange }: {
  placeholder: string
  query: string
  onChange: (query: string) => void
}) {
  return (
    <div className="relative mb-4">
      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-[#71717A]"><Icon.Search /></span>
      <input
        type="search"
        aria-label={placeholder}
        className="w-full pl-9 pr-4 py-2 border border-[#E4E4E7] rounded-lg text-sm bg-white placeholder-[#A1A1AA] focus:outline-none focus:ring-2 focus:ring-[#0F766E]/30 focus:border-[#0F766E]"
        placeholder={placeholder}
        value={query}
        onChange={event => onChange(event.target.value)}
      />
    </div>
  )
}
