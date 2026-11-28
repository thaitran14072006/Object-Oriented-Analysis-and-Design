#requires -Version 7.2
[CmdletBinding()]
param(
    [switch]$InitializeAdmin,
    [switch]$DemoData
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Invoke-Compose {
    docker compose @args
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose thất bại (mã $LASTEXITCODE). Xem lỗi phía trên."
    }
}

function Invoke-Sql {
    Invoke-Compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d CarRental @args
}

function Read-AdminPassword {
    Write-Host 'Mật khẩu Admin cần 8–128 ký tự và có chữ hoa, chữ thường, chữ số.'
    while ($true) {
        $securePassword = Read-Host 'Mật khẩu Admin' -AsSecureString
        try {
            $password = [Net.NetworkCredential]::new('', $securePassword).Password
        } finally {
            $securePassword.Dispose()
        }

        $requirements = @()
        if ($password.Length -lt 8 -or $password.Length -gt 128) { $requirements += 'độ dài 8–128 ký tự' }
        if ($password -cnotmatch '\p{Lu}') { $requirements += 'chữ hoa' }
        if ($password -cnotmatch '\p{Ll}') { $requirements += 'chữ thường' }
        if ($password -cnotmatch '\p{Nd}') { $requirements += 'chữ số' }
        if ($requirements.Count -eq 0) {
            return $password
        }

        Write-Warning "Mật khẩu chưa đạt yêu cầu về $($requirements -join ', '). Vui lòng nhập lại."
    }
}

function Initialize-Admin {
    $accounts = Invoke-Sql -h -1 -W -Q 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.TaiKhoan;'
    if (($accounts -join '').Trim() -ne '0') {
        Write-Host 'Cơ sở dữ liệu đã có tài khoản. Dùng tài khoản hiện có để đăng nhập.'
        return
    }

    $adminVariables = [ordered]@{
        BootstrapAdmin__Id = 'ADMIN01'
        BootstrapAdmin__Name = 'Quản trị RentalPro'
        BootstrapAdmin__Email = Read-Host 'Email Admin'
        BootstrapAdmin__Phone = Read-Host 'Số điện thoại Admin'
        BootstrapAdmin__Password = Read-AdminPassword
    }
    $original = @{}
    $options = @('run', '--rm', '--no-deps', '-T')
    try {
        foreach ($key in $adminVariables.Keys) {
            $original[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            [Environment]::SetEnvironmentVariable($key, $adminVariables[$key], 'Process')
            $options += @('-e', $key)
        }
        Invoke-Compose @options web --bootstrap-admin
        Write-Host 'Đã khởi tạo tài khoản ADMIN01.'
    } finally {
        foreach ($key in $original.Keys) {
            if ($null -eq $original[$key]) {
                Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue
            } else {
                [Environment]::SetEnvironmentVariable($key, $original[$key], 'Process')
            }
        }
    }
}

Push-Location -LiteralPath $projectRoot
try {
    $engine = docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0 -or $engine -ne 'linux') {
        throw 'Cần Docker Desktop đang chạy ở chế độ Linux containers và terminal có quyền truy cập Docker Engine.'
    }

    if (-not (Test-Path -LiteralPath '.env')) {
        $bytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(24)
        $sqlPassword = 'Aa9' + [Convert]::ToHexString($bytes)
        New-Item -ItemType File -Path '.env' -Value "MSSQL_SA_PASSWORD=$sqlPassword" | Out-Null
    }

    Invoke-Compose config --quiet
    Invoke-Compose up -d --build web

    if ($InitializeAdmin) { Initialize-Admin }

    if ($DemoData) {
        Invoke-Sql -f 65001 -i '/seeds/02_vehicle_types.sql'
        Invoke-Sql -f 65001 -i '/seeds/nhu-demo.sql'
    }

    $address = (Invoke-Compose port web 8080 | Select-Object -First 1).Trim()
    $loginUrl = "http://$address/login"
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $loginUrl -TimeoutSec 2 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) {
                Invoke-Compose ps -a
                Write-Host "RentalPro đã sẵn sàng tại $loginUrl"
                return
            }
        } catch [System.Net.Http.HttpRequestException], [System.Threading.Tasks.TaskCanceledException] {
            # Kestrel may still be starting after Compose returns.
        }
        Start-Sleep -Seconds 1
    }

    Invoke-Compose logs --tail 60 web db-init sqlserver
    throw "RentalPro chưa trả HTTP 200 tại $loginUrl."
} finally {
    Pop-Location
}
