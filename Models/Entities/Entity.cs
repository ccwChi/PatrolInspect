using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PatrolInspect.Models.Entities
{
    /// <summary>
    /// QC巡檢記錄實體
    /// </summary>
    public class InspectionQcRecord
    {
        public int RecordId { get; set; }
        public string CardId { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string InspectType { get; set; } = string.Empty;
        public string? InspectWo { get; set; }
        public DateTime ArriveAt { get; set; }
        public DateTime? SubmitDataAt { get; set; }
        public string Source { get; set; } = "NFC";
        public DateTime CreateDate { get; set; }
        public string? InspectItemOkNo { get; set; }
        public string? InspectItemNgNo { get; set; }
        public string UserInputWorkOrderNo { get; set; } = string.Empty;
    }

    /// <summary>
    /// 巡檢排程事件實體 INSPECTION_SCHEDULE_EVENT
    /// </summary>
    public class InspectionScheduleEvent
    {
        public int EventId { get; set; }
        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string? EventDetail { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Area { get; set; } = string.Empty;
        public DateTime CreateDate { get; set; } = DateTime.Now;
        public string? CreateBy { get; set; }

        public DateTime? UpdateDate { get; set; }
        public string? UpdateBy { get; set; }
    }

    /// <summary>
    /// 機台區域對應實體 INSPECTION_DEVICE_AREA_MAPPING
    /// </summary>
    public class InspectionDeviceAreaMapping
    {
        public int DeviceLocateId { get; set; }
        public int AreaId { get; set; }
        public string Area { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string? DeviceName { get; set; }
        public string? NfcCardId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreateDate { get; set; } = DateTime.Now;
        public string? CreateBy { get; set; }
        public DateTime? UpdateDate { get; set; }
        public string? UpdateBy { get; set; }
    }

    public class InspectionDeviceAreaMappingDto
    {

        public string Area { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string? DeviceName { get; set; }
        public string? NfcCardId { get; set; }
        public string? InspectWo { get; set; }
        public string InspectType { get; set; } = string.Empty;

    }
    /// <summary>
    /// FN_EQPSTATUS
    /// </summary>
    public class FnDeviceStatus
    {
        public string DeviceID { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartTime { get; set; }
        public string? Duration { get; set; }
        public string? AlarmMessage { get; set; }
        public DateTime CreateTime { get; set; }
        public string? WO_ID { get; set; }
        public string? EternetStatus { get; set; }
        public string? BPM_NO { get; set; }
        public string? DeviceSchedulingStatus { get; set; }
        public string? DeviceName { get; internal set; }
    }

    public class ScheduleBaseInfo
    {
        public List<string> ScheduleUserNames { get; set; } = new();
        public List<string> ScheduleDepartments { get; set; } = new();
        public List<string> Areas { get; set; } = new();
        public List<string> EventTypes { get; set; } = new();
        public List<MesUserDto> Users { get; set; } = new();

    }

    public class InspectionItem
    {
        public int ItemId { get; set; }

        [Required(ErrorMessage = "檢驗項目名稱為必填")]
        [StringLength(100, ErrorMessage = "檢驗項目名稱不能超過100字")]
        public string InspectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "部門為必填")]
        [StringLength(50, ErrorMessage = "部門名稱不能超過50字")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "檢驗區域為必填")]
        [StringLength(50, ErrorMessage = "檢驗區域不能超過50字")]
        public string InspectArea { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "站點名稱不能超過50字")]
        public string? Station { get; set; }

        [Required(ErrorMessage = "資料類型為必填")]
        [StringLength(20, ErrorMessage = "資料類型不能超過20字")]
        public string DataType { get; set; } = "TEXT";

        [StringLength(999, ErrorMessage = "選項內容不能超過999字")]
        public string? SelectOptions { get; set; }

        public bool IsRequired { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreateDate { get; set; } = DateTime.Now;

        [StringLength(50, ErrorMessage = "建立者不能超過50字")]
        public string? CreateBy { get; set; }

        public DateTime? UpdateDate { get; set; }

        [StringLength(50, ErrorMessage = "更新者不能超過50字")]
        public string? UpdateBy { get; set; }

        [StringLength(50, ErrorMessage = "更新原因不能超過50字")]
        public string? UpdateReason { get; set; }

        // 計算屬性
        public string DataTypeDisplayName => DataType switch
        {
            "TEXT" => "文字",
            "NUMBER" => "數字",
            "BOOLEAN" => "是否",
            "SELECT" => "選單",
            "PHOTO" => "拍照",
            _ => DataType
        };

        public string StatusDisplayName => IsActive ? "啟用" : "停用";
    }

    public class UserTodayInspection
    {
        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public List<TimePeriod> TimePeriods { get; set; } = new();
    }

    public class TimePeriod
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public List<string> Areas { get; set; } = new();
        public string EventType { get; set; } = string.Empty;
        public string EventDetail { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
        public bool IsPast { get; set; }
        public List<DeviceInspectionInfo> DevicesToInspect { get; set; } = new();
        public List<DeviceInspectionInfo> ExtraTask { get; set; } = new();
    }

    public class DeviceInspectionInfo
    {
        public DeviceStatus DeviceStatus { get; set; } = new();
        public List<InspectionRecord> InspectionList { get; set; } = new();
    }

    public class DeviceStatus
    {
        public string DeviceID { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartTime { get; set; }
        public DateTime? CreateTime { get; set; }
        public string WO_ID { get; set; } = string.Empty;
        public string BPM_NO { get; set; } = string.Empty;
    }

    public class InspectionRecord
    {
        public int? RecordId { get; set; }
        public string Time { get; set; } = string.Empty;
        public string Inspector { get; set; } = string.Empty;
        public string InspectorId { get; set; } = string.Empty;
        public string InspectWo { get; set; } = string.Empty;
        public string? InspectType { get; set; }

    }

    // 用於 SQL 查詢結果的 DTO
    public class DeviceInspectionRawData
    {
        public string DeviceID { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartTime { get; set; }
        public DateTime? CreateTime { get; set; }
        public string WO_ID { get; set; } = string.Empty;
        public string BPM_NO { get; set; } = string.Empty;

        public int? RecordId { get; set; }
        public string? CardId { get; set; }
        public string? DeviceId { get; set; }
        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? InspectType { get; set; }
        public string? InspectWo { get; set; }
        public DateTime? ArriveAt { get; set; }
        public DateTime? SubmitDataAt { get; set; }
        public string? Source { get; set; }
        public DateTime? RecordCreateDate { get; set; }

        // Device Name (from mapping)
        public string DeviceName { get; set; } = string.Empty;
    }

    public class InspectionItemQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? Department { get; set; }
        public string? InspectArea { get; set; }
        public bool? IsActive { get; set; }
        public string? SearchText { get; set; }
    }

    public class InspectionItemCreateDto
    {
        [Required(ErrorMessage = "檢驗項目名稱為必填")]
        [StringLength(100, ErrorMessage = "檢驗項目名稱不能超過100字")]
        public string InspectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "部門為必填")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "檢驗區域為必填")]
        public string InspectArea { get; set; } = string.Empty;

        public string? Station { get; set; }

        [Required(ErrorMessage = "資料類型為必填")]
        public string DataType { get; set; } = "TEXT";

        public string? SelectOptions { get; set; }

        public bool IsRequired { get; set; } = false;

        // 驗證選項內容
        public bool IsValid()
        {
            if (DataType == "SELECT" && string.IsNullOrWhiteSpace(SelectOptions))
                return false;

            return true;
        }
    }

    public class InspectionItemUpdateDto : InspectionItemCreateDto
    {
        public int ItemId { get; set; }

        [StringLength(50, ErrorMessage = "更新原因不能超過50字")]
        public string? UpdateReason { get; set; }
    }

    public class InspectionItemStatusDto
    {
        public int ItemId { get; set; }
        public bool IsActive { get; set; }

        [StringLength(50, ErrorMessage = "更新原因不能超過50字")]
        public string? UpdateReason { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public class UpdateQuantityRequest
    {
        public int RecordId { get; set; }
        public int OkQuantity { get; set; }
        public int NgQuantity { get; set; }
        public string RemarkQuantity { get; set; }
    }



    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public string? ErrorCode { get; set; }

        public static ApiResponse<T> SuccessResult(T data, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResult(string message, string? errorCode = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode
            };
        }
    }

    public class SubmitWarehouseRequest
    {
        public int RecordId { get; set; }
        public List<WarehouseOrderInfo> Orders { get; set; } = new();
        public string Remark { get; set; } = string.Empty; 
    }

    public class WarehouseOrderInfo
    {
        public string WorkOrder { get; set; } = string.Empty;
        public int OkQuantity { get; set; }
        public int NgQuantity { get; set; }
        public string Remark { get; set; } = string.Empty;
    }

    public class WarehouseInspectionLog
    {
        public int LogId { get; set; }
        public int OriginalRecordId { get; set; }
        public string UserNo { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public DateTime ProcessTime { get; set; }
        public string OrderDetails { get; set; } = string.Empty;
    }

    public class InspectionValidWorkType
    {
        public int Id { get; set; }
        public string InspectType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }
}