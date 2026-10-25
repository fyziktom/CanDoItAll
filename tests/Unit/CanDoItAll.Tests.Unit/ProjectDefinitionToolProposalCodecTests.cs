using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDefinitionToolProposalCodecTests {
    private static readonly Guid Profile = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Agent = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Execution = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Chat = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Authority = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DatasetNamespaceId Namespace = new(Guid.Parse("66666666-6666-6666-6666-666666666666"));
    private static readonly ProjectWriteAdmission Parent = new(Profile, Guid.Parse("77777777-7777-7777-7777-777777777777"), Guid.Parse("88888888-8888-8888-8888-888888888888"));
    private static readonly AgentToolBusinessIntentId Intent = new(Guid.Parse("99999999-9999-9999-9999-999999999999"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_Process_step_converter_preserves_existing_bytes_and_rejects_missing_identity(bool taskPlan) {
        var options = taskPlan ? ProjectTaskPlanAgentCodec.Json : ProjectDefinitionToolProposalCodec.Json;
        var expected = """{"value":"01234567-89ab-cdef-0123-456789abcdef"}""";
        ProcessStepInstanceId step = new(new Guid("01234567-89ab-cdef-0123-456789abcdef"));
        Assert.Equal(expected, JsonSerializer.Serialize(step, options));
        Assert.Equal(step, JsonSerializer.Deserialize<ProcessStepInstanceId>(expected, options));
        foreach (var invalid in new[] { "{}", "null", "{\"value\":\"00000000-0000-0000-0000-000000000000\"}",
                "{\"value\":\"01234567-89ab-cdef-0123-456789abcdef\",\"replacement\":true}" }) {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProcessStepInstanceId>(invalid, options));
        }
    }

    [Fact]
    public void Scalar_preparation_preserves_original_expectation_raw_values_and_epoch() {
        var original = Source();
        var expected = new ProjectDefinitionExpectation(Parent, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var input = new ProjectDefinitionToolArguments(Request() with { ExpectedDefinition = expected }, ProjectId: Parent.ProjectId);
        var prepared = Prepare(ProjectStructureToolPolicy.ProjectStructureProjectUpdate, input, original);
        var (source, retained) = ProjectDefinitionToolProposalCodec.Read(prepared);

        Assert.Equal(2, prepared.SemanticVersion);
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, prepared.Recovery);
        Assert.Equal(original.Epoch, source.Operation(Intent).Epoch);
        Assert.Equal(expected, retained.Request.ExpectedDefinition);
        Assert.Equal("  Original draft  ", retained.Request.Name);
        Assert.Contains("\"Active\"", prepared.ArgumentsJson);
        Assert.Equal(prepared, ProjectDefinitionToolProposalCodec.Prepare(prepared.ToolName,
            JsonSerializer.SerializeToElement(retained, ProjectDefinitionToolProposalCodec.Json), prepared.SourcePreparation!));
    }

    [Theory]
    [InlineData(SavedProcessPrincipal.LocalOperator)]
    [InlineData(SavedProcessPrincipal.AuthenticatedOperator)]
    [InlineData(SavedProcessPrincipal.AgentExecution)]
    public void Process_source_roundtrip_preserves_exact_preparation_all_admitted_identities_and_material_policy(SavedProcessPrincipal principal) {
        var original = ProcessSource(principal);
        var expectation = new ProjectDefinitionExpectation(Parent, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var prepared = Prepare(ProjectStructureToolPolicy.ProjectStructureProjectUpdate,
            new(Request() with { ExpectedDefinition = expectation }, ProjectId: Parent.ProjectId), original);
        var (restored, input) = ProjectDefinitionToolProposalCodec.Read(prepared);
        var dispatch = Assert.IsType<ProcessExecutionDispatchAuthority>(restored.Process);

        Assert.Equal(original.Session, restored.Session);
        Assert.Equal(original.Process!.Evidence, dispatch.Evidence);
        Assert.Equal(original.Process.RootRunId, dispatch.RootRunId);
        Assert.Equal(original.Process.ProjectReference, dispatch.ProjectReference);
        Assert.Equal(prepared.SourcePreparation, ProjectDefinitionToolProposalCodec.WriteSource(restored));
        Assert.Equal(original.Caller, restored.Caller);
        Assert.Equal(ProjectDefinitionGovernedBindings.ForProcess(new(original.Process!), ProjectProcessProjectOperation.Update),
            ProjectDefinitionGovernedBindings.ForProcess(new(dispatch), ProjectProcessProjectOperation.Update));
        Assert.Equal(prepared, Prepare(prepared.ToolName, input, restored));
    }

    [Fact]
    public void Process_source_with_empty_saved_step_identity_is_rejected() {
        var source = ProcessSource(SavedProcessPrincipal.LocalOperator);
        var envelope = ProjectDefinitionToolProposalCodec.WriteSource(source);
        var corrupted = envelope.PayloadJson.Replace(source.Process!.Evidence.StepInstanceId.Value.ToString("D"),
            Guid.Empty.ToString("D"), StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => ProjectDefinitionToolProposalCodec.ReadSource(
            AgentToolProtocolEnvelope.Create(envelope.Format, envelope.Version, corrupted)));
    }

    [Fact]
    public void Restoring_original_source_does_not_adopt_new_epoch_policy_or_caller_context() {
        var source = Source();
        var input = new ProjectDefinitionToolArguments(Request());
        var original = Prepare(ProjectStructureToolPolicy.ProjectStructureProjectCreate, input, source);
        var newer = Prepare(ProjectStructureToolPolicy.ProjectStructureProjectCreate, input,
            Source(epoch: 2, policy: "new-policy"));
        var restored = ProjectDefinitionToolProposalCodec.Read(original);

        Assert.NotEqual(original.Digest, newer.Digest);
        Assert.Equal(1, restored.Source.Epoch.Value);
        Assert.Equal("original-policy", restored.Source.Agent!.Governance.PolicyFingerprint);
        Assert.Equal(source.Caller, restored.Source.Caller);
    }

    [Fact]
    public void Source_interpretation_rejects_unsupported_version_forged_caller_and_foreign_execution() {
        var source = Source();
        var envelope = ProjectDefinitionToolProposalCodec.WriteSource(source);
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolProposalCodec.ReadSource(
            AgentToolProtocolEnvelope.Create(ProjectDefinitionToolProposalCodec.SourceFormat, 2, envelope.PayloadJson)));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolProposalCodec.WriteSource(source with {
            CallerBinding = ProjectMutationCallerScope.OpenApi.Bind(source.Namespace)
        }));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolProposalCodec.WriteSource(source with {
            Session = source.Session with { AgentId = Chat }
        }));
    }

    [Fact]
    public void Missing_original_definition_or_parent_and_unrelated_target_are_rejected_before_dispatch() {
        var source = Source();
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(ProjectStructureToolPolicy.ProjectStructureProjectUpdate,
            new(Request(), ProjectId: Parent.ProjectId), source));
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
            new(Request(), ParentProjectId: Parent.ProjectId), source));
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
            new(Request() with { ExpectedParentAdmission = Parent }, ParentProjectId: Chat), source));
        var accepted = Prepare(ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
            new(Request() with { ExpectedParentAdmission = Parent }, ParentProjectId: Parent.ProjectId), source);
        Assert.Equal(Parent, ProjectDefinitionToolProposalCodec.Read(accepted).Input.Request.ExpectedParentAdmission);
    }

    [Fact]
    public void Creation_identity_is_stable_and_separated_by_namespace_execution_and_intent() {
        var source = Source();
        var first = ProjectDefinitionToolProposalCodec.ReservedProjectId(source, Intent);
        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, ProjectDefinitionToolProposalCodec.ReservedProjectId(ProjectDefinitionToolProposalCodec.ReadSource(
            ProjectDefinitionToolProposalCodec.WriteSource(source)), Intent));
        Assert.NotEqual(first, ProjectDefinitionToolProposalCodec.ReservedProjectId(Source(@namespace: new(Chat)), Intent));
        Assert.NotEqual(first, ProjectDefinitionToolProposalCodec.ReservedProjectId(Source(execution: Authority), Intent));
        Assert.NotEqual(first, ProjectDefinitionToolProposalCodec.ReservedProjectId(source, new(Authority)));
    }

    [Fact]
    public void Recovered_reservation_must_match_original_intent_target_requester_and_parent_lifetime() {
        var source = Source();
        var input = new ProjectDefinitionToolArguments(Request() with { ExpectedParentAdmission = Parent }, ParentProjectId: Parent.ProjectId);
        var reservation = new ProjectCreationReservation(Intent.Value, Profile,
            ProjectDefinitionToolProposalCodec.ReservedProjectId(source, Intent), Chat, Agent, Parent.ProjectId, Parent.LifetimeId);
        ProjectDefinitionToolReceiptRecovery.RequireReservation(source, Intent, input, reservation);
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolReceiptRecovery.RequireReservation(source, Intent, input,
            reservation with { ProjectId = Authority }));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolReceiptRecovery.RequireReservation(source, Intent, input,
            reservation with { ParentLifetimeId = Authority }));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolReceiptRecovery.RequireReservation(source, Intent, input,
            reservation with { RequesterId = Authority }));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolReceiptRecovery.RequireReservation(source, new(Authority), input, reservation));
    }

    [Fact]
    public void Original_source_and_request_are_bounded_before_journal_admission() {
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolProposalCodec.WriteSource(Source(policy: new string('x', 65_000))));
        Assert.Throws<ProjectDefinitionLimitException>(() => Prepare(ProjectStructureToolPolicy.ProjectStructureProjectCreate,
            new(Request() with { Description = new string('x', 65_537) }), Source()));
    }

    [Fact]
    public void Altered_arguments_or_missing_original_source_cannot_reuse_the_approved_payload() {
        var prepared = Prepare(ProjectStructureToolPolicy.ProjectStructureProjectCreate, new(Request()), Source());
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolProposalCodec.Read(new(prepared.ToolName,
            prepared.SemanticVersion, prepared.Digest, prepared.ArgumentsJson.Replace("Original draft", "Changed draft", StringComparison.Ordinal),
            prepared.Effect, prepared.Recovery, prepared.SourcePreparation)));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectDefinitionToolProposalCodec.Read(new(prepared.ToolName,
            1, prepared.Digest, prepared.ArgumentsJson, prepared.Effect, AgentToolProposalRecovery.ReconcileBeforeRetry)));
    }

    private static ProjectDefinitionToolSource Source(long epoch = 1, string policy = "original-policy",
        DatasetNamespaceId? @namespace = null, Guid? execution = null) {
        var session = new AgentToolSessionAdmission(new(execution ?? Execution, Chat, new(Authority)), Agent,
            AgentRuntimeContextPurpose.InteractiveChat, new(Profile, "profile", new(0)));
        var governance = new AgentExecutionGovernanceSnapshot(new(Authority), Agent, Profile, new(0),
            WorkspaceScopeDescriptor.Organization(Profile.ToString("N")), true, true, "1", policy,
            [ProjectStructureToolPolicy.ProjectStructureProjectCreate, ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
                ProjectStructureToolPolicy.ProjectStructureProjectUpdate]);
        var dataset = @namespace ?? Namespace;
        return new(session, dataset, new(epoch), ProjectDefinitionGovernedBindings.AgentCaller(session).Bind(dataset),
            ProjectDefinitionRequestVersion.VersionOne, new(new() { CanRead = true, CanCreateProjects = true,
                CanCreateSubprojects = true, CanWriteNonTaskStructure = true, AllowAllProjects = true }, governance, null), null);
    }

    private static ProjectDefinitionToolSource ProcessSource(SavedProcessPrincipal kind) {
        const string owner = "original-process-owner";
        const string stepKey = "original-step";
        var now = new DateTimeOffset(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);
        var project = new ProcessProjectAdmission(Profile, Parent.ProjectId, Parent.LifetimeId);
        var run = new ProcessRunId(Chat);
        var step = new ProcessStepInstanceId(Intent.Value);
        ProcessLaunchPrincipal principal = kind switch {
            SavedProcessPrincipal.LocalOperator => new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.Api),
            SavedProcessPrincipal.AuthenticatedOperator => new ProcessLaunchPrincipal.AuthenticatedOperator("verified-subject", now.AddHours(1)),
            SavedProcessPrincipal.AgentExecution => new ProcessLaunchPrincipal.AgentExecution(new(Authority, Agent, 7,
                ProcessLaunchSourceScopeKind.Project, Parent.ProjectId.ToString("D"), true, true, "policy-v2", "agent-policy",
                [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction],
                ["capability"], ["write-target"], ["read-target"], ["artifact-ref"],
                ProcessLaunchAgentCeiling.CurrentSchemaVersion, project), ProcessLaunchAgentOperation.StructureStart),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var launch = new ProcessLaunchAuthority(principal, Profile, project, true, true, "launch-policy", new(false, true, false, true));
        var scope = new ProcessCapabilityScope {
            Directives = [new() { Kind = ProcessCapabilityScopeDirectiveKind.AllowOnly,
                Target = new() { Kind = ProcessCapabilityScopeTargetKind.RuntimeToolName,
                    Value = ProjectStructureToolPolicy.ProjectStructureProjectUpdate, SecondaryValue = "selector" }, Reason = "original directive" }],
            InstructionFragments = [new() { Key = "brief", Title = "Original title", Content = "Original instruction" }],
            RequiredReceipts = [new() { Key = "result", Kind = ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider,
                ToolName = ProjectStructureToolPolicy.ProjectStructureProjectUpdate, RuntimeToolProviderKey = "provider", McpServerKey = "server",
                MinimumCount = 2, RequireSuccessfulExit = false, RequireCurrentRun = false,
                Activation = ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool,
                Purpose = ProcessRequiredToolReceiptPurpose.AcceptanceProof, ApplicableBranchOutcomeKeys = ["branch"], Reason = "original receipt" }]
        };
        var dispatch = new ProcessExecutionDispatchAuthority(new(Execution, Agent, run, step, Authority, stepKey, now, true),
            new(Parent.LifetimeId), Parent.ProjectId, owner, "readiness",
            [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction], "project", scope, launch,
            new(new(Parent.ProjectId), "preparation", run, step, "reference-readiness"), true, now.AddMinutes(1));
        var background = new AgentToolBackgroundSourceBinding("process-step", stepKey,
            AgentToolProtocolEnvelope.ComputeDigest(owner), AgentToolProtocolEnvelope.ComputeDigest("original-execution"));
        var session = new AgentToolSessionAdmission(new(Execution, Guid.Empty, default, background), Agent,
            AgentRuntimeContextPurpose.GovernedProcessAutomation, new(Profile, "profile", new(7)));
        return new(session, Namespace, new(3), ProjectDefinitionGovernedBindings.ProcessCaller(dispatch).Bind(Namespace),
            ProjectDefinitionRequestVersion.VersionOne, null, dispatch);
    }

    public enum SavedProcessPrincipal {
        LocalOperator,
        AuthenticatedOperator,
        AgentExecution
    }

    private static ProjectStructureProjectSaveRequest Request() => new("  Original draft  ", "notes", "objective", "phase", ProjectStatus.Active);
    private static AgentToolPreparedPayload Prepare(string tool, ProjectDefinitionToolArguments input, ProjectDefinitionToolSource source)
        => ProjectDefinitionToolProposalCodec.Prepare(tool, JsonSerializer.SerializeToElement(input, ProjectDefinitionToolProposalCodec.Json),
            ProjectDefinitionToolProposalCodec.WriteSource(source));
}
