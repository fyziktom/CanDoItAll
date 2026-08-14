using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.LlmChats.Persistence.EntityConfigurations;

internal sealed class LlmChatDefinitionConfiguration : IEntityTypeConfiguration<LlmChatDefinitionRow>
{
    public void Configure(EntityTypeBuilder<LlmChatDefinitionRow> builder)
    {
        builder.ToTable("LlmChats_Definitions");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Name).HasMaxLength(LlmChatDefinitionValidation.MaximumNameLength).IsRequired();
        builder.Property(row => row.Summary).HasMaxLength(LlmChatDefinitionValidation.MaximumSummaryLength).IsRequired();
        builder.Property(row => row.AvatarImageUrl).HasMaxLength(LlmChatDefinitionValidation.MaximumAvatarImageUrlLength).IsRequired();
        builder.Property(row => row.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(row => new { row.Status, row.Name });
    }
}

internal sealed class LlmChatDefinitionRevisionConfiguration : IEntityTypeConfiguration<LlmChatDefinitionRevisionRow>
{
    public void Configure(EntityTypeBuilder<LlmChatDefinitionRevisionRow> builder)
    {
        builder.ToTable("LlmChats_DefinitionRevisions");
        builder.HasKey(row => new { row.DefinitionId, row.Revision });
        builder.Property(row => row.Name).HasMaxLength(LlmChatDefinitionValidation.MaximumNameLength).IsRequired();
        builder.Property(row => row.Summary).HasMaxLength(LlmChatDefinitionValidation.MaximumSummaryLength).IsRequired();
        builder.Property(row => row.AvatarImageUrl).HasMaxLength(LlmChatDefinitionValidation.MaximumAvatarImageUrlLength).IsRequired();
        builder.Property(row => row.SystemPrompt).HasMaxLength(LlmMessage.MaximumTextLength).IsRequired();
        builder.Property(row => row.ProviderName).HasMaxLength(LlmChatDefinitionValidation.MaximumProviderNameLength).IsRequired();
        builder.Property(row => row.Model).HasMaxLength(LlmChatDefinitionValidation.MaximumModelLength).IsRequired();
        builder.Property(row => row.ModelParameterConfigurationJson).IsRequired();
        builder.Property(row => row.ResponseSchemaJson).IsRequired();
        builder.Property(row => row.ResponseSchemaName).HasMaxLength(200).IsRequired();
        builder.Property(row => row.ResponseSchemaDescription).HasMaxLength(2_000).IsRequired();
        builder.Property(row => row.SettingsFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(row => row.Reason).HasMaxLength(LlmChatDefinitionValidation.MaximumRevisionReasonLength).IsRequired();
        builder.HasOne<LlmChatDefinitionRow>()
            .WithMany()
            .HasForeignKey(row => row.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.ProviderProfileId);
    }
}

internal sealed class LlmChatDefinitionTagConfiguration : IEntityTypeConfiguration<LlmChatDefinitionTagRow>
{
    public void Configure(EntityTypeBuilder<LlmChatDefinitionTagRow> builder)
    {
        builder.ToTable("LlmChats_DefinitionTags");
        builder.HasKey(row => new { row.DefinitionId, row.Tag });
        builder.Property(row => row.Tag).HasMaxLength(100).IsRequired();
        builder.HasOne<LlmChatDefinitionRow>()
            .WithMany()
            .HasForeignKey(row => row.DefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(row => row.Tag);
    }
}
