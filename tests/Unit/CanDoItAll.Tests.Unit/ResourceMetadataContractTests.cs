using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;

namespace CanDoItAll.Tests.Unit;

public sealed class ResourceMetadataContractTests {
    [Fact]
    public void Original_identity_is_explicit_nonempty_and_distinguishes_retained_bindings() {
        var context = Context();
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceMetadataRevision(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceMetadataContext(Guid.Empty, context.Namespace));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceMetadataExpectation.Absent(context, Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceProjectBinding.Bound(id, Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceProjectBinding.RetainedProject(Guid.Empty));
        var revision = new ResourceMetadataRevision(Guid.NewGuid());
        var retained = new ResourceMetadataExpectation.Existing(context, id, revision, new ResourceProjectBinding.RetainedProject(Guid.NewGuid()));
        var unbound = new ResourceMetadataExpectation.Existing(context, id, revision, new ResourceProjectBinding.RetainedUnbound());
        Assert.NotEqual<ResourceMetadataExpectation>(new ResourceMetadataExpectation.Absent(context, id), retained);
        Assert.NotEqual(retained, unbound);
        Assert.NotEqual(retained, new ResourceMetadataExpectation.Existing(Context(), id, revision, retained.Project));
    }

    [Fact]
    public void Capture_freezes_raw_fields_and_configuration_without_normalizing_or_aliasing() {
        var editor = Editor();
        editor.Name = "  submitted name  ";
        editor.Configuration.SetText("endpointUrl", "https://example.invalid/first");
        var values = ResourceMetadataValues.Capture(editor);
        editor.Name = "later edit";
        editor.Configuration.SetText("endpointUrl", "https://example.invalid/later");
        Assert.Equal("  submitted name  ", values.Name);
        Assert.Equal("https://example.invalid/first", values.Configuration["endpointUrl"]);
        var projected = values.ToEditorModel();
        projected.Configuration.SetText("endpointUrl", "another edit");
        Assert.Equal("https://example.invalid/first", values.ToEditorModel().Configuration.GetText("endpointUrl"));
        Assert.Null(projected.Id);
        Assert.Null(projected.ExpectedProjectAdmission);
    }

    [Fact]
    public void Capture_refuses_raw_byte_count_entry_and_aggregate_limits_before_accepting_values() {
        var editor = Editor();
        editor.Name = new('\u00e9', ResourceMetadataLimits.ScalarBytes / 2 + 1);
        Assert.Throws<ArgumentException>(() => ResourceMetadataValues.Capture(editor));
        editor = Editor();
        for (var index = 0; index <= ResourceMetadataLimits.ConfigurationEntries; index++) {
            editor.Configuration.SetText($"key-{index}", "value");
        }
        Assert.Throws<ArgumentException>(() => ResourceMetadataValues.Capture(editor));
        editor = Editor();
        for (var index = 0; index < 9; index++) {
            editor.Configuration.SetText($"key-{index}", new('x', ResourceMetadataLimits.ConfigurationValueBytes));
        }
        Assert.Throws<ArgumentException>(() => ResourceMetadataValues.Capture(editor));
        editor = Editor();
        editor.Configuration.SetText("too-long", new('x', ResourceMetadataLimits.ConfigurationValueBytes + 1));
        Assert.Throws<ArgumentException>(() => ResourceMetadataValues.Capture(editor));
    }

    [Fact]
    public void Invalid_references_enums_and_foreign_target_profile_cannot_form_a_command() {
        var editor = Editor();
        editor.LinkedSecretId = Guid.Empty;
        Assert.Throws<ArgumentException>(() => ResourceMetadataValues.Capture(editor));
        editor.LinkedSecretId = null;
        editor.Sensitivity = (ResourceSensitivity)int.MaxValue;
        Assert.Throws<ArgumentException>(() => ResourceMetadataValues.Capture(editor));
        var expected = new ResourceMetadataExpectation.Absent(Context(), Guid.NewGuid());
        var foreign = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => new ReplaceResourceMetadataCommand(expected, foreign, ResourceMetadataValues.Capture(Editor())));
    }

    [Fact]
    public void Snapshot_bound_counts_default_JSON_escaping_and_preserves_exact_values() {
        var expected = Existing();
        var values = Stored();
        var snapshot = new ResourceMetadataSnapshot(expected, values, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Assert.Same(values, snapshot.Values);
        Assert.Contains("exact ", snapshot.Values.Name, StringComparison.Ordinal);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(snapshot).Length < ResourceMetadataLimits.EncodedSnapshotBytes);
        var escaped = values with { ConfigJson = new('<', 200_000) };
        Assert.True(escaped.ConfigJson.Length < ResourceMetadataLimits.ConfigJsonBytes);
        Assert.Throws<ArgumentException>(() => new ResourceMetadataSnapshot(expected, escaped, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => new ResourceMetadataSnapshot(expected, values with { OwnerPartyId = Guid.Empty }, default, default));
        Assert.Throws<ArgumentException>(() => new ResourceMetadataSnapshot(expected, values with { ValidationStatus = (ResourceValidationStatus)int.MaxValue }, default, default));
    }

    [Theory]
    [InlineData("<")]
    [InlineData("\u001f")]
    [InlineData("\u00e9")]
    [InlineData("\ud83d\ude00")]
    [InlineData("<\"\\\ud83d\ude00")]
    public void Snapshot_accepts_the_exact_encoded_limit_and_refuses_one_more_byte(string escaped) {
        var expected = Existing();
        var values = Stored() with { Description = new('<', ResourceMetadataLimits.DescriptionBytes), ConfigJson = string.Empty };
        var empty = new ResourceMetadataSnapshot(expected, values, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var remaining = ResourceMetadataLimits.EncodedSnapshotBytes - JsonSerializer.SerializeToUtf8Bytes(empty).Length;
        var unitBytes = JsonSerializer.SerializeToUtf8Bytes(escaped).Length - 2;
        values = values with { ConfigJson = string.Concat(Enumerable.Repeat(escaped, remaining / unitBytes)) + new string('a', remaining % unitBytes) };
        var exact = new ResourceMetadataSnapshot(expected, values, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Assert.Equal(ResourceMetadataLimits.EncodedSnapshotBytes, JsonSerializer.SerializeToUtf8Bytes(exact).Length);
        Assert.Same(values, exact.Values);
        Assert.Throws<ArgumentException>(() => new ResourceMetadataSnapshot(expected,
            values with { ConfigJson = values.ConfigJson + "a" }, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Local_editor_projection_preserves_supported_secret_reference_and_refuses_ambiguous_retained_JSON() {
        var preparation = new ResourceMetadataPreparation(new ResourceConnectorPluginRegistry([new WebhookResourceConnectorPlugin()]));
        var secret = Guid.NewGuid();
        var values = Stored() with { LinkedSecretIdsJson = JsonSerializer.Serialize(new[] { secret }) };
        var snapshot = new ResourceMetadataSnapshot(Existing(), values, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Assert.Equal(secret, preparation.CreateEditor(snapshot).LinkedSecretId);
        Assert.Equal(ResourceMetadataReadOnlyReason.UnsupportedSecretReferences, preparation.Inspect(values with { LinkedSecretIdsJson = $"[\"{secret}\",\"{Guid.NewGuid()}\"]" }));
        Assert.Equal(ResourceMetadataReadOnlyReason.UnsupportedSecretReferences, preparation.Inspect(values with { LinkedSecretIdsJson = "null" }));
        Assert.Equal(ResourceMetadataReadOnlyReason.InvalidConfiguration, preparation.Inspect(values with { ConfigJson = "{\"name\":1,\"NAME\":2}" }));
        Assert.Equal(ResourceMetadataReadOnlyReason.InvalidConfiguration, preparation.Inspect(values with { ConfigSchemaVersion = "future-version" }));
        Assert.Equal(ResourceMetadataReadOnlyReason.UnavailableConnector, preparation.Inspect(values with { ConnectorPluginKey = "uninstalled-resource-plugin" }));
    }

    [Fact]
    public void Preparation_normalizes_only_the_captured_proposal_and_retains_the_full_locator_contract() {
        var context = Context();
        var admission = new ProjectWriteAdmission(context.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var editor = Editor();
        editor.Name = "  normalized  ";
        editor.Description = "  description  ";
        editor.Configuration.SetText("endpointUrl", "https://example.invalid/hook?token=fixture-only");
        editor.Configuration.SetText("method", "post");
        var command = new ReplaceResourceMetadataCommand(new ResourceMetadataExpectation.Absent(context, Guid.NewGuid()), admission, ResourceMetadataValues.Capture(editor));
        editor.Configuration.SetText("endpointUrl", "https://example.invalid/later");
        var preparation = new ResourceMetadataPreparation(new ResourceConnectorPluginRegistry([new WebhookResourceConnectorPlugin()]));
        var result = preparation.Prepare(command);
        Assert.Equal("normalized", result.Values.Name);
        Assert.Equal("description", result.Values.Description);
        Assert.Contains("fixture-only", result.Values.LocationOrIdentifier, StringComparison.Ordinal);
        Assert.Contains(result.Values.LocationOrIdentifier, result.Search.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("later", result.Values.ConfigJson, StringComparison.Ordinal);
        Assert.Equal("  normalized  ", command.Values.Name);
    }

    [Fact]
    public void Typed_outcomes_preserve_original_unknown_identity_and_exact_accepted_snapshot() {
        var expected = Existing();
        var bound = Assert.IsType<ResourceProjectBinding.Bound>(expected.Project);
        var command = new ReplaceResourceMetadataCommand(expected,
            new(expected.Context.DatabaseProfileId, bound.ProjectId, bound.LifetimeId), ResourceMetadataValues.Capture(Editor()));
        var unknown = new ResourceMetadataSaveOutcome.Unconfirmed(command);
        Assert.Same(command, unknown.Command);
        var snapshot = new ResourceMetadataSnapshot(expected, Stored(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var accepted = new ResourceMetadataSaveOutcome.Accepted(snapshot, ResourceMetadataSecondaryDiagnostic.ActivityPending);
        Assert.Same(snapshot, accepted.Snapshot);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceMetadataSaveOutcome.Rejected((ResourceMetadataRejectionReason)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceMetadataSaveOutcome.Accepted(snapshot, (ResourceMetadataSecondaryDiagnostic)int.MaxValue));
        var deletion = new DeleteResourceMetadataCommand(expected);
        Assert.Same(deletion, new ResourceMetadataDeleteOutcome.Unconfirmed(deletion).Command);
    }

    private static ResourceMetadataContext Context() => new(Guid.NewGuid(), new(Guid.NewGuid()));
    private static ResourceMetadataExpectation.Existing Existing()
        => new(Context(), Guid.NewGuid(), new(Guid.NewGuid()), new ResourceProjectBinding.Bound(Guid.NewGuid(), Guid.NewGuid()));
    private static ResourceEditorModel Editor() => new() {
        Name = "Resource", Description = "Description", ConnectorPluginKey = WebhookResourceConnectorPlugin.PluginKey, ConfigSchemaVersion = "1.0"
    };
    private static ResourceMetadataStoredValues Stored() => new(null, null, null, "exact ", "Description",
        WebhookResourceConnectorPlugin.PluginKey, "1.0", "https://example.invalid/hook",
        "{\"endpointUrl\":\"https://example.invalid/hook\",\"method\":\"post\"}", "[]",
        ResourceValidationStatus.Unknown, ResourceSensitivity.Normal, false, false);
}
