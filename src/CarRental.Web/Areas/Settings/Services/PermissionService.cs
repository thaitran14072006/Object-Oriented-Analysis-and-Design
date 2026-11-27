using System.Text.Json;
using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;

namespace CarRental.Web.Areas.Settings.Services;

public sealed record PermissionInput(string? TargetType, string? TargetId, string? Permission, bool? Allowed, string? Version);
public sealed record PermissionRole(string Id, string Name, bool Full);
public sealed record PermissionAccount(string Id, string Name, string Role, bool Active);
public sealed record RoleGrant(string Role, string Permission);
public sealed record PersonalGrant(string Account, string Permission, bool Allowed);
public sealed record PermissionSnapshot(IReadOnlyList<PermissionRole> Roles, IReadOnlyList<PermissionAccount> Accounts,
    IReadOnlyList<string> Permissions, IReadOnlyList<RoleGrant> RoleGrants, IReadOnlyList<PersonalGrant> PersonalGrants)
{
    public string Version => SecurityRules.TokenHash(JsonSerializer.Serialize(
        new { Roles, Accounts, Permissions, RoleGrants, PersonalGrants }));
}

public sealed class PermissionService(CarRentalDatabase database, TimeProvider time)
{
    public Task<PermissionSnapshot> GetAsync(CancellationToken token) =>
        database.WriteAsync(IdentityData.WriteLock, SnapshotAsync, token);

    public Task ChangeAsync(string actor, PermissionInput input, CancellationToken token) =>
        database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            var actorPermissions = await IdentityData.PermissionsAsync(work, actor);
            if (!actorPermissions.Contains(AdminAccess.Admin) || !actorPermissions.Contains(AdminAccess.Security))
                throw new BusinessException("Bạn không có quyền quản trị phân quyền.", 403);

            var snapshot = await SnapshotAsync(work);
            if (!string.Equals(snapshot.Version, input.Version, StringComparison.Ordinal))
                throw new BusinessException("Dữ liệu đã thay đổi. Vui lòng tải lại.", 409);
            if (input.Permission is null || !snapshot.Permissions.Contains(input.Permission))
                throw new BusinessException("Quyền không hợp lệ.");

            if (input.TargetType == "role" && snapshot.Roles.Any(role => role.Id == input.TargetId))
            {
                if (snapshot.Roles.Any(role => role.Id == input.TargetId && role.Full))
                    throw new BusinessException("Vai trò toàn quyền mặc định được cấp mọi quyền.");
                await work.ExecuteAsync("""
                    DELETE dbo.VaiTroQuyen WHERE maVaiTro=@id AND maQuyen=@permission;
                    IF @allowed=1 INSERT dbo.VaiTroQuyen (maVaiTro,maQuyen) VALUES (@id,@permission);
                    """, ("id", input.TargetId), ("permission", input.Permission), ("allowed", input.Allowed == true));
            }
            else if (input.TargetType == "account" && snapshot.Accounts.Any(account => account.Id == input.TargetId))
            {
                await work.ExecuteAsync("""
                    DELETE dbo.QuyenCaNhan WHERE maTaiKhoan=@id AND maQuyen=@permission;
                    IF @allowed IS NOT NULL
                        INSERT dbo.QuyenCaNhan (maTaiKhoan,maQuyen,choPhep) VALUES (@id,@permission,@allowed);
                    """, ("id", input.TargetId), ("permission", input.Permission), ("allowed", input.Allowed));
            }
            else
                throw new BusinessException("Vai trò hoặc tài khoản không tồn tại.", 409);

            var fullAdmins = await work.ValueAsync<int>("""
                SELECT COUNT(*) FROM dbo.TaiKhoan t JOIN dbo.VaiTro r ON r.maVaiTro=t.maVaiTro
                WHERE t.trangThai='HoatDong' AND r.toanQuyen=1 AND NOT EXISTS (
                    SELECT 1 FROM dbo.QuyenCaNhan c WHERE c.maTaiKhoan=t.maTaiKhoan AND c.choPhep=0
                )
                """);
            if (fullAdmins == 0)
                throw new BusinessException("Hệ thống phải còn ít nhất một tài khoản quản trị toàn quyền.", 409);

            await IdentityData.AuditAsync(work, time.GetUtcNow().UtcDateTime, actor, "PhanQuyen",
                input.TargetType == "role" ? "VaiTro" : "TaiKhoan", input.TargetId, true);
            return true;
        }, token);

    private static async Task<PermissionSnapshot> SnapshotAsync(SqlWork work)
    {
        var roles = await work.QueryAsync("SELECT maVaiTro,tenVaiTro,toanQuyen FROM dbo.VaiTro ORDER BY maVaiTro",
            row => new PermissionRole(row.GetString(0), row.GetString(1), row.GetBoolean(2)));
        var accounts = await work.QueryAsync("SELECT maTaiKhoan,hoTen,maVaiTro,trangThai FROM dbo.TaiKhoan ORDER BY maTaiKhoan",
            row => new PermissionAccount(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3) == "HoatDong"));
        var permissions = await work.QueryAsync("SELECT maQuyen FROM dbo.Quyen ORDER BY maQuyen", row => row.GetString(0));
        var roleGrants = await work.QueryAsync("SELECT maVaiTro,maQuyen FROM dbo.VaiTroQuyen ORDER BY maVaiTro,maQuyen",
            row => new RoleGrant(row.GetString(0), row.GetString(1)));
        var personalGrants = await work.QueryAsync(
            "SELECT maTaiKhoan,maQuyen,choPhep FROM dbo.QuyenCaNhan ORDER BY maTaiKhoan,maQuyen",
            row => new PersonalGrant(row.GetString(0), row.GetString(1), row.GetBoolean(2)));
        return new(roles, accounts, permissions, roleGrants, personalGrants);
    }
}
