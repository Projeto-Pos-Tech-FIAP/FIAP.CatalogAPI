using FIAP.CatalogAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIAP.CatalogAPI.Infrastructure.Data.Configurations;

internal class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(g => g.GameId);

        builder.Property(g => g.Title).IsRequired().HasMaxLength(200);
        builder.Property(g => g.BasePrice).HasColumnType("decimal(10,2)");

        builder.HasMany(g => g.GameGenres)
            .WithOne(gg => gg.Game)
            .HasForeignKey(gg => gg.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.LibraryGames)
            .WithOne(lg => lg.Game)
            .HasForeignKey(lg => lg.GameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
