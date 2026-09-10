using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

public sealed class PromptsDbContext(DbContextOptions<PromptsDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new PromptArtifactConfiguration());
        modelBuilder.ApplyConfiguration(new PromptVersionConfiguration());
        modelBuilder.ApplyConfiguration(new PromptCollectionConfiguration());
        modelBuilder.ApplyConfiguration(new PromptTagConfiguration());
        modelBuilder.ApplyConfiguration(new PromptArtifactTagConfiguration());
        modelBuilder.ApplyConfiguration(new PromptSupportedProviderModelConfiguration());
        modelBuilder.ApplyConfiguration(new PromptSupportedConsumerConfiguration());
        modelBuilder.ApplyConfiguration(new PromptTemplateTokenConfiguration());
        modelBuilder.ApplyConfiguration(new PromptCompatibilityWarningPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new PromptUsageRecordConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
