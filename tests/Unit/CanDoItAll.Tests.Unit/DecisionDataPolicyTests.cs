using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class DecisionDataPolicyTests {
    [Fact]
    public void DefaultPolicyIsExplicit() {
        var defaults = Default();
        Assert.Equal(ReadBasisMode.StepSnapshot, defaults.ReadBasisMode);
        Assert.Equal(PublicationFreshness.RequireCurrent, defaults.PublicationFreshness);
        Assert.Equal(OnBasisChange.PauseForReview, defaults.OnBasisChange);
        Assert.Equal(0, defaults.Budget.MaximumReplans);
        Assert.Equal(TimeSpan.FromMinutes(10), defaults.Budget.MaximumWorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(15), defaults.Budget.MaximumBasisAge);
        Assert.Equal(defaults, DecisionDataPolicyResolver.Resolve(defaults, Capabilities()));
    }

    [Fact]
    public void OverridePrecedenceIsDeterministic() {
        var owner = Capabilities() with { MinimumPublicationFreshness = PublicationFreshness.PinnedObservation };
        var workspace = new DecisionDataPolicyOverride(ReadBasisMode.RunSnapshot, PublicationFreshness.PinnedObservation, OnBasisChange.Fail,
            MaximumWorkDuration: TimeSpan.FromMinutes(18));
        var definition = new DecisionDataPolicyOverride(PublicationFreshness: PublicationFreshness.RequireCurrent,
            MaximumWorkDuration: TimeSpan.FromMinutes(17));
        var step = new DecisionDataPolicyOverride(OnBasisChange: OnBasisChange.Replan, MaximumReplans: 2,
            MaximumWorkDuration: TimeSpan.FromMinutes(16));
        var invocation = new DecisionDataPolicyOverride(ReadBasisMode: ReadBasisMode.StepSnapshot,
            MaximumWorkDuration: TimeSpan.FromMinutes(14));

        var resolved = DecisionDataPolicyResolver.Resolve(Default(), owner, workspace, definition, step, invocation);

        Assert.Equal(ReadBasisMode.StepSnapshot, resolved.ReadBasisMode);
        Assert.Equal(PublicationFreshness.RequireCurrent, resolved.PublicationFreshness);
        Assert.Equal(OnBasisChange.Replan, resolved.OnBasisChange);
        Assert.Equal(2, resolved.Budget.MaximumReplans);
        Assert.Equal(TimeSpan.FromMinutes(14), resolved.Budget.MaximumWorkDuration);
        Assert.Equal(Default().Budget.MaximumBasisAge, resolved.Budget.MaximumBasisAge);
        var inherited = DecisionDataPolicyResolver.Resolve(Default(), owner, workspace, definition);
        Assert.Equal(ReadBasisMode.RunSnapshot, inherited.ReadBasisMode);
        Assert.Equal(OnBasisChange.Fail, inherited.OnBasisChange);
        Assert.Equal(TimeSpan.FromMinutes(17), inherited.Budget.MaximumWorkDuration);
    }

    [Fact]
    public void OwnerMandateCannotBeWeakenedByInvocation() {
        var request = new DecisionDataPolicyOverride(PublicationFreshness: PublicationFreshness.PinnedObservation);
        var failure = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(), Capabilities(),
            authorizedInvocation: request));
        Assert.Equal(DecisionDataPolicyError.OwnerMandate, failure.Error);
        var historical = DecisionDataPolicyResolver.Resolve(Default(), Capabilities() with {
            MinimumPublicationFreshness = PublicationFreshness.PinnedObservation
        }, authorizedInvocation: request);
        Assert.Equal(PublicationFreshness.PinnedObservation, historical.PublicationFreshness);
        Assert.Equal(PublicationFreshness.RequireCurrent, Default().PublicationFreshness);
    }

    [Fact]
    public void UnsupportedPauseIsRejected() {
        var stateless = Capabilities() with { SupportsPauseForReview = false, SupportsReplan = false };
        var pause = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(), stateless));
        Assert.Equal(DecisionDataPolicyError.UnsupportedChangeResponse, pause.Error);
        var fail = DecisionDataPolicyResolver.Resolve(Default(), stateless,
            authorizedInvocation: new(OnBasisChange: OnBasisChange.Fail));
        Assert.Equal(OnBasisChange.Fail, fail.OnBasisChange);
        var replan = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(), stateless,
            authorizedInvocation: new(OnBasisChange: OnBasisChange.Replan, MaximumReplans: 1)));
        Assert.Equal(DecisionDataPolicyError.UnsupportedChangeResponse, replan.Error);
        var read = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(),
            Capabilities() with { SupportsRunSnapshot = false }, authorizedInvocation: new(ReadBasisMode: ReadBasisMode.RunSnapshot)));
        Assert.Equal(DecisionDataPolicyError.UnsupportedReadBasis, read.Error);
        var publication = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(),
            Capabilities() with { SupportsCurrentPublication = false }));
        Assert.Equal(DecisionDataPolicyError.UnsupportedPublication, publication.Error);
    }

    [Fact]
    public void UnknownModeIsRejected() {
        foreach (var preference in new[] {
            new DecisionDataPolicyOverride(ReadBasisMode: (ReadBasisMode)99),
            new DecisionDataPolicyOverride(PublicationFreshness: (PublicationFreshness)99),
            new DecisionDataPolicyOverride(OnBasisChange: (OnBasisChange)99)
        }) {
            var failure = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(), Capabilities(),
                workspace: preference, authorizedInvocation: new(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Fail)));
            Assert.Equal(DecisionDataPolicyError.UnknownMode, failure.Error);
        }
        Assert.Equal(DecisionDataPolicyError.UnknownMode, Assert.Throws<DecisionDataPolicyException>(() =>
            new DecisionDataPolicy((ReadBasisMode)(-1), PublicationFreshness.RequireCurrent, OnBasisChange.Fail, Default().Budget)).Error);
        Assert.Equal(DecisionDataPolicyError.UnknownMode, Assert.Throws<DecisionDataPolicyException>(() =>
            DecisionDataPolicyResolver.Resolve(Default(), Capabilities() with { MinimumPublicationFreshness = (PublicationFreshness)99 })).Error);
    }

    [Fact]
    public void NestedBudgetUsesTightestAllowance() {
        var request = new DecisionDataPolicyOverride(OnBasisChange: OnBasisChange.Replan, MaximumReplans: 5,
            MaximumWorkDuration: TimeSpan.FromMinutes(45), MaximumBasisAge: TimeSpan.FromMinutes(60));
        var enclosing = new[] {
            new DecisionDataPolicyBudget(2, TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(40)),
            new DecisionDataPolicyBudget(1, TimeSpan.FromMinutes(18), TimeSpan.FromMinutes(8))
        };
        var resolved = DecisionDataPolicyResolver.Resolve(Default(), Capabilities(), authorizedInvocation: request, enclosingBudgets: enclosing);
        Assert.Equal(1, resolved.Budget.MaximumReplans);
        Assert.Equal(TimeSpan.FromMinutes(12), resolved.Budget.MaximumWorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(8), resolved.Budget.MaximumBasisAge);
        Assert.Equal(5, request.MaximumReplans);
        var exhausted = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(), Capabilities(),
            authorizedInvocation: request, enclosingBudgets: [new(0, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))]));
        Assert.Equal(DecisionDataPolicyError.IncompatibleReplanBudget, exhausted.Error);
    }

    [Fact]
    public void InvalidBudgetIsRejected() {
        foreach (var invalid in new[] {
            new DecisionDataPolicyOverride(MaximumReplans: -1),
            new DecisionDataPolicyOverride(MaximumWorkDuration: TimeSpan.Zero),
            new DecisionDataPolicyOverride(MaximumWorkDuration: TimeSpan.FromTicks(-1)),
            new DecisionDataPolicyOverride(MaximumBasisAge: TimeSpan.Zero),
            new DecisionDataPolicyOverride(MaximumBasisAge: TimeSpan.FromTicks(-1))
        }) {
            var failure = Assert.Throws<DecisionDataPolicyException>(() => DecisionDataPolicyResolver.Resolve(Default(), Capabilities(),
                workspace: invalid, authorizedInvocation: new(MaximumReplans: 0, MaximumWorkDuration: TimeSpan.FromMinutes(1),
                    MaximumBasisAge: TimeSpan.FromMinutes(1))));
            Assert.Equal(DecisionDataPolicyError.InvalidBudget, failure.Error);
        }
        Assert.Equal(DecisionDataPolicyError.IncompatibleReplanBudget, Assert.Throws<DecisionDataPolicyException>(() =>
            DecisionDataPolicyResolver.Resolve(Default(), Capabilities(), authorizedInvocation: new(MaximumReplans: 1))).Error);
        Assert.Equal(DecisionDataPolicyError.IncompatibleReplanBudget, Assert.Throws<DecisionDataPolicyException>(() =>
            DecisionDataPolicyResolver.Resolve(Default(), Capabilities(), authorizedInvocation: new(OnBasisChange: OnBasisChange.Replan))).Error);
    }

    [Fact]
    public void VersionedPolicySerializationPreservesMeaning() {
        const string expected = "{\"schemaVersion\":1,\"readBasisMode\":1,\"publicationFreshness\":1,\"onBasisChange\":1,\"maximumReplans\":0,\"maximumWorkDuration\":\"00:10:00\",\"maximumBasisAge\":\"00:15:00\"}";
        var policy = Default();
        Assert.Equal(expected, DecisionDataPolicyCodec.Serialize(policy));
        Assert.Equal(policy, DecisionDataPolicyCodec.Deserialize(expected));
        var configured = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        configured.Converters.Add(new JsonStringEnumConverter());
        Assert.Equal(expected, JsonSerializer.Serialize(policy, configured));
        Assert.Equal(policy, JsonSerializer.Deserialize<DecisionDataPolicy>(expected, configured));
        var precise = new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.PinnedObservation, OnBasisChange.Replan,
            new(2, TimeSpan.FromTicks(1), TimeSpan.MaxValue));
        Assert.Equal(precise, DecisionDataPolicyCodec.Deserialize(DecisionDataPolicyCodec.Serialize(precise)));
        foreach (var invalid in new[] {
            expected.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            expected.Replace("\"schemaVersion\":1,", "", StringComparison.Ordinal),
            expected.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal),
            expected.Replace("\"readBasisMode\":1", "\"readBasisMode\":99", StringComparison.Ordinal),
            expected.Replace("\"maximumReplans\":0", "\"maximumReplans\":-1", StringComparison.Ordinal),
            expected.Replace("\"00:10:00\"", "\"-00:10:00\"", StringComparison.Ordinal),
            expected.Replace("\"00:15:00\"", "null", StringComparison.Ordinal),
            expected.Replace("\"onBasisChange\":1", "\"onBasisChange\":\"PauseForReview\"", StringComparison.Ordinal),
            expected[..^1] + ",\"callerMandate\":0}",
            "null",
            "[]",
            new string(' ', DecisionDataPolicyCodec.MaximumUtf8Bytes) + expected
        }) {
            Assert.Throws<JsonException>(() => DecisionDataPolicyCodec.Deserialize(invalid));
        }
    }

    private static DecisionDataPolicy Default() => DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15));

    private static DecisionDataPolicyCapabilities Capabilities() => new(true, true, true, true, true, true,
        PublicationFreshness.RequireCurrent, new(2, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30)));
}
