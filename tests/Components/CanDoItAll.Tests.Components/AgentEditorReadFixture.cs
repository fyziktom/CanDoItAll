using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

internal sealed class AgentEditorReadFixture : IAgentEditorReads {
    public AgentEditorModel Draft { get; set; } = new();
    public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];
    public IReadOnlyList<ProviderProfile> Providers { get; set; } = [];
    public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];
    public IReadOnlyList<AgentEditorProject> Projects { get; set; } = [];
    public IReadOnlyList<AgentEditorSecret> Secrets { get; set; } = [];
    public string? ProviderError { get; set; }
    public string? SecretError { get; set; }
    public int ProjectReads { get; private set; }
    public int ProviderReads { get; private set; }
    public Func<AgentEditorTarget, CancellationToken, Task<AgentEditorLoadResult>>? Load { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<AgentEditorProject>>>? ReadProjects { get; set; }

    public Task<AgentEditorLoadResult> LoadAsync(AgentEditorTarget target,
        IReadOnlyList<ProviderProfile>? initialProviders = null, CancellationToken cancellationToken = default) {
        if (Load is not null) {
            return Load(target, cancellationToken);
        }
        if (target.AgentId != Draft.Id) {
            throw new InvalidOperationException("The editor requested a different fixture target.");
        }
        return Task.FromResult(Result(Draft, initialProviders));
    }

    public AgentEditorLoadResult Result(AgentEditorModel draft, IReadOnlyList<ProviderProfile>? initialProviders = null)
        => new(draft, Agents, Capabilities, new(initialProviders ?? Providers, ProviderError), new(Secrets, SecretError), null);

    public Task<IReadOnlyList<ProviderProfile>> ReadProvidersAsync(CancellationToken cancellationToken = default) {
        ProviderReads++;
        return Task.FromResult(Providers);
    }

    public Task<IReadOnlyList<AgentEditorProject>> ReadProjectsAsync(CancellationToken cancellationToken = default) {
        ProjectReads++;
        return ReadProjects?.Invoke(cancellationToken) ?? Task.FromResult(Projects);
    }
}

internal static class AgentEditorFixtureServices {
    public static IServiceCollection AddAgentEditorReadFixture(this IServiceCollection services) {
        services.AddAgentFrameworkUi();
        services.AddSingleton<AgentEditorReadFixture>();
        services.AddSingleton<IAgentEditorReads>(provider => provider.GetRequiredService<AgentEditorReadFixture>());
        return services;
    }

    public static IRenderedComponent<AgentDetailsDialog> RenderEditor(this BunitContext context,
        AgentEditorModel draft, AgentEditorSection section,
        IReadOnlyList<ProviderProfile>? providers = null, IReadOnlyList<CapabilityCatalogItem>? capabilities = null) {
        var reads = context.Services.GetRequiredService<AgentEditorReadFixture>();
        reads.Draft = draft;
        reads.Providers = providers ?? [];
        reads.Capabilities = capabilities ?? [];
        return context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, draft.Id)
            .Add(component => component.Section, section));
    }
}
