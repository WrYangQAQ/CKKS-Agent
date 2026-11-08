namespace CKKS_Agent_System.Config
{
    public class EmailSetting
    {
        public const string SectionName = "EmailSetting";

        public string Host { get; set; } = string.Empty;      // SMTP 服务器地址
        public int Port { get; set; }                         // 端口号
        public string UserName { get; set; } = string.Empty;  // 邮箱账号
        public string Password { get; set; } = string.Empty;  // 邮箱授权码
        public string FromEmail { get; set; } = string.Empty; // 发件人邮箱
        public string FromName { get; set; } = string.Empty;  // 发件人显示名称
        public bool UseSsl { get; set; }                      // 是否使用 SSL（465 端口用 true）
    }
}
