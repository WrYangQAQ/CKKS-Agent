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
        public async Task<User?> GetUserByUsername(string username)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        // 根据手机号查找对应用户
        public async Task<User?> GetUserByPhoneNumber(string phoneNumber)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }

        // 根据邮箱查找对应用户
        public async Task<User?> GetUserByEmail(string email)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        // 添加用户对象到数据库中（注册）
        public async Task<bool> AddUser(User user)
        {
            _dbContext.Users.Add(user);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 从对象库中删除用户
        public async Task<bool> DeleteUser(User user)
        {
            _dbContext.Users.Remove(user);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 更新用户对象到数据库中
        public async Task<bool> UpdateUser(User user)
        {
            _dbContext.Users.Update(user);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 根据用户ID查找对应用户
        public async Task<User?> GetUserById(long userId)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
