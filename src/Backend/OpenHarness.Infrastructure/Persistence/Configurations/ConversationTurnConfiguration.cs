using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence.Configurations;

public sealed class ConversationTurnConfiguration : IEntityTypeConfiguration<ConversationTurn>
{
    public void Configure(EntityTypeBuilder<ConversationTurn> builder)
    {
        builder.ToTable("conversation_turns");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.Content)
            .IsRequired();

        builder.HasIndex(x => new { x.SessionId, x.SequenceNumber })
            .IsUnique();

        builder.OwnsOne(x => x.TokenUsage, b => b.ToJson("token_usage"));

        builder.Property(x => x.Embedding)
            .HasColumnType("vector(1536)");

        builder.HasIndex(x => x.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        builder.HasOne(x => x.Session)
            .WithMany(s => s.Turns)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
