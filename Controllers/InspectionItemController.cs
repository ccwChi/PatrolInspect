using Microsoft.AspNetCore.Mvc;
using PatrolInspect.Models;

namespace PatrolInspect.Controllers
{
    public class InspectionItemController : Controller
    {
        private readonly ILogger<InspectionController> _logger;

        public InspectionItemController(ILogger<InspectionController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // 檢查登入狀態
            var userNo = HttpContext.Session.GetString("UserNo");
            if (string.IsNullOrEmpty(userNo))
                return RedirectToAction("Login", "Account");

            // 設定使用者基本資訊到 ViewBag
            ViewBag.UserNo = userNo;
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.DepartmentName = HttpContext.Session.GetString("DepartmentName");
            ViewBag.TitleName = HttpContext.Session.GetString("TitleName");
            ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 設定巡檢統計數據
            ViewBag.TotalRecords = 55;
            ViewBag.TodayRecords = 12;
            ViewBag.CompletedRecords = 45;
            ViewBag.InProgressRecords = 8;
            ViewBag.AbnormalRecords = 2;

            // 設定最近巡檢記錄
            ViewBag.RecentRecords = GetMockInspectionRecords().Take(8).ToList();

            return View();
        }

        [HttpPost]
        public IActionResult Logout()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            if (!string.IsNullOrEmpty(userNo))
            {
                _logger.LogInformation("User logged out: {UserNo}", userNo);
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // 取得模擬巡檢記錄數據
        private List<InspectionRecord> GetMockInspectionRecords()
        {
            var random = new Random(42);
            var records = new List<InspectionRecord>();

            var mockData = new[]
            {
                new { Category = "光罩", Station = "RFID燒錄", WorkOrder = "1700030149", PartNumber = "9001-0003920", Inspector = "徐伊君", Status = "已完成" },
                new { Category = "晶圓", Station = "Q包前Q檢", WorkOrder = "1700030117", PartNumber = "9434-0002470", Inspector = "徐伊君", Status = "已完成" },
                new { Category = "晶圓", Station = "Q包", WorkOrder = "1700030117", PartNumber = "9434-0002470", Inspector = "陳溍鴻", Status = "進行中" },
                new { Category = "晶圓", Station = "組裝", WorkOrder = "1300030190", PartNumber = "9434-0001080", Inspector = "陳溍鴻", Status = "已完成" },
                new { Category = "晶圓", Station = "組裝", WorkOrder = "1700030079", PartNumber = "9434-0001450", Inspector = "陳溍鴻", Status = "已完成" },
                new { Category = "晶圓", Station = "Q包", WorkOrder = "1300030173", PartNumber = "9434-0001080", Inspector = "陳溍鴻", Status = "異常" },
                new { Category = "晶圓", Station = "重量量測", WorkOrder = "1700030132", PartNumber = "9434-0001700", Inspector = "徐伊君", Status = "已完成" },
                new { Category = "光罩", Station = "組裝", WorkOrder = "1700030149", PartNumber = "9001-0003920", Inspector = "徐伊君", Status = "進行中" },
                new { Category = "晶圓", Station = "RFID燒錄", WorkOrder = "1700030110", PartNumber = "9434-0001700", Inspector = "徐伊君", Status = "已完成" },
                new { Category = "晶圓", Station = "Q包", WorkOrder = "1300030204", PartNumber = "9434-0000370", Inspector = "陳溍鴻", Status = "已完成" }
            };

            for (int i = 0; i < mockData.Length; i++)
            {
                var data = mockData[i];
                records.Add(new InspectionRecord
                {
                    RecordId = i + 1,
                    Category = data.Category,
                    Station = data.Station,
                    WorkOrder = data.WorkOrder,
                    PartNumber = data.PartNumber,
                    Inspector = data.Inspector,
                    Status = data.Status,
                    InspectionTime = DateTime.Now.AddHours(-random.Next(1, 48))
                });
            }

            return records.OrderByDescending(r => r.InspectionTime).ToList();
        }
    }
}