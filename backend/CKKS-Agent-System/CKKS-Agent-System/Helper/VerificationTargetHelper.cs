using CKKS_Agent_System.Enum;
using System.Text;

namespace CKKS_Agent_System.Helper
{
    public static class VerificationTargetHelper
    {
        // 规范化邮箱：去除首尾空格，并统一转换为小写。
        public static string? NormalizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return email.Trim().ToLowerInvariant();
        }


        // 规范化中国大陆手机号，统一返回 +86 加 11 位手机号的格式。
        // 支持 13812345678、+86 13812345678、0086-13812345678 等常见输入。
        public static string? NormalizePhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return null;
            }

            // 只允许数字、首位加号及常见分隔符，避免把无关字符静默忽略。
            var builder = new StringBuilder();
            bool hasLeadingPlus = false;

            foreach (char character in phoneNumber.Trim())
            {
                if (char.IsDigit(character))
                {
                    builder.Append(character);
                }
                else if (character == '+' && builder.Length == 0 && !hasLeadingPlus)
                {
                    hasLeadingPlus = true;
                }
                else if (char.IsWhiteSpace(character) || character == '-' || character == '(' || character == ')')
                {
                    // 忽略常见的展示分隔符。
                    continue;
                }
                else
                {
                    return null;
                }
            }

            string digits = builder.ToString();

            // 兼容国际区号的三种常见写法：+86、86、0086。
            if (digits.StartsWith("0086"))
            {
                digits = digits[4..];
            }
            else if (digits.Length == 13 && digits.StartsWith("86"))
            {
                digits = digits[2..];
            }

            // 中国大陆手机号为 11 位，且号段第二位为 3 到 9。
            if (digits.Length != 11 || digits[0] != '1' || digits[1] < '3' || digits[1] > '9')
            {
                return null;
            }

            return $"+86{digits}";
        }

        // 根据验证码类型，统一规范化发送目标。类型不支持或目标不合法时返回 null。
        public static string? Normalize(VerifyType type, string? target)
        {
            return type switch
            {
                VerifyType.Email => NormalizeEmail(target),
                VerifyType.PhoneNumber => NormalizePhoneNumber(target),
                _ => null
            };
        }
    }
}
