using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;

namespace CKKS_Agent_System.Helper
{
    public static class VerifyCodeHelper
    {
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public const int CodeExpirationMinutes = 5; // 验证码有效期，单位：分钟

        // 生成 6 位随机验证码
        public static string GenerateCode(int length = 6)
        {
            // 使用 string.Create 避免多次分配内存
            return string.Create(length, (Chars, length), (span, state) =>
            {
                for (int i = 0; i < span.Length; i++)
                {
                    // 使用 RandomNumberGenerator 获取密码学安全的随机索引
                    span[i] = state.Chars[RandomNumberGenerator.GetInt32(state.Chars.Length)];
                }
            });
        }

        // 保存验证码到 Redis（设置 5 分钟过期）
        public static async Task SaveCodeAsync(IDistributedCache cache, string target, string code, string action)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CodeExpirationMinutes) // 5分钟过期
            };
            // 存进 Redis，Key 是 "VerifyCode:Register:邮箱或手机号"
            await cache.SetStringAsync($"VerifyCode:{action}:{target}", code, options);
        }

        // 校验验证码
        public static async Task<bool> ValidateCodeAsync(IDistributedCache cache, string target, string inputCode, string action)
        {
            // 从 Redis 里拿
            var savedCode = await cache.GetStringAsync($"VerifyCode:{action}:{target}");

            // 没拿到，说明过期了，或者根本没发过
            if (string.IsNullOrEmpty(savedCode))
                return false;

            // 比对错误
            if (savedCode != inputCode)
                return false;

            // 验证成功后在 Redis 里进行删除，防止同一个验证码被重复利用（防重放攻击）
            await cache.RemoveAsync($"VerifyCode:{action}:{target}");

            return true;
        }
    }
}