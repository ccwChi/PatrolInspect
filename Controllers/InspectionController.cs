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


        

        [HttpGet]
        public async Task<IActionResult> GetInspectTypeList()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            if (string.IsNullOrEmpty(userNo))
            {
                return Json(new { success = false, message = "未登入" });
            }

            try
            {
                var InspectTypeList = await _inspectionRepository.GetInspectTypeList();

                return Json(new
                {
                    success = true,
                    data = InspectTypeList
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Areas Users InspectType");
                return Json(new { success = false, message = "載入基礎資料失敗" });
            }
        }
        //[HttpPost]
        //public IActionResult Logout()
        //{
        //    HttpContext.Session.Clear();
        //    return RedirectToAction("Login", "Account");
        //}

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

            if (string.IsNullOrEmpty(userNo) || string.IsNullOrEmpty(userName))
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

                var nfcList = await _inspectionRepository.FindNFCcard(request.NfcId);
                if (nfcList == null || !nfcList.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "此感應卡不存在於資料庫",
                        errorCode = "CARD_NOT_FOUND"
                    });
                }

                var firstNfcInfo = nfcList.First();

                // 步驟2：處理替換確認
                if (request.ConfirmReplace && request.OldRecordId.HasValue)
                {
                    // 用戶已確認替換，更新舊記錄並建立新記錄
                    await _inspectionRepository.UpdateInspectionRecordAsync(request.OldRecordId.Value, firstNfcInfo.DeviceId , userNo);

                    var firstArriveTime = await CreateNewInspectionRecord(request, nfcList, request.WorkOrderNo, userNo, userName);

                    return Json(new
                    {
                        success = true,
                        message = $"目前已在 {firstNfcInfo.DeviceId} 機台開始檢驗",
                        arriveTime =firstArriveTime ,
                        deviceId = firstNfcInfo.DeviceId,
                        isExisting = false
                    });
                }

                // 步驟3：檢查是否有未完成的巡檢記錄
                var pendingRecord = await _inspectionRepository.GetPendingInspectionByUserAsync(userNo);
                if (pendingRecord?.Any() == true)
                {
                    var firstPendingRecord = pendingRecord.First();

                    var recordIds = string.Join(",", pendingRecord.Select(x => x.RecordId));
                    var workOrders = string.Join(",", pendingRecord.Select(x => x.InspectWo));
                    // 如果是同一台機台，直接返回現有記錄
                    if (firstPendingRecord.DeviceId == firstNfcInfo.DeviceId)
                    {
                        return Json(new
                        {
                            success = true,
                            message = $"目前已在 {firstPendingRecord.DeviceId} 檢驗",
                            arriveTime = firstPendingRecord.ArriveAt.ToString("HH:mm:ss"),
                            deviceId = firstNfcInfo.DeviceId,
                            workOrders,
                            isExisting = true
                        });
                    }

                    if (string.IsNullOrWhiteSpace(firstPendingRecord.DeviceId))
                    {
                        return Json(new
                        {
                            success = true,
                            needConfirmation = true,
                            message = $"您在 {firstPendingRecord.InspectType}, {workOrders} 還有未完成的巡檢記錄",
                            pendingDeviceId = firstNfcInfo.DeviceId,
                            newDeviceId = firstNfcInfo.DeviceId,
                            pendingRecordId = firstPendingRecord.RecordId,
                            nfcId = request.NfcId // 保留 NFC ID 供確認時使用
                        });
                    }
                    // 如果是不同機台，請求用戶確認
                    return Json(new
                    {
                        success = true,
                        needConfirmation = true,
                        message = $"您在 {firstNfcInfo.DeviceId} 機台 {workOrders} 還有未完成的巡檢記錄",
                        pendingDeviceId = firstNfcInfo.DeviceId,
                        newDeviceId = firstNfcInfo.DeviceId,
                        pendingRecordId = firstPendingRecord.RecordId,
                        nfcId = request.NfcId // 保留 NFC ID 供確認時使用
                    });
                }

                // 步驟4：建立新的巡檢記錄
                var result = await CreateNewInspectionRecord(request, nfcList, request.WorkOrderNo, userNo, userName);

                return Json(new
                {
                    success = true,
                    message = $"目前已在 {firstNfcInfo.DeviceId} 機台開始檢驗",
                    arriveTime = result,
                    deviceId = firstNfcInfo.DeviceId,
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


        private async Task<string> CreateNewInspectionRecord(ProcessNFCRequest request, List<InspectionDeviceAreaMappingDto> nfcInfo,
            string userInputWorkOrderNo, string userNo, string userName)
        {
            string? firstArriveTime = null;

            foreach (var item in nfcInfo)
            {
                var record = new InspectionQcRecord
                {
                    CardId = request.NfcId,
                    DeviceId = item.DeviceId,
                    UserNo = userNo,
                    UserName = userName ?? "",
                    Area = item.Area,
                    InspectType = request.InspectType,
                    InspectWo = item.InspectWo,
                    ArriveAt = DateTime.Now,
                    Source = request.Source,
                    UserInputWorkOrderNo = userInputWorkOrderNo
                };

                await _inspectionRepository.CreateInspectionRecordAsync(record);

                // 記錄第一筆的時間
                if (firstArriveTime == null)
                {
                    firstArriveTime = record.ArriveAt.ToString("HH:mm:ss");
                }
            }

            return firstArriveTime ?? DateTime.Now.ToString("HH:mm:ss");
        }



        [HttpPost]
        public async Task<IActionResult> UpdateInspectionQuantity([FromBody] UpdateQuantityRequest request)
        {
            try
            {
                // 檢查登入狀態
                var userNo = HttpContext.Session.GetString("UserNo");
                if (string.IsNullOrEmpty(userNo))
                {
                    return Json(new { success = false, message = "請重新登入" });
                }

                // 驗證輸入
                if (request.RecordId <= 0)
                {
                    return Json(new { success = false, message = "無效的記錄ID" });
                }

                if (request.OkQuantity < 0 || request.NgQuantity < 0)
                {
                    return Json(new { success = false, message = "數量不能為負數" });
                }


                // 更新檢驗數量
                var result = await _inspectionRepository.UpdateInspectionQuantityAsync(
                    request.RecordId,
                    request.OkQuantity,
                    request.NgQuantity,
                    userNo
                );

                dynamic repositoryResult = result;

                if (repositoryResult.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = "檢驗數量更新成功",
                        data = new
                        {
                            recordId = request.RecordId,
                            okQuantity = request.OkQuantity,
                            ngQuantity = request.NgQuantity,
                            updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        }
                    });
                }
                else
                {
                    return Json(new { success = false, message = repositoryResult.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新檢驗數量時發生錯誤: RecordId={RecordId}", request.RecordId);
                return Json(new { success = false, message = "系統錯誤，請稍後再試" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitWarehouseInspection([FromBody] SubmitWarehouseRequest request)
        {
            try
            {
                // 檢查登入狀態
                var userNo = HttpContext.Session.GetString("UserNo");
                var userName = HttpContext.Session.GetString("UserName");

                if (string.IsNullOrEmpty(userNo) || string.IsNullOrEmpty(userName))
                {
                    return Json(new { success = false, message = "請重新登入" });
                }

                // 驗證輸入
                if (request.RecordId <= 0)
                {
                    return Json(new { success = false, message = "無效的記錄ID" });
                }

                if (request.Orders == null || !request.Orders.Any())
                {
                    return Json(new { success = false, message = "請至少新增一筆工單資料" });
                }

                // 驗證每筆工單資料
                foreach (var order in request.Orders)
                {
                    if (string.IsNullOrWhiteSpace(order.WorkOrder))
                    {
                        return Json(new { success = false, message = "工單號碼不能為空" });
                    }

                    if (order.OkQuantity < 0 || order.NgQuantity < 0)
                    {
                        return Json(new { success = false, message = "數量不能為負數" });
                    }

                    if (order.OkQuantity == 0 && order.NgQuantity == 0)
                    {
                        return Json(new { success = false, message = $"工單 {order.WorkOrder} 的OK數量和NG數量不能都為0" });
                    }
                }

                // 呼叫Repository處理入庫檢驗
                var result = await _inspectionRepository.SubmitWarehouseInspectionAsync(request.RecordId, request.Orders, userNo
                );

                if (result.Success)
                {
                    return Json(new
                    {
                        success = true,
                        message = "入庫檢驗提交成功",
                        data = new
                        {
                            recordId = request.RecordId,
                            processedOrders = request.Orders.Count,
                            updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        }
                    });
                }
                else
                {
                    return Json(new { success = false, message = result.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提交入庫檢驗時發生錯誤: RecordId={RecordId}", request.RecordId);
                return Json(new { success = false, message = "系統錯誤，請稍後再試" });
            }
        }



    }
}