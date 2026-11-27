using Microsoft.AspNetCore.Mvc;

namespace CarRental.Web.Controllers;

public sealed class HomeController(IWebHostEnvironment environment) : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        var index = environment.WebRootFileProvider.GetFileInfo("frontend/index.html");
        if (!index.Exists)
        {
            return Problem("Chạy npm install và npm run build trong thư mục Frontend để tạo giao diện.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return File(index.CreateReadStream(), "text/html; charset=utf-8");
    }
}
