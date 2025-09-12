using Dapper;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;
using System.Data;
using System.Linq;

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

        public async Task<UserTodayInspection> GetTodayInspectionAsync(string userNo, string userName, string department)
        {
            var today = DateTime.Now.Date;

            try
            {
                // 1. 取得使用者今天的時段排班
                var timePeriods = await GetProcessedTimePeriodsAsync(userNo, today);

                // 2. 取得今天所有的巡檢紀錄 (不限使用者)
                var allTodayRecords = await GetAllTodayInspectionRecordsAsync(today);

                // 3. 取得裝置名稱對應表
                var deviceNameMapping = await GetDeviceNameMappingAsync();

                // 4. 為每個時段處理機台資料
                foreach (var period in timePeriods)
                {
                    if (!period.Areas.Any())
                        continue;

                    // 取得該時段負責區域的機台狀態和巡檢紀錄
                    var periodDeviceData = await GetDeviceInspectionDataAsync(period.Areas, today);

                    // 分析並組織資料
                    var (devicesToInspect, extraTasks) = ProcessDeviceInspectionData(
                        periodDeviceData,
                        allTodayRecords,
                        deviceNameMapping,
                        userNo,
                        period.Areas
                    );

                    period.DevicesToInspect = devicesToInspect;
                    period.ExtraTask = extraTasks;
                }

                return new UserTodayInspection
                {
                    UserNo = userNo,
                    UserName = userName,
                    Department = department,
                    TimePeriods = timePeriods
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user today inspection for {UserNo}", userNo);
                throw;
            }
        }

        private async Task<List<TimePeriod>> GetProcessedTimePeriodsAsync(string userNo, DateTime date)
        {
            using var connection = CreateMesConnection();

            var sql = @"
                WITH TimeGroups AS (
                    SELECT 
                        StartDateTime,
                        EndDateTime,
                        EventType,
                        EventDetail,
                        STRING_AGG(Area, ',') AS Areas
                    FROM INSPECTION_SCHEDULE_EVENT 
                    WHERE UserNo = @UserNo 
                      AND CAST(StartDateTime AS DATE) = @Date
                    GROUP BY StartDateTime, EndDateTime, EventType, EventDetail
                )
                SELECT 
                    StartDateTime,
                    EndDateTime,
                    EventType,
                    EventDetail,
                    Areas,
                    FORMAT(StartDateTime, 'HH:mm') AS StartTime,
                    FORMAT(EndDateTime, 'HH:mm') AS EndTime,
                    CASE 
                        WHEN StartDateTime <= GETDATE() AND EndDateTime >= GETDATE() THEN 1 
                        ELSE 0 
                    END AS IsCurrent,
                    CASE 
                        WHEN EndDateTime < GETDATE() THEN 1 
                        ELSE 0 
                    END AS IsPast
                FROM TimeGroups
                ORDER BY StartDateTime";

            try
            {
                var results = await connection.QueryAsync(sql, new { UserNo = userNo, Date = date.Date });

                return results.Select(r => new TimePeriod
                {
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    StartDateTime = r.StartDateTime,
                    EndDateTime = r.EndDateTime,
                    Areas = ((r.Areas as string) ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .ToList(),
                    EventType = r.EventType,
                    EventDetail = r.EventDetail,
                    IsCurrent = r.IsCurrent == 1,
                    IsPast = r.IsPast == 1,
                    DevicesToInspect = new List<DeviceInspectionInfo>(),
                    ExtraTask = new List<DeviceInspectionInfo>()
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting processed time periods for {UserNo} on {Date}", userNo, date);
                throw;
            }
        }

        private async Task<List<DeviceInspectionRawData>> GetDeviceInspectionDataAsync(List<string> areas, DateTime date)
        {
            if (!areas.Any()) return new List<DeviceInspectionRawData>();

            using var connection = CreateFineReportConnection();

            var sql = @"
                WITH deviceIds AS (
                    SELECT DeviceId, DeviceName
                    FROM TNCIMDEV01.MES_DEV.dbo.INSPECTION_DEVICE_AREA_MAPPING
                    WHERE Area IN @Areas AND IsActive = 1
                ),
                machineStatus AS (
                    SELECT DeviceID, DeviceStatus, StartTime, CreateTime, WO_ID, BPM_NO
                    FROM FineReport.dbo.FN_EQPSTATUS 
                    WHERE DeviceID IN (SELECT DeviceId FROM deviceIds) 
                )
                SELECT 
                    ms.DeviceID,
                    ms.DeviceStatus as Status,
                    ms.StartTime,
                    ms.CreateTime,
                    ms.WO_ID,
                    ms.BPM_NO,
                    iqc.RecordId,
                    iqc.CardId,
                    iqc.DeviceId,
                    iqc.UserNo,
                    iqc.UserName,
                    iqc.InspectType,
                    iqc.InspectWo,
                    iqc.ArriveAt,
                    iqc.SubmitDataAt,
                    iqc.Source,
                    iqc.CreateDate AS RecordCreateDate,
                    d.DeviceName
                FROM machineStatus ms
                LEFT JOIN deviceIds d ON ms.DeviceID = d.DeviceId
                LEFT JOIN TNCIMDEV01.MES_DEV.dbo.INSPECTION_QC_RECORD iqc 
                    ON ms.DeviceID = iqc.DeviceId 
                    AND CAST(iqc.ArriveAt AS DATE) = @Date
                WHERE (iqc.InspectType <> 'CANCEL' OR iqc.InspectType IS NULL)
                ORDER BY ms.DeviceID, iqc.ArriveAt DESC";

            try
            {
                var results = await connection.QueryAsync<DeviceInspectionRawData>(sql, new { Areas = areas, Date = date });
                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device inspection data for areas: {Areas}", string.Join(",", areas));
                throw;
            }
        }

        private async Task<List<InspectionQcRecord>> GetAllTodayInspectionRecordsAsync(DateTime date)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT RecordId, CardId, DeviceId, UserNo, UserName, InspectType,
                       ArriveAt, SubmitDataAt, Source, CreateDate,
                       InspectItemOkNo, InspectItemNgNo
                FROM INSPECTION_QC_RECORD 
                WHERE CAST(ArriveAt AS DATE) = @Date
                  AND InspectType <> 'CANCEL'
                ORDER BY ArriveAt DESC";

            try
            {
                var records = await connection.QueryAsync<InspectionQcRecord>(sql, new { Date = date.Date });
                return records.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all today inspection records on {Date}", date);
                throw;
            }
        }

        private async Task<Dictionary<string, string>> GetDeviceNameMappingAsync()
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT DeviceId, DeviceName 
                FROM INSPECTION_DEVICE_AREA_MAPPING 
                WHERE IsActive = 1";

            try
            {
                var mapping = await connection.QueryAsync(sql);
                return mapping.ToDictionary(
                    x => (string)x.DeviceId,
                    x => (string)(x.DeviceName ?? x.DeviceId)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device name mapping");
                throw;
            }
        }


        private (List<DeviceInspectionInfo> devicesToInspect, List<DeviceInspectionInfo> extraTasks)
            ProcessDeviceInspectionData( List<DeviceInspectionRawData> rawData, List<InspectionQcRecord> allTodayRecords,
                Dictionary<string, string> deviceNameMapping, string currentUserNo, List<string> periodAreas)
        {
            var devicesToInspect = new List<DeviceInspectionInfo>();
            var extraTasks = new List<DeviceInspectionInfo>();

            // 將原始資料按 DeviceID 分組
            var deviceGroups = rawData.GroupBy(x => x.DeviceID).ToDictionary(g => g.Key, g => g.ToList());

            // 處理該時段負責區域的機台
            foreach (var deviceGroup in deviceGroups)
            {
                var deviceId = deviceGroup.Key;
                var deviceRecords = deviceGroup.Value;

                // 取得機台狀態 (第一筆記錄的機台狀態)
                var firstRecord = deviceRecords.First();
                var deviceStatus = new DeviceStatus
                {
                    DeviceID = firstRecord.DeviceID,
                    DeviceName = deviceNameMapping.GetValueOrDefault(firstRecord.DeviceID, firstRecord.DeviceID),
                    Status = firstRecord.Status,
                    StartTime = firstRecord.StartTime,
                    CreateTime = firstRecord.CreateTime,
                    WO_ID = firstRecord.WO_ID ?? string.Empty,
                    BPM_NO = firstRecord.BPM_NO ?? string.Empty
                };

                // 處理巡檢紀錄
                var inspectionRecords = deviceRecords
                    .Where(r => r.RecordId.HasValue)
                    .OrderByDescending(r => r.ArriveAt)
                    .Select(r => new InspectionRecord
                    {
                        Time = FormatInspectionTime(r.ArriveAt!.Value, r.SubmitDataAt),
                        Inspector = r.UserName ?? "Unknown",
                        InspectWo = r.InspectWo ?? ""
                    })
                    .ToList();

                var deviceInfo = new DeviceInspectionInfo
                {
                    DeviceStatus = deviceStatus,
                    InspectionList = inspectionRecords
                };

                devicesToInspect.Add(deviceInfo);
            }

            // 處理額外任務 (當前使用者在非負責區域進行的巡檢)
            var currentUserRecords = allTodayRecords.Where(r => r.UserNo == currentUserNo).ToList();

            foreach (var record in currentUserRecords)
            {
                // 檢查該機台是否不在當前時段的負責區域中
                if (!deviceGroups.ContainsKey(record.DeviceId))
                {
                    var deviceStatus = new DeviceStatus
                    {
                        DeviceID = record.DeviceId,
                        DeviceName = deviceNameMapping.GetValueOrDefault(record.DeviceId, record.DeviceId),
                        // 這裡可能需要額外查詢機台狀態，暫時用預設值
                        Status = "---",
                        WO_ID = string.Empty,
                        BPM_NO = string.Empty
                    };

                    var inspectionRecord = new InspectionRecord
                    {
                        Time = FormatInspectionTime(record.ArriveAt, record.SubmitDataAt),
                        Inspector = record.UserName
                    };

                    var extraTaskInfo = new DeviceInspectionInfo
                    {
                        DeviceStatus = deviceStatus,
                        InspectionList = new List<InspectionRecord> { inspectionRecord }
                    };

                    extraTasks.Add(extraTaskInfo);
                }
            }

            return (devicesToInspect, extraTasks);
        }

        private static string FormatInspectionTime(DateTime arriveAt, DateTime? submitDataAt)
        {
            var arriveTime = arriveAt.ToString("HH:mm");
            var submitTime = submitDataAt?.ToString("HH:mm") ?? "檢驗中";
            return $"{arriveTime} - {submitTime}";
        }


        //==============================讀取NFC並新增===========================================
        //========================================================================================================

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

        public async Task<InspectionDeviceAreaMapping?> FindNFCcard(string nfcId)
        {
            using var connection = CreateMesConnection();

            var sql = @"
                    SELECT Area, DeviceId, DeviceName from INSPECTION_DEVICE_AREA_MAPPING where NfcCardId = @nfcId and IsActive = 1";


            try
            {
                var nfcInfo = await connection.QueryFirstOrDefaultAsync<InspectionDeviceAreaMapping>(sql, new {nfcId});

                return nfcInfo;
            }
            catch (Exception ex)
            {
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

        //==============================作廢區=====================================
        //========================================================================================================



    }
}