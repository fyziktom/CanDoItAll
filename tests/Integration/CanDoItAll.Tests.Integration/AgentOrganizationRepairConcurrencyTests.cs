using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class AgentOrganizationRepairConcurrencyTests {
    [Fact]
    public async Task Repair_preserves_a_user_commit_after_its_assignment_read() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var factory = services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var native = factory.GetWorkspaceService(factory.GetOrganizationScope());
        var provider = (await native.ListProvidersAsync()).Single(item => item.Name == ManagedSeedProviderFallbacks.OpenAiDefaultProviderName);
        var targetId = await native.SaveAgentAsync(new() {
            Name = "Owned CRM runtime repair agent", TemplateKey = "crmhr-ai-resource-owned-repair",
            ProviderProfileId = provider.Id, Model = provider.DefaultModel
        });
        var target = (await native.ListAgentsAsync(false)).Single(agent => agent.Id == targetId);
        var store = new FileSandboxWorkspaceStore(factory.GetWorkspaceRoot(), factory.GetOrganizationScope(),
            services.GetRequiredService<IAgentCatalogMutationPolicy>());
        await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == target.Id
                ? agent with { Model = "retired-repair-model", UpdatedAtUtc = AgentConfigurationVersion.NextRevision(agent.UpdatedAtUtc, DateTimeOffset.UtcNow) }
                : agent).ToArray()
        });
        Assert.Equal("retired-repair-model", (await native.GetAgentEditorAsync(target.Id)).Model);
        var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RepairReadProxy>();
        var control = (RepairReadProxy)(object)proxy;
        control.Native = native;
        control.AfterAgentRead = async () => {
            var peer = await native.GetAgentEditorAsync(target.Id);
            peer.Model = target.Model;
            peer.Summary = "User configuration committed after repair read 東京";
            await native.SaveAgentAsync(peer);
        };
        var repair = new AgentFrameworkOrganizationCatalogRepairService(
            services.GetRequiredService<IAiTechnicalAgentProjectionStore>(), new RepairFactory(factory, proxy),
            services.GetRequiredService<IProviderProfileService>(), services.GetRequiredService<IAgentCatalogMutationPolicy>(),
            services.GetRequiredService<AgentConfigurationChanges>());
        await repair.EnsureCurrentOrganizationCatalogAsync();
        Assert.Equal(1, control.InterceptedReads);
        var current = await native.GetAgentEditorAsync(target.Id);
        Assert.Equal("User configuration committed after repair read 東京", current.Summary);
        Assert.Equal(target.Model, current.Model);
    }

    [Fact]
    public async Task Legacy_import_creates_capabilities_and_agents_without_borrowing_their_other_scope_originals() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var factory = services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var legacy = factory.GetWorkspaceService(WorkspaceScopeDescriptor.Organization("owned-repair-legacy"));
        var capabilityId = await legacy.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "owned-repair-skill", Name = "Owned legacy repair skill",
            ConfigurationJson = """{"skillSource":"inline","inlineSkill":{"name":"owned-repair-skill","instructions":"Retain the stored extension."},"future":17}"""
        });
        var agentId = await legacy.SaveAgentAsync(new() {
            Name = "Owned legacy repair agent", Instructions = "Retain the capability assignment.", SelectedCapabilityIds = [capabilityId]
        });
        var facts = DispatchProxy.Create<IAiTechnicalAgentProjectionStore, RepairFactsProxy>();
        ((RepairFactsProxy)(object)facts).Facts = new([Guid.NewGuid()], [new(Guid.NewGuid(), agentId)]);
        var repair = new AgentFrameworkOrganizationCatalogRepairService(facts, factory,
            services.GetRequiredService<IProviderProfileService>(), services.GetRequiredService<IAgentCatalogMutationPolicy>(),
            services.GetRequiredService<AgentConfigurationChanges>());
        await repair.EnsureCurrentOrganizationCatalogAsync();
        var current = factory.GetWorkspaceService(factory.GetOrganizationScope());
        var imported = await current.GetAgentEditorAsync(agentId);
        var importedCapability = (await current.ListCapabilitiesAsync()).Single(item => item.Key == "owned-repair-skill");
        Assert.NotEqual(capabilityId, importedCapability.Id);
        Assert.False(importedCapability.IsBuiltIn);
        Assert.Contains("17", importedCapability.ConfigurationJson);
        Assert.Equal([importedCapability.Id], imported.SelectedCapabilityIds);
        Assert.Equal([capabilityId], (await legacy.GetAgentEditorAsync(agentId)).SelectedCapabilityIds);
    }

    private sealed class RepairFactory(ICanDoItAllAgentWorkspaceFactory native, IAgentFrameworkWorkspaceService workspace)
        : ICanDoItAllAgentWorkspaceFactory {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => workspace;
        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope)
            => scope == native.GetOrganizationScope() ? workspace : native.GetWorkspaceService(scope);
        public WorkspaceScopeDescriptor GetOrganizationScope() => native.GetOrganizationScope();
        public string GetWorkspaceRoot() => native.GetWorkspaceRoot();
    }
}

public class RepairReadProxy : DispatchProxy {
    public IAgentFrameworkWorkspaceService Native { get; set; } = default!;
    public Func<Task>? AfterAgentRead { get; set; }
    public int InterceptedReads { get; private set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        if (method?.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
            return ReadAsync((bool)args![0]!, (CancellationToken)args[1]!);
        }
        return method!.Invoke(Native, args);
    }

    private async Task<IReadOnlyList<AgentDefinition>> ReadAsync(bool includeTemplates, CancellationToken token) {
        var result = await Native.ListAgentsAsync(includeTemplates, token);
        if (AfterAgentRead is { } after) {
            AfterAgentRead = null;
            InterceptedReads++;
            await after();
        }
        return result;
    }
}

public class RepairFactsProxy : DispatchProxy {
    public AiTechnicalCatalogRepairFacts Facts { get; set; } = new([], []);
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method?.Name == nameof(IAiTechnicalAgentProjectionStore.ReadCatalogRepairFactsAsync)
            ? Task.FromResult(Facts) : throw new InvalidOperationException($"Unexpected repair projection call: {method?.Name}");
}
