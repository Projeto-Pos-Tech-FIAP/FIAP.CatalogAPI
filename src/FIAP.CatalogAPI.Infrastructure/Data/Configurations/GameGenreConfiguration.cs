using FIAP.CatalogAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIAP.CatalogAPI.Infrastructure.Data.Configurations;

internal class GameGenreConfiguration : IEntityTypeConfiguration<GameGenre>
{
    public void Configure(EntityTypeBuilder<GameGenre> builder)
    {
        builder.HasKey(gg => new { gg.GameId, gg.GenreId });
    }
}
