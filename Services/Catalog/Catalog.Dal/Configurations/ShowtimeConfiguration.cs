using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class ShowtimeConfiguration : IEntityTypeConfiguration<Showtime>
{
    public void Configure(EntityTypeBuilder<Showtime> builder)
    {
        builder.ToTable("Showtimes", t =>
        {
            t.HasCheckConstraint("CK_Showtimes_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Showtimes_Language", "[Language] IN ('uk', 'en')");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StartsAt).HasColumnType("datetime2(0)");
        builder.Property(s => s.Price).HasColumnType("decimal(18,2)");
        builder.Property(s => s.Language).IsRequired().HasMaxLength(2).IsFixedLength().IsUnicode(false)
               .HasDefaultValue("uk");

        // правило: у залі не може бути двох сеансів в один час
        builder.HasIndex(s => new { s.HallId, s.StartsAt }).IsUnique();
        // розклад фільму: найближчі не скасовані сеанси
        builder.HasIndex(s => new { s.MovieId, s.StartsAt })
               .HasFilter("[IsCancelled] = 0")
               .IncludeProperties(s => new { s.HallId, s.Price });
    }
}
