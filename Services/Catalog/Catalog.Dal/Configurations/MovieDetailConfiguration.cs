using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class MovieDetailConfiguration : IEntityTypeConfiguration<MovieDetail>
{
    public void Configure(EntityTypeBuilder<MovieDetail> builder)
    {
        builder.ToTable("MovieDetails", t =>
            t.HasCheckConstraint("CK_MovieDetails_TrailerUrl", "[TrailerUrl] IS NULL OR [TrailerUrl] LIKE 'https://%'"));
        builder.HasKey(d => d.MovieId);
        builder.Property(d => d.MovieId).ValueGeneratedNever();
        builder.Property(d => d.Description).IsRequired().HasMaxLength(2000);
        builder.Property(d => d.Director).HasMaxLength(200);
        builder.Property(d => d.Country).HasMaxLength(100);
        builder.Property(d => d.TrailerUrl).HasMaxLength(500);
        builder.Property(d => d.PosterUrl).HasMaxLength(500);
    }
}
