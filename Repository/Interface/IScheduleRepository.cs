using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IScheduleRepository
    {
        // 基礎資料
        Task<ScheduleBaseInfo> GetScheduleBaseInfoAsync();

        // 搜尋
        Task<List<InspectionScheduleEvent>> GetSearchSchedules(string userName, string depart, DateTime? startDate, DateTime? endDate);

        Task<List<int>> CreateSchedulesBatchAsync(List<InspectionScheduleEvent> schedules);
        Task<bool> DeleteSchedulesBatchAsync(List<int> eventIds);

    }
}