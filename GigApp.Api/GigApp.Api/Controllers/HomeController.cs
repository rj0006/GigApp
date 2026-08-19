using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _environment;

        public HomeController(IWebHostEnvironment environment) => _environment = environment;

        [HttpGet("/")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Home";
            ViewData["ShowDevCredentials"] = _environment.IsDevelopment();
            return View();
        }

        [HttpGet("/Home/Error")]
        public IActionResult Error()
        {
            ViewData["Title"] = "Something went wrong";
            return View();
        }
    }
}
