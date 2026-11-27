using CarRental.Web.Areas.Customers.Services;
using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Areas.Settings.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var checks = 0;
var passwords = new PasswordHasher<string>();
var clock = new TestTime();
var now = clock.GetUtcNow().UtcDateTime;
var password = SecurityRules.NewToken() + "Aa1";
var changedPassword = SecurityRules.NewToken() + "Aa2";
var delivery = new MemoryDelivery();
var otp = new OtpService(new EphemeralDataProtectionProvider(), delivery);

Check(Enumerable.Range(0, 50).Select(_ => SecurityRules.NewOtp()).All(SecurityRules.IsOtp), "OTP có đúng 6 chữ số");
Check(!SecurityRules.IsOtp("12345\n") && !SecurityRules.IsOtp("１２３４５６"), "Từ chối OTP sai định dạng");
Check(!SecurityRules.CanResend(now, now.AddSeconds(59)) && SecurityRules.CanResend(now, now.AddSeconds(60)), "Gửi lại sau 60 giây");
Check(SecurityRules.IsOtpCurrent(now.AddMinutes(5), null, now.AddSeconds(299)), "OTP còn hiệu lực trước 5 phút");
Check(!SecurityRules.IsOtpCurrent(now.AddMinutes(5), null, now.AddMinutes(5)), "OTP hết hiệu lực đúng 5 phút");
Check(!SecurityRules.IsOtpCurrent(now.AddMinutes(5), now, now), "OTP đã dùng bị từ chối");
Check(!SecurityRules.IsIdleExpired(now, now.AddMinutes(29)) && SecurityRules.IsIdleExpired(now, now.AddMinutes(30)), "Phiên hết hạn đúng 30 phút");
Check(SecurityRules.IsEmployeeId("NV-01") && !SecurityRules.IsEmployeeId("NV/01"), "Định dạng mã nhân viên");
Check(SecurityRules.IsPhone("+84900000000") && !SecurityRules.IsPhone("0900abc000"), "Định dạng điện thoại");
Check(SecurityRules.IsEmail("admin@example.test") && !SecurityRules.IsEmail("a b@example.test"), "Định dạng email");
SecurityRules.ValidatePassword(password, password);
await ExpectAsync(() => Task.Run(() => SecurityRules.ValidatePassword("weakpass", "weakpass")), 400,
    "Từ chối mật khẩu thiếu chữ hoa và số");
await ExpectAsync(() => Task.Run(() => SecurityRules.ValidatePassword(password, changedPassword)), 400,
    "Từ chối xác nhận mật khẩu không khớp");
var hash = passwords.HashPassword("ADMIN01", password);
Check(passwords.VerifyHashedPassword("ADMIN01", hash, password) != PasswordVerificationResult.Failed
      && passwords.VerifyHashedPassword("ADMIN01", hash, changedPassword) == PasswordVerificationResult.Failed
      && hash != passwords.HashPassword("ADMIN01", password), "Mật khẩu băm và salt riêng");
var session = SecurityRules.NewToken();
Check(session.Length == 43 && session != SecurityRules.NewToken() && SecurityRules.TokenHash(session).Length == 64,
    "Token ngẫu nhiên 256 bit và giá trị băm");
var dummy = otp.DummyRecoveryTicket();
Check(otp.Read(dummy, SecurityRules.Recovery).Id == 0, "Ticket khôi phục có thể xác thực");
await ExpectAsync(() => Task.Run(() => otp.Read(dummy, SecurityRules.Login)), 400, "Ticket OTP gắn mục đích");
await ExpectAsync(() => Task.Run(() => otp.Read(dummy.Insert(0, "x"), SecurityRules.Recovery)), 400,
    "Ticket bị sửa bị từ chối");

if (args is ["--sql"])
{
    var connectionString = Environment.GetEnvironmentVariable("CARRENTAL_SQL_TEST_CONNECTION");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Đặt CARRENTAL_SQL_TEST_CONNECTION cho SQL Server thử có quyền tạo database.");
    await RunSqlAsync(connectionString);
}
else
{
    Console.WriteLine("Bỏ qua kiểm thử SQL Server. Dùng --sql khi có máy chủ thử.");
}
Console.WriteLine($"PASS {checks}");

async Task RunSqlAsync(string connectionString)
{
    var dbName = "CarRental_Test_" + Guid.NewGuid().ToString("N");
    var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", Pooling = false };
    var isolated = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = dbName, Pooling = false };
    await using var adminConnection = new SqlConnection(master.ConnectionString);
    await adminConnection.OpenAsync();
    await ExecuteAsync(adminConnection, $"CREATE DATABASE [{dbName}]");
    try
    {
        await using var connection = new SqlConnection(isolated.ConnectionString);
        await connection.OpenAsync();
        foreach (var migration in new[] { "01_identity.sql", "03_customers.sql" })
            await ExecuteAsync(connection, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, migration)));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CarRental"] = isolated.ConnectionString,
            ["BootstrapAdmin:Id"] = "ADMIN01",
            ["BootstrapAdmin:Name"] = "Quản trị thử",
            ["BootstrapAdmin:Email"] = "admin@example.test",
            ["BootstrapAdmin:Phone"] = "0900000001",
            ["BootstrapAdmin:Password"] = password
        }).Build();
        var database = new CarRentalDatabase(config);
        var auth = new AuthService(database, otp, passwords, clock, config);
        var settings = new SettingsService(database, otp, passwords, clock);
        var rights = new PermissionService(database, clock);
        var customers = new CustomerService(database, clock, config);
        var token = CancellationToken.None;

        await auth.BootstrapAsync(token);
        await auth.BootstrapAsync(token);
        await ExecuteAsync(connection, """
            INSERT dbo.TaiKhoan (maTaiKhoan,maVaiTro,hoTen,email,soDienThoai,matKhauBam,trangThai)
            SELECT 'NV02','OPERATOR',N'Nhân viên thử','operator@example.test','0900000002',matKhauBam,'HoatDong'
            FROM dbo.TaiKhoan WHERE maTaiKhoan='ADMIN01';
            """);
        var admin = await auth.LoginAsync("admin01", password, token);
        Check(admin.Admin?.Id == "ADMIN01" && admin.SessionToken is not null, "Đăng nhập và tạo phiên SQL");
        Check(await auth.SessionAsync("ADMIN01", admin.SessionToken!, token) is not null, "Phiên được nhận diện");
        clock.Advance(TimeSpan.FromMinutes(30));
        Check(await auth.SessionAsync("ADMIN01", admin.SessionToken!, token) is null, "Phiên SQL hết hạn sau khi không hoạt động");

        for (var attempt = 0; attempt < SecurityRules.AttemptLimit; attempt++)
        {
            try { await auth.LoginAsync("NV02", changedPassword, token); }
            catch (BusinessException error) when (error.Status == 401) { }
        }
        await ExpectAsync(() => auth.LoginAsync("NV02", password, token), 401, "Khóa đăng nhập sau năm lần sai");
        clock.Advance(TimeSpan.FromMinutes(15));
        var staff = await auth.LoginAsync("NV02", password, token);
        Check(staff.Admin!.Permissions.Contains(AdminAccess.CustomersRead) &&
            !staff.Admin.Permissions.Contains(AdminAccess.Security), "Nhân viên có quyền theo vai trò");

        var snapshot = await rights.GetAsync(token);
        await ExpectAsync(() => rights.ChangeAsync("ADMIN01",
            new("account", "ADMIN01", AdminAccess.Security, false, snapshot.Version), token), 409,
            "Giữ ít nhất một Admin toàn quyền");
        await rights.ChangeAsync("ADMIN01", new("account", "NV02", AdminAccess.CustomersRead, false, snapshot.Version), token);
        Check(!(await auth.SessionAsync("NV02", staff.SessionToken!, token))!.Permissions.Contains(AdminAccess.CustomersRead),
            "Quyền từ chối cá nhân ghi đè vai trò");
        await ExpectAsync(() => rights.ChangeAsync("ADMIN01",
            new("account", "NV02", AdminAccess.CustomersRead, null, snapshot.Version), token), 409, "Chặn ghi quyền bằng phiên bản cũ");

        var twoFactor = await settings.TwoFactorOtpAsync("ADMIN01", "EMAIL", token);
        await settings.EnableTwoFactorAsync("ADMIN01", new(twoFactor, delivery.Code), token);
        var first = await auth.LoginAsync("ADMIN01", password, token);
        Check(first.Ticket is not null && first.SessionToken is null, "Chưa cấp phiên khi còn thiếu OTP");
        var oldCode = delivery.Code;
        await ExpectAsync(() => auth.ResendAsync(first.Ticket, SecurityRules.Login, token), 429, "Chặn gửi lại OTP sớm");
        clock.Advance(TimeSpan.FromSeconds(60));
        var next = await auth.ResendAsync(first.Ticket, SecurityRules.Login, token);
        await ExpectAsync(() => auth.VerifyLoginAsync(new(first.Ticket, oldCode), token), 401, "Mã và ticket cũ bị vô hiệu");
        var verified = await auth.VerifyLoginAsync(new(next, delivery.Code), token);
        Check(verified.SessionToken is not null, "OTP đúng hoàn tất đăng nhập");
        await ExpectAsync(() => auth.VerifyLoginAsync(new(next, delivery.Code), token), 401, "Chặn sử dụng lại OTP");
        var reset = await auth.RequestRecoveryAsync("admin@example.test", token);
        await auth.ResetAsync(new(reset, delivery.Code), changedPassword, changedPassword, token);
        Check(await auth.SessionAsync("ADMIN01", verified.SessionToken!, token) is null, "Khôi phục mật khẩu thu hồi phiên");
        await ExpectAsync(() => auth.LoginAsync("ADMIN01", password, token), 401, "Mật khẩu cũ bị từ chối sau đặt lại");

        await settings.SaveAppearanceAsync("ADMIN01", new("blue", "mono", "compact", 110), token);
        var profileEmail = await settings.ContactOtpAsync("ADMIN01", "EMAIL", "new@example.test", token);
        await settings.UpdateProfileAsync("ADMIN01", new("Tên mới", "new@example.test", "0900000001", null,
            new(profileEmail, delivery.Code), null), token);
        var settingsJson = System.Text.Json.JsonSerializer.Serialize(await settings.GetAsync("ADMIN01", token));
        Check(settingsJson.Contains("new@example.test") && settingsJson.Contains("compact"), "Lưu hồ sơ đã xác minh và cài đặt giao diện");

        var contact = new CustomerContact("Khách thử", "0900000021", "000000000021", "Địa chỉ kiểm thử", "THUONG");
        var license = new CustomerLicense("GPLX01234", "B", new DateOnly(2030, 1, 1), "Đã đối chiếu giấy tờ gốc");
        var customerId = await customers.CreateAsync("ADMIN01", new(contact, license, false), token);
        await ExpectAsync(() => customers.CreateAsync("ADMIN01", new(contact, license, true), token), 409, "Chặn trùng CCCD hoặc GPLX");
        var other = new NewCustomer(contact with { Cccd = "000000000022" }, license with { Number = "GPLX01235" }, false);
        await ExpectAsync(() => customers.CreateAsync("ADMIN01", other, token), 409, "Yêu cầu xác nhận nếu chỉ trùng điện thoại");
        Check(await customers.CreateAsync("ADMIN01", other with { ConfirmSharedPhone = true }, token) != customerId,
            "Cho phép hai khách dùng cùng số điện thoại sau xác nhận");
        await customers.RateAsync("ADMIN01", customerId, new(5, null), token);
        await ExpectAsync(() => customers.RateAsync("ADMIN01", customerId, new(2, null), token), 400,
            "Hạ tín nhiệm cần nhận xét");
        await customers.RateAsync("ADMIN01", customerId, new(2, "Đã đối chiếu lịch sử"), token);
        var directory = await customers.FindForRentalAsync(customerId, token);
        Check(directory is { Rating: 2, Verified: true, LicenseClass: "B" }, "Hợp đồng Bookings lấy tín nhiệm và GPLX");
        var history = System.Text.Json.JsonSerializer.Serialize(await customers.HistoryAsync(customerId, token));
        Check(history.Contains("\"available\":false"), "Báo tình trạng lịch sử khi chưa có mô-đun Đặt xe");
        await ExecuteAsync(connection, """
            CREATE TABLE dbo.DonThue (maDon varchar(20) NOT NULL PRIMARY KEY, maKhachHang varchar(20) NOT NULL,
                maXe varchar(20) NOT NULL, nhanDuKien datetime2 NOT NULL, traDuKien datetime2 NOT NULL, trangThai varchar(25) NOT NULL);
            """);
        await using var order = connection.CreateCommand();
        order.CommandText = """
            INSERT dbo.DonThue VALUES ('DH01', @id, 'XE01', '2026-12-01T10:00:00', '2026-12-02T10:00:00', 'DaTraXe')
            """;
        order.Parameters.AddWithValue("@id", customerId);
        await order.ExecuteNonQueryAsync();
        history = System.Text.Json.JsonSerializer.Serialize(await customers.HistoryAsync(customerId, token));
        Check(history.Contains("DH01") && history.Contains("XE01"), "Đọc lịch sử đơn từ dữ liệu Bookings");
    }
    finally
    {
        await ExecuteAsync(adminConnection, $"DROP DATABASE [{dbName}]");
    }
}

static async Task ExecuteAsync(SqlConnection connection, string text)
{
    await using var command = connection.CreateCommand();
    command.CommandText = text;
    await command.ExecuteNonQueryAsync();
}

void Check(bool success, string title)
{
    if (!success) throw new InvalidOperationException("FAIL " + title);
    Console.WriteLine("PASS " + title);
    checks++;
}

async Task ExpectAsync(Func<Task> execute, int status, string title)
{
    try { await execute(); }
    catch (BusinessException error) when (error.Status == status)
    {
        Check(true, title);
        return;
    }
    Check(false, title);
}

sealed class TestTime : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan time) => _now += time;
}

sealed class MemoryDelivery : IOtpDelivery
{
    public string Code { get; private set; } = "";
    public Task SendAsync(string channel, string destination, string code, CancellationToken cancellationToken)
    {
        Code = code;
        return Task.CompletedTask;
    }
}
