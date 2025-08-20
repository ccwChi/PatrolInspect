using System.ComponentModel.DataAnnotations;

namespace PatrolInspect.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "請輸入工號")]
        [StringLength(20, ErrorMessage = "工號長度不能超過20位")]
        public string UserNo { get; set; } = string.Empty;
    }
}