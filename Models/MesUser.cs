using System.ComponentModel.DataAnnotations;

namespace PatrolInspect.Models
{
    public class MesUser
    {
        public string UserNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string? FatherDepartmentName { get; set; }
        public string TitleName { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; }

        // 計算屬性
        public bool IsActive => ExpirationDate == null;
        //public string DisplayDepartment => string.IsNullOrEmpty(FatherDepartmentName)
        //    ? DepartmentName
        //    : $"{FatherDepartmentName} - {DepartmentName}";
    }
}