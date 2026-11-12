# CKKS-Agent

面向 CKKS 同态加密参数自动化配置的毕业设计项目。项目目标是让 Agent 根据密文推理场景规划候选参数，并通过确定性的安全校验与实际性能评估筛选配置，降低手工调参的门槛。仓库仍处于开发阶段：目前主要实现了用户认证相关的后端基础功能，以及 WPF 前端的登录、注册界面；Agent 寻优、Microsoft SEAL 评估与 128-bit 安全边界校验尚未在本仓库实现。

## 当前进度

- 后端：注册、登录、验证码发送、Access Token 刷新、登出、密码修改和绑定邮箱接口；使用 SQL Server 保存用户和刷新令牌，Redis 保存验证码及发送冷却状态。绑定邮箱目前仅提供后端 API，前端界面尚未接入。
- 验证码：支持邮件模板发送和阿里云短信发送，验证码有效期为 5 分钟。使用邮件或短信需要自行配置对应服务的有效凭据。
- 前端：基于 WPF 的登录、验证码注册、登出、会话信息和头像上传界面，已对接用户 API。头像上传支持 JPG、JPEG、PNG，最大 2 MB。

规划中的功能包括模型上传与结构解析、Agent 参数搜索、静态安全剪枝、基于 Microsoft SEAL 的 CKKS 密文性能评估、128-bit 安全约束拦截、搜索过程审计及结果可视化。这些属于设计目标，不代表当前版本已经提供。

## 技术栈与目录

| 组件 | 技术 | 位置 |
| --- | --- | --- |
| 后端 API | C#、ASP.NET Core、.NET 10、EF Core | `backend/CKKS-Agent-System/CKKS-Agent-System/` |
| 桌面前端 | C#、WPF、.NET 10 for Windows | `frontend/CKKS-Agent.Wpf/` |
| 数据与缓存 | SQL Server、Redis | 后端配置与实体模型 |
| 消息发送 | MailKit、阿里云短信 SDK | 后端 `Service/` |

## 本地开发

需要 Windows、.NET 10 SDK、可用的 SQL Server 和 Redis 实例。邮件和短信功能还需要各自的服务账号及已配置的发送能力。

1. 参考后端 `appsettings.json` 的配置项，在本地的 `appsettings.Development.json` 或环境变量中设置真实连接串、JWT 密钥、邮箱授权码和阿里云 AccessKey。开发配置文件已被 `.gitignore` 排除；不要把真实凭据写回受 Git 跟踪的 `appsettings.json`。
2. 准备与 `AppDbContext` 中 `Users`、`RefreshTokens` 实体匹配的 SQL Server 数据表。仓库目前没有提供迁移或初始化脚本，单纯启动服务不会自动建表。
3. 在仓库根目录运行：

```powershell
dotnet restore .\backend\CKKS-Agent-System\CKKS-Agent-System.slnx
dotnet run --project .\backend\CKKS-Agent-System\CKKS-Agent-System\CKKS-Agent-System.csproj --launch-profile https
```

后端 HTTPS 开发地址在 `launchSettings.json` 中配置为 `https://localhost:7206`。另开终端运行前端：

```powershell
dotnet run --project .\frontend\CKKS-Agent.Wpf\CKKS-Agent.Wpf.csproj
```

前端运行说明见 [`frontend/CKKS-Agent.Wpf/README.md`](frontend/CKKS-Agent.Wpf/README.md)。头像上传接口为 `POST /api/user/upload-avatar`，需要 Bearer Token，表单文件字段为 `file`。

## 当前 API

以下接口均位于 `/api/user` 下：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| POST | `/login` | 用户登录 |
| POST | `/register/send-code` | 向邮箱或手机号发送注册验证码 |
| POST | `/register` | 用户注册 |
| POST | `/refresh` | 刷新访问令牌 |
| POST | `/logout` | 撤销刷新令牌 |
| POST | `/upload-avatar` | 登录后上传头像 |
| POST | `/reset-password/send-code` | 登录后发送修改密码验证码 |
| POST | `/reset-password` | 登录后凭验证码修改密码 |
| POST | `/bind-email/send-code` | 登录后向待绑定邮箱发送验证码 |
| POST | `/bind-email` | 登录后凭验证码绑定邮箱 |

接口的请求和响应结构以 `backend/CKKS-Agent-System/CKKS-Agent-System/DTO/` 中的类型为准。开发环境下还启用了 ASP.NET Core OpenAPI 文档端点。

## 设计方向

项目计划将参数搜索拆成“候选生成 → 静态安全约束检查 → 密文计算性能评估 → 结果反馈与调整”。Agent 负责规划和迭代；密码学安全边界由确定性的校验模块执行，避免直接采用未经验证的模型输出。目标是在安全要求下比较计算延迟和精度损失，并保留可追溯的搜索记录。

## 许可证

本项目采用 [MIT License](LICENSE)。
