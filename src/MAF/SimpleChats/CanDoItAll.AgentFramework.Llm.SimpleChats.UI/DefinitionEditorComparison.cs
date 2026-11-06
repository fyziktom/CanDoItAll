using System.Collections.Immutable;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

public sealed record DefinitionRuntimeChoice(Guid? ProviderId, string Model, DefinitionEditorEffort? Effort, string Parameters);
public sealed record DefinitionOutputChoice(DefinitionEditorFormat Format, string Schema, string Name, string Description);

public sealed class DefinitionEditorComparison {
    private readonly DefinitionEditorValues local;

    public DefinitionEditorComparison(DefinitionEditorValues original, DefinitionEditorValues local, DefinitionEditorValues current) {
        this.local = local;
        Name = new(original.Name, local.Name, current.Name, StringComparer.Ordinal);
        Summary = TextMerge.Compare(original.Summary, local.Summary, current.Summary).Field;
        Avatar = new(original.AvatarImageUrl, local.AvatarImageUrl, current.AvatarImageUrl, StringComparer.Ordinal);
        Prompt = TextMerge.Compare(original.SystemPrompt, local.SystemPrompt, current.SystemPrompt).Field;
        Runtime = new(RuntimeOf(original), RuntimeOf(local), RuntimeOf(current));
        Temperature = new(original.TemperatureText, local.TemperatureText, current.TemperatureText, StringComparer.Ordinal);
        Timeout = new(original.TimeoutText, local.TimeoutText, current.TimeoutText, StringComparer.Ordinal);
        Output = new(OutputOf(original), OutputOf(local), OutputOf(current));
        Tags = new(original.Tags, local.Tags, current.Tags, TagEquality.Instance);
        PendingTag = new(original.PendingTagInput, local.PendingTagInput, current.PendingTagInput, StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(local.PendingTagInput)) {
            PendingTag.RequireChoice();
        }
    }

    public MergeField<string> Name { get; }
    public MergeField<string> Summary { get; }
    public MergeField<string> Avatar { get; }
    public MergeField<string> Prompt { get; }
    public MergeField<DefinitionRuntimeChoice> Runtime { get; }
    public MergeField<string> Temperature { get; }
    public MergeField<string> Timeout { get; }
    public MergeField<DefinitionOutputChoice> Output { get; }
    public MergeField<ImmutableArray<string>> Tags { get; }
    public MergeField<string> PendingTag { get; }
    public bool IsResolved => Name.IsResolved && Summary.IsResolved && Avatar.IsResolved && Prompt.IsResolved && Runtime.IsResolved
        && Temperature.IsResolved && Timeout.IsResolved && Output.IsResolved && Tags.IsResolved && PendingTag.IsResolved;

    public DefinitionEditorValues Apply() {
        if (!IsResolved) {
            throw new InvalidOperationException("Choose a result for every changed field before applying the comparison.");
        }
        return local with {
            Name = Name.Result, Summary = Summary.Result, AvatarImageUrl = Avatar.Result, SystemPrompt = Prompt.Result,
            ProviderProfileId = Runtime.Result.ProviderId, Model = Runtime.Result.Model, ThinkingEffort = Runtime.Result.Effort,
            ModelParameterConfigurationJson = Runtime.Result.Parameters, TemperatureInput = Temperature.Result, TimeoutInput = Timeout.Result,
            ResponseFormat = Output.Result.Format, SchemaJson = Output.Result.Schema, SchemaName = Output.Result.Name,
            SchemaDescription = Output.Result.Description, Tags = Tags.Result, PendingTagInput = PendingTag.Result
        };
    }

    public static DefinitionEditorValues Acknowledge(DefinitionEditorValues submitted, DefinitionEditorValues local, DefinitionEditorValues accepted) {
        var fields = new DefinitionEditorComparison(submitted, local, accepted);
        KeepLater(fields.Name);
        KeepLater(fields.Summary);
        KeepLater(fields.Avatar);
        KeepLater(fields.Prompt);
        KeepLater(fields.Runtime);
        KeepLater(fields.Temperature);
        KeepLater(fields.Timeout);
        KeepLater(fields.Output);
        KeepLater(fields.Tags);
        KeepLater(fields.PendingTag);
        return fields.Apply() with { RevisionReason = local.RevisionReason == submitted.RevisionReason ? accepted.RevisionReason : local.RevisionReason };
    }

    public static bool Same(DefinitionEditorValues first, DefinitionEditorValues second)
        => first.Name == second.Name && first.Summary == second.Summary && first.AvatarImageUrl == second.AvatarImageUrl
            && first.SystemPrompt == second.SystemPrompt && RuntimeOf(first) == RuntimeOf(second)
            && first.TemperatureText == second.TemperatureText && first.TimeoutText == second.TimeoutText
            && OutputOf(first) == OutputOf(second) && TagEquality.Instance.Equals(first.Tags, second.Tags)
            && first.RevisionReason == second.RevisionReason && first.PendingTagInput == second.PendingTagInput;

    private static void KeepLater<T>(MergeField<T> field) {
        if (field.LocalChanged) {
            field.UseLocal();
        }
    }
    private static DefinitionRuntimeChoice RuntimeOf(DefinitionEditorValues value)
        => new(value.ProviderProfileId, value.Model, value.ThinkingEffort, value.ModelParameterConfigurationJson);
    private static DefinitionOutputChoice OutputOf(DefinitionEditorValues value)
        => new(value.ResponseFormat, value.SchemaJson, value.SchemaName, value.SchemaDescription);

    private sealed class TagEquality : IEqualityComparer<ImmutableArray<string>> {
        public static TagEquality Instance { get; } = new();
        public bool Equals(ImmutableArray<string> first, ImmutableArray<string> second) => first.SequenceEqual(second, StringComparer.Ordinal);
        public int GetHashCode(ImmutableArray<string> value) {
            var hash = new HashCode();
            foreach (var tag in value) {
                hash.Add(tag, StringComparer.Ordinal);
            }
            return hash.ToHashCode();
        }
    }
}
