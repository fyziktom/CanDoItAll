using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class CapabilityVerificationRaceIntegrationTests {
    [Fact]
    public Task Detached_capability_during_diagnostic_does_not_persist_stale_proof()
        => AssertSupersededAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent with { Capabilities = [] }).ToArray()
        });

    [Fact]
    public Task Changed_capability_definition_during_diagnostic_does_not_persist_proof()
        => AssertSupersededAsync(catalog => catalog with {
            Capabilities = catalog.Capabilities.Select(capability => capability with { Description = "Changed during diagnostic" }).ToArray()
        });

    [Fact]
    public Task Missing_agent_after_diagnostic_does_not_update_global_proof()
        => AssertSupersededAsync(catalog => catalog with { Agents = [] });

    private static async Task AssertSupersededAsync(Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> change) {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var pending = fixture.Catalog.VerifyCapabilityAsync(fixture.Agent.Id, fixture.Capability.Id);
        await fixture.Proof.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Store.UpdateCatalogAsync(change);
        var before = await fixture.Store.LoadCatalogAsync();
        fixture.Proof.Release.SetResult();
        var failure = await Assert.ThrowsAsync<CapabilityVerificationException>(() => pending);
        Assert.Equal(CapabilityVerificationDisposition.Superseded, failure.Outcome.Disposition);
        var after = await fixture.Store.LoadCatalogAsync();
        Assert.Equal(before.Agents.Select(agent => agent.UpdatedAtUtc), after.Agents.Select(agent => agent.UpdatedAtUtc));
        Assert.Equal(CapabilityProofStatus.NotRun, after.Capabilities.Single(capability => capability.Id == fixture.Capability.Id).ProofStatus);
        Assert.Equal(1, fixture.Proof.Calls);
    }
}

internal sealed class CapabilityFileFixture : IDisposable {
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("candoitall-capability-proof-");
    public FileSandboxWorkspaceStore Store { get; }
    public CapabilityStoreProbe StoreProbe { get; }
    public string RootPath => directory.FullName;
    public AgentFrameworkWorkspaceCatalogService Catalog { get; }
    public PausedInlineProof Proof { get; } = new();
    public CapabilityVerificationPublication Verification { get; }
    public AgentDefinition Agent { get; private set; } = default!;
    public CapabilityCatalogItem Capability { get; } = new(Guid.NewGuid(), CapabilityKind.Skill,
        "safe-inline-proof", "Safe inline proof", "Local diagnostic fixture", "inline://safe-proof",
        """{"inlineSkill":{"instructions":"Read the local fixture."}}""", CapabilityProofStatus.NotRun, "", null, false);

    private CapabilityFileFixture() {
        Store = new(directory.FullName);
        var instrumented = DispatchProxy.Create<ISandboxWorkspaceStore, CapabilityStoreProbe>();
        StoreProbe = (CapabilityStoreProbe)(object)instrumented;
        StoreProbe.Inner = Store;
        var profiles = new ProviderProfileService();
        var registry = new WorkspaceBackedProviderProfileRegistry(instrumented, profiles);
        Verification = new(instrumented, Proof, registry);
        Catalog = new(instrumented, DispatchProxy.Create<IAgentPackageService, UnusedCapabilityDependency>(), Proof,
            profiles, DispatchProxy.Create<IProviderDiagnosticsService, UnusedCapabilityDependency>(), registry, registry);
    }

    public static async Task<CapabilityFileFixture> CreateAsync() {
        var fixture = new CapabilityFileFixture();
        var seed = await fixture.Store.LoadCatalogAsync();
        fixture.Agent = seed.Agents.First(agent => !agent.IsTemplate) with {
            Id = Guid.NewGuid(), Name = "Capability proof fixture", TemplateKey = "", ProviderProfileId = null, ConfigurationJson = "{}",
            Capabilities = [new(fixture.Capability.Id, fixture.Capability.Key, fixture.Capability.Kind, CapabilityProofStatus.NotRun, null, "")]
        };
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with { Agents = [fixture.Agent], Capabilities = [fixture.Capability] });
        return fixture;
    }

    public void Dispose() => directory.Delete(recursive: true);
}

internal sealed class PausedInlineProof : ICapabilityProofService {
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }
    public Action? AfterDiagnostic { get; set; }
    public AgentDefinition? CapturedAgent { get; private set; }
    public ProviderProfile? CapturedProvider { get; private set; }
    public CapabilityCatalogItem? CapturedCapability { get; private set; }
    public async Task<CapabilityVerificationResult> VerifyAsync(AgentDefinition agent, ProviderProfile? provider,
        CapabilityCatalogItem capability, CancellationToken cancellationToken = default) {
        Calls++;
        CapturedAgent = agent;
        CapturedProvider = provider;
        CapturedCapability = capability;
        Started.TrySetResult();
        await Release.Task;
        var result = await new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()).VerifyAsync(agent, provider, capability, cancellationToken);
        AfterDiagnostic?.Invoke();
        return result;
    }
}

public class UnusedCapabilityDependency : DispatchProxy {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => throw new InvalidOperationException("Unexpected dependency call in capability fixture.");
}

public class CapabilityStoreProbe : DispatchProxy {
    public ISandboxWorkspaceStore Inner { get; set; } = default!;
    public Action? BeforeCatalogWrite { get; set; }
    public Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog>? BeforeUpdate { get; set; }
    public int Writes { get; private set; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        if (targetMethod!.Name == nameof(ISandboxWorkspaceStore.UpdateCatalogAsync) && args!.Length == 2) {
            Writes++;
            var update = (Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog>)args[0]!;
            return Inner.UpdateCatalogAsync(catalog => {
                var next = update(BeforeUpdate?.Invoke(catalog) ?? catalog);
                BeforeCatalogWrite?.Invoke();
                return next;
            }, (CancellationToken)args[1]!);
        }
        return targetMethod.Invoke(Inner, args);
    }
}
