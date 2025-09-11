using Microsoft.AspNetCore.Mvc;

namespace PatrolInspect.Controllers
{
    public class NFCManageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
