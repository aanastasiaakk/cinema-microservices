using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Middleware;

/// <summary>
/// ЄДИНИЙ глобальний обробник винятків → ProblemDetails (RFC 9457). Контролери не містять try/catch.
/// Мапінг: EntityNotFound → 404, Validation → 400 (ValidationProblemDetails), Conflict/Domain → 409,
/// конфлікт конкурентності / порушення унікального індексу в БД → 409, інше → 500.
/// Кожен виняток логується ОДИН раз — тут. Деталі (stack trace) — лише в Development.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, IHostEnvironment env,
                                           ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title, safeDetail) = ex switch
        {
            EntityNotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Message),
            ValidationException e => (StatusCodes.Status400BadRequest, "One or more validation errors occurred.", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            DomainException e => (StatusCodes.Status409Conflict, "Business rule violated", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict",
                "The record was changed by another request; reload and try again"),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                (StatusCodes.Status409Conflict, "Conflict", "A record with the same unique key already exists"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad request", ex.Message),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested
                => (499, "Client closed request", null),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", null)
        };

        if (status >= 500)
            logger.LogError(ex, "Request {Method} {Path} failed", http.Request.Method, http.Request.Path);
        else
            logger.LogWarning("Request {Method} {Path} rejected with {StatusCode}: {ExceptionType} {Reason}",
                http.Request.Method, http.Request.Path, status, ex.GetType().Name, ex.Message);

        ProblemDetails problem = ex is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = safeDetail;
        problem.Instance = http.Request.Path;
        if (env.IsDevelopment() && status >= 500)
            problem.Extensions["exception"] = ex.ToString();           // деталі — лише в Development

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = ex
        });
    }
}
