using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessPlanningLaunchApiTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, OnBasisChange.PauseForReview, false)]
    [InlineData(ReadBasisMode.StepSnapshot, OnBasisChange.PauseForReview, false)]
    [InlineData(ReadBasisMode.RunSnapshot, OnBasisChange.Fail, false)]
    [InlineData(ReadBasisMode.StepSnapshot, OnBasisChange.Fail, false)]
    [InlineData(ReadBasisMode.RunSnapshot, OnBasisChange.PauseForReview, true)]
    [InlineData(ReadBasisMode.StepSnapshot, OnBasisChange.PauseForReview, true)]
    [InlineData(ReadBasisMode.RunSnapshot, OnBasisChange.Fail, true)]
    [InlineData(ReadBasisMode.StepSnapshot, OnBasisChange.Fail, true)]
    public async Task Public_launch_preserves_reviewed_policy_and_accounts_original_publication_or_conflict(
        ReadBasisMode mode, OnBasisChange response, bool changeBasis) {
        await using var fixture = await ProcessPlanningLaunchFixture.CreateAsync(mode, response: response);
        using var preview = await fixture.PostAsync("/api/processes/launch/check", fixture.Request);
        using var recoveredResponse = await fixture.Host.Client.GetAsync($"/api/processes/launch/intents/{fixture.Request.CallerIntentId:D}");
        using var recovered = await ProcessPlanningLaunchFixture.ReadAsync(recoveredResponse);
        Assert.Equal(preview.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid(),
            recovered.RootElement.GetProperty("admissionId").GetGuid());
        Assert.Equal("Prepared", recovered.RootElement.GetProperty("continuationState").GetString());
        var original = await fixture.SavedAsync();
        var root = Assert.IsType<ProcessDecisionRunAdmission>(original.Preparation.InitialCommit.InitialRunDecisionAdmission);
        Assert.Equal(mode, root.Policy.ReadBasisMode);
        Assert.Equal(response, root.Policy.OnBasisChange);
        var stepAdmission = Assert.Single(original.Preparation.InitialCommit.InitialAssignments!).DecisionDataPolicy!;
        Assert.Equal(response == OnBasisChange.Fail ? OnBasisChange.Fail : (OnBasisChange?)null, stepAdmission.ChildOnBasisChange);
        Assert.Equal(fixture.Entry.Policy.Expectation.Namespace.Value, root.WorkspaceNamespaceId);
        Assert.Equal(fixture.Entry.Policy.Expectation.Head!.Revision, root.WorkspaceHeadRevision);
        Assert.Equal(root.Policy, preview.RootElement.GetProperty("launchPlan").GetProperty("decision").GetProperty("policy")
            .Deserialize<DecisionDataPolicy>(fixture.Json));
        Assert.Contains(ProcessHostCapabilityIds.ManagedProcessAdapter, original.Preparation.InitialCommit.InitialPlan!.Steps.Single().RequiredHostCapabilities);
        Assert.Contains(ProcessHostCapabilityIds.ManagedProcessAdapter, original.Preparation.InitialCommit.Mutation.State.Steps.Single().RequiredHostCapabilities);
        Assert.Empty(fixture.Entry.Wire.Requests);
        var nextMode = mode == ReadBasisMode.RunSnapshot ? ReadBasisMode.StepSnapshot : ReadBasisMode.RunSnapshot;
        var changed = await fixture.SetPolicyAsync(new(nextMode, PublicationFreshness.RequireCurrent, OnBasisChange.PauseForReview,
            new(0, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(6))));
        using var retryPreview = await fixture.PostAsync("/api/processes/launch/check", fixture.Request);
        var unchanged = await fixture.SavedAsync();
        Assert.Equal(original.PreparationFingerprint, unchanged.PreparationFingerprint);
        Assert.Equal(root.Funding, unchanged.Preparation.InitialCommit.InitialRunDecisionAdmission!.Funding);
        var newIntent = fixture.Request with { CallerIntentId = Guid.NewGuid() };
        using var newPreview = await fixture.PostAsync("/api/processes/launch/check", newIntent);
        var newRoot = (await fixture.SavedAsync(newIntent.CallerIntentId)).Preparation.InitialCommit.InitialRunDecisionAdmission!;
        Assert.Equal(nextMode, newRoot.Policy.ReadBasisMode);
        Assert.Equal(changed.Expectation.Head!.Revision, newRoot.WorkspaceHeadRevision);
        Assert.NotEqual(root.BudgetId, newRoot.BudgetId);

        using var accepted = await fixture.PostAsync("/api/processes/launch", fixture.Request with {
            PreparedAdmissionId = original.Preparation.AdmissionId.Value, Execute = true
        });
        var runId = original.Preparation.InitialCommit.Mutation.State.RunId;
        Assert.Equal(runId.Value, accepted.RootElement.GetProperty("runId").GetGuid());
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IProcessRuntimeStateStore>();
        var deadline = TimeProvider.System.GetTimestamp();
        var state = (await store.LoadAsync(runId))!;
        while (!state.AppliedResults.Any(item => item.ExecutionRunId is not null) &&
               !ProcessRuntimeTerminalStates.IsRunTerminal(state.Status) &&
               TimeProvider.System.GetElapsedTime(deadline) < TimeSpan.FromSeconds(30)) {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            state = (await store.LoadAsync(runId))!;
        }
        Assert.True(state.AppliedResults.Any(item => item.ExecutionRunId is not null),
            $"Queued launch did not produce a planning result. Status={state.Status}; " +
            string.Join("; ", state.AppliedResults.SelectMany(item => item.Diagnostics).Select(item => $"{item.Code}: {item.SafeSummary}")));
        var waiting = Assert.Single(state.AppliedResults.Where(item => item.ExecutionRunId is not null));
        var child = await fixture.Entry.Files.Store.GetExecutionRunAsync(waiting.ExecutionRunId!.Value.Value);
        Assert.True(waiting.Outcome == StrategyOutcome.NeedsManager,
            $"Expected manager review; observed {waiting.Outcome}. {fixture.StartupFailure}; child={child?.State}; " +
            $"summary={child?.ResultSummary}; provider requests={fixture.Entry.Wire.Requests.Count}; " +
            string.Join("; ", waiting.Diagnostics.Select(item => $"{item.Code}: {item.SafeSummary}")));
        var execution = waiting.ExecutionRunId!.Value.Value;
        var route = $"/api/processes/runs/{runId.Value:D}/steps/{waiting.StepInstanceId.Value:D}/planning/{execution:D}/review";
        using var reviewResponse = await fixture.Host.Client.GetAsync(route);
        using var reviewBody = await ProcessPlanningLaunchFixture.ReadAsync(reviewResponse);
        var review = reviewBody.RootElement.Deserialize<ProcessPlanningReviewApiView>(fixture.Json)!;
        Assert.Equal(root.Policy, review.ParentPolicy);
        Assert.Equal(response, review.ChildPolicy.OnBasisChange);
        Assert.Contains(AgentPlanningReviewChoice.Approve, review.AvailableChoices);
        Assert.NotNull(review.Publication);
        if (changeBasis) {
            var project = fixture.Entry.Files.Sql.Project;
            await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project.ProjectId,
                new(ProjectObjectType.WorkItem, "Concurrent task", "Task", "", $"project:{project.ProjectId:D}",
                    ObjectSubtype: ProjectObjectSubtypePolicy.Task,
                    MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                        WorkItem = new() { WorkItemKind = ProjectWorkItemKind.Task, Description = "Concurrent accepted work",
                            ExecutionState = ProjectTaskExecutionState.NotStarted }
                    })) { ExpectedProjectAdmission = project });
        }
        var actionId = Guid.NewGuid();
        ProcessPlanningReviewApiSubmission submission = new(new(actionId, AgentPlanningReviewChoice.Approve, review.Expected,
            review.Publication.Binding, review.Publication.ApprovalId), review.Process!, AgentExecutionOperationId.New());
        await using var database = Context(services);
        var budgetBeforePublication = await database.DecisionBudgets.AsNoTracking().SingleAsync(item => item.BudgetId == root.BudgetId.Value);
        using var approveResponse = await fixture.Host.Client.PostAsJsonAsync(route, submission, fixture.Json);
        using var approvedBody = await ProcessPlanningLaunchFixture.ReadAsync(approveResponse);
        var approved = approvedBody.RootElement.Deserialize<ProcessPlanningReviewApiResult>(fixture.Json)!;
        var paused = changeBasis && response == OnBasisChange.PauseForReview;
        RunOutcome? expectedOutcome = paused ? null : changeBasis ? RunOutcome.Failed : RunOutcome.Succeeded;
        Assert.Equal(paused, approved.AccountingPending);
        Assert.Equal(expectedOutcome, approved.Agent.Outcome);
        var settled = (await fixture.Entry.Files.Store.GetExecutionRunAsync(execution))!;
        var decision = settled.ToolAdmission!.PlanningDecision!;
        Assert.Null(decision.Work.Open);
        Assert.Equal(changeBasis ? paused ? AgentPlanningGenerationState.PausedBasisChanged : AgentPlanningGenerationState.Failed
            : AgentPlanningGenerationState.Completed, decision.Current.State);
        Assert.Equal(response == OnBasisChange.Fail ? OnBasisChange.Fail : (OnBasisChange?)null, decision.InheritedOrigin!.ChildOnBasisChange);
        Assert.Equal(1, decision.CurrentGeneration);
        var charged = decision.Work.ClosedWorkTicks;
        Assert.True(charged > 0);
        var budgetBeforeReplay = await database.DecisionBudgets.AsNoTracking().SingleAsync(item => item.BudgetId == root.BudgetId.Value);
        using var duplicateActivity = await fixture.Host.Client.PostAsJsonAsync(route, submission, fixture.Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicateActivity.StatusCode);
        using var duplicateBody = JsonDocument.Parse(await duplicateActivity.Content.ReadAsStringAsync());
        Assert.Equal("agents.execution-operation-duplicate", duplicateBody.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        using var replayResponse = await fixture.Host.Client.PostAsJsonAsync(route,
            submission with { ActivityOperationId = AgentExecutionOperationId.New() }, fixture.Json);
        using var replayBody = await ProcessPlanningLaunchFixture.ReadAsync(replayResponse);
        var replay = replayBody.RootElement.Deserialize<ProcessPlanningReviewApiResult>(fixture.Json)!;
        Assert.True(replay.Replayed);
        Assert.Equal(paused, replay.AccountingPending);
        Assert.Equal(expectedOutcome, replay.Agent.Outcome);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, changeBasis ? 0 : 1);
        Assert.Equal(charged, (await fixture.Entry.Files.Store.GetExecutionRunAsync(execution))!.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        var budget = await database.DecisionBudgets.AsNoTracking().SingleAsync(item => item.BudgetId == root.BudgetId.Value);
        Assert.Equal(budgetBeforeReplay.ReservedWorkTicks, budget.ReservedWorkTicks);
        Assert.Equal(budgetBeforeReplay.ConsumedWorkTicks, budget.ConsumedWorkTicks);
        Assert.Equal(budgetBeforePublication.ConsumedWorkTicks + (paused ? 0 : charged), budget.ConsumedWorkTicks);
        var childAllocation = await database.DecisionBudgetAllocations.SingleAsync(item =>
            item.BudgetId == decision.InheritedOrigin.Allocation.BudgetId.Value && item.AllocationId == decision.InheritedOrigin.Allocation.Id.Value);
        Assert.Equal(paused ? (long?)null : charged, childAllocation.ConsumedWorkTicks);
        var rootAllocations = await database.DecisionBudgetAllocations.Where(item => item.BudgetId == root.BudgetId.Value).ToArrayAsync();
        Assert.Equal(rootAllocations.Sum(item => item.ConsumedWorkTicks ?? 0), budget.ConsumedWorkTicks);
        Assert.Equal(rootAllocations.Where(item => item.ConsumedWorkTicks is null).Sum(item => item.GrantedWorkTicks), budget.ReservedWorkTicks);
        if (paused) {
            Assert.Equal(budgetBeforePublication.ReservedWorkTicks, budget.ReservedWorkTicks);
            Assert.Equal(decision.InheritedOrigin.Allocation.Grant.WorkTicks, budget.ReservedWorkTicks);
            Assert.Equal(ProcessRuntimeStatus.Blocked, (await store.LoadAsync(runId))!.Status);
        } else {
            Assert.Equal(0, budget.ReservedWorkTicks);
            Assert.Equal(changeBasis ? ProcessRuntimeStatus.Failed : ProcessRuntimeStatus.Completed, (await store.LoadAsync(runId))!.Status);
        }
        if (root.MaterialScope is { } shared) {
            Assert.Equal(paused ? ProcessDecisionRunScopePhase.Active : changeBasis ? ProcessDecisionRunScopePhase.Cancelled : ProcessDecisionRunScopePhase.Completed,
                (await database.DecisionRunScopes.SingleAsync(item => item.ScopeId == shared.ScopeId.Value)).Phase);
        } else {
            Assert.False(await database.DecisionRunScopes.AnyAsync(item => item.BudgetId == root.BudgetId.Value));
        }
    }

    [Fact]
    public async Task Concurrent_public_previews_retain_one_original_funding_identity_and_changed_selection_conflicts() {
        await using var fixture = await ProcessPlanningLaunchFixture.CreateAsync();
        fixture.Resolver.RequireConcurrentEntry = true;
        var responses = await Task.WhenAll(fixture.Host.Client.PostAsJsonAsync("/api/processes/launch/check", fixture.Request),
            fixture.Host.Client.PostAsJsonAsync("/api/processes/launch/check", fixture.Request));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        using var first = await ProcessPlanningLaunchFixture.ReadAsync(firstResponse);
        using var second = await ProcessPlanningLaunchFixture.ReadAsync(secondResponse);
        Assert.Equal(first.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid(),
            second.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        var saved = await fixture.SavedAsync();
        using var conflict = await fixture.Host.Client.PostAsJsonAsync("/api/processes/launch", fixture.Request with {
            Decision = fixture.Request.Decision! with { Policy = new(ReadBasisMode: ReadBasisMode.RunSnapshot) }
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(saved.PreparationFingerprint, (await fixture.SavedAsync()).PreparationFingerprint);
        await using var database = Context(fixture.Host.App.Services);
        Assert.Equal(1, await database.PreparedLaunches.CountAsync(item => item.CallerIntentId == fixture.Request.CallerIntentId));
        Assert.False(await database.DecisionBudgets.AnyAsync());
        Assert.False(await database.RuntimeStates.AnyAsync());
        Assert.Empty(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(UnsupportedLaunch.Contract, HttpStatusCode.BadRequest)]
    [InlineData(UnsupportedLaunch.Replan, HttpStatusCode.BadRequest)]
    [InlineData(UnsupportedLaunch.PinnedPublication, HttpStatusCode.BadRequest)]
    [InlineData(UnsupportedLaunch.NoIntent, HttpStatusCode.Forbidden)]
    [InlineData(UnsupportedLaunch.ExecutorPermission, HttpStatusCode.Forbidden)]
    [InlineData(UnsupportedLaunch.MultipleActivities, HttpStatusCode.BadRequest)]
    [InlineData(UnsupportedLaunch.MissingWorkspacePolicy, HttpStatusCode.BadRequest)]
    [InlineData(UnsupportedLaunch.MissingTaskOperation, HttpStatusCode.BadRequest)]
    public async Task Unsupported_public_launch_creates_no_preparation_funding_runtime_or_provider_work(UnsupportedLaunch reason, HttpStatusCode expected) {
        await using var fixture = await ProcessPlanningLaunchFixture.CreateAsync(activities: reason == UnsupportedLaunch.MultipleActivities ? 2 : 1,
            allowTaskOperation: reason != UnsupportedLaunch.MissingTaskOperation);
        var selection = fixture.Request.Decision!;
        var input = fixture.Request with { Decision = reason switch {
            UnsupportedLaunch.Contract => selection with { Execution = selection.Execution with { Version = 2 } },
            UnsupportedLaunch.Replan => selection with { Policy = new(OnBasisChange: OnBasisChange.Replan, MaximumReplans: 1) },
            UnsupportedLaunch.PinnedPublication => selection with { Policy = new(PublicationFreshness: PublicationFreshness.PinnedObservation) },
            _ => selection
        } };
        if (reason == UnsupportedLaunch.NoIntent) {
            input = input with { CallerIntentId = null };
        }
        if (reason == UnsupportedLaunch.MissingWorkspacePolicy) {
            await fixture.RestoreLegacySettingsWithoutPolicyAsync();
        }
        if (reason == UnsupportedLaunch.ExecutorPermission) {
            await fixture.Entry.Files.Store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(agent => agent.Id == fixture.Entry.Files.Agent.Id
                    ? agent with { Permissions = agent.Permissions with { CanUseTools = false } } : agent).ToArray()
            });
        }
        using var response = await fixture.Host.Client.PostAsJsonAsync("/api/processes/launch/check", input);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {body}");
        if (expected == HttpStatusCode.BadRequest) {
            Assert.Contains("process.launch_decision_rejected", body, StringComparison.Ordinal);
            var expectedReason = reason switch {
                UnsupportedLaunch.Contract => ProcessLaunchDecisionRejection.UnsupportedContract,
                UnsupportedLaunch.MultipleActivities or UnsupportedLaunch.MissingTaskOperation => ProcessLaunchDecisionRejection.UnsupportedPlan,
                UnsupportedLaunch.MissingWorkspacePolicy => ProcessLaunchDecisionRejection.MissingWorkspacePolicy,
                _ => ProcessLaunchDecisionRejection.UnsupportedPolicy
            };
            using var error = JsonDocument.Parse(body);
            Assert.Equal(new ProcessLaunchDecisionRejectedException(expectedReason).Message,
                error.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
        }
        await using var database = Context(fixture.Host.App.Services);
        Assert.False(await database.PreparedLaunches.AnyAsync());
        Assert.False(await database.DecisionBudgets.AnyAsync());
        Assert.False(await database.RuntimeStates.AnyAsync());
        Assert.Empty(fixture.Entry.Wire.Requests);
    }

    public enum UnsupportedLaunch { Contract, Replan, PinnedPublication, NoIntent, ExecutorPermission, MultipleActivities, MissingWorkspacePolicy, MissingTaskOperation }
}
