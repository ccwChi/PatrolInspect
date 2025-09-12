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
        public int InspectItemId { get; set; }
        [StringLength(100, ErrorMessage = "檢驗項目名稱長度不能超過100字")]
        public string InspectName { get; set; } = string.Empty;
        [StringLength(50, ErrorMessage = "部門名稱長度不能超過50字")]
        public string Department { get; set; } = string.Empty;
        [StringLength(50, ErrorMessage = "檢驗區域長度不能超過50字")]
        public string InspectArea { get; set; } = string.Empty;
        [StringLength(50, ErrorMessage = "站點名稱長度不能超過50字")]
        public string? Station { get; set; }
        [StringLength(20)]
        public string DataType { get; set; } = "TEXT";
        [StringLength(500, ErrorMessage = "選項內容長度不能超過500字")]
        public string? SelectOptions { get; set; }
        public bool IsRequired { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreateDate { get; set; } = DateTime.Now;
        [StringLength(50)]
        public string CreateBy { get; set; } = string.Empty;
        public DateTime? UpdateDate { get; set; }
        [StringLength(50)]
        public string? UpdateBy { get; set; }

        [StringLength(200, ErrorMessage = "異動原因長度不能超過200字")]
        public string? UpdateReason { get; set; }
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
        public string Time { get; set; } = string.Empty;
        public string Inspector { get; set; } = string.Empty;
        public string InspectWo { get; set; } = string.Empty;

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
        public string? UserNo { get; set; }
        public string? UserName { get; set; }
        public string? InspectType { get; set; }
        public string? InspectWo { get; set; }
        public DateTime? ArriveAt { get; set; }
        public DateTime? SubmitDataAt { get; set; }
        public string? Source { get; set; }
        public DateTime? RecordCreateDate { get; set; }

        // Device Name (from mapping)
        public string DeviceName { get; set; } = string.Empty;
    }


}