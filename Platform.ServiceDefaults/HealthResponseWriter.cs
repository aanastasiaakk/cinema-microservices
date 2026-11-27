using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>JSON-деталі health checks: загальний статус, тривалість і кожна перевірка (назва, статус, теги, опис, помилка).</summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                tags = e.Value.Tags,
                error = e.Value.Exception?.Message,
                data = e.Value.Data.Count > 0 ? e.Value.Data : null
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
