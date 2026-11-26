using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

/// <summary>M:N Movie ↔ Genre через явну проміжну сутність зі складеним ключем і власними полями.</summary>
public sealed class MovieGenreConfiguration : IEntityTypeConfiguration<MovieGenre>
{
    public void Configure(EntityTypeBuilder<MovieGenre> builder)
    {
        builder.ToTable("MovieGenres");
        builder.HasKey(mg => new { mg.MovieId, mg.GenreId });                 // складений ключ
        builder.Property(mg => mg.AddedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(mg => mg.Movie).WithMany(m => m.MovieGenres)
               .HasForeignKey(mg => mg.MovieId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(mg => mg.Genre).WithMany(g => g.MovieGenres)
               .HasForeignKey(mg => mg.GenreId)
               .OnDelete(DeleteBehavior.Restrict);                           // жанр із фільмами не видаляється

        // «усі фільми жанру» (зворотний напрямок, PK покриває лише MovieId → GenreId)
        builder.HasIndex(mg => mg.GenreId).IncludeProperties(mg => mg.AddedAt);
        // правило: у фільму не більше одного основного жанру
        builder.HasIndex(mg => mg.MovieId).IsUnique().HasFilter("[IsPrimary] = 1")
               .HasDatabaseName("IX_MovieGenres_MovieId_Primary");
    }
}
