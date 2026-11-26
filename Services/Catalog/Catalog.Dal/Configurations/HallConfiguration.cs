using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        builder.ToTable("Halls", t =>
        {
            t.HasCheckConstraint("CK_Halls_Rows", "[Rows] BETWEEN 1 AND 50");
            t.HasCheckConstraint("CK_Halls_SeatsPerRow", "[SeatsPerRow] BETWEEN 1 AND 60");
            t.HasCheckConstraint("CK_Halls_Format", "[Format] IN ('2D', '3D', 'IMAX', '4DX')");
        });
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).IsRequired().HasMaxLength(100);
        builder.Property(h => h.Format).IsRequired().HasMaxLength(10).IsUnicode(false).HasDefaultValue("2D");
        builder.Property(h => h.Capacity).HasComputedColumnSql("CAST([Rows] AS INT) * [SeatsPerRow]", stored: true);
        builder.HasIndex(h => h.Name).IsUnique();

        builder.HasMany(h => h.Showtimes)
               .WithOne(s => s.Hall)
               .HasForeignKey(s => s.HallId)
               .OnDelete(DeleteBehavior.Restrict);

        // довідник залів з фіксованими Id (HasData)
        builder.HasData(
            Hall.Seed(1, "Зал 1 (IMAX)", 10, 12, "IMAX"),
            Hall.Seed(2, "Зал 2", 8, 10, "2D"));
    }
}
