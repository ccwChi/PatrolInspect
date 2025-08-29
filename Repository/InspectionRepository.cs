using Dapper;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using Microsoft.Extensions.Options;
using PatrolInspect.Repositories.Interfaces;
using System.Data;
using Microsoft.Data.SqlClient;

namespace PatrolInspect.Repository
{
    public class InspectionRepository : IInspectionRepository
    {
        private readonly string _mesConnString;
        private readonly string _fnReportConnString;
        private readonly ILogger<InspectionRepository> _logger;

        public InspectionRepository(IConfiguration configuration, IOptions<AppSettings> appSettings, ILogger<InspectionRepository> logger)
        {
            _logger = logger;
            var envFlag = appSettings.Value.EnvFlag;

            var MesConnKey = EnvironmentHelper.GetMesConnectionStringKey(envFlag);
            _mesConnString = configuration.GetConnectionString(MesConnKey)
                ?? throw new ArgumentNullException($"ConnectionString '{MesConnKey}' not found");

            // FineReport 連線字串 (需要在 appsettings.json 中新增)
            var FnConnKey = EnvironmentHelper.GetFnReportConnectionStringKey(envFlag);
            _fnReportConnString = configuration.GetConnectionString(FnConnKey)
                ?? throw new ArgumentNullException("FineReport ConnectionString not found");
        }

        private IDbConnection CreateMesConnection()
        {
            return new SqlConnection(_mesConnString);
        }

        private IDbConnection CreateFineReportConnection()
        {
            return new SqlConnection(_fnReportConnString);
        }

        public async Task<List<string>> GetUserTodayAreasAsync(string userNo, DateTime date)
        {
            using var connection = CreateMesConnection();

            // 先嘗試從 Area 欄位解析，如果是數字格式
            var sql = @"
                SELECT DISTINCT Area
                FROM INSPECTION_SCHEDULE_EVENT 
                WHERE UserNo = @UserNo 
                  AND IsActive = 1
                  AND CAST(StartDateTime AS DATE) = @Date
                  AND Area IS NOT NULL
                ORDER BY Area";

            try
            {
                var areas = await connection.QueryAsync<string?>(sql, new
                {
                    UserNo = userNo,
                    Date = date.Date
                });

                var areaList = areas
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a!)   // 經過 Where 後，這裡確實非 null
                    .ToList();

                _logger.LogInformation("User {UserNo} assigned to areas: {Areas} for date {Date}",
                    userNo, string.Join(",", areaList), date.Date);


                return areaList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user areas for {UserNo} on {Date}", userNo, date);
                throw;
            }
        }

        public async Task<List<InspectionDeviceAreaMapping>> GetAreaDevicesAsync(List<string> areas)
        {
            if (!areas.Any()) return new List<InspectionDeviceAreaMapping>();

            using var connection = CreateMesConnection();
            var sql = @"
                SELECT DeviceLocateId, AreaId, Area, DeviceId, DeviceName, 
                       NfcCardId, IsActive, CreateDate, CreateBy, UpdateDate, UpdateBy
                FROM INSPECTION_DEVICE_AREA_MAPPING 
                WHERE Area IN @Areas AND IsActive = 1
                ORDER BY AreaId, DeviceId";

            try
            {
                var devices = await connection.QueryAsync<InspectionDeviceAreaMapping>(sql, new { Areas = areas });
                _logger.LogInformation("Found {Count} devices in areas: {Areas}", devices.Count(), string.Join(",", areas));
                return devices.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting devices for areas: {Areas}", string.Join(",", areas));
                throw;
            }
        }

        public async Task<List<FnDeviceStatus>> GetDeviceStatusAsync(List<string> deviceIds)
        {
            if (!deviceIds.Any()) return new List<FnDeviceStatus>();

            using var connection = CreateFineReportConnection();
            var sql = @"
                SELECT DeviceID, DeviceStatus, StartTime, Duration, AlarmMessage, 
                       CreateTime, WO_ID, EternetStatus, BPM_NO, DeviceSchedulingStatus
                FROM FN_EQPSTATUS 
                WHERE DeviceID IN @DeviceIds";

            try
            {
                var statusList = await connection.QueryAsync<FnDeviceStatus>(sql, new { DeviceIds = deviceIds });
                _logger.LogInformation("Retrieved status for {Count} devices", statusList.Count());
                return statusList.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device status for devices: {Devices}", string.Join(",", deviceIds));
                throw;
            }
        }

        public async Task<List<InspectionQcRecord>> GetTodayInspectionRecordsAsync(string userNo, DateTime date)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT RecordId, CardId, DeviceId, UserNo, UserName, 
                       ArriveAt, SubmitDataAt, Source, CreateDate
                FROM INSPECTION_QC_RECORD 
                WHERE UserNo = @UserNo 
                  AND CAST(ArriveAt AS DATE) = @Date
                ORDER BY ArriveAt DESC";

            try
            {
                var records = await connection.QueryAsync<InspectionQcRecord>(sql, new
                {
                    UserNo = userNo,
                    Date = date.Date
                });
                return records.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today inspection records for {UserNo} on {Date}", userNo, date);
                throw;
            }
        }

        public async Task<UserTodayInspection> GetUserTodayInspectionAsync(string userNo)
        {
            var today = DateTime.Now.Date;

            try
            {
                // 1. 取得使用者今天負責的區域
                var assignedAreas = await GetUserTodayAreasAsync(userNo, today);

                if (!assignedAreas.Any())
                {
                    _logger.LogWarning("User {UserNo} has no areas assigned for today", userNo);
                    return new UserTodayInspection
                    {
                        UserNo = userNo,
                        AssignedAreas = new List<string>(),
                        DevicesToInspect = new List<InspectionDeviceInfo>()
                    };
                }

                // 2. 取得區域內的所有機台
                var areaDevices = await GetAreaDevicesAsync(assignedAreas);
                var deviceIds = areaDevices.Select(d => d.DeviceId).ToList();
                // [string, stirng, string...]

                // 3. 取得機台狀態
                var deviceStatuses = await GetDeviceStatusAsync(deviceIds);
                var statusDict = deviceStatuses.ToDictionary(s => s.DeviceID, s => s);

                // 4. 取得今天的巡檢記錄
                var todayRecords = await GetTodayInspectionRecordsAsync(userNo, today);
                var inspectedDevicesDict = todayRecords
                    .Where(r => !string.IsNullOrEmpty(r.DeviceId))
                    .GroupBy(r => r.DeviceId!)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(r => r.ArriveAt).First()
                    );

                // 5. 組合成巡檢設備清單
                var devicesToInspect = areaDevices.Select(device =>
                {
                    statusDict.TryGetValue(device.DeviceId, out var status);
                    inspectedDevicesDict.TryGetValue(device.DeviceId, out var lastInspection);

                    var isRunning = status?.DeviceStatus?.ToUpper() == "RUN";
                    var alreadyInspected = lastInspection != null;

                    return new InspectionDeviceInfo
                    {
                        Device = device,
                        Status = status,
                        RequiresInspection = isRunning && !alreadyInspected,
                        LastInspectionTime = lastInspection?.ArriveAt,
                        LastInspectorName = lastInspection?.UserName
                    };
                }).ToList();

                // 6. 統計資訊
                var runningDevices = devicesToInspect.Count(d => d.IsRunning);
                var completedInspections = devicesToInspect.Count(d => d.LastInspectionTime.HasValue);

                // 7. 取得區域名稱
                var areaNames = areaDevices
                    .GroupBy(d => d.AreaId)
                    .Select(g => g.First().Area)
                    .ToList();

                var result = new UserTodayInspection
                {
                    UserNo = userNo,
                    AssignedAreas = assignedAreas,
                    DevicesToInspect = devicesToInspect,
                    TotalDevices = devicesToInspect.Count,
                    RunningDevices = runningDevices,
                    CompletedInspections = completedInspections,
                    Areas = areaNames
                };

                _logger.LogInformation("User {UserNo} inspection summary: {Total} devices, {Running} running, {Completed} inspected",
                    userNo, result.TotalDevices, result.RunningDevices, result.CompletedInspections);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user today inspection for {UserNo}", userNo);
                throw;
            }
        }

        public async Task<int> CreateInspectionRecordAsync(InspectionQcRecord record)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                INSERT INTO INSPECTION_QC_RECORD 
                (CardId, DeviceId, UserNo, UserName, InspectType, ArriveAt, SubmitDataAt, Source, CreateDate)
                VALUES 
                (@CardId, @DeviceId, @UserNo, @UserName, @InspectType, @ArriveAt, @SubmitDataAt, @Source, @CreateDate);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            try
            {
                record.CreateDate = DateTime.Now;
                var recordId = await connection.QuerySingleAsync<int>(sql, record);

                _logger.LogInformation("Created inspection record: {RecordId} for device: {DeviceId} by user: {UserNo}",
                    recordId, record.DeviceId, record.UserNo);

                return recordId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inspection record for device: {DeviceId} by user: {UserNo}",
                    record.DeviceId, record.UserNo);
                throw;
            }
        }


        public async Task<InspectionQcRecord?> GetPendingInspectionByUserAsync(string userNo)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT TOP 1 
                    RecordId, CardId, DeviceId, UserNo, UserName, InspectType,
                    ArriveAt, SubmitDataAt, Source, CreateDate
                FROM INSPECTION_QC_RECORD 
                WHERE UserNo = @UserNo 
                  AND SubmitDataAt IS NULL
                ORDER BY ArriveAt DESC";

            try
            {
                var record = await connection.QueryFirstOrDefaultAsync<InspectionQcRecord>(sql, new { UserNo = userNo });

                if (record != null)
                {
                    _logger.LogDebug("Found pending inspection record: {RecordId} for user: {UserNo} at device: {DeviceId}",
                        record.RecordId, userNo, record.DeviceId);
                }

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending inspection for user: {UserNo}", userNo);
                throw;
            }
        }

        public async Task<bool> UpdateInspectionRecordAsync(int recordId, string userNo)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                UPDATE INSPECTION_QC_RECORD 
                SET InspectType = 'CANCEL', 
                    SubmitDataAt = GETDATE()
                WHERE RecordId = @RecordId 
                  AND UserNo = @UserNo 
                  AND SubmitDataAt IS NULL"; 

            try
            {
                var rowsAffected = await connection.ExecuteAsync(sql, new { RecordId = recordId, UserNo = userNo });

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Deleted pending inspection record: {RecordId} for user: {UserNo}", recordId, userNo);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to delete inspection record: {RecordId} for user: {UserNo} (record not found or already completed)", recordId, userNo);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inspection record: {RecordId} for user: {UserNo}", recordId, userNo);
                throw;
            }
        }





    }
}