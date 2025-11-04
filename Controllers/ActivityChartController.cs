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

            var viewModel = new ActivityChartViewModel
            {
                SelectedDate = selectedDate,
                UserActivities = new List<UserActivityViewModel>()
            };

            try
            {
                var activities = await _activityChartRepository.GetActivitiesByDateAsync(selectedDate);

                if (activities != null && activities.Any())
                {
                    var userActivities = activities
                        .GroupBy(a => new { a.UserNo, a.UserName })
                        .Select(g => new UserActivityViewModel
                        {
                            UserNo = g.Key.UserNo,
                            UserName = g.Key.UserName,
                            Activities = g.OrderBy(a => a.ArriveAt).ToList()
                        })
                        .OrderBy(u => u.UserNo)
                        .ToList();

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