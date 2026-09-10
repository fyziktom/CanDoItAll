using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.EntityConfigurations;

internal sealed class LlmChatDefinitionCreateReceiptConfiguration : IEntityTypeConfiguration<LlmChatDefinitionCreateReceiptRow> {
    public void Configure(EntityTypeBuilder<LlmChatDefinitionCreateReceiptRow> builder) {
        builder.ToTable("LlmChats_DefinitionCreateReceipts");
        builder.HasKey(row => new { row.Producer, row.Actor, row.HistoryNamespace, row.IntentId });
        builder.Property(row => row.Producer).HasMaxLength(LlmChatDefinitionCreateScope.MaximumIdentityLength).IsRequired();
        builder.Property(row => row.Actor).HasMaxLength(LlmChatDefinitionCreateScope.MaximumIdentityLength).IsRequired();
        builder.Property(row => row.HistoryNamespace).HasMaxLength(LlmChatDefinitionCreateScope.MaximumIdentityLength).IsRequired();
        builder.Property(row => row.SemanticFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasOne<LlmChatDefinitionRevisionRow>()
            .WithMany()
            .HasForeignKey(row => new { row.DefinitionId, row.DefinitionRevision })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LlmChats_CreateReceipt_OriginalRevision");
    }
}
