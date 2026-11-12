using System.Globalization;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Prompts;

namespace CanDoItAll.Prompts.UI.Editor;

public readonly record struct PromptModelIdentity(string Provider, string Model) {
    public static PromptModelIdentity From(PromptProviderModel value)
        => new(value.Provider.Trim().ToUpperInvariant(), value.Model.Trim().ToUpperInvariant());
}

public readonly record struct PromptNumericDraft<T>(string Raw, T? Value) where T : struct, IParsable<T>, IFormattable {
    public static PromptNumericDraft<T> From(string raw)
        => new(raw, T.TryParse(raw, CultureInfo.InvariantCulture, out var value) ? value : null);
    public static PromptNumericDraft<T> FromValue(T? value) => From(PromptGalleryDraftCapture.Format(value));
}

public sealed class PromptMergeReview {
    public PromptMergeReview(PromptGalleryEditorSource original, PromptGalleryDraftCapture local, PromptGalleryItemDetails current) {
        Local = local;
        Current = current;
        Title = new(original.Title, local.Values.Title, current.Title);
        Summary = new(original.Summary, local.Values.Summary, current.Summary);
        Kind = new(original.Kind, local.Values.Kind, current.Kind);
        Phase = new(original.Phase, local.Values.Phase, current.Phase);
        Text = TextMerge.Compare(original.Content, local.Values.Content, current.DraftContent);
        Tags = Membership(original.Tags, local.Values.Tags, current.Tags, StringComparer.OrdinalIgnoreCase,
            values => values.Select(value => value.Trim()).Where(value => value.Length > 0).ToArray());
        Consumers = Membership(original.SupportedConsumers, local.Values.SupportedConsumers, current.SupportedConsumers);
        Models = CollectionMerge.ByKey(original.SupportedModels, local.Values.SupportedModels,
            current.SupportedModels, PromptModelIdentity.From, valueComparer: new ModelEquality());
        Temperature = new(PromptNumericDraft<double>.FromValue(original.Recommendations.Temperature),
            PromptNumericDraft<double>.From(local.Temperature), PromptNumericDraft<double>.FromValue(current.Recommendations.Temperature));
        MaxOutputTokens = new(PromptNumericDraft<int>.FromValue(original.Recommendations.MaxOutputTokens),
            PromptNumericDraft<int>.From(local.MaxOutputTokens), PromptNumericDraft<int>.FromValue(current.Recommendations.MaxOutputTokens));
        TopP = new(PromptNumericDraft<double>.FromValue(original.Recommendations.TopP),
            PromptNumericDraft<double>.From(local.TopP), PromptNumericDraft<double>.FromValue(current.Recommendations.TopP));
    }

    public PromptGalleryDraftCapture Local { get; }
    public PromptGalleryItemDetails Current { get; }
    public MergeField<string> Title { get; }
    public MergeField<string> Summary { get; }
    public MergeField<PromptGalleryItemKind> Kind { get; }
    public MergeField<string> Phase { get; }
    public TextMergeResult Text { get; }
    public MergeField<IReadOnlyList<string>> Tags { get; }
    public MergeField<IReadOnlyList<PromptGalleryConsumer>> Consumers { get; }
    public IReadOnlyList<KeyedMergeRow<PromptModelIdentity, PromptProviderModel>> Models { get; }
    public MergeField<PromptNumericDraft<double>> Temperature { get; }
    public MergeField<PromptNumericDraft<int>> MaxOutputTokens { get; }
    public MergeField<PromptNumericDraft<double>> TopP { get; }
    public bool IsResolved => Title.IsResolved && Summary.IsResolved && Kind.IsResolved && Phase.IsResolved && Text.Field.IsResolved &&
        Tags.IsResolved && Consumers.IsResolved && Models.All(row => row.Field.IsResolved) &&
        Temperature.IsResolved && MaxOutputTokens.IsResolved && TopP.IsResolved;
    public string? GroupError => Tags.IsResolved && Tags.Result.Count > PromptGalleryLimits.MaximumTags
        ? "The result can contain at most 50 tags."
        : Models.Count(row => row.Field.IsResolved && row.Field.Result.Exists) > PromptGalleryLimits.MaximumSupportedModels
        ? "The result can contain at most 100 supported provider models."
        : Models.Where(row => row.Field.IsResolved && row.Field.Result.Exists)
        .Count(row => row.Field.Result.Value!.IsPreferred) > 1 ? "Choose at most one preferred provider model."
        : Temperature.IsResolved && Temperature.Result.Value is { } temperature && (!double.IsFinite(temperature) || temperature is < 0 or > 2) ? "Temperature must be between 0 and 2."
        : TopP.IsResolved && TopP.Result.Value is { } topP && (!double.IsFinite(topP) || topP is < 0 or > 1) ? "Top-p must be between 0 and 1."
        : MaxOutputTokens.IsResolved && MaxOutputTokens.Result.Value is < 1 or > 1_000_000 ? "Maximum output tokens must be between 1 and 1,000,000." : null;
    public bool CanApply => IsResolved && GroupError is null;

    public PromptGalleryDraftCapture Result() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the field choices and preferred model group before applying.");
        }
        return CaptureCandidate();
    }

    public PromptGalleryDraftCapture CaptureCandidate() {
        var models = Models.Select(row => Chosen(row.Field)).Where(entry => entry.Exists).Select(entry => entry.Value!).ToArray();
        var values = new PromptGalleryEditorSubmission(Chosen(Title), Chosen(Summary), Chosen(Kind), Chosen(Phase),
            Chosen(Text.Field), Chosen(Tags), models, Chosen(Consumers),
            new(Chosen(Temperature).Value, Chosen(MaxOutputTokens).Value, Chosen(TopP).Value));
        return Local with { Values = values, Temperature = Chosen(Temperature).Raw,
            MaxOutputTokens = Chosen(MaxOutputTokens).Raw, TopP = Chosen(TopP).Raw };
    }

    private static T Chosen<T>(MergeField<T> field) => field.IsResolved ? field.Result : field.Local;

    private static MergeField<IReadOnlyList<T>> Membership<T>(IReadOnlyList<T> original, IReadOnlyList<T> local,
        IReadOnlyList<T> current, IEqualityComparer<T>? comparer = null, Func<IReadOnlyList<T>, IReadOnlyList<T>>? normalize = null) where T : notnull {
        original = normalize?.Invoke(original) ?? original;
        local = normalize?.Invoke(local) ?? local;
        current = normalize?.Invoke(current) ?? current;
        var field = new MergeField<IReadOnlyList<T>>(original, local, current, new SetEquality<T>(comparer));
        field.SetResult(CollectionMerge.Membership(original, local, current, comparer));
        return field;
    }

    private sealed class SetEquality<T>(IEqualityComparer<T>? comparer) : IEqualityComparer<IReadOnlyList<T>> {
        public bool Equals(IReadOnlyList<T>? x, IReadOnlyList<T>? y) => x is not null && y is not null && x.ToHashSet(comparer).SetEquals(y);
        public int GetHashCode(IReadOnlyList<T> value) => value.Distinct(comparer).Count();
    }
    private sealed class ModelEquality : IEqualityComparer<PromptProviderModel> {
        public bool Equals(PromptProviderModel? x, PromptProviderModel? y) => x is not null && y is not null &&
            PromptModelIdentity.From(x) == PromptModelIdentity.From(y) && x.IsPreferred == y.IsPreferred;
        public int GetHashCode(PromptProviderModel value) => HashCode.Combine(PromptModelIdentity.From(value), value.IsPreferred);
    }
}
