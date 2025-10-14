using Dapper;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;
using System.Data;
using System.Linq;
using static PatrolInspect.Controllers.InspectionController;

namespace PatrolInspect.Repository
{
    public class InspectionRepository : IInspectionRepository
    {
        private readonly string _mesConnString;
        private readonly string _fnReportConnString;
        private readonly int _envFlag;
        private readonly ILogger<InspectionRepository> _logger;

        public InspectionRepository(IConfiguration configuration, IOptions<AppSettings> appSettings, ILogger<InspectionRepository> logger)
        {
            _logger = logger;
            _envFlag = appSettings.Value.EnvFlag;
            _logger.LogInformation("Current EnvFlag: {EnvFlag}", _envFlag);
            var MesConnKey = EnvironmentHelper.GetMesConnectionStringKey(_envFlag);
            _mesConnString = configuration.GetConnectionString(MesConnKey)
                ?? throw new ArgumentNullException($"ConnectionString '{MesConnKey}' not found");

            // FineReport 連線字串 (需要在 appsettings.json 中新增)
            var FnConnKey = EnvironmentHelper.GetFnReportConnectionStringKey(_envFlag);
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

        public async Task <List<string>> GetInspectTypeList()
        {
            using var connection = CreateMesConnection();
            var sql = @" SELECT EventType from INSPECTION_EVENT_TYPE_MASTER where IsActive = '1' order by SortOrder";

            try
            {
                var eventTypes = await connection.QueryAsync<string>(sql);
                return eventTypes.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department from INSPECTION_SCHEDULE_EVENT");
                throw;
            }
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
                    //if (!period.Areas.Any())
                    //    continue;

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

            var dbname = _envFlag.ToString() == "1" ? "TNCIMDB01.MES" : "TNCIMDEV01.MES_DEV";

            using var connection = CreateFineReportConnection();

            var sql = $@"
                WITH deviceIds AS (
                    SELECT DeviceId, DeviceName
                    FROM {dbname}.dbo.INSPECTION_DEVICE_AREA_MAPPING
                    WHERE Area IN @Areas AND IsActive = 1
                ),
                machineStatus AS (
                    SELECT d.DeviceID,fne.DeviceStatus, fnr.WO_ID
                    FROM deviceIds d 
                    LEFT JOIN FineReport.dbo.FN_ORDER_RUNTIME fnr on d.DeviceId = fnr.DeviceID
                    LEFT JOIN FineReport.dbo.FN_EQPSTATUS fne on d.deviceId = fne.DeviceID
                )
                SELECT 
                    ms.DeviceID,
                    ms.DeviceStatus as Status,
                    ms.WO_ID,
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
                LEFT JOIN {dbname}.dbo.INSPECTION_QC_RECORD iqc 
                    ON ms.DeviceID = iqc.DeviceId 
                    AND CAST(iqc.ArriveAt AS DATE) = @Date
                    AND ms.WO_ID = iqc.InspectWo
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
                SELECT RecordId, CardId, DeviceId, UserNo, UserName, InspectType, InspectWo,
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
                    WO_ID =  string.Join(",", deviceRecords
                    .Where(r=> !string.IsNullOrEmpty(r.WO_ID))
                    .Select(r=> r.WO_ID!)
                    .Distinct()
                    .OrderBy(x=>x)),
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
                        InspectWo = r.InspectWo ?? "",
                        RecordId = r.RecordId,
                        InspectType = r.InspectType,
                        InspectorId = r.UserNo
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
                        WO_ID = record.InspectWo ?? string.Empty,
                        BPM_NO = string.Empty
                    };

                    var inspectionRecord = new InspectionRecord
                    {
                        InspectWo = record.InspectWo ?? string.Empty,
                        RecordId = record.RecordId,
                        Time = FormatInspectionTime(record.ArriveAt, record.SubmitDataAt),
                        Inspector = record.UserName,
                        InspectorId = record.UserNo,
                        InspectType = record.InspectType
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






        public async Task<object> UpdateInspectionQuantityAsync(int recordId, int okQuantity, int ngQuantity, string updatedBy)
        {
            using var connection = CreateMesConnection();

            try
            {
                // 1. 檢查記錄是否存在且可以更新
                var checkSql = @"
                    SELECT RecordId, UserNo, DeviceId, InspectWo, ArriveAt, SubmitDataAt, 
                           InspectItemOkNo, InspectItemNgNo
                    FROM INSPECTION_QC_RECORD 
                    WHERE RecordId = @RecordId
                    AND SubmitDataAt is NULL";

                var existingRecord = await connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { RecordId = recordId });

                if (existingRecord == null)
                {
                    return new
                    {
                        Success = false,
                        Message = "找不到指定的檢驗記錄"
                    };
                }

                // 4. 更新檢驗數量
                var updateSql = @"
                        UPDATE INSPECTION_QC_RECORD 
                        SET InspectItemOkNo = @OkQuantity,
                            InspectItemNgNo = @NgQuantity,
                            SubmitDataAt = GETDATE()
                        WHERE RecordId = @RecordId
                        AND SubmitDataAt is null";

                var updateResult = await connection.ExecuteAsync(
                    updateSql,
                    new
                    {
                        RecordId = recordId,
                        OkQuantity = okQuantity.ToString(),
                        NgQuantity = ngQuantity.ToString()
                    }
                );

                if (updateResult == 0)
                {
                    return new
                    {
                        Success = false,
                        Message = "更新失敗，請稍後再試"
                    };
                }


                return new
                {
                    Success = true,
                    Message = "檢驗數量更新成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新檢驗數量時發生錯誤: RecordId={RecordId}", recordId);
                throw; // 讓Controller處理異常
            }
        }
        //==============================讀取NFC並新增===========================================
        //========================================================================================================

        public async Task<int> CreateInspectionRecordAsync(InspectionQcRecord record)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                INSERT INTO INSPECTION_QC_RECORD 
                (CardId, DeviceId, UserNo, UserName,Area, InspectType, InspectWo, ArriveAt, SubmitDataAt, Source, CreateDate)
                VALUES 
                (@CardId, @DeviceId, @UserNo, @UserName,@Area, @InspectType, @InspectWo, @ArriveAt, @SubmitDataAt, @Source, @CreateDate);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            if (!string.IsNullOrWhiteSpace(record.UserInputWorkOrderNo))
            {
                record.InspectWo = record.UserInputWorkOrderNo ?? "";
            }

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

        public async Task<bool> UpdateInspectionRecordAsync(int recordId, string  deviceId, string userNo)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                UPDATE INSPECTION_QC_RECORD 
                SET InspectType = 'CANCEL', 
                    SubmitDataAt = GETDATE()
                WHERE DeviceId <> @deviceId 
                  AND UserNo = @UserNo 
                  AND SubmitDataAt IS NULL"; 

            try
            {
                var rowsAffected = await connection.ExecuteAsync(sql, new { DeviceId = deviceId, UserNo = userNo });

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

        public async Task<List<InspectionDeviceAreaMappingDto>> FindNFCcard(string nfcId)
        {
            using var connection = CreateFineReportConnection();

            var dbname = _envFlag.ToString() == "1" ? "TNCIMDB01.MES" : "TNCIMDEV01.MES_DEV";

            var sql = $@"
                        with nfcInfo as (
                        SELECT Area, DeviceId, DeviceName, NfcCardId from {dbname}.dbo.INSPECTION_DEVICE_AREA_MAPPING 
                        where 1=1 and NfcCardId = @nfcId and IsActive = 1
                    	)
                        Select d.*, fnr.WO_ID as InspectWo from nfcInfo d
                        LEFT JOIN FineReport.dbo.FN_ORDER_RUNTIME fnr on d.DeviceId = fnr.DeviceID
    					LEFT JOIN FineReport.dbo.FN_EQPSTATUS fne on d.deviceId = fne.DeviceID";


            try
            {
                var nfcInfo = await connection.QueryAsync<InspectionDeviceAreaMappingDto>(sql, new {nfcId});

                return nfcInfo.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<InspectionQcRecord>> GetPendingInspectionByUserAsync(string userNo)
        {
            using var connection = CreateMesConnection();
            var sql = @"
                SELECT 
                    RecordId, CardId, DeviceId, UserNo, UserName, InspectType,
                    ArriveAt, SubmitDataAt, Source, CreateDate
                FROM INSPECTION_QC_RECORD 
                WHERE UserNo = @UserNo 
                  AND SubmitDataAt IS NULL
                ORDER BY ArriveAt DESC";

            try
            {
                var record = await connection.QueryAsync<InspectionQcRecord>(sql, new { UserNo = userNo });

                return record.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending inspection for user: {UserNo}", userNo);
                throw;
            }
        }

        public async Task<(bool Success, string Message)> SubmitWarehouseInspectionAsync(int originalRecordId, List<WarehouseOrderInfo> orders, string userNo)
        {
            await using var connection = (SqlConnection)CreateMesConnection();

            await connection.OpenAsync();


            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. 取得原始記錄資料
                var originalRecord = await GetOriginalInspectionRecordAsync(connection, transaction, originalRecordId, userNo);
                if (originalRecord == null)
                {
                    return (false, "找不到指定的檢驗記錄或您沒有權限修改");
                }

                // 2. 驗證記錄狀態
                if (originalRecord.SubmitDataAt.HasValue)
                {
                    return (false, "該檢驗記錄已經完成，無法重複提交");
                }

                // 3. 更新原始記錄為第一筆工單
                var firstOrder = orders.First();
                await UpdateOriginalRecordAsync(connection, transaction, originalRecordId, firstOrder);

                // 4. 為其餘工單創建新記錄
                if (orders.Count > 1)
                {
                    var remainingOrders = orders.Skip(1);
                    await CreateAdditionalWarehouseRecordsAsync(connection, transaction, originalRecord, remainingOrders);
                }

                // 5. 記錄處理日誌
                await LogWarehouseInspectionAsync(connection, transaction, originalRecordId, orders, userNo);

                transaction.Commit();

                _logger.LogInformation(
                    "入庫檢驗提交成功: RecordId={RecordId}, OrderCount={OrderCount}, UserNo={UserNo}",
                    originalRecordId, orders.Count, userNo);

                return (true, "入庫檢驗提交成功");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "提交入庫檢驗時發生錯誤: RecordId={RecordId}", originalRecordId);
                throw;
            }
        }

        private async Task<InspectionQcRecord?> GetOriginalInspectionRecordAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            int recordId,
            string userNo)
        {
            var sql = @"
                SELECT RecordId, CardId, DeviceId, UserNo, UserName, Area, InspectType,
                       InspectWo, ArriveAt, SubmitDataAt, Source, CreateDate
                FROM INSPECTION_QC_RECORD 
                WHERE RecordId = @RecordId AND UserNo = @UserNo";

            return await connection.QueryFirstOrDefaultAsync<InspectionQcRecord>(
                sql,
                new { RecordId = recordId, UserNo = userNo },
                transaction);
        }

        private async Task UpdateOriginalRecordAsync(IDbConnection connection, IDbTransaction transaction, int recordId, WarehouseOrderInfo orderInfo)
        {
            var updateSql = @"
                UPDATE INSPECTION_QC_RECORD 
                SET InspectWo = @WorkOrder,
                    InspectItemOkNo = @OkQuantity,
                    InspectItemNgNo = @NgQuantity,
                    SubmitDataAt = GETDATE()
                WHERE RecordId = @RecordId";

            await connection.ExecuteAsync(updateSql, new
            {
                RecordId = recordId,
                WorkOrder = orderInfo.WorkOrder,
                OkQuantity = orderInfo.OkQuantity.ToString(),
                NgQuantity = orderInfo.NgQuantity.ToString()
            }, transaction);
        }

        private async Task CreateAdditionalWarehouseRecordsAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            InspectionQcRecord originalRecord,
            IEnumerable<WarehouseOrderInfo> orders)
        {
            var insertSql = @"
                INSERT INTO INSPECTION_QC_RECORD 
                (CardId, DeviceId, UserNo, UserName, Area, InspectType, InspectWo, 
                 ArriveAt, SubmitDataAt, Source, CreateDate, InspectItemOkNo, InspectItemNgNo)
                VALUES 
                (@CardId, @DeviceId, @UserNo, @UserName, @Area, @InspectType, @InspectWo, 
                 @ArriveAt, @SubmitDataAt, @Source, @CreateDate, @InspectItemOkNo, @InspectItemNgNo)";

            foreach (var order in orders)
            {
                var newRecord = new
                {
                    CardId = originalRecord.CardId,
                    DeviceId = originalRecord.DeviceId,
                    UserNo = originalRecord.UserNo,
                    UserName = originalRecord.UserName,
                    Area = originalRecord.Area,
                    InspectType = originalRecord.InspectType,
                    InspectWo = order.WorkOrder,
                    ArriveAt = originalRecord.ArriveAt,
                    SubmitDataAt = DateTime.Now,
                    Source = originalRecord.Source,
                    CreateDate = DateTime.Now,
                    InspectItemOkNo = order.OkQuantity.ToString(),
                    InspectItemNgNo = order.NgQuantity.ToString(),
                };

                await connection.ExecuteAsync(insertSql, newRecord, transaction);
            }
        }

        private async Task LogWarehouseInspectionAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            int originalRecordId,
            List<WarehouseOrderInfo> orders,
            string userNo)
        {
            // 可選：如果需要記錄處理日誌的話
            var logSql = @"
                INSERT INTO INSPECTION_WAREHOUSE_LOG 
                (OriginalRecordId, UserNo, OrderCount, ProcessTime, OrderDetails)
                VALUES 
                (@OriginalRecordId, @UserNo, @OrderCount, @ProcessTime, @OrderDetails)";

            var orderDetails = string.Join(";", orders.Select(o => $"{o.WorkOrder}:{o.OkQuantity}:{o.NgQuantity}"));

            try
            {
                await connection.ExecuteAsync(logSql, new
                {
                    OriginalRecordId = originalRecordId,
                    UserNo = userNo,
                    OrderCount = orders.Count,
                    ProcessTime = DateTime.Now,
                    OrderDetails = orderDetails
                }, transaction);
            }
            catch (Exception ex)
            {
                // 日誌記錄失敗不影響主要流程
                _logger.LogWarning(ex, "記錄入庫檢驗日誌失敗: RecordId={RecordId}", originalRecordId);
            }
        }


        //==============================================================================================

        

    }
}