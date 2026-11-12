using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanProducedWorkspaceTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false, false)]
    [InlineData(ReadBasisMode.StepSnapshot, true, false)]
    [InlineData(ReadBasisMode.StepSnapshot, true, true)]
    public async Task Produced_child_uses_original_publication_permission_and_waiting_plan(
        ReadBasisMode mode, bool publicationAllowed, bool exhaustWork) {
        var clock = new ProjectTaskPlanScenarioClock();
        await using var entry = await ProjectTaskPlanExecutionEntryTests.EntryFixture.CreateAsync(
            configureServices: services => services.AddSingleton<TimeProvider>(clock));
        await using var process = await ProcessStepMaterialFixture.CreateAsync(mode, existingSql: entry.Files.Sql,
            executorId: entry.Files.Agent.Id, allowTaskPublication: publicationAllowed);
        var services = entry.Files.Sql.Services;
        var produced = await services.GetRequiredService<ProcessDecisionStepPreparationCoordinator>()
            .PrepareAsync(process.State, process.Claim, TimeSpan.FromSeconds(30));
        Assert.Equal(ProcessDecisionStepPreparationStatus.Ready, produced.Status);
        Assert.True(produced.EnrolledNow);
        var parent = Assert.IsType<ProcessDecisionChildMaterialBinding>(produced.Child);
        var assignment = await services.GetRequiredService<IProcessRuntimeStepAssignmentStore>()
            .LoadAsync(parent.Child.Address.RunId, parent.Child.Address.StepInstanceId)
            ?? throw new InvalidOperationException("The produced child lost its selected assignment.");
        var context = ExecutionInvocationContext.Empty with {
            SourceKind = ProjectTaskPlanProcessContract.SourceKind, SourceId = assignment.StepKey,
            CorrelationId = assignment.RunId.ToString(), CausationId = assignment.StepInstanceId.ToString(),
            RequestedBy = "process-runtime", RequestedByKind = "system",
            ProcessRunId = assignment.RunId.ToString(), ProcessStepId = assignment.StepInstanceId.ToString(),
            MetadataJson = services.GetRequiredService<ProcessExecutionMetadataComposer>()
                .ComposeClaimedExecution(assignment, new(process.Claim.ClaimToken.Value))
        };
        var envelope = AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
            ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(parent));
        const string originalInput = "Prepare the original reviewed task plan from the supplied material.";
        var evidenceCaptures = 0;
        AgentInheritedPlanningWorkspaceRequest request = new(parent.Child.Address.ChildId.Value,
            new(new(parent.Child.BudgetId, parent.Child.AllocationId, parent.Child.Grant), AgentInheritedPlanningStartMode.FreshEnrollment),
            entry.Files.Agent.Id, originalInput, AgentExecutionOperationId.New(), context, AgentPlanningOwner.ProjectTaskPlan,
            WorkspaceScopeDescriptor.Project(entry.Files.Sql.Project.ProjectId.ToString("D")), envelope,
            async (run, token) => {
                Assert.Equal(parent.Child.Address.ChildId.Value, run.Id);
                Assert.Equal(envelope, run.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentBinding);
                var child = await services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(parent.Child.Address, token);
                Assert.Equal(parent.Child, child!.Admission);
            }, candidate => {
                evidenceCaptures++;
                var evidence = ProcessExecutionClaimEvidenceReader.Read(candidate, out _)
                    ?? throw new InvalidOperationException("The prepared candidate lost its original Process claim.");
                return AgentToolProtocolEnvelope.Create(ProcessDecisionInitialSourceCodec.Format, ProcessDecisionInitialSourceCodec.Version,
                    ProcessDecisionInitialSourceCodec.Serialize(evidence));
            });
        var workspace = services.GetRequiredService<IAgentInheritedPlanningWorkspaceService>();
        clock.Freeze();
        entry.Wire.OnRequest = token => {
            clock.Advance(exhaustWork ? TimeSpan.FromTicks(parent.Child.Grant.WorkTicks) : TimeSpan.FromSeconds(1));
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        if (!publicationAllowed) {
            var failure = await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() => workspace.ExecuteInheritedPlanningAsync(request));
            Assert.IsType<AgentToolAdmissionException>(failure.InnerException);
            var selected = await services.GetRequiredService<IProcessDecisionResultQuery>().ReadAsync(
                parent.Child.Address.RunId, parent.Child.Address.StepInstanceId, new(parent.Child.Address.ChildId.Value));
            Assert.Equal(request.AgentId, selected!.Child!.OriginalAgentId);
            var reader = Assert.Single(services.GetServices<IProcessDecisionChildOutcomeReader>(), item => item.Contract == parent.ExecutionContract);
            var closed = Assert.IsType<ProcessDecisionChildResult.Closed>(await Assert.IsAssignableFrom<IProcessDecisionLiveOutcomeReader>(reader)
                .ReadAsync(selected.Child, new ProcessExecutionOwnerEvidence<AgentInheritedPlanningPreparationFailure>(failure.Failure)));
            Assert.Equal(ProcessDecisionChildTerminalStatus.Failed, closed.Status);
            Assert.Equal(ProcessDecisionChildOutcome.NoEffect, closed.Closure.Outcome);
            Assert.Equal(failure.Failure.WorkTicks, closed.Work.Consumed.WorkTicks);
            Assert.Empty(entry.Wire.Requests);
            Assert.Null(await entry.Files.Store.GetExecutionRunAsync(request.ExecutionRunId));
            await entry.Files.Sql.VerifyAsync(0, 0);
            return;
        }
        if (exhaustWork) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workspace.ExecuteInheritedPlanningAsync(request));
            var cancelled = await entry.Files.Store.GetExecutionRunAsync(request.ExecutionRunId);
            Assert.NotNull(cancelled);
            Assert.Equal(RunOutcome.Cancelled, cancelled.Outcome);
            Assert.Single(entry.Wire.Requests);
            await entry.Files.Sql.VerifyAsync(0, 0);
            return;
        }
        var result = await workspace.ExecuteInheritedPlanningAsync(request);
        Assert.True(result.Run.State == ExecutionState.WaitingOnTool, result.Run.ResultSummary);
        var admission = result.Run.ToolAdmission!;
        var decision = admission.PlanningDecision!;
        Assert.Equal(AgentToolJournalRecord.InheritedPlanningSchemaVersion, admission.SchemaVersion);
        Assert.Equal(parent.Child.MaterialId.Value, decision.InheritedOrigin!.BasisId);
        Assert.Equal(parent.Child.Grant, decision.InheritedOrigin.Allocation.Grant);
        Assert.Equal(mode, decision.InheritedOrigin.ParentPolicy.ReadBasisMode);
        Assert.Equal(ReadBasisMode.RunSnapshot, decision.Policy.ReadBasisMode);
        Assert.Equal(decision.InheritedOrigin.ResolvePolicy(), decision.Policy);
        Assert.Equal(originalInput, admission.BackgroundInput!.Content);
        Assert.Single(result.Run.PendingApprovals);
        Assert.Single(entry.Wire.Requests);
        Assert.Contains(originalInput, entry.Wire.Requests.Single(), StringComparison.Ordinal);
        Assert.Null(decision.Work.Open);
        Assert.Equal(TimeSpan.FromSeconds(1).Ticks, decision.Work.ClosedWorkTicks);
        Assert.Equal(1, evidenceCaptures);
        await entry.Files.Sql.VerifyAsync(0, 0);
        var reopened = await workspace.ExecuteInheritedPlanningAsync(request with {
            Start = request.Start with { Mode = AgentInheritedPlanningStartMode.ExistingOnly },
            ActivityOperationId = AgentExecutionOperationId.New(), OriginalInput = "A replacement input must not be used.",
            CaptureSourceEvidence = _ => throw new InvalidOperationException("A waiting child must never be rebound.")
        });
        Assert.Null(reopened.Execution);
        Assert.Equal(result.Run.Revision, reopened.Run.Revision);
        Assert.Equal(originalInput, reopened.Run.ToolAdmission!.BackgroundInput!.Content);
        Assert.Single(entry.Wire.Requests);
    }
}
