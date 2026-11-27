using CarRental.Web.Areas.Customers.Services;
using CarRental.Web.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Web.Areas.Customers.Controllers;

[ApiController, Area("Customers"), Route("api/customers")]
[Authorize(Policy = AdminAccess.CustomersRead)]
public sealed class CustomersController(CustomerService customers) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Search(string? q, CancellationToken token, int page = 1) =>
        Ok(await customers.SearchAsync(q, page, token));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken token) => Ok(await customers.GetAsync(id, token));

    [HttpGet("groups")]
    public async Task<IActionResult> Groups(CancellationToken token) => Ok(await customers.GroupsAsync(token));

    [HttpPut("groups"), Authorize(Policy = AdminAccess.CustomersEdit)]
    public async Task<IActionResult> Group(CustomerGroup input, CancellationToken token)
    {
        await customers.SaveGroupAsync(User.AccountId(), input, token);
        return NoContent();
    }

    [HttpPost, Authorize(Policy = AdminAccess.CustomersEdit)]
    public async Task<IActionResult> Create(NewCustomer input, CancellationToken token)
    {
        var id = await customers.CreateAsync(User.AccountId(), input, token);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id}/profile"), Authorize(Policy = AdminAccess.CustomersEdit)]
    public async Task<IActionResult> Profile(string id, CustomerContact input, CancellationToken token)
    {
        await customers.UpdateProfileAsync(User.AccountId(), id, input, token);
        return NoContent();
    }

    [HttpPut("{id}/license"), Authorize(Policy = AdminAccess.CustomersEdit)]
    public async Task<IActionResult> License(string id, CustomerLicense input, CancellationToken token)
    {
        await customers.UpdateLicenseAsync(User.AccountId(), id, input, token);
        return NoContent();
    }

    [HttpPut("{id}/rating"), Authorize(Policy = AdminAccess.CustomersRate)]
    public async Task<IActionResult> Rating(string id, RatingInput input, CancellationToken token)
    {
        await customers.RateAsync(User.AccountId(), id, input, token);
        return NoContent();
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> History(string id, CancellationToken token) =>
        Ok(await customers.HistoryAsync(id, token));
}
