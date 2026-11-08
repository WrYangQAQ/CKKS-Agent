using CKKS_Agent_System.Helper;

namespace CKKS_Agent_System.Entity
{
    public class RefreshToken
    {

        // 主键，使用雪花ID生成算法
        public long Id { get; set; } = IDGeneration.NextId();

        // RefreshToken 的字符串本体哈希值（随机生成的不可猜字符串，不是 JWT）
        public string TokenHash { get; set; } = string.Empty;

        // 关联的用户ID
        public long UserId { get; set; }

        // 过期时间（UTC）
        public DateTime ExpiresAt { get; set; }

        // 创建时间（UTC）
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 撤销时间：不为 null 表示该 token 已被作废（登出、改密、轮换时写入）
        public DateTime? RevokedAt { get; set; }

        // 轮换后新的 token 的 ID
        public long? ReplacedByTokenId { get; set; }
    }
}