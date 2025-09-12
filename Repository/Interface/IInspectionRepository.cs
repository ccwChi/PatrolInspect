using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IInspectionRepository
    {

        // 主要功能
        Task<UserTodayInspection> GetTodayInspectionAsync(string userNo, string userName, string department);


        // NFC 功能
        Task<InspectionDeviceAreaMapping?> FindNFCcard(string nfcId);
        Task<int> CreateInspectionRecordAsync(InspectionQcRecord record);
        Task<bool> UpdateInspectionRecordAsync(int recordId, string userNo);
        Task<InspectionQcRecord?> GetPendingInspectionByUserAsync(string userNo);

    }
}