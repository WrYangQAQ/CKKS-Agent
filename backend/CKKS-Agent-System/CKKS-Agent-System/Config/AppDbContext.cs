using CKKS_Agent_System.Entity;
using Microsoft.EntityFrameworkCore;

namespace CKKS_Agent_System.Config
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- User 配置 ----------
            modelBuilder.Entity<User>(entity =>
            {
                // 雪花ID由代码生成，不让数据库自增
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.UserId).ValueGeneratedNever();

                entity.Property(u => u.Username).IsRequired().HasMaxLength(64);
                
                // 用户名唯一
                entity.HasIndex(u => u.Username).IsUnique();
                
                // 邮箱、手机号允许为空，仅非空值参与唯一约束
                entity.HasIndex(u => u.Email)
                      .IsUnique()
                      .HasFilter("[Email] IS NOT NULL");
                entity.HasIndex(u => u.PhoneNumber)
                      .IsUnique()
                      .HasFilter("[PhoneNumber] IS NOT NULL");

                entity.Property(u => u.Email).HasMaxLength(128);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(128);
            });

            // ---------- RefreshToken 配置 ----------
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Id).ValueGeneratedNever();

                entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);

                // 关键索引 Token 唯一。
                // 每次校验 RefreshToken 都是「按 Token 字符串查一条记录」，
                // 唯一索引既加速查询，又杜绝并发下同一字符串写入两条。
                entity.HasIndex(t => t.TokenHash).IsUnique();

                // 关键索引 UserId 普通索引。
                // 登出全部设备 / 改密码时，需要按用户批量作废 token。
                entity.HasIndex(t => t.UserId);

                // 多对一：一个用户可有多条 RefreshToken（多设备登录）
                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // 用户删除时级联删除其 token
            });
        }
    }
}
