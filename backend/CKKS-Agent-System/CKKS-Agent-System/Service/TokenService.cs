using CKKS_Agent_System.Config;
using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Entity;
using CKKS_Agent_System.Enum;
using CKKS_Agent_System.Helper;
using CKKS_Agent_System.Repository;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CKKS_Agent_System.Service
{
    public class TokenService
    {
        private readonly JwtSetting _jwtSetting;
        private readonly TokenRepository _tokenRepository;
        private readonly ILogger<TokenService> _logger;
        private readonly UserRepository _userRepository; 

        public TokenService(
            IOptions<JwtSetting> jwtOptions, 
            TokenRepository tokenRepository, 
            ILogger<TokenService> logger, 
            UserRepository userRepository)
        {
            _jwtSetting = jwtOptions.Value;
            _tokenRepository = tokenRepository;
            _logger = logger;
            _userRepository = userRepository;

            if (string.IsNullOrWhiteSpace(_jwtSetting.SecretKey))
            {
                throw new InvalidOperationException("JWT SecretKey 未配置");
            }

            if (_jwtSetting.RefreshTokenExpireDays <= 0)
            {
                throw new InvalidOperationException("RefreshTokenExpireDays 必须大于 0");
            }
        }

        // 生成短期 JWT
        public string GenerateShortTermToken(User user)
        {
            // 检查密钥是否为空
            if (string.IsNullOrWhiteSpace(_jwtSetting.SecretKey))
            {
                throw new InvalidOperationException("JWT SecretKey 未配置");
            }

            // 使用 UTF8 获取字节，构建 JWT 签名对称密钥
            var keyBytes = Encoding.UTF8.GetBytes(_jwtSetting.SecretKey);
            var symmetricKey = new SymmetricSecurityKey(keyBytes);

            // 构建标准 Claims
            var claims = new[]
            {
                // 写入通用风格用户信息
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()), 
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),

                // 写入兼容 ASP.NET Core 风格用户信息
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()), 
                new Claim(ClaimTypes.Name, user.Username)
            };

            // 构建 TokenDescriptor
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),    // 将 Claim 数组包装为 ClaimsIdentity 对象
                Expires = DateTime.UtcNow.AddMinutes(_jwtSetting.AccessTokenExpireMinutes),
                Issuer = _jwtSetting.Issuer,     // 加上签发者
                Audience = _jwtSetting.Audience, // 加上受众
                SigningCredentials = new SigningCredentials(
                    symmetricKey,
                    SecurityAlgorithms.HmacSha256)  // 使用 HMAC-SHA256 算法进行签名
            };

            // 生成并返回
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // 生成长期 Refresh Token
        public string GenerateRefreshToken()
        {
            // 分配 32 个字节的内存（生成的字符串大约是 43-44 个字符长）
            var randomNumber = new byte[32];

            // 使用密码学安全的随机数填充这个数组
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }

            // 转成 Base64 字符串，方便前端存储和网络传输
            return Convert.ToBase64String(randomNumber);
        }

        // 持久化 Refresh Token
        public async Task<bool> PersistRefreshTokenAsync(long userId, string refreshToken)
        {
            var token = new RefreshToken
            {
                UserId = userId,
                TokenHash = HashHelper.ComputeSHA256Hash(refreshToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSetting.RefreshTokenExpireDays)
            };

            return await _tokenRepository.AddRefreshTokenAsync(token);
        }

        // 验证 Refresh Token 是否有效
        public bool IsRefreshTokenActive(RefreshToken token)
        {
            // 检查是否已撤销或已过期
            return token != null && DateTime.UtcNow < token.ExpiresAt && token.RevokedAt == null;
        }

        // 注销 Refresh Token
        public async Task<bool> RevokeRefreshTokenAsync(string tokenHash)
        {
            var token = await _tokenRepository.GetRefreshTokenByHashAsync(tokenHash);
            if (token != null && IsRefreshTokenActive(token))
            {
                token.RevokedAt = DateTime.UtcNow;
                return await _tokenRepository.UpdateRefreshTokenAsync(token);
            }
            return false;
        }

        // 给用户轮换 Refresh Token（撤销旧的，生成新的）
        public async Task<RotateRefreshTokenDto> RotateRefreshTokenAsync(RefreshToken oldToken)
        {
            // 生成新的 Refresh Token
            string newTokenString = GenerateRefreshToken();
            var newToken = new RefreshToken
            {
                UserId = oldToken.UserId,
                TokenHash = HashHelper.ComputeSHA256Hash(newTokenString),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSetting.RefreshTokenExpireDays)
            };

            // 撤销旧的 Refresh Token，并记录新 Token 的字符串
            oldToken.RevokedAt = DateTime.UtcNow;
            oldToken.ReplacedByTokenId = newToken.Id;

            // 持久化两个 Token 的状态
            bool rotateSuccess = await _tokenRepository.RotateRefreshTokenAsync(oldToken, newToken);
            if (rotateSuccess)
            {
                return new RotateRefreshTokenDto
                {
                    NewToken = newToken,
                    TokenPlaintext = newTokenString
                };
            }
            else
            {
                throw new Exception("轮换 Refresh Token 失败");
            }
        }

        // 给用户刷新短期令牌 Access Token
        public async Task<ApiResponse> RefreshAccessTokenAsync(string? refreshToken)
        {
            // 检查 refreshToken 是否为空
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "刷新令牌不能为空",
                    Code = ResponseCode.InvalidParameter
                };
            }

            // 验证 refreshToken 是否有效
            var tokenHash = HashHelper.ComputeSHA256Hash(refreshToken);
            var storedToken = await _tokenRepository.GetRefreshTokenByHashAsync(tokenHash);

            if (storedToken == null || DateTime.UtcNow >= storedToken.ExpiresAt)
            {
                return new ApiResponse 
                { 
                    Success = false, 
                    Message = "刷新令牌无效或已过期", 
                    Code = ResponseCode.InvalidCredentials 
                };
            }

            // 如果 Token 已经被撤销了（比如被人重放，或者用户主动退出），说明有风险
            if (storedToken.RevokedAt != null)
            {
                // 直接把这个用户的所有 Token 全部强制下线，让黑客和用户都得重新登录
                bool revokeSuccess = await _tokenRepository.RevokeAllUserTokensAsync(storedToken.UserId);
                if (!revokeSuccess)
                {
                    _logger.LogWarning($"在检测到已撤销的刷新令牌后，未能撤销用户 {storedToken.UserId} 的所有令牌。");
                }
                return new ApiResponse { Success = false, Message = "检测到异常访问，请重新登录", Code = ResponseCode.InvalidCredentials };
            }

            // 查出用户信息，生成新的 AccessToken
            var user = await _userRepository.GetUserByIdAsync(storedToken.UserId);
            if (user == null)
            {
                return new ApiResponse { Success = false, Message = "用户不存在" };
            }
            var rotateResult = await RotateRefreshTokenAsync(storedToken);
            var newAccessToken = GenerateShortTermToken(user);

            if (rotateResult == null || string.IsNullOrWhiteSpace(rotateResult.TokenPlaintext))
            {
                return new ApiResponse { Success = false, Message = "刷新令牌轮换失败", Code = ResponseCode.InternalError };
            }

            var dto = new RotateAccessTokenDto
            {
                AccessToken = newAccessToken,
                ExpiresIn = _jwtSetting.AccessTokenExpireMinutes * 60, // 转换为秒
                RefreshToken = rotateResult.TokenPlaintext
            };

            return new ApiResponse 
            { 
                Success = true, 
                Message = "刷新令牌成功", 
                Code = ResponseCode.Success,
                Data = dto 
            };
        }

        // 注销用户的所有 Refresh Token
        public async Task<bool> RevokeAllRefreshToken(long userId)
        {
            return await _tokenRepository.RevokeAllUserTokensAsync(userId);
        }
    }
}
