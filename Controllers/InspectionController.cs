using Microsoft.AspNetCore.Mvc;
using PatrolInspect.Models;

namespace PatrolInspect.Controllers
{
    public class InspectionController : Controller
    {
        private readonly ILogger<InspectionController> _logger;

        public InspectionController(ILogger<InspectionController> logger)
        {
            _logger = logger;
        }

        // 主要量測頁面
        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                // 檢查登入狀態
                if (!IsUserLoggedIn())
                {
                    _logger.LogWarning("Unauthorized access attempt to inspection page");
                    return RedirectToAction("Login", "Account");
                }

                // 準備使用者資訊
                var userInfo = GetCurrentUserInfo();

                ViewBag.UserNo = userInfo.UserNo;
                ViewBag.UserName = userInfo.UserName;
                ViewBag.DepartmentName = userInfo.DepartmentName;
                ViewBag.TitleName = userInfo.TitleName;
                ViewBag.LoginTime = userInfo.LoginTime;
                ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                _logger.LogInformation("User {UserNo} accessed inspection page", userInfo.UserNo);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Inspection Index");
                return RedirectToAction("Error", "Home");
            }
        }

        // Dashboard（保留原有功能）
        [HttpGet]
        public IActionResult Dashboard()
        {
            try
            {
                // 檢查登入狀態
                if (!IsUserLoggedIn())
                {
                    return RedirectToAction("Login", "Account");
                }

                var userInfo = GetCurrentUserInfo();

                ViewBag.UserNo = userInfo.UserNo;
                ViewBag.UserName = userInfo.UserName;
                ViewBag.DepartmentName = userInfo.DepartmentName;
                ViewBag.TitleName = userInfo.TitleName;
                ViewBag.LoginTime = userInfo.LoginTime;
                ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Dashboard");
                return RedirectToAction("Error", "Home");
            }
        }

        // NFC 掃描記錄
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordNfcScan([FromBody] NfcScanRequest request)
        {
            try
            {
                if (!IsUserLoggedIn())
                {
                    return Json(new { success = false, message = "請重新登入" });
                }

                if (request == null || string.IsNullOrWhiteSpace(request.CardId))
                {
                    return Json(new { success = false, message = "無效的掃描資料" });
                }

                var userInfo = GetCurrentUserInfo();

                // TODO: 實作 NFC 掃描記錄邏輯
                // 1. 驗證 CardId 是否有效
                // 2. 檢查是否重複掃描
                // 3. 記錄到資料庫

                _logger.LogInformation("NFC scan recorded: User {UserNo}, CardId {CardId}, DeviceId {DeviceId}",
                    userInfo.UserNo, request.CardId, request.DeviceId);

                // 模擬處理
                await Task.Delay(500); // 模擬資料庫操作

                return Json(new
                {
                    success = true,
                    message = "掃描記錄成功",
                    timestamp = DateTime.Now,
                    cardId = request.CardId,
                    deviceId = request.DeviceId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording NFC scan");
                return Json(new { success = false, message = "記錄失敗，請稍後再試" });
            }
        }

        // API: 取得使用者負責的設備
        [HttpGet]
        public async Task<IActionResult> GetUserDevices(string userNo)
        {
            try
            {
                if (!IsUserLoggedIn())
                {
                    return Json(new { success = false, message = "請重新登入" });
                }

                // TODO: 實作從資料庫取得使用者負責設備的邏輯
                // 這裡先回傳模擬資料

                var mockDevices = new[]
                {
                    new
                    {
                        deviceId = 1,
                        deviceCode = "T100",
                        deviceName = "100T射出機",
                        areaName = "射出成型區A1",
                        cardId = "NFC001",
                        lastInspection = DateTime.Now.AddHours(-2),
                        lastInspector = "王小明",
                        inspectionInterval = 240,
                        inspectionStatus = "normal"
                    },
                    new
                    {
                        deviceId = 2,
                        deviceCode = "T200",
                        deviceName = "200T射出機",
                        areaName = "射出成型區A1",
                        cardId = "NFC002",
                        lastInspection = DateTime.Now.AddHours(-3.5),
                        lastInspector = "李美華",
                        inspectionInterval = 240,
                        inspectionStatus = "warning"
                    },
                    new
                    {
                        deviceId = 3,
                        deviceCode = "T150",
                        deviceName = "150T射出機",
                        areaName = "射出成型區A2",
                        cardId = "NFC003",
                        lastInspection = DateTime.Now.AddHours(-5),
                        lastInspector = "張三豐",
                        inspectionInterval = 240,
                        inspectionStatus = "danger"
                    }
                };

                await Task.Delay(100); // 模擬資料庫查詢延遲

                return Json(new { success = true, data = mockDevices });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user devices for {UserNo}", userNo);
                return Json(new { success = false, message = "載入設備資訊失敗" });
            }
        }

        // 登出
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            if (!string.IsNullOrEmpty(userNo))
            {
                _logger.LogInformation("User logged out from inspection page: {UserNo}", userNo);
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // 私有方法：檢查使用者是否已登入
        private bool IsUserLoggedIn()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            return !string.IsNullOrEmpty(userNo);
        }

        // 私有方法：取得目前使用者資訊
        private (string UserNo, string UserName, string DepartmentName, string TitleName, string LoginTime) GetCurrentUserInfo()
        {
            return (
                UserNo: HttpContext.Session.GetString("UserNo") ?? "",
                UserName: HttpContext.Session.GetString("UserName") ?? "",
                DepartmentName: HttpContext.Session.GetString("DisplayDepartment") ?? HttpContext.Session.GetString("DepartmentName") ?? "",
                TitleName: HttpContext.Session.GetString("TitleName") ?? "",
                LoginTime: HttpContext.Session.GetString("LoginTime") ?? ""
            );
        }
    }

    // NFC 掃描請求模型
    public class NfcScanRequest
    {
        public string CardId { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public DateTime? ScanTime { get; set; }
        public string Source { get; set; } = "NFC";
    }
}