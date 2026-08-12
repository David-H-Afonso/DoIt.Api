using System.Text.Json;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Middleware;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = GetRequestId(context);
        context.Response.Headers["X-Request-Id"] = requestId;
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.ToString()
        });

        try
        {
            await next(context);
        }
        catch (ApiException exception)
        {
            logger.LogWarning(
                "API request rejected. RequestId={RequestId} Method={Method} Path={Path} StatusCode={StatusCode} ErrorCode={ErrorCode}",
                requestId,
                context.Request.Method,
                context.Request.Path,
                exception.StatusCode,
                exception.Code);
            await WriteErrorAsync(context, exception.StatusCode, exception.Code, exception.Message, GetCategory(exception.StatusCode), requestId);
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(
                "API request failed validation. RequestId={RequestId} Method={Method} Path={Path}",
                requestId,
                context.Request.Method,
                context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "validation_error", exception.Message, "validation", requestId);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                exception,
                "API request hit a concurrent update. RequestId={RequestId} Method={Method} Path={Path}",
                requestId,
                context.Request.Method,
                context.Request.Path);
            await WriteErrorAsync(
                context,
                StatusCodes.Status409Conflict,
                "data_changed",
                "The data changed while you were saving. Reload the task and try again.",
                "conflict",
                requestId);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Database rejected an API write. RequestId={RequestId} Method={Method} Path={Path}",
                requestId,
                context.Request.Method,
                context.Request.Path);
            await WriteErrorAsync(
                context,
                StatusCodes.Status409Conflict,
                "data_conflict",
                "We could not save that change because the data conflicts with another change. Reload and try again.",
                "data",
                requestId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "API request failed unexpectedly. RequestId={RequestId} Method={Method} Path={Path}",
                requestId,
                context.Request.Method,
                context.Request.Path);
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "server_error",
                $"We could not complete that action. Try again. If it continues, provide support code {requestId}.",
                "server",
                requestId);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message, string category, string requestId)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers["X-Request-Id"] = requestId;
        await JsonSerializer.SerializeAsync(context.Response.Body, new ErrorResponse(code, message, category, requestId), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string GetRequestId(HttpContext context)
    {
        var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(requestId) && requestId.Length <= 64
            ? requestId
            : Guid.NewGuid().ToString("N");
    }

    private static string GetCategory(int statusCode) => statusCode switch
    {
        >= 400 and < 500 when statusCode == StatusCodes.Status401Unauthorized => "authentication",
        >= 400 and < 500 when statusCode == StatusCodes.Status403Forbidden => "authorization",
        >= 400 and < 500 when statusCode == StatusCodes.Status404NotFound => "not_found",
        >= 400 and < 500 when statusCode == StatusCodes.Status409Conflict => "conflict",
        >= 400 and < 500 when statusCode == StatusCodes.Status429TooManyRequests => "rate_limit",
        >= 400 and < 500 => "validation",
        _ => "server"
    };
}
