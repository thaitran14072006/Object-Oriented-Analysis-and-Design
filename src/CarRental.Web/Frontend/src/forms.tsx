import type { ReactNode, ButtonHTMLAttributes } from 'react'

export const fieldClass = 'w-full min-w-0 rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900'
export function Action({ secondary = false, className = '', ...props }:
  ButtonHTMLAttributes<HTMLButtonElement> & { secondary?: boolean }) {
  const color = secondary
    ? 'border border-zinc-300 bg-white text-zinc-700 hover:bg-zinc-100'
    : 'bg-[var(--rental-accent,#0F766E)] text-white hover:brightness-90'
  return <button {...props} className={'rounded-md px-3 py-2 text-sm font-medium ' + color + ' ' + className} />
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="grid gap-1 text-sm font-medium text-zinc-700">{label}{children}</label>
}

export function Notice({ error, message }: { error?: string; message?: string }) {
  if (error) return <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-800">{error}</p>
  if (message) return <p role="status" className="rounded-md bg-teal-50 p-3 text-sm text-teal-900">{message}</p>
  return null
}

export function Panel({ title, children }: { title: string; children: ReactNode }) {
  return <section className="rounded-lg border border-zinc-200 bg-white p-4 space-y-4 sm:p-5">
    <h2 className="text-base font-semibold">{title}</h2>{children}
  </section>
}
