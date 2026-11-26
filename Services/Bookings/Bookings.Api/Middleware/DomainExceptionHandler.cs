using Bookings.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Api.Middleware;

/// <summary>
/// ЄДИНИЙ глобальний обробник винятків: перетворює доменні винятки на HTTP-статуси
/// у форматі ProblemDetails (RFC 9457). Контролери не містять try/catch.
/// Ланцюжок: THROW 50002 у процедурі → SqlErrorMapper (DAL) → BusinessConflictException → 409.
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails,
                                           ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            BusinessConflictException => (StatusCodes.Status409Conflict, "Business rule conflict"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad request"),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested
                => (499, "Client closed request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status >= 500)
            logger.LogError(ex, "Unhandled exception");   // деталі — лише в лог
        else
            logger.LogInformation("{Exception}: {Message}", ex.GetType().Name, ex.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // повідомлення доменних винятків безпечні; для 500 сирий текст клієнту не віддаємо
            Detail = ex is DomainException ? ex.Message : null,
            Instance = http.Request.Path
        };
        if (ex is ValidationException { Errors.Count: > 0 } validation)
            problem.Extensions["errors"] = validation.Errors;

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = ex
        });
    }
}
