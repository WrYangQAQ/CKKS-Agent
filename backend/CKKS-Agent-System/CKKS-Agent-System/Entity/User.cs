using CKKS_Agent_System.Helper;

namespace CKKS_Agent_System.Entity
{
    public class User
    {
        public long UserId { get; set; } = IDGeneration.NextId();  // 初始化时生成一个唯一的用户ID（雪花ID算法生成）

        public string Username { get; set; } = string.Empty;  // 用户名

        public string? Email { get; set; }  // 用户邮箱

        public string? PhoneNumber { get; set; }  // 用户手机号

        public string PasswordHash { get; set; } = string.Empty;  // 密码哈希值

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // 用户创建时间，使用UTC时间

        public string AvatarUrl { get; set; } = "/Avatars/c21dec73-4a74-4f2c-bccf-00b42a2418e2.png";  // 用户头像URL

        public bool IsEmailVerified { get; set; } = false;  // 邮箱是否已验证

        public bool IsPhoneNumberVerified { get; set; } = false;  // 手机号是否已验证

    }
}
