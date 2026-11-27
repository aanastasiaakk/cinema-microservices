using System.Diagnostics;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;

namespace Catalog.Api.Metrics;

/// <summary>Декоратори BLL каталогу: тривалість усіх операцій (успішних і невдалих) + бізнес-лічильники після успіху.</summary>
public sealed class MeteredMovieService(IMovieService inner, CatalogMetrics metrics) : IMovieService
{
    public Task<PagedResult<MovieListItemDto>> GetListAsync(MovieQuery query, CancellationToken ct) => M("movie.list", () => inner.GetListAsync(query, ct));
    public Task<MovieDto> GetByIdAsync(long id, CancellationToken ct) => M("movie.get", () => inner.GetByIdAsync(id, ct));

    public async Task<MovieDto> CreateAsync(CreateMovieDto input, CancellationToken ct)
    {
        var movie = await M("movie.create", () => inner.CreateAsync(input, ct));
        metrics.MovieCreated(movie.AgeRating);
        return movie;
    }

    public Task ChangePriceAsync(long id, decimal price, CancellationToken ct) => Change("price", () => inner.ChangePriceAsync(id, price, ct));
    public Task DeactivateAsync(long id, CancellationToken ct) => Change("deactivated", () => inner.DeactivateAsync(id, ct));
    public Task ActivateAsync(long id, CancellationToken ct) => Change("activated", () => inner.ActivateAsync(id, ct));

    public async Task<MovieDto> AddGenreAsync(long id, AddMovieGenreDto input, CancellationToken ct)
    {
        var movie = await M("movie.genres", () => inner.AddGenreAsync(id, input, ct));
        metrics.MovieUpdated("genres");
        return movie;
    }

    public Task RemoveGenreAsync(long id, int genreId, CancellationToken ct) => Change("genres", () => inner.RemoveGenreAsync(id, genreId, ct));
    public Task DeleteAsync(long id, CancellationToken ct) => Change("deleted", () => inner.DeleteAsync(id, ct));

    private async Task Change(string change, Func<Task> call)
    {
        await M($"movie.{change}", async () => { await call(); return true; });
        metrics.MovieUpdated(change);
    }

    private Task<T> M<T>(string operation, Func<Task<T>> call) => Timing.MeasureAsync(metrics, operation, call);
}

public sealed class MeteredShowtimeService(IShowtimeService inner, CatalogMetrics metrics) : IShowtimeService
{
    public Task<PagedResult<ShowtimeDto>> GetListAsync(ShowtimeQuery query, CancellationToken ct)
        => Timing.MeasureAsync(metrics, "showtime.list", () => inner.GetListAsync(query, ct));
    public Task<ShowtimeDto> GetByIdAsync(long id, CancellationToken ct)
        => Timing.MeasureAsync(metrics, "showtime.get", () => inner.GetByIdAsync(id, ct));

    public async Task<ShowtimeDto> CreateAsync(CreateShowtimeDto input, CancellationToken ct)
    {
        var showtime = await Timing.MeasureAsync(metrics, "showtime.schedule", () => inner.CreateAsync(input, ct));
        metrics.ShowtimeScheduled(showtime.Language);
        return showtime;
    }

    public async Task CancelAsync(long id, CancellationToken ct)
    {
        await Timing.MeasureAsync(metrics, "showtime.cancel", async () => { await inner.CancelAsync(id, ct); return true; });
        metrics.ShowtimeCancelled();
    }

    public Task<IReadOnlyList<HallDto>> GetHallsAsync(CancellationToken ct) => Timing.MeasureAsync(metrics, "hall.list", () => inner.GetHallsAsync(ct));
}

internal static class Timing
{
    public static async Task<T> MeasureAsync<T>(CatalogMetrics metrics, string operation, Func<Task<T>> call)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            metrics.RecordDuration(operation, success: true, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch
        {
            metrics.RecordDuration(operation, success: false, sw.Elapsed.TotalMilliseconds);   // і для невдалих
            throw;
        }
    }
}
