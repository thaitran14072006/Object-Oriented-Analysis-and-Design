using CarRental.Web.Areas.Bookings.Data;
using CarRental.Web.Areas.Bookings.Services;
using CarRental.Web.Areas.RentalSchedule.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace CarRental.Web.Areas.RentalSchedule.Controllers;
[Area("RentalSchedule"),ApiController,Authorize(Policy="BillingAdmin"),Route("api/rental-schedule")]
public sealed class ScheduleController(RentalDbContext db,IBillingService billing):ControllerBase {
 ScheduleService Service=>new(new BookingService(db,billing));
 [HttpGet] public async Task<IActionResult> List()=>Ok(await Service.List());
 [HttpGet("{id}/history")] public async Task<IActionResult> History(string id)=>Ok(await Service.History(id));
 [HttpPost("{id}/adjust")] public async Task<IActionResult> Adjust(string id,ScheduleInput input)=>Ok(await Service.Adjust(id,input,User.FindFirstValue(ClaimTypes.NameIdentifier)!));
}
