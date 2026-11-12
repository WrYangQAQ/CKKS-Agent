using Azure.Core;
using CKKS_Agent_System.Config;
using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Entity;
using CKKS_Agent_System.Enum;
using CKKS_Agent_System.Helper;
using CKKS_Agent_System.Repository;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1.X509;

namespace CKKS_Agent_System.Service
{
    public class UserService
    {
        private readonly UserRepository _userRepository;
        private readonly TokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly JwtSetting _jwtSetting;
        private readonly FileUploadOption _uploadOption;
        private readonly IDistributedCache _cache;
        private readonly ILogger<UserService> _logger;

        // 允许上传的图片文件后缀
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png"
        };

        public UserService(
            UserRepository userRepository, 
            TokenService tokenService,
            IEmailService emailService,
            ISmsService smsService,
            IOptions<JwtSetting> options, 
            IOptions<FileUploadOption> uploadOption,
            IDistributedCache cache,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _emailService = emailService;
            _smsService = smsService;
            _jwtSetting = options.Value;
            _uploadOption = uploadOption.Value;
            _cache = cache;
            _logger = logger;
        }

        // 用户登录
        public async Task<ApiResponse> LoginAsync(LoginRequest request)
        {
            // 如果传了不支持的类型（userFinded 为 null 且请求本身不合法）
            if (request.LoginType != LoginType.Username &&
                request.LoginType != LoginType.PhoneNumber &&
                request.LoginType != LoginType.Email)
            {
                return new ApiResponse 
                { 
                    Success = false, 
                    Message = "不支持的登录类型", 
                    Code = ResponseCode.UnsupportedLoginType 
                };
            }

            // 根据登录类型，归一化登录标识符（邮箱、手机号）以便数据库查询
            var identifier = request.LoginType switch
            {
                LoginType.Username => request.Identifier,
                LoginType.PhoneNumber => VerificationTargetHelper.NormalizePhoneNumber(request.Identifier),
                LoginType.Email => VerificationTargetHelper.NormalizeEmail(request.Identifier),
                _ => request.Identifier
            };

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "登录标识符不能为空",
                    Code = ResponseCode.InvalidParameter
                };
            }

            // 根据登录类型，一次性查出用户
            User? userFinded = request.LoginType switch
            {
                LoginType.Username => await _userRepository.GetUserByUsernameAsync(identifier),
                LoginType.PhoneNumber => await _userRepository.GetUserByPhoneNumberAsync(identifier),
                LoginType.Email => await _userRepository.GetUserByEmailAsync(identifier),
                _ => null
            };

            // 统一校验用户和密码（统一错误提示，防止用户枚举）
            if (userFinded == null || !IsPasswordMatched(request.Password, userFinded.PasswordHash))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "账号或密码错误",
                    Code = ResponseCode.InvalidCredentials
                };
            } 

            // 生成短期 AccessToken
            var shortTermToken = _tokenService.GenerateShortTermToken(userFinded);

            // 生成长期 RefreshToken 并持久化
            var longTermToken = _tokenService.GenerateRefreshToken();
            bool persistSuccess = await _tokenService.PersistRefreshTokenAsync(userFinded.UserId, longTermToken);

            if (!persistSuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "系统异常：刷新令牌生成失败",
                    Code = ResponseCode.RefreshTokenPersistError
                };
            }

            // 组装返回数据
            var loginResponse = new LoginResponse(userFinded, shortTermToken, _jwtSetting.AccessTokenExpireMinutes * 60);

            loginResponse.RefreshToken = longTermToken;

            return new ApiResponse
            {
                Success = true,
                Message = "登录成功",
                Data = loginResponse,
                Code = ResponseCode.Success
            };
        }

        // 向用户发送注册验证码
        public async Task<ApiResponse> SendRegisterCodeAsync(SendCodeRequest request)
        {
            var target = VerificationTargetHelper.Normalize(request.Type, request.Target);

            // 基础空参校验及类型校验
            if (string.IsNullOrWhiteSpace(target))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "发送目标不能为空",
                    Code = ResponseCode.InvalidParameter
                };
            }

            if (request.Type != VerifyType.Email && request.Type != VerifyType.PhoneNumber)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "不支持的验证码类型",
                    Code = ResponseCode.UnsupportedVerifyType
                };
            }

            // 查询数据库中是否已存在该邮箱或手机号
            var existingUser = request.Type switch
            {
                VerifyType.Email => await _userRepository.GetUserByEmailAsync(target),
                VerifyType.PhoneNumber => await _userRepository.GetUserByPhoneNumberAsync(target),
                _ => null
            };

            if (existingUser != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = request.Type == VerifyType.Email ? "邮箱已被注册" : "手机号已被注册",
                    Code = request.Type == VerifyType.Email ? ResponseCode.EmailAlreadyExists : ResponseCode.PhoneNumberAlreadyExists
                };
            }

            // 检查 Redis 里是否已经有这个 Key（单独用一个 Key 来做防刷拦截）
            var cooldownKey = $"VerifyCode:Cooldown:{SendCodeAction.Register.ToString()}:{target}";
            if (await _cache.GetStringAsync(cooldownKey) != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "发送过于频繁，请 1 分钟后再试",
                    Code = ResponseCode.TooManyVerifyCode
                };
            }

            // 生成验证码并存入 Redis
            var code = VerifyCodeHelper.GenerateCode();

            // 发送验证码
            bool sendSuccess = false;
            if (request.Type == VerifyType.Email)
            {
                sendSuccess = await SendVerifyCodeByEmailAsync(target, code, SendCodeAction.Register);
                if (!sendSuccess)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "邮件发送失败",
                        Code = ResponseCode.InternalError
                    };
                }
            }
            else
            {
                sendSuccess = await SendVerifyCodeByPhoneNumberAsync(target, code);
                if (!sendSuccess)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "短信发送失败",
                        Code = ResponseCode.InternalError
                    };
                }
            }

            // 确定验证码发送成功后，存入 Redis 并设置过期时间（5 分钟）
            await VerifyCodeHelper.SaveCodeAsync(_cache, target, code, SendCodeAction.Register.ToString());

            // 写入防刷冷却锁，60秒过期
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };
            await _cache.SetStringAsync(cooldownKey, "1", options);

            return new ApiResponse 
            { 
                Success = true, 
                Message = "验证码发送成功",
                Code = ResponseCode.Success
            };
        }

        // 用户注册
        public async Task<ApiResponse> RegisterAsync(RegisterRequest request)
        {
            if (request.Type != VerifyType.Email && request.Type != VerifyType.PhoneNumber)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "不支持的注册验证类型",
                    Code = ResponseCode.UnsupportedVerifyType
                };
            }

            // 防御性编程：确保对应类型的字段不为空
            if (request.Type == VerifyType.Email && string.IsNullOrEmpty(request.Email))
            {
                return new ApiResponse { Success = false, Message = "邮箱不能为空", Code = ResponseCode.InvalidParameter };
            }
            if (request.Type == VerifyType.PhoneNumber && string.IsNullOrEmpty(request.PhoneNumber))
            {
                return new ApiResponse { Success = false, Message = "手机号不能为空", Code = ResponseCode.InvalidParameter };
            }

            // 校验密码合法性
            if (!PasswordHelper.IsPasswordValid(request.Password))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "用户输入密码不符合规范，请重新输入！",
                    Code = ResponseCode.InvalidParameter
                };
            }

            // 归一化邮箱与手机号，确保数据库查询时不会因为大小写或格式差异而漏掉已存在的记录
            request.PhoneNumber = VerificationTargetHelper.NormalizePhoneNumber(request.PhoneNumber);
            request.Email = VerificationTargetHelper.NormalizeEmail(request.Email);

            // 检查用户名、邮箱、手机号是否已存在
            var existingUserByUsername = await _userRepository.GetUserByUsernameAsync(request.Username);
            if (existingUserByUsername != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "用户名已被注册",
                    Code = ResponseCode.UserNameAlreadyExists
                };
            }

            if (request.Type == VerifyType.Email)
            {
                var existingUserByEmail = await _userRepository.GetUserByEmailAsync(request.Email!);
                if (existingUserByEmail != null)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "邮箱已被注册",
                        Code = ResponseCode.EmailAlreadyExists
                    };
                }
            }
            else
            {
                var existingUserByPhoneNumber = await _userRepository.GetUserByPhoneNumberAsync(request.PhoneNumber!);
                if (existingUserByPhoneNumber != null)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "手机号已被注册",
                        Code = ResponseCode.PhoneNumberAlreadyExists
                    };
                }
            }

            var verifySuccess = await VerifyCodeHelper.ValidateCodeAsync
                (_cache, request.Type == 
                    VerifyType.Email ? request.Email! : request.PhoneNumber!, request.VerificationCode, SendCodeAction.Register.ToString()
                );

            if (!verifySuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "验证码错误或已过期",
                    Code = ResponseCode.InvalidVerificationCode
                };
            }

            // 创建新用户对象并保存到数据库
            var user = new User();
            user.Username = request.Username;
            user.PasswordHash = PasswordHelper.HashPassword(request.Password);

            if (request.Type == VerifyType.Email)
            {
                user.Email = request.Email;
                user.PhoneNumber = null;
                user.IsEmailVerified = true;
            }
            else
            {
                user.PhoneNumber = request.PhoneNumber;
                user.Email = null;
                user.IsPhoneNumberVerified = true;
            }
            bool addSuccess = await _userRepository.AddUserAsync(user);

            if (!addSuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "注册失败，请稍后再试!",
                    Code = ResponseCode.DatabaseError
                };
            }
            else 
            {
                return new ApiResponse
                {
                    Success = true,
                    Message = "注册成功!",
                    Code = ResponseCode.Success
                };
            }
        }

        // 用户上传头像
        public async Task<ApiResponse> UploadAvatarAsync(long userId, IFormFile file)
        {
            // 检查文件是否为空
            if (file == null || file.Length == 0)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "请选择要上传的文件",
                    Code = ResponseCode.FileProcessingFailed
                };
            }

            // 限制文件大小
            if (file.Length > _uploadOption.AvatarUploadFileSize * 1024 * 1024)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "图片大小不能超过 " + _uploadOption.AvatarUploadFileSize + "MB",
                    Code = ResponseCode.FileTooLarge
                };
            }


            // 限制文件类型
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "只支持 jpg, png, jpeg 格式的图片",
                    Code = ResponseCode.InvalidFileType
                };
            }
            var safeExtension = extension == ".jpeg" ? ".jpg" : extension;

            // 用雪花 ID 生成新的文件名
            var newFileName = $"{IDGeneration.NextId()}{safeExtension}";

            // 确定保存的物理路径
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Avatars");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }
            var filePath = Path.Combine(uploadDir, newFileName);

            // 对上传图片进行安全验证和重新编码，防止伪造文件上传恶意脚本
            using var inputStream = file.OpenReadStream();
            using var safeOutputStream = new MemoryStream();

            bool isSafeImage = await ImageValidatorHelper.ValidateAndReencodeAsync(inputStream, safeOutputStream, extension);

            if (!isSafeImage)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "文件损坏或包含非法内容，请上传真实的图片",
                    Code = ResponseCode.InvalidFileContent
                };
            }

            try
            {
                // 保存文件到硬盘
                safeOutputStream.Position = 0; // 重置流指针到开头
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await safeOutputStream.CopyToAsync(fileStream);
                }
            }
            catch
            {
                DeleteFile(filePath);
                return new ApiResponse
                {
                    Success = false,
                    Message = "文件保存失败，请稍后再试",
                    Code = ResponseCode.FileProcessingFailed
                };
            }

            // 构造可供前端访问的 URL 路径
            var avatarUrl = $"/Avatars/{newFileName}";

            // 更新数据库中的 User 实体
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                // 删除已上传的头像文件
                DeleteFile(filePath);

                return new ApiResponse
                {
                    Success = false,
                    Message = "用户不存在",
                    Code = ResponseCode.UserNotFound
                };
            }

            // 在更新数据库中的 User 实体之前，先获取旧头像的 URL，以便在更新成功后删除旧头像文件
            var oldAvatarUrl = user.AvatarUrl;

            user.AvatarUrl = avatarUrl;
            try
            {
                bool updateSuccess = await _userRepository.UpdateUserAsync(user);
                if (!updateSuccess)
                {
                    // 删除已上传的头像文件
                    DeleteFile(filePath);
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "头像上传失败，请稍后再试",
                        Code = ResponseCode.DatabaseError
                    };
                }

                // 在更新用户头像成功后删掉用户之前的头像文件
                if (!string.IsNullOrEmpty(oldAvatarUrl) && oldAvatarUrl.StartsWith("/Avatars/") &&
                    oldAvatarUrl != "/Avatars/c21dec73-4a74-4f2c-bccf-00b42a2418e2.png")
                {
                    var oldFileName = Path.GetFileName(oldAvatarUrl);
                    var oldFilePath = Path.Combine(uploadDir, oldFileName);
                    DeleteFile(oldFilePath);
                }
            }
            catch
            {
                DeleteFile(filePath);
                throw;
            }

            return new ApiResponse
            {
                Success = true,
                Message = "头像上传成功",
                Data = new { AvatarUrl = avatarUrl } // 把新地址返回给前端
            };
        }

        // 向用户发送修改密码所需验证码
        public async Task<ApiResponse> SendResetPasswordCodeAsync(SendCodeRequest request, long userId)
        {

            // 校验用户是否存在
            var currentUser = await _userRepository.GetUserByIdAsync(userId);
            if (currentUser == null)
            {
                _logger.LogWarning("修改密码验证码：用户不存在。UserId={UserId}", userId);
                return new ApiResponse
                {
                    Success = false,
                    Message = "验证码发送失败！",
                    Code = ResponseCode.UserNotFound
                };
            }

            // 校验验证类型是否合法
            if (request.Type != VerifyType.Email && request.Type != VerifyType.PhoneNumber)
            {
                _logger.LogWarning("修改密码验证码：不支持的验证类型。UserId={UserId}, Type={Type}", userId, request.Type);
                return new ApiResponse
                {
                    Success = false,
                    Message = "验证码发送失败！",
                    Code = ResponseCode.UnsupportedVerifyType
                };
            }

            var normalizedTarget = VerificationTargetHelper.Normalize(request.Type, request.Target);

            if (normalizedTarget == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "联系方式无效！",
                    Code = ResponseCode.BadRequest
                };
            }

            // 验证传入参数与数据库一致性
            if (request.Type == VerifyType.Email)
            {
                if (currentUser.Email != normalizedTarget || !currentUser.IsEmailVerified)
                {
                    _logger.LogWarning(
                        "修改密码验证码：邮箱校验失败。UserId={UserId}, TargetMatched={TargetMatched}, Verified={Verified}",
                        userId,
                        currentUser.Email == normalizedTarget,
                        currentUser.IsEmailVerified);
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "验证方式无效！",
                        Code = ResponseCode.Unauthorized
                    };
                }
            }

            if (request.Type == VerifyType.PhoneNumber)
            {
                if (currentUser.PhoneNumber != normalizedTarget || !currentUser.IsPhoneNumberVerified)
                {
                    _logger.LogWarning(
                        "修改密码验证码：手机号校验失败。UserId={UserId}, TargetMatched={TargetMatched}, Verified={Verified}",
                        userId,
                        currentUser.PhoneNumber == normalizedTarget,
                        currentUser.IsPhoneNumberVerified);
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "验证方式无效！",
                        Code = ResponseCode.Unauthorized
                    };
                }
            }

            // 构建冷却锁的 Key 并检查 Redis 里是否已经有了对应 Value
            var cooldownKey = $"VerifyCode:Cooldown:{SendCodeAction.ResetPassword.ToString()}:{currentUser.UserId}:{normalizedTarget}";

            if (await _cache.GetStringAsync(cooldownKey) != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "发送过于频繁，请 1 分钟后再试",
                    Code = ResponseCode.TooManyVerifyCode
                };
            }

            // 生成验证码
            var code = VerifyCodeHelper.GenerateCode();

            // 发送验证码
            bool sendSuccess = false;
            if (request.Type == VerifyType.Email)
            {
                sendSuccess = await SendVerifyCodeByEmailAsync(normalizedTarget, code, SendCodeAction.ResetPassword);
                if (!sendSuccess)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "邮件发送失败",
                        Code = ResponseCode.InternalError
                    };
                }
            }
            else
            {
                sendSuccess = await SendVerifyCodeByPhoneNumberAsync(normalizedTarget, code);
                if (!sendSuccess)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "短信发送失败",
                        Code = ResponseCode.InternalError
                    };
                }
            }

            // 将验证码写入 Redis 并设置过期时间
            await VerifyCodeHelper.SaveCodeAsync(_cache, normalizedTarget, code, SendCodeAction.ResetPassword.ToString());

            // 设置冷却锁
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };
            await _cache.SetStringAsync(cooldownKey, "1", options);

            return new ApiResponse
            {
                Success = true,
                Message = "验证码发送成功",
                Code = ResponseCode.Success
            };
        }

        // 用户修改密码
        public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, long userId)
        {
            // 校验密码输入合法性
            if (!PasswordHelper.IsPasswordValid(request.NewPassword))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "输入密码不符合规范，请重新输入！",
                    Code = ResponseCode.InvalidParameter
                };
            }

            // 校验验证方式
            if (request.Type != VerifyType.Email && request.Type != VerifyType.PhoneNumber)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "不支持的验证类型",
                    Code = ResponseCode.UnsupportedVerifyType
                };
            }

            // 校验当前用户是否合法
            var currentUser = await _userRepository.GetUserByIdAsync(userId);
            if (currentUser == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户不存在，请稍后重试",
                    Code = ResponseCode.UserNotFound
                };
            }

            // 校验用户验证方式
            string target = string.Empty;

            if (request.Type == VerifyType.Email && currentUser.Email != null && currentUser.IsEmailVerified)
            {
                target = currentUser.Email;
            }

            if (request.Type == VerifyType.PhoneNumber && currentUser.PhoneNumber != null && currentUser.IsPhoneNumberVerified)
            {
                target = currentUser.PhoneNumber;
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "用户该验证方式已失效或不存在，请更换验证方式",
                    Code = ResponseCode.InvalidCredentials
                };
            }

            // 对验证码进行校验
            var verifySuccess = await VerifyCodeHelper.ValidateCodeAsync(_cache, target, request.VerificationCode, SendCodeAction.ResetPassword.ToString());
            if (!verifySuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "验证码错误或已失效",
                    Code = ResponseCode.InvalidVerificationCode
                };
            }

            // 完成所有的校验，计算新密码的带盐哈希，存入数据库
            var pwdHash = PasswordHelper.HashPassword(request.NewPassword);
            currentUser.PasswordHash = pwdHash;
            try
            {
                var updateSuccess = await _userRepository.UpdateUserAsync(currentUser);
                if (updateSuccess)
                {
                    try
                    {
                        bool revokeSuccess = await _tokenService.RevokeAllRefreshToken(userId);

                        if (!revokeSuccess)
                        {
                            _logger.LogError($"注销用户令牌的过程中出现错误，用户ID：{userId}");
                            return new ApiResponse
                            {
                                Success = true,
                                Message = "密码已修改，但部分登录设备可能仍保持登录",
                                Code = ResponseCode.Success
                            };
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, $"注销用户令牌的过程中出现异常，用户ID：{userId}");
                        return new ApiResponse
                        {
                            Success = true,
                            Message = "密码已修改，但部分登录设备可能仍保持登录",
                            Code = ResponseCode.Success
                        };
                    }

                    return new ApiResponse
                    {
                        Success = true,
                        Message = "密码重置成功！",
                        Code = ResponseCode.Success
                    };
                }
                else
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "数据库更新时出现了错误，请稍后重试",
                        Code = ResponseCode.DatabaseError
                    };
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"数据库出现异常");
                return new ApiResponse
                {
                    Success = false,
                    Message = "服务器开小差了哦，请稍后重试",
                    Code = ResponseCode.InternalError
                };
            }
        }

        // 用户修改绑定邮箱

        // 用户修改绑定手机号

        // 单独绑定邮箱验证码发送
        public async Task<ApiResponse> SendBindEmailCodeAsync(SendCodeRequest request, long userId)
        {
            // 校验请求类型
            if (request.Type != VerifyType.Email)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "您正在访问错误的接口，请重试",
                    Code = ResponseCode.UnsupportedVerifyType
                };
            }

            // 校验用户存在
            var currentUser = await _userRepository.GetUserByIdAsync(userId);
            if (currentUser == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户不存在，请重新登录试试",
                    Code = ResponseCode.UserNotFound
                };
            }

            // 校验用户邮箱是否有空位
            if (currentUser.IsEmailVerified || !string.IsNullOrWhiteSpace(currentUser.Email))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户已经绑定了一个邮箱",
                    Code = ResponseCode.Conflict
                };
            }

            var target = VerificationTargetHelper.Normalize(request.Type, request.Target);
            if (target == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "请输入您想要绑定的邮箱",
                    Code = ResponseCode.BadRequest
                };
            }

            // 校验目标邮箱是否已被其它用户占用
            var existingUser = await _userRepository.GetUserByEmailAsync(target);
            if (existingUser != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "该邮箱已被其它用户绑定了",
                    Code = ResponseCode.Conflict
                };
            }

            // 检查冷却锁
            var cooldownKey = $"VerifyCode:Cooldown:{SendCodeAction.BindEmail.ToString()}:{currentUser.UserId}:{target}";
            if (await _cache.GetStringAsync(cooldownKey) != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "发送过于频繁，请 1 分钟后再试",
                    Code = ResponseCode.TooManyVerifyCode
                };
            }

            // 生成验证码
            var code = VerifyCodeHelper.GenerateCode();

            // 发送验证码
            bool sendSuccess = await SendVerifyCodeByEmailAsync(target, code, SendCodeAction.BindEmail);
            if (!sendSuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "邮件发送失败",
                    Code = ResponseCode.InternalError
                };
            }

            // 将验证码写入 Redis 并设置过期时间 5 min
            var codeKeyTarget = $"{userId}:{target}";
            await VerifyCodeHelper.SaveCodeAsync(_cache, codeKeyTarget, code, SendCodeAction.BindEmail.ToString());

            // 写入防刷冷却锁，60秒过期
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };
            await _cache.SetStringAsync(cooldownKey, "1", options);

            return new ApiResponse
            {
                Success = true,
                Message = "验证码发送成功",
                Code = ResponseCode.Success
            };
        }

        // 未绑定邮箱用户绑定邮箱（单独绑定）
        public async Task<ApiResponse> BindEmailAsync(BindEmailRequest request, long userId)
        {
            var target = VerificationTargetHelper.NormalizeEmail(request.Email);
            if (string.IsNullOrWhiteSpace(target))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "请输入您想要绑定的邮箱",
                    Code = ResponseCode.BadRequest
                };
            }

            // 校验用户存在
            var currentUser = await _userRepository.GetUserByIdAsync(userId);
            if (currentUser == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户不存在，请重新登录试试",
                    Code = ResponseCode.UserNotFound
                };
            }

            // 校验用户邮箱是否有空位
            if (currentUser.IsEmailVerified || !string.IsNullOrWhiteSpace(currentUser.Email))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户已经绑定了一个邮箱",
                    Code = ResponseCode.Conflict
                };
            }

            // 校验目标邮箱是否已被其它用户占用
            var existingUser = await _userRepository.GetUserByEmailAsync(target);
            if (existingUser != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "该邮箱已被其它用户绑定了",
                    Code = ResponseCode.Conflict
                };
            }

            // 校验验证码是否正确
            var codeKeyTarget = $"{userId}:{target}";
            var verifiedSuccess = await VerifyCodeHelper.ValidateCodeAsync(
                _cache,
                codeKeyTarget,
                request.VerificationCode,
                SendCodeAction.BindEmail.ToString());

            if (!verifiedSuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "验证码错误或已过期",
                    Code = ResponseCode.InvalidVerificationCode
                };
            }

            // 将新邮箱入库保存
            try
            {
                currentUser.Email = target;
                currentUser.IsEmailVerified = true;
                bool updateSuccess = await _userRepository.UpdateUserAsync(currentUser);
                if (!updateSuccess)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "在保存的过程中出错了",
                        Code = ResponseCode.DatabaseError
                    };
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "保存邮箱失败。UserId={UserId}", userId);
                return new ApiResponse
                {
                    Success = false,
                    Message = "在保存的过程中出错了",
                    Code = ResponseCode.DatabaseError
                };
            }

            return new ApiResponse
            {
                Success = true,
                Message = "邮箱绑定成功",
                Code = ResponseCode.Success
            };
        }

        // 单独绑定手机号发送验证码
        public async Task<ApiResponse> SendBindPhoneNumberCodeAsync(SendCodeRequest request, long userId)
        {
            // 校验请求类型
            if (request.Type != VerifyType.PhoneNumber)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "您正在访问错误的接口，请重试",
                    Code = ResponseCode.UnsupportedVerifyType
                };
            }

            // 校验用户存在
            var currentUser = await _userRepository.GetUserByIdAsync(userId);
            if (currentUser == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户不存在，请重新登录试试",
                    Code = ResponseCode.UserNotFound
                };
            }

            // 校验用户手机号是否有空位
            if (currentUser.IsPhoneNumberVerified || !string.IsNullOrWhiteSpace(currentUser.PhoneNumber))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户已经绑定了一个手机号码",
                    Code = ResponseCode.Conflict
                };
            }

            var target = VerificationTargetHelper.Normalize(request.Type, request.Target);
            if (target == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "请输入您想要绑定的手机号码",
                    Code = ResponseCode.BadRequest
                };
            }

            // 校验目标手机号是否已被其它用户占用
            var existingUser = await _userRepository.GetUserByPhoneNumberAsync(target);
            if (existingUser != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "该手机号已被其它用户绑定了",
                    Code = ResponseCode.Conflict
                };
            }

            // 检查冷却锁
            var cooldownKey = $"VerifyCode:Cooldown:{SendCodeAction.BindPhoneNumber.ToString()}:{currentUser.UserId}:{target}";
            if (await _cache.GetStringAsync(cooldownKey) != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "发送过于频繁，请 1 分钟后再试",
                    Code = ResponseCode.TooManyVerifyCode
                };
            }

            // 生成验证码
            var code = VerifyCodeHelper.GenerateCode();

            // 发送验证码
            bool sendSuccess = await SendVerifyCodeByPhoneNumberAsync(target, code);
            if (!sendSuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "验证码短信发送失败",
                    Code = ResponseCode.InternalError
                };
            }

            // 将验证码写入 Redis 并设置过期时间 5 min
            var codeKeyTarget = $"{userId}:{target}";
            await VerifyCodeHelper.SaveCodeAsync(_cache, codeKeyTarget, code, SendCodeAction.BindPhoneNumber.ToString());

            // 写入防刷冷却锁，60秒过期
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };
            await _cache.SetStringAsync(cooldownKey, "1", options);

            return new ApiResponse
            {
                Success = true,
                Message = "验证码发送成功",
                Code = ResponseCode.Success
            };
        }

        // 未绑定手机号用户绑定手机号（单独绑定）
        public async Task<ApiResponse> BindPhoneNumberAsync(BindPhoneNumberRequest request, long userId)
        {
            var target = VerificationTargetHelper.NormalizePhoneNumber(request.PhoneNumber);
            if (string.IsNullOrWhiteSpace(target))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "请输入您想要绑定的手机号码",
                    Code = ResponseCode.BadRequest
                };
            }

            // 校验用户存在
            var currentUser = await _userRepository.GetUserByIdAsync(userId);
            if (currentUser == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户不存在，请重新登录试试",
                    Code = ResponseCode.UserNotFound
                };
            }

            // 校验用户手机号是否有空位
            if (currentUser.IsPhoneNumberVerified || !string.IsNullOrWhiteSpace(currentUser.PhoneNumber))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "当前用户已经绑定了一个手机号",
                    Code = ResponseCode.Conflict
                };
            }

            // 校验目标手机号是否已被其它用户占用
            var existingUser = await _userRepository.GetUserByPhoneNumberAsync(target);
            if (existingUser != null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "该手机号已被其它用户绑定了",
                    Code = ResponseCode.Conflict
                };
            }

            // 校验验证码是否正确
            var codeKeyTarget = $"{userId}:{target}";
            var verifiedSuccess = await VerifyCodeHelper.ValidateCodeAsync(
                _cache,
                codeKeyTarget,
                request.VerificationCode,
                SendCodeAction.BindPhoneNumber.ToString());

            if (!verifiedSuccess)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "验证码错误或已过期",
                    Code = ResponseCode.InvalidVerificationCode
                };
            }

            // 将新手机号入库保存
            try
            {
                currentUser.PhoneNumber = target;
                currentUser.IsPhoneNumberVerified = true;
                bool updateSuccess = await _userRepository.UpdateUserAsync(currentUser);
                if (!updateSuccess)
                {
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "在保存的过程中出错了",
                        Code = ResponseCode.DatabaseError
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存手机号失败。UserId={UserId}", userId);
                return new ApiResponse
                {
                    Success = false,
                    Message = "在保存的过程中出错了",
                    Code = ResponseCode.DatabaseError
                };
            }

            return new ApiResponse
            {
                Success = true,
                Message = "手机号绑定成功",
                Code = ResponseCode.Success
            };
        }

        // ============== 以下是私有工具方法 ==============

        // 验证密码是否匹配
        private bool IsPasswordMatched(string inputPassword, string storedPasswordHash)
        {
            return PasswordHelper.VerifyPassword(inputPassword, storedPasswordHash);
        }

        // 向目标邮箱发送验证码
        private async Task<bool> SendVerifyCodeByEmailAsync(string email, string code, SendCodeAction action)
        {
            // 根据 action 确定邮件主题与模板，未支持的场景直接失败，避免静默走错模板
            var (subject, template) = action switch
            {
                SendCodeAction.Register => ("【CKKS Agent System】注册验证码", "email-verify.html"),
                SendCodeAction.ResetPassword => ("【CKKS Agent System】重置密码验证码", "email-reset-password.html"),
                SendCodeAction.ResetEmail => ("【CKKS Agent System】更换邮箱验证码", "email-reset-email.html"),
                SendCodeAction.ResetPhoneNumber => ("【CKKS Agent System】更换手机号验证码", "email-reset-phone.html"),
                SendCodeAction.BindEmail => ("【CKKS Agent System】绑定邮箱验证码", "email-bind-email.html"),
                SendCodeAction.BindPhoneNumber => ("【CKKS Agent System】绑定手机号验证码", "email-bind-phone.html"),
                _ => throw new NotSupportedException($"不支持的验证码场景：{action}")
            };

            var variables = new Dictionary<string, string>
            {
                { "CODE", code } // 对应模板里的 {{CODE}}
            };

            try
            {
                await _emailService.SendTemplateEmailAsync(email, subject, template, variables);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "发送验证码邮件失败。Action={Action}, Email={Email}, Template={Template}",
                    action, MaskHelper.MaskEmail(email), template);
                return false;
            }
        }

        // 向目标手机发送验证码
        private async Task<bool> SendVerifyCodeByPhoneNumberAsync(string phoneNumber, string code)
        {
            try
            {
                var result = await _smsService.SendVerificationCodeAsync(phoneNumber, code);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送短信验证码失败，手机号: {PhoneNumber}", MaskHelper.MaskPhone(phoneNumber));
                throw;
            }
        }

        // 删除目标地址文件
        private void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // 忽略删除失败的情况
                }
            }
        }
    }
}
