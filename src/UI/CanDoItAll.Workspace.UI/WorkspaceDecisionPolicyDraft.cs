using System.Globalization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Workspace.UI;

public sealed class WorkspaceDecisionPolicyDraft {
    public ReadBasisMode? ReadBasisMode { get; set; }
    public PublicationFreshness? PublicationFreshness { get; set; }
    public OnBasisChange? OnBasisChange { get; set; }
    public int? MaximumReplans { get; set; }
    public string MaximumWorkDuration { get; set; } = string.Empty;
    public string MaximumBasisAge { get; set; } = string.Empty;

    public DecisionDataPolicy Capture() {
        if (ReadBasisMode is not { } read || PublicationFreshness is not { } freshness ||
            OnBasisChange is not { } change || MaximumReplans is not { } replans ||
            !TimeSpan.TryParseExact(MaximumWorkDuration, "c", CultureInfo.InvariantCulture, out var work) ||
            !TimeSpan.TryParseExact(MaximumBasisAge, "c", CultureInfo.InvariantCulture, out var age)) {
            throw new ArgumentException("Choose all policy options and supply both durations as days.hours:minutes:seconds, with optional fractional seconds.");
        }
        return new(read, freshness, change, new(replans, work, age));
    }

    internal void Reconcile(DecisionDataPolicy? value, Action<DefaultsField, Action> apply) {
        apply(DefaultsField.PolicyReadBasis, () => ReadBasisMode = value?.ReadBasisMode);
        apply(DefaultsField.PolicyFreshness, () => PublicationFreshness = value?.PublicationFreshness);
        apply(DefaultsField.PolicyOnChange, () => OnBasisChange = value?.OnBasisChange);
        apply(DefaultsField.PolicyReplans, () => MaximumReplans = value?.Budget.MaximumReplans);
        apply(DefaultsField.PolicyWorkDuration, () => MaximumWorkDuration = value?.Budget.MaximumWorkDuration.ToString("c", CultureInfo.InvariantCulture) ?? string.Empty);
        apply(DefaultsField.PolicyBasisAge, () => MaximumBasisAge = value?.Budget.MaximumBasisAge.ToString("c", CultureInfo.InvariantCulture) ?? string.Empty);
    }
}
