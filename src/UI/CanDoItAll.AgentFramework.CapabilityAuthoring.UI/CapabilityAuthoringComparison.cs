using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

public sealed record CapabilityExecutionConfiguration(CapabilityKind Kind, string Key, string Endpoint,
    string Json, bool IsBuiltIn) {
    public static CapabilityExecutionConfiguration From(CapabilityAuthoringSubmission value)
        => new(value.Kind, value.Key, value.EndpointOrPath, value.ConfigurationJson, value.IsBuiltIn);
}

public sealed class CapabilityAuthoringComparison {
    public CapabilityAuthoringComparison(CapabilityAuthoringSubmission original, CapabilityAuthoringSubmission local,
        CapabilityAuthoringSubmission current, long draftRevision, long changeSequence, bool incompleteConfiguration) {
        if (original.Id is null || original.Id != local.Id || local.Id != current.Id || string.IsNullOrWhiteSpace(current.ExpectedFingerprint)) {
            throw new InvalidOperationException("Capability comparison requires one saved identity and its current fingerprint.");
        }
        Current = current;
        Local = local;
        DraftRevision = draftRevision;
        ChangeSequence = changeSequence;
        HasIncompleteConfiguration = incompleteConfiguration;
        Name = new(original.Name, local.Name, current.Name, StringComparer.Ordinal);
        Description = TextMerge.Compare(original.Description, local.Description, current.Description);
        Configuration = new(CapabilityExecutionConfiguration.From(original), CapabilityExecutionConfiguration.From(local),
            CapabilityExecutionConfiguration.From(current));
        if (Configuration.CurrentChanged) {
            Configuration.RequireChoice();
        }
        Tags = CollectionMerge.Membership(original.Tags, local.Tags, current.Tags, StringComparer.OrdinalIgnoreCase);
    }

    public CapabilityAuthoringSubmission Current { get; }
    public CapabilityAuthoringSubmission Local { get; }
    public long DraftRevision { get; }
    public long ChangeSequence { get; }
    public MergeField<string> Name { get; }
    public TextMergeResult Description { get; }
    public MergeField<CapabilityExecutionConfiguration> Configuration { get; }
    public IReadOnlyList<string> Tags { get; }
    public bool HasIncompleteConfiguration { get; }
    public bool KeepIncompleteConfiguration { get; set; }
    public bool IsStale { get; private set; }
    public bool CanApply => !IsStale && Name.IsResolved && Description.Field.IsResolved &&
        (HasIncompleteConfiguration ? KeepIncompleteConfiguration : Configuration.IsResolved);
    public void Invalidate() => IsStale = true;

    public CapabilityEditorModel Result() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the capability comparison before applying it locally.");
        }
        var result = Local.ToEditorModel();
        result.Name = Name.Result;
        result.Description = Description.Field.Result;
        result.Tags = Tags.ToList();
        result.ExpectedFingerprint = Current.ExpectedFingerprint;
        if (!HasIncompleteConfiguration) {
            var configuration = Configuration.Result;
            result.Kind = configuration.Kind;
            result.Key = configuration.Key;
            result.EndpointOrPath = configuration.Endpoint;
            result.ConfigurationJson = configuration.Json;
            result.IsBuiltIn = configuration.IsBuiltIn;
        }
        return result;
    }
}
