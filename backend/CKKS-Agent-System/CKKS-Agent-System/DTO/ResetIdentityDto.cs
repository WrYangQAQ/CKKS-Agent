using CKKS_Agent_System.Enum;
using System.ComponentModel.DataAnnotations;

namespace CKKS_Agent_System.DTO
{
    public class SendResetIndentiyAuthCodeRequest
    {
        public SendCodeAction Action { get; set; }
    }

    public class ResetIndentiyAuthRequest
    {
        public ResetMethod Method { get; set; }
        public string? Password { get; set; }
        public string? VerifyCode { get; set; }
        public VerifyType Channel { get; set; }  // 用户想要换绑的东西 邮箱/电话号
    }

    public class ResetIdentityRequest
    {
        public VerifyType Type { get; set; }
        public string VerifyCode { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
    }
}
