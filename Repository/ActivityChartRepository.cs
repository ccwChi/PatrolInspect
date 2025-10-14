using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PatrolInspect.Controllers;
using PatrolInspect.Models;
using System.Data;

namespace PatrolInspect.Repository
{
    public class ActivityChartRepository 
    {
        private readonly ILogger<ActivityChartRepository> _logger;
        private readonly string _mesConnString;
        private readonly int _envFlag;
        public ActivityChartRepository(IConfiguration configuration,IOptions<AppSettings> appSettings , ILogger<ActivityChartRepository> logger)
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

        public async Task<List<InspectionActivity>> GetActivitiesByDateAsync(DateTime date)
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
                    Source
                FROM INSPECTION_QC_RECORD                
                WHERE CAST(ArriveAt AS DATE) = @Date
                AND InspectType <> 'CANCEL'
                ORDER BY UserNo, ArriveAt";

            try
            {
                var activities = await connection.QueryAsync<InspectionActivity>(sql, new { Date = date.Date });
                return activities.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activities by date: {Date}", date);
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
                    Source
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
                _logger.LogError(ex, "Error getting user activities: {UserNo}, {Date}", userNo, date);
                throw;
            }
        }



        public async Task<List<InspectionData>> GetInspectionDataByDateAsync(string date)
        {
            try
            {
                using (var connection = new SqlConnection(_mesConnString))
                {
                    var sql = @"
                        SELECT 
                            機台, 
                            排班時間範圍, 
                            運作工時, 
                            是否應做檢驗, 
                            工單, 
                            負責人, 
                            檢驗項目, 
                            最後檢驗時間, 
                            狀態
                        FROM MES.dbo.INSPECTION_SUMMARY_HOURLY WITH (NOLOCK)
                        WHERE 日期 = @Date
                        ORDER BY 機台, 排班時間範圍
                    ";

                    var result = await connection.QueryAsync<InspectionData>(sql, new { Date = date });
                    return result.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection data for date: {Date}", date);
                throw;
            }
        }

    }
}
