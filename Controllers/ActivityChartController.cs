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

                var data = await _activityChartRepository.GetInspectionDataByDateAsync(date);

                return Json(new
                {
                    success = true,
                    data = data,
                    count = data.Count
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
    public class InspectionData
    {
        public string 機台 { get; set; }
        public string 排班時間範圍 { get; set; }
        public decimal 運作工時 { get; set; }
        public string 是否應做檢驗 { get; set; }
        public string 工單 { get; set; }
        public string 負責人 { get; set; }
        public string 檢驗項目 { get; set; }
        public DateTime? 最後檢驗時間 { get; set; }
        public string 狀態 { get; set; }
    }




}