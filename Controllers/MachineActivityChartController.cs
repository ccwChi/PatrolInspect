using Microsoft.AspNetCore.Mvc;

namespace PatrolInspect.Controllers
{
    public class MachineActivityChartController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
