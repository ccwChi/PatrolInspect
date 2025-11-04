namespace PatrolInspect.Models
{
    public class ActivityChartViewModel
    {
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public List<UserActivityViewModel> UserActivities { get; set; } = new();
    }

    public class MachineInspectionData
    {
        public string deviceId { get; set; }
        public string deviceName { get; set; }
        public string area { get; set; }
        public string scheduleRange { get; set; }
        public decimal runTime { get; set; }
        public string workOrder { get; set; }
        public string inspectUserName { get; set; }
        public List<InspectionDetail> inspections { get; set; }
        public string status { get; set; }
    }

    public class InspectionDetail
    {
        public string inspectType { get; set; }
        public DateTime? inspectStartTime { get; set; }
        public DateTime? inspectEndTime { get; set; }
        public string inspectUserName { get; set; }
        public string responseUserNos { get; set; }
        public string responseUserNames { get; set; }
        public string workOrderNo { get; set; }
        public string status { get; set; }
        public string runTime { get; set; }
        public string nonOffTime { get; set; }
        public string prodNo { get; set; }
        public string prodDesc { get; set; }
    }

    public class MachineInspectionViewModel
    {
        public DateTime SelectedDate { get; set; }
        public List<MachineTimelineViewModel> MachineTimelines { get; set; } = new();
    }

    public class MachineTimelineViewModel
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public List<TimeSlot> TimeSlots { get; set; } = new();
    }

    public class TimeSlot
    {
        public string TimeRange { get; set; } = string.Empty;
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal RunTime { get; set; }
        public List<InspectionDetail> Inspections { get; set; } = new();
        public double LeftPosition { get; set; }
        public double Width { get; set; }
    }

    public class InspectionUtilizationData
    {
        public string Area { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string ScheduleRange { get; set; }
        public DateTime ScheduleStart { get; set; }
        public DateTime ScheduleEnd { get; set; }
        public string DeviceStatusWo { get; set; }
        public decimal RunTime { get; set; }
        public decimal NonOffTime { get; set; }
        public decimal IdleTime { get; set; }
        public string WorkOrderNo { get; set; }
        public string InspectType { get; set; }
        public DateTime? InspectStartTime { get; set; }
        public DateTime? InspectEndTime { get; set; }
        public string InspectUserNo { get; set; }
        public string InspectUserName { get; set; }
        public string ResponseUserNos { get; set; }
        public string ResponseUserNames { get; set; }
        public string Status { get; set; }
        public string ProdNo { get; set; }
        public string ProdDesc { get; set; }
    }
}
