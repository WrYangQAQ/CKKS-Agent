using System.Text.Json.Serialization;

namespace CKKS_Agent_System.DTO
{
    public class RotateAccessTokenDto
    {
        // 新的短期通行证
        public string AccessToken { get; set; } = string.Empty;

        // 过期时间（秒）
        public int ExpiresIn { get; set; }

        // 新生成的 RefreshToken 明文
        public string RefreshToken { get; set; } = string.Empty;
    }
}
