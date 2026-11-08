using CKKS_Agent_System.Config;
using CKKS_Agent_System.Entity;
using Microsoft.EntityFrameworkCore;

namespace CKKS_Agent_System.Repository
{
    public class TokenRepository
    {
        private readonly AppDbContext _dbContext;

        public TokenRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // 保存 Refresh Token
        public async Task<bool> AddRefreshToken(RefreshToken token)
        {
            _dbContext.RefreshTokens.Add(token);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 修改 Refresh Token（例如撤销或轮换）
        public async Task<bool> UpdateRefreshToken(RefreshToken token)
        {
            _dbContext.RefreshTokens.Update(token);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 1;
        }

        // 刷新用户的 Refresh Token（注销旧的，颁发新的）
        public async Task<bool> RotateRefreshToken(RefreshToken oldToken, RefreshToken newToken)
        {
            // 1. 撤销旧的 Refresh Token
            _dbContext.RefreshTokens.Update(oldToken);

            // 2. 添加新的 Refresh Token
            _dbContext.RefreshTokens.Add(newToken);

            // 3. 保存更改
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == 2; // 应该有两条记录被修改（旧的撤销，新的添加）
        }

        // 按照传入的哈希值查找 Refresh Token
        public async Task<RefreshToken?> GetRefreshTokenByHash(string tokenHash)
        {
            return await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        // 批量更新 Refresh Token（例如批量撤销某个用户的所有 token）
        public async Task<bool> UpdateRefreshTokens(List<RefreshToken> tokens)
        {
            _dbContext.RefreshTokens.UpdateRange(tokens);
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == tokens.Count();
        }

        // 按照 userId 批量查询用户持有的 RefreshToken
        public async Task<List<RefreshToken>> SearchRefreshTokensByUserId(long userId)
        {
            return await _dbContext.RefreshTokens
                .Where(token => token.UserId == userId)
                .ToListAsync();
        }

        // 按照 userId 注销用户所有的 RefreshToken
        public async Task<bool> RevokeAllUserTokensAsync(long userId)
        {
            var tokens = await SearchRefreshTokensByUserId(userId);
            foreach (var token in tokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
            int affectedRows = await _dbContext.SaveChangesAsync();
            return affectedRows == tokens.Count;
        }

    }
}
