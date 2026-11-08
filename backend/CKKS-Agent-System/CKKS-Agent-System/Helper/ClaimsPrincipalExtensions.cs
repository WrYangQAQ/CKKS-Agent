using System.Security.Claims;

namespace CKKS_Agent_System.Helper
{
    public static class ClaimsPrincipalExtensions
    {
        // 从当前用户的 Claims 中提取雪花 ID
        public static long GetUserId(this ClaimsPrincipal user)
        {
            // 优先找标准的 sub，找不到再找微软的 NameIdentifier
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userIdStr))
            {
                throw new UnauthorizedAccessException("无法从令牌中获取用户 ID");
            }

            return long.Parse(userIdStr);
        }
    }
}