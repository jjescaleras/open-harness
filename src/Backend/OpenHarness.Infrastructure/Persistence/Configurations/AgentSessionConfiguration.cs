using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence.Configurations;

public sealed class AgentSessionConfiguration : IEntityTypeConfiguration<AgentSession>
{
    public void Configure(EntityTypeBuilder<AgentSession> builder)
    {
        builder.ToTable("agent_sessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.OwnsOne(x => x.ModelConfig, b => b.ToJson("model_config"));

        builder.HasOne(x => x.User)
            .WithMany(u => u.AgentSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkspacePolicy)
            .WithMany(w => w.AgentSessions)
            .HasForeignKey(x => x.WorkspacePolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ParentSession)
            .WithMany(s => s.ChildSessions)
            .HasForeignKey(x => x.ParentSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
