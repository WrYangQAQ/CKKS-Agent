using CKKS_Agent_System.Entity;

namespace CKKS_Agent_System.DTO
{
    public class UserInfo
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;

        public UserInfo(User user)
        {
            UserId = user.UserId;
            Username = user.Username;
            Email = user.Email;
            PhoneNumber = user.PhoneNumber;
            AvatarUrl = user.AvatarUrl;
        }
    }
}
