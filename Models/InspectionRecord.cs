using System.ComponentModel.DataAnnotations;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Models
{
    /// <summary>
    /// 巡檢設備擴展資訊 (包含狀態和巡檢記錄)
    /// </summary>
    public class InspectionDeviceInfo
    {
        public InspectionDeviceAreaMapping Device { get; set; } = new();
        public FnDeviceStatus? Status { get; set; }
        public bool RequiresInspection { get; set; }
        public DateTime? LastInspectionStartTime { get; set; }
        public DateTime? LastInspectionEndTime { get; set; }
        public string? LastInspectorName { get; set; }
        public bool HasAlarm => !string.IsNullOrEmpty(Status?.AlarmMessage);
    }

    /// <summary>
    /// 使用者今日巡檢總覽
    /// </summary>
    public class UserTodayInspection
    {
        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public List<TimePeriod> TimePeriods { get; set; } = new List<TimePeriod>(); // 新增
        public int TotalDevices { get; set; }
        public int RunningDevices { get; set; }
        public int WithWoDevices { get; set; }
        public int CompletedInspections { get; set; }

        // 為了向後相容，保留這些屬性
        public List<string> AssignedAreas { get; set; } = new List<string>();
        public List<InspectionDeviceInfo> DevicesToInspect { get; set; } = new List<InspectionDeviceInfo>();
    }

    /// <summary>
    /// 巡檢記錄請求DTO
    /// </summary>
    public class InspectionRecordRequest
    {
        public string CardId { get; set; } = string.Empty;
        public string? DeviceId { get; set; }

        public string InspectType { get; set; } = string.Empty;
        public string? Source { get; set; } = "NFC";
    }

    /// <summary>
    /// 排班事件DTO (用於前端編輯)
    /// </summary>
    public class ScheduleEventDto
    {

        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string EventTypeName { get; set; } = string.Empty;
        public string EventDetail { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Area { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? CreateBy { get; set; }
    }

    public class ConfirmReplaceRequest
    {
        public string CardId { get; set; } = string.Empty;
        public string NewDeviceId { get; set; } = string.Empty;
        public int OldRecordId { get; set; }
        public string? InspectType { get; set; }
        public string? Source { get; set; }
    }

    public class TimePeriod
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public List<string> Areas { get; set; } = new List<string>();
        public string? EventType { get; set; }
        public string? EventTypeName { get; set; }
        public string? EventDetail { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsPast { get; set; }
        public List<InspectionDeviceInfo> DevicesToInspect { get; set; } = new List<InspectionDeviceInfo>();
        public Dictionary<string, List<InspectionQcRecord>> DeviceRecordsMap { get; set; }
            = new Dictionary<string, List<InspectionQcRecord>>(StringComparer.OrdinalIgnoreCase);
    }


    public class InspectionItemQueryDto
    {
        public string? Department { get; set; }
        public string? InspectArea { get; set; }
        public bool? IsActive { get; set; }
        public string? SearchText { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    // 新增/編輯用的 DTO
    public class InspectionItemDto
    {
        public int InspectItemId { get; set; }

        public string InspectName { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string InspectArea { get; set; } = string.Empty;

        public string? Station { get; set; }

        public string DataType { get; set; } = "TEXT";

        public string? SelectOptions { get; set; }

        public bool IsRequired { get; set; } = false;

        public string? UpdateReason { get; set; }
    }

    // 分頁結果
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    // API 回應格式
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}