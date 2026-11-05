using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components.Forms;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.SharedProviders.UI;

public sealed record SharedProviderLocalSettings(string LocalAlias, bool IsEnabled);

public sealed record SharedProviderImportBaseline(Guid ImportId, Guid ProviderId, Guid SourceId,
    SharedProviderPublicationId PublicationId, Guid ImportToken, Guid ProviderToken, SharedProviderLocalSettings Settings);

public sealed record SharedProviderImportSubmission(Guid DraftId, SharedProviderImportBaseline Baseline,
    SharedProviderLocalSettings Settings, long AliasRevision, long EnabledRevision);

public sealed class SharedProviderImportDraft {
    private string localAlias;
    private bool isEnabled;
    private long aliasRevision;
    private long enabledRevision;
    private readonly ValidationMessageStore validation;

    public SharedProviderImportDraft(SharedProviderImportBaseline baseline) {
        Baseline = baseline;
        Latest = baseline;
        localAlias = baseline.Settings.LocalAlias;
        isEnabled = baseline.Settings.IsEnabled;
        Context = new(this);
        validation = new(Context);
        Context.OnValidationRequested += (_, _) => ValidateAlias();
    }

    public Guid Id { get; } = Guid.NewGuid();
    public EditContext Context { get; }
    public SharedProviderImportBaseline Baseline { get; private set; }
    public SharedProviderImportBaseline Latest { get; private set; }
    public bool HasConflict { get; private set; }
    public bool IsDirty => Settings != Baseline.Settings;
    public bool NeedsCurrentRead { get; private set; }
    public SharedProviderImportComparison? Comparison { get; private set; }
    public void Invalidate() {
        HasConflict = true;
        NeedsCurrentRead = true;
        Comparison?.Invalidate();
    }
    private SharedProviderLocalSettings Settings => new(localAlias, isEnabled);

    public string LocalAlias {
        get => localAlias;
        set {
            localAlias = value;
            aliasRevision++;
            Comparison?.Invalidate();
            Context.NotifyFieldChanged(new(this, nameof(LocalAlias)));
        }
    }

    public bool IsEnabled {
        get => isEnabled;
        set {
            isEnabled = value;
            enabledRevision++;
            Comparison?.Invalidate();
            Context.NotifyFieldChanged(new(this, nameof(IsEnabled)));
        }
    }

    public SharedProviderImportSubmission Capture() => new(Id, Baseline, Settings, aliasRevision, enabledRevision);

    public bool CanSubmit(SharedProviderImportSubmission submission) =>
        submission == Capture() && !HasConflict && Context.Validate();

    public void Reconcile(SharedProviderImportBaseline latest) {
        EnsureIdentity(latest);
        Latest = latest;
        NeedsCurrentRead = false;
        Comparison?.Invalidate();
        if (!IsDirty) {
            Adopt(latest);
        } else if (latest.Settings == Baseline.Settings) {
            Baseline = latest;
            HasConflict = false;
        } else {
            HasConflict = true;
        }
    }

    public void Accept(SharedProviderImportSubmission submission, SharedProviderImportBaseline accepted) {
        EnsureIdentity(accepted);
        if (submission.DraftId != Id) {
            return;
        }
        if (aliasRevision == submission.AliasRevision) {
            localAlias = accepted.Settings.LocalAlias;
            Context.MarkAsUnmodified(new(this, nameof(LocalAlias)));
        }
        if (enabledRevision == submission.EnabledRevision) {
            isEnabled = accepted.Settings.IsEnabled;
            Context.MarkAsUnmodified(new(this, nameof(IsEnabled)));
        }
        Baseline = accepted;
        Latest = accepted;
        HasConflict = false;
        NeedsCurrentRead = false;
        Comparison = null;
    }

    public void Compare() {
        if (HasConflict && !NeedsCurrentRead) {
            Comparison = new(Baseline, Settings, Latest);
        }
    }

    public bool ApplyComparison() {
        if (NeedsCurrentRead || Comparison is not { CanApply: true } review || review.Current != Latest) {
            return false;
        }
        localAlias = review.Incomplete ? localAlias : review.Alias.Result;
        isEnabled = review.Enabled.Result;
        Baseline = review.Current;
        HasConflict = false;
        Comparison = null;
        return true;
    }

    private void Adopt(SharedProviderImportBaseline accepted) {
        localAlias = accepted.Settings.LocalAlias;
        isEnabled = accepted.Settings.IsEnabled;
        Baseline = accepted;
        HasConflict = false;
        NeedsCurrentRead = false;
        Comparison = null;
        Context.MarkAsUnmodified();
        validation.Clear();
        Context.NotifyValidationStateChanged();
    }

    private void EnsureIdentity(SharedProviderImportBaseline value) {
        if (value.ImportId != Baseline.ImportId || value.ProviderId != Baseline.ProviderId ||
            value.SourceId != Baseline.SourceId || value.PublicationId != Baseline.PublicationId) {
            throw new ArgumentException("The local draft belongs to a different import.", nameof(value));
        }
    }

    private void ValidateAlias() {
        validation.Clear();
        if (string.IsNullOrWhiteSpace(localAlias)) {
            validation.Add(new FieldIdentifier(this, nameof(LocalAlias)), "Enter a local alias.");
        }
        Context.NotifyValidationStateChanged();
    }
}

public sealed class SharedProviderImportComparison(SharedProviderImportBaseline original, SharedProviderLocalSettings local,
    SharedProviderImportBaseline current) {
    public SharedProviderImportBaseline Current { get; } = current;
    public MergeField<string> Alias { get; } = new(original.Settings.LocalAlias, local.LocalAlias, current.Settings.LocalAlias, StringComparer.Ordinal);
    public MergeField<bool> Enabled { get; } = new(original.Settings.IsEnabled, local.IsEnabled, current.Settings.IsEnabled);
    public bool Incomplete { get; } = string.IsNullOrWhiteSpace(local.LocalAlias);
    public bool KeepIncomplete { get; set; }
    public bool IsStale { get; private set; }
    public bool CanApply => !IsStale && (Incomplete ? KeepIncomplete : Alias.IsResolved) && Enabled.IsResolved;
    public void Invalidate() => IsStale = true;
}
