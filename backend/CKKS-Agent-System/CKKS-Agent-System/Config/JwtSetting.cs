namespace CKKS_Agent_System.Config
{
    public class JwtSetting
    {
        public const string SectionName = "JwtSetting";
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int AccessTokenExpireMinutes { get; set; } = 15; // 默认 15 分钟
        public int RefreshTokenExpireDays { get; set; } = 7; // 默认 7 天
    }
}
