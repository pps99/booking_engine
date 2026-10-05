using Microsoft.AspNetCore.Mvc;
using Rezerv.BookingEngine.Application;

namespace Rezerv.BookingEngine.Api;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BookingEngineException exception)
        {
            context.Response.StatusCode = exception.StatusCode;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = exception.StatusCode,
                Title = exception.Code,
                Detail = exception.Message,
                Instance = context.Request.Path
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error for {Method} {Path}.",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "internal_error",
                Detail = "An unexpected error occurred.",
                Instance = context.Request.Path
            });
        }
    }
}

