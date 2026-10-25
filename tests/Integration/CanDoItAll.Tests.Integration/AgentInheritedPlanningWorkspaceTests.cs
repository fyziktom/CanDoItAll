using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed class AgentInheritedPlanningWorkspaceTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_workspace_runs_original_material_through_credentials_usage_and_waiting_completion(ReadBasisMode mode) {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync(mode);
        var result = await fixture.ExecuteAsync();
        Assert.Same(fixture.Workspace, fixture.Inherited);
        Assert.Equal(ExecutionState.WaitingOnTool, result.Execution!.State);
        var detail = await fixture.Workspace.GetExecutionRunDetailAsync(result.Run.Id);
        Assert.Null(detail.Run.ChatSessionId);
        Assert.Null(detail.PlanningReview);
        Assert.Null(detail.Run.Outcome);
        Assert.Equal(AgentToolJournalRecord.InheritedPlanningSchemaVersion, detail.Run.ToolAdmission!.SchemaVersion);
        Assert.Equal(mode, detail.Run.ToolAdmission.PlanningDecision!.InheritedOrigin!.ParentPolicy.ReadBasisMode);
        Assert.Null(detail.Run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Equal(fixture.Participant.Basis, Assert.Single(detail.Run.ToolAdmission.PlanningDecision.Current.Bases));
        Assert.Equal(fixture.Request.ContextScope, AgentPlanningRunMetadata.ReadScope(detail.Run));
        Assert.Equal(11, Assert.Single(detail.Metrics).InputTokens);
        Assert.Equal(7, detail.Metrics.Single().OutputTokens);
        Assert.Equal(11, Assert.Single(detail.UsageObservations).InputTokens);
        Assert.NotNull(detail.UsageObservations.Single().HistoryEvidence);
        Assert.Single(detail.Approvals);
        Assert.Equal(1, fixture.Bindings);
        Assert.Equal(1, fixture.Participant.Reads);
        Assert.Equal(new HistoryExternalReference(fixture.Request.ContextScope.Key, HistoryExternalReference.LocalProjectType),
            Assert.Single(fixture.Wire.Contexts).ExternalReference);
        AssertOriginalInput(fixture);
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Retained_initial_child_uses_saved_input_context_and_binding() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var original = await fixture.SeedInitialAsync();
        var result = await fixture.ExecuteAsync(fixture.Request with {
            OriginalInput = "Replacement input must not enter the provider.", ActivityOperationId = AgentExecutionOperationId.New(),
            Context = ExecutionInvocationContext.Empty with { SourceId = "replacement" },
            CaptureSourceEvidence = _ => throw new InvalidOperationException("Retained execution must not create a new binding.")
        });
        Assert.NotNull(result.Execution);
        Assert.Equal(original.SourceId, result.Run.SourceId);
        Assert.Equal(original.MetadataJson, result.Run.MetadataJson);
        Assert.Equal(original.ToolAdmission!.PlanningDecision!.InheritedOrigin, result.Run.ToolAdmission!.PlanningDecision!.InheritedOrigin);
        Assert.Equal(1, fixture.Bindings);
        AssertOriginalInput(fixture);
    }

    [Fact]
    public async Task Competing_workspace_call_returns_original_without_terminalizing_active_provider() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Wire.OnRequest = async token => {
            entered.SetResult();
            await release.Task.WaitAsync(token);
        };
        var active = fixture.ExecuteAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var before = await fixture.Files.Store.GetExecutionRunAsync(fixture.Request.ExecutionRunId);
            var retained = await fixture.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Null(retained.Execution);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(retained.Run));
            Assert.NotNull(retained.Run.ToolAdmission!.PlanningDecision!.Work.Open);
            Assert.False(active.IsCompleted);
        } finally {
            release.TrySetResult();
        }
        Assert.Equal(ExecutionState.WaitingOnTool, (await active.WaitAsync(TimeSpan.FromSeconds(20))).Run.State);
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Waiting_child_is_returned_and_generic_approval_cannot_bypass_parent_owner() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var original = (await fixture.ExecuteAsync()).Run;
        var retained = await fixture.ExecuteAsync();
        Assert.Null(retained.Execution);
        var refused = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Workspace.ContinueExecutionRunAsync(original.Id,
            AgentExecutionOperationId.New(), []));
        Assert.Equal("tool-admission.effect-dispatch-unavailable", refused.Code);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunAsync(original.Id)));
        Assert.Single(fixture.Wire.Requests);
        Assert.Single((await fixture.Workspace.GetExecutionRunDetailAsync(original.Id)).Metrics);
    }

    [Fact]
    public async Task Provider_failure_keeps_usage_unknown_work_and_does_not_restart() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        fixture.Participant.ContinueSteps = 1;
        fixture.Wire.OnRequest = _ => {
            fixture.Wire.StatusCode = fixture.Wire.Requests.Count == 1 ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            return Task.CompletedTask;
        };
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.ExecuteAsync()));
        var detail = await fixture.Workspace.GetExecutionRunDetailAsync(fixture.Request.ExecutionRunId);
        Assert.Equal(RunOutcome.Failed, detail.Run.Outcome);
        Assert.NotNull(detail.Run.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(AgentToolProviderDispatchState.Started, detail.Run.ToolAdmission.ProviderDispatches[^1].State);
        Assert.Equal(11, detail.UsageObservations.Sum(item => item.InputTokens));
        Assert.Equal(7, detail.UsageObservations.Sum(item => item.OutputTokens));
        Assert.Single(detail.Metrics);
        Assert.Null((await fixture.ExecuteAsync()).Execution);
        Assert.Equal(2, fixture.Wire.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_read_revocation_refuses_retained_entry_and_detail(bool parent) {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var original = (await fixture.ExecuteAsync()).Run;
        fixture.ReadAllowed = parent;
        fixture.Participant.DenyRead = parent;
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.ExecuteAsync()));
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.Workspace.GetExecutionRunDetailAsync(original.Id)));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunAsync(original.Id)));
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Parent_rejection_leaves_no_initial_run_or_provider_work() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        fixture.DenyParent = true;
        var failure = await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() => fixture.ExecuteAsync());
        Assert.IsType<UnauthorizedAccessException>(failure.InnerException);
        Assert.Equal(fixture.Request.ExecutionRunId, failure.Failure.ExecutionRunId);
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(fixture.Request.ExecutionRunId));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("provider")]
    [InlineData("agent-options")]
    public async Task Changed_registered_contract_settles_recovered_startup_without_entering_provider(string change) {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var original = await fixture.SeedInitialAsync();
        if (change == "participant") {
            fixture.Participant.Prompt = new("Changed contract", "other_plan", fixture.Participant.Prompt.SchemaJson);
        } else if (change == "provider") {
            var providers = fixture.Files.Sql.Services.GetRequiredService<IProviderProfileRegistry>();
            var provider = await providers.GetProviderEditorAsync(fixture.Files.Provider.Id);
            provider.BaseUrl = "https://changed.example.test/v1";
            await providers.SaveProviderAsync(provider);
        } else {
            await fixture.Files.Store.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == fixture.Request.AgentId
                    ? agent with { Temperature = agent.Temperature + 0.1 } : agent).ToArray()
            });
        }
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync());
        Assert.Equal("planning.runtime-contract-changed", failure.Code);
        var closed = (await fixture.Files.Store.GetExecutionRunAsync(original.Id))!;
        Assert.Equal(RunOutcome.Failed, closed.Outcome);
        Assert.Null(closed.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.True(closed.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks >= original.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Equal(original.ToolAdmission.PlanningDecision.InheritedOrigin, closed.ToolAdmission.PlanningDecision.InheritedOrigin);
        Assert.Equal(original.MetadataJson, closed.MetadataJson);
        Assert.Empty(fixture.Wire.Requests);
        Assert.Null((await fixture.ExecuteAsync()).Execution);
    }

    [Fact]
    public async Task Recovery_without_an_original_journal_cannot_prepare_or_bind_a_new_child() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(fixture.Request with {
            Start = fixture.Request.Start with { Mode = AgentInheritedPlanningStartMode.ExistingOnly }
        }));
        Assert.Equal(AgentInheritedPlanningRunAdmission.MissingOriginalCode, failure.Code);
        Assert.Equal(0, fixture.Bindings);
        Assert.Empty(fixture.Wire.Requests);
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(fixture.Request.ExecutionRunId));
    }

    private static void AssertOriginalInput(AgentInheritedPlanningWorkspaceFixture fixture) {
        using var body = JsonDocument.Parse(Assert.Single(fixture.Wire.Requests));
        var user = body.RootElement.GetProperty("messages").EnumerateArray().Single(message => message.GetProperty("role").GetString() == "user");
        using var input = JsonDocument.Parse(user.GetProperty("content").GetString()!);
        Assert.Equal(fixture.Request.OriginalInput, input.RootElement.GetProperty("OriginalRequest").GetString());
        Assert.Equal("original", input.RootElement.GetProperty("MaterialBasis").GetProperty("tasks")[0].GetString());
    }
}
