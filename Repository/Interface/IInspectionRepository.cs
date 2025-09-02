using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IInspectionRepository
    {
        Task<UserTodayInspection> GetTodayInspectionAsync(string userNo, string userName, string department);
        Task<List<InspectionScheduleEvent>> GetUserAllTodaySchedulesAsync(string userNo, DateTime date);
        List<TimePeriod> ProcessTimePeriodsData(List<InspectionScheduleEvent> scheduleEvents);
        Task<List<InspectionDeviceAreaMapping>> GetAreaDevicesAsync(List<string> areas);
        Task<List<FnDeviceStatus>> GetDeviceStatusAsync(List<string> deviceIds);
        Task<List<InspectionQcRecord>> GetTodayInspectionRecordsAsync(string userNo, DateTime date);
        Task<int> CreateInspectionRecordAsync(InspectionQcRecord record);
        Task<InspectionQcRecord?> GetPendingInspectionByUserAsync(string userNo);
        Task<bool> UpdateInspectionRecordAsync(int recordId, string userNo);
    }
}