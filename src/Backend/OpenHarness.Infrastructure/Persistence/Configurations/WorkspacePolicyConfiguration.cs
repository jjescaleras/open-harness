using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence.Configurations;

public sealed class WorkspacePolicyConfiguration : IEntityTypeConfiguration<WorkspacePolicy>
{
    public void Configure(EntityTypeBuilder<WorkspacePolicy> builder)
    {
        builder.ToTable("workspace_policies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RootPath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(x => x.SandboxMode)
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(x => x.ApprovalPolicy)
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.OwnsOne(x => x.RulesConfig, b => b.ToJson("rules_config"));

        builder.HasOne(x => x.Owner)
            .WithMany(u => u.WorkspacePolicies)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
