using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registered_process_hierarchy_uses_executor_creation_grants_and_original_initiator_authority(bool revokeExecutor) {
        var clock = new NativeClock();
        await using var application = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false,
            authority: await CaptureProjectOperationsAsync(fixture));
        Assert.NotEqual(fixture.Agent.Id, run.AgentId);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var journal = new AgentToolAdmissionJournal(store, NativeProfile(services), clock, [NativeBackgroundPolicy(services, fixture, clock)]);
        run = run with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(run, "Create and connect the three reviewed projects.") };
        await store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var context = await ToolProducerContextAsync(fixture, run);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run);
        await using var lease = await journal.AcquireRunAsync(run.ToolAdmission.Session.Reference, deadline.Token);
        using var bound = lease.Bind();
        var tools = await provider.CreateToolsAsync(context, deadline.Token);
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, deadline.Token);
        var targets = new List<ProjectWriteAdmission>();
        for (var index = 0; index < 3; index++) {
            var created = await ProjectStructureResultDisclosureIntegrationTests.InvokeHierarchyTestToolAsync(journal, lease, provider,
                context, tools, ProjectStructureToolPolicy.ProjectStructureProjectCreate,
                new() { ["request"] = new ProjectStructureProjectSaveRequest($"Process hierarchy target {index}", "", "", "") }, deadline.Token);
            var id = created.Result.Deserialize<ProjectSummary>(ProjectDefinitionToolProposalCodec.Json)!.Id;
            targets.Add((await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id, deadline.Token))!);
        }
        await store.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => {
                if (agent.Id != run.AgentId) {
                    return agent;
                }
                var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
                access.CanWrite = false;
                access.CanWriteNonTaskStructure = false;
                return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) };
            }).ToArray()
        }, deadline.Token);
        var linked = await ProjectStructureResultDisclosureIntegrationTests.InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, Arguments(targets[0], targets[1]), deadline.Token);
        var edge = linked.Result.Deserialize<ProjectStructureHierarchyAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)!;
        var reconnected = await ProjectStructureResultDisclosureIntegrationTests.InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, Arguments(targets[2], targets[1], new(edge.EdgeId, targets[0], targets[1])), deadline.Token);
        Assert.True(reconnected.Result.Deserialize<ProjectStructureHierarchyAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)!.Reconnected);
        var catalog = await store.LoadCatalogAsync(deadline.Token);
        var originalSource = AgentProjectStructureAccessMetadata.Read(catalog.Agents.Single(agent => agent.Id == fixture.Agent.Id).ConfigurationJson);
        var executor = AgentProjectStructureAccessMetadata.Read(catalog.Agents.Single(agent => agent.Id == run.AgentId).ConfigurationJson);
        Assert.All(targets, target => Assert.DoesNotContain(target.ProjectId, originalSource.AllowedProjectIds));
        Assert.All(targets, target => Assert.Contains(new AgentProjectStructureLifetime(target.DatabaseProfileId, target.ProjectId, target.LifetimeId),
            executor.AllowedProjectLifetimes));
        Assert.True(executor.CanRead);
        Assert.False(executor.CanWrite);
        Assert.False(executor.CanWriteNonTaskStructure);
        clock.Now = clock.Now.AddHours(2);
        var metadata = Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureSubprojectLink);
        await using (var disclosure = await metadata.AuthorizeResultDisclosureAsync!(
            new(reconnected.Intent, reconnected.Payload, AgentToolEffectState.Committed, reconnected.Result), deadline.Token)) {
            Assert.NotNull(disclosure);
        }
        if (revokeExecutor) {
            await services.GetRequiredService<IAgentFrameworkWorkspaceService>().RevokeAgentProjectStructureLifetimeAsync(run.AgentId,
                new(targets[1].DatabaseProfileId, targets[1].ProjectId, targets[1].LifetimeId), deadline.Token);
        } else {
            await fixture.RevokeAsync(Revocation.Read);
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => metadata.AuthorizeResultDisclosureAsync!(
            new(reconnected.Intent, reconnected.Payload, AgentToolEffectState.Committed, reconnected.Result), deadline.Token).AsTask());
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(deadline.Token);
        Assert.Equal(2, await database.Set<ProjectLifecycleReceiptRecord>().CountAsync(deadline.Token));
        Assert.Equal(3, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync(deadline.Token));

        static Dictionary<string, object?> Arguments(ProjectWriteAdmission parent, ProjectWriteAdmission child,
            ProjectHierarchyEdgeExpectation? previous = null) => new() {
            ["parentProjectId"] = parent.ProjectId,
            ["request"] = new ProjectStructureSubprojectChangeRequest(child.ProjectId, previous?.Parent.ProjectId) {
                ExpectedParentAdmission = parent, ExpectedChildAdmission = child, ExpectedCurrentEdge = previous
            }
        };
    }
}
