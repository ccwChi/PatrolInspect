using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IScheduleRepository
    {
        Task<List<InspectionScheduleEvent>> GetAllSchedulesAsync();
        Task<List<InspectionScheduleEvent>> GetSearchSchedules(string userName, string depart, DateTime? startDate, DateTime? endDate);
        Task<List<InspectionScheduleEvent>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<InspectionScheduleEvent?> GetScheduleByIdAsync(int eventId);

        Task<int> CreateScheduleAsync(InspectionScheduleEvent schedule);
        Task<List<int>> CreateSchedulesBatchAsync(List<InspectionScheduleEvent> schedules);
        Task<bool> UpdateScheduleAsync(InspectionScheduleEvent schedule);
        Task<bool> DeleteScheduleAsync(int eventId);
        Task<bool> DeleteSchedulesBatchAsync(List<int> eventIds);

        // 基礎資料
        Task<List<string>> GetAreasAsync();
        Task<List<MesUser>> GetUsersAsync();
        Task<ScheduleBaseInfo> GetScheduleBaseInfoAsync();
        Task<List<InspectEventTypeMaster>> GetInspectTypesAsync();
    }
}