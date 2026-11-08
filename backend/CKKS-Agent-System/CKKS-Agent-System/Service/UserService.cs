using Azure.Core;
using CKKS_Agent_System.Config;
using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Entity;
using CKKS_Agent_System.Enum;
using CKKS_Agent_System.Helper;
using CKKS_Agent_System.Repository;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace CKKS_Agent_System.Service
{
    public class UserService
    {
        private readonly UserRepository _userRepository;
        private readonly TokenService _tokenService;
        private readonly JwtSetting _jwtSetting;
        private readonly IDistributedCache _cache;
        private readonly ILogger<UserService> _logger;

        // 定义允许的文件扩展名和最大文件大小常量
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxPictureAvatarSize = 2 * 1024 * 1024;
        private const long MaxPictureWebsiteDecorationImageSize = 20 * 1024 * 1024;

        public UserService(
            UserRepository userRepository, 
            TokenService tokenService,
            IOptions<JwtSetting> options, 
            IDistributedCache cache,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _jwtSetting = options.Value;
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
                LoginType.Username => await _userRepository.GetUserByUsername(identifier),
                LoginType.PhoneNumber => await _userRepository.GetUserByPhoneNumber(identifier),
                LoginType.Email => await _userRepository.GetUserByEmail(identifier),
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
            bool persistSuccess = await _tokenService.PersistRefreshToken(userFinded.UserId, longTermToken);

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
        public async Task<ApiResponse> SendCodeAsync(SendCodeRequest request)
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
                VerifyType.Email => await _userRepository.GetUserByEmail(target),
                VerifyType.PhoneNumber => await _userRepository.GetUserByPhoneNumber(target),
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

            // 防刷：检查 Redis 里是否已经有这个 Key（单独用一个 Key 来做防刷拦截）
            var cooldownKey = $"VerifyCode:Cooldown:{target}";
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
            await VerifyCodeHelper.SaveCodeAsync(_cache, target, code, "Register");

            // 写入防刷冷却锁，60秒过期
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };
            await _cache.SetStringAsync(cooldownKey, "1", options);

            // 发送验证码（这里先模拟）
            if (request.Type == VerifyType.Email)
            {
                // await _emailService.SendAsync(request.Email, $"您的注册验证码是：{code}");
                Console.WriteLine($"[模拟发送邮箱] 收件人：{target}，验证码：{code}");
            }
            else
            {
                // await _smsService.SendAsync(request.PhoneNumber, code);
                Console.WriteLine($"[模拟发送短信] 收件人：{target}，验证码：{code}");
            }

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

            // 归一化邮箱与手机号，确保数据库查询时不会因为大小写或格式差异而漏掉已存在的记录
            request.PhoneNumber = VerificationTargetHelper.NormalizePhoneNumber(request.PhoneNumber);
            request.Email = VerificationTargetHelper.NormalizeEmail(request.Email);

            // 检查用户名、邮箱、手机号是否已存在
            var existingUserByUsername = await _userRepository.GetUserByUsername(request.Username);
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
                var existingUserByEmail = await _userRepository.GetUserByEmail(request.Email!);
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
                var existingUserByPhoneNumber = await _userRepository.GetUserByPhoneNumber(request.PhoneNumber!);
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
                    VerifyType.Email ? request.Email! : request.PhoneNumber!, request.VerificationCode, "Register"
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
            bool addSuccess = await _userRepository.AddUser(user);

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
            if (file == null || file.Length == 0)
                return new ApiResponse { Success = false, Message = "请选择要上传的文件" };

            // 限制文件大小（比如最大 2MB）
            if (file.Length > 2 * 1024 * 1024)
                return new ApiResponse { Success = false, Message = "图片大小不能超过 2MB" };

            // 限制文件类型（防止上传木马脚本）
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return new ApiResponse { Success = false, Message = "只支持 jpg, png, gif 格式的图片" };

            // 重命名文件（用雪花 ID 或 Guid 生成新的文件名）
            var newFileName = $"{IDGeneration.NextId()}{extension}";

            // 确定保存的物理路径（比如保存到 wwwroot/Avatars 目录下）
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Avatars");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var filePath = Path.Combine(uploadDir, newFileName);

            // 保存文件到硬盘
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 构造可供前端访问的 URL 路径
            var avatarUrl = $"/Avatars/{newFileName}";

            // 更新数据库中的 User 实体
            var user = await _userRepository.GetUserById(userId);
            if (user == null) return new ApiResponse { Success = false, Message = "用户不存在" };

            // （可选）如果你想顺便删掉旧头像，可以在这里写 File.Delete(旧路径) 的逻辑

            user.AvatarUrl = avatarUrl;
            await _userRepository.UpdateUser(user);

            return new ApiResponse
            {
                Success = true,
                Message = "头像上传成功",
                Data = new { AvatarUrl = avatarUrl } // 把新地址返回给前端
            };
        }

        // ============== 以下是私有工具方法 ==============

        // 验证密码是否匹配
        public bool IsPasswordMatched(string inputPassword, string storedPasswordHash)
        {
            return PasswordHelper.VerifyPassword(inputPassword, storedPasswordHash);
        }
    }
}
