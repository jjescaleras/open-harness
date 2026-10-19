using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence.Configurations;

public sealed class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("tool_executions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CallId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ToolName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(x => x.Arguments)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Result)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.Arguments)
            .HasMethod("gin")
            .HasOperators("jsonb_path_ops");

        builder.HasIndex(x => x.Result)
            .HasMethod("gin")
            .HasOperators("jsonb_path_ops");

        builder.HasOne(x => x.Session)
            .WithMany(s => s.ToolExecutions)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Turn)
            .WithMany(t => t.ToolExecutions)
            .HasForeignKey(x => x.TurnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
