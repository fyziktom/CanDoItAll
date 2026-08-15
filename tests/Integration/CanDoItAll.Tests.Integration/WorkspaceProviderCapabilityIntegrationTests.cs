using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkspaceProviderCapabilityIntegrationTests
{
    [Fact]
    public async Task SaveProviderAsync_persists_ollama_structured_output_as_false_even_when_editor_posts_true()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var saveResult = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "Ollama capability truth",
            ConnectorPluginKey = OllamaProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "http://127.0.0.1:11434",
                ["defaultModel"] = "llama3.1",
                ["timeoutSeconds"] = "45"
            }),
            IsEnabled = true,
            SupportsStructuredOutput = true
        });
        Assert.True(saveResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var provider = await dbContext.Set<ProviderProfile>().SingleAsync(item => item.Id == saveResult.Value);

        Assert.False(provider.SupportsStructuredOutput);
    }

    [Fact]
    public async Task SaveProviderAsync_persists_openai_structured_output_as_true_from_capability_defaults()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var secretService = scope.ServiceProvider.GetRequiredService<SecretService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var secretResult = await secretService.SaveAsync(new SecretEditorModel
        {
            Name = "OpenAI capability truth key",
            Kind = SecretKind.ApiKey,
            SecretValue = "sk-test",
            Scope = "workspace"
        });
        Assert.True(secretResult.IsSuccess);

        var saveResult = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "OpenAI capability truth",
            ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            ApiKeySecretId = secretResult.Value,
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "https://api.openai.com/v1/models",
                ["defaultModel"] = OpenAiProviderAdapter.DefaultModel,
                ["timeoutSeconds"] = "45"
            }),
            IsEnabled = true,
            SupportsStructuredOutput = false
        });
        Assert.True(saveResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var provider = await dbContext.Set<ProviderProfile>().SingleAsync(item => item.Id == saveResult.Value);

        Assert.True(provider.SupportsStructuredOutput);
    }

}
