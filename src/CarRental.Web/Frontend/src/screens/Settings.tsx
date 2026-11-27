import { useEffect, useState, type FormEvent } from 'react'
import { api, errorText, openExternal, type Admin, type Appearance, type OtpProof } from '../api'
import { Action, Field, fieldClass, Notice, Panel } from '../forms'
import { PageHeader } from '../components'

interface Profile extends Admin {
  email: string
  phone: string
  address: string | null
  role: string
  twoFactor: boolean
  channel: string | null
}

interface SettingsData {
  profile: Profile
  appearance: Appearance | null
  providers: string[]
}

interface Rights {
  roles: { id: string; name: string; full: boolean }[]
  accounts: { id: string; name: string; role: string }[]
  permissions: string[]
  roleGrants: { role: string; permission: string }[]
  personalGrants: { account: string; permission: string; allowed: boolean }[]
  version: string
}

const defaultAppearance: Appearance = { color: 'teal', font: 'system', layout: 'standard', size: 100 }
const otpFields = { inputMode: 'numeric', pattern: '[0-9]{6}', maxLength: 6, autoComplete: 'one-time-code' } as const

export function Settings({ admin, onProfile }: { admin: Admin; onProfile: (admin: Admin, appearance: Appearance | null) => void }) {
  const personal = admin.permissions.includes('Settings.Personal')
  const security = admin.permissions.includes('Admin.Security')
  const [data, setData] = useState<SettingsData | null>(null)
  const [profile, setProfile] = useState({ name: '', email: '', phone: '', address: '' })
  const [emailProof, setEmailProof] = useState<OtpProof>()
  const [phoneProof, setPhoneProof] = useState<OtpProof>()
  const [password, setPassword] = useState({ current: '', newPassword: '', confirmation: '' })
  const [channel, setChannel] = useState('EMAIL')
  const [twoFactor, setTwoFactor] = useState<OtpProof>()
  const [appearance, setAppearance] = useState(defaultAppearance)
  const [google, setGoogle] = useState(false)
  const [linkReady, setLinkReady] = useState(false)
  const [rights, setRights] = useState<Rights>()
  const [grant, setGrant] = useState({ targetType: 'role', targetId: '', permission: '', allowed: 'inherit' })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  async function loadPersonal() {
    const settings = await api<SettingsData>('/api/settings')
    setData(settings)
    const { name, email, phone, address } = settings.profile
    setProfile({ name, email, phone, address: address ?? '' })
    setAppearance(settings.appearance ?? defaultAppearance)
    setChannel(settings.profile.channel ?? 'EMAIL')
    onProfile(settings.profile, settings.appearance)
  }

  async function loadRights() {
    setRights(await api<Rights>('/api/settings/permissions'))
  }

  useEffect(() => {
    const requests: Promise<unknown>[] = []
    if (personal) {
      requests.push(loadPersonal())
      requests.push(api<{ google: boolean }>('/api/identity/options').then(result => setGoogle(result.google)))
      if (new URLSearchParams(location.search).get('link') === 'ready') {
        requests.push(api('/api/settings/external').then(() => setLinkReady(true)))
      }
    }
    if (security) requests.push(loadRights())
    Promise.all(requests).catch(failure => setError(errorText(failure)))
  }, [personal, security])

  const emailChanged = data != null && profile.email.trim() !== data.profile.email
  const phoneChanged = data != null && profile.phone.trim() !== data.profile.phone

  async function run(work: () => Promise<void>, done = '') {
    setBusy(true)
    setError('')
    setMessage('')
    try { await work(); setMessage(done) }
    catch (failure) { setError(errorText(failure)) }
    finally { setBusy(false) }
  }

  function submit(event: FormEvent, work: () => Promise<void>, done: string) {
    event.preventDefault()
    void run(work, done)
  }

  function sendCode(route: string, channel: string, save: (proof: OtpProof) => void, destination?: string) {
    void run(async () => {
      const result = await api<{ ticket: string }>(route, 'POST', { channel, destination })
      save({ ticket: result.ticket, code: '' })
    }, 'Đã gửi mã xác thực.')
  }

  function clearLink() {
    setLinkReady(false)
    history.replaceState(null, '', '/#settings')
  }

  const current = grant.targetType === 'role'
    ? rights?.roles.find(role => role.id === grant.targetId)?.full || rights?.roleGrants.some(
      item => item.role === grant.targetId && item.permission === grant.permission) ? 'Được cấp' : 'Chưa cấp'
    : rights?.personalGrants.find(item => item.account === grant.targetId && item.permission === grant.permission)?.allowed
      === true ? 'Cho phép cá nhân' : rights?.personalGrants.some(
        item => item.account === grant.targetId && item.permission === grant.permission) ? 'Từ chối cá nhân' : 'Theo vai trò'

  return <div className="space-y-4">
    <PageHeader title="Cài đặt" desc="Hồ sơ, bảo mật và giao diện" />
    <Notice error={error} message={message} />
    {personal && !data && !error && <p role="status">Đang tải cài đặt…</p>}
    {personal && data && <div className="grid gap-4 xl:grid-cols-2">
      <Panel title="Thông tin cá nhân">
        <form className="grid gap-3" onSubmit={event => submit(event, async () => {
          await api('/api/settings/profile', 'PUT', { ...profile, emailProof, phoneProof })
          setEmailProof(undefined); setPhoneProof(undefined)
          await loadPersonal()
        }, 'Đã cập nhật thông tin cá nhân.')}>
          <Field label="Họ và tên"><input required maxLength={100} className={fieldClass} value={profile.name}
            onChange={event => setProfile({ ...profile, name: event.target.value })} /></Field>
          <Field label="Địa chỉ"><input maxLength={255} className={fieldClass} value={profile.address}
            onChange={event => setProfile({ ...profile, address: event.target.value })} /></Field>
          <Field label="Email"><input required className={fieldClass} type="email" maxLength={254} value={profile.email}
            onChange={event => { setEmailProof(undefined); setProfile({ ...profile, email: event.target.value }) }} /></Field>
          {emailChanged && <div className="grid gap-2">
            <Action type="button" secondary disabled={busy} onClick={() => sendCode('/api/settings/profile/otp',
              'EMAIL', setEmailProof, profile.email)}>{emailProof ? 'Gửi lại mã email' : 'Gửi mã đến email mới'}</Action>
            {emailProof && <Field label="Mã xác minh email"><input {...otpFields} className={fieldClass} required
              value={emailProof.code} onChange={event => setEmailProof({ ...emailProof, code: event.target.value })} /></Field>}
          </div>}
          <Field label="Số điện thoại"><input required className={fieldClass} type="tel" value={profile.phone}
            onChange={event => { setPhoneProof(undefined); setProfile({ ...profile, phone: event.target.value }) }} /></Field>
          {phoneChanged && <div className="grid gap-2">
            <Action type="button" secondary disabled={busy} onClick={() => sendCode('/api/settings/profile/otp',
              'SMS', setPhoneProof, profile.phone)}>{phoneProof ? 'Gửi lại mã SMS' : 'Gửi mã đến số mới'}</Action>
            {phoneProof && <Field label="Mã xác minh SMS"><input {...otpFields} className={fieldClass} required
              value={phoneProof.code} onChange={event => setPhoneProof({ ...phoneProof, code: event.target.value })} /></Field>}
          </div>}
          <Action type="submit" disabled={busy || (emailChanged && !emailProof) || (phoneChanged && !phoneProof)}>Lưu hồ sơ</Action>
        </form>
      </Panel>
      <div className="grid content-start gap-4">
        <Panel title="Đổi mật khẩu">
          <form className="grid gap-3" onSubmit={event => submit(event, async () => {
            await api('/api/settings/password', 'PUT', password)
            setPassword({ current: '', newPassword: '', confirmation: '' })
          }, 'Đã đổi mật khẩu.')}>
            {(['current', 'newPassword', 'confirmation'] as const).map((key, i) =>
              <Field key={key} label={['Mật khẩu hiện tại', 'Mật khẩu mới', 'Xác nhận mật khẩu mới'][i]}>
                <input required maxLength={128} className={fieldClass} type="password"
                  autoComplete={key === 'current' ? 'current-password' : 'new-password'} value={password[key]}
                  onChange={event => setPassword({ ...password, [key]: event.target.value })} />
              </Field>)}
            <Action type="submit" disabled={busy}>Đổi mật khẩu</Action>
          </form>
        </Panel>
        <Panel title="Xác thực hai yếu tố">
          <p className="text-sm text-zinc-600">{data.profile.twoFactor ? 'Đã bật qua ' + data.profile.channel : 'Chưa bật'}</p>
          <form className="grid gap-3" onSubmit={event => submit(event, async () => {
            await api('/api/settings/2fa', 'PUT', twoFactor)
            setTwoFactor(undefined)
            await loadPersonal()
          }, 'Đã lưu xác thực hai yếu tố.')}>
            <Field label="Kênh nhận mã"><select className={fieldClass} value={channel}
              onChange={event => { setTwoFactor(undefined); setChannel(event.target.value) }}>
              <option value="EMAIL">Email</option><option value="SMS">SMS</option>
            </select></Field>
            <Action type="button" secondary disabled={busy} onClick={() => sendCode('/api/settings/2fa/otp', channel, setTwoFactor)}>
              {twoFactor ? 'Gửi lại mã' : 'Gửi mã xác thực'}</Action>
            {twoFactor && <>
              <Field label="Mã xác thực hai yếu tố"><input {...otpFields} className={fieldClass} required value={twoFactor.code}
                onChange={event => setTwoFactor({ ...twoFactor, code: event.target.value })} /></Field>
              <Action type="submit" disabled={busy}>Xác minh và bật</Action>
            </>}
          </form>
        </Panel>
      </div>
      <Panel title="Tùy chỉnh giao diện">
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={event => submit(event, async () => {
          await api('/api/settings/appearance', 'PUT', appearance)
          await loadPersonal()
        }, 'Đã áp dụng tùy chọn giao diện.')}>
          <Field label="Màu nhấn"><select className={fieldClass} value={appearance.color}
            onChange={event => setAppearance({ ...appearance, color: event.target.value })}>
            <option value="teal">Xanh ngọc</option><option value="blue">Xanh dương</option><option value="purple">Tím</option>
          </select></Field>
          <Field label="Phông chữ"><select className={fieldClass} value={appearance.font}
            onChange={event => setAppearance({ ...appearance, font: event.target.value })}>
            <option value="system">Mặc định</option><option value="serif">Có chân</option><option value="mono">Đơn cách</option>
          </select></Field>
          <Field label="Bố cục"><select className={fieldClass} value={appearance.layout}
            onChange={event => setAppearance({ ...appearance, layout: event.target.value })}>
            <option value="standard">Tiêu chuẩn</option><option value="compact">Gọn</option>
          </select></Field>
          <Field label="Kích thước"><select className={fieldClass} value={appearance.size}
            onChange={event => setAppearance({ ...appearance, size: Number(event.target.value) })}>
            {[90, 100, 110, 120].map(size => <option key={size} value={size}>{size}%</option>)}
          </select></Field>
          <Action type="submit" disabled={busy} className="sm:col-span-2">Áp dụng giao diện</Action>
        </form>
      </Panel>
      <Panel title="Danh tính liên kết">
        {linkReady ? <div className="space-y-3">
          <p className="text-sm">Xác nhận liên kết danh tính Google vừa xác thực với tài khoản RentalPro hiện tại.</p>
          <div className="flex gap-2">
            <Action type="button" disabled={busy} onClick={() => void run(async () => {
              await api('/api/settings/external', 'POST'); clearLink(); await loadPersonal()
            }, 'Đã liên kết Google.')}>Xác nhận liên kết</Action>
            <Action secondary type="button" disabled={busy} onClick={() => void run(async () => {
              await api('/api/settings/external', 'DELETE'); clearLink()
            })}>Hủy</Action>
          </div>
        </div> : data.providers.includes('Google') ? <p className="text-sm">Đã liên kết Google.</p>
          : google ? <Action type="button" disabled={busy} onClick={() => void run(() => openExternal('/settings/external'))}>Liên kết Google</Action>
            : <p className="text-sm text-zinc-600">Google chưa được cấu hình.</p>}
      </Panel>
    </div>}
    {security && rights && <Panel title="Phân quyền">
      <form className="grid items-end gap-3 sm:grid-cols-2 lg:grid-cols-4" onSubmit={event => submit(event, async () => {
        await api('/api/settings/permissions', 'PUT', {
          ...grant, allowed: grant.allowed === 'inherit' ? null : grant.allowed === 'allow', version: rights.version,
        })
        await loadRights()
      }, 'Đã cập nhật quyền.')}>
        <Field label="Áp dụng cho"><select className={fieldClass} value={grant.targetType} onChange={event =>
          setGrant({ ...grant, targetType: event.target.value, targetId: '', allowed: event.target.value === 'role' ? 'allow' : 'inherit' })}>
          <option value="role">Vai trò</option><option value="account">Cá nhân</option>
        </select></Field>
        <Field label={grant.targetType === 'role' ? 'Vai trò' : 'Tài khoản'}>
          <select required className={fieldClass} value={grant.targetId} onChange={event => setGrant({ ...grant, targetId: event.target.value })}>
            <option value="">Chọn</option>
            {(grant.targetType === 'role' ? rights.roles : rights.accounts).map(item =>
              <option key={item.id} value={item.id}>{item.name} ({item.id})</option>)}
          </select>
        </Field>
        <Field label="Quyền"><select required className={fieldClass} value={grant.permission}
          onChange={event => setGrant({ ...grant, permission: event.target.value })}>
          <option value="">Chọn</option>
          {rights.permissions.map(permission => <option key={permission}>{permission}</option>)}
        </select></Field>
        <Field label="Thiết lập"><select className={fieldClass} value={grant.allowed === 'inherit' && grant.targetType === 'role' ? 'deny' : grant.allowed}
          onChange={event => setGrant({ ...grant, allowed: event.target.value })}>
          {grant.targetType === 'account' && <option value="inherit">Theo vai trò</option>}
          <option value="allow">Cho phép</option><option value="deny">Từ chối</option>
        </select></Field>
        {grant.targetId && grant.permission && <p className="text-sm lg:col-span-4">Hiện tại <strong>{current}</strong></p>}
        <Action type="submit" disabled={busy || (grant.targetType === 'role' && rights.roles.some(role => role.id === grant.targetId && role.full))}>Lưu quyền</Action>
        <Action type="button" secondary disabled={busy} onClick={() => void run(loadRights)}>Tải lại quyền</Action>
      </form>
    </Panel>}
  </div>
}
