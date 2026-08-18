using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.EntityConfigurations;

internal sealed class LlmChatOperationConfiguration : IEntityTypeConfiguration<LlmChatOperationRow>
{
    public void Configure(EntityTypeBuilder<LlmChatOperationRow> builder)
    {
        builder.ToTable("LlmChats_Operations");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(row => row.AttributionScopeKey)
            .HasMaxLength(LlmChatOperation.MaximumAttributionScopeKeyLength)
            .IsRequired();
        builder.Property(row => row.FailureCode).HasMaxLength(200).IsRequired();
        builder.Property(row => row.LastEventSequence).IsRequired();
        builder.Property(row => row.ConcurrencyToken).IsConcurrencyToken();
        builder.HasOne<LlmChatConversationRow>()
            .WithMany()
            .HasForeignKey(row => row.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.ConversationId, row.StartedAtUtc });
        builder.HasIndex(row => new
        {
            row.AttributionScopeKind,
            row.AttributionScopeKey,
            row.StartedAtUtc
        });
        builder.HasIndex(row => new { row.Status, row.StartedAtUtc });
        builder.HasIndex(row => new { row.Status, row.LeaseExpiresAtUtc, row.StartedAtUtc });
        builder.HasIndex(row => new { row.Status, row.CompletedAtUtc });
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
        builder.Property(row => row.FinishReason)
            .HasMaxLength(LlmChatInvocationRecord.MaximumFinishReasonLength)
            .IsRequired();
        builder.Property(row => row.CorrelationId).HasMaxLength(LlmInvocationRequest.MaximumCorrelationIdLength).IsRequired();
        builder.Property(row => row.ProviderCostUsd).HasPrecision(18, 8);
        builder.Property(row => row.CalculatedCostUsd).HasPrecision(18, 8);
        builder.Property(row => row.PricingProfileHash)
            .HasMaxLength(ProviderPricingSnapshot.ProfileHashLength)
            .IsRequired();
        builder.Property(row => row.PricingVersion)
            .HasMaxLength(LlmChatInvocationRecord.MaximumPricingVersionLength)
            .IsRequired();
        builder.HasOne<LlmChatOperationRow>()
            .WithMany()
            .HasForeignKey(row => row.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.ProviderProfileId, row.StartedAtUtc });
    }
}

internal sealed class LlmChatOperationEventConfiguration : IEntityTypeConfiguration<LlmChatOperationEventRow>
{
    public void Configure(EntityTypeBuilder<LlmChatOperationEventRow> builder)
    {
        builder.ToTable("LlmChats_OperationEvents");
        builder.HasKey(row => new { row.OperationId, row.Sequence });
        builder.Property(row => row.Text)
            .HasMaxLength(LlmChatStreamingLimits.MaximumPersistedEventTextBytes)
            .IsRequired();
        builder.Property(row => row.Model)
            .HasMaxLength(LlmChatOperationStateChangedEvent.MaximumModelLength)
            .IsRequired();
        builder.Property(row => row.FailureCode)
            .HasMaxLength(LlmChatOperationStateChangedEvent.MaximumFailureCodeLength)
            .IsRequired();
        builder.Property(row => row.FinishReason)
            .HasMaxLength(LlmChatInvocationRecord.MaximumFinishReasonLength)
            .IsRequired();
        builder.HasOne<LlmChatOperationRow>()
            .WithMany()
            .HasForeignKey(row => row.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.OccurredAtUtc);
    }
}
