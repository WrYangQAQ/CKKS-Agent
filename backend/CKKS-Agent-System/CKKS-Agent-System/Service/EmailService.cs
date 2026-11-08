using CKKS_Agent_System.Config;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CKKS_Agent_System.Service
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task SendTemplateEmailAsync(string toEmail, string subject, string templateName, Dictionary<string, string> variables);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSetting _settings;
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _env;

        public EmailService(IOptions<EmailSetting> options, ILogger<EmailService> logger, IWebHostEnvironment env)
        {
            _settings = options.Value;
            _logger = logger;
            _env = env;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress("", toEmail)); // 收件人昵称可以为空
            message.Subject = subject;

            // 使用 BodyBuilder 支持 HTML 正文
            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // 465 端口用 SslOnConnect，587 端口用 StartTls[reference:8]
                await client.ConnectAsync(
                    _settings.Host,
                    _settings.Port,
                    _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
                );

                await client.AuthenticateAsync(_settings.UserName, _settings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送邮件到 {ToEmail} 失败", toEmail);
                throw; // 让上层知道发送失败了
            }
        }

        public async Task SendTemplateEmailAsync(string toEmail, string subject, string templateName, Dictionary<string, string> variables)
        {
            // 拼出模板的物理路径
            var templatePath = Path.Combine(_env.WebRootPath, "Templates", templateName);

            if (!File.Exists(templatePath))
            {
                _logger.LogError($"邮件模板文件不存在: {templatePath}");
                throw new FileNotFoundException("邮件模板文件不存在", templatePath);
            }

            // 读取 HTML 内容
            var htmlBody = await File.ReadAllTextAsync(templatePath);

            // 循环替换所有的占位符
            foreach (var variable in variables)
            {
                htmlBody = htmlBody.Replace($"{{{{{variable.Key}}}}}", variable.Value); // 把 {{CODE}} 替换成实际的验证码
            }

            await SendEmailAsync(toEmail, subject, htmlBody);
        }
    }
}