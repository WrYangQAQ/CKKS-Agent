namespace CKKS_Agent_System.Helper
{
    public class MaskHelper
    {
        /// <summary>
        /// 邮箱脱敏：a***@example.com
        /// 规则：保留本地部分首字符，其余用 *** 替代；域名原样保留。
        /// </summary>
        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;

            var atIndex = email.LastIndexOf('@');
            // 没有 @ 或 @ 在首尾，无法解析，整体打码
            if (atIndex <= 0 || atIndex == email.Length - 1)
                return "***";

            var local = email.Substring(0, atIndex);
            var domain = email.Substring(atIndex); // 含 @

            // 本地部分只有一个字符，就保留它 + ***，避免暴露长度
            var maskedLocal = local.Length <= 1
                ? local + "***"
                : local[0] + "***";

            return maskedLocal + domain;
        }

        /// <summary>
        /// 手机号脱敏：138****8000
        /// 规则：保留前 3 位和后 4 位，中间用 **** 替代。
        /// 对长度不足 7 的号码整体打码。
        /// </summary>
        public static string MaskPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            var trimmed = phone.Trim();

            // 太短无法安全脱敏，整体打码
            if (trimmed.Length < 7) return "***";

            var head = trimmed.Substring(0, 3);
            var tail = trimmed.Substring(trimmed.Length - 4);
            return $"{head}****{tail}";
        }
    }
}
