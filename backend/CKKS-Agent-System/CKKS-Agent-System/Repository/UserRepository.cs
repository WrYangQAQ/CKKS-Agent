using CKKS_Agent_System.Config;
using CKKS_Agent_System.Entity;
using Microsoft.EntityFrameworkCore;

namespace CKKS_Agent_System.Repository
{
    public class UserRepository
    {
        private readonly AppDbContext _dbContext;

        public UserRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // 根据用户名查找对应用户
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        // 根据手机号查找对应用户
        public async Task<User?> GetUserByPhoneNumberAsync(string phoneNumber)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }

        // 根据邮箱查找对应用户
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        // 添加用户对象到数据库中（注册）
        public async Task<bool> AddUserAsync(User user)
        {
            _dbContext.Users.Add(user);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 从对象库中删除用户
        public async Task<bool> DeleteUserAsync(User user)
        {
            _dbContext.Users.Remove(user);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 更新用户对象到数据库中
        public async Task<bool> UpdateUserAsync(User user)
        {
            _dbContext.Users.Update(user);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 根据用户ID查找对应用户
        public async Task<User?> GetUserByIdAsync(long userId)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
