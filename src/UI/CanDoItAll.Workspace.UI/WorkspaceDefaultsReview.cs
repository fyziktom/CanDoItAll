using CanDoItAll.Editing;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.UI;

public sealed class WorkspaceDefaultsReview {
    private readonly Guid origin;
    private readonly long revision;

    public WorkspaceDefaultsReview(WorkspaceDefaultsDraft draft, WorkspaceSettingsSnapshotV2 current) {
        if (!draft.CanCompare(current)) {
            throw new InvalidOperationException("The current values must belong to the original Workspace settings.");
        }
        origin = draft.Origin;
        revision = draft.Revision;
        Current = current;
        var baseline = draft.Baseline!;
        var local = draft.Model;
        var saved = current.Values;
        Name = new(baseline.WorkspaceName, local.WorkspaceName, saved.WorkspaceName);
        Provider = new(baseline.DefaultProviderProfileId, local.DefaultProviderProfileId, saved.DefaultProviderProfileId);
        OutputFormat = new(baseline.DefaultPromptOutputFormat, local.DefaultPromptOutputFormat, saved.DefaultPromptOutputFormat);
        Currency = new((baseline.CurrencyCode, baseline.CurrencyCultureName), (local.CurrencyCode, local.CurrencyCultureName),
            (saved.CurrencyCode, saved.CurrencyCultureName));
        Notes = TextMerge.Compare(baseline.Notes, local.Notes, saved.Notes).Field;
        var policy = draft.PolicyBaseline;
        HasUnfinishedPolicy = draft.EditContext.GetValidationMessages(new FieldIdentifier(draft.Policy, nameof(draft.Policy.MaximumReplans))).Any();
        if (draft.PolicyEditing && !HasUnfinishedPolicy) {
            try {
                policy = draft.Policy.Capture();
            } catch (ArgumentException) {
                HasUnfinishedPolicy = true;
            }
        }
        Policy = new(draft.PolicyBaseline, policy, current.DecisionDataPolicy);
        if (Policy.LocalChanged && Policy.Local != Policy.Current) {
            Policy.RequireChoice();
        }
    }

    public WorkspaceSettingsSnapshotV2 Current { get; }
    public MergeField<string> Name { get; }
    public MergeField<Guid?> Provider { get; }
    public MergeField<string> OutputFormat { get; }
    public MergeField<(string Code, string Culture)> Currency { get; }
    public MergeField<string> Notes { get; }
    public MergeField<DecisionDataPolicy?> Policy { get; }
    public bool HasUnfinishedPolicy { get; }
    public bool KeepUnfinishedPolicy { get; set; }

    public bool CanApply(WorkspaceDefaultsDraft draft) => draft.Origin == origin && draft.Revision == revision &&
        Name.IsResolved && Provider.IsResolved && OutputFormat.IsResolved && Currency.IsResolved && Notes.IsResolved &&
        (HasUnfinishedPolicy ? KeepUnfinishedPolicy : Policy.IsResolved);

    internal WorkspaceSettingsValues ResolveValues() => new(Name.Result, Provider.Result, OutputFormat.Result,
        Currency.Result.Code, Currency.Result.Culture, Notes.Result);
}
