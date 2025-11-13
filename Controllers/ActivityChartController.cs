using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;
using PatrolInspect.Repository;

namespace PatrolInspect.Controllers
{
    public class ActivityChartController : Controller
    {
        private readonly ActivityChartRepository _activityChartRepository;
        private readonly ILogger<ActivityChartController> _logger;

        public ActivityChartController(
            ActivityChartRepository userActivityRepository,
            ILogger<ActivityChartController> logger)
        {
            _activityChartRepository = userActivityRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> User(DateTime? date)
        {
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.DepartmentName = HttpContext.Session.GetString("DepartmentName");
            ViewBag.TitleName = HttpContext.Session.GetString("TitleName");
            ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var selectedDate = date ?? DateTime.Today;

            var workdayStart = selectedDate.Date.AddHours(7);
            var workdayEnd = selectedDate.Date.AddDays(1).AddHours(8);
            var displayStart = selectedDate.Date.AddHours(7);
            var displayEnd = workdayEnd;

            var viewModel = new ActivityChartViewModel
            {
                SelectedDate = selectedDate,
                UserActivities = new List<UserActivityViewModel>()
            };

            try
            {
                var validWorkingHoursTypes = await _activityChartRepository.GetActiveValidWorkTypesAsync();
                ViewBag.ValidWorkingHoursTypes = validWorkingHoursTypes;

                var activities = await _activityChartRepository.GetActivitiesByDateRangeAsync(workdayStart, workdayEnd);


                if (activities != null && activities.Any())
                {
                    var userActivities = activities
                        .GroupBy(a => new { a.UserNo, a.UserName })
                        .Select(g =>
                        {
                            var timeGroups = g
                                .Where(a => a.SubmitDataAt.HasValue)
                                .GroupBy(a => new { a.ArriveAt, a.SubmitDataAt })
                                .ToList();

                            var userActivity = new UserActivityViewModel
                            {
                                UserNo = g.Key.UserNo,
                                UserName = g.Key.UserName,
                                Activities = g.OrderBy(a => a.ArriveAt).ToList(),
                                TotalWorkingMinutes = 460
                            };

                            // 計算有效工時 - 先按時間段分組，再檢查該時間段是否有有效工時類型
                            // 在計算有效工時時使用
                            var validRecords = g.Where(a =>
                                a.SubmitDataAt.HasValue &&
                                validWorkingHoursTypes.Contains(a.InspectType)
                            ).ToList();

                            // 需要去重的類型（入庫檢驗、全檢）
                            var dedupeTypes = new[] { "入庫檢驗" };
                            var needDedupeRecords = validRecords
                                .Where(a => dedupeTypes.Contains(a.InspectType) || a.InspectType.Contains("全檢"))
                                .GroupBy(a => new DateTime(a.ArriveAt.Year, a.ArriveAt.Month, a.ArriveAt.Day,
                                                          a.ArriveAt.Hour, a.ArriveAt.Minute, a.ArriveAt.Second))
                                .Select(group => group.First())
                                .ToList();

                            // 不需要去重的類型
                            var normalRecords = validRecords
                                .Where(a => !dedupeTypes.Contains(a.InspectType) && !a.InspectType.Contains("全檢"))
                                .ToList();

                            // 計算有效工時（扣除休息時間）
                            userActivity.ValidWorkingMinutes =
                                needDedupeRecords.Sum(a => CalculateWorkingMinutesExcludingBreaks(a.ArriveAt, a.SubmitDataAt!.Value)) +
                                normalRecords.Sum(a => CalculateWorkingMinutesExcludingBreaks(a.ArriveAt, a.SubmitDataAt!.Value));


                            return userActivity;
                        })
                        .OrderBy(u => u.UserNo)
                        .ToList();

                    ViewBag.DisplayStart = displayStart;
                    ViewBag.DisplayEnd = displayEnd;
                    ViewBag.WorkdayStart = workdayStart;
                    ViewBag.WorkdayEnd = workdayEnd;
                    viewModel.UserActivities = userActivities;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading activity chart for date: {Date}", selectedDate);
                TempData["ErrorMessage"] = $"載入稼動表時發生錯誤: {ex.Message}";
                return View(viewModel);
            }
        }

       
        #region　 //////　　為了計算有效工時，要減去非上班時間的邏輯　　///////

        public class BreakTimeRange
        {
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
        }

        // 在 Controller 開頭定義休息時段
        private readonly List<BreakTimeRange> _breakTimes = new List<BreakTimeRange>
        {
            new BreakTimeRange { Start = new TimeSpan(05, 00, 0), End = new TimeSpan(08, 00, 0) },  
            new BreakTimeRange { Start = new TimeSpan(10, 00, 0), End = new TimeSpan(10, 10, 0) }, 
            new BreakTimeRange { Start = new TimeSpan(12, 0, 0), End = new TimeSpan(13, 0, 0) },  
            new BreakTimeRange { Start = new TimeSpan(15, 0, 0), End = new TimeSpan(15, 10, 0) },  
            new BreakTimeRange { Start = new TimeSpan(17, 00, 0), End = new TimeSpan(20, 00, 0) },  
            new BreakTimeRange { Start = new TimeSpan(12, 00, 0), End = new TimeSpan(13, 00, 0) }  
        };

        // 計算扣除休息時間後的實際工時
        private double CalculateWorkingMinutesExcludingBreaks(DateTime arriveAt, DateTime submitDataAt)
        {
            var totalMinutes = (submitDataAt - arriveAt).TotalMinutes;
            var breakMinutes = 0.0;

            foreach (var breakTime in _breakTimes)
            {
                // 將 DateTime 轉為當天的 TimeSpan
                var recordStart = arriveAt.TimeOfDay;
                var recordEnd = submitDataAt.TimeOfDay;

                // 如果跨日，submitDataAt 的 TimeOfDay 可能比 arriveAt 小，需特殊處理
                if (recordEnd < recordStart)
                {
                    // 跨日情況：分段計算
                    var overlapMinutes1 = CalculateOverlapMinutes(recordStart, new TimeSpan(24, 0, 0), breakTime.Start, breakTime.End);
                    var overlapMinutes2 = CalculateOverlapMinutes(new TimeSpan(0, 0, 0), recordEnd, breakTime.Start, breakTime.End);
                    breakMinutes += overlapMinutes1 + overlapMinutes2;
                    //// 第一段：arriveAt 到當日結束
                    //var overlapMinutes1 = CalculateOverlapMinutes(recordStart, new TimeSpan(24, 0, 0), breakTime.Start, breakTime.End);
                    //// 第二段：隔日開始到 submitDataAt
                    //var overlapMinutes2 = CalculateOverlapMinutes(new TimeSpan(0, 0, 0), recordEnd, breakTime.Start, breakTime.End);
                    //breakMinutes += overlapMinutes1 + overlapMinutes2;
                }
                else
                {
                    // 正常情況：同一天內
                    var overlapMinutes = CalculateOverlapMinutes(recordStart, recordEnd, breakTime.Start, breakTime.End);
                    breakMinutes += overlapMinutes;
                }
            }

            return Math.Max(0, totalMinutes - breakMinutes);
        }

        // 計算兩個時段的重疊分鐘數
        private double CalculateOverlapMinutes(TimeSpan recordStart, TimeSpan recordEnd, TimeSpan breakStart, TimeSpan breakEnd)
        {
            // 計算重疊區間
            var overlapStart = recordStart > breakStart ? recordStart : breakStart;
            var overlapEnd = recordEnd < breakEnd ? recordEnd : breakEnd;

            // 如果有重疊
            if (overlapStart < overlapEnd)
            {
                return (overlapEnd - overlapStart).TotalMinutes;
            }

            return 0;
        }

        #endregion



        [HttpGet]
        public async Task<IActionResult> DownloadUserCsv(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;

            try
            {
                var activities = await _activityChartRepository.GetActivitiesByDateAsync(selectedDate);

                if (activities == null || !activities.Any())
                {
                    return Content("查無資料，無法下載。");
                }

                // 1. 依人員分組
                var grouped = activities
                    .GroupBy(a => new { a.UserNo, a.UserName })
                    .OrderBy(g => g.Key.UserNo);

                // 2. 建立 CSV 內容
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("人員,員工編號,檢驗類型,區域,設備,工單,開始時間,結束時間,時長(分),OK數,NG數");

                foreach (var user in grouped)
                {
                    foreach (var a in user)
                    {
                        var duration = a.SubmitDataAt.HasValue
                            ? (a.SubmitDataAt.Value - a.ArriveAt).TotalMinutes.ToString("F0")
                            : "-";

                        sb.AppendLine(
                            $"{user.Key.UserName}," +
                            $"{user.Key.UserNo}," +
                            $"{a.InspectType}," +
                            $"{a.Area}," +
                            $"{a.DeviceId}," +
                            $"{a.InspectWo}," +
                            $"{a.ArriveAt:HH:mm:ss}," +
                            $"{(a.SubmitDataAt?.ToString("HH:mm:ss") ?? "進行中")}," +
                            $"{duration}," +
                            $"{a.InspectItemOkNo}," +
                            $"{a.InspectItemNgNo}"
                        );
                    }
                }

                // 3. 轉 Big5 編碼 → Excel 可以正常打開
                var big5 = System.Text.Encoding.GetEncoding(950);
                var bytes = big5.GetBytes(sb.ToString());

                var fileName = $"巡檢稼動表_{selectedDate:yyyyMMdd}.csv";
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "匯出 CSV 發生錯誤");
                return Content("匯出失敗：" + ex.Message);
            }
        }



        [HttpGet]
        public async Task<IActionResult> Machine(DateTime? date)
        {
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.DepartmentName = HttpContext.Session.GetString("DepartmentName");
            ViewBag.TitleName = HttpContext.Session.GetString("TitleName");
            ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var selectedDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetInspectionData(string date)
        {
            try
            {
                if (string.IsNullOrEmpty(date))
                {
                    date = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                }

                var rawData = await _activityChartRepository.GetInspectionDataByDateAsync(date);

                // 只處理同一時段多筆檢驗記錄的合併
                var groupedData = rawData
                    .GroupBy(d => new { d.DeviceId, d.DeviceName, d.ScheduleRange })
                    .Select(g =>
                    {
                        var first = g.First();
                        var inspections = g
                                          .Select(x => new InspectionDetail
                                          {
                                              inspectType = x.InspectType,
                                              inspectStartTime = x.InspectStartTime,
                                              inspectEndTime = x.InspectEndTime,
                                              inspectUserName = x.InspectUserName,
                                              responseUserNames = x.ResponseUserNames,
                                              responseUserNos = x.ResponseUserNos,
                                              workOrderNo = x.DeviceStatusWo,
                                              status = x.Status,
                                              runTime = x.RunTime.ToString(),
                                              nonOffTime = x.NonOffTime.ToString(),
                                              prodNo = x.ProdNo,
                                              prodDesc = x.ProdDesc
                                          })
                                          .ToList();

                        return new MachineInspectionData
                        {
                            deviceId = first.DeviceId,
                            deviceName = first.DeviceName, 
                            area = first.Area,
                            scheduleRange = first.ScheduleRange,
                            inspections = inspections,
                        };
                    })
                    .OrderBy(d => d.deviceId)
                    .ThenBy(d => d.scheduleRange)
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = groupedData,
                    count = groupedData.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection data for date: {Date}", date);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }






    }
}