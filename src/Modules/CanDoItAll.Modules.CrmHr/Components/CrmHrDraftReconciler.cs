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
// A draft takes the owner's values when the target changed, when its own commit caused the reload, or when it is
// clean. A dirty draft of the same target is merged in place: every field the operator did not touch takes the owner's
// newer value, every touched field keeps the operator's value, and the instance (and the EditContext bound to it)
// survives. A list draft has no fields to merge and is kept whole while it is dirty.
public sealed class CrmHrDraftReconciler
{
    private const string WholeDraft = "";
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> FieldsByType = new();
    private readonly Dictionary<string, Dictionary<string, string>> baselines = new(StringComparer.Ordinal);

    // True when the draft no longer equals the owner values it is based on.
    public bool IsDirty<TDraft>(string editor, TDraft draft)
        where TDraft : class
    {
        ArgumentNullException.ThrowIfNull(draft);
        return baselines.TryGetValue(editor, out var baseline) && !Matches(baseline, Fingerprint(draft));
    }

    // Replaces the draft and records the owner values it starts from.
    public TDraft Replace<TDraft>(string editor, TDraft fresh)
        where TDraft : class
    {
        ArgumentNullException.ThrowIfNull(fresh);
        baselines[editor] = Fingerprint(fresh);
        return fresh;
    }

    public TDraft Reconcile<TDraft>(string editor, TDraft current, Func<TDraft> fresh, bool targetChanged, bool committed)
        where TDraft : class
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(fresh);
        if (targetChanged || committed || !baselines.TryGetValue(editor, out var baseline))
        {
            return Replace(editor, fresh());
        }

        var currentValues = Fingerprint(current);
        if (Matches(baseline, currentValues))
        {
            return Replace(editor, fresh());
        }

        if (currentValues.ContainsKey(WholeDraft))
        {
            return current;
        }

        var owner = fresh();
        var ownerValues = Fingerprint(owner);
        foreach (var field in Fields(current.GetType()))
        {
            var untouched = baseline.TryGetValue(field.Name, out var based) &&
                            string.Equals(based, currentValues[field.Name], StringComparison.Ordinal);
            if (untouched)
            {
                field.SetValue(current, field.GetValue(owner));
            }
        }

        baselines[editor] = ownerValues;
        return current;
    }

    // Forgets every baseline: the next reconcile of each editor takes the owner's values.
    public void Reset() => baselines.Clear();

    private static bool Matches(Dictionary<string, string> baseline, Dictionary<string, string> values)
        => baseline.Count == values.Count &&
           baseline.All(pair => values.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));

    private static Dictionary<string, string> Fingerprint(object draft)
    {
        if (draft is IEnumerable)
        {
            return new(StringComparer.Ordinal) { [WholeDraft] = JsonSerializer.Serialize(draft, draft.GetType()) };
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Fields(draft.GetType()))
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
}
