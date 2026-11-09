using CKKS_Agent_System.DTO;
using CKKS_Agent_System.Enum;
using Microsoft.EntityFrameworkCore;

namespace CKKS_Agent_System.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // 继续执行后续流程
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "发生未处理异常。TraceId: {TraceId}; Method: {Method}; Path: {Path}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path);

            // 响应已经发送时，不再更改状态码和响应体
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "响应已开始写入，无法返回统一异常响应。TraceId: {TraceId}",
                    context.TraceIdentifier);

                throw;
            }

            await WriteResponseAsync(context, ex);
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, Exception exception)
    {
        var (httpStatusCode, response) = exception switch
        {
            // 参数异常可安全地提示给调用方
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                new ApiResponse(
                    false,
                    exception.Message,
                    code: ResponseCode.InvalidParameter)
            ),

            // EF Core 数据库异常：对外不暴露数据库详情
            DbUpdateException => (
                StatusCodes.Status500InternalServerError,
                new ApiResponse(
                    false,
                    "数据保存失败，请稍后重试",
                    code: ResponseCode.DatabaseError)
            ),

            // 其他异常，包括配置错误、Redis/邮件/短信服务异常等
            _ => (
                StatusCodes.Status500InternalServerError,
                new ApiResponse(
                    false,
                    "服务器内部错误，请稍后重试",
                    code: ResponseCode.InternalError)
            )
        };

        context.Response.Clear();
        context.Response.StatusCode = httpStatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(response);
    }
}