using System.ComponentModel.DataAnnotations;
using CKKS_Agent_System.Enum;

namespace CKKS_Agent_System.DTO
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "密码不能为空")]
        [MinLength(6, ErrorMessage = "密码至少6位")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public VerifyType Type { get; set; }

        [Required(ErrorMessage = "验证码不能为空")]
        public string VerificationCode { get; set; } = string.Empty;
    }
}
