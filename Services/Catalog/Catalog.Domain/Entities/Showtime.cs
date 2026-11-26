using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>Сеанс: 1:N від Movie і від Hall. Id сеансів 1–4 збігаються з копією в CinemaBooking.</summary>
public class Showtime
{
    public static readonly IReadOnlyList<string> Languages = ["uk", "en"];

    public long Id { get; private set; }
    public long MovieId { get; private set; }
    public Movie Movie { get; private set; } = null!;
    public int HallId { get; private set; }
    public Hall Hall { get; private set; } = null!;
    public DateTime StartsAt { get; private set; }
    public decimal Price { get; private set; }
    public string Language { get; private set; } = "uk";
    public bool IsCancelled { get; private set; }

    private Showtime() { }   // для EF Core

    /// <summary>Створення сеансу. Правила: фільм активний, ціна &gt; 0, мова зі списку, початок у майбутньому.</summary>
    public Showtime(Movie movie, int hallId, DateTime startsAt, decimal price, string language, DateTime nowUtc)
    {
        if (!movie.IsActive) throw new DomainException("Cannot schedule an inactive movie");
        if (startsAt <= nowUtc) throw new DomainException("Showtime must start in the future");
        if (!Languages.Contains(language)) throw new DomainException("Language must be 'uk' or 'en'");
        Movie = movie;
        MovieId = movie.Id;
        HallId = hallId;
        StartsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Unspecified).AddTicks(-(startsAt.Ticks % TimeSpan.TicksPerSecond));
        Language = language;
        ChangePrice(price);
    }

    public void ChangePrice(decimal price)
    {
        if (IsCancelled) throw new DomainException("Cannot change price of a cancelled showtime");
        if (price <= 0) throw new DomainException("Price must be positive");
        Price = price;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (IsCancelled) throw new DomainException("Showtime is already cancelled");
        if (StartsAt <= nowUtc) throw new DomainException("Cannot cancel a showtime that has already started");
        IsCancelled = true;
    }
}
