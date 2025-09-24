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

    // 分頁結果


    // API 回應格式
    //public class ApiResponse<T>
    //{
    //    public bool Success { get; set; }
    //    public string Message { get; set; } = string.Empty;
    //    public T? Data { get; set; }
    //}

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
}