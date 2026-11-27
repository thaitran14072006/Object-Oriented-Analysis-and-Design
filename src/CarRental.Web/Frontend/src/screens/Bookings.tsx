import {useState} from 'react'
import {PageHeader,Table} from '../components'
import {api,EntryForm,useRecords} from './billingApi'
import {Reviews} from './Reviews'
export function Bookings(){
 const b=useRecords('bookings'),[q,setQ]=useState(''),[msg,setMsg]=useState('')
 async function state(r:any,action:string){try{await api(`bookings/${r.id}/state`,'POST',{version:r.version,action});await b.reload();setMsg('Đã cập nhật')}catch(e){setMsg((e as Error).message)}}
 const actions:Record<string,string[]>={Pending:['confirm','reject','cancel'],Confirmed:['handover','cancel'],HandedOver:['return']}
 const names:Record<string,string>={confirm:'Xác nhận',reject:'Từ chối',cancel:'Hủy',handover:'Giao xe',return:'Nhận trả'}
 return <div><PageHeader title="Quản lý đặt xe & thuê xe" desc="Dữ liệu đơn thuê từ SQL Server"/><p role="alert">{b.error || msg}</p><input className="border rounded p-2 mb-3" placeholder="Tìm mã đơn, khách, xe" value={q} onChange={e=>setQ(e.target.value)}/><Table headers={['Mã đơn','Khách','Xe','Nhận','Trả','Trạng thái','Thao tác']} rows={b.rows.filter(r=>[r.id,r.customer,r.vehicle].join(' ').toLowerCase().includes(q.toLowerCase())).map(r=>[r.id,r.customer,r.vehicle,r.start,r.end,r.status==='HandedOver'&&new Date(r.end).getTime()<Date.now()?'Quá hạn':r.status,<div className="flex gap-2">{(actions[r.status]||[]).map(a=><button key={a} className="underline" onClick={()=>void state(r,a)}>{names[a]}</button>)}</div>])}/><EntryForm title="Tạo đơn thuê" fields={[{key:'vehicle',label:'Mã xe'},{key:'customer',label:'Mã khách'},{key:'rateId',label:'Mã mức giá'},{key:'start',label:'Nhận dự kiến',type:'datetime-local'},{key:'end',label:'Trả dự kiến',type:'datetime-local'},{key:'source',label:'Nguồn',options:['TrucTiep','DienThoai','LienKet']}]} submit={async v=>{await api('bookings','POST',v);await b.reload()}}/><Reviews/></div>
}
