#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][uri]$BaseUrl,
    [Parameter(Mandatory)][pscredential]$Credential
)
$ErrorActionPreference = 'Stop'
if (-not $BaseUrl.IsLoopback) { throw 'Chỉ chạy kiểm thử này trên localhost với dữ liệu demo.' }
$root = $BaseUrl.AbsoluteUri.TrimEnd('/')
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
function Call-Api([string]$Method,[string]$Path,$Data=$null,[int]$Expected=200) {
    $headers = @{}
    if ($Method -ne 'GET') {
        $csrf = Invoke-RestMethod "$root/api/identity/csrf" -WebSession $session
        $headers['X-CSRF-TOKEN'] = $csrf.token
    }
    $options = @{ Uri="$root$Path";Method=$Method;WebSession=$session;Headers=$headers;SkipHttpErrorCheck=$true }
    if ($null -ne $Data) { $options.Body=ConvertTo-Json -Depth 12 -InputObject $Data; $options.ContentType='application/json; charset=utf-8' }
    $r = Invoke-WebRequest @options
    if ([int]$r.StatusCode -ne $Expected) { throw "$Method $Path trả HTTP $($r.StatusCode), cần $Expected. $($r.Content)" }
    if ($r.Content) { return ConvertFrom-Json $r.Content }
}
function Check([bool]$Condition,[string]$Label) { if (-not $Condition) {throw $Label}; Write-Host "PASS $Label" }
$login = Invoke-WebRequest "$root/login" -SkipHttpErrorCheck
Check ($login.StatusCode -eq 200) 'Trang đăng nhập Docker trả 200'
$null=Call-Api GET /api/billing/invoices $null 401
$null=Call-Api POST /api/identity/login @{employeeId=$Credential.UserName;password=$Credential.GetNetworkCredential().Password}
Check ($null -ne (Call-Api GET /api/identity/me)) 'Đăng nhập cookie và phiên SQL trong container'
$tag=[guid]::NewGuid().ToString('N').Substring(0,8).ToUpperInvariant()
$vehicle='NHT_'+$tag
$null=Call-Api POST /api/fleet @{id=$vehicle;typeId='NHU_SEDAN';name='Xe kiểm thử Docker';brand='Toyota';plate="TEST-$tag";color='Trắng';year=2024;seats=5;transmission='Tự động';parking='Bãi demo';status='SanSang';images=@()} 201
$start=[datetime]::UtcNow.Date.AddDays(2).AddHours(2)
$end=$start.AddDays(2)
$rows=Call-Api GET ("/api/billing/vehicles?start=$($start.ToString('O'))&end=$($end.ToString('O'))&min=400000&max=600000")
Check (@($rows | Where-Object vehicleId -eq $vehicle).Count -eq 1) 'Tìm xe theo khoảng giá trên SQL container'
$b=Call-Api POST /api/bookings @{vehicle=$vehicle;customer='NHU_KH01';rateId='NHU_GIA01';start=$start.ToString('O');end=$end.ToString('O');source='TrucTiep';promotionCode='NHU10'}
$payment=@{version=0;amount=300000;kind='DAT_COC';method='TIEN_MAT';requestId="C_$tag"}
$null=Call-Api POST "/api/billing/bookings/$($b.id)/payments" $payment
$quote=@{rateId='NHU_GIA01';start=$start.ToString('O');end=$end.ToString('O');fees=@(@{id='NHU_GIAOXE';quantity=1});promotionCode='NHU10'}
$preview=Call-Api POST /api/billing/invoices/preview @{booking=$b.id;quote=$quote}
Check ($preview.cost.total -eq 950000 -and $preview.paid -eq 300000 -and $preview.remaining -eq 650000) 'Xem trước: tổng 950000, cọc 300000, còn 650000'
$i=Call-Api POST /api/billing/invoices @{booking=$b.id;quote=$quote;expectedTotal=$preview.cost.total}
$null=Call-Api POST "/api/billing/bookings/$($b.id)/payments" $payment
$null=Call-Api POST "/api/rental-schedule/$($b.id)/adjust" @{version=0;invoiceVersion=0;start=$start.ToString('O');end=$end.AddDays(1).ToString('O');reason='Kiểm thử Docker';fees=$null}
$detail=Call-Api GET "/api/billing/invoices/$($i.id)"
Check ($detail.invoice.total -eq 1450000 -and $detail.paid -eq 300000 -and $detail.remaining -eq 1150000) 'Gia hạn cùng giao dịch giữ giảm chốt 100000 và cọc cũ'
$null=Call-Api POST "/api/billing/invoices/$($i.id)/payments" @{version=$detail.invoice.version;amount=1150000;kind='THANH_TOAN';method='CHUYEN_KHOAN';requestId="T_$tag"}
$detail=Call-Api GET "/api/billing/invoices/$($i.id)"
Check ($detail.remaining -eq 0) 'Thu đủ tiền, còn phải thu bằng 0'
$null=Call-Api POST "/api/billing/invoices/$($i.id)/payments" @{version=$detail.invoice.version;amount=1;kind='THANH_TOAN';method='TIEN_MAT';requestId="OVER_$tag"} 409
Check $true 'Chặn thu vượt trên bản Docker'
$complaint=Call-Api POST /api/billing/complaints @{invoiceId=$i.id;channel='TrucTiep';item='Phí giao xe';content='Phản ánh kiểm thử Docker'}
$null=Call-Api POST "/api/billing/complaints/$($complaint.id)/resolve" @{resolution='Đã đối chiếu, giữ nguyên hóa đơn'}
$today=[TimeZoneInfo]::ConvertTimeFromUtc([datetime]::UtcNow,[TimeZoneInfo]::FindSystemTimeZoneById('SE Asia Standard Time')).ToString('yyyy-MM-dd')
$report=Call-Api GET "/api/reports?type=revenue&from=$today&to=$today"
$total=($report.rows | Measure-Object -Property amount -Sum).Sum
Check ($total -ge 1450000) 'Báo cáo doanh thu container đối chiếu được cọc và thanh toán'
$null=Call-Api POST /api/identity/logout $null 204
$null=Call-Api GET /api/billing/invoices $null 401
Check $true 'Đăng xuất thu hồi phiên trên container'
Write-Host "DONE Docker Billing: $($b.id) / $($i.id)"
