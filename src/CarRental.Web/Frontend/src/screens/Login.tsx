import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api, errorText, openExternal, type Admin } from '../api'
import { Action, Field, fieldClass, Notice } from '../forms'
import { Icon } from '../icons'

type Stage = 'login' | 'otp' | 'recovery' | 'reset'
type LoginReply = { admin?: Admin; ticket?: string }

export function Login() {
  const [stage, setStage] = useState<Stage>('login')
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [code, setCode] = useState('')
  const [ticket, setTicket] = useState('')
  const [google, setGoogle] = useState(false)
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')
  const externalStarted = useRef(false)

  useEffect(() => {
    api<{ google: boolean }>('/api/identity/options').then(result => setGoogle(result.google), () => {})
    if (externalStarted.current) return
    externalStarted.current = true
    const external = new URLSearchParams(location.search).get('external')
    if (external === null) return
    history.replaceState(null, '', '/login')
    if (external === 'ready')
      void execute(async () => finish(await api<LoginReply>('/api/identity/external/finish', 'POST')))
    else
      setError('Phiên xác thực Google không hoàn tất. Hãy thử lại.')
  }, [])

  function finish(reply: LoginReply) {
    if (reply.admin) {
      location.replace('/')
    } else if (reply.ticket) {
      setTicket(reply.ticket)
      setPassword('')
      setStage('otp')
      setStatus('Nhập mã đã gửi đến kênh xác minh của bạn.')
    }
  }

  async function execute(work: () => Promise<void>) {
    setBusy(true)
    setError('')
    setStatus('')
    try { await work() }
    catch (failure) { setError(errorText(failure)) }
    finally { setBusy(false) }
  }

  function go(next: Stage) {
    setStage(next)
    setCode('')
    setPassword('')
    setConfirmation('')
    setError('')
    setStatus('')
    setTicket('')
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    void execute(async () => {
      if (stage === 'login') {
        finish(await api<LoginReply>('/api/identity/login', 'POST', { employeeId: identifier, password }))
      } else if (stage === 'otp') {
        finish(await api<LoginReply>('/api/identity/login/otp', 'POST', { ticket, code }))
      } else if (stage === 'recovery') {
        const result = await api<{ ticket: string; message: string }>('/api/identity/recovery', 'POST', { identifier })
        setTicket(result.ticket)
        setStatus(result.message)
        setStage('reset')
      } else {
        await api('/api/identity/recovery/reset', 'POST', { ticket, code, password, confirmation })
        go('login')
        setStatus('Đã đặt lại mật khẩu. Bạn có thể đăng nhập.')
      }
    })
  }

  function resend() {
    void execute(async () => {
      const reply = await api<{ ticket: string }>('/api/identity/otp/resend', 'POST', { ticket, recovery: stage === 'reset' })
      setTicket(reply.ticket)
      setCode('')
      setStatus(stage === 'reset' ? 'Nếu yêu cầu hợp lệ, bạn sẽ nhận được mã mới.' : 'Đã gửi mã mới.')
    })
  }

  const isVerification = stage === 'otp' || stage === 'reset'
  const title = { login: 'Đăng nhập quản trị', otp: 'Xác thực hai yếu tố', recovery: 'Khôi phục mật khẩu',
    reset: 'Đặt lại mật khẩu' }[stage]
  return <main className="min-h-dvh grid place-items-center px-4 py-8">
    <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 shadow-sm sm:p-8">
      <div className="mb-5 flex items-center gap-3 text-sm font-semibold">
        <span className="grid h-9 w-9 place-items-center rounded-lg bg-[#0F766E] text-white"><Icon.Car /></span>
        RentalPro
      </div>
      <h1 className="mb-5 text-xl font-bold">{title}</h1>
      <form className="grid gap-4" onSubmit={submit}>
        <Notice error={error} message={status} />
        {(stage === 'login' || stage === 'recovery') && <Field label={stage === 'login' ? 'Mã nhân viên' : 'Mã nhân viên hoặc email'}>
          <input className={fieldClass} required maxLength={stage === 'login' ? 20 : 254}
            autoComplete="username" value={identifier} onChange={event => setIdentifier(event.target.value)} />
        </Field>}
        {isVerification && <Field label="Mã OTP">
          <input className={fieldClass} required inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}"
            maxLength={6} value={code} onChange={event => setCode(event.target.value)} />
        </Field>}
        {(stage === 'login' || stage === 'reset') && <Field label={stage === 'login' ? 'Mật khẩu' : 'Mật khẩu mới'}>
          <input className={fieldClass} type="password" autoComplete={stage === 'login' ? 'current-password' : 'new-password'}
            required maxLength={128} value={password} onChange={event => setPassword(event.target.value)} />
        </Field>}
        {stage === 'reset' && <Field label="Xác nhận mật khẩu mới">
          <input className={fieldClass} type="password" autoComplete="new-password" required maxLength={128}
            value={confirmation} onChange={event => setConfirmation(event.target.value)} />
        </Field>}
        <Action type="submit" disabled={busy}>
          {{ login: 'Đăng nhập', otp: 'Xác minh', recovery: 'Gửi mã xác thực', reset: 'Lưu mật khẩu mới' }[stage]}
        </Action>
      </form>
      <div className="mt-4 flex flex-wrap gap-2">
        {stage === 'login' ? <>
          <Action type="button" secondary disabled={busy} onClick={() => go('recovery')}>Quên mật khẩu</Action>
          {google && <Action type="button" secondary disabled={busy}
            onClick={() => void execute(() => openExternal('/identity/external'))}>Đăng nhập với Google</Action>}
        </> : <>
          <Action type="button" secondary disabled={busy} onClick={() => go('login')}>Về đăng nhập</Action>
          {isVerification && <Action type="button" secondary disabled={busy} onClick={resend}>Gửi lại mã</Action>}
        </>}
      </div>
    </div>
  </main>
}
