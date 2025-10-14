using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using static PatrolInspect.Controllers.InspectionController;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IInspectionRepository
    {

        // 主要功能
        Task<UserTodayInspection> GetTodayInspectionAsync(string userNo, string userName, string department);
        Task<object> UpdateInspectionQuantityAsync(int recordId, int okQuantity, int ngQuantity, string updatedBy);
        Task<(bool Success, string Message)> SubmitWarehouseInspectionAsync(int originalRecordId, List<WarehouseOrderInfo> orders, string userNo);
        Task<List<string>> GetInspectTypeList();
        // NFC 功能
        Task<List<InspectionDeviceAreaMappingDto>> FindNFCcard(string nfcId);
        Task<int> CreateInspectionRecordAsync(InspectionQcRecord record);
        Task<bool> UpdateInspectionRecordAsync(int recordId, string deviceId, string userNo);
        Task<List<InspectionQcRecord>> GetPendingInspectionByUserAsync(string userNo);



    }
}