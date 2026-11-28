using System.Data;
using CarRental.Web.Areas.Reports.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Web.Areas.Reports.Services;

public sealed class PeriodicReportService(RentalDbContext db, ReportService reports, IWebHostEnvironment environment, TimeProvider clock)
{
    private string Folder => Path.Combine(environment.ContentRootPath, "App_Data", "Reports");
    public Task<List<PeriodicReport>> List() => db.Set<PeriodicReport>().AsNoTracking().OrderByDescending(x => x.From).ToListAsync();

    public async Task Generate(DateOnly from, DateOnly to)
    {
        ReportService.Range(from, to);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ReportService.BusinessTimeZone).DateTime);
        BillingService.Require(to < today, "Chỉ tạo báo cáo định kỳ cho kỳ đã kết thúc.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var key = $"CarRental:Report:Promotions:{from:yyyyMMdd}:{to:yyyyMMdd}";
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Report lock failed',1;");
        var record = await db.Set<PeriodicReport>().SingleOrDefaultAsync(x => x.Kind == "HIEU_QUA_KHUYEN_MAI" && x.From == from && x.To == to);
        if (record?.Status == "ThanhCong" && File.Exists(Path.Combine(Folder, Path.GetFileName(record.Path))))
        { await transaction.CommitAsync(); return; }
        if (record == null) { record = new PeriodicReport { From = from, To = to }; db.Add(record); }
        record.Created = clock.GetUtcNow().UtcDateTime;
        record.Path = $"Khuyen-mai-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx";
        Exception? failure = null;
        try
        {
            var bytes = ReportService.Export(await reports.Build("promotions", from, to));
            Directory.CreateDirectory(Folder);
            var temporary = Path.Combine(Folder, Guid.NewGuid() + ".tmp");
            try { await File.WriteAllBytesAsync(temporary, bytes); File.Move(temporary, Path.Combine(Folder, record.Path), true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            record.Status = "ThanhCong";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.SqlClient.SqlException)
        { record.Status = "ThatBai"; failure = ex; }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        if (failure != null) throw new BusinessError("Không tạo được tệp báo cáo, kỳ này sẽ được thử lại.");
    }

    public async Task<(byte[] Bytes, string Name)> Download(long id)
    {
        var record = await db.Set<PeriodicReport>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status == "ThanhCong")
            ?? throw new BusinessError("Báo cáo chưa có kết quả.");
        var name = Path.GetFileName(record.Path);
        var path = Path.Combine(Folder, name);
        BillingService.Require(File.Exists(path), "Tệp báo cáo không còn tồn tại, cần tạo lại kỳ này.");
        return (await File.ReadAllBytesAsync(path), name);
    }
}

public sealed class PeriodicReportWorker(IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider clock, ILogger<PeriodicReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Reports:Enabled")) return;
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("CarRental")))
        {
            logger.LogWarning("Chưa cấu hình ConnectionStrings:CarRental. Tạm bỏ qua báo cáo định kỳ.");
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<PeriodicReportService>();
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ReportService.BusinessTimeZone).DateTime);
                var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
                var pending = (await service.List()).Where(x => x.Status == "ThatBai").Select(x => (x.From, x.To))
                    .Append((from, from.AddMonths(1).AddDays(-1))).Distinct();
                foreach (var period in pending)
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    try { await service.Generate(period.Item1, period.Item2); }
                    catch (Exception ex) { logger.LogError(ex, "Không tạo được báo cáo kỳ {From} đến {To}.", period.Item1, period.Item2); }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Chưa thể chạy báo cáo định kỳ."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
