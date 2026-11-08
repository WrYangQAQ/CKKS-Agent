using CKKS_Agent_System.Config;
using CKKS_Agent_System.Service;
using CKKS_Agent_System.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// 添加认证服务，并配置 JWT Bearer 方案
builder.Services.AddAuthentication(options =>
{
    // 将 JWT Bearer 设为默认的认证与质询方案
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSetting:Issuer"],
        ValidAudience = builder.Configuration["JwtSetting:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtSetting:SecretKey"]!)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // 自定义未授权和禁止访问的响应格式
    options.Events = new JwtBearerEvents
    {
        // 当用户未登录（没有 Token）或 Token 无效/过期时触发
        OnChallenge = context =>
        {
            // 阻止默认的响应体
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                Success = false,
                Message = "登录已过期或未授权，请重新登录",
                Code = 401 // 对应 ResponseCode.Unauthorized（HTTP Status 401）
            });

            return context.Response.WriteAsync(result);
        },

        // 当用户已登录，但没有权限访问该接口（比如权限不足）时触发
        OnForbidden = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                Success = false,
                Message = "您没有权限访问此资源",
                Code = 403 // 对应 ResponseCode.Forbidden（HTTP Status 403）
            });

            return context.Response.WriteAsync(result);
        }
    };
});

// 添加授权服务
builder.Services.AddAuthorization();

// 注册数据库服务
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 从配置中读取 JWT 设置并由框架注入到 JwtSetting 类中
builder.Services.Configure<JwtSetting>(
    builder.Configuration.GetSection(JwtSetting.SectionName));

// 注册 Redis 分布式缓存
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "CKKS_Agent_"; // 给缓存加个前缀，防止多个项目混用
});

// 注册自定义服务层
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TokenService>();

// 注册自定义仓储层
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<TokenRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 启用认证与授权中间件
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
