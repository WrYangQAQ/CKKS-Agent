using CKKS_Agent_System.Enum;

namespace CKKS_Agent_System.DTO
{
    public class SendCodeRequest
    {
        public VerifyType Type { get; set; }  // 验证码类型（邮箱或手机号）

        public string Target { get; set; } = string.Empty;  // 验证码发送目标（邮箱地址或手机号）
    }
}
