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
        private readonly string _FnReportConn;
        private readonly int _envFlag;

        public ActivityChartRepository(IConfiguration configuration, IOptions<AppSettings> appSettings, ILogger<ActivityChartRepository> logger)
        {
            _logger = logger;
            _envFlag = appSettings.Value.EnvFlag;
            _logger.LogInformation("Current EnvFlag: {EnvFlag}", _envFlag);
            var MesConnKey = EnvironmentHelper.GetMesConnectionStringKey(_envFlag);
            _mesConnString = configuration.GetConnectionString(MesConnKey)
                ?? throw new ArgumentNullException($"ConnectionString '{MesConnKey}' not found");

            var FnReportConnKey = EnvironmentHelper.GetFnReportConnectionStringKey(_envFlag);
            _FnReportConn = configuration.GetConnectionString(FnReportConnKey)
                ?? throw new ArgumentNullException($"ConnectionString '{FnReportConnKey}' not found");
        }

        private IDbConnection CreateMesConnection()
        {
            return new SqlConnection(_mesConnString);
        }

        private IDbConnection CreateFnReportConnection()
        {
            return new SqlConnection(_FnReportConn);
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
                InspectItemNgNo,
                Remark
            FROM INSPECTION_QC_RECORD                
            WHERE ArriveAt >= @StartDateTime 
              AND ArriveAt < @EndDateTime
            AND (InspectType <> 'CANCEL' OR InspectType IS NULL)
            AND (Remark IS NULL OR Remark <> 'CANCEL THIS ACTION')

            --UNION ALL

            --SELECT 
            --    NULL AS RecordId,              
            --    ''   AS CardId,
            --    ''   AS Area,
            --    ''   AS DeviceId,
            --    a.UserNo,
            --    a.UserName,
            --    SUBSTRING(b.StatusName_TW, 4, LEN(b.StatusName_TW)) AS InspectType,
            --    '' AS InspectWo,
            --    a.StartTime AS ArriveAt,
            --    a.EndTime AS SubmitDataAt,
            --    'ABC報工' AS Source,
            --   null AS InspectItemOkNo,       
            --    null AS InspectItemNgNo,
            --    '' AS Remark
            --FROM [ABC_USER_WH] a
            --LEFT JOIN ABC_BAS_STATUS b ON b.StatusNo = a.StatusNo
            --WHERE a.UserNo IN ('G03078', 'G01629', 'G01824', 'G02449', 'G01813')
            --  AND a.StatusNo IN ('0007','0008','0005')
            --AND a.StartTime >= @StartDateTime 
            --AND a.StartTime < @EndDateTime
            --ORDER BY UserNo, ArriveAt;";
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
                    InspectItemNgNo,
                    Remark
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
        public async Task<List<EqpOOSInspectionActivity>> GetEQPOOSActivitiesByDateAsync(string eqpNO, DateTime date)
        {
            using var connection = CreateFnReportConnection();
            //var sql = @"
            //    SELECT distinct DeviceID EqpNo,Description EqpName,
            //          CASE --WHEN BPM_NO <> '' THEN '借機'
            //     	WHEN DeviceSchedulingStatus <> '' THEN DeviceSchedulingStatus
            //    ELSE Status
            //    END AS Status, 工單 InspectWo,Param_Name4,CASE WHEN OOC_FLAG=0 THEN 'OOS' WHEN OOC_FLAG=1 THEN 'OOC' ELSE '無OOS/OOC' END InspectType,
            //    StartTime ArriveAt, NULL AS SubmitDataAt,FACTORY_NO Fab,工段 Area,料號 PartNo,Param_Index,OOSalarmTime
            //    FROM (
            //    	SELECT St.DeviceID,mes.工單,st.BPM_NO,st.DeviceSchedulingStatus,
            //         Description,
            //        CASE 
            //             WHEN St.WO_ID = '' AND BPM_NO <> '' THEN DeviceStatus  + '_'+'借機'  
            //       	    WHEN St.WO_ID = '' AND St.DeviceStatus = 'RUN' THEN DeviceStatus + '_' +'運作無工單'
            //             WHEN St.WO_ID <> '' AND St.DeviceStatus = 'IDLE' and DATEDIFF(minute,StartTime,GETDATE())>30 THEN DeviceStatus + '_' +'閒置有工單'
            //      	ELSE DeviceStatus  + '_' +'正常'
            //      	END AS Status,OOS.Param_Name4,OOS.OOC_FLAG,St.StartTime,mes.FACTORY_NO,mes.工段,mes.料號,OOS.Param_Index,OOS.start_time OOSalarmTime
            //    		FROM FineReport.dbo.FN_EQPSTATUS St WITH (NOLOCK)
            //    	JOIN
            //       	 FN_DEVICE_MAP d WITH (NOLOCK) ON St.DeviceID = d.DeviceID
            //       	JOIN 
            //       	 FineReport.dbo.FN_MESWIP mes  WITH (NOLOCK) ON d.GdDeviceID =mes.Machine_No and 狀態 = '已進站'
            //       	LEFT JOIN 
            //       	 TNSPCETLDB.SPC_Interface_DB.dbo.SPC_ALARM_ETL_Data OOS ON St.WO_ID=OOS.ITEM_NAME1 and convert(char(10),OOS.start_time,111) >='2025/10/23'
            //      	 WHERE d.Dept LIKE '%射出%' AND d.DeviceID =@eqpNO AND convert(char(10),St.StartTime,111)>='2025/10/23'-- 'FANUC_100T-04'
            //    )as tt 
            //                    --AND CAST(ArriveAt AS DATE) = @Date
            //                    ORDER BY ArriveAt";

            var sql = @" WITH BaseStaticData AS (
    SELECT
        St.DeviceID,
        mes.工單 AS InspectWo,
        d.Description,
        mes.FACTORY_NO AS Fab,
        mes.工段 AS Area,
        mes.料號 AS PartNo,
        --MIN('2025-10-30 01:47:59.000') AS ArriveAt, -- 【關鍵】取得工單最早進站時間
        MIN(St.StartTime) AS ArriveAt,
        MAX(CASE WHEN St.WO_ID = '' AND St.DeviceStatus = 'RUN' THEN DeviceStatus + '_運作無工單' ELSE DeviceStatus + '_正常' END) AS Status,
        -- 確保工單/設備組合唯一
        ROW_NUMBER() OVER(ORDER BY St.DeviceID, mes.工單) AS RN
    FROM FineReport.dbo.FN_EQPSTATUS AS St WITH (NOLOCK)
    JOIN FN_DEVICE_MAP AS d WITH (NOLOCK) ON St.DeviceID = d.DeviceID
    JOIN FineReport.dbo.FN_MESWIP AS mes WITH (NOLOCK)
        ON d.GdDeviceID = mes.Machine_No AND mes.狀態 = '已進站' AND mes.DATA_SOURCE ='GD'
    WHERE d.Dept LIKE '%射出%' AND St.DeviceID=@eqpNO
      AND CONVERT(CHAR(10), St.StartTime, 111) >= '2025/10/23'
    GROUP BY St.DeviceID, mes.工單, d.Description, mes.FACTORY_NO, mes.工段, mes.料號
),
-- 2. 獨立提取 OOS 警報時間點並進行篩選
OOS_Filtered AS (
    SELECT DISTINCT
        OOS.start_time AS OOSalarmTime,
        St.DeviceID,
        mes.工單 AS InspectWo,
        OOS.COMMENTS_TIME SubmitDataAt,
        OOS.Param_Name4,
        OOS.Param_Index,
        OOS.COMMENTS_USER_ID+'_'+OOS.COMMENTS+'_'+CONVERT(CHAR(19),OOS.COMMENTS_TIME,121) CommentsDataAt,
        CASE
        WHEN OOS.OOC_FLAG = 0 THEN 'OOS'
        WHEN OOS.OOC_FLAG = 1 THEN 'OOC'
        ELSE '無OOS/OOC' END AS InspectType
    FROM FineReport.dbo.FN_EQPSTATUS AS St WITH (NOLOCK)
    JOIN FN_DEVICE_MAP AS d WITH (NOLOCK) ON St.DeviceID = d.DeviceID
    JOIN FineReport.dbo.FN_MESWIP AS mes WITH (NOLOCK)
        ON d.GdDeviceID = mes.Machine_No AND mes.狀態 = '已進站'
    JOIN TNSPCETLDB.SPC_Interface_DB.dbo.SPC_ALARM_ETL_Data AS OOS WITH (NOLOCK)
        ON St.WO_ID = OOS.ITEM_NAME1
    WHERE St.DeviceID = @eqpNO
),
OOS_Processed AS (
    -- 確保只選取有效的 OOS 記錄，並計算 NextOOS
    SELECT
        T1.OOSalarmTime,
        LEAD(T1.OOSalarmTime) OVER (ORDER BY T1.OOSalarmTime) AS NextOOS,
        T1.SubmitDataAt,
        T1.InspectType,
        T1.Param_Name4,
        T1.Param_Index,
        T1.CommentsDataAt
    FROM OOS_Filtered T1
    JOIN BaseStaticData T2
        ON T1.DeviceID = T2.DeviceID AND T1.InspectWo = T2.InspectWo
    WHERE T1.OOSalarmTime > T2.ArriveAt -- 核心篩選條件：OOS 晚於工單開始時間
),
-- 3. 處理 ""有 OOS 警報"" 的時間軸 (與您原來的邏輯相同)
StatusTimeline_WithOOS AS (
    -- 區間 A: 工單起始時間 到 第一筆 OOS 警報時間 (無OOS/OOC)
    SELECT
        b.ArriveAt AS StartTime,
        (SELECT MIN(OOSalarmTime) FROM OOS_Processed) AS EndTime,
        '無OOS/OOC' AS InspectType,
        NULL AS Param_Name4,
        NULL AS Param_Index,
        NULL AS CommentsDataAt
    FROM BaseStaticData b
    WHERE b.ArriveAt < (SELECT MIN(OOSalarmTime) FROM OOS_Processed)

    UNION ALL

    -- 區間 B: OOS 警報時間 到 提交時間或 OOS+5分鐘 (OOS/OOC)
    SELECT
        OOSalarmTime AS StartTime,
        ISNULL(SubmitDataAt,DATEADD(MINUTE, 5, OOSalarmTime)) AS EndTime,
        InspectType,
        Param_Name4,
        Param_Index,
        CommentsDataAt
    FROM OOS_Processed
    WHERE OOSalarmTime IS NOT NULL

    UNION ALL

    -- 區間 C: 提交時間或 OOS+5分鐘 到 下一筆 OOS警報時間 (無OOS/OOC)
    SELECT
        ISNULL(SubmitDataAt,DATEADD(MINUTE, 5, OOSalarmTime)) AS StartTime,
        NextOOS AS EndTime,
        '無OOS/OOC' AS InspectType,
        NULL AS Param_Name4,
        NULL AS Param_Index,
        NULL AS CommentsDataAt
    FROM OOS_Processed
    WHERE NextOOS IS NOT NULL
      AND ISNULL(SubmitDataAt,DATEADD(MINUTE, 5, OOSalarmTime)) < NextOOS

    UNION ALL

    -- 區間 D: 最後一筆 OOS 結束時間 到 查詢當前時間 (無OOS/OOC)
    SELECT
        ISNULL(SubmitDataAt,DATEADD(MINUTE, 5, OOSalarmTime)) AS StartTime,
        GETDATE() AS EndTime,
        '無OOS/OOC' AS InspectType,
        NULL AS Param_Name4,
        NULL AS Param_Index,
        NULL AS CommentsDataAt
    FROM OOS_Processed
    WHERE NextOOS IS NULL
      AND ISNULL(SubmitDataAt,DATEADD(MINUTE, 5, OOSalarmTime)) < GETDATE()
),
-- 4. 處理 ""無 OOS 警報"" 的時間軸
StatusTimeline_NoOOS AS (
    SELECT
        ArriveAt AS StartTime,
        GETDATE() AS EndTime,
        '無OOS/OOC' AS InspectType,
        NULL AS Param_Name4,
        NULL AS Param_Index,
        NULL AS CommentsDataAt
    FROM BaseStaticData
    WHERE ArriveAt < GETDATE() -- 確保開始時間早於現在
)
-- 5. 組合最終結果：根據 OOS 數據是否存在來選擇時間軸
SELECT
    b.DeviceID AS EqpNo,
    b.Description AS EqpName,
    b.Status,
    b.InspectWo,
    s.Param_Name4,
    s.InspectType,
    s.StartTime ArriveAt,
    s.EndTime SubmitDataAt,
    b.Fab,
    b.Area,
    b.PartNo,
    s.Param_Index,
    b.ArriveAt WoCheckInAt,
    s.CommentsDataAt
FROM BaseStaticData b
CROSS APPLY (
    -- 【核心判斷邏輯】
    SELECT COUNT(*) FROM OOS_Processed
) AS OOS_Check(OOS_Count) -- 檢查 OOS_Processed 中是否有數據

CROSS APPLY (
    -- 如果有 OOS 數據 (OOS_Count > 0)，則使用 StatusTimeline_WithOOS
    SELECT * FROM StatusTimeline_WithOOS
    WHERE OOS_Check.OOS_Count > 0

    UNION ALL

    -- 如果沒有 OOS 數據 (OOS_Count = 0)，則使用 StatusTimeline_NoOOS
    SELECT * FROM StatusTimeline_NoOOS
    WHERE OOS_Check.OOS_Count = 0
) AS s -- 選擇的時間軸結果
WHERE s.StartTime < s.EndTime and convert(char(10),s.StartTime,111)>= convert(char(10),GETDATE(),111) --只秀當天紀錄
ORDER BY s.StartTime";
            try
            {
                var activities = await connection.QueryAsync<EqpOOSInspectionActivity>(sql, new { eqpNo = eqpNO, Date = date.Date });
                return activities.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting eqp OOS activities: {eqpNO}, {Date}", eqpNO, date);
                throw;
            }
        }

    }
}
