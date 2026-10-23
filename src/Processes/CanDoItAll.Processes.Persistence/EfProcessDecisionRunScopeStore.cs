using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Processes.Persistence.ProcessDecisionRows;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionRunScopeStore(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase? runtimeDatabase = null) {
    private readonly EfProcessDecisionMaterialStore materials = new(context, runtimeDatabase);

    public async Task<ProcessDecisionRunScopeState?> ReadAsync(ProcessDecisionMaterialScopeId scopeId, CancellationToken cancellationToken = default)
        => (await LoadAsync(scopeId, false, cancellationToken).ConfigureAwait(false))?.State;

    internal async Task<(ProcessDecisionRunAdmission Root, DecisionBudgetBalance Budget, ProcessDecisionRunScopeState Scope)>
        ReadPreparationAsync(ProcessDecisionMaterialScopeId scopeId, CancellationToken cancellationToken) {
        var loaded = await LoadAsync(scopeId, false, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original shared root scope is missing.");
        return (loaded.Root, ReadBalance(loaded.Budget), loaded.State);
    }

    internal void StageInitial(ProcessRuntimeCommitRequest request) {
        if (request.InitialRunDecisionAdmission?.MaterialScope is not { } admission) {
            return;
        }
        admission.RequireRoot(request.Mutation.State, request.InitialPlan!.Definition.VersionId, request.InitialRunDecisionAdmission);
        var current = ProcessDecisionRunScopeTransitions.Create(admission);
        materials.StageRunScope(admission);
        var entity = new ProcessDecisionRunScopeEntity { ScopeId = admission.ScopeId.Value, BudgetId = admission.Funding.BudgetId.Value };
        Write(entity, current);
        context.DecisionRunScopes.Add(entity);
    }

    internal async Task RequireWorkAdmissionAsync(ProcessRuntimeCommitRequest request, bool admitsClaim, CancellationToken cancellationToken) {
        if (request.DecisionBudget is null && !admitsClaim) {
            return;
        }
        var scope = await (from head in context.DecisionRunScopes.AsNoTracking()
            join root in context.DecisionBudgets.AsNoTracking() on head.BudgetId equals root.BudgetId
            where root.RunId == request.OriginalState.RootRunId.Value && root.StepInstanceId == null
            select head).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (scope is null) {
            return;
        }
        if (admitsClaim && scope.Phase != ProcessDecisionRunScopePhase.Active) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.InvalidPhase);
        }
        if (request.DecisionBudget is null) {
            return;
        }
        var accounting = request.DecisionBudget;
        var rootCommand = accounting.Expectation.BudgetId.Value == scope.BudgetId && accounting.Expectation.StepInstanceId is null &&
            request.OriginalState.RunId == request.OriginalState.RootRunId;
        if (accounting is ProcessDecisionBudgetReservation) {
            if (request.DecisionRunScope is { Kind: ProcessDecisionRunScopeCommandKind.BeginCapture } begin) {
                if (!rootCommand || begin.Expectation != new ProcessDecisionRunScopeExpectation(new(scope.ScopeId), scope.Revision) ||
                        begin.Capture?.AllocationId != accounting.AllocationId) {
                    throw new InvalidOperationException("Shared capture requires its original root reservation and scope expectation.");
                }
            } else if (scope.Phase != ProcessDecisionRunScopePhase.Active) {
                throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.InvalidPhase);
            }
        } else if (rootCommand && scope.CaptureAllocationId == accounting.AllocationId.Value &&
                request.DecisionRunScope?.Kind is not (ProcessDecisionRunScopeCommandKind.CompleteCapture or ProcessDecisionRunScopeCommandKind.AbortCapture)) {
            throw new InvalidOperationException("The pending shared capture must close atomically with its scope completion or explicit abort.");
        }
    }

    internal async Task<ProcessDecisionRunScopeReceipt?> StageAsync(ProcessRuntimeCommitRequest request,
        ProcessDecisionBudgetReceipt? accounting, CancellationToken cancellationToken) {
        if (request.DecisionRunScope is not { } command) {
            return null;
        }
        command.Validate();
        RequireTransaction();
        var loaded = await LoadAsync(command.Expectation.ScopeId, true, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The original shared run scope was not found.");
        var current = loaded.State;
        if (request.OriginalState.RunId != current.Admission.Funding.RootRunId || request.OriginalState.RootRunId != request.OriginalState.RunId ||
                request.Decision is not null && !(request.Decision is ProcessDecisionBeginRefresh begin &&
                    command == ProcessDecisionRefreshReceipt.ScopeCommand(begin.Intent.Request))) {
            throw new InvalidOperationException("Only the original root runtime may transition its shared material scope.");
        }
        if (command.Kind is ProcessDecisionRunScopeCommandKind.BeginCapture or ProcessDecisionRunScopeCommandKind.CompleteCapture &&
                (ProcessRuntimeTerminalStates.IsRunTerminal(request.OriginalState.Status) || ProcessRuntimeTerminalStates.IsRunTerminal(request.Mutation.State.Status) ||
                request.OriginalState.Status == ProcessRuntimeStatus.CancelRequested || request.Mutation.State.Status == ProcessRuntimeStatus.CancelRequested)) {
            throw new InvalidOperationException("A stopped root cannot begin or activate shared material capture.");
        }
        var captureCommand = command.Kind is ProcessDecisionRunScopeCommandKind.BeginCapture or ProcessDecisionRunScopeCommandKind.CompleteCapture or ProcessDecisionRunScopeCommandKind.AbortCapture;
        if (captureCommand ? accounting is null || accounting.Command.Expectation.StepInstanceId is not null || accounting.Funding is not null ||
                accounting.Change.Balance.BudgetId != current.Admission.Funding.BudgetId : accounting is not null) {
            throw new InvalidOperationException("The shared scope command has incompatible original root accounting.");
        }
        if (request.Decision is ProcessDecisionBeginRefresh refresh) {
            var members = await ReadMembersAsync(current, cancellationToken).ConfigureAwait(false);
            var original = refresh.Intent.Request;
            if (current.CurrentMemberships != 1 || members.Length != 1 || members[0].HasActiveClaim ||
                    members[0].Membership.RunId != original.Child.RunId || members[0].Membership.StepInstanceId != original.Child.StepInstanceId ||
                    members[0].Decision.Expectation != original.Decision || members[0].Decision.MaterialId != refresh.Intent.OriginalMaterialId) {
                throw new InvalidOperationException("Shared refresh admission requires its complete original single-activity membership without live claims.");
            }
        }
        ProcessDecisionRunScopeState next;
        switch (command.Kind) {
            case ProcessDecisionRunScopeCommandKind.BeginReconciliation:
                next = ProcessDecisionRunScopeTransitions.BeginReconciliation(current, command.Expectation);
                break;
            case ProcessDecisionRunScopeCommandKind.BeginCapture when accounting!.Command is ProcessDecisionBudgetReservation:
                next = ProcessDecisionRunScopeTransitions.BeginCapture(current, command.Expectation, command.Capture!, loaded.Root,
                    accounting.Change, await ReadAllocationsAsync(current, accounting.Change.Allocation, cancellationToken).ConfigureAwait(false),
                    await ReadMembersAsync(current, cancellationToken).ConfigureAwait(false));
                break;
            case ProcessDecisionRunScopeCommandKind.CompleteCapture when accounting!.Command is ProcessDecisionBudgetSettlement:
                next = ProcessDecisionRunScopeTransitions.CompleteCapture(current, command.Expectation, request.DecisionMaterial!, accounting.Change);
                await materials.StageCanonicalAsync(request.DecisionMaterial!, cancellationToken).ConfigureAwait(false);
                break;
            case ProcessDecisionRunScopeCommandKind.AbortCapture when accounting!.Command is ProcessDecisionBudgetSettlement:
                next = ProcessDecisionRunScopeTransitions.AbortCapture(current, command.Expectation, accounting.Change);
                break;
            case ProcessDecisionRunScopeCommandKind.Complete or ProcessDecisionRunScopeCommandKind.Cancel:
                next = ProcessDecisionRunScopeTransitions.Finish(current, command.Expectation, command.Kind == ProcessDecisionRunScopeCommandKind.Cancel,
                    await ReadMembersAsync(current, cancellationToken).ConfigureAwait(false), ReadBalance(loaded.Budget),
                    await ReadAllocationsAsync(current, null, cancellationToken).ConfigureAwait(false));
                break;
            default:
                throw new InvalidDataException("The shared scope command has an incompatible capture transition.");
        }
        next.Validate();
        Write(loaded.Entity, next);
        return new(command, next);
    }

    internal async Task<ProcessDecisionRunScopeReceipt> StageResultCompletionAsync(ProcessRuntimeCommitRequest request,
        CancellationToken cancellationToken, ProcessDecisionRefreshIntent? refreshAccounting = null) {
        ProcessDecisionRunScopeCompletion.Require(request);
        var command = request.DecisionRunScope is { CompletesRuntimeResult: true } terminal
            ? terminal : throw new InvalidOperationException("The scope requires its explicit terminal result command.");
        RequireTransaction();
        var loaded = await LoadAsync(command.Expectation.ScopeId, true, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The original shared run scope was not found.");
        var current = loaded.State;
        if (request.OriginalState.RunId != current.Admission.Funding.RootRunId) {
            throw new InvalidOperationException("Only the original root result may finish its shared material scope.");
        }
        var pending = await EfProcessRootCancellationGuard.ReadPendingAsync(context, current.Admission.Funding.RootRunId,
            cancellationToken, includeRootLocal: true).ConfigureAwait(false);
        if ((pending & ~ProcessRootCancellationPending.SharedScope) != ProcessRootCancellationPending.None) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.UnclosedWork);
        }
        var members = await ReadMembersAsync(current, cancellationToken).ConfigureAwait(false);
        var allocations = await ReadAllocationsAsync(current, null, cancellationToken).ConfigureAwait(false);
        var cancelled = command.Kind == ProcessDecisionRunScopeCommandKind.CancelResult;
        var next = refreshAccounting is null ? ProcessDecisionRunScopeTransitions.FinishResult(current, command.Expectation,
            cancelled, members, ReadBalance(loaded.Budget), allocations) :
            ProcessDecisionRunScopeTransitions.FinishRefreshResult(current, command.Expectation, cancelled,
                members, ReadBalance(loaded.Budget), allocations, refreshAccounting);
        next.Validate();
        Write(loaded.Entity, next);
        return new(command, next);
    }

    internal async Task RequireActiveDecisionAsync(ProcessDecisionHeadEntity head, ProcessDecisionGeneration generation,
        CancellationToken cancellationToken) {
        if (head.SharedRunScopeId is not { } scopeId) {
            return;
        }
        var loaded = await LoadAsync(new(scopeId), false, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The admitted shared material scope is missing.");
        var member = await context.DecisionRunScopeMembers.AsNoTracking().SingleOrDefaultAsync(item =>
            item.DecisionId == head.DecisionId && item.Generation == generation.Reference.Generation, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original shared material membership is missing.");
        if (member.ScopeId != scopeId || member.RunId != head.RunId || member.StepInstanceId != head.StepInstanceId ||
                member.MaterialId != generation.MaterialId.Value || loaded.State.Admission.Funding.RootRunId.Value != head.RootRunId) {
            throw new InvalidDataException("The local decision and shared material membership disagree.");
        }
        if (loaded.State.Material is null) {
            throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.InvalidPhase);
        }
        ProcessDecisionRunScopeTransitions.RequirePublication(loaded.State,
            new(new(new(scopeId), loaded.State.Material!.Stamp.Reference.Generation, new(member.MaterialId)), new(head.RunId),
                new(head.StepInstanceId), generation.Reference));
    }

    internal async Task StageInheritanceAsync(ProcessRuntimeStateSnapshot state, ProcessDecisionDataPolicyAdmission admission,
        ProcessDecisionCommand command, ProcessDecisionGeneration generation, ProcessDecisionMaterialId? previousMaterial,
        CancellationToken cancellationToken) {
        RequireTransaction();
        if (command.InheritedScope is not { } expected || admission.SharedRunScopeId != expected.ScopeId || command.Material is not { } stamp) {
            throw new InvalidDataException("The local decision requires its original admitted shared scope and material stamp.");
        }
        var loaded = await LoadAsync(expected.ScopeId, true, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original shared run scope is missing.");
        var current = loaded.State;
        loaded.Root.RequireChild(admission);
        if (state.RootRunId != current.Admission.Funding.RootRunId || stamp != current.Material?.Stamp ||
                state.ProjectAdmission is { } project && project.DatabaseProfileId != current.Admission.DatabaseProfileId ||
                previousMaterial == stamp.Reference.MaterialId) {
            throw new InvalidDataException("Inheritance must retain the active original root material and cannot refresh to the same material.");
        }
        if (previousMaterial is { } previous) {
            var prior = await context.DecisionMaterials.AsNoTracking().SingleAsync(item => item.MaterialId == previous.Value, cancellationToken).ConfigureAwait(false);
            if (prior.ScopeId != expected.ScopeId.Value || prior.Generation >= stamp.Reference.Generation) {
                throw new InvalidDataException("Parent refresh must advance within the original shared scope history.");
            }
        }
        var member = new ProcessDecisionRunScopeMembership(stamp.Reference, state.RunId, command.StepInstanceId, generation.Reference);
        var joined = ProcessDecisionRunScopeTransitions.Join(current, expected, member);
        joined.State.Validate();
        context.DecisionRunScopeMembers.Add(new() { ScopeId = expected.ScopeId.Value, MaterialId = stamp.Reference.MaterialId.Value,
            RunId = state.RunId.Value, StepInstanceId = command.StepInstanceId.Value, DecisionId = generation.Reference.DecisionId.Value,
            Generation = generation.Reference.Generation });
        Write(loaded.Entity, joined.State);
    }

    private async Task<LoadedScope?> LoadAsync(ProcessDecisionMaterialScopeId scopeId, bool tracking, CancellationToken cancellationToken) {
        var query = from head in context.DecisionRunScopes
            join budget in context.DecisionBudgets.AsNoTracking() on head.BudgetId equals budget.BudgetId
            join scope in context.DecisionMaterialScopes.AsNoTracking() on head.ScopeId equals scope.ScopeId
            join material in context.DecisionMaterials.AsNoTracking().Select(item => new {
                MaterialId = (Guid?)item.MaterialId, ScopeId = (Guid?)item.ScopeId, Generation = (int?)item.Generation,
                CapturedAtUtcTicks = (long?)item.CapturedAtUtcTicks, item.Fingerprint
            }) on head.CurrentMaterialId equals material.MaterialId into materialRows
            from material in materialRows.DefaultIfEmpty()
            where head.ScopeId == scopeId.Value
            select new { Head = head, Budget = budget, Scope = scope, Material = material };
        var row = await (tracking ? query.AsTracking() : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null) {
            return null;
        }
        var root = ProcessDecisionRunAdmissionCodec.Deserialize(row.Budget.RunAdmissionJson
            ?? throw new InvalidDataException("The shared scope has no original root admission."));
        var admission = root.MaterialScope ?? throw new InvalidDataException("The root did not originally admit a shared material scope.");
        if (admission.ScopeId != scopeId || root.BudgetId.Value != row.Head.BudgetId || row.Budget.RootRunId != root.RunId.Value ||
                row.Budget.RunId != root.RunId.Value || row.Budget.StepInstanceId is not null || row.Budget.ParentBudgetId is not null ||
                row.Budget.AdmissionCommandId is null || ReadBalance(row.Budget).Ceiling != root.Policy.Budget ||
                row.Scope.RootRunId != root.RunId.Value || row.Scope.OwnerRunId != root.RunId.Value || row.Scope.StepInstanceId is not null ||
                row.Scope.DatabaseProfileId != root.DatabaseProfileId || row.Scope.WorkspaceNamespaceId != root.WorkspaceNamespaceId ||
                row.Scope.MaterialProtocol != ProcessDecisionMaterialProtocol.SharedContentV1 || row.Scope.Executor is not null ||
                row.Scope.ContractId != admission.ContentContract.SchemaId || row.Scope.ContractVersion != admission.ContentContract.Version ||
                runtimeDatabase?.Profile.Profile.Id != root.DatabaseProfileId ||
                row.Head.CurrentMaterialId is not null && (row.Material is null || row.Material.ScopeId != scopeId.Value)) {
            throw new InvalidDataException("The shared head, material scope and original root admission disagree.");
        }
        ProcessDecisionRunScopeState current = new(admission, row.Head.Revision, row.Head.Phase,
            row.Material?.MaterialId is { } materialId ? new(new(new(scopeId, row.Material.Generation!.Value, new(materialId)), row.Material.Fingerprint),
                new(row.Material.CapturedAtUtcTicks!.Value, TimeSpan.Zero)) : null, row.Head.TotalMemberships, row.Head.CurrentMemberships,
            row.Head.CaptureAllocationId is { } allocation ? new(new(allocation), new(row.Head.CaptureMaterialId!.Value), row.Head.CaptureKind!.Value) : null);
        current.Validate();
        return new(row.Head, row.Budget, root, current);
    }

    private async Task<DecisionBudgetAllocation[]> ReadAllocationsAsync(ProcessDecisionRunScopeState current,
        DecisionBudgetAllocation? pending, CancellationToken cancellationToken) {
        var rows = await context.DecisionBudgetAllocations.AsNoTracking().Where(item => item.BudgetId == current.Admission.Funding.BudgetId.Value)
            .OrderBy(item => item.AllocationId).Take(DecisionBudgetBalance.MaximumAllocations + 1).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var allocations = rows.Select(ReadAllocation).ToList();
        if (pending is not null) {
            if (allocations.Any(item => item.Id == pending.Id)) {
                throw new InvalidOperationException("Shared capture cannot adopt an existing root allocation.");
            }
            allocations.Add(pending);
        }
        return [.. allocations];
    }

    private async Task<ProcessDecisionRunScopeMemberObservation[]> ReadMembersAsync(ProcessDecisionRunScopeState current,
        CancellationToken cancellationToken) {
        var materialId = current.Material?.Stamp.Reference.MaterialId.Value;
        var rows = await (from member in context.DecisionRunScopeMembers.AsNoTracking()
            join head in context.DecisionHeads.AsNoTracking() on member.DecisionId equals head.DecisionId
            join generation in context.DecisionGenerations.AsNoTracking()
                on new { member.DecisionId, member.Generation } equals new { generation.DecisionId, generation.Generation }
            join step in context.RuntimeSteps.AsNoTracking()
                on new { member.RunId, member.StepInstanceId } equals new { step.RunId, step.StepInstanceId }
            where member.ScopeId == current.Admission.ScopeId.Value && member.MaterialId == materialId
            orderby member.DecisionId, member.Generation
            select new { Member = member, Head = head, Generation = generation, step.ActiveClaimToken,
                HasClaim = context.DispatchClaims.Any(claim => claim.RunId == member.RunId && claim.StepInstanceId == member.StepInstanceId &&
                    (claim.Status == DispatchClaimStatus.Claimed || claim.Status == DispatchClaimStatus.LeaseRenewed || claim.Status == DispatchClaimStatus.Reclaimed)),
                Children = context.DecisionChildren.Count(child => child.DecisionId == member.DecisionId && child.Generation == member.Generation),
                OpenChildren = context.DecisionChildren.Count(child => child.DecisionId == member.DecisionId && child.Generation == member.Generation && child.OwnerRevision == null) })
            .Take(ProcessDecisionRunScopeState.MaximumMemberships + 1).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(row => {
            if (row.Head.RootRunId != current.Admission.Funding.RootRunId.Value || row.Head.SharedRunScopeId != current.Admission.ScopeId.Value ||
                    row.Head.RunId != row.Member.RunId || row.Head.StepInstanceId != row.Member.StepInstanceId ||
                    row.Head.BudgetId != row.Generation.BudgetId || row.Head.CurrentGeneration < row.Generation.Generation ||
                    row.Children != row.Generation.ChildCount || row.OpenChildren != row.Generation.UnclosedChildCount) {
                throw new InvalidDataException("The shared membership has inconsistent original runtime or child ownership.");
            }
            return new ProcessDecisionRunScopeMemberObservation(new(current.Material!.Stamp.Reference, new(row.Member.RunId), new(row.Member.StepInstanceId),
                new(new(row.Member.DecisionId), row.Member.Generation)),
                row.Head.CurrentGeneration == row.Generation.Generation ? RequireHead(row.Head, row.Generation) : ReadGeneration(row.Generation),
                row.ActiveClaimToken is not null || row.HasClaim);
        }).ToArray();
    }

    private void RequireTransaction() {
        if (context.Database.IsRelational() && context.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Shared scope transitions require the runtime owner's fenced transaction.");
        }
    }

    private static void Write(ProcessDecisionRunScopeEntity entity, ProcessDecisionRunScopeState state) {
        entity.Revision = state.Revision;
        entity.Phase = state.Phase;
        entity.CurrentMaterialId = state.Material?.Stamp.Reference.MaterialId.Value;
        entity.TotalMemberships = state.TotalMemberships;
        entity.CurrentMemberships = state.CurrentMemberships;
        entity.CaptureAllocationId = state.Capture?.AllocationId.Value;
        entity.CaptureMaterialId = state.Capture?.MaterialId.Value;
        entity.CaptureKind = state.Capture?.Kind;
    }

    private sealed record LoadedScope(ProcessDecisionRunScopeEntity Entity, ProcessDecisionBudgetEntity Budget,
        ProcessDecisionRunAdmission Root, ProcessDecisionRunScopeState State);
}
