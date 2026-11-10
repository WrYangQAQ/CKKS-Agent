# CKKS Agent WPF 前端

CKKS System 的用户端桌面界面，包含以下已对接后端的功能：

- 用户名、邮箱或手机号登录
- 邮箱或手机验证码注册
- 发送注册验证码
- 登出并撤销刷新令牌
- 登录后的用户资料与会话状态展示
- 登录后上传头像（JPG、JPEG、PNG，最大 2 MB）

运行前先启动后端，再执行：

```powershell
dotnet run --project .\CKKS-Agent.Wpf.csproj
```

默认连接后端 Development HTTPS 地址 `https://localhost:7206/api/user/`。Access Token 与 Refresh Token 均仅保存在进程内存；关闭应用后不会保留登录状态。

头像上传调用 `POST /api/user/upload-avatar`，以 Bearer Token 鉴权，`multipart/form-data` 文件字段为 `file`。

> 开发环境为方便使用本机 ASP.NET HTTPS 开发证书，客户端仅对 `localhost` / `127.0.0.1` 放宽证书验证。发布时应替换为正式 API 域名和可信证书。
