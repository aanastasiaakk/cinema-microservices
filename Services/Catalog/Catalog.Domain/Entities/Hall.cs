using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>Зал кінотеатру. Довідник з фіксованими Id (HasData). Capacity — обчислюване поле БД.</summary>
public class Hall
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public byte Rows { get; private set; }
    public byte SeatsPerRow { get; private set; }
    public string Format { get; private set; } = "2D";
    /// <summary>Rows * SeatsPerRow — рахує SQL Server (PERSISTED computed column).</summary>
    public int Capacity { get; private set; }

    public List<Showtime> Showtimes { get; } = [];                     // 1:N

    private Hall() { }   // для EF Core

    public Hall(string name, byte rows, byte seatsPerRow, string format)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Hall name is required");
        if (rows is < 1 or > 50) throw new DomainException("Rows must be between 1 and 50");
        if (seatsPerRow is < 1 or > 60) throw new DomainException("Seats per row must be between 1 and 60");
        if (!HallFormats.IsValid(format))
            throw new DomainException($"Format must be one of: {string.Join(", ", HallFormats.All)}");
        Name = name.Trim();
        Rows = rows;
        SeatsPerRow = seatsPerRow;
        Format = format;
    }

    public static Hall Seed(int id, string name, byte rows, byte seatsPerRow, string format) =>
        new(name, rows, seatsPerRow, format) { Id = id };
}
