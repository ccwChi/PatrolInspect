using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PatrolInspect.Controllers;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using System.Data;

namespace PatrolInspect.Repository
{
    public class ActivityChartRepository
    {
        private readonly ILogger<ActivityChartRepository> _logger;
        private readonly string _mesConnString;
        private readonly int _envFlag;

        public ActivityChartRepository(IConfiguration configuration, IOptions<AppSettings> appSettings, ILogger<ActivityChartRepository> logger)
        {
            _logger = logger;
            _envFlag = appSettings.Value.EnvFlag;
            _logger.LogInformation("Current EnvFlag: {EnvFlag}", _envFlag);
            var MesConnKey = EnvironmentHelper.GetMesConnectionStringKey(_envFlag);
            _mesConnString = configuration.GetConnectionString(MesConnKey)
                ?? throw new ArgumentNullException($"ConnectionString '{MesConnKey}' not found");
        }

        private IDbConnection CreateMesConnection()
        {
            return new SqlConnection(_mesConnString);
        }

        public async Task<List<InspectionActivity>> GetActivitiesByDateRangeAsync(DateTime startDateTime, DateTime endDateTime)
        {
            using var connection = CreateMesConnection();
            var sql = @"
            SELECT 
                RecordId,
                CardId,
                Area,
                DeviceId,
                UserNo,
                UserName,
                InspectType,
                InspectWo,
                ArriveAt,
                SubmitDataAt,
                Source,
                InspectItemOkNo,
                InspectItemNgNo
            FROM INSPECTION_QC_RECORD                
            WHERE ArriveAt >= @StartDateTime 
              AND ArriveAt < @EndDateTime
            AND InspectType <> 'CANCEL'

            UNION ALL

            SELECT 
                NULL AS RecordId,              
                ''   AS CardId,
                ''   AS Area,
                ''   AS DeviceId,
                a.UserNo,
                a.UserName,
                SUBSTRING(b.StatusName_TW, 4, LEN(b.StatusName_TW)) AS InspectType,
                '' AS InspectWo,
                a.StartTime AS ArriveAt,
                a.EndTime AS SubmitDataAt,
                'ABC報工' AS Source,
                null AS InspectItemOkNo,       
                null AS InspectItemNgNo
            FROM [ABC_USER_WH] a
            LEFT JOIN ABC_BAS_STATUS b ON b.StatusNo = a.StatusNo
            WHERE a.UserNo IN ('G03078', 'G01629', 'G01824', 'G02449', 'G01813')
              AND a.StatusNo IN ('0007','0008','0005')
            AND a.StartTime >= @StartDateTime 
            AND a.StartTime < @EndDateTime
            ORDER BY UserNo, ArriveAt;";

            try
            {
                var activities = await connection.QueryAsync<InspectionActivity>(sql, new
                {
                    StartDateTime = startDateTime,
                    EndDateTime = endDateTime
                });
                return activities.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<InspectionActivity>> GetUserActivitiesByDateAsync(string userNo, DateTime date)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT 
                    RecordId,
                    CardId,
                    Area,
                    DeviceId,
                    UserNo,
                    UserName,
                    InspectType,
                    InspectWo,
                    ArriveAt,
                    SubmitDataAt,
                    Source,
                    InspectItemOkNo,
                    InspectItemNgNo
                FROM INSPECTION_QC_RECORD
                WHERE UserNo = @UserNo 
                AND CAST(ArriveAt AS DATE) = @Date
                ORDER BY ArriveAt";

            try
            {
                var activities = await connection.QueryAsync<InspectionActivity>(sql, new { UserNo = userNo, Date = date.Date });
                return activities.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<InspectionUtilizationData>> GetInspectionDataByDateAsync(string date)
        {
            try
            {
                using (var connection = new SqlConnection(_mesConnString))
                {
                    var sql = @"
                        SELECT 
                            Area,
                            DeviceId,
                            DeviceName,
                            ScheduleRange,
                            ScheduleStart,
                            ScheduleEnd,
                            RunTime,
                            NonOffTime,
                            DeviceStatusWo,
                            IdleTime,
                            WorkOrderNo,
                            InspectType,
                            InspectStartTime,
                            InspectEndTime,
                            InspectUserNo,
                            InspectUserName,
                            ResponseUserNames,
                            ResponseUserNos,
                            Status,
                            ProdNo,
                            ProdDesc
                        FROM INSPECTION_UTILIZATION_DEVICE WITH (NOLOCK)
                        WHERE DataDate = @Date
                        ORDER BY DeviceId, ScheduleStart, InspectStartTime
                    ";

                    var result = await connection.QueryAsync<InspectionUtilizationData>(sql, new { Date = date });
                    return result.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection data for date: {Date}", date);
                throw;
            }
        }

        public async Task<List<string>> GetActiveValidWorkTypesAsync()
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT InspectType 
                FROM INSPECTION_VALID_WORKTYPE 
                WHERE IsActive = 1 
                ORDER BY DisplayOrder";

            try
            {
                var types = await connection.QueryAsync<string>(sql);
                return types.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active valid work types");
                throw;
            }
        }

        public async Task<List<InspectionValidWorkType>> GetAllWorkTypesAsync()
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT Id, InspectType, IsActive, DisplayOrder, CreateDate, UpdateDate
                FROM INSPECTION_VALID_WORKTYPE 
                ORDER BY DisplayOrder";

            try
            {
                var types = await connection.QueryAsync<InspectionValidWorkType>(sql);
                return types.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all work types");
                throw;
            }
        }

        public async Task<bool> UpdateWorkTypeStatusAsync(int id, bool isActive)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                UPDATE INSPECTION_VALID_WORKTYPE 
                SET IsActive = @IsActive, UpdateDate = GETDATE()
                WHERE Id = @Id";

            try
            {
                var result = await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating work type status: {Id}", id);
                throw;
            }
        }
    }

    // DTO

}