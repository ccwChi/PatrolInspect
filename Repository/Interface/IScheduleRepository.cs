using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IScheduleRepository
    {
        Task<List<InspectionScheduleEvent>> GetAllSchedulesAsync();
        Task<List<InspectionScheduleEvent>> GetSchedulesByUserAsync(string userNo);
        Task<List<InspectionScheduleEvent>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<int> CreateScheduleAsync(InspectionScheduleEvent schedule);
        Task<List<int>> CreateSchedulesBatchAsync(List<InspectionScheduleEvent> schedules);
        Task<bool> UpdateScheduleAsync(InspectionScheduleEvent schedule);
        Task<bool> DeleteScheduleAsync(int eventId);
        Task<bool> DeleteSchedulesBatchAsync(List<int> eventIds);
        Task<InspectionScheduleEvent?> GetScheduleByIdAsync(int eventId);
    }
}