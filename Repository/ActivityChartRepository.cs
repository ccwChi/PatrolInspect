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
                            Status
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
    public class InspectionUtilizationData
    {
        public string Area { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string ScheduleRange { get; set; }
        public DateTime ScheduleStart { get; set; }
        public DateTime ScheduleEnd { get; set; }
        public string DeviceStatusWo { get; set; }
        public decimal RunTime { get; set; }
        public decimal NonOffTime { get; set; }
        public decimal IdleTime { get; set; }
        public string WorkOrderNo { get; set; }
        public string InspectType { get; set; }
        public DateTime? InspectStartTime { get; set; }
        public DateTime? InspectEndTime { get; set; }
        public string InspectUserNo { get; set; }
        public string InspectUserName { get; set; }
        public string ResponseUserNos { get; set; }
        public string ResponseUserNames { get; set; }
        public string Status { get; set; }
    }
}