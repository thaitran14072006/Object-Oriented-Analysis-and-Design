using CarRental.Web.Areas.Bookings.Services;
using CarRental.Web.Areas.Reviews.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace CarRental.Web.Areas.Reviews.Controllers;
[Area("Reviews"),ApiController,Authorize(Policy="BillingAdmin"),Route("api/reviews")]
public sealed class ReviewsController(RentalDbContext db,IBillingService billing):ControllerBase {
 ReviewService Service=>new(db,new BookingService(db,billing));
 [HttpGet] public async Task<IActionResult> List()=>Ok(await Service.List());
 [HttpPost] public async Task<IActionResult> Create(ReviewInput input){await Service.Create(input,User.FindFirstValue(ClaimTypes.NameIdentifier)!);return Ok(new{success=true});}
 [HttpPut("{id:long}")] public async Task<IActionResult> Resolve(long id,ReviewDecision input){await Service.Resolve(id,input);return Ok(new{success=true});}
}
