using CKKS_Agent_System.Config;
using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Enum;
using CKKS_Agent_System.Helper;
using CKKS_Agent_System.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

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
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _userService.LoginAsync(request);
            return ReturnResponse(response);
        }

        // 发送验证码接口
        [HttpPost("send-code")]
        public async Task<IActionResult> SendRegisterCode([FromBody] SendCodeRequest request)
        {
            var response = await _userService.SendCodeAsync(request);
            return ReturnResponse(response);
        }

        // 用户注册接口
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
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
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            var response = await _tokenService.RefreshAccessTokenAsync(request.RefreshToken);
            return ReturnResponse(response);
        }

        // 登出接口
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
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
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
        {
            var userId = GetUserIdFromClaims();
            var response = await _userService.UploadAvatarAsync(userId, file);
            return ReturnResponse(response);
        }

        // ==============  以下是私有工具方法  ==============
        private long GetUserIdFromClaims()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
            {
                throw new UnauthorizedAccessException("用户未登录或令牌无效");
            }
            return userId;
        }
    }
}
