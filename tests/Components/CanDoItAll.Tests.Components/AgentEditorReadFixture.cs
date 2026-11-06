using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Tests.Support;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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
    public Func<CancellationToken, Task<IReadOnlyList<CapabilityCatalogItem>>>? ReadCapabilities { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<ProviderProfile>>>? ReadProviders { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<AgentEditorSecret>>>? ReadSecrets { get; set; }

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

    public Task<IReadOnlyList<CapabilityCatalogItem>> ReadCapabilitiesAsync(CancellationToken cancellationToken = default)
        => ReadCapabilities?.Invoke(cancellationToken) ?? Task.FromResult(Capabilities);

    public Task<IReadOnlyList<AgentEditorSecret>> ReadSecretsAsync(CancellationToken cancellationToken = default)
        => ReadSecrets?.Invoke(cancellationToken) ?? Task.FromResult(Secrets);

    public Task<IReadOnlyList<ProviderProfile>> ReadProvidersAsync(CancellationToken cancellationToken = default) {
        ProviderReads++;
        return ReadProviders?.Invoke(cancellationToken) ?? Task.FromResult(Providers);
    }

    public Task<IReadOnlyList<AgentEditorProject>> ReadProjectsAsync(CancellationToken cancellationToken = default) {
        ProjectReads++;
        return ReadProjects?.Invoke(cancellationToken) ?? Task.FromResult(Projects);
    }
}

internal static class AgentEditorFixtureServices {
    public static IServiceCollection AddAgentEditorReadFixture(this IServiceCollection services) {
        services.AddAgentConfigurationFixture();
        services.AddSingleton<AgentEditorReadFixture>();
        services.AddSingleton<IAgentEditorReads>(provider => provider.GetRequiredService<AgentEditorReadFixture>());
        return services;
    }

    public static IServiceCollection AddAgentConfigurationFixture(this IServiceCollection services) {
        services.AddAgentFrameworkUi();
        services.AddSingleton<EditingChangeFeed>();
        services.AddSingleton<IEditingUiAccess>(new EditingUiTestAccess());
        services.AddSingleton<AgentConfigurationWriteContext>();
        var scope = new EditingScope(EditingScopeKind.Dataset, Guid.NewGuid(), 1);
        services.AddSingleton(provider => new AgentConfigurationChanges(provider.GetRequiredService<EditingChangeFeed>(),
            () => scope, provider.GetRequiredService<IEditingUiAccess>(), provider.GetRequiredService<AgentConfigurationWriteContext>(),
            NullLogger<EditingObservation>.Instance));
        return services;
    }

    public static IRenderedComponent<AgentDetailsDialog> RenderEditor(this BunitContext context,
        AgentEditorModel draft, AgentEditorSection section,
        IReadOnlyList<ProviderProfile>? providers = null, IReadOnlyList<CapabilityCatalogItem>? capabilities = null) {
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var reads = context.Services.GetRequiredService<AgentEditorReadFixture>();
        reads.Draft = draft;
        reads.Providers = providers ?? [];
        reads.Capabilities = capabilities ?? [];
        return context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, draft.Id)
            .Add(component => component.Section, section));
    }
}

public class AgentEditorSaveProbe : DispatchProxy {
    private AgentDefinition? acceptedAgent;
    public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];
    public List<AgentEditorModel> SavedModels { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch {
        nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) => Task.FromResult(Capabilities),
        nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
            Task.FromResult<IReadOnlyList<AgentDefinition>>(acceptedAgent is { } agent ? [agent] : []),
        nameof(IAgentFrameworkWorkspaceService.SaveAgentDefinitionAsync) => Save((AgentEditorModel)args![0]!),
        "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
        _ => throw new InvalidOperationException($"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
    };

    private Task<AgentDefinition> Save(AgentEditorModel model) {
        SavedModels.Add(model);
        var accepted = AgentCatalogPanelTests.CreateAgent(model.Id ?? Guid.NewGuid(), model.Name, "editor-test") with {
            ProviderProfileId = model.ProviderProfileId,
            Model = model.Model,
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(model.ConfigurationJson, model.WorkspaceToolAccess),
            Permissions = model.Permissions,
            Capabilities = Capabilities.Where(capability => model.SelectedCapabilityIds.Contains(capability.Id))
                .Select(capability => new AgentCapabilityAssignment(capability.Id, capability.Key, capability.Kind,
                    CapabilityProofStatus.NotRun, null, string.Empty))
                .ToArray()
        };
        acceptedAgent = accepted;
        return Task.FromResult(accepted);
    }
}
