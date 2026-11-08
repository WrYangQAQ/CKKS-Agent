using CKKS_Agent_System.Entity;

namespace CKKS_Agent_System.DTO
{
    public class RotateRefreshTokenDto
    {
        // 轮换后的新 RefreshToken 对象
        public RefreshToken? NewToken { get; set; }

        // 轮换后新生成的 RefreshToken 的明文字符串（用于返回给客户端）
        public string? TokenPlaintext { get; set; }
    }
}
