using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.LlmChats.Persistence.EntityConfigurations;

internal sealed class LlmChatOperationConfiguration : IEntityTypeConfiguration<LlmChatOperationRow>
{
    public void Configure(EntityTypeBuilder<LlmChatOperationRow> builder)
    {
        builder.ToTable("LlmChats_Operations");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(row => row.FailureCode).HasMaxLength(200).IsRequired();
        builder.Property(row => row.ConcurrencyToken).IsConcurrencyToken();
        builder.HasOne<LlmChatConversationRow>()
            .WithMany()
            .HasForeignKey(row => row.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.ConversationId, row.StartedAtUtc });
        builder.HasIndex(row => new { row.Status, row.StartedAtUtc });
    }
}

internal sealed class LlmChatInvocationRecordConfiguration : IEntityTypeConfiguration<LlmChatInvocationRecordRow>
{
    public void Configure(EntityTypeBuilder<LlmChatInvocationRecordRow> builder)
    {
        builder.ToTable("LlmChats_InvocationRecords");
        builder.HasKey(row => new { row.OperationId, row.Ordinal });
        builder.Property(row => row.ProviderName).HasMaxLength(LlmConversationProviderSnapshot.MaximumNameLength).IsRequired();
        builder.Property(row => row.Model).HasMaxLength(LlmConversationProviderSnapshot.MaximumModelLength).IsRequired();
        builder.Property(row => row.FailureCode).HasMaxLength(200).IsRequired();
        builder.Property(row => row.CorrelationId).HasMaxLength(LlmInvocationRequest.MaximumCorrelationIdLength).IsRequired();
        builder.HasOne<LlmChatOperationRow>()
            .WithMany()
            .HasForeignKey(row => row.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.ProviderProfileId, row.StartedAtUtc });
    }
}
