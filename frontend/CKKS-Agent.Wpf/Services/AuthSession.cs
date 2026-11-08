namespace CKKS_Agent.Wpf.Services;

/// <summary>仅在进程内保存短期 Access Token；不要将它落盘或写入配置文件。</summary>
public static class AuthSession
{
    public static string? AccessToken { get; private set; }

    public static void SetAccessToken(string accessToken) => AccessToken = accessToken;

    public static void Clear() => AccessToken = null;
}
