using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.Data.SqlClient;

namespace CarRental.Web.Areas.Identity.Data;

public sealed record Account(string Id, string Role, string Name, string Email, string Phone, string? Address,
    string PasswordHash, bool Active, bool TwoFactor, string? Channel, int Failures, DateTime? LockedUntil)
{
    public string Destination(string channel) => channel == "SMS" ? Phone : Email;
}

public sealed record AdminProfile(string Id, string Role, string Name, string Email, string Phone, string? Address,
    bool TwoFactor, string? Channel, IReadOnlyList<string> Permissions);

public sealed class IdentityData(CarRentalDatabase database, TimeProvider time) : IAdminAudit
{
    public const string WriteLock = "RentalPro.Identity";
    private const string AccountColumns = """
        maTaiKhoan, maVaiTro, hoTen, email, soDienThoai, diaChi, matKhauBam, trangThai,
        batHaiYeuTo, kenhOTP, soLanSai, khoaDen
        """;

    public static Task<Account?> AccountAsync(SqlWork work, string id) => work.OneAsync(
        $"SELECT {AccountColumns} FROM dbo.TaiKhoan WHERE maTaiKhoan=@id", MapAccount, ("id", id));

    public static Task<Account?> RecoveryAccountAsync(SqlWork work, string identifier) =>
        identifier.Contains('@')
            ? work.OneAsync($"SELECT {AccountColumns} FROM dbo.TaiKhoan WHERE email=@input", MapAccount,
                ("input", identifier))
            : AccountAsync(work, identifier);

    private static Account MapAccount(SqlDataReader row) => new(row.GetString(0), row.GetString(1),
        row.GetString(2), row.GetString(3), row.GetString(4), row.IsDBNull(5) ? null : row.GetString(5),
        row.GetString(6), row.GetString(7) == "HoatDong", row.GetBoolean(8),
        row.IsDBNull(9) ? null : row.GetString(9), row.GetInt32(10),
        row.IsDBNull(11) ? null : row.GetDateTime(11));

    public static Task<IReadOnlyList<string>> PermissionsAsync(SqlWork work, string id) => work.QueryAsync("""
        SELECT q.maQuyen
        FROM dbo.TaiKhoan t JOIN dbo.VaiTro v ON v.maVaiTro=t.maVaiTro
        CROSS JOIN dbo.Quyen q
        LEFT JOIN dbo.QuyenCaNhan c ON c.maTaiKhoan=t.maTaiKhoan AND c.maQuyen=q.maQuyen
        LEFT JOIN dbo.VaiTroQuyen r ON r.maVaiTro=t.maVaiTro AND r.maQuyen=q.maQuyen
        WHERE t.maTaiKhoan=@id AND t.trangThai='HoatDong'
          AND COALESCE(c.choPhep, CASE WHEN v.toanQuyen=1 OR r.maQuyen IS NOT NULL THEN 1 ELSE 0 END)=1
        ORDER BY q.maQuyen
        """, row => row.GetString(0), ("id", id));

    public static async Task<AdminProfile> ProfileAsync(SqlWork work, Account account) =>
        new(account.Id, account.Role, account.Name, account.Email, account.Phone, account.Address,
            account.TwoFactor, account.Channel, await PermissionsAsync(work, account.Id));

    public static Task AuditAsync(SqlWork work, DateTime now, string? id, string action,
        string entityType, string? entityId, bool success) => work.ExecuteAsync("""
        INSERT dbo.NhatKyThaoTac (maTaiKhoan, thoiDiem, hanhDong, loaiDoiTuong, maDoiTuong, ketQua)
        VALUES (@id,@now,@action,@kind,@entity,@result)
        """, ("id", id), ("now", now), ("action", action), ("kind", entityType), ("entity", entityId),
        ("result", success ? "ThanhCong" : "ThatBai"));

    public async Task RecordAsync(string? adminId, string action, string entityType, string? entityId,
        bool succeeded, CancellationToken cancellationToken)
    {
        await database.ReadAsync(async work =>
        {
            await AuditAsync(work, time.GetUtcNow().UtcDateTime, adminId, action, entityType, entityId, succeeded);
            return true;
        }, cancellationToken);
    }
}
