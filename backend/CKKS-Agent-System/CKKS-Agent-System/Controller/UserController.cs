using CKKS_Agent_System.Config;
using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Enum;
using CKKS_Agent_System.Helper;
using CKKS_Agent_System.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Security.Claims;

namespace CKKS_Agent_System.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : BaseController
    {
        private readonly UserService _userService;
        private readonly TokenService _tokenService;

        public UserController(UserService userService, TokenService tokenService)
        {
            _userService = userService;
            _tokenService = tokenService;
        }

        // 登录接口
        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
        {
            var response = await _userService.LoginAsync(request);
            return ReturnResponse(response);
        }

        // 发送注册验证码接口
        [HttpPost("register/send-code")]
        public async Task<IActionResult> SendRegisterCodeAsync([FromBody] SendCodeRequest request)
        {
            var response = await _userService.SendRegisterCodeAsync(request);
            return ReturnResponse(response);
        }

        // 用户注册接口
        [HttpPost("register")]
        public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request)
        {
            // 模型验证（[Required] 等特性自动触发）
            if (!ModelState.IsValid)
            {
                var errorMsg = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
                return ReturnResponse(new ApiResponse { Success = false, Message = errorMsg ?? "参数错误" });
            }

            var response = await _userService.RegisterAsync(request);
            return ReturnResponse(response);
        }

        // 用户令牌刷新接口
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshAsync([FromBody] RefreshTokenRequest request)
        {
            var response = await _tokenService.RefreshAccessTokenAsync(request.RefreshToken);
            return ReturnResponse(response);
        }

        // 登出接口
        [HttpPost("logout")]
        public async Task<IActionResult> LogoutAsync([FromBody] LogoutRequest request)
        {
            var tokenHash = HashHelper.ComputeSHA256Hash(request.RefreshToken);
            await _tokenService.RevokeRefreshTokenAsync(tokenHash);   // 数据库挂了就让异常正常抛出

            return ReturnResponse(new ApiResponse
            {
                Success = true,
                Message = "登出成功",
                Code = ResponseCode.Success
            });
        }

        // 用户头像上传接口
        [Authorize]
        [HttpPost("upload-avatar")]
        public async Task<IActionResult> UploadAvatarAsync([FromForm] IFormFile file)
        {
            var userId = GetUserIdFromClaims();
            var response = await _userService.UploadAvatarAsync(userId, file);
            return ReturnResponse(response);
        }

        // 发送修改密码验证码接口
        [Authorize]
        [HttpPost("reset-password/send-code")]
        public async Task<IActionResult> SendResetPasswordCodeAsync([FromBody] SendCodeRequest request)
        {
            long userId = GetUserIdFromClaims();
            var response = await _userService.SendResetPasswordCodeAsync(request, userId);
            return ReturnResponse(response);
        }

        // 用户修改密码接口
        [Authorize]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest request)
        {
            var userId = GetUserIdFromClaims();
            var response = await _userService.ResetPasswordAsync(request, userId);
            return ReturnResponse(response);
        }

        // 单独绑定邮箱验证码发送接口
        [Authorize]
        [HttpPost("bind-email/send-code")]
        public async Task<IActionResult> SendBindEmailCodeAsync([FromBody] SendCodeRequest request)
        {
            var userId = GetUserIdFromClaims();
            var response = await _userService.SendBindEmailCodeAsync(request, userId);
            return ReturnResponse(response);
        }

        // 绑定邮箱接口
        [Authorize]
        [HttpPost("bind-email")]
        public async Task<IActionResult> BindEmailAsync([FromBody] BindEmailRequest request)
        {
            long userId = GetUserIdFromClaims();
            var response = await _userService.BindEmailAsync(request, userId);
            return ReturnResponse(response);
        }

        // 单独绑定手机号验证码发送接口
        [Authorize]
        [HttpPost("bind-phone/send-code")]
        public async Task<IActionResult> SendBindPhoneNumberCodeAsync([FromBody] SendCodeRequest request)
        {
            var userId = GetUserIdFromClaims();
            var response = await _userService.SendBindPhoneNumberCodeAsync(request, userId);
            return ReturnResponse(response);
        }

        // 绑定手机接口
        [Authorize]
        [HttpPost("bind-phone")]
        public async Task<IActionResult> BindPhoneNumberAsync([FromBody] BindPhoneNumberRequest request)
        {
            long userId = GetUserIdFromClaims();
            var response = await _userService.BindPhoneNumberAsync(request, userId);
            return ReturnResponse(response);
        }

        // 用户修改绑定时的用户认证验证码发送接口
        [Authorize]
        [HttpPost("reset-identity-auth/send-code")]
        public async Task<IActionResult> SendResetAuthCodeAsync([FromBody] SendResetIndentiyAuthCodeRequest request)
        {
            long userId = GetUserIdFromClaims();
            var response = await _userService.SendResetIdentityAuthCodeAsync(request, userId);
            return ReturnResponse(response);
        }

        // 用户修改绑定时的用户认证接口
        [Authorize]
        [HttpPost("reset-identity-auth")]
        public async Task<IActionResult> ResetAuthAsync([FromBody] ResetIndentiyAuthRequest request)
        {
            long userId = GetUserIdFromClaims();
            var response = await _userService.ResetIdentityAuthAsync(request, userId);
            return ReturnResponse(response);
        }

        // 用户换绑时的验证码发送接口
        [Authorize]
        [HttpPost("reset-identity/send-code")]
        public async Task<IActionResult> SendResetIdentityCodeAsync([FromBody] SendCodeRequest request)
        {
            long userId = GetUserIdFromClaims();
            var response = await _userService.SendResetIdentityCodeAsync(request, userId);
            return ReturnResponse(response);
        }

        // 用户换绑
        [Authorize]
        [HttpPost("reset-identity")]
        public async Task<IActionResult> ResetIdentityAsync([FromBody] ResetIdentityRequest request)
        {
            long userId = GetUserIdFromClaims();
            var response = await _userService.ResetIdentityAsync(request, userId);
            return ReturnResponse(response);
        }

        // ==============  以下是私有工具方法  ==============
        private long GetUserIdFromClaims()
        {
            // 兼容：映射后的长名、JWT 标准短名、以及历史遗留的自定义名
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub")
                           ?? User.FindFirst("UserId");

            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
            {
                throw new UnauthorizedAccessException("用户未登录或令牌无效");
            }

            return userId;
        }
    }
}
