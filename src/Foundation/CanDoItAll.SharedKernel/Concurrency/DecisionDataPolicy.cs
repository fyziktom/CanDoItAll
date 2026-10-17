using System.Text.Json.Serialization;

namespace CanDoItAll.SharedKernel;

public enum ReadBasisMode { RunSnapshot = 0, StepSnapshot = 1 }
public enum PublicationFreshness { PinnedObservation = 0, RequireCurrent = 1 }
public enum OnBasisChange { Fail = 0, PauseForReview = 1, Replan = 2 }

public enum DecisionDataPolicyError {
    UnknownMode,
    InvalidBudget,
    UnsupportedReadBasis,
    UnsupportedPublication,
    OwnerMandate,
    UnsupportedChangeResponse,
    IncompatibleReplanBudget
}

public sealed class DecisionDataPolicyException(DecisionDataPolicyError error, string message) : ArgumentException(message) {
    public DecisionDataPolicyError Error { get; } = error;
}

public sealed record DecisionDataPolicyBudget {
    public DecisionDataPolicyBudget(int maximumReplans, TimeSpan maximumWorkDuration, TimeSpan maximumBasisAge) {
        if (maximumReplans < 0 || maximumWorkDuration <= TimeSpan.Zero || maximumBasisAge <= TimeSpan.Zero) {
            throw new DecisionDataPolicyException(DecisionDataPolicyError.InvalidBudget,
                "A decision policy requires a nonnegative replan count and positive finite work and basis-age allowances.");
        }
        MaximumReplans = maximumReplans;
        MaximumWorkDuration = maximumWorkDuration;
        MaximumBasisAge = maximumBasisAge;
    }

    public int MaximumReplans { get; }
    public TimeSpan MaximumWorkDuration { get; }
    public TimeSpan MaximumBasisAge { get; }
}

[JsonConverter(typeof(DecisionDataPolicyJsonConverter))]
public sealed record DecisionDataPolicy {
    public const int SchemaVersion = 1;

    public DecisionDataPolicy(ReadBasisMode readBasisMode, PublicationFreshness publicationFreshness,
        OnBasisChange onBasisChange, DecisionDataPolicyBudget budget) {
        ArgumentNullException.ThrowIfNull(budget);
        RequireDefined(readBasisMode);
        RequireDefined(publicationFreshness);
        RequireDefined(onBasisChange);
        if ((onBasisChange == OnBasisChange.Replan) != (budget.MaximumReplans > 0)) {
            throw new DecisionDataPolicyException(DecisionDataPolicyError.IncompatibleReplanBudget,
                "Automatic replanning requires a positive allowance; other change responses require zero automatic replans.");
        }
        ReadBasisMode = readBasisMode;
        PublicationFreshness = publicationFreshness;
        OnBasisChange = onBasisChange;
        Budget = budget;
    }

    public ReadBasisMode ReadBasisMode { get; }
    public PublicationFreshness PublicationFreshness { get; }
    public OnBasisChange OnBasisChange { get; }
    public DecisionDataPolicyBudget Budget { get; }

    public static DecisionDataPolicy PlanningDefault(TimeSpan maximumWorkDuration, TimeSpan maximumBasisAge)
        => new(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.PauseForReview,
            new(0, maximumWorkDuration, maximumBasisAge));

    internal static void RequireDefined<T>(T value) where T : struct, Enum {
        if (!Enum.IsDefined(value)) {
            throw new DecisionDataPolicyException(DecisionDataPolicyError.UnknownMode, "The decision policy contains an unsupported enum value.");
        }
    }
}

public sealed record DecisionDataPolicyOverride(
    ReadBasisMode? ReadBasisMode = null,
    PublicationFreshness? PublicationFreshness = null,
    OnBasisChange? OnBasisChange = null,
    int? MaximumReplans = null,
    TimeSpan? MaximumWorkDuration = null,
    TimeSpan? MaximumBasisAge = null);

public sealed record DecisionDataPolicyCapabilities(
    bool SupportsRunSnapshot,
    bool SupportsStepSnapshot,
    bool SupportsPinnedObservation,
    bool SupportsCurrentPublication,
    bool SupportsPauseForReview,
    bool SupportsReplan,
    PublicationFreshness MinimumPublicationFreshness,
    DecisionDataPolicyBudget BudgetCeiling);
