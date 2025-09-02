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


        public async Task<UserTodayInspection> GetTodayInspectionAsync(string userNo, string userName, string department)
        {
            var today = DateTime.Now.Date;
            string eventType = string.Empty;
            string eventTypeName = string.Empty;
            try
            {
                // 1. 取得使用者今天所有的排程
                var allScheduleEvents = await GetUserAllTodaySchedulesAsync(userNo, today);

                // 2. 處理成時段資料
                var timePeriods = ProcessTimePeriodsData(allScheduleEvents);

                // 3. 取得今天的巡檢記錄
                var todayRecords = await GetTodayInspectionRecordsAsync(userNo, today);
                var inspectedDevicesDict = todayRecords
                    .Where(r => !string.IsNullOrEmpty(r.DeviceId))
                    .GroupBy(r => r.DeviceId!)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ArriveAt).First());

                var totalDevices = 0;
                var runningDevices = 0;
                var completedInspections = 0;
                var allDevicesForCurrentUser = new List<InspectionDeviceInfo>();

                // 4. 為每個時段載入機台資料
                foreach (var period in timePeriods)
                {
                    if (period.Areas.Any())
                    {
                        var areaDevices = await GetAreaDevicesAsync(period.Areas);
                        var deviceIds = areaDevices.Select(d => d.DeviceId).ToList();
                        var deviceStatuses = await GetDeviceStatusAsync(deviceIds);
                        var statusDict = deviceStatuses.ToDictionary(s => s.DeviceID, s => s);

                        var devicesToInspect = areaDevices.Select(device =>
                        {
                            statusDict.TryGetValue(device.DeviceId, out var status);
                            inspectedDevicesDict.TryGetValue(device.DeviceId, out var lastInspection);

                            var isRunning = status?.DeviceStatus?.ToUpper() == "RUN";
                            var alreadyInspected = lastInspection != null && lastInspection.SubmitDataAt == null;

                            return new InspectionDeviceInfo
                            {
                                Device = device,
                                Status = status,
                                RequiresInspection = isRunning && !alreadyInspected,
                                LastInspectionTime = lastInspection?.ArriveAt,
                                LastInspectorName = lastInspection?.UserName
                            };
                        }).ToList(); 

                        period.DevicesToInspect = devicesToInspect;

                        // 統計（只統計當前時段的數據）
                        if (period.IsCurrent)
                        {
                            totalDevices += devicesToInspect.Count;
                            runningDevices += devicesToInspect.Count(d => d.IsRunning);
                            completedInspections += devicesToInspect.Count(d => d.LastInspectionTime.HasValue);
                            allDevicesForCurrentUser.AddRange(devicesToInspect);
                        }
                    }
                }

                // 5. 取得當前時段的區域（為了向後相容）
                var currentPeriod = timePeriods.FirstOrDefault(p => p.IsCurrent);
                var assignedAreas = currentPeriod?.Areas ?? new List<string>();

                return new UserTodayInspection
                {
                    UserNo = userNo,
                    UserName= userName,
                    Department = department,
                    TimePeriods = timePeriods,
                    TotalDevices = totalDevices,
                    RunningDevices = runningDevices,
                    CompletedInspections = completedInspections,

                    // 為了向後相容保留的屬性
                    AssignedAreas = assignedAreas,
                    DevicesToInspect = allDevicesForCurrentUser
                };
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

        public async Task<List<InspectionScheduleEvent>> GetUserAllTodaySchedulesAsync(string userNo, DateTime date)
        {
            using var connection = CreateMesConnection();

            var sql = @"
                    SELECT EventId, UserNo, UserName, Department, EventType, EventTypeName, EventDetail, 
                           StartDateTime, EndDateTime, Area, IsActive, 
                           CreateDate, CreateBy, UpdateDate, UpdateBy
                    FROM INSPECTION_SCHEDULE_EVENT 
                    WHERE UserNo = @UserNo 
                      AND IsActive = 1
                      AND CAST(StartDateTime AS DATE) = @Date
                    ORDER BY StartDateTime, Area";

            try
            {
                var scheduleEvents = await connection.QueryAsync<InspectionScheduleEvent>(sql, new
                {
                    UserNo = userNo,
                    Date = date.Date
                });

                var eventList = scheduleEvents.ToList();

                _logger.LogInformation("User {UserNo} has {Count} schedule events for date {Date}",
                    userNo, eventList.Count, date.Date);

                return eventList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user all schedules for {UserNo} on {Date}", userNo, date);
                throw;
            }
        }



        public List<TimePeriod> ProcessTimePeriodsData(List<InspectionScheduleEvent> scheduleEvents)
        {
            var currentTime = DateTime.Now;

            // 按時間分組排程
            var timeGroups = scheduleEvents
                .GroupBy(s => new { s.StartDateTime, s.EndDateTime, s.EventType,s.EventTypeName, s.EventDetail})
                .Select(g => new
                {
                    StartDateTime = g.Key.StartDateTime,
                    EndDateTime = g.Key.EndDateTime,
                    EventType = g.Key.EventType,
                    EventTypeName = g.Key.EventTypeName,
                    EventDetail = g.Key.EventDetail,
                    Areas = g.Select(s => s.Area).Distinct().ToList()
                })
                .OrderBy(g => g.StartDateTime)
                .ToList();

            var periods = new List<TimePeriod>();

            foreach (var group in timeGroups)
            {
                var isCurrent = group.StartDateTime <= currentTime && group.EndDateTime >= currentTime;
                var isPast = group.EndDateTime < currentTime;

                var period = new TimePeriod
                {
                    StartTime = group.StartDateTime.ToString("HH:mm"),
                    EndTime = group.EndDateTime.ToString("HH:mm"),
                    StartDateTime = group.StartDateTime,
                    EndDateTime = group.EndDateTime,
                    Areas = group.Areas,
                    EventType = group.EventType,
                    EventTypeName = group.EventTypeName,
                    EventDetail = group.EventDetail,
                    IsCurrent = isCurrent,
                    IsPast = isPast,
                    DevicesToInspect = new List<InspectionDeviceInfo>()
                };

                periods.Add(period);
            }

            return periods;
        }
    }
}