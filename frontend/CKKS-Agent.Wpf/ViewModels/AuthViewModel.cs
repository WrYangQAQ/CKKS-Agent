using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using CKKS_Agent.Wpf.Models;
using CKKS_Agent.Wpf.Services;

namespace CKKS_Agent.Wpf.ViewModels;

public sealed class AuthViewModel : INotifyPropertyChanged
{
    private readonly UserApiClient _api = new();

    private bool _isLoginMode = true;
    private bool _isBusy;
    private string _identifier = "";
    private string _loginPassword = "";
    private string _registerUsername = "";
    private string _registerPassword = "";
    private string _verificationTarget = "";
    private string _verificationCode = "";
    private string _statusMessage = "";
    private string _loginType = "Username";
    private string _verifyType = "Email";
    private string _sendCodeText = "发送验证码";
    private Brush _statusBrush = Brushes.Firebrick;
    private UserInfo _currentUser = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLoginMode
    {
        get => _isLoginMode;
        private set
        {
            _isLoginMode = value;
            Raise();
            Raise(nameof(IsRegisterMode));
            Raise(nameof(AuthTitle));
            Raise(nameof(ModeSwitchText));
        }
    }

    public bool IsRegisterMode => !IsLoginMode;
    public bool IsAuthenticated { get; private set; }
    public bool IsAnonymous => !IsAuthenticated;
    public string AuthTitle => IsLoginMode ? "账号登录" : "创建账号";
    public string ModeSwitchText => IsLoginMode ? "还没有账户？立即注册" : "已有账户？返回登录";

    /// <summary>登录账号输入框的标签，跟随登录方式变化。</summary>
    public string IdentifierLabel => LoginType switch
    {
        "PhoneNumber" => "手机号码",
        "Email" => "邮箱地址",
        _ => "用户名"
    };

    /// <summary>注册时验证目标输入框的标签，跟随验证方式变化。</summary>
    public string VerificationTargetLabel => VerifyType == "PhoneNumber" ? "手机号码" : "邮箱地址";

    public string Identifier { get => _identifier; set { _identifier = value; Raise(); } }
    public string LoginPassword { get => _loginPassword; set { _loginPassword = value; Raise(); } }
    public string RegisterUsername { get => _registerUsername; set { _registerUsername = value; Raise(); } }
    public string RegisterPassword { get => _registerPassword; set { _registerPassword = value; Raise(); } }
    public string VerificationTarget { get => _verificationTarget; set { _verificationTarget = value; Raise(); } }
    public string VerificationCode { get => _verificationCode; set { _verificationCode = value; Raise(); } }
    public string LoginType { get => _loginType; set { _loginType = value; Raise(); Raise(nameof(IdentifierLabel)); } }
    public string VerifyType { get => _verifyType; set { _verifyType = value; Raise(); Raise(nameof(VerificationTargetLabel)); } }

    public UserInfo CurrentUser
    {
        get => _currentUser;
        private set
        {
            _currentUser = value;
            Raise();
            Raise(nameof(UserInitial));
        }
    }

    public string UserInitial => string.IsNullOrWhiteSpace(CurrentUser.Username)
        ? "U"
        : CurrentUser.Username[..1].ToUpperInvariant();

    public string SendCodeText { get => _sendCodeText; private set { _sendCodeText = value; Raise(); } }
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; Raise(); } }
    public Brush StatusBrush { get => _statusBrush; private set { _statusBrush = value; Raise(); } }

    public bool CanSubmit => !_isBusy;

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand SendCodeCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand SwitchModeCommand { get; }

    public AuthViewModel()
    {
        LoginCommand = new AsyncCommand(LoginAsync, () => CanSubmit);
        RegisterCommand = new AsyncCommand(RegisterAsync, () => CanSubmit);
        SendCodeCommand = new AsyncCommand(SendCodeAsync, () => !_isBusy);
        LogoutCommand = new AsyncCommand(LogoutAsync);
        SwitchModeCommand = new RelayCommand(SwitchMode);
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Identifier) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            Error("请输入账号和密码。");
            return;
        }

        SetBusy(true);
        StatusMessage = "正在验证账号…";
        StatusBrush = Brushes.DimGray;

        var response = await _api.LoginAsync(new LoginRequest
        {
            LoginType = LoginType,
            Identifier = Identifier.Trim(),
            Password = LoginPassword
        });
        SetBusy(false);

        if (!response.Success || response.Data is null)
        {
            Error(response.Message);
            return;
        }

        AuthSession.SetTokens(response.Data.AccessToken, response.Data.RefreshToken);
        CurrentUser = response.Data.UserInfo;
        IsAuthenticated = true;
        Raise(nameof(IsAuthenticated));
        Raise(nameof(IsAnonymous));
    }

    private async Task SendCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(VerificationTarget))
        {
            Error("请先输入邮箱地址或手机号码。");
            return;
        }

        SetBusy(true);
        SendCodeText = "发送中…";

        var response = await _api.SendCodeAsync(new SendCodeRequest
        {
            Type = VerifyType,
            Target = VerificationTarget.Trim()
        });
        SetBusy(false);
        SendCodeText = "发送验证码";

        if (response.Success)
        {
            Success(response.Message);
        }
        else
        {
            Error(response.Message);
        }
    }

    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(RegisterUsername)
            || RegisterPassword.Length < 6
            || string.IsNullOrWhiteSpace(VerificationTarget)
            || string.IsNullOrWhiteSpace(VerificationCode))
        {
            Error("请完整填写注册信息；密码至少 6 位。");
            return;
        }

        SetBusy(true);
        StatusMessage = "正在创建账户…";
        StatusBrush = Brushes.DimGray;

        var request = new RegisterRequest
        {
            Username = RegisterUsername.Trim(),
            Password = RegisterPassword,
            Type = VerifyType,
            VerificationCode = VerificationCode.Trim(),
            Email = VerifyType == "Email" ? VerificationTarget.Trim() : null,
            PhoneNumber = VerifyType == "PhoneNumber" ? VerificationTarget.Trim() : null
        };

        var response = await _api.RegisterAsync(request);
        SetBusy(false);

        if (response.Success)
        {
            Success("注册成功，请使用新账户登录。");
            SwitchMode();
        }
        else
        {
            Error(response.Message);
        }
    }

    private async Task LogoutAsync()
    {
        var token = AuthSession.RefreshToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            await _api.LogoutAsync(new LogoutRequest { RefreshToken = token });
        }

        AuthSession.Clear();
        IsAuthenticated = false;
        CurrentUser = new UserInfo();
        Identifier = "";
        LoginPassword = "";
        StatusMessage = "";
        Raise(nameof(IsAuthenticated));
        Raise(nameof(IsAnonymous));
    }

    private void SwitchMode()
    {
        IsLoginMode = !IsLoginMode;
        StatusMessage = "";
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        Raise(nameof(CanSubmit));
        ((AsyncCommand)LoginCommand).RaiseCanExecuteChanged();
        ((AsyncCommand)RegisterCommand).RaiseCanExecuteChanged();
        ((AsyncCommand)SendCodeCommand).RaiseCanExecuteChanged();
    }

    private void Error(string message)
    {
        StatusMessage = string.IsNullOrWhiteSpace(message) ? "操作失败，请稍后重试。" : message;
        StatusBrush = Brushes.Firebrick;
    }

    private void Success(string message)
    {
        StatusMessage = message;
        StatusBrush = Brushes.SeaGreen;
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
