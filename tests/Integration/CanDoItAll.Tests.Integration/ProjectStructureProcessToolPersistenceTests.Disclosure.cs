using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProjectStructureProcessToolPersistenceTests {
    [Theory]
    [InlineData(AgentToolEffectState.Committed)]
    [InlineData(AgentToolEffectState.Unknown)]
    public async Task Cached_node_start_result_requires_current_read_but_not_a_new_mutation_grant(AgentToolEffectState effect) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var disclosure = await CompleteForDisclosureAsync(fixture, journal, lease, effect);
        await fixture.ChangeSourceAsync(ReadOnlySource);
        await using var context = new ProcessPersistenceDbContext(Options(scope.ServiceProvider));
        var result = ReadResult(disclosure);
        var before = await context.PreparedLaunches.AsNoTracking().SingleAsync(row => row.Id == result.Observation!.AdmissionId.Value);
        var events = await context.RuntimeEvents.CountAsync(row => row.RunId == result.RunId);
        await using (var held = await DisclosureCallback(scope.ServiceProvider, fixture)(disclosure, default)) {
            Assert.NotNull(held);
        }
        var after = await context.PreparedLaunches.AsNoTracking().SingleAsync(row => row.Id == before.Id);
        Assert.Equal(before.PayloadJson, after.PayloadJson);
        Assert.Equal(before.State, after.State);
        Assert.Equal(before.ContinuationGeneration, after.ContinuationGeneration);
        Assert.Equal(events, await context.RuntimeEvents.CountAsync(row => row.RunId == result.RunId));
        Assert.Equal(effect, disclosure.EffectState);
        Assert.Equal(result.Observation!.AdmissionId, ReadResult(disclosure).Observation!.AdmissionId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Cached_result_identity_cannot_be_retargeted_to_another_receipt_or_project(int change) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var disclosure = await CompleteForDisclosureAsync(fixture, journal, lease);
        var original = ReadResult(disclosure);
        var changed = change switch {
            0 => original with { ProjectId = Guid.NewGuid() },
            1 => original with { NodeId = "another-source-node" },
            2 => original with { LaunchPlanId = Guid.NewGuid() },
            3 => original with { ProcessDefinitionId = Guid.NewGuid() },
            4 => original with { Observation = original.Observation! with { AdmissionId = new(Guid.NewGuid()) } },
            _ => original with { RunId = Guid.NewGuid() }
        };
        var callback = DisclosureCallback(scope.ServiceProvider, fixture);
        var denied = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => callback(disclosure with {
            Result = JsonSerializer.SerializeToElement(changed, ProjectStructureProcessProposalCodec.SerializerOptions)
        }, default).AsTask());
        Assert.Equal("ProcessToolAdmissionDenied", denied.ErrorCode);
        await using (var allowed = await callback(disclosure, default)) {
            Assert.NotNull(allowed);
        }
        await using var context = new ProcessPersistenceDbContext(Options(scope.ServiceProvider));
        Assert.Equal(1, await context.PreparedLaunches.CountAsync(row => row.CallerIntentId == disclosure.IntentId.Value));
        Assert.Equal(1, await context.RuntimeStates.CountAsync(row => row.RunId == original.RunId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Cached_result_denies_current_source_revocation_without_erasing_the_receipt(int revocation) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var disclosure = await CompleteForDisclosureAsync(fixture, journal, lease);
        await fixture.ChangeSourceAsync(agent => revocation switch {
            0 => agent with { Permissions = agent.Permissions with { CanUseTools = false } },
            1 => agent with { Status = AgentLifecycleStatus.Suspended },
            _ => agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new()) }
        });
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() =>
            DisclosureCallback(scope.ServiceProvider, fixture)(disclosure, default).AsTask());
        var saved = Assert.IsType<ProcessPreparedLaunchSnapshot>(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .FindByIntentAsync(new(disclosure.IntentId.Value)));
        Assert.Equal(ReadResult(disclosure).Observation!.AdmissionId, saved.Preparation.AdmissionId);
        Assert.NotNull(saved.AcceptedAtUtc);
    }

    [Fact]
    public async Task Uncommitted_cached_failure_checks_current_project_read_without_allocating_a_Process_receipt() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var batch = await fixture.AdmitAsync(journal, lease, "failed");
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "failed", fixture.Payload, default);
        await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(), AgentToolEffectState.NotCommitted, default);
        var disclosure = new AgentToolResultDisclosure(claim.Proposal.IntentId, fixture.Payload, AgentToolEffectState.NotCommitted,
            JsonSerializer.SerializeToElement(new { success = false, code = "ProcessDefinitionRequired" }));
        await fixture.ChangeSourceAsync(ReadOnlySource);
        await using (var allowed = await DisclosureCallback(scope.ServiceProvider, fixture)(disclosure, default)) {
            Assert.NotNull(allowed);
        }
        await fixture.RevokeAsync();
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() =>
            DisclosureCallback(scope.ServiceProvider, fixture)(disclosure, default).AsTask());
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>().FindByIntentAsync(new(disclosure.IntentId.Value)));
    }

    [Fact]
    public async Task Result_disclosure_holds_the_exact_catalog_read_lease_until_the_saved_result_is_released() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var disclosure = await CompleteForDisclosureAsync(fixture, journal, lease);
        var callback = DisclosureCallback(scope.ServiceProvider, fixture);
        await using var held = await callback(disclosure, default);
        Assert.NotNull(held);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200))) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ChangeSourceAsync(agent => agent with {
                Permissions = agent.Permissions with { CanUseTools = false }
            }, timeout.Token));
        }
        await held.DisposeAsync();
        await fixture.RevokeAsync();
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => callback(disclosure, default).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cached_result_observes_the_original_receipt_after_human_unlink_or_project_retirement(bool retireProject) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var journal = fixture.Journal.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default);
        using var current = lease.Bind();
        var disclosure = await CompleteForDisclosureAsync(fixture, journal, lease);
        var result = ReadResult(disclosure);
        await fixture.ChangeSourceAsync(agent => ChangeSourceAccess(agent, access => {
            access.CanWrite = false;
            access.AllowAllProjects = true;
        }));
        if (retireProject) {
            var deleted = await scope.ServiceProvider.GetRequiredService<ProjectsService>().DeleteAsync(fixture.Input.ProjectId);
            Assert.Equal(fixture.Input.ProjectId, deleted.ProjectId);
            Assert.Empty(deleted.Warnings);
        } else {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchRelationService>().UnlinkObjectsAsync(fixture.Input.ProjectId,
                fixture.Input.NodeId, ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(result.RunId!.Value), ProjectObjectLinkKind.Uses));
        }
        await using (var held = await DisclosureCallback(scope.ServiceProvider, fixture)(disclosure, default)) {
            Assert.NotNull(held);
        }
        await using var workbench = new WorkbenchDbContext(OwnerOptions<WorkbenchDbContext>(scope.ServiceProvider));
        Assert.False(await workbench.Set<ProjectObjectLinkRecord>().AnyAsync(row => row.ProjectId == fixture.Input.ProjectId &&
            row.SourceNodeKey == fixture.Input.NodeId && row.TargetNodeKey == ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(result.RunId!.Value)));
        var saved = Assert.IsType<ProcessPreparedLaunchSnapshot>(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .FindByIntentAsync(new(disclosure.IntentId.Value)));
        Assert.Equal(result.Observation!.AdmissionId, saved.Preparation.AdmissionId);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Delivered, saved.LinkDeliveryState);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Removed, (await scope.ServiceProvider.GetRequiredService<ProjectProcessLaunchDeliveryService>()
            .GetStatusAsync(saved.Preparation.AdmissionId)).State);
    }

    private static AgentDefinition ReadOnlySource(AgentDefinition agent) => ChangeSourceAccess(agent, access => {
        access.CanWrite = false;
        access.CanWriteNonTaskStructure = false;
        access.CanWriteTasks = false;
    });

    private static AgentDefinition ChangeSourceAccess(AgentDefinition agent, Action<AgentProjectStructureAccessSettings> change) {
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        change(access);
        return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) };
    }

    private static Func<AgentToolResultDisclosure, CancellationToken, ValueTask<IAsyncDisposable?>> DisclosureCallback(
        IServiceProvider services, Fixture fixture) => Assert.Single(services.GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()).GetToolMetadata(fixture.Context)
            .Single(metadata => metadata.ToolName == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart).AuthorizeResultDisclosureAsync!;

    private static ProjectStructureProcessNodeStartResult ReadResult(AgentToolResultDisclosure disclosure)
        => disclosure.Result.Deserialize<ProjectStructureProcessNodeStartResult>(ProjectStructureProcessProposalCodec.SerializerOptions)!;

    private static async Task<AgentToolResultDisclosure> CompleteForDisclosureAsync(Fixture fixture,
        AgentToolAdmissionJournal journal, AgentToolRunLease lease, AgentToolEffectState effect = AgentToolEffectState.Committed) {
        var batch = await fixture.AdmitAsync(journal, lease, "start");
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "start", fixture.Payload, default);
        ProjectStructureProcessNodeStartResult result;
        using (claim.Bind()) {
            result = await fixture.StartAsync(await fixture.RequireAndSeedAsync());
        }
        var value = JsonSerializer.SerializeToElement(result, ProjectStructureProcessProposalCodec.SerializerOptions);
        await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(value.GetRawText()), effect, default);
        Assert.NotEqual(Guid.Empty, claim.Proposal.IntentId.Value);
        Assert.NotNull(result.RunId);
        Assert.NotNull(result.Observation);
        return new(claim.Proposal.IntentId, fixture.Payload, effect, value);
    }
}
