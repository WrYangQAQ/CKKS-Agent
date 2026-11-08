# CKKS Agent WPF 前端

这是头像上传界面的 WPF 项目。执行：

```powershell
dotnet run --project .\CKKS-Agent.Wpf.csproj
```

上传协议为 `POST /api/user/avatar`，`multipart/form-data`，文件字段为 `avatar`。开发环境地址已按后端 `launchSettings.json` 配置为 `https://localhost:7206`；在登录成功后调用 `AuthSession.SetAccessToken(loginResponse.AccessToken)`，头像上传会自动带上该短期 Token。

客户端校验用于提升体验，**不能替代服务器校验**；后端应继续按 `backend/AVATAR_UPLOAD_GUIDE.md` 校验文件、当前用户身份及图像内容。
