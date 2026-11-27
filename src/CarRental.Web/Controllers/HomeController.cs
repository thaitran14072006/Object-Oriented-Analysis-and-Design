using CarRental.Web.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Web.Controllers;

public sealed class HomeController(IWebHostEnvironment environment) : Controller
{
    [HttpGet("/"), Authorize(Policy = AdminAccess.Admin)]
    public IActionResult Index() => Frontend();

    [HttpGet("/login"), AllowAnonymous]
    public IActionResult Login() => Frontend();

    private IActionResult Frontend()
    {
        var index = environment.WebRootFileProvider.GetFileInfo("frontend/index.html");
        if (!index.Exists)
        {
            return Problem("Chạy npm install và npm run build trong thư mục Frontend để tạo giao diện.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        Response.Headers.CacheControl = "no-store";
        return File(index.CreateReadStream(), "text/html; charset=utf-8");
    }
}
