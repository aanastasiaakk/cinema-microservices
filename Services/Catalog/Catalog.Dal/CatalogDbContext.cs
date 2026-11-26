using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

/// <summary>DbContext каталогу (БД CinemaCatalog, Проєкт №2 з Лаб. 01).</summary>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<MovieDetail> MovieDetails => Set<MovieDetail>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();

    /// <summary>Усі IEntityTypeConfiguration&lt;T&gt; з цієї збірки підключаються ЦЕНТРАЛІЗОВАНО одним викликом.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
}
