using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Areas.Fleet.Services;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Web.Areas.Fleet.Controllers;

[Area("Fleet"), ApiController, Route("api/fleet"), Authorize(Policy = "BillingAdmin")]
public sealed class FleetController(FleetService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(string? q, string? status, string? type, bool archived = false)
        => Ok(await service.Filter(q, status, type, archived).ToListAsync());
    [HttpGet("types")] public async Task<IActionResult> Types() => Ok(await service.Types());
    [HttpGet("{id}")] public async Task<IActionResult> Detail(string id) => Ok(await service.Detail(id));
    [HttpPost] public async Task<IActionResult> Create(VehicleInput input) { await service.Save(input, true); return StatusCode(201, new { input.Id }); }
    [HttpPut("{id}")] public async Task<IActionResult> Update(string id, VehicleInput input)
    {
        if (id != input.Id) return BadRequest(new { error = "Không được đổi mã xe." });
        await service.Save(input, false); return NoContent();
    }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string id) => Ok(new { archived = await service.Delete(id) });
    [HttpGet("export")] public async Task<IActionResult> Export(string? q, string? status, string? type, bool archived = false)
    {
        var rows = await service.Filter(q, status, type, archived).ToListAsync();
        return File(SpreadsheetExport.Create(["Mã xe", "Tên xe", "Biển số", "Loại xe", "Năm SX", "Tình trạng", "Vị trí đỗ"],
            rows.Select(x => new object?[] { x.Id, x.Name, x.Plate, x.TypeId, x.Year, x.Status, x.Parking })), SpreadsheetExport.ContentType, "Danh-sach-xe.xlsx");
    }
}
