using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class PromptsDbContextTests {
    [Fact]
    public void Runtime_model_excludes_foreign_records_and_preserves_timestamp_concurrency() {
        using var context = new PromptsDbContext(new DbContextOptionsBuilder<PromptsDbContext>()
            .UseInMemoryDatabase($"prompts-model-{Guid.NewGuid():N}").Options);
        Assert.Equal(new[] {
            typeof(PromptArtifact), typeof(PromptVersion), typeof(PromptCollection), typeof(PromptTag),
            typeof(PromptArtifactTag), typeof(PromptSupportedProviderModel), typeof(PromptSupportedConsumer),
            typeof(PromptTemplateToken), typeof(PromptCompatibilityWarningPreference), typeof(PromptUsageRecord)
        }.OrderBy(type => type.Name), context.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        var token = Assert.Single(context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()), property => property.IsConcurrencyToken);
        Assert.Equal(typeof(PromptArtifact), token.DeclaringType.ClrType);
        Assert.Equal(nameof(PromptArtifact.UpdatedAtUtc), token.Name);
        Assert.Equal(typeof(DateTimeOffset), token.ClrType);
        Assert.Throws<InvalidOperationException>(() => context.Set<SearchDocument>().ToList());
        Assert.Throws<InvalidOperationException>(() => context.Set<Project>().ToList());
    }
}
