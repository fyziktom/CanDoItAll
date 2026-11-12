using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAcceptedSourceCodecTests {
    [Theory]
    [InlineData(SourcePrincipal.Local)]
    [InlineData(SourcePrincipal.Authenticated)]
    [InlineData(SourcePrincipal.OrganizationAgent)]
    [InlineData(SourcePrincipal.ProjectAgent)]
    public void Every_original_principal_and_its_project_lifetime_round_trip_without_reauthorization(SourcePrincipal kind) {
        var saved = Principal(kind);
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        AssertOriginal(saved, step, Read(saved, projection, step));
        AssertOriginal(saved, step, ProcessAcceptedSourceCodec.ReadLegacy(saved, step));
    }

    [Fact]
    public void Explicit_null_policy_is_refused_even_when_its_digest_is_consistent() {
        var saved = Principal(SourcePrincipal.Local);
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        var value = JsonNode.Parse(projection.Assignments[step].PayloadJson)!.AsObject();
        Assert.False(value.ContainsKey("decisionDataPolicy"));
        value["decisionDataPolicy"] = null;
        var changed = Resign(value.ToJsonString(), false, projection.Header.Fingerprint);
        Assert.Throws<JsonException>(() => Read(saved, projection, step, assignment: changed));
    }

    [Fact]
    public void Oversized_header_metadata_is_refused_before_it_can_be_accepted() {
        var saved = Principal(SourcePrincipal.OrganizationAgent, new string('x', ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes));
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Capture(saved));
    }

    [Fact]
    public void Explicit_legacy_read_preserves_metadata_above_the_new_projection_limit() {
        var saved = Saved();
        var initial = saved.Preparation.InitialCommit;
        var assignment = initial.InitialAssignments![0] with {
            LaunchVariables = new Dictionary<string, string> { ["LargeMetadata"] = new('x', ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes) }
        };
        var changed = saved.Preparation with { InitialCommit = initial with { InitialAssignments = [assignment] } };
        var legacy = ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(changed));
        AssertOriginal(legacy, assignment.StepInstanceId, ProcessAcceptedSourceCodec.ReadLegacy(legacy, assignment.StepInstanceId));
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Capture(legacy));
    }

    [Theory]
    [InlineData(1, 256)]
    [InlineData(64, 256)]
    [InlineData(1, 1_048_576)]
    public void Round_trip_preserves_original_source_and_selected_policy_without_prompts(int steps, int promptCharacters) {
        var saved = Saved(steps, promptCharacters);
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var selected = saved.Preparation.InitialCommit.InitialAssignments![^1];
        AssertOriginal(saved, selected.StepInstanceId, Read(saved, projection, selected.StepInstanceId));
        Assert.Equal(steps, projection.Assignments.Count);
        Assert.DoesNotContain(selected.Prompt, projection.Header.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(selected.Prompt, projection.Assignments[selected.StepInstanceId].PayloadJson, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(projection.Header.PayloadJson) < 4096);
        Assert.True(Encoding.UTF8.GetByteCount(projection.Assignments[selected.StepInstanceId].PayloadJson) < 4096);
    }

    [Theory]
    [InlineData(ProcessAcceptedSourceVersion.LegacyPreparedLaunch)]
    [InlineData((ProcessAcceptedSourceVersion)2)]
    public void Another_format_never_falls_back_to_projected_evidence(ProcessAcceptedSourceVersion version) {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var selected = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        Assert.Throws<NotSupportedException>(() => ProcessAcceptedSourceCodec.Read(version, saved.Preparation.AdmissionId,
            selected, saved.PreparationFingerprint, projection.Header, projection.Assignments[selected]));
    }

    [Theory]
    [InlineData(ExpectedChange.Admission)]
    [InlineData(ExpectedChange.Step)]
    [InlineData(ExpectedChange.Preparation)]
    public void Another_selected_identity_is_refused(ExpectedChange change) {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Read(ProcessAcceptedSourceVersion.OriginalProjection,
            change == ExpectedChange.Admission ? new(Guid.NewGuid()) : saved.Preparation.AdmissionId,
            change == ExpectedChange.Step ? ProcessStepInstanceId.New() : step,
            change == ExpectedChange.Preparation ? "sha256:" + new string('0', 64) : saved.PreparationFingerprint,
            projection.Header, projection.Assignments[step]));
    }

    [Fact]
    public void An_assignment_from_another_valid_header_is_refused() {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var other = ProcessAcceptedSourceCodec.Capture(Saved());
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Read(ProcessAcceptedSourceVersion.OriginalProjection,
            saved.Preparation.AdmissionId, step, saved.PreparationFingerprint, projection.Header, other.Assignments.Values.Single()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Changed_payload_without_its_digest_is_refused(bool header) {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        var original = header ? projection.Header : projection.Assignments[step];
        var changed = original with { PayloadJson = original.PayloadJson + " " };
        Assert.Throws<InvalidDataException>(() => Read(saved, projection, step, header ? changed : null, header ? null : changed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unknown_fields_are_refused_even_with_a_consistent_digest(bool header) {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        var value = JsonNode.Parse((header ? projection.Header : projection.Assignments[step]).PayloadJson)!.AsObject();
        value["unrecognizedEvidence"] = true;
        var changed = Resign(value.ToJsonString(), header, projection.Header.Fingerprint);
        Assert.Throws<JsonException>(() => Read(saved, projection, step, header ? changed : null, header ? null : changed));
    }

    [Fact]
    public void A_custom_identifier_converter_cannot_hide_extra_evidence() {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        var value = JsonNode.Parse(projection.Header.PayloadJson)!.AsObject();
        value["runId"]!["unrecognizedEvidence"] = true;
        var changed = Resign(value.ToJsonString(), true, projection.Header.Fingerprint);
        Assert.Throws<InvalidDataException>(() => Read(saved, projection, step, changed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Missing_evidence_is_refused(bool header) {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        Assert.Throws<ArgumentNullException>(() => ProcessAcceptedSourceCodec.Read(ProcessAcceptedSourceVersion.OriginalProjection,
            saved.Preparation.AdmissionId, step, saved.PreparationFingerprint,
            header ? null! : projection.Header, header ? projection.Assignments[step] : null!));
    }

    [Fact]
    public void Duplicate_original_step_assignments_are_refused() {
        var saved = Saved();
        var initial = saved.Preparation.InitialCommit;
        var repeated = saved with { Preparation = saved.Preparation with {
            InitialCommit = initial with { InitialAssignments = [initial.InitialAssignments![0], initial.InitialAssignments[0]] }
        } };
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Capture(repeated));
    }

    [Theory]
    [InlineData(AssignmentChange.Run)]
    [InlineData(AssignmentChange.Plan)]
    [InlineData(AssignmentChange.Step)]
    public void Capture_does_not_rebind_an_assignment_to_another_original_owner(AssignmentChange change) {
        var saved = Saved();
        var initial = saved.Preparation.InitialCommit;
        var assignment = initial.InitialAssignments![0];
        assignment = change switch {
            AssignmentChange.Run => assignment with { RunId = new(Guid.NewGuid()) },
            AssignmentChange.Plan => assignment with { PlanId = new(Guid.NewGuid()) },
            AssignmentChange.Step => assignment with { StepInstanceId = ProcessStepInstanceId.New() },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var changed = saved with { Preparation = saved.Preparation with { InitialCommit = initial with { InitialAssignments = [assignment] } } };
        var exception = Assert.ThrowsAny<Exception>(() => ProcessAcceptedSourceCodec.Capture(changed));
        Assert.True(exception is InvalidDataException or InvalidOperationException or ArgumentException);
    }

    [Theory]
    [InlineData(HeaderChange.Admission)]
    [InlineData(HeaderChange.Plan)]
    [InlineData(HeaderChange.PlanHash)]
    public void Capture_refuses_an_initial_state_that_does_not_match_its_original_plan(HeaderChange change) {
        var saved = Saved();
        var initial = saved.Preparation.InitialCommit;
        var state = initial.Mutation.State;
        state = change switch {
            HeaderChange.Admission => state with { LaunchAdmissionId = new(Guid.NewGuid()) },
            HeaderChange.Plan => state with { PlanId = new(Guid.NewGuid()) },
            HeaderChange.PlanHash => state with { PlanHash = "changed-plan-hash" },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var changed = saved with { Preparation = saved.Preparation with { InitialCommit = initial with { Mutation = initial.Mutation with { State = state } } } };
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Capture(changed));
    }

    [Fact]
    public void Oversized_original_assignment_metadata_is_refused_at_capture() {
        var saved = Saved();
        var initial = saved.Preparation.InitialCommit;
        var assignment = initial.InitialAssignments![0] with {
            LaunchVariables = new Dictionary<string, string> { ["LargeMetadata"] = new('x', ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes) }
        };
        var changed = saved with { Preparation = saved.Preparation with { InitialCommit = initial with { InitialAssignments = [assignment] } } };
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Capture(changed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_metadata_limit_counts_UTF8_bytes_before_decoding(bool header) {
        var saved = Saved();
        var projection = ProcessAcceptedSourceCodec.Capture(saved);
        var step = saved.Preparation.InitialCommit.InitialAssignments![0].StepInstanceId;
        var payload = "{\"data\":\"" + new string('\u00e9', ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes / 2) + "\"}";
        Assert.True(payload.Length < ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes);
        var changed = Resign(payload, header, projection.Header.Fingerprint);
        Assert.Throws<InvalidDataException>(() => Read(saved, projection, step, header ? changed : null, header ? null : changed));
    }

    internal static ProcessPreparedLaunchSnapshot Saved(int steps = 1, int promptCharacters = 256)
        => ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedSourceScalingTests.Create(steps, promptCharacters)));

    internal static void AssertOriginal(ProcessPreparedLaunchSnapshot saved, ProcessStepInstanceId step, ProcessAcceptedSource actual) {
        var initial = saved.Preparation.InitialCommit;
        var original = initial.InitialAssignments!.Single(item => item.StepInstanceId == step);
        Assert.Equal(saved.Preparation.AdmissionId, actual.Header.AdmissionId);
        Assert.Equal(saved.PreparationFingerprint, actual.Header.PreparationFingerprint);
        Assert.Equal(initial.Mutation.State.RootRunId, actual.Header.RootRunId);
        Assert.Equal(initial.InitialPlan!.Definition.VersionId, actual.Header.DefinitionVersionId);
        Assert.Equal(initial.Mutation.State.PlanHash, actual.Header.PlanHash);
        Assert.Equal(JsonSerializer.Serialize(saved.Preparation.Authority), JsonSerializer.Serialize(actual.Header.Authority));
        Assert.Equal(JsonSerializer.Serialize(ProcessAssignmentSource.From(original)), JsonSerializer.Serialize(actual.Assignment));
    }

    private static ProcessAcceptedSource Read(ProcessPreparedLaunchSnapshot saved, ProcessAcceptedSourceProjection projection,
        ProcessStepInstanceId step, ProcessAcceptedSourceEnvelope? header = null, ProcessAcceptedSourceEnvelope? assignment = null)
        => ProcessAcceptedSourceCodec.Read(ProcessAcceptedSourceVersion.OriginalProjection, saved.Preparation.AdmissionId,
            step, saved.PreparationFingerprint, header ?? projection.Header, assignment ?? projection.Assignments[step]);

    private static ProcessAcceptedSourceEnvelope Resign(string payload, bool header, string headerFingerprint) {
        var domain = header ? "process-accepted-source-header/v1\n" : "process-accepted-source-assignment/v1\n" + headerFingerprint + "\n";
        return new(payload, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(domain + payload))));
    }

    private static ProcessPreparedLaunchSnapshot Principal(SourcePrincipal kind, string artifactReference = "artifact://fixture") {
        var profile = Guid.NewGuid();
        ProcessProjectAdmission? project = kind == SourcePrincipal.ProjectAgent ? new(profile, Guid.NewGuid(), Guid.NewGuid()) : null;
        ProcessLaunchPrincipal principal = kind == SourcePrincipal.Local
            ? new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface)
            : kind == SourcePrincipal.Authenticated
            ? new ProcessLaunchPrincipal.AuthenticatedOperator("original-operator", ProcessProjectAdmissionFixture.Now.AddDays(-1))
            : new ProcessLaunchPrincipal.AgentExecution(new(Guid.NewGuid(), Guid.NewGuid(), 3,
                project is null ? ProcessLaunchSourceScopeKind.Organization : ProcessLaunchSourceScopeKind.Project,
                (project?.ProjectId ?? profile).ToString("N"), true, true, "fixture-v1", "sha256:fixture",
                ["execute-process"], ["fixture-capability"], ["write-alias"], ["read-alias"], [artifactReference],
                ProcessLaunchAgentCeiling.CurrentSchemaVersion, project), ProcessLaunchAgentOperation.StructureStart);
        var preparation = ProcessPreparedLaunchFixture.Create(new(principal, profile, project, true, true, "fixture-policy"));
        return ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(preparation));
    }

    public enum ExpectedChange { Admission, Step, Preparation }
    public enum AssignmentChange { Run, Plan, Step }
    public enum HeaderChange { Admission, Plan, PlanHash }
    public enum SourcePrincipal { Local, Authenticated, OrganizationAgent, ProjectAgent }
}
