using System.Security.Claims;
using CarRental.Web.Areas.Maintenance.Data;
using CarRental.Web.Areas.Maintenance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Web.Areas.Maintenance.Controllers;

[Area("Maintenance"), ApiController, Route("api/maintenance"), Authorize(Policy = "Maintenance.Access")]
public sealed class MaintenanceController(MaintenanceService service) : ControllerBase
{
    private string Admin => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("vehicles")] public async Task<IActionResult> Vehicles() => Ok(await service.Vehicles());
    [HttpGet] public async Task<IActionResult> List(string? q, string? status, string? vehicle) => Ok(await service.List(q, status, vehicle));
    [HttpGet("{id}")] public async Task<IActionResult> Detail(string id) => Ok(await service.Detail(id));
    [HttpPost] public async Task<IActionResult> Create(MaintenanceInput input) => StatusCode(201, new { id = await service.Create(input, Admin) });
    [HttpPost("{id}/start")] public async Task<IActionResult> Start(string id) { await service.Change(id, "start", Admin); return NoContent(); }
    [HttpPost("{id}/progress")] public async Task<IActionResult> Progress(string id, ProgressInput input) { await service.Change(id, "progress", Admin, progress: input); return NoContent(); }
    [HttpPost("{id}/cost")] public async Task<IActionResult> Cost(string id, MaintenanceCostInput input) { await service.Change(id, "cost", Admin, cost: input); return NoContent(); }
    [HttpPost("{id}/safety")] public async Task<IActionResult> Safety(string id, SafetyInput input) { await service.Change(id, "safety", Admin, safety: input); return NoContent(); }
    [HttpPost("{id}/complete")] public async Task<IActionResult> Complete(string id) { await service.Change(id, "complete", Admin); return NoContent(); }
}
