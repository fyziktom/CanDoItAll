using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionReviewRootTests {
    [Theory]
    [InlineData(ProcessRuntimeStatus.Active)]
    [InlineData(ProcessRuntimeStatus.CancelRequested)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    public async Task Waiting_descendant_completion_checks_current_root_inside_the_commit(ProcessRuntimeStatus rootStatus) {
        await using var app = await TestApplication.CreateAsync();
        await using var database = Context(app.Services);
        var clock = new RootClock();
        var owner = new EfProcessRuntimeUnitOfWork(database, clock,
            runtimeDatabase: app.Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var initial = ProcessProjectAdmissionFixture.Initial();
        var rootAdmission = ProcessDecisionRunPolicyFixture.Root(initial.OriginalState.RunId,
            initial.InitialPlan!.Definition.VersionId, app.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id,
            ReadBasisMode.StepSnapshot);
        initial = initial with {
            InitialRunDecisionAdmission = rootAdmission,
            InitialAssignments = initial.InitialAssignments!.Select(item => item with {
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(rootAdmission, item.StepInstanceId)
            }).ToArray()
        };
        var root = (await owner.CommitAsync(initial)).State;
        root = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(root))).State;
        var child = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Descendant(root, rootAdmission, ProcessDecisionChildOwner.Agent))).State;
        var available = await BudgetAsync(database, child);
        var capture = await owner.CommitAsync(Request(child, null,
            new ProcessDecisionBudgetReservation(new(Step(child), available.BudgetId, available.Revision),
                new(Guid.NewGuid()), new(100, 0))));
        ProcessDecisionMaterialScope scope = new(new(Guid.NewGuid()), rootAdmission.DatabaseProfileId,
            rootAdmission.WorkspaceNamespaceId, root.RunId, child.RunId, Step(child));
        var material = ProcessDecisionMaterial.FromContent(scope, new(scope.Id, 1, new(Guid.NewGuid())),
            ProcessDecisionPolicyFixture.ContentContract(), clock.GetUtcNow(), "{\"facts\":[]}");
        child = (await owner.CommitAsync(Request(capture.State,
            new ProcessDecisionStart(Step(child), new(Guid.NewGuid()), material.Reference.MaterialId) { Material = material.Stamp },
            new ProcessDecisionBudgetSettlement(new(Step(child), capture.DecisionBudget!.Change.Balance.BudgetId,
                capture.DecisionBudget.Change.Balance.Revision), capture.DecisionBudget.Change.Allocation.Id, new(1, new(5, 0)))) with {
            DecisionMaterial = material
        })).State;
        var enrollment = await owner.CommitAsync(await EnrollmentRequestAsync(database, child));
        child = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(enrollment.State))).State;
        var claim = child.Claims.Single(item => item.ClaimToken == child.Steps.Single().ActiveClaimToken);
        var selectedChild = enrollment.Decision!.Child!;
        var waitingKey = new StrategyResultIdempotencyKey(Guid.NewGuid());
        StrategyId strategy = new("review-root-fixture");
        RuntimeCommandContext commandContext = new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("review-root-fixture")),
            new("review-root-fixture"), clock.GetUtcNow());
        var waiting = ProcessRuntimeEngine.PrepareStrategyResult(child, commandContext,
            new(Step(child), claim.OwnerId, claim.ClaimToken, waitingKey,
                new(strategy, "1.0.0", waitingKey.Value, StrategyOutcome.NeedsManager, [], [], [], [], "sha256:" + new string('a', 64)) {
                    ExecutionRunId = new(selectedChild.ChildId.Value)
                }));
        Assert.True(waiting.Succeeded);
        var head = await DecisionAsync(database, child);
        child = (await owner.CommitAsync(new(commandContext.CommandId, child, waiting) {
            Decision = new ProcessDecisionChangePhase(Step(child), head.Expectation, ProcessDecisionPhaseChange.WaitForReview)
        })).State;
        head = await DecisionAsync(database, child);
        var budget = await BudgetAsync(database, child);
        ProcessDecisionChildClosure closure = new(7, ProcessDecisionChildOutcome.NoEffect, null);
        DecisionBudgetSettlement work = new(7, new(2, 0));
        commandContext = commandContext with { CommandId = RuntimeCommandId.New() };
        var completed = ProcessDecisionReviewCompletion.Prepare(child, commandContext,
            new(new(root.RunId, child.RunId, Step(child), head.Reference, selectedChild.ChildId),
                strategy, waitingKey, new(Guid.NewGuid()), StrategyOutcome.Succeeded, closure, work));
        Assert.True(completed.Succeeded);
        ProcessRuntimeCommitRequest completion = new(commandContext.CommandId, child, completed) {
            Decision = new ProcessDecisionClosure(Step(child), head.Expectation, selectedChild.ChildId, closure) {
                Completion = ProcessDecisionCompletion.Complete
            },
            DecisionBudget = new ProcessDecisionBudgetSettlement(new(Step(child), budget.BudgetId, budget.Revision), selectedChild.AllocationId, work)
        };
        if (rootStatus != ProcessRuntimeStatus.Active) {
            root = (await new ProcessRuntimeEngine(owner).BeginRootCancellationAsync(root,
                commandContext with { CommandId = RuntimeCommandId.New() })).State;
            if (rootStatus == ProcessRuntimeStatus.Cancelled) {
                root = await ProcessRootCancellationFixture.LegacyCancelledAsync(database, root);
            }
        }
        Assert.Equal(rootStatus, root.Status);
        var events = await database.RuntimeEvents.CountAsync(item => item.RunId == child.RunId.Value);
        if (rootStatus == ProcessRuntimeStatus.Active) {
            var result = await owner.CommitAsync(completion);
            Assert.True(result.CommittedNow);
            Assert.Equal(ProcessRuntimeStatus.Completed, result.State.Status);
            Assert.Equal(work, result.DecisionBudget!.Change.Allocation.Settlement);
            Assert.Equal(events + 2, await database.RuntimeEvents.CountAsync(item => item.RunId == child.RunId.Value));
            return;
        }
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(completion));
        Assert.Contains("stopped or cancelling root", failure.Message, StringComparison.Ordinal);
        Assert.Equal(budget, await BudgetAsync(database, child));
        Assert.Single((await owner.LoadAsync(child.RunId))!.AppliedResults);
        Assert.Equal(events, await database.RuntimeEvents.CountAsync(item => item.RunId == child.RunId.Value));
        Assert.False(await database.IdempotencyKeys.AnyAsync(item => item.CommandId == completion.CommandId.Value));
        Assert.Null((await database.DecisionChildren.AsNoTracking().SingleAsync(item => item.ChildId == selectedChild.ChildId.Value)).OwnerRevision);
    }

    private sealed class RootClock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now.AddSeconds(10);
    }
}
