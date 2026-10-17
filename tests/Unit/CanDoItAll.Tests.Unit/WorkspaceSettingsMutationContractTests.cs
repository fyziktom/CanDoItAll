using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceSettingsMutationContractTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DatasetNamespaceId Namespace = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly WorkspaceSettingsHead Head = new(Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"));

    [Fact]
    public void CapturedValuesPreserveAllSixFieldsWithoutAliasingTheEditor() {
        var provider = Guid.NewGuid();
        var editor = new WorkspaceSettingsModel {
            WorkspaceName = "  Planning  ",
            DefaultProviderProfileId = provider,
            DefaultPromptOutputFormat = "  Markdown  ",
            CurrencyCode = "eur",
            CurrencyCultureName = "de-DE",
            Notes = "  Keep raw notes\n  "
        };
        var values = WorkspaceSettingsValues.Capture(editor);
        editor.WorkspaceName = "Later typing";
        editor.DefaultProviderProfileId = null;
        editor.DefaultPromptOutputFormat = "JSON";
        editor.CurrencyCode = "USD";
        editor.CurrencyCultureName = "en-US";
        editor.Notes = "Later notes";

        Assert.Equal(new WorkspaceSettingsValues("  Planning  ", provider, "  Markdown  ", "eur", "de-DE", "  Keep raw notes\n  "), values);
        var restored = values.ToEditorModel();
        Assert.Equal(values, WorkspaceSettingsValues.Capture(restored));
        restored.WorkspaceName = "Independent editor";
        Assert.Equal("  Planning  ", values.WorkspaceName);
    }

    [Fact]
    public void ValuesRejectNullTextAndEmptyProviderWithoutAddingDefaults() {
        Assert.Throws<ArgumentNullException>(() => WorkspaceSettingsValues.Capture(null!));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsValues("", Guid.Empty, "", "", "", ""));
        foreach (var create in new Func<WorkspaceSettingsValues>[] {
            () => new(null!, null, "", "", "", ""),
            () => new("", null, null!, "", "", ""),
            () => new("", null, "", null!, "", ""),
            () => new("", null, "", "", null!, ""),
            () => new("", null, "", "", "", null!)
        }) {
            Assert.Throws<ArgumentNullException>(create);
        }
        var blank = new WorkspaceSettingsValues("", null, " ", "", "", "");
        Assert.Equal("", blank.WorkspaceName);
        Assert.Equal(" ", blank.DefaultPromptOutputFormat);
        Assert.Null(blank.DefaultProviderProfileId);
        Assert.Equal("CanDoItAll", new WorkspaceSettingsModel().WorkspaceName);
    }

    [Fact]
    public void HeadRequiresBothNonemptyIdentifiers() {
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsHead(Guid.Empty, Head.Revision));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsHead(Head.Id, Guid.Empty));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsHead(Guid.Empty, Guid.Empty));
        Assert.Equal(Head, JsonSerializer.Deserialize<WorkspaceSettingsHead>(JsonSerializer.Serialize(Head, Json), Json));
    }

    [Fact]
    public void ExpectationRequiresDatasetAndAnExplicitConsistentState() {
        Assert.Throws<ArgumentNullException>(() => new WorkspaceSettingsExpectation(null!, WorkspaceSettingsHeadState.Empty, null));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsExpectation(Namespace, default, null));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsExpectation(Namespace, (WorkspaceSettingsHeadState)99, Head));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsExpectation(Namespace, WorkspaceSettingsHeadState.Empty, Head));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsExpectation(Namespace, WorkspaceSettingsHeadState.Existing, null));
        Assert.Null(Empty().Head);
        Assert.Equal(Head, Existing().Head);
        Assert.NotEqual(Empty(), Existing());
        Assert.NotEqual(Empty(), new(new(Guid.NewGuid()), WorkspaceSettingsHeadState.Empty, null));
    }

    [Fact]
    public void ExpectationWireIsExplicitAndRejectsMissingUnknownOrContradictoryState() {
        const string empty = "{\"namespace\":\"11111111-1111-1111-1111-111111111111\",\"state\":\"empty\",\"head\":null}";
        Assert.Equal(empty, JsonSerializer.Serialize(Empty(), Json));
        Assert.Equal(Empty(), JsonSerializer.Deserialize<WorkspaceSettingsExpectation>(empty, Json));
        var existing = JsonSerializer.Serialize(Existing(), Json);
        Assert.Equal(Existing(), JsonSerializer.Deserialize<WorkspaceSettingsExpectation>(existing, Json));
        var configured = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        Assert.Equal(empty, JsonSerializer.Serialize(Empty(), configured));
        Assert.Equal(Existing(), JsonSerializer.Deserialize<WorkspaceSettingsExpectation>(existing, configured));

        foreach (var invalid in new[] {
            "null", "[]", "{}",
            empty.Replace("\"head\":null", "\"head\":{}", StringComparison.Ordinal),
            empty.Replace("\"state\":\"empty\",", "", StringComparison.Ordinal),
            empty.Replace(",\"head\":null", "", StringComparison.Ordinal),
            empty.Replace("\"state\":\"empty\"", "\"state\":0", StringComparison.Ordinal),
            empty.Replace("\"empty\"", "\"existing\"", StringComparison.Ordinal),
            empty.Replace("\"empty\"", "\"unknown\"", StringComparison.Ordinal),
            empty.Replace("\"empty\"", "null", StringComparison.Ordinal),
            empty.Replace("\"head\":null", "\"head\":null,\"state\":\"empty\"", StringComparison.Ordinal),
            empty.Replace("\"head\":null", "\"head\":null,\"current\":true", StringComparison.Ordinal),
            empty.Replace("\"11111111-1111-1111-1111-111111111111\"", "null", StringComparison.Ordinal),
            empty.Replace("11111111-1111-1111-1111-111111111111", Guid.Empty.ToString("D"), StringComparison.Ordinal),
            existing.Replace("\"existing\"", "\"empty\"", StringComparison.Ordinal),
            existing.Replace(Head.Revision.ToString("D"), Guid.Empty.ToString("D"), StringComparison.Ordinal),
            existing.Replace("\"head\":{", "\"head\":{\"id\":\"44444444-4444-4444-4444-444444444444\",", StringComparison.Ordinal),
            existing.Replace("\"head\":{", "\"head\":{\"revision\":\"44444444-4444-4444-4444-444444444444\",", StringComparison.Ordinal)
        }) {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkspaceSettingsExpectation>(invalid, Json));
        }
    }

    [Theory]
    [InlineData("workspaceName")]
    [InlineData("defaultProviderProfileId")]
    [InlineData("defaultPromptOutputFormat")]
    [InlineData("currencyCode")]
    [InlineData("currencyCultureName")]
    [InlineData("notes")]
    public void CompleteValuesRequireEveryWireMemberIncludingAnExplicitNullProvider(string omitted) {
        var values = Values();
        var configured = new JsonSerializerOptions(Json) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var wire = JsonSerializer.SerializeToNode(values, configured)!.AsObject();
        Assert.Equal(6, wire.Count);
        Assert.True(wire.ContainsKey("defaultProviderProfileId"));
        Assert.Null(wire["defaultProviderProfileId"]);
        Assert.Equal(values, wire.Deserialize<WorkspaceSettingsValues>(configured));
        Assert.True(wire.Remove(omitted));
        Assert.Throws<JsonException>(() => wire.Deserialize<WorkspaceSettingsValues>(configured));
    }

    [Fact]
    public void SnapshotAndCommandRoundTripTheOriginalExpectationAndExactValues() {
        foreach (var expected in new[] { Empty(), Existing() }) {
            var snapshot = new WorkspaceSettingsSnapshot(expected, Values());
            var command = new ReplaceWorkspaceSettingsCommand(expected, snapshot.Values);
            Assert.Equal(snapshot, JsonSerializer.Deserialize<WorkspaceSettingsSnapshot>(JsonSerializer.Serialize(snapshot, Json), Json));
            Assert.Equal(command, JsonSerializer.Deserialize<ReplaceWorkspaceSettingsCommand>(JsonSerializer.Serialize(command, Json), Json));
            var wire = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            Assert.Equal(new[] { "expected", "values" }, wire.Select(property => property.Key).Order(StringComparer.Ordinal));
            Assert.Equal(expected, wire["expected"]!.Deserialize<WorkspaceSettingsExpectation>(Json));
            Assert.Equal("  Original  ", wire["values"]!["workspaceName"]!.GetValue<string>());
        }
    }

    [Fact]
    public void SnapshotAndCommandRejectMissingOrNullOriginalEvidence() {
        Assert.Throws<ArgumentNullException>(() => new WorkspaceSettingsSnapshot(null!, Values()));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceSettingsSnapshot(Empty(), null!));
        Assert.Throws<ArgumentNullException>(() => new ReplaceWorkspaceSettingsCommand(null!, Values()));
        Assert.Throws<ArgumentNullException>(() => new ReplaceWorkspaceSettingsCommand(Empty(), null!));
        var command = JsonSerializer.SerializeToNode(new ReplaceWorkspaceSettingsCommand(Empty(), Values()), Json)!.AsObject();
        var snapshot = JsonSerializer.SerializeToNode(new WorkspaceSettingsSnapshot(Empty(), Values()), Json)!.AsObject();
        command.Remove("expected");
        snapshot.Remove("expectation");
        Assert.Throws<JsonException>(() => command.Deserialize<ReplaceWorkspaceSettingsCommand>(Json));
        Assert.Throws<JsonException>(() => snapshot.Deserialize<WorkspaceSettingsSnapshot>(Json));
        command["expected"] = null;
        snapshot["expectation"] = null;
        Assert.Throws<JsonException>(() => command.Deserialize<ReplaceWorkspaceSettingsCommand>(Json));
        Assert.Throws<JsonException>(() => snapshot.Deserialize<WorkspaceSettingsSnapshot>(Json));
    }

    [Fact]
    public void OutcomesKeepAcceptedEvidenceSeparateFromRefusalAndUncertainty() {
        var snapshot = new WorkspaceSettingsSnapshot(Existing(), Values());
        Assert.Throws<ArgumentNullException>(() => new WorkspaceSettingsSaveOutcome.Accepted(null!));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsSaveOutcome.Accepted(new(Empty(), Values())));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceSettingsSaveOutcome.Accepted(snapshot, (WorkspaceSettingsSecondaryDiagnostic)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceSettingsSaveOutcome.Rejected(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceSettingsSaveOutcome.Rejected((WorkspaceSettingsRejectionReason)99));
        WorkspaceSettingsSaveOutcome[] outcomes = [
            new WorkspaceSettingsSaveOutcome.Accepted(snapshot),
            new WorkspaceSettingsSaveOutcome.Accepted(snapshot, WorkspaceSettingsSecondaryDiagnostic.ActivityPending),
            new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.HeadChanged),
            new WorkspaceSettingsSaveOutcome.Unconfirmed()
        ];
        foreach (var outcome in outcomes) {
            var wire = JsonSerializer.Serialize(outcome, Json);
            Assert.Equal(outcome, JsonSerializer.Deserialize<WorkspaceSettingsSaveOutcome>(wire, Json));
        }
        var warning = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(outcomes[1]);
        Assert.Same(snapshot, warning.Snapshot);
        Assert.Equal(WorkspaceSettingsSecondaryDiagnostic.ActivityPending, warning.Diagnostic);
        Assert.Equal("{\"status\":\"unconfirmed\"}", JsonSerializer.Serialize(outcomes[3], Json));
    }

    [Fact]
    public void ImmutableWireObjectsRejectUnknownMembers() {
        AssertUnknown(Values());
        AssertUnknown(Head);
        AssertUnknown(new WorkspaceSettingsSnapshot(Existing(), Values()));
        AssertUnknown(new ReplaceWorkspaceSettingsCommand(Existing(), Values()));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkspaceSettingsSaveOutcome>("{\"status\":\"unconfirmed\",\"accepted\":true}", Json));
    }

    private static void AssertUnknown<T>(T value) {
        var wire = JsonSerializer.SerializeToNode(value, Json)!.AsObject();
        wire["unknown"] = true;
        Assert.Throws<JsonException>(() => wire.Deserialize<T>(Json));
    }

    private static WorkspaceSettingsExpectation Empty() => new(Namespace, WorkspaceSettingsHeadState.Empty, null);
    private static WorkspaceSettingsExpectation Existing() => new(Namespace, WorkspaceSettingsHeadState.Existing, Head);
    private static WorkspaceSettingsValues Values() => new("  Original  ", null, " Markdown ", "eur", "de-DE", "  Keep notes  ");
}
