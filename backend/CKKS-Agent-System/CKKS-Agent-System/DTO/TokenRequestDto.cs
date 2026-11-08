using System.ComponentModel.DataAnnotations;

namespace CKKS_Agent_System.DTO
{
    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "RefreshToken 不能为空")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        [Required(ErrorMessage = "RefreshToken 不能为空")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
