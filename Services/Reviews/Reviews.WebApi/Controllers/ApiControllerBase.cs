using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Reviews.WebApi.Controllers;

/// <summary>
/// Базовий контролер: доступ лише до ISender (MediatR). Контролери нічого не знають про MongoDB —
/// вони перетворюють HTTP-запит на команду/запит і повертають результат.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;
    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>If-Match: "3" → 3 (очікувана версія документа для оптимістичної конкурентності).</summary>
    protected int? IfMatchVersion()
    {
        var raw = Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Replace("W/", "").Trim('"', ' ');
        return int.TryParse(trimmed, out var v) ? v : throw new BadHttpRequestException("If-Match must contain a document version, e.g. \"2\"");
    }

    protected void SetETag(int version) => Response.Headers.ETag = $"\"{version}\"";
}
