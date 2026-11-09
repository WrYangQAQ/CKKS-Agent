using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using CKKS_Agent.Wpf.Models;

namespace CKKS_Agent.Wpf.Services;

public sealed class UserApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;

    public UserApiClient()
    {
        // 后端 Development profile 的 HTTPS 地址；本机开发证书异常时仅信任 localhost。
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request.RequestUri?.Host is "localhost" or "127.0.0.1"
        };
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:7206/api/user/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public Task<ApiResponse<LoginResult>> LoginAsync(LoginRequest request)
        => PostAsync<LoginRequest, LoginResult>("login", request);

    public Task<ApiResponse<object>> RegisterAsync(RegisterRequest request)
        => PostAsync<RegisterRequest, object>("register", request);

    public Task<ApiResponse<object>> SendCodeAsync(SendCodeRequest request)
        => PostAsync<SendCodeRequest, object>("send-code", request);

    public Task<ApiResponse<object>> LogoutAsync(LogoutRequest request)
        => PostAsync<LogoutRequest, object>("logout", request);

    private async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest request)
    {
        try
        {
            using var response = await _client.PostAsJsonAsync(path, request, JsonOptions);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<TResponse>>(JsonOptions);
            if (envelope is not null)
            {
                return envelope;
            }

            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = $"服务器返回了无法识别的响应（{(int)response.StatusCode}）。"
            };
        }
        catch (HttpRequestException)
        {
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = "无法连接到后端服务，请确认 API 已启动。"
            };
        }
        catch (TaskCanceledException)
        {
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = "请求超时，请稍后重试。"
            };
        }
    }
}
