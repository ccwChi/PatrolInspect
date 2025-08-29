using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;

namespace PatrolInspect.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly IScheduleRepository _scheduleRepository;
        private readonly ILogger<ScheduleController> _logger;

        public ScheduleController(IScheduleRepository scheduleRepository, ILogger<ScheduleController> logger)
        {
            _scheduleRepository = scheduleRepository;
            _logger = logger;
        }

        // 顯示編輯頁面
        [HttpGet]
        public IActionResult Editor()
        {
            // 檢查登入狀態
            var userNo = HttpContext.Session.GetString("UserNo");
            if (string.IsNullOrEmpty(userNo))
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.CurrentUser = userNo;
            ViewBag.CurrentUserName = HttpContext.Session.GetString("UserName");
            return View();
        }

        // 取得所有排班資料 (API)
        [HttpGet]
        public async Task<IActionResult> GetSchedules()
        {
            try
            {
                var schedules = await _scheduleRepository.GetAllSchedulesAsync();

                // 轉換為前端需要的格式
                var result = schedules.Select(s => new
                {
                    eventId = s.EventId,
                    userNo = s.UserNo,
                    userName = s.UserName,
                    eventType = s.EventType,
                    eventDetail = s.EventDetail,
                    startDate = s.StartDateTime.ToString("yyyy-MM-dd"),
                    startTime = s.StartDateTime.ToString("HH:mm"),
                    endDate = s.EndDateTime.ToString("yyyy-MM-dd"),
                    endTime = s.EndDateTime.ToString("HH:mm"),
                    area = s.Area ?? "",
                    isActive = s.IsActive
                }).ToList();

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schedules");
                return Json(new { success = false, message = "載入排班資料失敗" });
            }
        }

        // 儲存排班資料 (API)
        [HttpPost]
        public async Task<IActionResult> SaveSchedules([FromBody] List<ScheduleEventDto> schedules)
        {
            try
            {
                var currentUser = HttpContext.Session.GetString("UserNo") ?? "SYSTEM";

                // 轉換DTO到實體
                var scheduleEntities = schedules.Select(dto => new InspectionScheduleEvent
                {
                    UserNo = dto.UserNo,
                    UserName = dto.UserName,
                    EventType = dto.EventType,
                    EventDetail = dto.EventDetail,
                    StartDateTime = dto.StartDateTime,
                    EndDateTime = dto.EndDateTime,
                    Area = dto.Area,
                    IsActive = dto.IsActive,
                    CreateBy = dto.CreateBy ?? currentUser
                }).ToList();

                var eventIds = await _scheduleRepository.CreateSchedulesBatchAsync(scheduleEntities);

                _logger.LogInformation("Successfully saved {Count} schedules by user: {User}", eventIds.Count, currentUser);

                return Json(new
                {
                    success = true,
                    message = $"成功儲存 {eventIds.Count} 筆排班資料",
                    eventIds = eventIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving schedules");
                return Json(new { success = false, message = "儲存排班資料失敗: " + ex.Message });
            }
        }

        // 刪除排班資料 (API)
        [HttpPost]
        public async Task<IActionResult> DeleteSchedules([FromBody] List<int> eventIds)
        {
            try
            {
                var success = await _scheduleRepository.DeleteSchedulesBatchAsync(eventIds);

                if (success)
                {
                    _logger.LogInformation("Successfully deleted {Count} schedules", eventIds.Count);
                    return Json(new { success = true, message = $"成功刪除 {eventIds.Count} 筆排班資料" });
                }
                else
                {
                    return Json(new { success = false, message = "刪除失敗" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedules");
                return Json(new { success = false, message = "刪除排班資料失敗: " + ex.Message });
            }
        }

        // 取得特定使用者的排班資料 (API)
        [HttpGet]
        public async Task<IActionResult> GetUserSchedules(string userNo)
        {
            try
            {
                var schedules = await _scheduleRepository.GetSchedulesByUserAsync(userNo);
                return Json(new { success = true, data = schedules });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user schedules for: {UserNo}", userNo);
                return Json(new { success = false, message = "載入使用者排班資料失敗" });
            }
        }

        // 取得FullCalendar格式的資料 (API)
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(DateTime? start, DateTime? end)
        {
            try
            {
                List<InspectionScheduleEvent> schedules;

                if (start.HasValue && end.HasValue)
                {
                    schedules = await _scheduleRepository.GetSchedulesByDateRangeAsync(start.Value, end.Value);
                }
                else
                {
                    schedules = await _scheduleRepository.GetAllSchedulesAsync();
                }

                // 轉換為FullCalendar格式
                var events = schedules.Where(s => s.IsActive).Select(s => new
                {
                    id = s.EventId,
                    title = $"{s.UserName} - {s.EventType}",
                    start = s.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = s.EndDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    description = s.EventDetail,
                    backgroundColor = GetAreaColor(s.Area),
                    borderColor = GetAreaColor(s.Area),
                    extendedProps = new
                    {
                        userNo = s.UserNo,
                        userName = s.UserName,
                        eventType = s.EventType,
                        eventDetail = s.EventDetail,
                        area = s.Area
                    }
                }).ToList();

                return Json(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting calendar events");
                return Json(new { success = false, message = "載入行事曆資料失敗" });
            }
        }

        private string GetAreaColor(string? area)
        {
            if (string.IsNullOrEmpty(area)) return "#ddd";

            // 如果area是數字字串，轉換為數字來判斷顏色
            if (int.TryParse(area, out int areaId))
            {
                return areaId switch
                {
                    1 => "#ff6b6b",
                    2 => "#4ecdc4",
                    3 => "#45b7d1",
                    4 => "#96ceb4",
                    5 => "#ffeaa7",
                    _ => "#ddd"
                };
            }

            // 如果area是文字，用hash來產生顏色
            var hash = area.GetHashCode();
            var colors = new[] { "#ff6b6b", "#4ecdc4", "#45b7d1", "#96ceb4", "#ffeaa7", "#fd79a8", "#fdcb6e", "#6c5ce7" };
            return colors[Math.Abs(hash) % colors.Length];
        }
    }
}