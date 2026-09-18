using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessNativeMutationAdmissionContractTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Runtime_only_native_Process_authority_is_not_exported_as_a_client_claim(int kind) {
        var admission = Admission();
        Assert.NotEqual(Guid.Empty, admission.Dispatch.Evidence.ExecutionRunId);
        Assert.NotEqual(Guid.Empty, admission.ProjectAdmission.LifetimeId);
        var request = Request(kind, admission);
        var serialized = JsonSerializer.Serialize(request, request.GetType(), Json);
        Assert.DoesNotContain("processMutationAdmission", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(admission.Dispatch.Evidence.ExecutionRunId.ToString("D"), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("expectedProjectAdmission", serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Client_JSON_cannot_supply_a_Process_mutation_or_project_lifetime_admission(int kind) {
        var request = Request(kind, null);
        var serialized = JsonSerializer.Serialize(request, request.GetType(), Json);
        using var document = JsonDocument.Parse(serialized);
        var properties = document.RootElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone());
        properties["processMutationAdmission"] = JsonSerializer.SerializeToElement(new { dispatch = new { executionRunId = Guid.NewGuid() } });
        properties["expectedProjectAdmission"] = JsonSerializer.SerializeToElement(new { databaseProfileId = Guid.NewGuid(), projectId = Guid.NewGuid(), lifetimeId = Guid.NewGuid() });
        var decoded = JsonSerializer.Deserialize(JsonSerializer.Serialize(properties, Json), request.GetType(), Json);
        var authority = decoded switch {
            ProjectObjectCreateRequest value => value.ProcessMutationAdmission,
            ProjectObjectEditRequest value => value.ProcessMutationAdmission,
            ProjectObjectReclassificationRequest value => value.ProcessMutationAdmission,
            ProjectStructureAgentContext value => value.ProcessMutationAdmission,
            _ => throw new InvalidOperationException("Unexpected native contract.")
        };
        Assert.Null(authority);
        var lifetime = decoded switch {
            ProjectObjectCreateRequest value => value.ExpectedProjectAdmission,
            ProjectObjectEditRequest value => value.ExpectedProjectAdmission,
            ProjectObjectReclassificationRequest value => value.ExpectedProjectAdmission,
            ProjectStructureAgentContext value => value.ExpectedProjectAdmission,
            _ => throw new InvalidOperationException("Unexpected native contract.")
        };
        Assert.Null(lifetime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Saved_launch_observation_preserves_the_accepted_run_identity_through_the_public_tool_codec(bool sdkStringEnums) {
        var observation = new ProcessLaunchObservation(new(Guid.NewGuid()), new(Guid.NewGuid()),
            ProcessLaunchContinuationState.Accepted, ProcessLaunchLinkDeliveryState.Pending, null);
        var writeOptions = sdkStringEnums
            ? ProjectStructureProcessProposalCodec.ResultSerializerOptions
            : ProjectStructureProcessProposalCodec.SerializerOptions;
        var wire = JsonSerializer.SerializeToElement(observation, writeOptions);
        Assert.Equal(sdkStringEnums ? JsonValueKind.String : JsonValueKind.Number, wire.GetProperty("continuationState").ValueKind);
        Assert.Equal(observation.AcceptedRunId!.Value.Value, wire.GetProperty("acceptedRunId").GetProperty("value").GetGuid());
        var restored = wire.Deserialize<ProcessLaunchObservation>(ProjectStructureProcessProposalCodec.ResultSerializerOptions);
        Assert.Equal(observation, restored);
        Assert.Equal(observation.AcceptedRunId, restored!.AcceptedRunId);
    }

    private static object Request(int kind, ProjectProcessMutationAdmission? admission) => kind switch {
        0 => new ProjectObjectCreateRequest(ProjectObjectType.ProjectBlock, "Title", "", "", null) {
            ExpectedProjectAdmission = admission?.ProjectAdmission, ProcessMutationAdmission = admission
        },
        1 => new ProjectObjectEditRequest("Title", "", "", null, null, "{}") {
            ExpectedProjectAdmission = admission?.ProjectAdmission, ProcessMutationAdmission = admission
        },
        2 => new ProjectObjectReclassificationRequest(ProjectObjectType.ProjectBlock, "feature", "Title", "", "") {
            ExpectedProjectAdmission = admission?.ProjectAdmission, ProcessMutationAdmission = admission
        },
        3 => new ProjectStructureAgentContext("source", "Source", "fixture", "fixture", "", "session") {
            ExpectedProjectAdmission = admission?.ProjectAdmission, ProcessMutationAdmission = admission
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static ProjectProcessMutationAdmission Admission() {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var evidence = new ProcessExecutionClaimEvidence(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid()), new(Guid.NewGuid()),
            Guid.NewGuid(), "execute", DateTimeOffset.UtcNow, true);
        var source = new ProcessLaunchAuthority(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
            project.DatabaseProfileId, project, true, true, "fixture-source");
        return new(new(evidence, evidence.RunId, project.ProjectId, "sha256:" + new string('a', 64), "sha256:readiness", [], "scope",
            ProcessCapabilityScope.Empty, source, new(new(Guid.NewGuid()), "sha256:preparation", evidence.RunId, evidence.StepInstanceId, "sha256:readiness"),
            true, DateTimeOffset.UtcNow));
    }
}
