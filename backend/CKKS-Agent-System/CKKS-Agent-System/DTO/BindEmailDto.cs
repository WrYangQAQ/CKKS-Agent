using System.ComponentModel.DataAnnotations;

namespace CKKS_Agent_System.DTO
{
    public class BindEmailRequest
    {
        [Required(ErrorMessage = "请填写邮箱！")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string VerificationCode { get; set; } = string.Empty;
    }
}
