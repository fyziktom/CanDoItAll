using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

internal sealed class AgentSettingsTestAccess : IAgentSettingsAccess {
    public static AgentSettingsTestAccess Instance { get; } = new();
    public Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken token) => read(token);
    public Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken token) => write(token);
    public ValueTask AuthorizePreviewAsync(CancellationToken token) => ValueTask.CompletedTask;
}

internal static class AgentSettingsTestServices {
    public static void AddSettingsOpeningFixture(this IServiceCollection services) {
        services.AddAgentConfigurationFixture();
        services.AddSingleton(provider => new WorkflowChanges(provider.GetRequiredService<EditingChangeFeed>(),
            provider.GetRequiredService<AgentConfigurationChanges>().Scope, new EditingOperationContext(), NullLogger<WorkflowChanges>.Instance));
    }
}
