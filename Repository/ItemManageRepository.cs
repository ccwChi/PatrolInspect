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
                    SELECT ItemId, InspectName, Department, InspectArea, Station, 
                           DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                           CreateBy, UpdateDate, UpdateBy, UpdateReason
                    FROM INSPECTION_ITEM_LIST 
                    {whereClause}
                    ORDER BY CreateDate DESC, ItemId DESC
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
                SELECT ItemId, InspectName, Department, InspectArea, Station, 
                       DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                       CreateBy, UpdateDate, UpdateBy, UpdateReason
                FROM INSPECTION_ITEM_LIST 
                WHERE IsActive = 1
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

        public async Task<InspectionItem?> GetInspectionItemByIdAsync(int itemId)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT ItemId, InspectName, Department, InspectArea, Station, 
                       DataType, SelectOptions, IsRequired, IsActive, CreateDate, 
                       CreateBy, UpdateDate, UpdateBy, UpdateReason
                FROM INSPECTION_ITEM_LIST 
                WHERE ItemId = @ItemId";

            try
            {
                return await connection.QueryFirstOrDefaultAsync<InspectionItem>(sql, new { ItemId = itemId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection item by ID: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<List<InspectionItem>> GetInspectionItemsByDepartmentAsync(string department, bool? isActive = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT ItemId, InspectName, Department, InspectArea, Station, 
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
                SELECT ItemId, InspectName, Department, InspectArea, Station, 
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
                OUTPUT INSERTED.ItemId
                VALUES 
                (@InspectName, @Department, @InspectArea, @Station, @DataType, @SelectOptions, @IsRequired, @CreateBy)";

            try
            {
                var itemId = await connection.QuerySingleAsync<int>(sql, item);
                _logger.LogInformation("Created inspection item: {ItemId} - {InspectName} by {CreateBy}",
                    itemId, item.InspectName, item.CreateBy);
                return itemId;
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
                WHERE ItemId = @ItemId";

            try
            {
                var affected = await connection.ExecuteAsync(sql, item);
                var success = affected > 0;

                if (success)
                {
                    _logger.LogInformation("Updated inspection item: {ItemId} - {InspectName} by {UpdateBy}",
                        item.ItemId, item.InspectName, item.UpdateBy);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inspection item: {ItemId}", item.ItemId);
                throw;
            }
        }

        public async Task<bool> ToggleInspectionItemStatusAsync(int itemId, bool isActive, string updateBy, string? updateReason = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE INSPECTION_ITEM_LIST 
                SET IsActive = @IsActive,
                    UpdateDate = GETDATE(),
                    UpdateBy = @UpdateBy,
                    UpdateReason = @UpdateReason
                WHERE ItemId = @ItemId";

            try
            {
                var affected = await connection.ExecuteAsync(sql, new
                {
                    ItemId = itemId,
                    IsActive = isActive,
                    UpdateBy = updateBy,
                    UpdateReason = updateReason ?? $"狀態變更為{(isActive ? "啟用" : "停用")}"
                });

                var success = affected > 0;

                if (success)
                {
                    _logger.LogInformation("Toggled inspection item status: {ItemId} to {IsActive} by {UpdateBy}",
                        itemId, isActive, updateBy);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling inspection item status: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<bool> DeleteInspectionItemAsync(int itemId)
        {
            using var connection = CreateConnection();
            var sql = "DELETE FROM INSPECTION_ITEM_LIST WHERE ItemId = @ItemId";

            try
            {
                var affected = await connection.ExecuteAsync(sql, new { ItemId = itemId });
                var success = affected > 0;

                if (success)
                {
                    _logger.LogInformation("Deleted inspection item: {ItemId}", itemId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inspection item: {ItemId}", itemId);
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
                AND (@ExcludeId IS NULL OR ItemId != @ExcludeId)";

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

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = CreateConnection();
                await connection.QueryFirstOrDefaultAsync<int>("SELECT 1");
                _logger.LogInformation("Inspection item database connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inspection item database connection test failed");
                return false;
            }
        }
    }
}