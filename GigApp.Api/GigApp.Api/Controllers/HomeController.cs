using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GigApp.Api.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _environment;

        public HomeController(IWebHostEnvironment environment) => _environment = environment;

        // The storefront is the front door now. The old landing page stays at
        // /welcome because it carries the dev sign-in credentials.
        [HttpGet("/")]
        public IActionResult Index() => Redirect("/services");

        [HttpGet("/welcome")]
        public IActionResult Welcome()
        {
            ViewData["Title"] = "Home";
            ViewData["ShowDevCredentials"] = _environment.IsDevelopment();
            return View("Index");
        }

        [HttpGet("/Home/Error")]
        public IActionResult Error()
        {
            ViewData["Title"] = "Something went wrong";
            return View();
        }
    }
}
