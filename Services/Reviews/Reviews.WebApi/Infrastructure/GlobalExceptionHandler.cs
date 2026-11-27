using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi.Infrastructure;

/// <summary>
/// Єдиний глобальний обробник → ProblemDetails. Доменні й MongoDB-специфічні винятки:
/// NotFound → 404, ValidationException (FluentValidation) → 400 (ValidationProblemDetails з помилками по полях),
/// DomainException → 400, ConflictException (у т. ч. конфлікт версій) → 409,
/// MongoWriteException DuplicateKey → 409 (інші помилки запису → 400),
/// MongoConnectionException / недоступний сервер (TimeoutException вибору сервера) → 503, інше → 500.
/// Виняток логується один раз — тут.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, IHostEnvironment env,
                                           ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title, detail) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", ex.Message),
            ValidationException => (StatusCodes.Status400BadRequest, "One or more validation errors occurred.", null),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated", ex.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", ex.Message),
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } =>
                (StatusCodes.Status409Conflict, "Duplicate key", "A document with the same unique key already exists"),
            MongoWriteException => (StatusCodes.Status400BadRequest, "Write rejected by MongoDB", "The document was rejected by the database"),
            MongoConnectionException => (StatusCodes.Status503ServiceUnavailable, "Database unavailable", "MongoDB is not reachable, try again later"),
            TimeoutException when ex.Message.Contains("server", StringComparison.OrdinalIgnoreCase) =>
                (StatusCodes.Status503ServiceUnavailable, "Database unavailable", "MongoDB is not reachable, try again later"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad request", ex.Message),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested => (499, "Client closed request", null),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", null)
        };

        if (status >= 500)
            logger.LogError(ex, "Request {Method} {Path} failed with {StatusCode}", http.Request.Method, http.Request.Path, status);
        else
            logger.LogWarning("Request {Method} {Path} rejected with {StatusCode}: {ExceptionType} {Reason}",
                http.Request.Method, http.Request.Path, status, ex.GetType().Name, ex.Message);

        ProblemDetails problem = ex is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;
        problem.Instance = http.Request.Path;
        if (env.IsDevelopment() && status >= 500) problem.Extensions["exception"] = ex.ToString();

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem, Exception = ex });
    }
}
