using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IItemManageRepository
    {
        // 查詢相關
        Task<PagedResult<InspectionItem>> GetInspectionItemsAsync(InspectionItemQueryDto query);
        Task<List<InspectionItem>> GetAllInspectionItemsAsync();
        Task<InspectionItem?> GetInspectionItemByIdAsync(int itemId);
        Task<List<InspectionItem>> GetInspectionItemsByDepartmentAsync(string department, bool? isActive = null);
        Task<List<InspectionItem>> GetInspectionItemsByAreaAsync(string inspectArea, bool? isActive = null);

        // 新增、修改、刪除
        Task<int> CreateInspectionItemAsync(InspectionItem item);
        Task<bool> UpdateInspectionItemAsync(InspectionItem item);
        Task<bool> ToggleInspectionItemStatusAsync(int itemId, bool isActive, string updateBy, string? updateReason = null);
        Task<bool> DeleteInspectionItemAsync(int itemId);

        // 驗證相關
        Task<bool> IsInspectionItemNameExistsAsync(string inspectName, string department, string inspectArea, int? excludeId = null);
        Task<bool> TestConnectionAsync();

    }
}