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
                Source,
                InspectItemOkNo,
                InspectItemNgNo
            FROM INSPECTION_QC_RECORD                
            WHERE CAST(ArriveAt AS DATE) = @Date
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
            FROM [MES].[dbo].[ABC_USER_WH] a
            LEFT JOIN ABC_BAS_STATUS b ON b.StatusNo = a.StatusNo
            WHERE a.UserNo IN ('G03078', 'G01629', 'G01824', 'G02449', 'G01813')
              AND a.StatusNo IN ('0007','0008','0005')
              AND CAST(StartTime AS DATE) = @Date
            ORDER BY UserNo, ArriveAt;";

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
                _logger.LogError(ex, "Error getting user activities: {UserNo}, {Date}", userNo, date);
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
    }

    // DTO
  
}