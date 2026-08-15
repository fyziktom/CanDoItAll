using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.LlmChats.Persistence.EntityConfigurations;

internal sealed class LlmChatConversationConfiguration : IEntityTypeConfiguration<LlmChatConversationRow>
{
    public void Configure(EntityTypeBuilder<LlmChatConversationRow> builder)
    {
        builder.ToTable("LlmChats_Conversations");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Title).HasMaxLength(LlmConversationDocument.MaximumTitleLength).IsRequired();
        builder.Property(row => row.ConcurrencyToken).IsConcurrencyToken();
        builder.HasOne<LlmChatDefinitionRevisionRow>()
            .WithMany()
            .HasForeignKey(row => new { row.DefinitionId, row.DefinitionRevision })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LlmChatTranscriptRow>()
            .WithOne()
            .HasForeignKey<LlmChatConversationRow>(row => row.Id)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.DefinitionId, row.UpdatedAtUtc });
        builder.HasIndex(row => new { row.Status, row.UpdatedAtUtc });
    }
}

internal sealed class LlmChatTranscriptConfiguration : IEntityTypeConfiguration<LlmChatTranscriptRow>
{
    public void Configure(EntityTypeBuilder<LlmChatTranscriptRow> builder)
    {
        builder.ToTable("LlmChats_Transcripts");
        builder.HasKey(row => row.ConversationId);
        builder.Property(row => row.ProviderName).HasMaxLength(LlmConversationProviderSnapshot.MaximumNameLength).IsRequired();
        builder.Property(row => row.Model).HasMaxLength(LlmConversationProviderSnapshot.MaximumModelLength).IsRequired();
        builder.Property(row => row.TranscriptRevision).IsConcurrencyToken();
        builder.Property(row => row.CompensationProviderName).HasMaxLength(LlmConversationProviderSnapshot.MaximumNameLength);
        builder.Property(row => row.CompensationModel).HasMaxLength(LlmConversationProviderSnapshot.MaximumModelLength);
        builder.Property(row => row.CompensationAccelerationStrategyId).HasMaxLength(LlmConversationAccelerationEnvelope.MaximumStrategyIdLength);
        builder.Property(row => row.CompensationAccelerationProviderName).HasMaxLength(LlmConversationProviderSnapshot.MaximumNameLength);
        builder.Property(row => row.CompensationAccelerationModel).HasMaxLength(LlmConversationProviderSnapshot.MaximumModelLength);
        builder.Property(row => row.AccelerationStrategyId).HasMaxLength(LlmConversationAccelerationEnvelope.MaximumStrategyIdLength);
        builder.Property(row => row.AccelerationProviderName).HasMaxLength(LlmConversationProviderSnapshot.MaximumNameLength);
        builder.Property(row => row.AccelerationModel).HasMaxLength(LlmConversationProviderSnapshot.MaximumModelLength);
    }
}

internal sealed class LlmChatMessageConfiguration : IEntityTypeConfiguration<LlmChatMessageRow>
{
    public void Configure(EntityTypeBuilder<LlmChatMessageRow> builder)
    {
        builder.ToTable("LlmChats_Messages");
        builder.HasKey(row => row.EntryId);
        builder.Property(row => row.Text).HasMaxLength(LlmConversationTranscriptEntry.MaximumTextLength).IsRequired();
        builder.Property(row => row.Model).HasMaxLength(LlmConversationTranscriptEntry.MaximumModelLength).IsRequired();
        builder.HasOne<LlmChatTranscriptRow>()
            .WithMany()
            .HasForeignKey(row => row.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(row => new { row.ConversationId, row.Sequence }).IsUnique();
        builder.HasIndex(row => new { row.ConversationId, row.TurnId, row.Role }).IsUnique();
    }
}
