using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("Genres", t =>
            t.HasCheckConstraint("CK_Genres_Slug", "[Slug] NOT LIKE '%[^a-z0-9-]%'"));
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(50);
        builder.Property(g => g.Slug).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(g => g.Name).IsUnique();
        builder.HasIndex(g => g.Slug).IsUnique();

        // Статичний (модельний) сідінг довідника з ФІКСОВАНИМИ Id — потрапляє в міграцію
        builder.HasData(
            Genre.Seed(1, "Драма", "drama"),
            Genre.Seed(2, "Пригоди", "adventure"),
            Genre.Seed(3, "Анімація", "animation"),
            Genre.Seed(4, "Фантастика", "sci-fi"),
            Genre.Seed(5, "Історичний", "historical"),
            Genre.Seed(6, "Сімейний", "family"));
    }
}
