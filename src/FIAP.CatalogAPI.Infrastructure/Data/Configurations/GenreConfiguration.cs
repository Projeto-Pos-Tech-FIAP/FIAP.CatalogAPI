using FIAP.CatalogAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIAP.CatalogAPI.Infrastructure.Data.Configurations;

internal class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.HasKey(g => g.GenreId);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);

        builder.HasMany(g => g.GameGenres)
            .WithOne(gg => gg.Genre)
            .HasForeignKey(gg => gg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
