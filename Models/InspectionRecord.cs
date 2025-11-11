using System.ComponentModel.DataAnnotations;
using PatrolInspect.Models.Entities;

namespace PatrolInspect.Models
{

    //public class InspectionItemQueryDto
    //{
    //    public string? Department { get; set; }
    //    public string? InspectArea { get; set; }
    //    public bool? IsActive { get; set; }
    //    public string? SearchText { get; set; }
    //    public int Page { get; set; } = 1;
    //    public int PageSize { get; set; } = 50;
    //}

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

    public class ProcessNFCRequest
    {
        public string NfcId { get; set; } = string.Empty;
        public string Source { get; set; } = "NFC";
        public string InspectType { get; set; } = string.Empty;
        public bool ConfirmReplace { get; set; } = false;
        public int? OldRecordId { get; set; }
        public string WorkOrderNo { get; set; } = string.Empty;
    }

    public class InspectionItemRecord
    {
        public int RecordId { get; set; }
        public string TimeSlot { get; set; } = string.Empty; // 時段
        public string Category { get; set; } = string.Empty; // 類別
        public string Station { get; set; } = string.Empty; // 站點
        public string WorkOrder { get; set; } = string.Empty; // 工單
        public string PartNumber { get; set; } = string.Empty; // 料號
        public string ProductName { get; set; } = string.Empty; // 品名
        public string AuditUnit { get; set; } = string.Empty; // 授稽單位
        public string Inspector { get; set; } = string.Empty; // 稽核人員
        public string SerialNumbers { get; set; } = string.Empty; // 檢查序號

        // 各項檢驗內容
        public string CleaningOperation { get; set; } = string.Empty;
        public string RfidBurning { get; set; } = string.Empty;
        public string Measurement { get; set; } = string.Empty;
        public string LdWindSpeedTest { get; set; } = string.Empty;
        public string QPackaging { get; set; } = string.Empty;
        public string Rsp150Assembly { get; set; } = string.Empty;
        public string AluTesting { get; set; } = string.Empty;
        public string Rsp200Assembly { get; set; } = string.Empty;
        public string VacuumPackaging { get; set; } = string.Empty;
        public string SixSAudit { get; set; } = string.Empty;
        public string AbnormalDescription { get; set; } = string.Empty;
        public string CarNumber { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;

        public DateTime InspectionTime { get; set; } = DateTime.Now;
        public bool IsCompleted { get; set; }
        public string Status { get; set; } = "進行中"; // 進行中, 已完成, 異常
    }

    public class InspectionActivity
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
        public string Source { get; set; } = string.Empty;

        // 計算屬性
        public double StartHour => ArriveAt.Hour + ArriveAt.Minute / 60.0;
        public double EndHour => SubmitDataAt.HasValue
            ? SubmitDataAt.Value.Hour + SubmitDataAt.Value.Minute / 60.0
            : StartHour + 0.5; // 如果沒有結束時間，預設顯示 30 分鐘
        public double Duration => EndHour - StartHour;
        public double LeftPosition => (StartHour / 24.0) * 100; // 百分比
        public double Width => (Duration / 24.0) * 100; // 百分比
    }

    public class UserActivityViewModel
    {
        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<InspectionActivity> Activities { get; set; } = new();
    }

    public class ActivityChartViewModel
    {
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public List<UserActivityViewModel> UserActivities { get; set; } = new();
        public String SelectedEQP { get; set; } = string.Empty;
        public List<EqpOOSActivityViewModel> EqpOOSActivities { get; set; } = new();
    }
    
    public class EqpOOSActivityViewModel
    {
        public string EqpNo { get; set; } = string.Empty;
        public string EqpName { get; set; } = string.Empty;
        public string InspectWo { get; set; } = string.Empty;
        public List<EqpOOSInspectionActivity> Activities { get; set; } = new();
    }
    public class EqpOOSInspectionActivity
    {
        public int RecordId { get; set; }
        public string CardId { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string EqpNo { get; set; } = string.Empty;
        public string EqpName { get; set; } = string.Empty;
        public string InspectType { get; set; } = string.Empty;
        public string? InspectWo { get; set; }
        public DateTime ArriveAt { get; set; }
        public DateTime? SubmitDataAt { get; set; }
        public DateTime? WoCheckInAt { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Fab { get; set; } = string.Empty;
        public string Param_Index { get; set; } = string.Empty;
        public string Param_Name4 { get; set; } = string.Empty;
        public string PartNo { get; set; } = string.Empty;
        public string CommentsDataAt { get; set; } = string.Empty;
        


        // 計算屬性
        public double StartHour => ArriveAt.Hour + ArriveAt.Minute / 60.0;
        //public double EndHour => SubmitDataAt.HasValue
        //    ? SubmitDataAt.Value.Hour + SubmitDataAt.Value.Minute / 60.0
        //    : StartHour + 0.5; // 如果沒有結束時間，預設顯示 30 分鐘
        public double EndHour => DateTime.Now.Hour + DateTime.Now.Minute / 60.0;
        public double Duration => EndHour - StartHour;
        public double LeftPosition => (StartHour / 24.0) * 100; // 百分比
        public double Width => (Duration / 24.0) * 100; // 百分比
    }
}