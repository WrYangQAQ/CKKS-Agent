using Microsoft.AspNetCore.Identity;

namespace CKKS_Agent_System.Helper
{
    public static class PasswordHelper
    {
        private static readonly PasswordHasher<object> _hasher = new PasswordHasher<object>();


        // 将明文密码加密为带随机盐的哈希串
        public static string HashPassword(string plainPassword)
        {
            return _hasher.HashPassword(null!, plainPassword);
        }


        // 验证密码是否匹配
        public static bool VerifyPassword(string inputPassword, string storedPasswordHash)
        {
            if (string.IsNullOrEmpty(inputPassword) || string.IsNullOrEmpty(storedPasswordHash))
            {
                return false;
            }

            var result = _hasher.VerifyHashedPassword(null!, storedPasswordHash, inputPassword);

            // 只要不是 Failed 就算通过
            return result != PasswordVerificationResult.Failed;
        }
    }
}