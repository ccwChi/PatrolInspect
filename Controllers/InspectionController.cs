using Microsoft.AspNetCore.Mvc;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;
using PatrolInspect.Repository;

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

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // API: 取得使用者今天的巡檢任務
        [HttpGet]
        public async Task<IActionResult> GetTodayInspection()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            var userName = HttpContext.Session.GetString("UserName");
            var department = HttpContext.Session.GetString("DepartmentName");
    
            if (string.IsNullOrEmpty(userNo) || string.IsNullOrWhiteSpace(userName))
            {
                return Json(new { success = false, message = "未登入" });
            }

            try
            {
                var todayInspection = await _inspectionRepository.GetTodayInspectionAsync(userNo, userName!, department!);
                return Json(new { success = true, data = todayInspection });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today inspection for user: {UserNo}", userNo);
                return Json(new { success = false, message = "載入巡檢資料失敗" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessNFCInspection([FromBody] ProcessNFCRequest request)
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            var userName = HttpContext.Session.GetString("UserName");

            if (string.IsNullOrEmpty(userNo))
            {
                return Json(new
                {
                    success = false,
                    message = "未登入",
                    errorCode = "NOT_LOGGED_IN"
                });
            }

            try
            {
                // 步驟1：驗證 NFC 卡片
                if (string.IsNullOrWhiteSpace(request.NfcId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "NFC ID 不能為空",
                        errorCode = "INVALID_INPUT"
                    });
                }

                var nfcCard = await _inspectionRepository.FindNFCcard(request.NfcId);
                if (nfcCard == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "此感應卡不存在於資料庫",
                        errorCode = "CARD_NOT_FOUND"
                    });
                }

                // 步驟2：處理替換確認
                if (request.ConfirmReplace && request.OldRecordId.HasValue)
                {
                    // 用戶已確認替換，更新舊記錄並建立新記錄
                    await _inspectionRepository.UpdateInspectionRecordAsync(request.OldRecordId.Value, userNo);

                    var newRecord = await CreateNewInspectionRecord(request, nfcCard, userNo, userName);

                    return Json(new
                    {
                        success = true,
                        message = $"目前已在 {nfcCard.DeviceId} 機台開始檢驗",
                        recordId = newRecord.recordId,
                        arriveTime = newRecord.arriveTime,
                        deviceId = nfcCard.DeviceId,
                        isExisting = false
                    });
                }

                // 步驟3：檢查是否有未完成的巡檢記錄
                var pendingRecord = await _inspectionRepository.GetPendingInspectionByUserAsync(userNo);
                if (pendingRecord != null)
                {
                    // 如果是同一台機台，直接返回現有記錄
                    if (pendingRecord.DeviceId == nfcCard.DeviceId)
                    {
                        return Json(new
                        {
                            success = true,
                            message = $"目前已在 {nfcCard.DeviceId} 機台開始檢驗",
                            recordId = pendingRecord.RecordId,
                            arriveTime = pendingRecord.ArriveAt.ToString("HH:mm:ss"),
                            deviceId = nfcCard.DeviceId,
                            isExisting = true
                        });
                    }

                    // 如果是不同機台，請求用戶確認
                    return Json(new
                    {
                        success = true,
                        needConfirmation = true,
                        message = $"您在 {pendingRecord.DeviceId} 機台還有未完成的巡檢記錄",
                        pendingDeviceId = pendingRecord.DeviceId,
                        newDeviceId = nfcCard.DeviceId,
                        pendingRecordId = pendingRecord.RecordId,
                        nfcId = request.NfcId // 保留 NFC ID 供確認時使用
                    });
                }

                // 步驟4：建立新的巡檢記錄
                var result = await CreateNewInspectionRecord(request, nfcCard, userNo, userName);

                return Json(new
                {
                    success = true,
                    message = $"目前已在 {nfcCard.DeviceId} 機台開始檢驗",
                    recordId = result.recordId,
                    arriveTime = result.arriveTime,
                    deviceId = nfcCard.DeviceId,
                    isExisting = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing NFC inspection for NfcId: {NfcId} by user: {UserNo}",
                    request.NfcId, userNo);
                return Json(new
                {
                    success = false,
                    message = "處理 NFC 巡檢失敗: " + ex.Message,
                    errorCode = "SYSTEM_ERROR"
                });
            }
        }

        // 輔助方法：建立新巡檢記錄
        private async Task<(int recordId, string arriveTime)> CreateNewInspectionRecord( ProcessNFCRequest request, InspectionDeviceAreaMapping nfcCard, string userNo, string userName)
        {
            var record = new InspectionQcRecord
            {
                CardId = request.NfcId,
                DeviceId = nfcCard.DeviceId,
                UserNo = userNo,
                UserName = userName ?? "",
                InspectType = request.InspectType,
                ArriveAt = DateTime.Now,
                Source = request.Source
            };

            var recordId = await _inspectionRepository.CreateInspectionRecordAsync(record);

            return (recordId, record.ArriveAt.ToString("HH:mm:ss"));
        }



    }
}