using Microsoft.AspNetCore.Mvc;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;

namespace PatrolInspect.Controllers
{
    public class InspectionController : Controller
    {
        private readonly IInspectionRepository _inspectionRepository;
        private readonly ILogger<InspectionController> _logger;

        public InspectionController(IInspectionRepository inspectionRepository, ILogger<InspectionController> logger)
        {
            _inspectionRepository = inspectionRepository;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // 檢查登入狀態
            var userNo = HttpContext.Session.GetString("UserNo");
            var userName = HttpContext.Session.GetString("UserName");

            if (string.IsNullOrEmpty(userNo))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // 取得使用者今天的巡檢任務
                //var userInspection = await _inspectionRepository.GetUserTodayInspectionAsync(userNo);
                //userInspection.UserName = userName ?? "";

                ViewBag.UserNo = userNo;
                ViewBag.UserName = userName;
                ViewBag.DepartmentName = HttpContext.Session.GetString("DepartmentName");
                ViewBag.TitleName = HttpContext.Session.GetString("TitleName");
                ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                return View();
                //return View(userInspection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard for user: {UserNo}", userNo);
                ViewBag.ErrorMessage = "載入巡檢資料時發生錯誤，請稍後再試";
                return View(new UserTodayInspection { UserNo = userNo, UserName = userName ?? "" });
            }
        }

        // API: 取得使用者今天的巡檢任務
        [HttpGet]
        public async Task<IActionResult> GetTodayInspection()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            var userName = HttpContext.Session.GetString("UserName");
            var department = HttpContext.Session.GetString("DepartmentName");
    
            if (string.IsNullOrEmpty(userNo))
            {
                return Json(new { success = false, message = "未登入" });
            }

            try
            {
                var result = await _inspectionRepository.GetTodayInspectionAsync(userNo, userName, department);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today inspection for user: {UserNo}", userNo);
                return Json(new { success = false, message = "載入巡檢資料失敗" });
            }
        }

        // API: 刷新機台狀態
        //[HttpPost]
        //public async Task<IActionResult> RefreshDeviceStatus()
        //{
        //    var userNo = HttpContext.Session.GetString("UserNo");
        //    if (string.IsNullOrEmpty(userNo))
        //    {
        //        return Json(new { success = false, message = "未登入" });
        //    }

        //    try
        //    {
        //        var result = await _inspectionRepository.GetUserTodayInspectionAsync(userNo);
        //        return Json(new { success = true, data = result });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error refreshing device status for user: {UserNo}", userNo);
        //        return Json(new { success = false, message = "刷新失敗" });
        //    }
        //}

        // API: 記錄巡檢到達
        [HttpPost]
        public async Task<IActionResult> RecordInspection([FromBody] InspectionRecordRequest request)
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            var userName = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(userNo))
            {
                return Json(new { success = false, message = "未登入" });
            }

            try
            {
                // 檢查是否有該使用者未完成的巡檢記錄
                var pendingRecord = await _inspectionRepository.GetPendingInspectionByUserAsync(userNo);

                if (pendingRecord != null)
                {
                    // 如果是同一台機台，提示已經在檢驗中
                    if (pendingRecord.DeviceId == request.DeviceId)
                    {
                        return Json(new
                        {
                            success = true,
                            message = $"目前已在 {request.DeviceId} 機台開始檢驗",
                            recordId = pendingRecord.RecordId,
                            arriveTime = pendingRecord.ArriveAt.ToString("HH:mm:ss"),
                            isExisting = true, // 標記這是現有記錄
                            deviceId = request.DeviceId
                        });
                    }

                    // 如果是不同機台，詢問是否要建立新記錄並刪除舊記錄
                    return Json(new
                    {
                        success = false,
                        needConfirmation = true,
                        message = $"您在 {pendingRecord.DeviceId} 機台還有未完成的巡檢記錄，確定要在 {request.DeviceId} 機台開始新的巡檢嗎？",
                        pendingDeviceId = pendingRecord.DeviceId,
                        newDeviceId = request.DeviceId,
                        pendingRecordId = pendingRecord.RecordId
                    });
                }

                // 建立新的巡檢記錄
                var record = new InspectionQcRecord
                {
                    CardId = request.CardId,
                    DeviceId = request.DeviceId,
                    UserNo = userNo,
                    UserName = userName ?? "",
                    InspectType = request.InspectType ?? "INSPECT",
                    ArriveAt = DateTime.Now,
                    Source = request.Source ?? "NFC"
                };

                var recordId = await _inspectionRepository.CreateInspectionRecordAsync(record);

                return Json(new
                {
                    success = true,
                    message = $"目前已在 {request.DeviceId} 機台開始檢驗",
                    recordId = recordId,
                    arriveTime = record.ArriveAt.ToString("HH:mm:ss"),
                    isExisting = false, // 標記這是新記錄
                    deviceId = request.DeviceId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording inspection for device: {DeviceId} by user: {UserNo}",
                    request.DeviceId, userNo);
                return Json(new { success = false, message = "記錄巡檢失敗" });
            }
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }


        [HttpPost]
        public async Task<IActionResult> ConfirmReplaceInspection([FromBody] ConfirmReplaceRequest request)
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            var userName = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(userNo))
            {
                return Json(new { success = false, message = "未登入" });
            }

            try
            {
                // 更新舊的未完成記錄
                await _inspectionRepository.UpdateInspectionRecordAsync(request.OldRecordId, userNo);

                // 建立新記錄
                var record = new InspectionQcRecord
                {
                    CardId = request.CardId,
                    DeviceId = request.NewDeviceId,
                    UserNo = userNo,
                    UserName = userName ?? "",
                    InspectType = request.InspectType ?? "INJECT_INSPECT",
                    ArriveAt = DateTime.Now,
                    Source = request.Source ?? "NFC"
                };

                var recordId = await _inspectionRepository.CreateInspectionRecordAsync(record);

                return Json(new
                {
                    success = true,
                    message = $"目前已在 {request.NewDeviceId} 機台開始檢驗",
                    recordId = recordId,
                    arriveTime = record.ArriveAt.ToString("HH:mm:ss"),
                    isExisting = false,
                    deviceId = request.NewDeviceId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing inspection record for user: {UserNo}", userNo);
                return Json(new { success = false, message = "替換巡檢記錄失敗" });
            }
        }
    }
}