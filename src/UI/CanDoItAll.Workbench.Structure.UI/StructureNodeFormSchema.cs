using System.Collections.Immutable;
using System.Globalization;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Workbench.Structure.UI;

public readonly record struct StructureNodeFieldKey {
    public StructureNodeFieldKey(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128) {
            throw new ArgumentException("The node field key is too long.", nameof(value));
        }
        Value = value;
    }
    public string Value { get; }
}

public enum StructureNodeFieldKind { Text, MultilineText, Number, Date, DateTime, Choice }

public abstract record StructureNodeFieldValue {
    public sealed record Text(string Value) : StructureNodeFieldValue;
    public sealed record Number(decimal? Value) : StructureNodeFieldValue;
    public sealed record Date(DateTime? Value) : StructureNodeFieldValue;
    public sealed record Choice(string Key) : StructureNodeFieldValue;
}

public sealed record StructureNodeFieldOption(string Key, string Label);

public sealed record StructureNodeFormField(StructureNodeFieldKey Key, string Label, StructureNodeFieldKind Kind,
    bool IsRequired, IReadOnlyList<StructureNodeFieldOption> Options) {
    public StructureNodeFieldValue Parse(string text) => Kind switch {
        StructureNodeFieldKind.Text or StructureNodeFieldKind.MultilineText => new StructureNodeFieldValue.Text(text),
        StructureNodeFieldKind.Number when text.Length == 0 => new StructureNodeFieldValue.Number(null),
        StructureNodeFieldKind.Number when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => new StructureNodeFieldValue.Number(number),
        StructureNodeFieldKind.Date or StructureNodeFieldKind.DateTime when text.Length == 0 => new StructureNodeFieldValue.Date(null),
        StructureNodeFieldKind.Date when DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => new StructureNodeFieldValue.Date(date),
        StructureNodeFieldKind.DateTime when DateTime.TryParseExact(text,
            ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => new StructureNodeFieldValue.Date(date),
        StructureNodeFieldKind.Choice => new StructureNodeFieldValue.Choice(text),
        _ => throw new ArgumentException($"Complete or correct {Label} before comparing this node.")
    };

    public string Format(StructureNodeFieldValue value) => value switch {
        StructureNodeFieldValue.Text text when Kind is StructureNodeFieldKind.Text or StructureNodeFieldKind.MultilineText => text.Value,
        StructureNodeFieldValue.Number number when Kind == StructureNodeFieldKind.Number => number.Value?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty,
        StructureNodeFieldValue.Date date when Kind == StructureNodeFieldKind.Date => date.Value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        StructureNodeFieldValue.Date date when Kind == StructureNodeFieldKind.DateTime => date.Value?.ToString(
            date.Value is { } instant && instant.Ticks % TimeSpan.TicksPerSecond != 0 ? "yyyy-MM-ddTHH:mm:ss.FFFFFFF" : date.Value is { Second: not 0 } ? "yyyy-MM-ddTHH:mm:ss" : "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
        StructureNodeFieldValue.Choice choice when Kind == StructureNodeFieldKind.Choice => choice.Key,
        _ => throw new ArgumentException("The node comparison value does not match its field type.", nameof(value))
    };

    public bool IsValid(StructureNodeFieldValue value) {
        var text = Format(value);
        return (!IsRequired || !string.IsNullOrWhiteSpace(text)) &&
            (Kind != StructureNodeFieldKind.Choice || text.Length == 0 || Options.Any(option => option.Key == text));
    }
}

public sealed class StructureNodeFormSchema {
    public const int MaximumFields = 128;
    public const int MaximumCharacters = 2_000_000;

    public StructureNodeFormSchema(IReadOnlyList<CanvasWorkbenchInputField> fields, bool inlineNote = false, bool showDefaultFields = true) {
        if (fields.Count > MaximumFields || inlineNote && fields.Count != 0) {
            throw new ArgumentException("The node form exceeds its supported field scope.", nameof(fields));
        }
        InlineNote = inlineNote;
        ShowDefaultFields = showDefaultFields || inlineNote;
        Fields = fields.Select(field => new StructureNodeFormField(new(field.Key), field.Label, field.InputMode switch {
            "text" or "email" or "tel" or "url" => StructureNodeFieldKind.Text,
            "textarea" => StructureNodeFieldKind.MultilineText,
            "number" => StructureNodeFieldKind.Number,
            "date" => StructureNodeFieldKind.Date,
            "datetime-local" => StructureNodeFieldKind.DateTime,
            "select" => StructureNodeFieldKind.Choice,
            _ => throw new ArgumentException($"The node field {field.Label} does not support comparison.", nameof(fields))
        }, field.IsRequired, field.Options.Select(option => new StructureNodeFieldOption(option.Value, option.Label)).ToImmutableArray())).ToImmutableArray();
        if (Fields.Select(field => field.Key).Distinct().Count() != Fields.Count || Fields.Any(field =>
                field.Options.Select(option => option.Key).Distinct(StringComparer.Ordinal).Count() != field.Options.Count)) {
            throw new ArgumentException("The node form contains duplicate field or option identities.", nameof(fields));
        }
    }

    public bool InlineNote { get; }
    public bool ShowDefaultFields { get; }
    public IReadOnlyList<StructureNodeFormField> Fields { get; }

    public StructureNodeFormValues Read(string title, string subtitle, string notes, IReadOnlyList<CanvasWorkbenchInputValue>? inputs) {
        var values = inputs ?? [];
        if (values.Count != Fields.Count || values.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != values.Count) {
            throw new ArgumentException("The node draft does not contain its exact original field set.", nameof(inputs));
        }
        if ((long)title.Length + subtitle.Length + notes.Length + values.Sum(value => (long)value.Value.Length) > MaximumCharacters) {
            throw new ArgumentException("The node draft is too large to compare safely.", nameof(inputs));
        }
        var byKey = values.ToDictionary(value => new StructureNodeFieldKey(value.Key), value => value.Value);
        var result = ImmutableDictionary.CreateBuilder<StructureNodeFieldKey, StructureNodeFieldValue>();
        foreach (var field in Fields) {
            if (!byKey.TryGetValue(field.Key, out var text)) {
                throw new ArgumentException("The node draft field identities changed after the editor opened.", nameof(inputs));
            }
            result.Add(field.Key, field.Parse(text));
        }
        return new(title, subtitle, notes, result.ToImmutable());
    }

    public IReadOnlyList<CanvasWorkbenchInputValue> Write(StructureNodeFormValues values) => Fields.Select(field => new CanvasWorkbenchInputValue {
        Key = field.Key.Value,
        Value = field.Format(values.Fields[field.Key])
    }).ToArray();
}

public sealed record StructureNodeFormValues(string Title, string Subtitle, string Notes,
    IReadOnlyDictionary<StructureNodeFieldKey, StructureNodeFieldValue> Fields);
