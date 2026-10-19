using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionRunScopePersistenceTests {
    [Fact]
    public async Task Original_scope_admission_is_atomic_and_no_work_starts_before_first_capture() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = Initial(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var current = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        Assert.Equal(ProcessDecisionRunScopePhase.AwaitingCapture, current.Phase);
        Assert.Null(current.Material);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Single(await context.DecisionMaterialScopes.ToArrayAsync());
        Assert.True((await owner.CommitAsync(initial)).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(initial with {
            InitialRunDecisionAdmission = initial.InitialRunDecisionAdmission with { MaterialScope = null }
        }));
        foreach (var step in new[] { (CanDoItAll.Processes.Abstractions.ProcessStepInstanceId?)null, Step(state) }) {
            var budget = (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RunId, step))!;
            await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(Request(state, null,
                new ProcessDecisionBudgetReservation(new(step, budget.BudgetId, budget.Revision), new(Guid.NewGuid()), new(5, 0)))));
        }
        await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(Claim(state)));
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.Equal(current, await Scopes(context, app.Services).ReadAsync(current.Admission.ScopeId));
    }

    [Fact]
    public async Task Capture_commits_one_canonical_payload_with_accounting_and_replays_original_receipts() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var beginRequest = await BeginRequestAsync(context, state, scope);
        var begun = await owner.CommitAsync(beginRequest);
        Assert.Equal(begun.DecisionRunScope!.State, await Scopes(context, app.Services).ReadAsync(scope.Admission.ScopeId));
        var completion = await CompleteRequestAsync(context, begun.State, begun.DecisionRunScope!.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(Request(begun.State, null, completion.DecisionBudget)));
        var result = await owner.CommitAsync(completion);
        Assert.Equal(ProcessDecisionRunScopePhase.Active, result.DecisionRunScope!.State.Phase);
        Assert.Equal(3, result.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(0, result.DecisionBudget.Change.Balance.ReservedWorkTicks);
        var row = Assert.Single(await context.DecisionMaterials.ToArrayAsync());
        Assert.Equal(completion.DecisionMaterial!.PayloadJson, row.PayloadJson);
        Assert.Equal(completion.DecisionMaterial.CapturedAtUtc.UtcTicks, row.CapturedAtUtcTicks);
        Assert.Equal(begun.DecisionRunScope, (await owner.CommitAsync(beginRequest)).DecisionRunScope);
        Assert.Equal(result.DecisionRunScope, (await owner.CommitAsync(completion)).DecisionRunScope);
        Assert.Equal(result.DecisionBudget, (await owner.CommitAsync(completion)).DecisionBudget);
        Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Agent_and_nested_workflow_share_original_material_without_recapture_or_payload_duplication() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var captured = await CaptureAsync(context, app.Services, state, (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!);
        var joined = await owner.CommitAsync(await JoinRequestAsync(context, app.Services, captured.State, captured.DecisionRunScope!.State));
        var root = (await owner.CommitAsync(Claim(joined.State))).State;
        var nested = (await owner.CommitAsync(Descendant(root, initial.InitialRunDecisionAdmission!))).State;
        var scope = (await Scopes(context, app.Services).ReadAsync(captured.DecisionRunScope.State.Admission.ScopeId))!;
        var inherited = await owner.CommitAsync(await JoinRequestAsync(context, app.Services, nested, scope));
        Assert.Equal(joined.Decision!.Current.MaterialId, inherited.Decision!.Current.MaterialId);
        Assert.Equal(2, await context.DecisionRunScopeMembers.CountAsync());
        Assert.Single(await context.DecisionMaterials.ToArrayAsync());
        Assert.Single(await context.DecisionMaterialScopes.ToArrayAsync());
        var rootBudget = (await new EfProcessDecisionBudgetStore(context).ReadAsync(root.RunId, null))!;
        Assert.Equal(7, rootBudget.ConsumedWorkTicks);
        Assert.Equal(3, rootBudget.AllocationCount);
        Assert.Equal(0, rootBudget.ConsumedReplans);
        var receipts = await context.IdempotencyKeys.AsNoTracking().Where(item => item.DecisionReceiptJson != null).Select(item => item.DecisionReceiptJson!).ToArrayAsync();
        Assert.All(receipts, receipt => Assert.DoesNotContain("payloadJson", receipt, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Shared_start_rejects_recapture_missing_inheritance_and_wrong_scope_without_spending_preparation(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var captured = await CaptureAsync(context, app.Services, state, (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!);
        var request = await JoinRequestAsync(context, app.Services, captured.State, captured.DecisionRunScope!.State);
        var before = await BudgetAsync(context, state);
        request = change switch {
            0 => request with { DecisionMaterial = ProcessDecisionMaterial.FromContent(captured.DecisionRunScope.State.Admission.MaterialScope,
                captured.DecisionRunScope.State.Material!.Stamp.Reference, captured.DecisionRunScope.State.Admission.ContentContract,
                ProcessProjectAdmissionFixture.Now, "{\"facts\":[]}") },
            1 => request with { Decision = request.Decision! with { InheritedScope = null } },
            _ => request with { Decision = request.Decision! with { InheritedScope = request.Decision.InheritedScope! with { ScopeId = new(Guid.NewGuid()) } } }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(request));
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
        Assert.Empty(await context.DecisionRunScopeMembers.ToArrayAsync());
        Assert.Equal(before, await BudgetAsync(context, state));
    }

    [Fact]
    public async Task Parent_refresh_requires_quiescence_and_child_rebind_charges_no_second_replan() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var captured = await CaptureAsync(context, app.Services, state, (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!);
        var joined = await owner.CommitAsync(await JoinRequestAsync(context, app.Services, captured.State, captured.DecisionRunScope!.State));
        var scope = (await Scopes(context, app.Services).ReadAsync(captured.DecisionRunScope.State.Admission.ScopeId))!;
        var frozen = await owner.CommitAsync(ScopeRequest(joined.State, new(scope.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
        await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(Claim(frozen.State)));
        var blocked = await BeginRequestAsync(context, frozen.State, frozen.DecisionRunScope!.State, ProcessDecisionRunCaptureKind.AutomaticReplan);
        Assert.Equal(ProcessDecisionRunScopeRefusal.UnclosedWork,
            (await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(blocked))).Reason);
        var quiet = await QuiesceAsync(context, app.Services, frozen.State);
        var refreshed = await CaptureAsync(context, app.Services, quiet.State, frozen.DecisionRunScope.State, ProcessDecisionRunCaptureKind.AutomaticReplan);
        Assert.Equal(2, refreshed.DecisionRunScope!.State.Material!.Stamp.Reference.Generation);
        Assert.Equal(0, refreshed.DecisionRunScope.State.CurrentMemberships);
        var rebound = await owner.CommitAsync(await JoinRequestAsync(context, app.Services, refreshed.State, refreshed.DecisionRunScope.State, quiet.Decision!.Current));
        Assert.Equal(2, rebound.Decision!.Current.Reference.Generation);
        Assert.Equal(refreshed.DecisionRunScope.State.Material.Stamp.Reference.MaterialId, rebound.Decision.Current.MaterialId);
        Assert.Equal(0, rebound.DecisionBudget!.Change.Allocation.Settlement!.Consumed.Replans);
        var root = (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RunId, null))!;
        Assert.Equal(1, root.ConsumedReplans);
        Assert.Equal(10, root.ConsumedWorkTicks);
        Assert.Equal(2, await context.DecisionMaterials.CountAsync());
        Assert.Equal(2, await context.DecisionRunScopeMembers.CountAsync());
        Assert.Equal(1, (await Scopes(context, app.Services).ReadAsync(scope.Admission.ScopeId))!.CurrentMemberships);
        Assert.Equal(joined.Decision.Current.MaterialId, (await new EfProcessDecisionStore(context).ReadGenerationAsync(state.RunId, Step(state), joined.Decision.Current.Reference))!.MaterialId);
    }

    [Theory]
    [InlineData(ProcessDecisionPublicationKind.Dispatch)]
    [InlineData(ProcessDecisionPublicationKind.ManagerReview)]
    public async Task Parent_reconciliation_revokes_live_child_publication_in_the_effect_transaction(ProcessDecisionPublicationKind kind) {
        await using var app = await TestApplication.CreateAsync();
        await using var serviceScope = app.Services.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        await using var context = Context(services);
        var owner = Owner(context, services);
        var initial = Initial(services);
        var state = (await owner.CommitAsync(initial)).State;
        var captured = await CaptureAsync(context, services, state, (await Scopes(context, services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!);
        var joined = await owner.CommitAsync(await JoinRequestAsync(context, services, captured.State, captured.DecisionRunScope!.State));
        var enrolled = await owner.CommitAsync(await EnrollmentRequestAsync(context, joined.State));
        state = (await owner.CommitAsync(Claim(enrolled.State))).State;
        if (kind == ProcessDecisionPublicationKind.ManagerReview) {
            state = (await QuiesceAsync(context, services, state, ProcessDecisionPhaseChange.WaitForReview)).State;
        }
        ProcessDecisionChildAddress address = new(state.RootRunId, state.RunId, Step(state), enrolled.Decision!.Current.Reference, enrolled.Decision.Child!.ChildId);
        var original = (await services.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(address))!.Admission;
        await RequireAsync(services, original, kind);
        var scope = (await Scopes(context, services).ReadAsync(captured.DecisionRunScope.State.Admission.ScopeId))!;
        var frozen = await owner.CommitAsync(ScopeRequest(state, new(scope.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
        Assert.False((await services.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(address))!.IsCurrent);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, original, kind));
        var newEnrollment = await EnrollmentRequestAsync(context, frozen.State);
        await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(newEnrollment));
    }

    internal static async Task RequireAsync(IServiceProvider services, ProcessDecisionChildAdmission original, ProcessDecisionPublicationKind kind) {
        await using var context = Context(services);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context);
        await services.GetRequiredService<IProcessDecisionPublicationFence>().RequireAsync(original, kind);
        await transaction.CommitAsync();
    }
}
