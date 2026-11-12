using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
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
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Historical_hierarchy_pages_registered_original_creations_and_edges_with_current_source_and_executor_authority(
        bool process, bool revokeTarget) => VerifyHistoricalCreatedTargetPagesAsync(process, revokeTarget, definitions: false);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Historical_created_definition_pages_recover_original_unresolved_edits_with_current_source_and_executor_authority(
        bool process, bool revokeTarget) => VerifyHistoricalCreatedTargetPagesAsync(process, revokeTarget, definitions: true);

    private static async Task VerifyHistoricalCreatedTargetPagesAsync(bool process, bool revokeTarget, bool definitions) {
        var clock = new NativeClock();
        var harness = ToolProducerHarness(clock);
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = registration => {
            harness.ConfigureServices!(registration);
            registration.AddScoped(provider => new EfProcessExecutionAuthorityQuery(
                provider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>(), clock));
        } });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seed = await SeedHistoricalReceiptsAsync(services, clock, process, receiptCount: 5, hierarchy: !definitions,
            createdDefinitions: definitions, unresolvedLast: definitions);
        var originalRun = await HistoricalFileDigestAsync(seed.RunPath);
        if (process) {
            Assert.NotEqual(seed.Owner.Agent.Id, seed.Run.AgentId);
            var payload = seed.Run.ToolAdmission!.Batches[^1].Proposals[0].Payload;
            var source = definitions ? ProjectDefinitionToolProposalCodec.Read(payload).Source : ProjectLifecycleToolProposalCodec.Read(payload).Source;
            var dispatch = Assert.IsType<ProcessExecutionDispatchAuthority>(source.Process);
            await using var database = seed.Owner.Context();
            var claim = await database.DispatchClaims.AsNoTracking().SingleAsync(item => item.ClaimToken == dispatch.Evidence.DispatchClaimToken);
            clock.Now = claim.ExpiresAtUtc.AddSeconds(1);
            var observed = await services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>().ObserveOriginalForReceiptReadAsync(dispatch.Evidence);
            Assert.NotNull(observed.Snapshot);
            Assert.False(observed.Snapshot.ObservedCurrentDispatch);
        }
        var reader = services.GetRequiredService<IAgentHistoricalReceiptReviewService>();
        var first = await reader.ReadSavedEffectReceiptsAsync(seed.Request);
        Assert.Equal(AgentHistoricalReceiptReviewStop.LookupLimit, first.Stop);
        Assert.Equal(seed.Intents.Take(4), first.Observations.Select(item => item.Binding.IntentId));
        Assert.All(first.Observations, item => Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, item.Knowledge));
        var cursor = Assert.IsType<AgentHistoricalReceiptCursor>(first.NextCursor);
        Assert.Equal(4, cursor.NextProposalOrdinal);
        var request = new AgentHistoricalReceiptReviewRequest(seed.Run.Id, seed.Run.ChatSessionId, seed.Run.AgentId, seed.Run.CreatedAtUtc, cursor);
        var last = await reader.ReadSavedEffectReceiptsAsync(request);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, last.Stop);
        Assert.Null(last.NextCursor);
        Assert.Equal(seed.Intents[^1], Assert.Single(last.Observations).Binding.IntentId);
        Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, last.Observations[0].Knowledge);
        await seed.Owner.RevokeAsync(Revocation.Write);
        var readOnly = await reader.ReadSavedEffectReceiptsAsync(request);
        Assert.Equal(last.Observations, readOnly.Observations);
        Assert.Equal(last.Stop, readOnly.Stop);
        if (revokeTarget) {
            var payload = seed.Run.ToolAdmission!.Batches[^1].Proposals[0].Payload;
            ProjectWriteAdmission target;
            if (definitions) {
                target = ProjectDefinitionToolProposalCodec.Read(payload).Input.Request.ExpectedDefinition!.Project;
            } else {
                var (source, input) = ProjectLifecycleToolProposalCodec.Read(payload);
                var original = ProjectLifecycleToolProposalCodec.Submission(source, seed.Intents[^1], input);
                target = Assert.IsType<ProjectLifecycleCommand.ReconnectLink>(original.Command).Expected.Child;
            }
            await services.GetRequiredService<IAgentFrameworkWorkspaceService>().RevokeAgentProjectStructureLifetimeAsync(seed.Run.AgentId,
                new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId));
        } else {
            await seed.Owner.RevokeAsync(Revocation.Read);
        }
        var denied = await reader.ReadSavedEffectReceiptsAsync(request);
        if (revokeTarget) {
            Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, denied.Stop);
            Assert.Equal(AgentHistoricalReceiptKnowledge.ReadDenied, Assert.Single(denied.Observations).Knowledge);
            Assert.Equal(seed.Intents[^1], denied.Observations[0].Binding.IntentId);
        } else {
            Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, denied.Stop);
            Assert.Empty(denied.Observations);
        }
        Assert.Null(denied.NextCursor);
        Assert.Equal(originalRun, await HistoricalFileDigestAsync(seed.RunPath));
        await AssertHistoricalReceiptsUnchangedAsync(services, seed);
    }

    private static async Task<IReadOnlyList<AgentToolBusinessIntentId>> SeedHistoricalCreatedTargetOperationsAsync(IServiceProvider services, Fixture owner,
        AgentToolAdmissionJournal journal, AgentToolRunLease lease, IAgentRuntimeToolProvider provider,
        AgentRuntimeToolProviderContext context, IReadOnlyList<AITool> tools, bool process, bool definitions, bool unresolvedLast) {
        List<AgentToolBusinessIntentId> intents = [];
        List<ProjectWriteAdmission> targets = [];
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        for (var index = 0; index < 3; index++) {
            Dictionary<string, object?> arguments = new() {
                ["request"] = new ProjectStructureProjectSaveRequest($"Historical hierarchy target {index}", "", "", "") {
                    ExpectedParentAdmission = process ? null : new(owner.Project.DatabaseProfileId, owner.Project.ProjectId, owner.Project.LifetimeId)
                }
            };
            if (!process) {
                arguments["parentProjectId"] = owner.Project.ProjectId;
            }
            var created = await ProjectStructureResultDisclosureIntegrationTests.InvokeHierarchyTestToolAsync(journal, lease, provider,
                context, tools, process ? ProjectStructureToolPolicy.ProjectStructureProjectCreate : ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
                arguments, default);
            var id = created.Result.Deserialize<ProjectSummary>(ProjectDefinitionToolProposalCodec.Json)!.Id;
            targets.Add((await admissions.CaptureAsync(id))!);
            intents.Add(created.Intent);
        }
        if (definitions) {
            for (var index = 0; index < 2; index++) {
                var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(targets[index].ProjectId)).Value!;
                Dictionary<string, object?> arguments = new() {
                    ["projectId"] = targets[index].ProjectId,
                    ["request"] = new ProjectStructureProjectSaveRequest($"Historical created target edit {index}", "", "", "") {
                        ExpectedDefinition = original.Expectation
                    }
                };
                var edited = await ProjectStructureResultDisclosureIntegrationTests.InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
                    ProjectStructureToolPolicy.ProjectStructureProjectUpdate, arguments, default, complete: index == 0 || !unresolvedLast);
                intents.Add(edited.Intent);
            }
            return intents;
        }
        var linked = await ProjectStructureResultDisclosureIntegrationTests.InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, Arguments(targets[0], targets[1]), default);
        intents.Add(linked.Intent);
        var edge = linked.Result.Deserialize<ProjectStructureHierarchyAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)!;
        var reconnected = await ProjectStructureResultDisclosureIntegrationTests.InvokeHierarchyTestToolAsync(journal, lease, provider, context, tools,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, Arguments(targets[2], targets[1], new(edge.EdgeId, targets[0], targets[1])), default);
        Assert.True(reconnected.Result.Deserialize<ProjectStructureHierarchyAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)!.Reconnected);
        intents.Add(reconnected.Intent);
        return intents;

        static Dictionary<string, object?> Arguments(ProjectWriteAdmission parent, ProjectWriteAdmission child,
            ProjectHierarchyEdgeExpectation? previous = null) => new() {
            ["parentProjectId"] = parent.ProjectId,
            ["request"] = new ProjectStructureSubprojectChangeRequest(child.ProjectId, previous?.Parent.ProjectId) {
                ExpectedParentAdmission = parent, ExpectedChildAdmission = child, ExpectedCurrentEdge = previous
            }
        };
    }
}
