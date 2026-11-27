export interface Admin {
  id: string
  name: string
  permissions: string[]
}

export interface Appearance {
  color: string
  font: string
  layout: string
  size: number
}

export interface OtpProof {
  ticket: string
  code: string
}

export class ApiError extends Error {
  constructor(message: string, public status: number, public details?: { code?: string; existingId?: string }) {
    super(message)
  }
}

export function errorText(error: unknown) {
  return error instanceof ApiError ? error.message : 'Không kết nối được với RentalPro. Vui lòng thử lại.'
}

export async function api<T>(url: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
  const headers = new Headers({ Accept: 'application/json' })
  if (method !== 'GET') {
    const { token } = await api<{ token: string }>('/api/identity/csrf')
    headers.set('X-CSRF-TOKEN', token)
  }
  if (body !== undefined) headers.set('Content-Type', 'application/json')
  const response = await fetch(url, {
    method, headers, signal, cache: 'no-store',
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    const result = await response.json().catch(() => null)
    const message = result?.message ?? result?.error ?? (response.status === 400
      ? 'Dữ liệu hoặc phiên gửi yêu cầu không hợp lệ. Hãy kiểm tra và thử lại.'
      : 'Không xử lý được yêu cầu (' + response.status + ').')
    if (response.status === 401 && location.pathname !== '/login')
      window.dispatchEvent(new Event('rentalpro:session-ended'))
    throw new ApiError(message, response.status, result?.details)
  }
  return response.status === 204 ? undefined as T : response.json()
}

export async function openExternal(url: '/identity/external' | '/settings/external') {
  const { token } = await api<{ token: string }>('/api/identity/csrf')
  const form = document.createElement('form')
  form.method = 'POST'
  form.action = url
  const field = document.createElement('input')
  field.type = 'hidden'
  field.name = '__RequestVerificationToken'
  field.value = token
  form.append(field)
  document.body.append(form)
  form.submit()
}
