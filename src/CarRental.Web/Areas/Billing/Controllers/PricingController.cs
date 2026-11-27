using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CarRental.Web.Areas.Billing.Controllers;

[ApiController, Route("api/billing/vehicles"), Authorize(Policy = "Pricing.Read")]
public class PricingController(RentalDbContext db) : ControllerBase
{
    [HttpGet] public Task<List<PricedVehicle>> Find(DateTime start, DateTime end, decimal min = 0, decimal max = 1000000000, string unit = "NGAY", string form = "NGAN_HAN", string season = "MAC_DINH") => new PricingSearch(db).Find(start, end, min, max, unit, form, season);
}
