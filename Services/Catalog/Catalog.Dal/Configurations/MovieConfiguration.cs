using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("Movies", t =>
        {
            t.HasCheckConstraint("CK_Movies_ReleaseYear", "[ReleaseYear] BETWEEN 1895 AND 2100");
            t.HasCheckConstraint("CK_Movies_DurationMin", "[DurationMin] BETWEEN 1 AND 600");
            t.HasCheckConstraint("CK_Movies_AgeRating", "[AgeRating] IN ('0+', '6+', '12+', '16+', '18+')");
            t.HasCheckConstraint("CK_Movies_BasePrice", "[BasePrice] > 0");
            t.HasCheckConstraint("CK_Movies_AverageRating", "[AverageRating] IS NULL OR [AverageRating] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Movies_ReviewsCount", "[ReviewsCount] >= 0");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(200);
        builder.Property(m => m.AgeRating).IsRequired().HasMaxLength(5).IsUnicode(false).HasDefaultValue("0+");
        builder.Property(m => m.BasePrice).HasColumnType("decimal(18,2)");
        builder.Property(m => m.AverageRating).HasColumnType("decimal(3,2)");
        builder.Property(m => m.ReviewsCount).HasDefaultValue(0);
        builder.Property(m => m.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()")
               .ValueGeneratedOnAdd();
        builder.Property(m => m.UpdatedAt).HasColumnType("datetime2(3)");
        builder.Property(m => m.RowVersion).IsRowVersion();          // оптимістична конкурентність

        // «чи вже є такий фільм» + пошук за назвою
        builder.HasIndex(m => new { m.Title, m.ReleaseYear }).IsUnique();
        // афіша: активні фільми за рейтингом
        builder.HasIndex(m => m.AverageRating)
               .IsDescending()
               .HasFilter("[IsActive] = 1")
               .IncludeProperties(m => new { m.Title, m.AgeRating, m.BasePrice })
               .HasDatabaseName("IX_Movies_Active_Rating");

        // 1:1 Movie ↔ MovieDetail: PK MovieDetail є водночас FK
        builder.HasOne(m => m.Detail)
               .WithOne()
               .HasForeignKey<MovieDetail>(d => d.MovieId)
               .OnDelete(DeleteBehavior.Cascade);

        // 1:N Movie → Showtimes: фільм із сеансами не видаляється
        builder.HasMany(m => m.Showtimes)
               .WithOne(s => s.Movie)
               .HasForeignKey(s => s.MovieId)
               .OnDelete(DeleteBehavior.Restrict);

        // колекції з приватним полем-доступом не потрібні: List<> без setter-а, EF пише в нього напряму
        builder.Navigation(m => m.MovieGenres).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
