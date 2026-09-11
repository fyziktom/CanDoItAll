using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    private const string FailureFollowupTaskCall = "approved-task-after-failed-lookup";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_failed_lookup_retains_original_scope_across_task_approval_and_lost_acknowledgement(bool revokeRead) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var alpha = await CreateProjectAsync(services, "Original failure Alpha");
        var beta = await CreateProjectAsync(services, "Unrelated Beta");
        await using var fixture = await CreateJournalAsync(services, alpha.ProjectId, requireApproval: true);
        var actor = await SaveMetadataActorAsync(services, fixture.Agent, alpha, structureWrite: false, taskWrite: true);
        Assert.True(actor.Permissions.RequiresApprovalForExternalCalls);
        Assert.False(actor.Permissions.AutoApproveExternalCallsByDefault);
        const string toolName = ProjectStructureToolPolicy.ProjectStructureAssetGet;
        var arguments = MissingAssetArguments(alpha.ProjectId);
        var start = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
        var task = new ProjectStructureTaskCreateRequest("One original Alpha task", start, start.AddHours(1));
        var initial = new ScriptClient(toolName, arguments, followupTask: task);
        var waiting = await ExecuteAsync(fixture, services, actor, initial);
        var approval = Assert.Single(waiting.PendingApprovals);
        Assert.Equal(ProjectStructureToolPolicy.ProjectTaskCreate, approval.ToolName);
        Assert.NotNull(approval.ToolAdmission);
        Assert.Equal(2, initial.Requests);
        var proposals = await ReadFailureProposalsAsync(fixture);
        var failed = Assert.Single(proposals, item => item.Payload.ToolName == toolName);
        AssertKnownLookupFailure(failed, alpha);
        var prepared = Assert.Single(proposals, item => item.Payload.ToolName == ProjectStructureToolPolicy.ProjectTaskCreate);
        Assert.Equal(AgentToolProposalState.Prepared, prepared.State);
        Assert.Equal(ExecutionApprovalStatus.Pending, prepared.ApprovalStatus);
        await AssertTaskAbsentAsync(services, alpha.ProjectId, task.Title);
        await AssertTaskAbsentAsync(services, beta.ProjectId, task.Title);
        var lookup = Assert.Single((await services.GetRequiredService<IProjectStructureAnalyticsService>()
            .QueryAsync(new(ProjectId: alpha.ProjectId, OperationName: "assets.get"))).Entries);
        Assert.False(lookup.Succeeded);
        Assert.Equal("NodeNotFound", lookup.ErrorCode);

        await fixture.ApproveAsync(waiting.PendingApprovals);
        var approved = Assert.Single(await ReadFailureProposalsAsync(fixture), item => item.IntentId == prepared.IntentId);
        Assert.Equal(AgentToolProposalState.Prepared, approved.State);
        Assert.Equal(ExecutionApprovalStatus.Approved, approved.ApprovalStatus);
        Assert.Equal(prepared.Payload.Digest, approved.ApprovedDigest);
        if (revokeRead) {
            await SaveActorAsync(services, actor, alpha, canRead: false);
            var deniedClient = new ScriptClient(toolName, arguments, followupTask: task);
            var denied = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, deniedClient));
            Assert.Contains("project-structure.result-disclosure-denied", Codes(denied));
            Assert.Equal(0, deniedClient.Requests);
            Assert.Equal(failed, Assert.Single(await ReadFailureProposalsAsync(fixture), item => item.IntentId == failed.IntentId));
            Assert.Equal(approved, Assert.Single(await ReadFailureProposalsAsync(fixture), item => item.IntentId == approved.IntentId));
            await AssertTaskAbsentAsync(services, alpha.ProjectId, task.Title);
            await AssertTaskAbsentAsync(services, beta.ProjectId, task.Title);
            await SaveMetadataActorAsync(services, actor, alpha, structureWrite: false, taskWrite: true);
        }

        var lostAck = new ScriptClient(toolName, arguments, failAfterResult: true, followupTask: task);
        var fault = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, lostAck));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(fault,
            "Injected final provider acknowledgement loss after the saved tool result.");
        Assert.Equal(1, lostAck.Requests);
        proposals = await ReadFailureProposalsAsync(fixture);
        Assert.Equal(failed, Assert.Single(proposals, item => item.IntentId == failed.IntentId));
        var completed = Assert.Single(proposals, item => item.IntentId == prepared.IntentId);
        Assert.Equal(AgentToolProposalState.Completed, completed.State);
        Assert.Equal(AgentToolEffectState.Committed, completed.EffectState);
        Assert.Equal(prepared.Payload, completed.Payload);
        var created = ReadCheckpointValue(completed).Deserialize<ProjectStructureTaskCreateResult>(MafToolProtocolCodec.SerializationOptions)!;
        Assert.False(string.IsNullOrWhiteSpace(created.TaskNodeId));
        var alphaSurface = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(alpha.ProjectId);
        Assert.Equal(created.TaskNodeId, Assert.Single(alphaSurface.Nodes, node => node.Title == task.Title).Id);
        await AssertTaskAbsentAsync(services, beta.ProjectId, task.Title);
        var editor = await services.GetRequiredService<ProjectsService>().GetAsync(alpha.ProjectId);
        editor.Name = "Human edited Alpha after task commit";
        Assert.True((await services.GetRequiredService<ProjectsService>().SaveAsync(editor)).IsSuccess);

        await using var restarted = application.Services.CreateAsyncScope();
        var readback = restarted.ServiceProvider;
        var replay = new ScriptClient(toolName, arguments, followupTask: task);
        Assert.Contains("completed", (await ExecuteAsync(fixture, readback, actor, replay)).ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, replay.Requests);
        Assert.Contains("NodeNotFound", Assert.Single(replay.Inputs), StringComparison.Ordinal);
        Assert.Contains(created.TaskNodeId, Assert.Single(replay.Inputs), StringComparison.Ordinal);
        Assert.Equal(failed, Assert.Single(await ReadFailureProposalsAsync(fixture), item => item.IntentId == failed.IntentId));
        Assert.Equal(completed, Assert.Single(await ReadFailureProposalsAsync(fixture), item => item.IntentId == completed.IntentId));
        alphaSurface = await readback.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(alpha.ProjectId);
        Assert.Equal(created.TaskNodeId, Assert.Single(alphaSurface.Nodes, node => node.Title == task.Title).Id);
        Assert.Equal("Human edited Alpha after task commit", (await readback.GetRequiredService<ProjectsService>().GetAsync(alpha.ProjectId)).Name);
        await AssertTaskAbsentAsync(readback, beta.ProjectId, task.Title);
        Assert.Equal(lookup, Assert.Single((await readback.GetRequiredService<IProjectStructureAnalyticsService>()
            .QueryAsync(new(ProjectId: alpha.ProjectId, OperationName: "assets.get"))).Entries));
        Assert.True(Assert.Single((await readback.GetRequiredService<IProjectStructureAnalyticsService>()
            .QueryAsync(new(ProjectId: alpha.ProjectId, OperationName: "tasks.create"))).Entries).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_failed_lookup_cannot_rebind_a_deleted_or_recreated_project(bool recreate) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Original failed lookup");
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var actor = await SaveActorAsync(services, fixture.Agent, project, canRead: true);
        const string toolName = ProjectStructureToolPolicy.ProjectStructureAssetGet;
        var arguments = MissingAssetArguments(project.ProjectId);
        var initial = new ScriptClient(toolName, arguments, failAfterResult: true);
        var fault = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, initial));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(fault,
            "Injected final provider acknowledgement loss after the saved tool result.");
        var saved = await ReadProposalAsync(fixture);
        AssertKnownLookupFailure(saved, project);
        var projects = services.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(project.ProjectId);
        if (recreate) {
            Assert.True((await projects.CreateAsync(project.ProjectId, new() { Name = "Different current project" })).IsSuccess);
            var replacement = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.ProjectId))!;
            Assert.NotEqual(project.LifetimeId, replacement.LifetimeId);
            actor = await SaveActorAsync(services, actor, replacement, canRead: true);
        }
        var retry = new ScriptClient(toolName, arguments);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, retry));
        Assert.Contains("project-structure.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, retry.Requests);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
        Assert.Contains(project, ProjectStructureDisclosureEvidenceCodec.Read(saved.DisclosureEvidence!).Targets);
        if (recreate) {
            Assert.Equal("Different current project", (await projects.GetAsync(project.ProjectId)).Name);
        }
    }

    [Fact]
    public async Task Known_failure_without_original_evidence_remains_unavailable_without_restamping() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Legacy failure provenance");
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var actor = await SaveActorAsync(services, fixture.Agent, project, canRead: true);
        var original = new ScriptClient(ProjectStructureToolPolicy.ProjectStructureAssetGet,
            MissingAssetArguments(project.ProjectId), failAfterResult: true);
        var fault = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, original));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(fault,
            "Injected final provider acknowledgement loss after the saved tool result.");
        var saved = await ReadProposalAsync(fixture);
        AssertKnownLookupFailure(saved, project);
        await using var lease = await fixture.NewJournal(fixture.NewStore()).AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var legacy = new CanDoItAll.AgentFramework.Tooling.AgentToolResultDisclosure(saved.IntentId, saved.Payload,
            saved.EffectState, ReadCheckpointValue(saved), Evidence: null);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            Disclosure(services, fixture).AuthorizeAsync(Context(fixture, actor, project.ProjectId), legacy, default).AsTask());
        Assert.Equal("tool-admission.disclosure-authorization-unavailable", denied.Code);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
        Assert.Equal(2, original.Requests);
    }

    [Theory]
    [InlineData(FailureDisposition.None)]
    [InlineData(FailureDisposition.NotCommitted)]
    [InlineData(FailureDisposition.Unknown)]
    [InlineData(FailureDisposition.Unsafe)]
    [InlineData(FailureDisposition.Cancelled)]
    [InlineData(FailureDisposition.Nested)]
    public async Task Only_a_direct_trusted_effect_free_failure_completes_original_owner_capture(FailureDisposition disposition) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Failure capture boundaries");
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var actor = await SaveActorAsync(services, fixture.Agent, project, canRead: true);
        var safe = ProjectStructureAgentException.CreateAgentVisible(404, "FixtureKnownFailure", "Safe fixture failure", true,
            effectState: disposition == FailureDisposition.None ? AgentToolEffectState.None :
                disposition == FailureDisposition.Unknown ? AgentToolEffectState.Unknown : AgentToolEffectState.NotCommitted);
        Exception failure = disposition switch {
            FailureDisposition.Unsafe => new ProjectStructureAgentException(404, "FixtureUnsafe", "Private diagnostic"),
            FailureDisposition.Cancelled => new OperationCanceledException("Cancellation remains cancellation"),
            FailureDisposition.Nested => new InvalidOperationException("An outer exception is not effect evidence", safe),
            _ => safe
        };
        await using var lease = await fixture.NewJournal(fixture.NewStore()).AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var function = AIFunctionFactory.Create((Guid projectId) => Task.FromException<object?>(failure),
            ProjectStructureToolPolicy.ProjectStructureAssetGet);
        var wrapped = Assert.IsAssignableFrom<AIFunction>(Assert.Single(Disclosure(services, fixture)
            .Wrap(Context(fixture, actor, project.ProjectId), [function], null)));
        using var effect = AgentToolInvocationEffectScope.Begin();
        var observed = await Assert.ThrowsAnyAsync<Exception>(() => wrapped.InvokeAsync(new() { ["projectId"] = project.ProjectId }).AsTask());
        Assert.Same(failure, observed);
        if (disposition is FailureDisposition.None or FailureDisposition.NotCommitted) {
            var evidence = ProjectStructureDisclosureEvidenceCodec.Read(effect.DisclosureEvidence!);
            Assert.Equal(ProjectStructureDisclosureState.Complete, evidence.State);
            Assert.Equal(project, Assert.Single(evidence.Targets));
            Assert.Equal(project.ProjectId, Assert.Single(evidence.DirectAccessProjectIds));
        } else {
            Assert.Null(effect.DisclosureEvidence);
        }
    }

    public enum FailureDisposition {
        None,
        NotCommitted,
        Unknown,
        Unsafe,
        Cancelled,
        Nested
    }

    private static Dictionary<string, object?> MissingAssetArguments(Guid projectId)
        => new() { ["projectId"] = projectId, ["nodeId"] = Guid.NewGuid().ToString("D") };

    private static async Task<AgentToolProposalRecord[]> ReadFailureProposalsAsync(AgentToolAdmissionJournalFixture fixture)
        => (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches
            .SelectMany(batch => batch.Proposals).ToArray();

    private static JsonElement ReadCheckpointValue(AgentToolProposalRecord proposal) {
        using var json = JsonDocument.Parse(proposal.Result!.PayloadJson);
        var checkpoint = json.RootElement.GetProperty("Value");
        return checkpoint.GetProperty(MafToolProtocolCodec.SerializationOptions.PropertyNamingPolicy?.ConvertName("Value") ?? "Value").Clone();
    }

    private static void AssertKnownLookupFailure(AgentToolProposalRecord proposal, ProjectWriteAdmission project) {
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolEffectState.None, proposal.EffectState);
        Assert.Equal(AgentToolProposalRecovery.RevalidateAndRead, proposal.Payload.Recovery);
        var result = ReadCheckpointValue(proposal).Deserialize<AgentToolFailureResult>(MafToolProtocolCodec.SerializationOptions)!;
        Assert.False(result.Succeeded);
        Assert.Equal("NodeNotFound", result.ErrorCode);
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        Assert.True(result.CanRetryWithCorrectedInput);
        var evidence = ProjectStructureDisclosureEvidenceCodec.Read(proposal.DisclosureEvidence!);
        Assert.Equal(ProjectStructureDisclosureState.Complete, evidence.State);
        Assert.Equal(project, Assert.Single(evidence.Targets));
        Assert.Equal(project.ProjectId, Assert.Single(evidence.DirectAccessProjectIds));
    }

    private static async Task AssertTaskAbsentAsync(IServiceProvider services, Guid projectId, string title) {
        var surface = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == title);
    }
}
