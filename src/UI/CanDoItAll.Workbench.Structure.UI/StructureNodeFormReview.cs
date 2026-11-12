using System.Collections.Immutable;
using CanDoItAll.Editing;

namespace CanDoItAll.Workbench.Structure.UI;

public sealed class StructureNodeFormReview {
    public StructureNodeFormReview(StructureNodeFormSchema schema, StructureNodeFormValues original,
        StructureNodeFormValues local, StructureNodeFormValues current) {
        Schema = schema;
        Current = current;
        Title = new(original.Title, local.Title, current.Title, StringComparer.Ordinal);
        Subtitle = new(original.Subtitle, local.Subtitle, current.Subtitle, StringComparer.Ordinal);
        Notes = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
        var keys = schema.Fields.Select(field => field.Key).ToHashSet();
        if (!keys.SetEquals(original.Fields.Keys) || !keys.SetEquals(local.Fields.Keys) || !keys.SetEquals(current.Fields.Keys)) {
            throw new ArgumentException("A node comparison requires the exact controlled field identities from the same editor.");
        }
        Fields = schema.Fields.Select(field => new StructureNodeFieldReview(field,
            original.Fields[field.Key], local.Fields[field.Key], current.Fields[field.Key])).ToArray();
    }

    public StructureNodeFormSchema Schema { get; }
    public StructureNodeFormValues Current { get; }
    public MergeField<string> Title { get; }
    public MergeField<string> Subtitle { get; }
    public MergeField<string> Notes { get; }
    public IReadOnlyList<StructureNodeFieldReview> Fields { get; }
    public long InputRevision { get; private set; }
    public bool HasPendingInput => Fields.Any(row => row.HasPendingInput);
    public bool CanApply => Title.IsResolved && Subtitle.IsResolved && Notes.IsResolved &&
        (!Schema.ShowDefaultFields || !string.IsNullOrWhiteSpace(Title.Result)) &&
        (!Schema.InlineNote || !string.IsNullOrWhiteSpace(Notes.Result)) && Fields.All(row => row.CanApply);

    public void RecordInput() => InputRevision++;

    public StructureNodeFormValues Candidate() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the node fields and finish their result inputs before applying.");
        }
        var fields = Fields.ToImmutableDictionary(field => field.Definition.Key, field => field.Candidate());
        var values = new StructureNodeFormValues(Title.Result, Schema.InlineNote ? Current.Subtitle : Subtitle.Result, Notes.Result, fields);
        if ((long)values.Title.Length + values.Subtitle.Length + values.Notes.Length + Schema.Write(values).Sum(value => (long)value.Value.Length) > StructureNodeFormSchema.MaximumCharacters) {
            throw new InvalidOperationException("The node comparison result exceeds its supported size.");
        }
        return values;
    }

    public void PreserveChoices(StructureNodeFormReview previous) {
        if (previous.HasPendingInput) {
            throw new InvalidOperationException("Complete or clear the unfinished result before refreshing its comparison.");
        }
        Keep(previous.Title, Title);
        Keep(previous.Subtitle, Subtitle);
        Keep(previous.Notes, Notes);
        var fields = previous.Fields.ToDictionary(field => field.Definition.Key);
        foreach (var field in Fields) {
            if (fields.TryGetValue(field.Definition.Key, out var prior) && prior.Definition.Kind == field.Definition.Kind) {
                field.PreserveChoice(prior);
            }
        }
    }

    internal static bool Keep<T>(MergeField<T> previous, MergeField<T> current) {
        var equal = EqualityComparer<T>.Default;
        if (!equal.Equals(previous.Original, current.Original) || !equal.Equals(previous.Local, current.Local) ||
            !equal.Equals(previous.Current, current.Current)) {
            return false;
        }
        switch (previous.Choice) {
            case MergeChoice.Local:
                current.UseLocal();
                break;
            case MergeChoice.Current:
                current.UseCurrent();
                break;
            case MergeChoice.Custom:
                current.SetResult(previous.Result);
                break;
        }
        return true;
    }
}

public sealed class StructureNodeFieldReview {
    public StructureNodeFieldReview(StructureNodeFormField definition, StructureNodeFieldValue original,
        StructureNodeFieldValue local, StructureNodeFieldValue current) {
        Definition = definition;
        definition.Format(original);
        definition.Format(local);
        definition.Format(current);
        if (definition.Kind is StructureNodeFieldKind.Text or StructureNodeFieldKind.MultilineText) {
            Text = TextMerge.Compare(((StructureNodeFieldValue.Text)original).Value,
                ((StructureNodeFieldValue.Text)local).Value, ((StructureNodeFieldValue.Text)current).Value).Field;
        } else {
            Scalar = new(original, local, current);
        }
        SynchronizeInput();
    }

    public StructureNodeFormField Definition { get; }
    public MergeField<string>? Text { get; }
    public MergeField<StructureNodeFieldValue>? Scalar { get; }
    public string ResultInput { get; private set; } = string.Empty;
    public bool HasPendingInput { get; private set; }
    public bool IsResolved => Text?.IsResolved ?? Scalar!.IsResolved;
    public bool CanApply => !HasPendingInput && IsResolved && Definition.IsValid(Candidate());

    public void SetResultInput(string input) {
        ResultInput = input;
        try {
            var value = Definition.Parse(input);
            if (Text is { } text) {
                text.SetResult(((StructureNodeFieldValue.Text)value).Value);
            } else {
                Scalar!.SetResult(value);
            }
            HasPendingInput = false;
        } catch (ArgumentException) {
            HasPendingInput = true;
        }
    }

    public void SynchronizeInput() {
        ResultInput = Text is { } text ? text.IsResolved ? text.Result : text.Local
            : Definition.Format(Scalar!.IsResolved ? Scalar.Result : Scalar.Local);
        HasPendingInput = false;
    }

    public StructureNodeFieldValue Candidate() => Text is { } text ? new StructureNodeFieldValue.Text(text.Result) : Scalar!.Result;

    public void PreserveChoice(StructureNodeFieldReview previous) {
        var kept = Text is { } text && previous.Text is { } priorText
            ? StructureNodeFormReview.Keep(priorText, text)
            : Scalar is { } scalar && previous.Scalar is { } priorScalar && StructureNodeFormReview.Keep(priorScalar, scalar);
        if (kept) {
            ResultInput = previous.ResultInput;
        }
    }
}
