using System.ComponentModel.DataAnnotations;

namespace PatrolInspect.Models
{
    public class InspectionRecord
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

    public class InspectionDashboardViewModel
    {
        public List<InspectionRecord> RecentRecords { get; set; } = new();
        public Dictionary<string, int> StatusSummary { get; set; } = new();
        public Dictionary<string, int> CategorySummary { get; set; } = new();
        public int TotalRecords { get; set; }
        public int TodayRecords { get; set; }
        public string CurrentUser { get; set; } = string.Empty;
    }

    public class InspectionFilterViewModel
    {
        public string TimeSlot { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Station { get; set; } = string.Empty;
        public string Inspector { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
