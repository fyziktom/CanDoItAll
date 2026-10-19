using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Expired_write_source_with_current_read_can_confirm_an_original_effect() {
        var clock = new NativeClock();
        await using var application = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false,
            authority: await CaptureProjectOperationsAsync(fixture));
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var journal = NewJournal();
        var initial = await journal.CreateForNewBackgroundRunAsync(run, "Update the original reviewed definition.");
        run = run with { ToolAdmission = initial with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, Support = AgentToolAdmissionSupport.EffectJournalOnly
        } };
        await store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var context = await ToolProducerContextAsync(fixture, run);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        var request = new ProjectStructureProjectSaveRequest("Accepted before interrupted receipt recovery", "", "", "") {
            ExpectedDefinition = original.Expectation
        };
        Dictionary<string, object?> arguments = new() { ["projectId"] = fixture.Project.ProjectId, ["request"] = request };
        var wire = JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json);
        AgentToolPreparedPayload payload;
        AgentToolBusinessIntentId intent;
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run);
        await using (var lease = await journal.AcquireRunAsync(run.ToolAdmission.Session.Reference, default)) {
            using var bound = lease.Bind();
            await journal.BeginEffectSegmentAsync(lease, default);
            var tools = await provider.CreateToolsAsync(context, default);
            var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
            payload = metadata.PrepareAdmission!(wire);
            var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("accepted-before-interruption", payload, false)], default)).Batches);
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, batch.Proposals[0].CallId, payload, default);
            intent = claim.Proposal.IntentId;
            using var dispatch = claim.Bind();
            using var effect = AgentToolInvocationEffectScope.Begin();
            Assert.IsType<JsonElement>(await Assert.Single(tools.OfType<AIFunction>(), item => item.Name == metadata.ToolName)
                .InvokeAsync(new AIFunctionArguments(arguments)));
            Assert.Equal(new AgentToolCommittedEffect(ProjectDefinitionToolAdmission.EffectSourceKind, intent.Value.ToString("D")), effect.CommittedEffect);
        }
        var accepted = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        var savedOriginal = (await store.GetExecutionRunAsync(run.Id))!.ToolAdmission!;
        Assert.Equal(AgentToolProposalState.Executing, savedOriginal.Batches[0].Proposals[0].State);
        Assert.Null(savedOriginal.Batches[0].Proposals[0].Result);
        clock.Now = clock.Now.AddHours(2);
        await fixture.RevokeAsync(Revocation.Write);
        var expired = await services.GetRequiredService<ProjectProcessExecutionMutationService>().ReadAccessAsync(run.Id);
        Assert.NotNull(expired);
        Assert.True(expired.CanRead);
        Assert.False(expired.CanWrite);
        Assert.False(expired.Dispatch.ObservedCurrentDispatch);

        var forbiddenStore = DispatchProxy.Create<ISandboxWorkspaceExecutionRunStore, ReceiptRunReadRefusal>();
        var reader = new ReceiptProcessReader(new ProcessExecutionProjectAuthorityReader(forbiddenStore,
            new(new Factory<ProcessPersistenceDbContext>(NativeOptions<ProcessPersistenceDbContext>(services), static options => new(options)), clock),
            services.GetRequiredService<ICanonicalRuntimeDatabase>()));
        var transactions = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var processes = new ProjectProcessExecutionMutationService(reader,
            new EfProcessExecutionMutationGuard(NativeOptions<ProcessPersistenceDbContext>(services), transactions, clock),
            NativeAuthority(services, clock), transactions);
        var recovery = new ProjectDefinitionToolReceiptRecovery(services.GetRequiredService<IAgentCatalogReadLeaseStore>(), processes,
            services.GetRequiredService<ProjectWriteAdmissionService>(), services.GetRequiredService<ProjectDefinitionReceiptReader>(), services.GetRequiredService<ProjectCreatedTargetProvenance>());
        var restarted = NewJournal();
        await using (var lease = await restarted.AcquireInterruptedReconciliationAsync(run.ToolAdmission.Session.Reference, default)) {
            using var bound = lease.Bind();
            var result = await restarted.ReconcileInterruptedAsync(lease, [recovery], (_, _) => new(true), default);
            Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
            Assert.Equal(1, result.Counts.KnownCommitted);
            Assert.Equal(intent, Assert.Single(result.Observations).Binding.IntentId);
            Assert.True(result.Observations[0].Persisted);
        }
        Assert.Equal(0, reader.GuidReads);
        Assert.True(reader.OriginalObservations > 0);
        var retained = (await store.GetExecutionRunAsync(run.Id))!.ToolAdmission!.Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed, retained.State);
        Assert.Equal(payload, retained.Payload);
        Assert.Equal(savedOriginal.Batches[0].Proposals[0].DispatchClaimId, retained.DispatchClaimId);
        Assert.Null(retained.Result);
        Assert.Equal(intent.Value.ToString("D"), retained.OwnerReceipt!.Effect.SourceId);
        var receipt = JsonSerializer.Deserialize<ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement>>(
            retained.OwnerReceipt.Receipt.PayloadJson, ProjectDefinitionToolProposalCodec.Json)!;
        Assert.Equal(accepted.Expectation, receipt.Acknowledgement.Definition);
        Assert.Equal(ProjectDefinitionToolProposalCodec.Read(payload).Source.Operation(intent), receipt.Operation);

        await fixture.RevokeAsync(Revocation.Read);
        await using (var lease = await restarted.AcquireInterruptedReconciliationAsync(run.ToolAdmission.Session.Reference, default)) {
            using var bound = lease.Bind();
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => restarted.ReconcileInterruptedAsync(lease, [recovery], (_, _) => new(true), default));
        }
        Assert.Equal(retained, (await store.GetExecutionRunAsync(run.Id))!.ToolAdmission!.Batches[0].Proposals[0]);
        Assert.Equal(accepted.Expectation, (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!.Expectation);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync());

        AgentToolAdmissionJournal NewJournal() => new(store, NativeProfile(services),
            backgroundSources: [NativeBackgroundPolicy(services, fixture, clock)]);
    }

    public class ReceiptRunReadRefusal : DispatchProxy {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException("Receipt-only Process observation must not reload the full execution record.");
    }

    private sealed class ReceiptProcessReader(ProcessExecutionProjectAuthorityReader reader) : IProcessExecutionDispatchAuthorityReader {
        internal int GuidReads { get; private set; }
        internal int OriginalObservations { get; private set; }
        public Task<ProcessExecutionDispatchAuthorityResult> ReadAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
            GuidReads++;
            return ((IProcessExecutionDispatchAuthorityReader)reader).ReadAsync(executionRunId, cancellationToken);
        }
        public Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalForReceiptReadAsync(ProcessExecutionClaimEvidence original,
            CancellationToken cancellationToken = default) {
            OriginalObservations++;
            return reader.ObserveOriginalForReceiptReadAsync(original, cancellationToken);
        }
    }
}
