using CarRental.Web.Areas.Reports.Services;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Web.Areas.Reports.Controllers;

[Area("Reports"), ApiController, Route("api/reports"), Authorize(Policy = "BillingAdmin")]
public sealed class ReportsController(ReportService service, PeriodicReportService periodic) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(string type, DateOnly from, DateOnly to) => Ok(await service.Build(type, from, to));
    [HttpGet("export")] public async Task<IActionResult> Export(string type, DateOnly from, DateOnly to) =>
        File(ReportService.Export(await service.Build(type, from, to)), SpreadsheetExport.ContentType, $"Bao-cao-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    [HttpGet("periodic")] public async Task<IActionResult> List() => Ok(await periodic.List());
    [HttpPost("periodic")] public async Task<IActionResult> Generate(PeriodInput input) { await periodic.Generate(input.From, input.To); return NoContent(); }
    [HttpGet("periodic/{id:long}/download")] public async Task<IActionResult> Download(long id)
    { var result = await periodic.Download(id); return File(result.Bytes, SpreadsheetExport.ContentType, result.Name); }
}

public sealed record PeriodInput(DateOnly From, DateOnly To);
