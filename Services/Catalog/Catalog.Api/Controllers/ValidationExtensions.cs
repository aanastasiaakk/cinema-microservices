using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

internal static class ValidationExtensions
{
    /// <summary>Явний виклик FluentValidation; null — якщо все гаразд, інакше 400 ValidationProblemDetails.</summary>
    public static async Task<ActionResult?> ValidateAsync<T>(this ControllerBase controller, IValidator<T> validator,
                                                            T input, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(input, ct);
        if (result.IsValid) return null;
        // помилки → ModelState → ValidationProblem(...): той самий формат ValidationProblemDetails
        // (type, title, status, errors, traceId), що й для DataAnnotations
        foreach (var error in result.Errors)
            controller.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return controller.ValidationProblem(controller.ModelState);
    }

    /// <summary>Метадані пагінації дублюються в заголовках (зручно для кешування й клієнтів).</summary>
    public static void AddPaginationHeaders<T>(this HttpResponse response, Catalog.Bll.Dto.PagedResult<T> page)
    {
        response.Headers["X-Total-Count"] = page.TotalCount.ToString();
        response.Headers["X-Total-Pages"] = page.TotalPages.ToString();
    }
}
