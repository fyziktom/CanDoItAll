using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_created_definition_target_can_be_edited_and_replayed_without_broadening_the_captured_source(bool projectScope) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateProjectAsync(services, "Original definition source");
        await using var fixture = projectScope ? await CreateJournalAsync(services, source) : await CreateDefinitionJournalAsync(services);
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, source);
        var journal = fixture.NewJournal();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await using var lease = await journal.AcquireRunAsync(fixture.Session, deadline.Token);
        using var bound = lease.Bind();
        var context = Context(fixture, actor, source.ProjectId);
        var provider = MetadataProvider(services);
        var tools = await provider.CreateToolsAsync(context, deadline.Token);
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var created = await InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            projectScope ? ProjectStructureToolPolicy.ProjectStructureSubprojectCreate : ProjectStructureToolPolicy.ProjectStructureProjectCreate,
            DefinitionArguments(projectScope ? ProjectStructureToolPolicy.ProjectStructureSubprojectCreate : ProjectStructureToolPolicy.ProjectStructureProjectCreate,
                source.ProjectId, new("Original created definition", "", "", "") { ExpectedParentAdmission = projectScope ? source : null }), deadline.Token);
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var target = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(
            created.Result.Deserialize<ProjectSummary>(ProjectDefinitionToolProposalCodec.Json)!.Id, deadline.Token));
        if (!projectScope) {
            var child = await InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
                ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
                DefinitionArguments(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate, target.ProjectId,
                    new("Child of original creation", "", "", "") { ExpectedParentAdmission = target }), deadline.Token);
            target = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(
                child.Result.Deserialize<ProjectSummary>(ProjectDefinitionToolProposalCodec.Json)!.Id, deadline.Token));
        }
        var projects = services.GetRequiredService<ProjectsService>();
        var original = (await projects.GetDefinitionAsync(target.ProjectId, deadline.Token)).Value!;
        var arguments = DefinitionArguments(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, target.ProjectId,
            new("Accepted created target edit", "", "", "") { ExpectedDefinition = original.Expectation });
        var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
        var payload = metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
        var captured = ProjectDefinitionToolProposalCodec.Read(payload).Source;
        Assert.Equal(source.ProjectId, Assert.Single(captured.Agent!.Access.AllowedProjectIds));
        var batch = (await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("created-definition", payload, false)], deadline.Token)).Batches[^1];
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "created-definition", payload, deadline.Token);
        var update = Assert.Single(tools.OfType<AIFunction>(), tool => tool.Name == metadata.ToolName);
        JsonElement accepted;
        using (claim.Bind()) {
            using var effect = AgentToolInvocationEffectScope.Begin();
            accepted = Assert.IsType<JsonElement>(await update.InvokeAsync(new AIFunctionArguments(arguments), deadline.Token));
            Assert.Equal(claim.Binding.IntentId.Value.ToString("D"), effect.CommittedEffect?.SourceId);
        }
        var later = await projects.GetAsync(target.ProjectId, deadline.Token);
        later.Name = "Later human target edit";
        Assert.True((await projects.SaveAsync(later, deadline.Token)).IsSuccess);
        var laterExpectation = (await projects.GetDefinitionAsync(target.ProjectId, deadline.Token)).Value!.Expectation;
        var retry = await journal.ClaimInvocationAsync(lease, batch.Id, "created-definition", payload, deadline.Token);
        using (retry.Bind()) {
            var replay = Assert.IsType<JsonElement>(await update.InvokeAsync(new AIFunctionArguments(arguments), deadline.Token));
            Assert.Equal(accepted.GetRawText(), replay.GetRawText());
            await journal.CompleteInvocationAsync(retry, AgentToolAdmissionJournalFixture.Envelope(replay.GetRawText()), AgentToolEffectState.Committed, deadline.Token);
        }
        var completed = await journal.ClaimInvocationAsync(lease, batch.Id, "created-definition", payload, deadline.Token);
        Assert.Equal(AgentToolProposalState.Completed, completed.Proposal.State);
        using (completed.Bind()) {
            var replay = Assert.IsType<JsonElement>(await update.InvokeAsync(new AIFunctionArguments(arguments), deadline.Token));
            Assert.Equal(accepted.GetRawText(), replay.GetRawText());
        }
        await using (var disclosure = await metadata.AuthorizeResultDisclosureAsync!(
            new(completed.Binding.IntentId, payload, AgentToolEffectState.Committed, accepted), deadline.Token)) {
            Assert.NotNull(disclosure);
        }
        await services.GetRequiredService<IAgentFrameworkWorkspaceService>().RevokeAgentProjectStructureLifetimeAsync(actor.Id,
            new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId), deadline.Token);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => metadata.AuthorizeResultDisclosureAsync!(
            new(completed.Binding.IntentId, payload, AgentToolEffectState.Committed, accepted), deadline.Token).AsTask());
        Assert.Equal(laterExpectation, (await projects.GetDefinitionAsync(target.ProjectId, deadline.Token)).Value!.Expectation);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token);
        Assert.Equal(1, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync(row => row.OperationId == claim.Binding.IntentId.Value, deadline.Token));
        Assert.Equal(projectScope ? 1 : 2, await database.Set<ProjectCreationReservationRecord>().CountAsync(deadline.Token));
    }

    [Fact]
    public async Task Project_scoped_creation_refuses_a_grandchild_before_reservation_or_grant_publication() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateProjectAsync(services, "Bounded creation scope");
        await using var fixture = await CreateJournalAsync(services, source);
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, source);
        var journal = fixture.NewJournal();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var lease = await journal.AcquireRunAsync(fixture.Session, deadline.Token);
        using var bound = lease.Bind();
        var context = Context(fixture, actor, source.ProjectId);
        var provider = MetadataProvider(services);
        var tools = await provider.CreateToolsAsync(context, deadline.Token);
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var child = await InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
            DefinitionArguments(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate, source.ProjectId,
                new("Direct child", "", "", "") { ExpectedParentAdmission = source }), deadline.Token);
        var target = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(
            child.Result.Deserialize<ProjectSummary>(ProjectDefinitionToolProposalCodec.Json)!.Id, deadline.Token));
        var catalog = services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        var before = (await catalog.LoadCatalogSnapshotAsync(deadline.Token)).Catalog.Agents.Single(item => item.Id == actor.Id).ConfigurationJson;
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => InvokeHierarchyTestToolAsync(journal, lease,
            provider, context, tools, ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
            DefinitionArguments(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate, target.ProjectId,
                new("Must not reserve grandchild", "", "", "") { ExpectedParentAdmission = target }), deadline.Token));
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        Assert.Equal(before, (await catalog.LoadCatalogSnapshotAsync(deadline.Token)).Catalog.Agents.Single(item => item.Id == actor.Id).ConfigurationJson);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token);
        Assert.Equal(1, await database.Set<ProjectCreationReservationRecord>().CountAsync(deadline.Token));
        Assert.Equal(1, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync(deadline.Token));
        Assert.Equal(2, await database.Set<Project>().CountAsync(deadline.Token));
    }
}
