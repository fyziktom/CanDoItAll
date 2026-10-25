using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
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
    public async Task Governed_hierarchy_uses_three_original_creations_and_recovers_exact_receipt_with_current_read_only_access(bool projectScope) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var sourceProject = await CreateProjectAsync(services, "Original hierarchy source");
        await using var fixture = projectScope ? await CreateJournalAsync(services, sourceProject) : await CreateDefinitionJournalAsync(services);
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, sourceProject);
        var journal = fixture.NewJournal();
        Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Session.ExecutionRunId));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await using var lease = await journal.AcquireRunAsync(fixture.Session, deadline.Token);
        using var bound = lease.Bind();
        var context = Context(fixture, actor, sourceProject.ProjectId);
        var provider = MetadataProvider(services);
        var tools = await provider.CreateToolsAsync(context, deadline.Token);
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var created = new List<ProjectWriteAdmission>();
        for (var index = 0; index < 3; index++) {
            var tool = projectScope ? ProjectStructureToolPolicy.ProjectStructureSubprojectCreate : ProjectStructureToolPolicy.ProjectStructureProjectCreate;
            var request = new ProjectStructureProjectSaveRequest($"Original hierarchy target {index}", "", "", "") {
                ExpectedParentAdmission = projectScope ? sourceProject : null
            };
            var result = await InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools, tool,
                DefinitionArguments(tool, sourceProject.ProjectId, request), deadline.Token);
            var summary = result.Result.Deserialize<ProjectSummary>(ProjectDefinitionToolProposalCodec.Json)!;
            created.Add((await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(summary.Id, deadline.Token))!);
        }
        var linkArguments = HierarchyArguments(created[0], created[1]);
        var link = await InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, linkArguments, deadline.Token);
        var linked = link.Result.Deserialize<ProjectStructureHierarchyAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)!;
        Assert.True(linked.Created);
        Assert.False(linked.Reconnected);
        var reconnectArguments = HierarchyArguments(created[2], created[1], new(linked.EdgeId, created[0], created[1]));
        var reconnect = await InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, reconnectArguments, deadline.Token);
        var reconnected = reconnect.Result.Deserialize<ProjectStructureHierarchyAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)!;
        Assert.True(reconnected.Reconnected);
        Assert.NotEqual(linked.EdgeId, reconnected.EdgeId);
        await using (var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token)) {
            Assert.Equal(2, await database.Set<ProjectLifecycleReceiptRecord>().CountAsync(deadline.Token));
            Assert.Equal(3, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync(deadline.Token));
            Assert.False(await database.Set<ProjectHierarchyLink>().AnyAsync(item => item.Id == linked.EdgeId, deadline.Token));
            Assert.True(await database.Set<ProjectHierarchyLink>().AnyAsync(item => item.Id == reconnected.EdgeId, deadline.Token));
        }
        var originalSource = ProjectLifecycleToolProposalCodec.Read(reconnect.Payload).Source;
        Assert.Equal(sourceProject.ProjectId, Assert.Single(originalSource.Agent!.Access.AllowedProjectIds));
        var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureSubprojectLink);
        var catalog = services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        await catalog.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => {
                if (agent.Id != actor.Id) {
                    return agent;
                }
                var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
                access.CanWriteNonTaskStructure = false;
                access.CanCreateProjects = false;
                access.CanCreateSubprojects = false;
                return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) };
            }).ToArray()
        }, deadline.Token);
        await using (var disclosure = await metadata.AuthorizeResultDisclosureAsync!(
            new(reconnect.Intent, reconnect.Payload, AgentToolEffectState.Committed, reconnect.Result), deadline.Token)) {
            Assert.NotNull(disclosure);
        }
        await services.GetRequiredService<IAgentFrameworkWorkspaceService>().RevokeAgentProjectStructureLifetimeAsync(actor.Id,
            new(created[1].DatabaseProfileId, created[1].ProjectId, created[1].LifetimeId), deadline.Token);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => metadata.AuthorizeResultDisclosureAsync!(
            new(reconnect.Intent, reconnect.Payload, AgentToolEffectState.Committed, reconnect.Result), deadline.Token).AsTask());
        await using var final = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token);
        Assert.Equal(2, await final.Set<ProjectLifecycleReceiptRecord>().CountAsync(deadline.Token));
    }

    [Fact]
    public async Task A_saved_success_result_and_current_grant_cannot_replace_the_original_creation_receipt_for_hierarchy_authority() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parent = await CreateProjectAsync(services, "Original authority parent");
        await using var fixture = await CreateDefinitionJournalAsync(services);
        var actor = await SaveDefinitionActorAsync(services, fixture.Agent, parent);
        var journal = fixture.NewJournal();
        Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Session.ExecutionRunId));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var lease = await journal.AcquireRunAsync(fixture.Session, deadline.Token);
        using var bound = lease.Bind();
        var context = Context(fixture, actor, parent.ProjectId);
        var provider = MetadataProvider(services);
        var tools = await provider.CreateToolsAsync(context, deadline.Token);
        var creationMetadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectCreate);
        var payload = creationMetadata.PrepareAdmission!(JsonSerializer.SerializeToElement(
            DefinitionArguments(creationMetadata.ToolName, parent.ProjectId, new("Unproven target", "", "", "")), ProjectDefinitionToolProposalCodec.Json));
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var batch = (await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("unproven-creation", payload, false)], deadline.Token)).Batches[^1];
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "unproven-creation", payload, deadline.Token);
        var source = ProjectDefinitionToolProposalCodec.Read(payload).Source;
        var id = ProjectDefinitionToolProposalCodec.ReservedProjectId(source, claim.Binding.IntentId);
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(id, new() { Name = "Unrelated native creation" }, deadline.Token)).IsSuccess);
        var target = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id, deadline.Token))!;
        await services.GetRequiredService<IAgentFrameworkWorkspaceService>().GrantAgentProjectStructureLifetimeAsync(actor.Id,
            new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId), deadline.Token);
        using (claim.Bind()) {
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope("{\"succeeded\":true}"),
                AgentToolEffectState.Committed, deadline.Token);
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, HierarchyArguments(parent, target), deadline.Token));
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token);
        Assert.Equal(0, await database.Set<ProjectLifecycleReceiptRecord>().CountAsync(deadline.Token));
        Assert.Equal(0, await database.Set<ProjectHierarchyLink>().CountAsync(deadline.Token));
    }

    private static Dictionary<string, object?> HierarchyArguments(ProjectWriteAdmission parent, ProjectWriteAdmission child,
        ProjectHierarchyEdgeExpectation? previous = null) => new() {
        ["parentProjectId"] = parent.ProjectId,
        ["request"] = new ProjectStructureSubprojectChangeRequest(child.ProjectId, previous?.Parent.ProjectId) {
            ExpectedParentAdmission = parent, ExpectedChildAdmission = child, ExpectedCurrentEdge = previous
        }
    };

    internal static async Task<(JsonElement Result, AgentToolPreparedPayload Payload, AgentToolBusinessIntentId Intent)> InvokeHierarchyTestToolAsync(
        AgentToolAdmissionJournal journal, AgentToolRunLease lease, IAgentRuntimeToolProvider provider, AgentRuntimeToolProviderContext context,
        IReadOnlyList<AITool> tools, string tool, Dictionary<string, object?> arguments, CancellationToken cancellationToken, bool complete = true) {
        var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == tool);
        var payload = metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
        var batch = (await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("original-effect", payload, false)], cancellationToken)).Batches[^1];
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "original-effect", payload, cancellationToken);
        using var dispatch = claim.Bind();
        using var effects = AgentToolInvocationEffectScope.Begin();
        var result = Assert.IsType<JsonElement>(await Assert.Single(tools.OfType<AIFunction>(), item => item.Name == tool)
            .InvokeAsync(new AIFunctionArguments(arguments), cancellationToken));
        Assert.NotNull(effects.CommittedEffect);
        Assert.Equal(claim.Binding.IntentId.Value.ToString("D"), effects.CommittedEffect.SourceId);
        if (complete) {
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(result.GetRawText()), AgentToolEffectState.Committed, cancellationToken);
        }
        return (result, payload, claim.Binding.IntentId);
    }
}
