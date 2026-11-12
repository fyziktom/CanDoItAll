using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Registered_process_definition_tool_replays_original_receipt_after_dispatch_expiry_but_cannot_dispatch_a_fresh_effect() {
        var clock = new NativeClock();
        await using var application = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false,
            authority: await CaptureProjectOperationsAsync(fixture));
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var journal = NewJournal();
        run = run with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(run, "Update the reviewed project definition.") };
        await store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var context = await ToolProducerContextAsync(fixture, run);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var projects = services.GetRequiredService<ProjectsService>();
        var original = (await projects.GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        var request = new ProjectStructureProjectSaveRequest("Accepted Process definition", "", "", "") { ExpectedDefinition = original.Expectation };
        Dictionary<string, object?> arguments = new() { ["projectId"] = fixture.Project.ProjectId, ["request"] = request };
        var wire = JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json);
        AgentToolPreparedPayload prepared;
        AgentToolBatchId batchId;
        AgentToolBusinessIntentId intent;
        JsonElement accepted;
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run);
        await using (var lease = await journal.AcquireRunAsync(run.ToolAdmission!.Session.Reference, default)) {
            using var bound = lease.Bind();
            var tools = await provider.CreateToolsAsync(context, default);
            var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
            prepared = metadata.PrepareAdmission!(wire);
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var batch = Assert.Single((await journal.AdmitBatchAsync(lease, prepared.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("accepted-effect", prepared, false), new("distinct-effect", prepared, false)], default)).Batches);
            batchId = batch.Id;
            var claim = await journal.ClaimInvocationAsync(lease, batchId, "accepted-effect", prepared, default);
            intent = claim.Proposal.IntentId;
            using var dispatch = claim.Bind();
            using var effect = AgentToolInvocationEffectScope.Begin();
            accepted = Assert.IsType<JsonElement>(await Assert.Single(tools.OfType<AIFunction>(), item => item.Name == metadata.ToolName)
                .InvokeAsync(new AIFunctionArguments(arguments)));
            Assert.Equal(new AgentToolCommittedEffect(ProjectDefinitionToolAdmission.EffectSourceKind, intent.Value.ToString("D")), effect.CommittedEffect);
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(accepted.GetRawText()), AgentToolEffectState.Committed, default);
        }

        var editor = await projects.GetAsync(fixture.Project.ProjectId);
        editor.Name = "Later Process human definition";
        Assert.True((await projects.SaveAsync(editor)).IsSuccess);
        var later = (await projects.GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        clock.Now = clock.Now.AddHours(2);
        await fixture.RevokeAsync(Revocation.Write);
        var expired = await services.GetRequiredService<ProjectProcessExecutionMutationService>().ReadAccessAsync(run.Id);
        Assert.NotNull(expired);
        Assert.True(expired.CanRead);
        Assert.False(expired.CanWrite);
        Assert.False(expired.Dispatch.ObservedCurrentDispatch);
        var restarted = NewJournal();
        await using (var lease = await restarted.AcquireRunAsync(run.ToolAdmission!.Session.Reference, default)) {
            using var bound = lease.Bind();
            var restoredContext = await ToolProducerContextAsync(fixture, run);
            var tools = await provider.CreateToolsAsync(restoredContext, default);
            var metadata = Assert.Single(provider.GetToolMetadata(restoredContext), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
            var saved = Assert.Single((await store.GetExecutionRunAsync(run.Id))!.ToolAdmission!.Batches[0].Proposals,
                item => item.CallId == "accepted-effect");
            Assert.Equal(prepared, saved.Payload);
            var restored = metadata.RestoreAdmission!(wire, saved.Payload.SourcePreparation!);
            Assert.Equal(prepared, restored);
            var claim = await restarted.ClaimInvocationAsync(lease, batchId, "accepted-effect", restored, default);
            using var dispatch = claim.Bind();
            using var effect = AgentToolInvocationEffectScope.Begin();
            var replay = Assert.IsType<JsonElement>(await Assert.Single(tools.OfType<AIFunction>(), item => item.Name == metadata.ToolName)
                .InvokeAsync(new AIFunctionArguments(arguments)));
            Assert.Equal(accepted.GetRawText(), replay.GetRawText());
            Assert.Equal(new AgentToolCommittedEffect(ProjectDefinitionToolAdmission.EffectSourceKind, intent.Value.ToString("D")), effect.CommittedEffect);
            await using (var disclosed = await metadata.AuthorizeResultDisclosureAsync!(
                new(intent, restored, AgentToolEffectState.Committed, replay), default)) {
                Assert.NotNull(disclosed);
            }
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
                restarted.ClaimInvocationAsync(lease, batchId, "distinct-effect", prepared, default));
            await fixture.RevokeAsync(Revocation.Read);
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => metadata.AuthorizeResultDisclosureAsync!(
                new(intent, restored, AgentToolEffectState.Committed, replay), default).AsTask());
        }
        Assert.Equal(later.Expectation, (await projects.GetDefinitionAsync(fixture.Project.ProjectId)).Value!.Expectation);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync());

        AgentToolAdmissionJournal NewJournal() => new(store, NativeProfile(services),
            backgroundSources: [NativeBackgroundPolicy(services, fixture, clock)]);
    }
}
