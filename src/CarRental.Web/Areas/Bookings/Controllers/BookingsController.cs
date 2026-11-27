using CarRental.Web.Areas.Bookings.Data;
using CarRental.Web.Areas.Bookings.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace CarRental.Web.Areas.Bookings.Controllers;
[Area("Bookings"), ApiController, Authorize(Policy="BillingAdmin"), Route("api/bookings")]
public sealed class BookingsController(RentalDbContext db,IBillingService billing):ControllerBase {
 BookingService Service=>new(db,billing);
 [HttpGet] public async Task<IActionResult> List()=>Ok(await Service.List());
 [HttpGet("{id}")] public async Task<IActionResult> Get(string id)=>Ok(await Service.Get(id));
 [HttpPost] public async Task<IActionResult> Create(BookingInput input)=>Ok(await Service.Create(input,User.FindFirstValue(ClaimTypes.NameIdentifier)!));
 [HttpPost("{id}/state")] public async Task<IActionResult> State(string id,ChangeState input)=>Ok(await Service.State(id,input));
}
