using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Platform.Aggregator.Resilience;

/// <summary>
/// Параметри resilience одного downstream-сервісу. Обґрунтування — у README (розділ 16.2).
/// Узгодженість таймаутів: TotalTimeout ≥ AttemptTimeout × (Retries + 1) + сума затримок;
/// SamplingDuration ≥ 2 × AttemptTimeout (інакше валідація опцій Polly не пропустить).
/// </summary>
public sealed record ServicePolicy(
    string Service,
    int HttpRetries,
    TimeSpan RetryDelay,
    TimeSpan AttemptTimeout,
    TimeSpan TotalTimeout,
    double FailureRatio,
    int MinimumThroughput,
    TimeSpan SamplingDuration,
    TimeSpan BreakDuration,
    int GrpcMaxAttempts)
{
    /// <summary>
    /// КРИТИЧНИЙ: catalog-api — без сеансів і фільму картка не має сенсу. Більше повторів (тимчасові збої
    /// поглинаються), ЧУТЛИВІШИЙ breaker (30 % збоїв) — швидко перестаємо «добивати» хворий сервіс.
    /// </summary>
    public static readonly ServicePolicy Catalog = new("catalog-api",
        HttpRetries: 3, RetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromSeconds(1.5), TotalTimeout: TimeSpan.FromSeconds(8),
        FailureRatio: 0.3, MinimumThroughput: 5, SamplingDuration: TimeSpan.FromSeconds(20), BreakDuration: TimeSpan.FromSeconds(10),
        GrpcMaxAttempts: 4);

    /// <summary>
    /// КРИТИЧНИЙ (корінь картки): bookings-api — без бронювання відповідь 503. Повтори є, але трохи менше,
    /// ніж для каталогу: один виклик на картку, а таймаут спроби довший (запит із JOIN-ами до SQL).
    /// </summary>
    public static readonly ServicePolicy Bookings = new("bookings-api",
        HttpRetries: 2, RetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromSeconds(2), TotalTimeout: TimeSpan.FromSeconds(8),
        FailureRatio: 0.5, MinimumThroughput: 5, SamplingDuration: TimeSpan.FromSeconds(20), BreakDuration: TimeSpan.FromSeconds(10),
        GrpcMaxAttempts: 3);

    /// <summary>
    /// НЕКРИТИЧНИЙ: reviews-api — без відгуків Aggregator однаково повертає часткову картку з warning.
    /// Швидко здаємося (1 повтор, короткі таймаути), breaker толерантний (70 %), але довго розімкнений —
    /// не витрачаємо час користувача на сервіс, без якого можна обійтися.
    /// </summary>
    public static readonly ServicePolicy Reviews = new("reviews-api",
        HttpRetries: 1, RetryDelay: TimeSpan.FromMilliseconds(100),
        AttemptTimeout: TimeSpan.FromSeconds(1), TotalTimeout: TimeSpan.FromSeconds(2.5),
        FailureRatio: 0.7, MinimumThroughput: 5, SamplingDuration: TimeSpan.FromSeconds(10), BreakDuration: TimeSpan.FromSeconds(30),
        GrpcMaxAttempts: 2);
}

public static class ResilienceExtensions
{
    /// <summary>
    /// Замінює стандартний обробник із ServiceDefaults на власний для цього клієнта: СПЕРШУ
    /// RemoveAllResilienceHandlers (без нашарування двох pipeline-ів), потім AddStandardResilienceHandler
    /// з параметрами політики. Retry — експоненційний з jitter і лише для безпечних методів (GET):
    /// POST/PUT/DELETE не повторюються. Переходи circuit breaker логуються (OnOpened — Error).
    /// </summary>
    public static IHttpClientBuilder AddServiceResilience(this IHttpClientBuilder builder, ServicePolicy policy)
    {
        builder.RemoveAllResilienceHandlers();
        builder.AddStandardResilienceHandler().Configure((o, sp) =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger($"Resilience.{policy.Service}");

            o.Retry.MaxRetryAttempts = policy.HttpRetries;
            o.Retry.Delay = policy.RetryDelay;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
            o.Retry.UseJitter = true;
            o.Retry.DisableForUnsafeHttpMethods();             // gRPC-виклики — POST: їх повторює ServiceConfig клієнта
            o.Retry.OnRetry = args =>
            {
                logger.LogWarning("Retry {AttemptNumber} to {Service} in {RetryDelayMs} ms: {Reason}",
                    args.AttemptNumber + 1, policy.Service, args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.Message ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
                return ValueTask.CompletedTask;
            };

            o.AttemptTimeout.Timeout = policy.AttemptTimeout;
            o.TotalRequestTimeout.Timeout = policy.TotalTimeout;

            o.CircuitBreaker.FailureRatio = policy.FailureRatio;
            o.CircuitBreaker.MinimumThroughput = policy.MinimumThroughput;
            o.CircuitBreaker.SamplingDuration = policy.SamplingDuration;
            o.CircuitBreaker.BreakDuration = policy.BreakDuration;
            o.CircuitBreaker.OnOpened = args =>
            {
                logger.LogError("Circuit OPENED for {Service} for {BreakDurationSec} s: {Reason}", policy.Service,
                    args.BreakDuration.TotalSeconds, args.Outcome.Exception?.Message ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnHalfOpened = _ =>
            {
                logger.LogWarning("Circuit HALF-OPEN for {Service}: probing with a trial request", policy.Service);
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnClosed = _ =>
            {
                logger.LogInformation("Circuit CLOSED for {Service}: calls flow normally again", policy.Service);
                return ValueTask.CompletedTask;
            };
        });
        return builder;
    }

    /// <summary>
    /// gRPC повертає помилки (Unavailable, ResourceExhausted…) у трейлерах із HTTP 200 — HTTP-обробник їх не
    /// бачить. Тому для gRPC-клієнтів повтори за gRPC-статусами задає ServiceConfig каналу.
    /// </summary>
    public static IHttpClientBuilder AddGrpcRetryPolicy(this IHttpClientBuilder builder, ServicePolicy policy)
        => builder.ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = policy.GrpcMaxAttempts,
                        InitialBackoff = policy.RetryDelay,
                        MaxBackoff = TimeSpan.FromSeconds(2),
                        BackoffMultiplier = 2,
                        RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted, StatusCode.Aborted }
                    }
                }
            }
        });
}
