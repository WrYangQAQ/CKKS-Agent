using AlibabaCloud.SDK.Dypnsapi20170525;
using AlibabaCloud.SDK.Dypnsapi20170525.Models;
using Azure;
using CKKS_Agent_System.Config;
using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Enum;
using CKKS_Agent_System.Helper;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CKKS_Agent_System.Service
{
    public interface ISmsService
    {
        Task<bool> SendVerificationCodeAsync(string phoneNumber, string code);
    }

    public class AliyunSmsService : ISmsService
    {
        private readonly AliyunSmsSetting _settings;
        private readonly VerificationSetting _verificationSetting;
        private readonly ILogger<AliyunSmsService> _logger;

        public AliyunSmsService(IOptions<AliyunSmsSetting> options, IOptions<VerificationSetting> verificationOptions, ILogger<AliyunSmsService> logger)
        {
            _settings = options.Value;
            _verificationSetting = verificationOptions.Value;
            _logger = logger;
        }

        public async Task<bool> SendVerificationCodeAsync(string phoneNumber, string code)
        {
            try
            {
                // 初始化 SDK 客户端配置
                var config = new AlibabaCloud.OpenApiClient.Models.Config
                {
                    AccessKeyId = _settings.AccessKeyId,
                    AccessKeySecret = _settings.AccessKeySecret,
                    Endpoint = _settings.Endpoint
                };

                // 创建客户端
                var client = new Client(config);

                // 构造模板参数 JSON（和阿里云模板里的占位符对应）
                var templateParam = new
                {
                    code = code,
                    min = _verificationSetting.CodeExpirationMinutes.ToString()
                };
                string jsonParam = JsonSerializer.Serialize(templateParam);

                // 构建请求对象
                var request = new SendSmsVerifyCodeRequest
                {
                    PhoneNumber = phoneNumber,
                    SignName = _settings.SignName,
                    TemplateCode = _settings.TemplateCode,
                    TemplateParam = jsonParam
                };

                // 发起请求
                var response = await client.SendSmsVerifyCodeAsync(request);

                // 检查响应结果
                if (response.Body.Code == "OK")
                {
                    _logger.LogInformation("短信发送成功，手机号: {PhoneNumber}", phoneNumber);
                    return true;
                }
                else
                {
                    _logger.LogWarning("短信发送失败，手机号: {PhoneNumber}, Code: {Code}, Message: {Message}",
                        phoneNumber, response.Body.Code, response.Body.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                // 如果是网络或鉴权异常，记录日志
                _logger.LogError(ex, "调用阿里云短信接口发生异常，手机号: {PhoneNumber}", phoneNumber);
                return false;
            }
        }
    }
}
