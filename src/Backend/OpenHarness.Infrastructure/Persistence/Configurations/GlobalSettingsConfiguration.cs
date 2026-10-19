using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence.Configurations;

public sealed class GlobalSettingsConfiguration : IEntityTypeConfiguration<GlobalSettings>
{
    public void Configure(EntityTypeBuilder<GlobalSettings> builder)
    {
        builder.ToTable("global_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Scope)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => x.Scope)
            .IsUnique();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.OwnsOne(x => x.General, b =>
        {
            b.ToJson("general");
            b.Property(g => g.DefaultPermissionMode).HasConversion<string>();
            b.Property(g => g.Appearance).HasConversion<string>();
            b.Property(g => g.WorkDetails).HasConversion<string>();
            b.Property(g => g.CollapseWorkDetails).HasConversion<string>();
            b.Property(g => g.SendBehaviorWhileBusy).HasConversion<string>();
            b.Property(g => g.PerformanceAndUsage).HasConversion<string>();
        });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
