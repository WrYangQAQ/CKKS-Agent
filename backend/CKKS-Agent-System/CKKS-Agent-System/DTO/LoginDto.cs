using CKKS_Agent_System.Enum;
using CKKS_Agent_System.Entity;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CKKS_Agent_System.DTO
{
    public class LoginRequest
    {
        public LoginType LoginType { get; set; }                 // 登录凭证类型
        public string Identifier { get; set; } = string.Empty;   // 登录凭证（用户名 / 邮箱 / 手机号）

        [Required(ErrorMessage = "密码不能为空")]
        [MinLength(6, ErrorMessage = "密码长度不能少于6位")]
        public string Password { get; set; } = string.Empty;     // 用户输入的密码
    }

    public class LoginResponse
    {
        // 短期通行证
        public string AccessToken { get; set; } = string.Empty;

        // 过期时间（秒），方便前端做倒计时或定时刷新
        public int ExpiresIn { get; set; }

        // 刷新令牌明文
        public string RefreshToken { get; set; } = string.Empty;

        // 安全的用户信息
        public UserInfo UserInfo { get; set; }

        public LoginResponse(User user, string accessToken, int expiresIn)
        {
            UserInfo = new UserInfo(user);
            AccessToken = accessToken;
            ExpiresIn = expiresIn;
        }

    }
}
