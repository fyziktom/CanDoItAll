using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

internal static class ProcessDecisionPolicyWire {
    internal static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 16,
        Converters = { new OverrideConverter() }
    };

    internal sealed record BudgetWire(
        [property: JsonRequired] int MaximumReplans,
        [property: JsonRequired] TimeSpan MaximumWorkDuration,
        [property: JsonRequired] TimeSpan MaximumBasisAge) {
        internal static BudgetWire From(DecisionDataPolicyBudget value)
            => new(value.MaximumReplans, value.MaximumWorkDuration, value.MaximumBasisAge);
        internal DecisionDataPolicyBudget ToModel() => new(MaximumReplans, MaximumWorkDuration, MaximumBasisAge);
    }

    internal sealed record CapabilitiesWire(
        [property: JsonRequired] bool SupportsRunSnapshot,
        [property: JsonRequired] bool SupportsStepSnapshot,
        [property: JsonRequired] bool SupportsPinnedObservation,
        [property: JsonRequired] bool SupportsCurrentPublication,
        [property: JsonRequired] bool SupportsPauseForReview,
        [property: JsonRequired] bool SupportsReplan,
        [property: JsonRequired] PublicationFreshness MinimumPublicationFreshness,
        [property: JsonRequired] BudgetWire BudgetCeiling) {
        internal static CapabilitiesWire From(DecisionDataPolicyCapabilities value)
            => new(value.SupportsRunSnapshot, value.SupportsStepSnapshot, value.SupportsPinnedObservation,
                value.SupportsCurrentPublication, value.SupportsPauseForReview, value.SupportsReplan,
                value.MinimumPublicationFreshness, BudgetWire.From(value.BudgetCeiling));
        internal DecisionDataPolicyCapabilities ToModel() {
            if (BudgetCeiling is null) {
                throw new InvalidDataException("The Process decision policy requires its owner budget ceiling.");
            }
            return new(SupportsRunSnapshot, SupportsStepSnapshot, SupportsPinnedObservation,
                SupportsCurrentPublication, SupportsPauseForReview, SupportsReplan, MinimumPublicationFreshness, BudgetCeiling.ToModel());
        }
    }

    private sealed class OverrideConverter : JsonConverter<DecisionDataPolicyOverride> {
        public override DecisionDataPolicyOverride? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using var document = JsonDocument.ParseValue(ref reader);
            return DecisionDataPolicyOverrideCodec.Deserialize(document.RootElement.GetRawText());
        }
        public override void Write(Utf8JsonWriter writer, DecisionDataPolicyOverride value, JsonSerializerOptions options)
            => writer.WriteRawValue(DecisionDataPolicyOverrideCodec.Serialize(value));
    }
}
