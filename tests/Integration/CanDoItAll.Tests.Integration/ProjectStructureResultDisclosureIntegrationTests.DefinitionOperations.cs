using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(ProjectStructureToolPolicy.ProjectStructureProjectCreate)]
    [InlineData(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate)]
    [InlineData(ProjectStructureToolPolicy.ProjectStructureProjectUpdate)]
    public async Task Governed_definition_tools_restore_original_preparation_and_exact_receipt_after_restart_and_write_revocation(string toolName) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parent = await CreateProjectAsync(services, "Original definition parent");
        await using var fixture = await CreateDefinitionJournalAsync(services);
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, parent);
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(parent.ProjectId)).Value!;
        var request = new ProjectStructureProjectSaveRequest("Accepted original definition", "Original description", "Original goal", "Plan") {
            ExpectedDefinition = toolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate ? original.Expectation : null,
            ExpectedParentAdmission = toolName == ProjectStructureToolPolicy.ProjectStructureSubprojectCreate ? parent : null
        };
        var arguments = DefinitionArguments(toolName, parent.ProjectId, request);
        AgentToolPreparedPayload prepared;
        AgentToolBatchId batchId;
        AgentToolBusinessIntentId intent;
        JsonElement accepted;
        Guid targetId;
        var provider = MetadataProvider(services);
        var firstJournal = fixture.NewJournal();
        await using (var lease = await firstJournal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var context = Context(fixture, actor, parent.ProjectId);
            var tools = await provider.CreateToolsAsync(context, default);
            var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == toolName);
            prepared = metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
            await firstJournal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var batch = Assert.Single((await firstJournal.AdmitBatchAsync(lease, prepared.Digest,
                AgentToolAdmissionJournalFixture.Envelope(), [new("original-definition", prepared, false)], default)).Batches);
            batchId = batch.Id;
            var claim = await firstJournal.ClaimInvocationAsync(lease, batch.Id, "original-definition", prepared, default);
            intent = claim.Proposal.IntentId;
            using var dispatch = claim.Bind();
            using var effect = AgentToolInvocationEffectScope.Begin();
            accepted = Assert.IsType<JsonElement>(await Assert.Single(tools.OfType<AIFunction>(), item => item.Name == toolName)
                .InvokeAsync(new AIFunctionArguments(arguments)));
            Assert.Equal(new AgentToolCommittedEffect(ProjectDefinitionToolAdmission.EffectSourceKind, intent.Value.ToString("D")), effect.CommittedEffect);
            targetId = toolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate
                ? accepted.Deserialize<ProjectDefinitionScalarsAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)!.Definition.Project.ProjectId
                : accepted.Deserialize<ProjectSummary>(ProjectDefinitionToolProposalCodec.Json)!.Id;
        }

        var persisted = await ReadProposalAsync(fixture);
        Assert.Equal(AgentToolProposalState.Executing, persisted.State);
        Assert.Equal(prepared, persisted.Payload);
        Assert.NotNull(persisted.Payload.SourcePreparation);
        var projects = services.GetRequiredService<ProjectsService>();
        var editor = await projects.GetAsync(targetId);
        editor.Name = "Later human definition";
        Assert.True((await projects.SaveAsync(editor)).IsSuccess);
        var later = (await projects.GetDefinitionAsync(targetId)).Value!;
        var target = later.Expectation.Project;
        actor = await SaveActorAsync(services, actor, target, canRead: true);

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using (var lease = await restarted.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var context = Context(fixture, actor, parent.ProjectId);
            var tools = await provider.CreateToolsAsync(context, default);
            var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == toolName);
            var wire = JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json);
            var currentPreparation = metadata.PrepareAdmission!(wire);
            Assert.NotEqual(prepared.Digest, currentPreparation.Digest);
            var restored = metadata.RestoreAdmission!(wire, persisted.Payload.SourcePreparation!);
            Assert.Equal(prepared, restored);
            Assert.Equal(ProjectDefinitionToolProposalCodec.Read(prepared).Source.Operation(intent),
                ProjectDefinitionToolProposalCodec.Read(restored).Source.Operation(intent));
            var claim = await restarted.ClaimInvocationAsync(lease, batchId, "original-definition", restored, default);
            using var dispatch = claim.Bind();
            using var effect = AgentToolInvocationEffectScope.Begin();
            var replay = Assert.IsType<JsonElement>(await Assert.Single(tools.OfType<AIFunction>(), item => item.Name == toolName)
                .InvokeAsync(new AIFunctionArguments(arguments)));
            Assert.Equal(accepted.GetRawText(), replay.GetRawText());
            Assert.Equal(new AgentToolCommittedEffect(ProjectDefinitionToolAdmission.EffectSourceKind, intent.Value.ToString("D")), effect.CommittedEffect);
            await using (var disclosed = await metadata.AuthorizeResultDisclosureAsync!(
                new(intent, restored, AgentToolEffectState.Committed, replay), default)) {
                Assert.NotNull(disclosed);
            }
            await SaveActorAsync(services, actor, target, canRead: false);
            var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => metadata.AuthorizeResultDisclosureAsync!(
                new(intent, restored, AgentToolEffectState.Committed, replay), default).AsTask());
            Assert.Equal("projects.definition-tool-admission-denied", denied.Code);
        }

        Assert.Equal(later.Expectation, (await projects.GetDefinitionAsync(targetId)).Value!.Expectation);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync());
        if (toolName != ProjectStructureToolPolicy.ProjectStructureProjectUpdate) {
            var source = ProjectDefinitionToolProposalCodec.Read(prepared).Source;
            var reserved = await services.GetRequiredService<ProjectWriteAdmissionService>().FindCreationReservationAsync(intent.Value, actor.Id);
            Assert.NotNull(reserved);
            Assert.Equal(targetId, ProjectDefinitionToolProposalCodec.ReservedProjectId(source, intent));
            Assert.Equal(targetId, reserved.ProjectId);
            Assert.Equal(toolName == ProjectStructureToolPolicy.ProjectStructureSubprojectCreate ? parent.ProjectId : (Guid?)null, reserved.ParentProjectId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Governed_definition_first_effect_rechecks_current_source_and_supplied_lease(bool invalidLease) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Must remain unchanged");
        await using var fixture = await CreateDefinitionJournalAsync(services);
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, project);
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(project.ProjectId)).Value!;
        var request = new ProjectStructureProjectSaveRequest("Must not commit", "", "", "", LeaseToken: invalidLease ? "not-a-current-lease" : null) {
            ExpectedDefinition = original.Expectation
        };
        var arguments = DefinitionArguments(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, project.ProjectId, request);
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var context = Context(fixture, actor, project.ProjectId);
        var provider = MetadataProvider(services);
        var tools = await provider.CreateToolsAsync(context, default);
        var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
        var payload = metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("new-effect", payload, false)], default)).Batches);
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "new-effect", payload, default);
        if (!invalidLease) {
            await SaveActorAsync(services, actor, project, canRead: true);
        }
        using var dispatch = claim.Bind();
        using var effect = AgentToolInvocationEffectScope.Begin();
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            Assert.Single(tools.OfType<AIFunction>(), item => item.Name == metadata.ToolName).InvokeAsync(new AIFunctionArguments(arguments)).AsTask());
        if (invalidLease) {
            Assert.Equal("LeaseMissing", failure.ErrorCode);
        } else {
            Assert.Equal(AgentToolEffectState.NotCommitted, failure.EffectState);
        }
        Assert.Null(effect.CommittedEffect);
        Assert.Equal(original.Expectation, (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(project.ProjectId)).Value!.Expectation);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(0, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync());
    }

    [Fact]
    public async Task Actual_MAF_definition_update_restores_completed_accepted_result_without_reexecuting_after_read_revocation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Before MAF definition");
        await using var fixture = await CreateJournalAsync(services, project, requireApproval: true);
        var actor = await SaveMetadataActorAsync(services, fixture.Agent, project, true, false);
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(project.ProjectId)).Value!;
        var arguments = DefinitionArguments(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, project.ProjectId,
            new("Accepted MAF definition", "", "", "") { ExpectedDefinition = original.Expectation });
        var waiting = await ExecuteAsync(fixture, services, actor,
            new(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, arguments));
        var approval = Assert.Single(waiting.PendingApprovals);
        Assert.Equal(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, approval.ToolName);
        Assert.NotNull(approval.ToolAdmission);
        Assert.Equal(original.Expectation, (await services.GetRequiredService<ProjectsService>()
            .GetDefinitionAsync(project.ProjectId)).Value!.Expectation);
        await fixture.ApproveAsync(waiting.PendingApprovals);
        var lostAcknowledgement = new ScriptClient(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, arguments, failAfterResult: true);
        var loss = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, lostAcknowledgement));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(loss, "Injected final provider acknowledgement loss after the saved tool result.");
        Assert.Equal(1, lostAcknowledgement.Requests);
        Assert.Equal("Accepted MAF definition", (await services.GetRequiredService<ProjectsService>().GetAsync(project.ProjectId)).Name);
        var saved = await ReadProposalAsync(fixture);
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolEffectState.Committed, saved.EffectState);
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, saved.Payload.Recovery);
        Assert.NotNull(saved.Payload.SourcePreparation);
        var editor = await services.GetRequiredService<ProjectsService>().GetAsync(project.ProjectId);
        editor.Name = "Later MAF human definition";
        Assert.True((await services.GetRequiredService<ProjectsService>().SaveAsync(editor)).IsSuccess);
        await SaveActorAsync(services, actor, project, canRead: false);
        var deniedClient = new ScriptClient(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, arguments);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, deniedClient));
        Assert.Contains("projects.definition-tool-admission-denied", Codes(denied));
        Assert.Equal(0, deniedClient.Requests);
        actor = await SaveActorAsync(services, actor, project, canRead: true);
        var readOnlyPolicy = AgentProjectStructureAccessMetadata.Read(actor.ConfigurationJson);
        Assert.True(readOnlyPolicy.CanRead);
        Assert.False(readOnlyPolicy.CanWrite);
        Assert.False(readOnlyPolicy.CanWriteNonTaskStructure);
        var changedPolicyClient = new ScriptClient(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, arguments);
        var changedPolicy = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, actor, changedPolicyClient));
        Assert.Contains("tool-admission.runtime-denied", Codes(changedPolicy));
        Assert.Equal(0, changedPolicyClient.Requests);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
        Assert.Equal("Later MAF human definition", (await services.GetRequiredService<ProjectsService>().GetAsync(project.ProjectId)).Name);
        actor = await SaveMetadataActorAsync(services, actor, project, structureWrite: true, taskWrite: false);
        var resumedClient = new ScriptClient(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, arguments);
        await ExecuteAsync(fixture, services, actor, resumedClient);
        Assert.Equal(1, resumedClient.Requests);
        Assert.Contains("Accepted MAF definition", Assert.Single(resumedClient.Inputs), StringComparison.Ordinal);
        Assert.DoesNotContain("Later MAF human definition", resumedClient.Inputs[0], StringComparison.Ordinal);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
        Assert.Equal("Later MAF human definition", (await services.GetRequiredService<ProjectsService>().GetAsync(project.ProjectId)).Name);
    }

    private static Task<AgentToolAdmissionJournalFixture> CreateDefinitionJournalAsync(IServiceProvider services) {
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        return AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: new(profile.ActiveProfileId!.Value,
            profile.ActiveFingerprint!, new(profile.Generation)), transientContext: new("Organization definition editing",
                workspaceScope: WorkspaceScopeDescriptor.Organization(profile.ActiveProfileId.Value.ToString("N"))));
    }

    private static async Task<AgentDefinition> SaveDefinitionActorAsync(IServiceProvider services, AgentDefinition original, ProjectWriteAdmission project) {
        var actor = await SaveActorAsync(services, original, project, canRead: true);
        var access = AgentProjectStructureAccessMetadata.Read(actor.ConfigurationJson);
        access.CanWriteNonTaskStructure = true;
        access.CanCreateProjects = true;
        access.CanCreateSubprojects = true;
        actor = actor with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(actor.ConfigurationJson, access) };
        var catalog = await services.GetRequiredService<ISandboxWorkspaceCatalogStore>().UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Where(item => item.Id != actor.Id).Append(actor).ToArray()
        });
        return Assert.Single(catalog.Agents, item => item.Id == actor.Id);
    }

    private static Dictionary<string, object?> DefinitionArguments(string tool, Guid project, ProjectStructureProjectSaveRequest request) {
        Dictionary<string, object?> arguments = new() { ["request"] = request };
        if (tool == ProjectStructureToolPolicy.ProjectStructureProjectUpdate) {
            arguments["projectId"] = project;
        } else if (tool == ProjectStructureToolPolicy.ProjectStructureSubprojectCreate) {
            arguments["parentProjectId"] = project;
        }
        return arguments;
    }
}
