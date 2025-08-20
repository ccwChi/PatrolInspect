using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using PatrolInspect.Models;

namespace PatrolInspect.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // 預設導向登入頁面
            return RedirectToAction("Login", "Account");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}