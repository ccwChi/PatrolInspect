using PatrolInspect.Models;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IItemManageRepository
    {
        Task<PagedResult<InspectionItem>> GetInspectionItemsAsync(InspectionItemQueryDto query);
        Task<List<InspectionItem>> GetAllInspectionItemsAsync();
        Task<InspectionItem?> GetInspectionItemByIdAsync(int inspectItemId);
        Task<List<InspectionItem>> GetInspectionItemsByDepartmentAsync(string department, bool? isActive = null);
        Task<List<InspectionItem>> GetInspectionItemsByAreaAsync(string inspectArea, bool? isActive = null);
        Task<int> CreateInspectionItemAsync(InspectionItem item);
        Task<bool> UpdateInspectionItemAsync(InspectionItem item);
        Task<bool> ToggleInspectionItemStatusAsync(int inspectItemId, bool isActive, string updateBy, string? updateReason = null);
        Task<bool> DeleteInspectionItemAsync(int inspectItemId);
        Task<bool> IsInspectionItemNameExistsAsync(string inspectName, string department, string inspectArea, int? excludeId = null);
    }
}