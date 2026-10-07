using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

internal sealed class SchedulerMutations(ISchedulerWorkspaceOwner owner, Func<Task> refresh, Action notify, Action<string> fail) : IDisposable {
    private readonly Dictionary<Guid, SchedulerMutationReceipt> planReceipts = [];
    private readonly Dictionary<Guid, SchedulerDraftValues> submissions = [];
    private readonly Dictionary<Guid, SchedulerReadLane> draftReviews = [];
    private readonly Dictionary<Guid, SchedulerReadLane> planReviews = [];
    private bool disposed;
    public IReadOnlyDictionary<Guid, SchedulerMutationReceipt> Receipts => planReceipts;
    public bool IsPlanLocked(Guid id) => planReceipts.GetValueOrDefault(id)?.Status is SchedulerMutationStatus.Pending or SchedulerMutationStatus.Unknown;
    public Task ToggleAsync(SchedulerPlanSummary plan) => MutatePlanAsync(plan.Id,
        plan.IsEnabled ? SchedulerMutationKind.Disable : SchedulerMutationKind.Enable, () => owner.ToggleAsync(plan.Id, !plan.IsEnabled));
    public Task DeleteAsync(Guid id) => MutatePlanAsync(id, SchedulerMutationKind.Delete, () => owner.DeleteAsync(id));
    private void Notify() => notify();
    private Task RefreshAsync() => refresh();
    public bool IsPlanReviewing(Guid id) => planReviews.ContainsKey(id);
    public void Dispose() {
        disposed = true;
        foreach (var lane in draftReviews.Values.Concat(planReviews.Values)) {
            lane.Dispose();
        }
    }
    public void Acknowledge(Guid id) {
        if (planReceipts.GetValueOrDefault(id)?.Status is SchedulerMutationStatus.Committed or SchedulerMutationStatus.CommittedWithWarning or SchedulerMutationStatus.Refused) {
            planReceipts.Remove(id);
            Notify();
        }
    }
    private bool CanAdmit(Guid? id) {
        if (id is { } existing && planReceipts.ContainsKey(existing)) {
            return true;
        }
        if (planReceipts.Count < 128) {
            return true;
        }
        foreach (var settled in planReceipts.Where(pair => pair.Value.Status is SchedulerMutationStatus.Committed or SchedulerMutationStatus.Refused).Take(64).ToArray()) {
            planReceipts.Remove(settled.Key);
        }
        if (planReceipts.Count < 128) {
            return true;
        }
        fail("Review and acknowledge retained operation warnings before another mutation (limit 128).");
        return false;
    }
    public async Task SaveAsync(SchedulerDraft draft) {
        if (disposed || draft.IsLocked || draft.Values.Id is { } id && IsPlanLocked(id)) {
            return;
        }
        if (!CanAdmit(draft.Values.Id)) {
            return;
        }
        var submitted = draft.Values;
        var submittedRevision = draft.Revision;
        if (!SchedulerInputSession.TryParse(submitted.InputJson, out _, out var error) || draft.Issues.Count > 0) {
            draft.Error = string.IsNullOrEmpty(error) ? "Resolve the input errors before saving." : error;
            Notify();
            return;
        }
        var pending = new SchedulerMutationReceipt(SchedulerMutationKind.Save, SchedulerMutationStatus.Pending,
            submitted.Id, SchedulerMutationStage.None, "Validating the captured schedule.");
        draft.Receipt = pending;
        submissions[draft.Origin] = submitted;
        if (submitted.Id is { } planId) {
            planReceipts[planId] = pending;
        }
        Notify();
        SchedulerMutationReceipt receipt;
        var ownerStarted = false;
        try {
            var validation = await owner.ValidateAsync(submitted, CancellationToken.None);
            if (!validation.Succeeded) {
                receipt = new(SchedulerMutationKind.Save, SchedulerMutationStatus.Refused, submitted.Id,
                    SchedulerMutationStage.None, string.Join(" ", validation.Issues.Select(issue => issue.Message)));
                if (draft.Revision == submittedRevision) {
                    draft.Issues = validation.Issues;
                }
            } else {
                ownerStarted = true;
                receipt = await owner.SaveAsync(submitted with { InputJson = validation.NormalizedInputJson });
            }
        } catch (Exception exception) {
            receipt = new(SchedulerMutationKind.Save, ownerStarted ? SchedulerMutationStatus.Unknown : SchedulerMutationStatus.Refused, submitted.Id,
                SchedulerMutationStage.None, (ownerStarted ? "Outcome unknown. Review the exact plan before retrying. " : "Submission validation failed before the owner write. ") + exception.Message);
        }
        draft.Receipt = receipt;
        if (receipt.Status != SchedulerMutationStatus.Unknown) {
            submissions.Remove(draft.Origin);
        }
        if (receipt.Status is SchedulerMutationStatus.Committed or SchedulerMutationStatus.CommittedWithWarning) {
            var saved = receipt.SavedValues ?? submitted with { Id = receipt.PlanId };
            if (draft.LastChanged(SchedulerDraftField.InputJson) <= submittedRevision) {
                draft.HasUnappliedInput = false;
            }
            draft.Values = Merge(draft, submittedRevision, saved);
            draft.Baseline = saved;
            draft.Error = string.Empty;
            submissions.Remove(draft.Origin);
        }
        if (receipt.PlanId is { } savedId) {
            planReceipts[savedId] = receipt;
        }
        Notify();
        if (receipt.Status is SchedulerMutationStatus.Committed or SchedulerMutationStatus.CommittedWithWarning) {
            await RefreshAsync();
        }
    }

    private async Task MutatePlanAsync(Guid id, SchedulerMutationKind kind, Func<Task<SchedulerMutationReceipt>> action) {
        if (disposed || IsPlanLocked(id) || !CanAdmit(id)) {
            return;
        }
        planReceipts[id] = new(kind, SchedulerMutationStatus.Pending, id, SchedulerMutationStage.None, "Operation pending.");
        Notify();
        try {
            planReceipts[id] = await action();
        } catch (Exception exception) {
            planReceipts[id] = new(kind, SchedulerMutationStatus.Unknown, id, SchedulerMutationStage.None, "Outcome unknown; operator review required. " + exception.Message);
        }
        Notify();
        await RefreshAsync();
    }

    public async Task ReviewPlanAsync(Guid id) {
        if (disposed || !planReceipts.TryGetValue(id, out var receipt) || receipt.Status != SchedulerMutationStatus.Unknown
            || receipt.Kind == SchedulerMutationKind.Save) {
            return;
        }
        if (planReviews.Remove(id, out var previous)) {
            previous.Dispose();
        }
        using var lane = new SchedulerReadLane();
        planReviews[id] = lane;
        using var request = lane.Begin();
        bool Current() => !disposed && request.IsCurrent && ReferenceEquals(planReceipts.GetValueOrDefault(id), receipt);
        Notify();
        try {
            var observed = await owner.EditorAsync(id, request.Token);
            if (!Current()) {
                return;
            }
            if (receipt.Kind == SchedulerMutationKind.Delete || observed.IsEnabled != (receipt.Kind == SchedulerMutationKind.Enable)) {
                planReceipts[id] = receipt with { Message = "The exact plan does not yet show the requested state. Replay remains locked." };
                Notify();
                return;
            }
        } catch (KeyNotFoundException) when (receipt.Kind == SchedulerMutationKind.Delete) {
        } catch (Exception exception) {
            if (Current()) {
                planReceipts[id] = receipt with { Message = exception.Message };
                Notify();
            }
            return;
        } finally {
            if (ReferenceEquals(planReviews.GetValueOrDefault(id), lane)) {
                planReviews.Remove(id);
                Notify();
            }
        }
        if (Current()) {
            planReceipts[id] = receipt with { Status = SchedulerMutationStatus.CommittedWithWarning, Stage = SchedulerMutationStage.Persisted,
                Message = "Exact persisted state reviewed. The earlier operation and trigger projection were not independently confirmed." };
        }
        Notify();
    }

    public async Task ReviewUnknownAsync(SchedulerDraft draft, Guid exactPlanId) {
        if (disposed || draft.Receipt?.Status != SchedulerMutationStatus.Unknown || exactPlanId == Guid.Empty
            || !submissions.TryGetValue(draft.Origin, out var submitted)) {
            return;
        }
        var receipt = draft.Receipt;
        if (draftReviews.Remove(draft.Origin, out var previous)) {
            previous.Dispose();
        }
        using var lane = new SchedulerReadLane();
        draftReviews[draft.Origin] = lane;
        using var request = lane.Begin();
        bool Current() => !disposed && request.IsCurrent && ReferenceEquals(draft.Receipt, receipt)
            && ReferenceEquals(submissions.GetValueOrDefault(draft.Origin), submitted);
        draft.IsReviewing = true;
        Notify();
        try {
            var observed = await owner.EditorAsync(exactPlanId, request.Token);
            if (!Current()) {
                return;
            }
            if (!SchedulerInputSession.SameTarget(submitted, observed) || submitted.Id is { } id && id != observed.Id) {
                throw new InvalidOperationException("The reviewed plan does not have the submitted exact target and identity.");
            }
            draft.Values = draft.Values with { Id = observed.Id };
            draft.Receipt = new(SchedulerMutationKind.Save, SchedulerMutationStatus.CommittedWithWarning, observed.Id,
                SchedulerMutationStage.Persisted, "Exact plan reviewed. Current persistence is visible; the earlier operation and projection were not independently confirmed.", observed);
            planReceipts[exactPlanId] = draft.Receipt;
            submissions.Remove(draft.Origin);
        } catch (Exception exception) {
            if (Current()) {
                draft.Error = exception.Message;
            }
        } finally {
            if (ReferenceEquals(draftReviews.GetValueOrDefault(draft.Origin), lane)) {
                draftReviews.Remove(draft.Origin);
                if (!disposed) {
                    draft.IsReviewing = false;
                    Notify();
                }
            }
        }
        Notify();
    }

    private static SchedulerDraftValues Merge(SchedulerDraft draft, long submittedRevision, SchedulerDraftValues saved) => draft.Values with {
        Id = saved.Id,
        Name = draft.LastChanged(SchedulerDraftField.Name) <= submittedRevision ? saved.Name : draft.Values.Name,
        Description = draft.LastChanged(SchedulerDraftField.Description) <= submittedRevision ? saved.Description : draft.Values.Description,
        TargetKind = draft.LastChanged(SchedulerDraftField.TargetKind) <= submittedRevision ? saved.TargetKind : draft.Values.TargetKind,
        TargetId = draft.LastChanged(SchedulerDraftField.TargetId) <= submittedRevision ? saved.TargetId : draft.Values.TargetId,
        TargetVersionId = draft.LastChanged(SchedulerDraftField.TargetVersionId) <= submittedRevision ? saved.TargetVersionId : draft.Values.TargetVersionId,
        CronExpression = draft.LastChanged(SchedulerDraftField.CronExpression) <= submittedRevision ? saved.CronExpression : draft.Values.CronExpression,
        TimeZoneId = draft.LastChanged(SchedulerDraftField.TimeZoneId) <= submittedRevision ? saved.TimeZoneId : draft.Values.TimeZoneId,
        MisfirePolicy = draft.LastChanged(SchedulerDraftField.MisfirePolicy) <= submittedRevision ? saved.MisfirePolicy : draft.Values.MisfirePolicy,
        IsEnabled = draft.LastChanged(SchedulerDraftField.IsEnabled) <= submittedRevision ? saved.IsEnabled : draft.Values.IsEnabled,
        StartAtUtc = draft.LastChanged(SchedulerDraftField.StartAtUtc) <= submittedRevision ? saved.StartAtUtc : draft.Values.StartAtUtc,
        EndAtUtc = draft.LastChanged(SchedulerDraftField.EndAtUtc) <= submittedRevision ? saved.EndAtUtc : draft.Values.EndAtUtc,
        InputJson = draft.LastChanged(SchedulerDraftField.InputJson) <= submittedRevision ? saved.InputJson : draft.Values.InputJson
    };
}
