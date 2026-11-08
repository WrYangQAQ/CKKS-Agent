# 头像上传后端实现清单（ASP.NET Core）

前端已按以下接口完成对接：`POST /api/user/avatar`，`multipart/form-data`，字段名 `avatar`；要求 JWT 登录。

## 推荐流程

1. 在 `UserController` 新增带 `[Authorize]` 的 Action，参数为 `[FromForm] IFormFile avatar`。从 `ClaimTypes.NameIdentifier`（或你签发 JWT 时使用的用户 ID claim）取用户 ID，**不要**从请求体接收用户 ID。
2. 在 `UserService` 中完成全部校验：文件非空、最大 2 MiB、只允许 JPEG/PNG/WebP；不要相信 `ContentType` 和文件扩展名。读取文件开头魔数后再使用图像库实际解码，拒绝解码失败、像素过大或动画图。
3. 用服务端生成的随机文件名保存，例如 `{Guid:N}.webp`；绝不使用原文件名，也不要把客户端传入路径拼入物理路径。将文件写到 `wwwroot/Avatar` 之外的受控目录或对象存储；若公开访问，使用固定 `/Avatar/{文件名}` 映射。
4. 建议解码后重编码为固定尺寸（例如 512×512）WebP/JPEG，从而移除 EXIF、GPS 和潜在的格式欺骗内容。图片处理库可选 ImageSharp；设置像素上限，避免解压炸弹。
5. 文件成功落盘后，再更新当前用户的 `AvatarUrl` 并保存数据库。数据库更新失败时删除新文件；更新成功后异步清理旧头像（默认头像不删）。
6. 返回 `{ success, message, data: { avatarUrl } }`，不能返回物理路径、异常堆栈或原始文件名。失败沿用现有 `ApiResponse` 与 `ResponseCode`。

## 必须补上的基础配置

- 在 `Program.cs` 的 `UseAuthorization()` 之前调用 `app.UseStaticFiles()`，否则 `/Avatar/...` 不会被公开提供。
- 用 `app.Use(async (ctx, next) => { ... })` 或 Kestrel 的请求体限制为头像接口设置 2 MiB 以上的合理上限；同时在 Action/服务层保留大小限制，不能只依赖一层。
- 生产环境保持 HTTPS，Cookie 使用 `Secure`、`HttpOnly`、合适的 `SameSite`；上传接口验证 `Authorization: Bearer`。
- 对该接口按“用户 ID + IP”限流，并记录失败原因（不记录文件内容、Token 或完整文件名）。
- 若前后端跨域，只允许明确的前端 Origin 和 `Authorization` 头；启用凭据时不能使用 `*`。

## 最小验收用例

- 未登录请求返回 401；伪造其他用户 ID 不能覆盖其头像。
- 2 MiB 以上文件返回 413；`.jpg` 后缀的脚本、空文件、错误魔数、损坏图片均返回 400。
- 合法 JPEG/PNG/WebP 返回新 URL；刷新个人资料后仍显示该 URL。
- 数据库保存失败或图像处理失败时，临时文件不残留；旧头像仍可用。
