import {BillingCatalog} from './BillingCatalog'
import {BillingWorkspace} from './BillingWorkspace'
import { useState } from 'react'
import { PageHeader, Table } from '../components'
import { api, EntryForm, useRecords, money, type Field } from './billingApi'
const f = (key:string,label:string,type?:string):Field=>({key,label,type})
const feeFields: Field[] = [1,2,3].flatMap(n=>[{key:`fee${n}`,label:`Mã phụ phí ${n}`,optional:true},{key:`qty${n}`,label:`Số lượng hoặc giờ trễ ${n}`,type:'number',optional:true}])
const feeValues = (v: any) => [1,2,3].filter(n=>v[`fee${n}`]).map(n=>({id:v[`fee${n}`],quantity:v[`qty${n}`]}))
export function Payments() {
 const rates=useRecords('billing/rates'), fees=useRecords('billing/surcharges'), invoices=useRecords('billing/invoices'), payments=useRecords('billing/payments'), complaints=useRecords('billing/complaints')
 const [tab,setTab]=useState('Hóa đơn')
 const reload=async()=>{await Promise.all([rates.reload(),fees.reload(),invoices.reload(),payments.reload(),complaints.reload()])}
 async function write(path:string,body:unknown,method='POST'){await api(path,method,body);await reload()}
 return <div><PageHeader title="Giá thuê và thanh toán" desc="Quản lý bảng giá, hóa đơn, khoản thu và phản ánh" /><div className="flex gap-2 flex-wrap">{['Hóa đơn','Bảng giá','Phụ phí','Khoản thu','Phản ánh'].map(t=><button key={t} onClick={()=>setTab(t)} className={`px-3 py-2 rounded ${tab===t?'bg-teal-700 text-white':'bg-white'}`}>{t}</button>)}</div><p role="alert" className="text-red-700 my-3">{rates.error||invoices.error}</p>
 {tab==='Bảng giá'&&<BillingCatalog kind='rates'/>}
 {tab==='Phụ phí'&&<BillingCatalog kind='surcharges'/>}
 {tab==='Hóa đơn'&&<BillingWorkspace/>}
 {tab==='Khoản thu'&&<><Table headers={['Mã','Đơn','Loại','Số tiền','Phương thức','Thời điểm']} rows={payments.rows.map(p=>[p.id,p.booking,p.kind,money(p.amount),p.method,p.created])}/><EntryForm title="Ghi đặt cọc hoặc thanh toán" fields={[f('id','Mã hóa đơn'),f('version','Phiên bản hóa đơn','number'),f('amount','Số tiền','number'),{key:'kind',label:'Loại khoản',options:['DAT_COC','THANH_TOAN']},{key:'method',label:'Phương thức',options:['TIEN_MAT','CHUYEN_KHOAN','KHAC']},f('requestId','Mã giao dịch duy nhất')]} submit={v=>write(`billing/invoices/${v.id}/payments`,v)}/></>}
 {tab==='Phản ánh'&&<><Table headers={['Mã','Hóa đơn','Khoản phí','Nội dung','Trạng thái','Giải quyết']} rows={complaints.rows.map(c=>[String(c.id),c.invoiceId,c.item,c.content,c.status,c.resolution||'—'])}/><EntryForm title="Tiếp nhận phản ánh" fields={[f('invoiceId','Mã hóa đơn'),{key:'channel',label:'Kênh',options:['TrucTiep','DienThoai']},f('item','Khoản bị phản ánh'),f('content','Nội dung')]} submit={v=>write('billing/complaints',v)}/><EntryForm title="Giải quyết phản ánh và điều chỉnh chi phí" fields={[f('id','Mã phản ánh'),f('resolution','Nội dung giải quyết'),f('version','Phiên bản hóa đơn','number'),f('start','Nhận dự kiến hiện tại','datetime-local'),f('end','Trả dự kiến hiện tại','datetime-local'),...feeFields]} submit={v=>write(`billing/complaints/${v.id}/resolve`,{resolution:v.resolution,adjustment:{version:v.version,start:v.start,end:v.end,fees:feeValues(v)}})}/><EntryForm title="Giải quyết và giữ nguyên hóa đơn" fields={[f('id','Mã phản ánh'),f('resolution','Nội dung giải quyết')]} submit={v=>write(`billing/complaints/${v.id}/resolve`,v)}/></>}
 </div>
}
