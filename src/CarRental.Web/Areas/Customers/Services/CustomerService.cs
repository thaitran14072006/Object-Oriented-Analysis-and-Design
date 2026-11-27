using System.Text.RegularExpressions;
using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.Data.SqlClient;

namespace CarRental.Web.Areas.Customers.Services;

public sealed record CustomerContact(string? Name, string? Phone, string? Cccd, string? Address, string? GroupId);
public sealed record CustomerLicense(string? Number, string? Class, DateOnly Expires, string? Verification);
public sealed record NewCustomer(CustomerContact? Contact, CustomerLicense? License, bool ConfirmSharedPhone);
public sealed record RatingInput(short Rating, string? Comment);
public sealed record CustomerGroup(string Id, string Name);
public sealed record CustomerRow(string Id, string Name, string Phone, string Cccd, string Address, string? GroupId,
    string? GroupName, string LicenseNumber, string LicenseClass, DateOnly LicenseExpires, string? Verification,
    short? Rating);
public sealed record RentalHistoryRow(string OrderId, string VehicleId, DateTime PickUp, DateTime Return, string Status);

public sealed partial class CustomerService(CarRentalDatabase database, TimeProvider time, IConfiguration configuration)
    : IRentalCustomerDirectory
{
    private const string WriteLock = "RentalPro.Customers";
    private const string CustomerColumns = """
        SELECT c.maKhachHang,c.hoTen,c.soDienThoai,c.cccd,c.diaChi,c.maNhom,n.tenNhom,
               c.soGPLX,c.hangGPLX,c.hetHanGPLX,c.thongTinXacMinh,r.mucTinNhiem
        FROM dbo.KhachHang c
        LEFT JOIN dbo.NhomKhachHang n ON n.maNhom=c.maNhom
        OUTER APPLY (
            SELECT TOP (1) d.mucTinNhiem FROM dbo.DanhGiaTinNhiem d
            WHERE d.maKhachHang=c.maKhachHang ORDER BY d.thoiDiem DESC,d.maTinNhiem DESC
        ) r
        """;
    private const int PageSize = 25;
    private short MaxRating => configuration.GetValue<short>("Customers:MaxRating", 5);

    public Task<object> SearchAsync(string? query, int page, CancellationToken token)
    {
        query = query?.Trim() ?? "";
        if (query.Length > 120 || page is < 1 or > 100000)
            throw new BusinessException("Từ khóa hoặc trang tìm kiếm không hợp lệ.");
        var escaped = query.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");

        return database.ReadAsync<object>(async work =>
        {
            var rows = await work.QueryAsync(CustomerColumns + """

                WHERE c.hoTen LIKE @pattern ESCAPE '~' OR c.soDienThoai LIKE @pattern ESCAPE '~'
                   OR c.cccd LIKE @pattern ESCAPE '~' OR c.soGPLX LIKE @pattern ESCAPE '~'
                ORDER BY c.hoTen,c.maKhachHang OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY
                """, Read, ("pattern", "%" + escaped + "%"), ("offset", (page - 1) * PageSize), ("take", PageSize + 1));
            return new { rows = rows.Take(PageSize), hasNext = rows.Count > PageSize, page, today = Today(), maxRating = MaxRating };
        }, token);
    }

    public Task<IReadOnlyList<CustomerGroup>> GroupsAsync(CancellationToken token) => database.ReadAsync(work =>
        work.QueryAsync("SELECT maNhom,tenNhom FROM dbo.NhomKhachHang ORDER BY tenNhom,maNhom",
            row => new CustomerGroup(row.GetString(0), row.GetString(1))), token);

    public Task SaveGroupAsync(string admin, CustomerGroup input, CancellationToken token)
    {
        var id = input.Id?.Trim().ToUpperInvariant();
        var name = input.Name?.Trim();
        if (!SecurityRules.IsEmployeeId(id) || name is not { Length: > 0 and <= 80 })
            throw new BusinessException("Mã hoặc tên nhóm khách hàng không hợp lệ.");
        return database.WriteAsync(WriteLock, async work =>
        {
            await work.ExecuteAsync("""
                UPDATE dbo.NhomKhachHang SET tenNhom=@name WHERE maNhom=@id;
                IF @@ROWCOUNT=0 INSERT dbo.NhomKhachHang (maNhom,tenNhom) VALUES (@id,@name);
                """, ("id", id), ("name", name));
            await IdentityData.AuditAsync(work, time.GetUtcNow().UtcDateTime, admin, "LuuNhomKhach", "NhomKhachHang", id, true);
            return true;
        }, token);
    }

    public Task<CustomerRow> GetAsync(string id, CancellationToken token) =>
        database.ReadAsync(work => RequireAsync(work, id), token);

    public Task<string> CreateAsync(string admin, NewCustomer input, CancellationToken token)
    {
        var contact = ValidateContact(input.Contact);
        var license = ValidateLicense(input.License);
        return database.WriteAsync(WriteLock, async work =>
        {
            await EnsureGroupAsync(work, contact.GroupId);
            await EnsureDocumentsUniqueAsync(work, null, contact.Cccd, license.Number);
            if (!input.ConfirmSharedPhone && await work.ValueAsync<int>(
                    "SELECT COUNT(*) FROM dbo.KhachHang WHERE soDienThoai=@phone", ("phone", contact.Phone)) != 0)
                throw new BusinessException("Số điện thoại đã có trong hồ sơ khác. Xác nhận đây là khách hàng khác.",
                    409, new { code = "shared_phone" });

            var id = "KH" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(9));
            await work.ExecuteAsync("""
                INSERT dbo.KhachHang (maKhachHang,maNhom,hoTen,soDienThoai,cccd,diaChi,soGPLX,hangGPLX,hetHanGPLX,thongTinXacMinh)
                VALUES (@id,@group,@name,@phone,@cccd,@address,@number,@class,@expires,@verification)
                """, ("id", id), ("group", contact.GroupId), ("name", contact.Name), ("phone", contact.Phone),
                ("cccd", contact.Cccd), ("address", contact.Address), ("number", license.Number),
                ("class", license.Class), ("expires", license.Expires), ("verification", license.Verification));
            await AuditAsync(work, admin, "ThemKhach", id);
            return id;
        }, token);
    }

    public Task UpdateProfileAsync(string admin, string id, CustomerContact input, CancellationToken token)
    {
        var contact = ValidateContact(input);
        return database.WriteAsync(WriteLock, async work =>
        {
            var existing = await RequireAsync(work, id);
            await EnsureGroupAsync(work, contact.GroupId);
            await EnsureDocumentsUniqueAsync(work, existing.Id, contact.Cccd, null);
            await work.ExecuteAsync("""
                UPDATE dbo.KhachHang SET maNhom=@group,hoTen=@name,soDienThoai=@phone,cccd=@cccd,
                       diaChi=@address,thongTinXacMinh=CASE WHEN cccd=@cccd THEN thongTinXacMinh ELSE NULL END
                WHERE maKhachHang=@id
                """, ("id", id), ("group", contact.GroupId), ("name", contact.Name), ("phone", contact.Phone),
                ("cccd", contact.Cccd), ("address", contact.Address));
            await AuditAsync(work, admin, "SuaKhach", existing.Id);
            return true;
        }, token);
    }

    public Task UpdateLicenseAsync(string admin, string id, CustomerLicense input, CancellationToken token)
    {
        var license = ValidateLicense(input);
        return database.WriteAsync(WriteLock, async work =>
        {
            var existing = await RequireAsync(work, id);
            await EnsureDocumentsUniqueAsync(work, existing.Id, null, license.Number);
            await work.ExecuteAsync("""
                UPDATE dbo.KhachHang SET soGPLX=@number,hangGPLX=@class,hetHanGPLX=@expires,thongTinXacMinh=@verification
                WHERE maKhachHang=@id
                """, ("id", id), ("number", license.Number), ("class", license.Class),
                ("expires", license.Expires), ("verification", license.Verification));
            await AuditAsync(work, admin, "SuaGiayPhep", existing.Id);
            return true;
        }, token);
    }

    public Task RateAsync(string admin, string id, RatingInput input, CancellationToken token)
    {
        var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        if (input.Rating < 1 || input.Rating > MaxRating || comment is { Length: > 1000 })
            throw new BusinessException("Mức tín nhiệm hoặc nhận xét không hợp lệ.");

        return database.WriteAsync(WriteLock, async work =>
        {
            var customer = await RequireAsync(work, id);
            if (customer.Rating > input.Rating && comment is null)
                throw new BusinessException("Vui lòng nhập nhận xét khi hạ mức tín nhiệm.");

            await work.ExecuteAsync("""
                INSERT dbo.DanhGiaTinNhiem (maKhachHang,maNguoiDanhGia,mucTinNhiem,nhanXet,thoiDiem)
                VALUES (@id,@admin,@rating,@comment,@now)
                """, ("id", id), ("admin", admin), ("rating", input.Rating), ("comment", comment),
                ("now", time.GetUtcNow().UtcDateTime));
            await AuditAsync(work, admin, "DanhGiaTinNhiem", customer.Id);
            return true;
        }, token);
    }

    public Task<object> HistoryAsync(string id, CancellationToken token) => database.ReadAsync<object>(async work =>
    {
        await RequireAsync(work, id);
        if (!await work.ValueAsync<bool>("SELECT CAST(CASE WHEN OBJECT_ID(N'dbo.DonThue',N'U') IS NULL THEN 0 ELSE 1 END AS bit)"))
            return new { available = false, rows = Array.Empty<RentalHistoryRow>() };

        var rows = await work.QueryAsync("""
            SELECT maDon,maXe,nhanDuKien,traDuKien,trangThai FROM dbo.DonThue
            WHERE maKhachHang=@id ORDER BY nhanDuKien DESC,maDon
            """, row => new RentalHistoryRow(row.GetString(0), row.GetString(1), row.GetDateTime(2),
            row.GetDateTime(3), row.GetString(4)), ("id", id));
        return new { available = true, rows };
    }, token);

    public Task<RentalCustomer?> FindForRentalAsync(string customerId, CancellationToken cancellationToken) =>
        database.ReadAsync(async work =>
        {
            var row = await FindAsync(work, customerId);
            return row is null ? null : new RentalCustomer(row.Id, row.Name, row.GroupId, row.LicenseClass,
                row.LicenseExpires, !string.IsNullOrWhiteSpace(row.Verification), row.LicenseExpires < Today(), row.Rating);
        }, cancellationToken);

    private DateOnly Today() => DateOnly.FromDateTime(time.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    private static CustomerContact ValidateContact(CustomerContact? input)
    {
        var name = input?.Name?.Trim();
        var phone = input?.Phone?.Trim();
        var cccd = input?.Cccd?.Trim();
        var address = input?.Address?.Trim();
        var group = string.IsNullOrWhiteSpace(input?.GroupId) ? null : input.GroupId.Trim();
        if (name is not { Length: > 0 and <= 100 } || address is not { Length: > 0 and <= 255 } ||
            !SecurityRules.IsPhone(phone) || cccd is null || !CccdPattern().IsMatch(cccd) ||
            (group is not null && !SecurityRules.IsEmployeeId(group)))
            throw new BusinessException("Nhập họ tên, địa chỉ, số điện thoại và CCCD 12 chữ số hợp lệ.");
        return new(name, phone, cccd, address, group);
    }

    private static CustomerLicense ValidateLicense(CustomerLicense? input)
    {
        var number = input?.Number?.Trim().ToUpperInvariant();
        var licenseClass = input?.Class?.Trim().ToUpperInvariant();
        var verification = string.IsNullOrWhiteSpace(input?.Verification) ? null : input.Verification.Trim();
        if (input is null || input.Expires == default || number is null || !LicenseNumberPattern().IsMatch(number) ||
            !SecurityRules.IsEmployeeId(licenseClass) || verification is { Length: > 500 })
            throw new BusinessException("Nhập số, hạng và ngày hết hạn GPLX hợp lệ.");
        return new(number, licenseClass, input.Expires, verification);
    }

    private static async Task EnsureGroupAsync(SqlWork work, string? id)
    {
        if (id is not null && await work.ValueAsync<int>("SELECT COUNT(*) FROM dbo.NhomKhachHang WHERE maNhom=@id",
                ("id", id)) != 1)
            throw new BusinessException("Nhóm khách hàng không tồn tại.");
    }

    private static async Task EnsureDocumentsUniqueAsync(SqlWork work, string? id, string? cccd, string? license)
    {
        var found = await work.OneAsync("""
            SELECT TOP (1) maKhachHang FROM dbo.KhachHang
            WHERE (@id IS NULL OR maKhachHang<>@id) AND (cccd=@cccd OR soGPLX=@license)
            ORDER BY maKhachHang
            """, row => row.GetString(0), ("id", id), ("cccd", cccd), ("license", license));
        if (found is not null)
            throw new BusinessException(id is null ? "Khách hàng đã tồn tại." :
                cccd is null ? "Số giấy phép lái xe đã có trong hồ sơ khác." : "CCCD đã có trong hồ sơ khác.",
                409, new { code = "duplicate_customer", existingId = found });
    }

    private async Task AuditAsync(SqlWork work, string admin, string action, string id) =>
        await IdentityData.AuditAsync(work, time.GetUtcNow().UtcDateTime, admin, action, "KhachHang", id, true);

    private static Task<CustomerRow?> FindAsync(SqlWork work, string id) => work.OneAsync(
        CustomerColumns + " WHERE c.maKhachHang=@id", Read, ("id", id));

    private static async Task<CustomerRow> RequireAsync(SqlWork work, string id) =>
        await FindAsync(work, id) ?? throw new BusinessException("Không tìm thấy hồ sơ khách hàng.", 404);

    private static CustomerRow Read(SqlDataReader row) => new(row.GetString(0), row.GetString(1), row.GetString(2),
        row.GetString(3), row.GetString(4), row.IsDBNull(5) ? null : row.GetString(5),
        row.IsDBNull(6) ? null : row.GetString(6), row.GetString(7), row.GetString(8),
        DateOnly.FromDateTime(row.GetDateTime(9)), row.IsDBNull(10) ? null : row.GetString(10),
        row.IsDBNull(11) ? null : row.GetInt16(11));

    [GeneratedRegex(@"\A[0-9]{12}\z")]
    private static partial Regex CccdPattern();
    [GeneratedRegex(@"\A[A-Z0-9][A-Z0-9-]{4,29}\z")]
    private static partial Regex LicenseNumberPattern();
}
