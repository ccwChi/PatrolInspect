using Dapper;
using PatrolInspect.Models;
using Microsoft.Extensions.Options;
using PatrolInspect.Repositories.Interfaces;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Repository
{
    public class ItemManageRepository : IItemManageRepository
    {
        private readonly string _mesConn;
        private readonly ILogger<ItemManageRepository> _logger;

        public ItemManageRepository(IConfiguration configuration, IOptions<AppSettings> appSettings, ILogger<ItemManageRepository> logger)
        {
            _logger = logger;
            var envFlag = appSettings.Value.EnvFlag;
            var connectionKey = EnvironmentHelper.GetMesConnectionStringKey(envFlag);
            _mesConn = configuration.GetConnectionString(connectionKey)
                ?? throw new ArgumentNullException($"ConnectionString '{connectionKey}' not found");
        }

        private IDbConnection CreateConnection() => new SqlConnection(_mesConn);

        public async Task<PagedResult<InspectionItem>> GetInspectionItemsAsync(InspectionItemQueryDto query)
        {
            using var connection = CreateConnection();

            var whereClause = new StringBuilder("WHERE 1=1");
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(query.Department))
            {
                whereClause.Append(" AND Department = @Department");
                parameters.Add("Department", query.Department);
            }

            if (!string.IsNullOrEmpty(query.InspectArea))
            {
                whereClause.Append(" AND InspectArea = @InspectArea");
                parameters.Add("InspectArea", query.InspectArea);
            }

            if (query.IsActive.HasValue)
            {
                whereClause.Append(" AND IsActive = @IsActive");
                parameters.Add("IsActive", query.IsActive.Value);
            }

            if (!string.IsNullOrEmpty(query.SearchText))
            {
                whereClause.Append(" AND InspectName LIKE @SearchText");
                parameters.Add("SearchText", $"%{query.SearchText}%");
            }

            try
            {
                // 取得總筆數
                var countSql = $"SELECT COUNT(*) FROM INSPECTION_ITEM_LIST {whereClause}";
                var totalCount = await connection.QuerySingleAsync<int>(countSql, parameters);

                // 取得分頁資料
                var offset = (query.Page - 1) * query.PageSize;
                var dataSql = $@"
                    SELECT InspectItemId, InspectName, Department, InspectArea, Station, 
                           DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                           CreateBy, UpdateDate, UpdateBy, UpdateReason
                    FROM INSPECTION_ITEM_LIST 
                    {whereClause}
                    ORDER BY CreateDate DESC, InspectItemId DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                parameters.Add("Offset", offset);
                parameters.Add("PageSize", query.PageSize);

                var items = await connection.QueryAsync<InspectionItem>(dataSql, parameters);

                return new PagedResult<InspectionItem>
                {
                    Items = items.ToList(),
                    TotalCount = totalCount,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection items with query: {@Query}", query);
                throw;
            }
        }

        public async Task<List<InspectionItem>> GetAllInspectionItemsAsync()
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT InspectItemId, InspectName, Department, InspectArea, Station, 
                       DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                       CreateBy, UpdateDate, UpdateBy, UpdateReason
                FROM INSPECTION_ITEM_LIST 
                ORDER BY Department, InspectArea, InspectName";

            try
            {
                var items = await connection.QueryAsync<InspectionItem>(sql);
                return items.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all inspection items");
                throw;
            }
        }

        public async Task<InspectionItem?> GetInspectionItemByIdAsync(int inspectItemId)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT InspectItemId, InspectName, Department, InspectArea, Station, 
                       DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                       CreateBy, UpdateDate, UpdateBy, UpdateReason
                FROM INSPECTION_ITEM_LIST 
                WHERE InspectItemId = @InspectItemId";

            try
            {
                return await connection.QueryFirstOrDefaultAsync<InspectionItem>(sql, new { InspectItemId = inspectItemId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection item by ID: {InspectItemId}", inspectItemId);
                throw;
            }
        }

        public async Task<List<InspectionItem>> GetInspectionItemsByDepartmentAsync(string department, bool? isActive = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT InspectItemId, InspectName, Department, InspectArea, Station, 
                       DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                       CreateBy, UpdateDate, UpdateBy, UpdateReason
                FROM INSPECTION_ITEM_LIST 
                WHERE Department = @Department
                AND (@IsActive IS NULL OR IsActive = @IsActive)
                ORDER BY InspectArea, InspectName";

            try
            {
                var items = await connection.QueryAsync<InspectionItem>(sql, new { Department = department, IsActive = isActive });
                return items.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection items by department: {Department}", department);
                throw;
            }
        }

        public async Task<List<InspectionItem>> GetInspectionItemsByAreaAsync(string inspectArea, bool? isActive = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT InspectItemId, InspectName, Department, InspectArea, Station, 
                       DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                       CreateBy, UpdateDate, UpdateBy, UpdateReason
                FROM INSPECTION_ITEM_LIST 
                WHERE InspectArea = @InspectArea
                AND (@IsActive IS NULL OR IsActive = @IsActive)
                ORDER BY Department, InspectName";

            try
            {
                var items = await connection.QueryAsync<InspectionItem>(sql, new { InspectArea = inspectArea, IsActive = isActive });
                return items.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection items by area: {InspectArea}", inspectArea);
                throw;
            }
        }

        public async Task<int> CreateInspectionItemAsync(InspectionItem item)
        {
            using var connection = CreateConnection();
            var sql = @"
                INSERT INTO INSPECTION_ITEM_LIST 
                (InspectName, Department, InspectArea, Station, DataType, SelectOptions, IsRequired, CreateBy)
                OUTPUT INSERTED.InspectItemId
                VALUES 
                (@InspectName, @Department, @InspectArea, @Station, @DataType, @SelectOptions, @IsRequired, @CreateBy)";

            try
            {
                var inspectItemId = await connection.QuerySingleAsync<int>(sql, item);
                _logger.LogInformation("Created inspection item: {InspectItemId} - {InspectName} by {CreateBy}",
                    inspectItemId, item.InspectName, item.CreateBy);
                return inspectItemId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inspection item: {InspectName}", item.InspectName);
                throw;
            }
        }

        public async Task<bool> UpdateInspectionItemAsync(InspectionItem item)
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE INSPECTION_ITEM_LIST 
                SET InspectName = @InspectName,
                    Department = @Department,
                    InspectArea = @InspectArea,
                    Station = @Station,
                    DataType = @DataType,
                    SelectOptions = @SelectOptions,
                    IsRequired = @IsRequired,
                    UpdateDate = GETDATE(),
                    UpdateBy = @UpdateBy,
                    UpdateReason = @UpdateReason
                WHERE InspectItemId = @InspectItemId";

            try
            {
                var affected = await connection.ExecuteAsync(sql, item);
                var success = affected > 0;

                if (success)
                {
                    _logger.LogInformation("Updated inspection item: {InspectItemId} - {InspectName} by {UpdateBy}",
                        item.InspectItemId, item.InspectName, item.UpdateBy);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inspection item: {InspectItemId}", item.InspectItemId);
                throw;
            }
        }

        public async Task<bool> ToggleInspectionItemStatusAsync(int inspectItemId, bool isActive, string updateBy, string? updateReason = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE INSPECTION_ITEM_LIST 
                SET IsActive = @IsActive,
                    UpdateDate = GETDATE(),
                    UpdateBy = @UpdateBy,
                    UpdateReason = @UpdateReason
                WHERE InspectItemId = @InspectItemId";

            try
            {
                var affected = await connection.ExecuteAsync(sql, new
                {
                    InspectItemId = inspectItemId,
                    IsActive = isActive,
                    UpdateBy = updateBy,
                    UpdateReason = updateReason ?? $"狀態變更為{(isActive ? "啟用" : "停用")}"
                });

                var success = affected > 0;

                if (success)
                {
                    _logger.LogInformation("Toggled inspection item status: {InspectItemId} to {IsActive} by {UpdateBy}",
                        inspectItemId, isActive, updateBy);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling inspection item status: {InspectItemId}", inspectItemId);
                throw;
            }
        }

        public async Task<bool> DeleteInspectionItemAsync(int inspectItemId)
        {
            using var connection = CreateConnection();
            var sql = "DELETE FROM INSPECTION_ITEM_LIST WHERE InspectItemId = @InspectItemId";

            try
            {
                var affected = await connection.ExecuteAsync(sql, new { InspectItemId = inspectItemId });
                var success = affected > 0;

                if (success)
                {
                    _logger.LogInformation("Deleted inspection item: {InspectItemId}", inspectItemId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inspection item: {InspectItemId}", inspectItemId);
                throw;
            }
        }

        public async Task<bool> IsInspectionItemNameExistsAsync(string inspectName, string department, string inspectArea, int? excludeId = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT COUNT(*) 
                FROM INSPECTION_ITEM_LIST 
                WHERE InspectName = @InspectName 
                AND Department = @Department 
                AND InspectArea = @InspectArea
                AND (@ExcludeId IS NULL OR InspectItemId != @ExcludeId)";

            try
            {
                var count = await connection.QuerySingleAsync<int>(sql, new
                {
                    InspectName = inspectName,
                    Department = department,
                    InspectArea = inspectArea,
                    ExcludeId = excludeId
                });

                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking inspection item name exists: {InspectName}", inspectName);
                throw;
            }
        }
    }
}