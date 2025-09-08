using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;
using PatrolInspect.Repository;

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
        public IActionResult Edit()
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


        [HttpGet]
        public async Task<IActionResult> GetAreasUsersInspectType()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            if (string.IsNullOrEmpty(userNo))
            {
                return Json(new { success = false, message = "未登入" });
            }

            try
            {
                var areas = await _scheduleRepository.GetAreasAsync();
                var users = await _scheduleRepository.GetUsersAsync();
                var scheduleBase = await _scheduleRepository.GetScheduleBaseInfoAsync();
                var inspectTypes = await _scheduleRepository.GetInspectTypesAsync();
                var scheduleUsers = scheduleBase.UserNames;
                var scheduleDeparts = scheduleBase.Departments;

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        areas,
                        users,
                        scheduleUsers,
                        scheduleDeparts,
                        inspectTypes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Areas Users InspectType");
                return Json(new { success = false, message = "載入基礎資料失敗" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveSchedules([FromBody] List<ScheduleEventDto> schedules)
        {
            try
            {
                var currentUser = HttpContext.Session.GetString("UserNo");

                // 轉換DTO到實體
                var scheduleEntities = schedules.Select(dto => new InspectionScheduleEvent
                {
                    UserNo = dto.UserNo,
                    UserName = dto.UserName,
                    Department = dto.DepartmentName,
                    EventType = dto.EventType,
                    EventTypeName = dto.EventTypeName,
                    EventDetail = dto.EventDetail,
                    StartDateTime = dto.StartDateTime,
                    EndDateTime = dto.EndDateTime,
                    Area = dto.Area,
                    IsActive = dto.IsActive,
                    CreateBy = currentUser
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


        [HttpGet]
        public async Task<IActionResult> GetSearchSchedules(string userName, string depart, DateTime startDate, DateTime endDate)
        {
            try
            {
                var schedules = await _scheduleRepository.GetSearchSchedules(userName, depart, startDate, endDate);
                return Json(new { success = true, data = schedules });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user schedules for: {userName}", userName);
                return Json(new { success = false, message = "搜尋資料失敗" });
            }
        }


    }
}