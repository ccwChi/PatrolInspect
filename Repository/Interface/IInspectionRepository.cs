using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IInspectionRepository
    {
        Task<List<string>> GetUserTodayAreasAsync(string userNo, DateTime date);
        Task<List<InspectionDeviceAreaMapping>> GetAreaDevicesAsync(List<string> areas);
        Task<List<FnDeviceStatus>> GetDeviceStatusAsync(List<string> deviceIds);
        Task<List<InspectionQcRecord>> GetTodayInspectionRecordsAsync(string userNo, DateTime date);
        Task<UserTodayInspection> GetUserTodayInspectionAsync(string userNo);
        Task<int> CreateInspectionRecordAsync(InspectionQcRecord record);
        Task<InspectionQcRecord?> GetPendingInspectionByUserAsync(string userNo);
        Task<bool> UpdateInspectionRecordAsync(int recordId, string userNo);
    }
}