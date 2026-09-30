using GlobalPay.Api.Middleware;
using GlobalPay.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Errors;

internal sealed class GlobalPayExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalPayExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalPayExceptionHandler(
        ILogger<GlobalPayExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var (statusCode, code) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled domain failure {Code}", code);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrase(statusCode),
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Use the correlationId when contacting support."
                : exception.Message,
        };

        problemDetails.Extensions["code"] = code;

        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        return await _problemDetailsService
            .TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
            })
            .ConfigureAwait(false);
    }

    private static (int StatusCode, string Code) MapException(Exception exception) => exception switch
    {
        BadHttpRequestException badRequest => (badRequest.StatusCode, "request.bad_request"),
        DomainException domain => (StatusCodes.Status409Conflict, domain.Code ?? "domain.error"),
        ArgumentException => (StatusCodes.Status400BadRequest, "request.invalid_argument"),
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "auth.forbidden"),
        _ => (StatusCodes.Status500InternalServerError, "server.error"),
    };

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Internal Server Error",
    };
}
