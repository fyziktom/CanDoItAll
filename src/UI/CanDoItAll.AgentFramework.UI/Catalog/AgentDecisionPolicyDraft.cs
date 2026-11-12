using System.Globalization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.UI.Catalog;

public sealed class AgentDecisionPolicyDraft {
    public ReadBasisMode? ReadBasisMode { get; set; }
    public PublicationFreshness? PublicationFreshness { get; set; }
    public OnBasisChange? OnBasisChange { get; set; }
    public int? MaximumReplans { get; set; }
    public string MaximumWorkDuration { get; set; } = string.Empty;
    public string MaximumBasisAge { get; set; } = string.Empty;

    public DecisionDataPolicyOverride Capture() {
        var result = new DecisionDataPolicyOverride(ReadBasisMode, PublicationFreshness, OnBasisChange, MaximumReplans,
            Duration(MaximumWorkDuration), Duration(MaximumBasisAge));
        _ = DecisionDataPolicyOverrideCodec.Serialize(result);
        return result;
    }

    public void Load(DecisionDataPolicyOverride? value) {
        ReadBasisMode = value?.ReadBasisMode;
        PublicationFreshness = value?.PublicationFreshness;
        OnBasisChange = value?.OnBasisChange;
        MaximumReplans = value?.MaximumReplans;
        MaximumWorkDuration = value?.MaximumWorkDuration?.ToString("c", CultureInfo.InvariantCulture) ?? string.Empty;
        MaximumBasisAge = value?.MaximumBasisAge?.ToString("c", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static TimeSpan? Duration(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        return TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out var duration) && duration > TimeSpan.Zero
            ? duration : throw new ArgumentException("Use a positive duration such as 00:05:00, or leave it blank to inherit.");
    }
}
