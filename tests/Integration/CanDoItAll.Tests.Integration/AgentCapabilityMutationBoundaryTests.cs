using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentCapabilityMutationBoundaryTests {
    [Fact]
    public async Task Catalog_commit_then_index_failure_is_verified_without_second_save() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var (commands, workspace) = Connect(fixture);
        var draft = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        var attempt = new AgentCapabilityAssignmentAttempt(draft, fixture.Capability.Id);
        var index = Path.Combine(WorkspaceScopeDescriptor.Sandbox.ResolveDataRoot(fixture.RootPath), "workspace.index.json");
        var preserved = index + ".preserved";
        fixture.StoreProbe.BeforeCatalogWrite = () => {
            File.Move(index, preserved);
            Directory.CreateDirectory(index);
        };
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, await commands.AssignAsync(attempt));
        fixture.StoreProbe.BeforeCatalogWrite = null;
        Directory.Delete(index);
        File.Move(preserved, index);
        var canonical = await workspace.GetAgentEditorAsync(attempt.AgentId);
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, attempt.Classify(canonical));
        Assert.Empty(canonical.SelectedCapabilityIds);
        Assert.Equal(1, fixture.StoreProbe.Writes);
    }

    [Fact]
    public async Task Concurrency_conflict_does_not_overwrite_remote_assignment() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var (commands, _) = Connect(fixture);
        var draft = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        var attempt = new AgentCapabilityAssignmentAttempt(draft, fixture.Capability.Id);
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent with { UpdatedAtUtc = agent.UpdatedAtUtc.AddTicks(1), Name = "Remote revision" }).ToArray()
        });
        Assert.Equal(AgentCapabilityOperationStatus.Conflict, await commands.AssignAsync(attempt));
        var canonical = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        Assert.Equal("Remote revision", canonical.Name);
        Assert.Contains(fixture.Capability.Id, canonical.SelectedCapabilityIds);
    }

    [Fact]
    public async Task Validation_rejection_allows_correction_without_catalog_write() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var (commands, _) = Connect(fixture);
        var draft = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        draft.ProviderProfileId = Guid.NewGuid();
        draft.ThinkingEffortOverride = AgentReasoningEffortLevel.High;
        var before = (await fixture.Store.LoadCatalogAsync()).CatalogDataRevision;
        Assert.Equal(AgentCapabilityOperationStatus.Rejected,
            await commands.AssignAsync(new(draft, fixture.Capability.Id)));
        Assert.Equal(before, (await fixture.Store.LoadCatalogAsync()).CatalogDataRevision);
        draft.ProviderProfileId = null;
        draft.ThinkingEffortOverride = null;
        Assert.Equal(AgentCapabilityOperationStatus.Committed, await commands.AssignAsync(new(draft, fixture.Capability.Id)));
    }

    [Fact]
    public async Task Cancellation_before_dispatch_performs_zero_writes() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var (commands, _) = Connect(fixture);
        var draft = await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(AgentCapabilityOperationStatus.CanceledBeforeDispatch,
            await commands.AssignAsync(new(draft, fixture.Capability.Id), cancellation.Token));
        Assert.Equal(0, fixture.StoreProbe.Writes);
    }

    [Fact]
    public async Task Cancellation_after_dispatch_is_not_assumed_rollback() {
        using var fixture = await CapabilityFileFixture.CreateAsync();
        var (commands, workspace) = Connect(fixture);
        using var cancellation = new CancellationTokenSource();
        var adapter = (CapabilityWorkspaceAdapter)(object)workspace;
        adapter.AfterSave = () => {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };
        var attempt = new AgentCapabilityAssignmentAttempt(await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id), fixture.Capability.Id);
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, await commands.AssignAsync(attempt, cancellation.Token));
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, attempt.Classify(await fixture.Catalog.GetAgentEditorAsync(fixture.Agent.Id)));
        Assert.Equal(1, fixture.StoreProbe.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_commit_with_projection_warning_reconciles_without_second_save(bool cancellation) {
        var bridge = new CapabilityProjectionBridge();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, configureServices: services => {
            services.AddRazorComponents().AddInteractiveServerComponents();
            services.AddAgentFrameworkUi();
            services.RemoveAll<IAiTechnicalAgentBridge>();
            services.AddSingleton<IAiTechnicalAgentBridge>(bridge);
        });
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var commands = scope.ServiceProvider.GetRequiredService<IAgentCapabilityCommands>();
        var id = await workspace.SaveAgentAsync(new() { Name = "Capabilities projection fixture" });
        var capability = (await workspace.ListCapabilitiesAsync()).First();
        var attempt = new AgentCapabilityAssignmentAttempt(await workspace.GetAgentEditorAsync(id), capability.Id);
        using var owner = new CancellationTokenSource();
        bridge.Failure = () => {
            if (cancellation) {
                owner.Cancel();
                return new OperationCanceledException(owner.Token);
            }
            return new IOException("Projection fixture unavailable");
        };
        Assert.Equal(AgentCapabilityOperationStatus.CommittedWithWarning, await commands.AssignAsync(attempt, owner.Token));
        bridge.Failure = null;
        var current = await workspace.GetAgentEditorAsync(id);
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, attempt.Classify(current));
        Assert.Equal(current.ExpectedUpdatedAtUtc, (await workspace.GetAgentEditorAsync(id)).ExpectedUpdatedAtUtc);
    }

    private static (AgentCapabilityCommands Commands, IAgentFrameworkWorkspaceService Workspace) Connect(CapabilityFileFixture fixture) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CapabilityWorkspaceAdapter>();
        ((CapabilityWorkspaceAdapter)(object)workspace).Catalog = fixture.Catalog;
        return (new(workspace, NullLogger<AgentCapabilityCommands>.Instance), workspace);
    }
}

public class CapabilityWorkspaceAdapter : DispatchProxy {
    internal AgentFrameworkWorkspaceCatalogService Catalog { get; set; } = default!;
    public Action? AfterSave { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
        nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) => SaveAsync((AgentEditorModel)args![0]!, (CancellationToken)args[1]!),
        nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync) => Catalog.GetAgentEditorAsync((Guid?)args![0], (CancellationToken)args[1]!),
        _ => throw new InvalidOperationException("Unexpected capability workspace call.")
    };
    private async Task<Guid> SaveAsync(AgentEditorModel request, CancellationToken token) {
        var id = await Catalog.SaveAgentAsync(request, token);
        AfterSave?.Invoke();
        return id;
    }
}

internal sealed class CapabilityProjectionBridge : IAiTechnicalAgentBridge {
    public Func<Exception>? Failure { get; set; }
    public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default)
        => Failure is null ? Task.CompletedTask : Task.FromException(Failure());
    public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
