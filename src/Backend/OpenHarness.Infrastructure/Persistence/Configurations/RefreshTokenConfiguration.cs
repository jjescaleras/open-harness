using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ReplacedByTokenHash)
            .HasMaxLength(64);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => x.FamilyId);

        builder.HasOne(x => x.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
