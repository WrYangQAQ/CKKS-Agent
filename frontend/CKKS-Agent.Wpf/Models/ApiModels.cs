namespace CKKS_Agent.Wpf.Models;

public sealed class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
    public int Code { get; set; }
}

public sealed class LoginResult
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiresIn { get; set; }
    public UserInfo UserInfo { get; set; } = new();
}

public sealed class UserInfo
{
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
}

public sealed class LoginRequest
{
    public string LoginType { get; set; } = "Username";
    public string Identifier { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Type { get; set; } = "Email";
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string VerificationCode { get; set; } = "";
}

public sealed class SendCodeRequest
{
    public string Type { get; set; } = "Email";
    public string Target { get; set; } = "";
}

public sealed class LogoutRequest
{
    public string RefreshToken { get; set; } = "";
}
