using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace CanDoItAll.Modules.CrmHr.Components;

// Decides what a read-back does to the editor drafts of one routed host. A record workspace has several independent
// editors over the same target (profile, connections, a new interaction, a skill, a capacity block). A reload of the
// same target, caused by another editor's commit, a sub-selection in the route or an agent refresh, must not discard
// what the operator typed into an editor that was not committed, and must not leave that editor holding stale owner
// values it would later write back.
//
// A draft takes the owner's values when the target changed or when it is clean. A dirty draft of the same target is
// merged in place: every field the operator did not touch takes the owner's newer value, every touched field keeps
// the operator's value, and the instance (and the EditContext bound to it) survives. A list draft has no fields to
// merge and is kept whole while it is dirty.
//
// The editor's own commit is reconciled against the submission it dispatched, not against the values it held before
// the operator started editing. A host records that submission with Submit before its first await, so the read-back
// can tell the owner's normalization and assigned identity (which every untouched field adopts) from what the
// operator typed while the write was in flight (which is kept, in the same draft instance and EditContext). With no
// later edits the read-back still starts a fresh draft from the owner's accepted values, exactly as before.
public sealed class CrmHrDraftReconciler
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> FieldsByType = new();
    private readonly Dictionary<string, DraftFingerprint> baselines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DraftFingerprint> submissions = new(StringComparer.Ordinal);

    // True when the draft no longer equals the owner values it is based on.
    public bool IsDirty<TDraft>(string editor, TDraft draft)
        where TDraft : class
    {
        ArgumentNullException.ThrowIfNull(draft);
        return baselines.TryGetValue(editor, out var baseline) && !baseline.Matches(Fingerprint(draft));
    }

    // Replaces the draft and records the owner values it starts from. Any submission still recorded for the editor
    // belongs to the draft being replaced and is retired with it.
    public TDraft Replace<TDraft>(string editor, TDraft fresh)
        where TDraft : class
    {
        ArgumentNullException.ThrowIfNull(fresh);
        baselines[editor] = Fingerprint(fresh);
        submissions.Remove(editor);
        return fresh;
    }

    // Records the values this editor dispatched to its owner and returns the submission the owner receives. The
    // recorded values are the operation's origin, retained separately from the live draft the operator keeps editing.
    public TSubmission Submit<TDraft, TSubmission>(string editor, TDraft draft, Func<TDraft, TSubmission> snapshot)
        where TDraft : class
        where TSubmission : class
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(snapshot);
        submissions[editor] = Fingerprint(draft);
        return snapshot(draft);
    }

    // True when the operator changed the draft after its submission was dispatched. A host uses it to decide whether
    // a create editor may start over once its own write was accepted.
    public bool ChangedAfterSubmission<TDraft>(string editor, TDraft draft)
        where TDraft : class
    {
        ArgumentNullException.ThrowIfNull(draft);
        return submissions.TryGetValue(editor, out var submitted) && !submitted.Matches(Fingerprint(draft));
    }

    // Forgets a dispatched submission: the owner rejected it, its lifetime was retired, or its read-back is done.
    public void Retire(string editor) => submissions.Remove(editor);

    public TDraft Reconcile<TDraft>(
        string editor,
        TDraft current,
        Func<TDraft> fresh,
        bool targetChanged,
        bool committed,
        Action<TDraft, TDraft>? adoptOwnerState = null)
        where TDraft : class
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(fresh);
        if (targetChanged)
        {
            return Replace(editor, fresh());
        }

        if (committed)
        {
            return ReconcileOwnCommit(editor, current, fresh, adoptOwnerState);
        }

        if (!baselines.TryGetValue(editor, out var baseline))
        {
            return Replace(editor, fresh());
        }

        var currentValues = Fingerprint(current);
        if (baseline.Matches(currentValues))
        {
            return Replace(editor, fresh());
        }

        if (currentValues.Items is not null)
        {
            return current;
        }

        return MergeFields(editor, current, fresh(), baseline, currentValues, adoptOwnerState);
    }

    // A list draft of the same target. Its own commit reconciles row by row: a row the operator did not touch after
    // the dispatch takes the owner's accepted row, including the identity the owner assigned to a row this commit
    // created, so the next save updates that row instead of recreating it. A row typed or added after the dispatch
    // keeps the operator's work. Rows are paired by the identity fields the caller names, never by position.
    public List<TItem> ReconcileList<TItem>(
        string editor,
        List<TItem> current,
        Func<List<TItem>> fresh,
        bool targetChanged,
        bool committed,
        params string[] identityFields)
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(identityFields);
        if (targetChanged)
        {
            return Replace(editor, fresh());
        }

        if (!committed)
        {
            return baselines.TryGetValue(editor, out var baseline) && !baseline.Matches(Fingerprint(current))
                ? current
                : Replace(editor, fresh());
        }

        if (!submissions.TryGetValue(editor, out var submitted))
        {
            return Replace(editor, fresh());
        }

        var currentValues = Fingerprint(current);
        submissions.Remove(editor);
        if (submitted.Matches(currentValues))
        {
            return Replace(editor, fresh());
        }

        var owner = fresh();
        var ownerValues = owner.Select(item => FieldValues(item)).ToList();
        var pairedOwnerRows = new bool[owner.Count];
        var pairedSubmittedRows = new bool[submitted.Items!.Count];
        var fields = Fields(typeof(TItem));
        for (var index = 0; index < current.Count; index++)
        {
            var row = currentValues.Items![index];
            var submittedRow = TakeMatch(submitted.Items, pairedSubmittedRows, row, identityFields);
            if (submittedRow < 0)
            {
                // Added after the dispatch: the owner has not seen this row and the operator still owns all of it.
                continue;
            }

            var ownerRow = TakeMatch(ownerValues, pairedOwnerRows, row, identityFields);
            if (ownerRow < 0)
            {
                continue;
            }

            foreach (var field in fields)
            {
                if (string.Equals(submitted.Items[submittedRow][field.Name], row[field.Name], StringComparison.Ordinal))
                {
                    field.SetValue(current[index], field.GetValue(owner[ownerRow]));
                }
            }
        }

        // The owner's rows are what this draft is now based on, so a row the operator added after the dispatch keeps
        // the list dirty and the next ordinary reload cannot quietly replace it.
        baselines[editor] = Fingerprint(owner);
        return current;
    }

    // Forgets every baseline and every dispatched submission: the next reconcile of each editor takes the owner's
    // values.
    public void Reset()
    {
        baselines.Clear();
        submissions.Clear();
    }

    private TDraft ReconcileOwnCommit<TDraft>(
        string editor,
        TDraft current,
        Func<TDraft> fresh,
        Action<TDraft, TDraft>? adoptOwnerState)
        where TDraft : class
    {
        if (!submissions.TryGetValue(editor, out var submitted))
        {
            return Replace(editor, fresh());
        }

        var currentValues = Fingerprint(current);
        submissions.Remove(editor);
        if (submitted.Matches(currentValues))
        {
            // Nothing was typed after the dispatch: the owner's accepted values are the whole truth of this editor.
            return Replace(editor, fresh());
        }

        if (currentValues.Items is not null)
        {
            return current;
        }

        return MergeFields(editor, current, fresh(), submitted, currentValues, adoptOwnerState);
    }

    private TDraft MergeFields<TDraft>(
        string editor,
        TDraft current,
        TDraft owner,
        DraftFingerprint reference,
        DraftFingerprint currentValues,
        Action<TDraft, TDraft>? adoptOwnerState)
        where TDraft : class
    {
        foreach (var field in Fields(current.GetType()))
        {
            var untouched = reference.Fields is { } referenceFields &&
                            referenceFields.TryGetValue(field.Name, out var based) &&
                            string.Equals(based, currentValues.Fields![field.Name], StringComparison.Ordinal);
            if (untouched)
            {
                field.SetValue(current, field.GetValue(owner));
            }
        }

        adoptOwnerState?.Invoke(current, owner);
        baselines[editor] = Fingerprint(owner);
        return current;
    }

    // The first not yet paired row of candidates whose identity fields equal the row's; -1 when there is none.
    private static int TakeMatch(
        IReadOnlyList<Dictionary<string, string>> candidates,
        bool[] paired,
        Dictionary<string, string> row,
        string[] identityFields)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (paired[index] || !identityFields.All(field =>
                    candidates[index].TryGetValue(field, out var candidate) &&
                    row.TryGetValue(field, out var value) &&
                    string.Equals(candidate, value, StringComparison.Ordinal)))
            {
                continue;
            }

            paired[index] = true;
            return index;
        }

        return -1;
    }

    private static DraftFingerprint Fingerprint(object draft)
    {
        if (draft is IEnumerable items and not string)
        {
            var rows = new List<Dictionary<string, string>>();
            foreach (var item in items)
            {
                rows.Add(item is null ? new Dictionary<string, string>(StringComparer.Ordinal) : FieldValues(item));
            }

            return new DraftFingerprint(null, rows);
        }

        return new DraftFingerprint(FieldValues(draft), null);
    }

    private static Dictionary<string, string> FieldValues(object draft)
    {
        var fields = Fields(draft.GetType());
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (fields.Length == 0)
        {
            // A row without settable fields (a value, an identifier) is compared as a whole.
            values[string.Empty] = JsonSerializer.Serialize(draft, draft.GetType());
            return values;
        }

        foreach (var field in fields)
        {
            values[field.Name] = JsonSerializer.Serialize(field.GetValue(draft), field.PropertyType);
        }

        return values;
    }

    private static PropertyInfo[] Fields(Type draftType)
        => FieldsByType.GetOrAdd(
            draftType,
            static type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetIndexParameters().Length == 0 &&
                                   property.GetMethod is { IsPublic: true } &&
                                   property.SetMethod is { IsPublic: true })
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray());

    private sealed record DraftFingerprint(
        Dictionary<string, string>? Fields,
        List<Dictionary<string, string>>? Items)
    {
        public bool Matches(DraftFingerprint other)
        {
            if (Fields is not null && other.Fields is not null)
            {
                return Matches(Fields, other.Fields);
            }

            if (Items is null || other.Items is null || Items.Count != other.Items.Count)
            {
                return false;
            }

            for (var index = 0; index < Items.Count; index++)
            {
                if (!Matches(Items[index], other.Items[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Matches(Dictionary<string, string> left, Dictionary<string, string> right)
            => left.Count == right.Count &&
               left.All(pair => right.TryGetValue(pair.Key, out var value) &&
                                string.Equals(pair.Value, value, StringComparison.Ordinal));
    }
}
