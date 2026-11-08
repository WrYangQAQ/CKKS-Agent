using System.Security.Cryptography;
using System.Text;

namespace CKKS_Agent_System.Helper
{
    public static class HashHelper
    {
        public static string ComputeSHA256Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // 1. 直接使用静态方法计算哈希（无需手动 Create 和 Dispose）
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

            // 2. 转为大写十六进制字符串
            // （Convert.ToHexString 是 .NET 5 引入的，性能极高，不带连字符）
            string hexString = Convert.ToHexString(hashBytes);
            return hexString;
        }
    }
}
