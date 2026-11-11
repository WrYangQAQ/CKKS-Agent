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

        // 验证密码是否合法：长度 6~20，且同时包含大写字母、小写字母和数字
        public static bool IsPasswordValid(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            if (input.Length < 6 || input.Length > 20)
                return false;

            bool hasLower = false, hasUpper = false, hasDigit = false;

            foreach (char c in input)
            {
                if (c >= 'a' && c <= 'z')
                    hasLower = true;
                else if (c >= 'A' && c <= 'Z')
                    hasUpper = true;
                else if (c >= '0' && c <= '9')   // 用显式范围而非 char.IsDigit，避免匹配全角/其他 Unicode 数字
                    hasDigit = true;
                else
                    return false;                 // 含其他字符则视为不合法
            }

            return hasLower && hasUpper && hasDigit;
        }
    }
}