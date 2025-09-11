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
        private async Task<(int recordId, string arriveTime)> CreateNewInspectionRecord(
            ProcessNFCRequest request,
            InspectionDeviceAreaMapping nfcCard,
            string userNo,
            string userName)
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
        // API: 記錄巡檢到達
        //[HttpPost]
        //public async Task<IActionResult> RecordInspection([FromBody] InspectionRecordRequest request)
        //{
        //    var userNo = HttpContext.Session.GetString("UserNo");
        //    var userName = HttpContext.Session.GetString("UserName");
        //    if (string.IsNullOrEmpty(userNo))
        //    {
        //        return Json(new { success = false, message = "未登入" });
        //    }

        //    try
        //    {
        //        // 檢查是否有該使用者未完成的巡檢記錄
        //        var pendingRecord = await _inspectionRepository.GetPendingInspectionByUserAsync(userNo);

        //        if (pendingRecord != null)
        //        {
        //            // 如果是同一台機台，提示已經在檢驗中
        //            if (pendingRecord.DeviceId == request.DeviceId)
        //            {
        //                return Json(new
        //                {
        //                    success = true,
        //                    message = $"目前已在 {request.DeviceId} 機台開始檢驗",
        //                    recordId = pendingRecord.RecordId,
        //                    arriveTime = pendingRecord.ArriveAt.ToString("HH:mm:ss"),
        //                    isExisting = true, // 標記這是現有記錄
        //                    deviceId = request.DeviceId
        //                });
        //            }

        //            // 如果是不同機台，詢問是否要建立新記錄並刪除舊記錄
        //            return Json(new
        //            {
        //                success = false,
        //                needConfirmation = true,
        //                message = $"您在 {pendingRecord.DeviceId} 機台還有未完成的巡檢記錄，確定要在 {request.DeviceId} 機台開始新的巡檢嗎？",
        //                pendingDeviceId = pendingRecord.DeviceId,
        //                newDeviceId = request.DeviceId,
        //                pendingRecordId = pendingRecord.RecordId
        //            });
        //        }

        //        // 建立新的巡檢記錄
        //        var record = new InspectionQcRecord
        //        {
        //            CardId = request.CardId,
        //            DeviceId = request.DeviceId,
        //            UserNo = userNo,
        //            UserName = userName ?? "",
        //            InspectType = request.InspectType ?? "INSPECT",
        //            ArriveAt = DateTime.Now,
        //            Source = request.Source ?? "NFC"
        //        };

        //        var recordId = await _inspectionRepository.CreateInspectionRecordAsync(record);

        //        return Json(new
        //        {
        //            success = true,
        //            message = $"目前已在 {request.DeviceId} 機台開始檢驗",
        //            recordId = recordId,
        //            arriveTime = record.ArriveAt.ToString("HH:mm:ss"),
        //            isExisting = false, // 標記這是新記錄
        //            deviceId = request.DeviceId
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error recording inspection for device: {DeviceId} by user: {UserNo}",
        //            request.DeviceId, userNo);
        //        return Json(new { success = false, message = "記錄巡檢失敗" });
        //    }
        //}




        //[HttpPost]
        //public async Task<IActionResult> ConfirmReplaceInspection([FromBody] ConfirmReplaceRequest request)
        //{
        //    var userNo = HttpContext.Session.GetString("UserNo");
        //    var userName = HttpContext.Session.GetString("UserName");
        //    if (string.IsNullOrEmpty(userNo))
        //    {
        //        return Json(new { success = false, message = "未登入" });
        //    }

        //    try
        //    {
        //        // 更新舊的未完成記錄
        //        await _inspectionRepository.UpdateInspectionRecordAsync(request.OldRecordId, userNo);

        //        // 建立新記錄
        //        var record = new InspectionQcRecord
        //        {
        //            CardId = request.CardId,
        //            DeviceId = request.NewDeviceId,
        //            UserNo = userNo,
        //            UserName = userName ?? "",
        //            InspectType = request.InspectType ?? "INJECT_INSPECT",
        //            ArriveAt = DateTime.Now,
        //            Source = request.Source ?? "NFC"
        //        };

        //        var recordId = await _inspectionRepository.CreateInspectionRecordAsync(record);

        //        return Json(new
        //        {
        //            success = true,
        //            message = $"目前已在 {request.NewDeviceId} 機台開始檢驗",
        //            recordId = recordId,
        //            arriveTime = record.ArriveAt.ToString("HH:mm:ss"),
        //            isExisting = false,
        //            deviceId = request.NewDeviceId
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error replacing inspection record for user: {UserNo}", userNo);
        //        return Json(new { success = false, message = "替換巡檢記錄失敗" });
        //    }
        //}


        //[HttpGet]
        //public async Task<IActionResult> findNFCcard(string nfcId)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(nfcId))
        //        {
        //            return Json(new { success = false, message = "NFC ID 不能為空" });
        //        }

        //        var nfcCard = await _inspectionRepository.FindNFCcard(nfcId);

        //        if (nfcCard != null) // 需要檢查 null
        //        {
        //            return Json(new
        //            {
        //                success = true,
        //                data = nfcCard
        //            });
        //        }
        //        else
        //        {
        //            return Json(new
        //            {
        //                success = false,
        //                message = "找不到對應的 NFC 卡片"
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error getting today inspection for nfcId: {nfcId}", nfcId);
        //        return Json(new { success = false, message = "查詢感應卡失敗" });
        //    }
        //}


    }
}