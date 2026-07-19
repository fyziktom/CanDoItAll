using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Prompts;

internal sealed class PromptArtifactConfiguration : IEntityTypeConfiguration<PromptArtifact>
{
    public void Configure(EntityTypeBuilder<PromptArtifact> builder)
    {
        builder.ToTable("Prompts_PromptArtifacts");
        builder.HasKey(prompt => prompt.Id);
        builder.Property(prompt => prompt.Title).HasMaxLength(200).IsRequired();
        builder.Property(prompt => prompt.Summary).HasColumnType("TEXT");
        builder.Property(prompt => prompt.Phase).HasMaxLength(80);
        builder.Property(prompt => prompt.CurrentDraftText).HasColumnType("TEXT");
        builder.Property(prompt => prompt.SearchText).HasColumnType("TEXT");
        builder.Property(prompt => prompt.SourceKey).HasMaxLength(200);
        builder.Property(prompt => prompt.SourceCatalog).HasMaxLength(120);
        builder.Property(prompt => prompt.SourceGroupKey).HasMaxLength(120);
        builder.Property(prompt => prompt.SourceGroupName).HasMaxLength(200);
        builder.Property(prompt => prompt.SourceItemKind).HasMaxLength(80);
        builder.Property(prompt => prompt.SourceFingerprint).HasMaxLength(64);
        builder.HasIndex(prompt => new { prompt.Provenance, prompt.SourceKey }).IsUnique();
        builder.HasIndex(prompt => new { prompt.IsArchived, prompt.Status, prompt.Kind, prompt.UpdatedAtUtc });
        builder.HasIndex(prompt => new { prompt.IsArchived, prompt.UpdatedAtUtc, prompt.Title, prompt.Id })
            .IsDescending(false, true, false, false);
    }
}

internal sealed class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.ToTable("Prompts_PromptVersions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Content).HasColumnType("TEXT");
        builder.Property(version => version.CreationReason).HasMaxLength(200);
        builder.Property(version => version.OutputFormat).HasMaxLength(80).IsRequired();
        builder.Property(version => version.SourceBlueprintId).HasMaxLength(200);
        builder.Property(version => version.TitleSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(version => version.SummarySnapshot).HasColumnType("TEXT");
        builder.HasIndex(version => new { version.PromptArtifactId, version.VersionNumber }).IsUnique();
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(version => version.PromptArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PromptCollectionConfiguration : IEntityTypeConfiguration<PromptCollection>
{
    public void Configure(EntityTypeBuilder<PromptCollection> builder)
    {
        builder.ToTable("Prompts_PromptCollections");
        builder.HasKey(collection => collection.Id);
        builder.Property(collection => collection.Name).HasMaxLength(120).IsRequired();
        builder.Property(collection => collection.Description).HasColumnType("TEXT");
    }
}

internal sealed class PromptTagConfiguration : IEntityTypeConfiguration<PromptTag>
{
    public void Configure(EntityTypeBuilder<PromptTag> builder)
    {
        builder.ToTable("Prompts_PromptTags");
        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Name).HasMaxLength(120).IsRequired();
        builder.Property(tag => tag.NameKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(tag => tag.Name).IsUnique();
        builder.HasIndex(tag => tag.NameKey);
    }
}

internal sealed class PromptArtifactTagConfiguration : IEntityTypeConfiguration<PromptArtifactTag>
{
    public void Configure(EntityTypeBuilder<PromptArtifactTag> builder)
    {
        builder.ToTable("Prompts_PromptArtifactTags");
        builder.HasKey(item => new { item.PromptArtifactId, item.PromptTagId });
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(item => item.PromptArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PromptTag>()
            .WithMany()
            .HasForeignKey(item => item.PromptTagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PromptSupportedProviderModelConfiguration : IEntityTypeConfiguration<PromptSupportedProviderModel>
{
    public void Configure(EntityTypeBuilder<PromptSupportedProviderModel> builder)
    {
        builder.ToTable("Prompts_PromptSupportedProviderModels");
        builder.HasKey(item => new { item.PromptArtifactId, item.ProviderKey, item.ModelKey });
        builder.Property(item => item.Provider).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ProviderKey).HasMaxLength(120).IsRequired();
        builder.Property(item => item.ModelKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => new { item.ProviderKey, item.ModelKey, item.PromptArtifactId });
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(item => item.PromptArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PromptSupportedConsumerConfiguration : IEntityTypeConfiguration<PromptSupportedConsumer>
{
    public void Configure(EntityTypeBuilder<PromptSupportedConsumer> builder)
    {
        builder.ToTable("Prompts_PromptSupportedConsumers");
        builder.HasKey(item => new { item.PromptArtifactId, item.Consumer });
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(item => item.PromptArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PromptTemplateTokenConfiguration : IEntityTypeConfiguration<PromptTemplateToken>
{
    public void Configure(EntityTypeBuilder<PromptTemplateToken> builder)
    {
        builder.ToTable("Prompts_PromptTemplateTokens");
        builder.HasKey(item => new { item.PromptArtifactId, item.NameKey });
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameKey).HasMaxLength(200).IsRequired();
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(item => item.PromptArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PromptCompatibilityWarningPreferenceConfiguration : IEntityTypeConfiguration<PromptCompatibilityWarningPreference>
{
    public void Configure(EntityTypeBuilder<PromptCompatibilityWarningPreference> builder)
    {
        builder.ToTable("Prompts_PromptCompatibilityWarningPreferences");
        builder.HasKey(item => new { item.PromptArtifactId, item.Consumer, item.IssueCode });
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(item => item.PromptArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PromptUsageRecordConfiguration : IEntityTypeConfiguration<PromptUsageRecord>
{
    public void Configure(EntityTypeBuilder<PromptUsageRecord> builder)
    {
        builder.ToTable("Prompts_PromptUsageRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.ProviderName).HasMaxLength(120);
        builder.Property(record => record.RepositoryName).HasMaxLength(200);
        builder.Property(record => record.BranchName).HasMaxLength(120);
        builder.Property(record => record.CommitSha).HasMaxLength(80);
        builder.Property(record => record.CommitUrl).HasMaxLength(500);
        builder.Property(record => record.UsageNote).HasColumnType("TEXT");
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(record => record.PromptArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
