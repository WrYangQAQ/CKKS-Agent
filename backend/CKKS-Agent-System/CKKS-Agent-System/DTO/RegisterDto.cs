using CKKS_Agent_System.Enum;
using System.ComponentModel.DataAnnotations;

namespace CKKS_Agent_System.DTO
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "用户名不能为空")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码不能为空")]
        [MinLength(6, ErrorMessage = "密码至少6位")]
        public string Password { get; set; } = string.Empty;

        public VerifyType Type { get; set; }  // 注册验证类型（邮箱或手机号）

        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "手机号格式不正确")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "验证码不能为空")]
        public string VerificationCode { get; set; } = string.Empty;
    }

    public class RegisterResponse
    {

    }
}
