using FIAP.CatalogAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIAP.CatalogAPI.Infrastructure.Data.Configurations;

internal class LibraryConfiguration : IEntityTypeConfiguration<Library>
{
    public void Configure(EntityTypeBuilder<Library> builder)
    {
        builder.HasKey(l => l.LibraryId);

        builder.HasIndex(l => l.UserGuid).IsUnique();

        builder.HasMany(l => l.LibraryGames)
            .WithOne(lg => lg.Library)
            .HasForeignKey(lg => lg.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
