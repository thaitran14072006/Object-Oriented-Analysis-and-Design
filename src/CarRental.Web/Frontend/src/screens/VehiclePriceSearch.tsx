import {useState} from 'react'
import {api,EntryForm,money} from './billingApi'
import {Table} from '../components'
export function VehiclePriceSearch(){
 const [rows,setRows]=useState<any[]>([])
 return <section><EntryForm title="Tìm xe theo khoảng đơn giá" fields={[{key:'start',label:'Nhận xe',type:'datetime-local'},{key:'end',label:'Trả xe',type:'datetime-local'},{key:'min',label:'Giá từ',type:'number'},{key:'max',label:'Giá đến',type:'number'},{key:'unit',label:'Đơn vị',options:['NGAY','GIO']},{key:'form',label:'Hình thức',options:['NGAN_HAN','DAI_HAN']},{key:'season',label:'Mùa vụ (MAC_DINH nếu thường)'}]} submit={async v=>{setRows(await api('billing/vehicles?'+new URLSearchParams(v)))}}/><Table headers={['Mã xe','Xe','Biển số','Mức giá','Đơn giá','Tiền thuê dự kiến']} rows={rows.map(x=>[x.vehicleId,x.name,x.plate,x.rateId,money(x.unitPrice)+' / '+x.unit,money(x.rent)])}/><p className="text-sm my-2">Kết quả đã loại xe trùng lịch thuê hoặc bảo trì. Khi tạo đơn, hệ thống kiểm tra lại lịch.</p></section>
}
