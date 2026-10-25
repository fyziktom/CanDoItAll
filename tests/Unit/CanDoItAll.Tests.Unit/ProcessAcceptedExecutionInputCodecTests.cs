using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAcceptedExecutionInputCodecTests {
    [Theory]
    [InlineData(1, 256)]
    [InlineData(128, 256)]
    [InlineData(1, 1_048_576)]
    public void Selected_input_preserves_complete_original_assignment_without_enlarging_metadata(int steps, int promptCharacters) {
        var saved = Saved(steps, promptCharacters);
        var source = ProcessAcceptedSourceCodec.Capture(saved);
        var original = saved.Preparation.InitialCommit.InitialAssignments![^1];
        var decoded = ProcessAcceptedExecutionInputCodec.Read(ProcessAcceptedExecutionInputVersion.OriginalStepRequirements,
            source.ExecutionInputs[original.StepInstanceId], source.Header.Fingerprint, source.Assignments[original.StepInstanceId]);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(decoded.Assignment));
        Assert.Equal(saved.Preparation.InitialCommit.InitialPlan!.Steps[^1].StepDefinitionId, decoded.Requirements!.StepDefinitionId);
        Assert.DoesNotContain(original.Prompt, source.Assignments[original.StepInstanceId].PayloadJson, StringComparison.Ordinal);
        Assert.InRange(Encoding.UTF8.GetByteCount(source.Assignments[original.StepInstanceId].PayloadJson), 2, 4096);
        Assert.Equal(steps, source.ExecutionInputs.Count);
    }

    [Fact]
    public void Ordinary_assignments_do_not_acquire_a_new_prompt_limit() {
        var preparation = ProcessPreparedLaunchFixture.Create();
        var initial = preparation.InitialCommit;
        preparation = preparation with { InitialCommit = initial with {
            InitialAssignments = [initial.InitialAssignments![0] with { Prompt = new('x', ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes + 1) }]
        } };
        var source = ProcessAcceptedSourceCodec.Capture(ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(preparation)));
        Assert.Empty(source.ExecutionInputs);
    }

    [Theory]
    [InlineData(Substitution.Header)]
    [InlineData(Substitution.Assignment)]
    [InlineData(Substitution.Payload)]
    [InlineData(Substitution.Fingerprint)]
    public void A_selected_input_cannot_be_substituted_across_original_evidence(Substitution change) {
        var source = ProcessAcceptedSourceCodec.Capture(Saved());
        var other = ProcessAcceptedSourceCodec.Capture(Saved());
        var input = source.ExecutionInputs.Values.Single();
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedExecutionInputCodec.Read(
            ProcessAcceptedExecutionInputVersion.OriginalStepRequirements,
            change switch {
                Substitution.Payload => input with { PayloadJson = other.ExecutionInputs.Values.Single().PayloadJson },
                Substitution.Fingerprint => input with { Fingerprint = other.ExecutionInputs.Values.Single().Fingerprint },
                _ => input
            },
            change == Substitution.Header ? other.Header.Fingerprint : source.Header.Fingerprint,
            change == Substitution.Assignment ? other.Assignments.Values.Single() : source.Assignments.Values.Single()));
    }

    [Theory]
    [InlineData(Malformed.UnknownField)]
    [InlineData(Malformed.MissingPrompt)]
    [InlineData(Malformed.NullPrompt)]
    [InlineData(Malformed.DuplicatePrompt)]
    [InlineData(Malformed.Whitespace)]
    [InlineData(Malformed.OtherExecutor)]
    [InlineData(Malformed.MissingArtifacts)]
    [InlineData(Malformed.TooManyArtifacts)]
    public void Consistent_digest_does_not_authorize_invalid_or_noncanonical_assignment(Malformed change) {
        var source = ProcessAcceptedSourceCodec.Capture(Saved());
        var original = source.ExecutionInputs.Values.Single();
        var payloadNode = JsonNode.Parse(original.PayloadJson)!.AsObject();
        var value = payloadNode["assignment"]!.AsObject();
        switch (change) {
            case Malformed.UnknownField:
                value["unknown"] = true;
                break;
            case Malformed.MissingPrompt:
                value.Remove("prompt");
                break;
            case Malformed.NullPrompt:
                value["prompt"] = null;
                break;
            case Malformed.OtherExecutor:
                value["executorId"] = Guid.NewGuid().ToString("D");
                break;
            case Malformed.MissingArtifacts:
                value.Remove("producedArtifactSlotIds");
                break;
            case Malformed.TooManyArtifacts:
                var items = new JsonArray();
                for (var index = 0; index <= ProcessAcceptedExecutionInputCodec.MaximumCollectionCount; index++) {
                    items.Add(JsonSerializer.SerializeToNode(new { value = Guid.NewGuid() }));
                }
                value["producedArtifactSlotIds"] = items;
                break;
        }
        var payload = change switch {
            Malformed.DuplicatePrompt => original.PayloadJson.Replace("\"prompt\":", "\"prompt\":\"duplicate\",\"prompt\":", StringComparison.Ordinal),
            Malformed.Whitespace => original.PayloadJson + " ",
            _ => payloadNode.ToJsonString()
        };
        var envelope = Resign(payload, source);
        var failure = Record.Exception(() => ProcessAcceptedExecutionInputCodec.Read(ProcessAcceptedExecutionInputVersion.OriginalStepRequirements,
            envelope, source.Header.Fingerprint, source.Assignments.Values.Single()));
        Assert.True(failure is InvalidDataException or JsonException, failure?.ToString());
    }

    [Theory]
    [InlineData(ProcessAcceptedExecutionInputVersion.NotCaptured)]
    [InlineData((ProcessAcceptedExecutionInputVersion)3)]
    public void Unsupported_versions_have_no_implicit_conversion(ProcessAcceptedExecutionInputVersion version) {
        var source = ProcessAcceptedSourceCodec.Capture(Saved());
        Assert.Throws<NotSupportedException>(() => ProcessAcceptedExecutionInputCodec.Read(version,
            source.ExecutionInputs.Values.Single(), source.Header.Fingerprint, source.Assignments.Values.Single()));
    }

    [Fact]
    public void Oversized_input_is_rejected_before_decoding_and_at_first_capture() {
        var source = ProcessAcceptedSourceCodec.Capture(Saved());
        var oversized = new string('é', ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes / 2 + 1);
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedExecutionInputCodec.Read(ProcessAcceptedExecutionInputVersion.OriginalStepRequirements,
            source.ExecutionInputs.Values.Single() with { PayloadJson = oversized }, source.Header.Fingerprint, source.Assignments.Values.Single()));
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedSourceCodec.Capture(Saved(1, ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes)));
    }

    [Fact]
    public void Historical_assignment_remains_readable_but_cannot_supply_requirements() {
        var saved = Saved();
        var source = ProcessAcceptedSourceCodec.Capture(saved);
        var assignment = saved.Preparation.InitialCommit.InitialAssignments!.Single();
        var domain = "process-accepted-execution-input/v1\n" + source.Header.Fingerprint + "\n" + source.Assignments.Values.Single().Fingerprint + "\n";
        var legacy = ProcessAcceptedSourceCodec.Encode(assignment, domain, ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes);
        var decoded = ProcessAcceptedExecutionInputCodec.Read(ProcessAcceptedExecutionInputVersion.OriginalAssignment,
            legacy, source.Header.Fingerprint, source.Assignments.Values.Single());
        Assert.Equal(JsonSerializer.Serialize(assignment), JsonSerializer.Serialize(decoded.Assignment));
        Assert.Null(decoded.Requirements);
        ProcessAcceptedExecutionInput input = new(saved.Preparation.AdmissionId, saved.Preparation.InitialCommit.Mutation.State.RootRunId,
            saved.PreparationFingerprint, legacy.Fingerprint, ProcessPreparedLaunchFixture.Local(Guid.NewGuid()), decoded.Assignment);
        var failure = Assert.Throws<ProcessAcceptedStepRequirementsRequiredException>(() => input.RequireRequirements());
        Assert.Equal(assignment.RunId, failure.RunId);
        Assert.Equal(assignment.StepInstanceId, failure.StepId);
        Assert.Throws<InvalidDataException>(() => ProcessAcceptedExecutionInputCodec.Read(ProcessAcceptedExecutionInputVersion.OriginalStepRequirements,
            legacy, source.Header.Fingerprint, source.Assignments.Values.Single()));
    }

    [Theory]
    [InlineData(InvalidRequirements.Missing)]
    [InlineData(InvalidRequirements.Null)]
    [InlineData(InvalidRequirements.UnknownField)]
    [InlineData(InvalidRequirements.UnknownKind)]
    [InlineData(InvalidRequirements.NullTools)]
    [InlineData(InvalidRequirements.InvalidTool)]
    [InlineData(InvalidRequirements.TooManyOutcomes)]
    public void Consistent_digest_cannot_hide_missing_or_invalid_requirements(InvalidRequirements change) {
        var source = ProcessAcceptedSourceCodec.Capture(Saved());
        var payload = JsonNode.Parse(source.ExecutionInputs.Values.Single().PayloadJson)!.AsObject();
        var requirements = payload["requirements"]!.AsObject();
        switch (change) {
            case InvalidRequirements.Missing:
                payload.Remove("requirements");
                break;
            case InvalidRequirements.Null:
                payload["requirements"] = null;
                break;
            case InvalidRequirements.UnknownField:
                requirements["unknown"] = true;
                break;
            case InvalidRequirements.UnknownKind:
                requirements["kind"] = 999;
                break;
            case InvalidRequirements.NullTools:
                requirements["requiredRuntimeToolNames"] = null;
                break;
            case InvalidRequirements.InvalidTool:
                requirements["requiredRuntimeToolNames"] = new JsonArray(" invalid ");
                break;
            case InvalidRequirements.TooManyOutcomes:
                var outcomes = new JsonArray();
                for (var index = 0; index <= ProcessAcceptedStepRequirements.MaximumDeclarations; index++) {
                    outcomes.Add(JsonSerializer.SerializeToNode(new { value = $"outcome-{index}" }));
                }
                requirements["configuredBranchOutcomeIds"] = outcomes;
                break;
        }
        var failure = Record.Exception(() => ProcessAcceptedExecutionInputCodec.Read(ProcessAcceptedExecutionInputVersion.OriginalStepRequirements,
            Resign(payload.ToJsonString(), source), source.Header.Fingerprint, source.Assignments.Values.Single()));
        Assert.True(failure is InvalidDataException or JsonException, failure?.ToString());
    }

    private static ProcessPreparedLaunchSnapshot Saved(int steps = 1, int promptCharacters = 256)
        => ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchScalingFixture.Create(steps, promptCharacters)));

    private static ProcessAcceptedSourceEnvelope Resign(string payload, ProcessAcceptedSourceProjection source) {
        var domain = "process-accepted-execution-input/v2\n" + source.Header.Fingerprint + "\n" + source.Assignments.Values.Single().Fingerprint + "\n";
        return new(payload, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(domain + payload))));
    }

    public enum Substitution { Header, Assignment, Payload, Fingerprint }
    public enum Malformed { UnknownField, MissingPrompt, NullPrompt, DuplicatePrompt, Whitespace, OtherExecutor, MissingArtifacts, TooManyArtifacts }
    public enum InvalidRequirements { Missing, Null, UnknownField, UnknownKind, NullTools, InvalidTool, TooManyOutcomes }
}
