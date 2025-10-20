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
        public IActionResult Machine()
        {
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.DepartmentName = HttpContext.Session.GetString("DepartmentName");
            ViewBag.TitleName = HttpContext.Session.GetString("TitleName");
            ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ViewBag.SelectedDate = DateTime.Now.ToString("yyyy-MM-dd");

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

    // DTO
    public class MachineInspectionData
    {
        public string deviceId { get; set; }
        public string deviceName { get; set; }
        public string area { get; set; }
        public string scheduleRange { get; set; }
        public decimal runTime { get; set; }
        public string workOrder { get; set; }
        public string inspectUserName { get; set; }
        public List<InspectionDetail> inspections { get; set; }
        public string status { get; set; }
    }

    public class InspectionDetail
    {
        public string inspectType { get; set; }
        public DateTime? inspectStartTime { get; set; }
        public DateTime? inspectEndTime { get; set; }
        public string inspectUserName { get; set; }
        public string responseUserNos { get; set; }
        public string responseUserNames { get; set; }
        public string workOrderNo { get; set; }
        public string status { get; set; }
        public string runTime { get; set; }
        public string nonOffTime { get; set; }

    }




}