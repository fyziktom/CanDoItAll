using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessLaunchDecisionTests {
    [Fact]
    public void Absent_selection_retains_fixed_legacy_fingerprint_and_wire_shape() {
        ProcessLaunchRequest request = new("legacy", null, null, null, null, "operator", new Dictionary<string, string>(), false, false);
        Assert.Equal("sha256:638c803210c39d275928f35cc3a01c76be31aea3c10b719fe3aff6283f2dfe46", ProcessLaunchIntentFingerprint.Compute(request));
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(request));
        Assert.False(wire.RootElement.TryGetProperty(nameof(request.Decision), out _));
        using var review = JsonDocument.Parse(JsonSerializer.Serialize(ProcessPreparedLaunchFixture.Create().Review));
        Assert.False(review.RootElement.TryGetProperty(nameof(ProcessLaunchPlanView.Decision), out _));
    }

    [Theory]
    [InlineData(SelectionChange.Contract)]
    [InlineData(SelectionChange.ContractVersion)]
    [InlineData(SelectionChange.ReadMode)]
    [InlineData(SelectionChange.WorkLimit)]
    [InlineData(SelectionChange.Removed)]
    public void Launch_and_producer_fingerprints_bind_selected_semantics(SelectionChange change) {
        var original = Prepared().Request;
        var selection = original.Decision!;
        var changed = original with { Decision = change switch {
            SelectionChange.Contract => selection with { Execution = selection.Execution with { ContractId = Guid.NewGuid() } },
            SelectionChange.ContractVersion => selection with { Execution = selection.Execution with { Version = 2 } },
            SelectionChange.ReadMode => selection with { Policy = new(ReadBasisMode: ReadBasisMode.RunSnapshot) },
            SelectionChange.WorkLimit => selection with { Policy = new(MaximumWorkDuration: TimeSpan.FromSeconds(2)) },
            SelectionChange.Removed => null,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        } };
        Assert.NotEqual(ProcessLaunchIntentFingerprint.Compute(original), ProcessLaunchIntentFingerprint.Compute(changed));
        Assert.NotEqual(ProcessLaunchProducerRequests.InputFingerprint(original, original.Authority!),
            ProcessLaunchProducerRequests.InputFingerprint(changed, original.Authority!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Unknown_selection_version_is_explicitly_refused(int version) {
        var selected = Prepared().Request.Decision! with { Version = version };
        Assert.Equal(ProcessLaunchDecisionRejection.InvalidSelection,
            Assert.Throws<ProcessLaunchDecisionRejectedException>(selected.Validate).Reason);
    }

    [Fact]
    public void Selection_requires_version_and_contract_and_refuses_unknown_members() {
        var selected = JsonSerializer.Serialize(Prepared().Request.Decision);
        foreach (var json in new[] { "{}", "{\"Version\":1}", selected[..^1] + ",\"FutureBehavior\":true}" }) {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProcessLaunchDecisionOptions>(json));
        }
        Assert.Throws<ProcessLaunchDecisionRejectedException>(() => new ProcessLaunchDecisionOptions(1, null!).Validate());
    }

    [Theory]
    [InlineData(AuthorityChange.Intent)]
    [InlineData(AuthorityChange.Project)]
    [InlineData(AuthorityChange.TaskPermission)]
    [InlineData(AuthorityChange.Missing)]
    [InlineData(AuthorityChange.InheritedRoot)]
    public void Explicit_selection_cannot_fall_back_to_an_unfunded_or_untrusted_launch(AuthorityChange change) {
        var request = Prepared().Request;
        request = change switch {
            AuthorityChange.Intent => request with { CallerIntentId = null },
            AuthorityChange.Project => request with { ProjectId = Guid.NewGuid() },
            AuthorityChange.TaskPermission => request with { Authority = request.Authority! with { CanCreateTasks = false } },
            AuthorityChange.Missing => request with { Authority = null },
            AuthorityChange.InheritedRoot => request with { RootRunIdOverride = ProcessRunId.New() },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<ProcessLaunchAuthorityRejectedException>(() => ProcessDecisionLaunchAdmissionCoordinator.RequireRequest(request));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Producer_attaches_only_admissions_and_round_trips_exact_review_and_original_preferences(ReadBasisMode mode) {
        var original = Prepared(mode);
        var owner = new Provider();
        var result = await new ProcessDecisionLaunchAdmissionCoordinator([owner]).PrepareAsync(original.Request, original.InitialCommit, original.Review, default);
        Assert.Equal(original.InitialCommit with {
            InitialRunDecisionAdmission = result.Initial.InitialRunDecisionAdmission,
            InitialAssignments = result.Initial.InitialAssignments
        }, result.Initial);
        Assert.Equal(original.InitialCommit.InitialAssignments!.Single() with {
            DecisionDataPolicy = result.Initial.InitialAssignments!.Single().DecisionDataPolicy
        }, result.Initial.InitialAssignments.Single());
        var preparation = original with { InitialCommit = result.Initial, Review = result.Review };
        var entity = ProcessPreparedLaunchCodec.ToEntity(preparation);
        var read = ProcessPreparedLaunchCodec.Read(entity).Preparation;
        Assert.Equal(preparation.Request.Decision, read.Request.Decision);
        Assert.Equal(result.Review.Decision, read.Review.Decision);
        Assert.Equal(result.Initial.InitialRunDecisionAdmission!.Funding, read.InitialCommit.InitialRunDecisionAdmission!.Funding);
        var oldHash = (mode == ReadBasisMode.RunSnapshot ? "process-shared-run-scope-v1\n" : string.Empty) +
            "process-root-decision-policy-v1\nprocess-decision-policy-v1\n" + entity.PayloadJson;
        entity.PreparationFingerprint = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(oldHash))).ToLowerInvariant();
        Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.Read(entity));
        Assert.Equal(1, owner.Calls);
    }

    [Fact]
    public async Task Missing_or_duplicate_owner_cannot_create_a_governed_preparation() {
        var prepared = Prepared();
        Assert.Throws<ArgumentException>(() => new ProcessDecisionLaunchAdmissionCoordinator([new Provider(), new Provider()]));
        var exception = await Assert.ThrowsAsync<ProcessLaunchDecisionRejectedException>(() => new ProcessDecisionLaunchAdmissionCoordinator([])
            .PrepareAsync(prepared.Request, prepared.InitialCommit, prepared.Review, default));
        Assert.Equal(ProcessLaunchDecisionRejection.UnsupportedContract, exception.Reason);
        var absent = prepared.Request with { Decision = null };
        var result = await new ProcessDecisionLaunchAdmissionCoordinator([]).PrepareAsync(absent, prepared.InitialCommit, prepared.Review, default);
        Assert.Same(prepared.InitialCommit, result.Initial);
        Assert.Same(prepared.Review, result.Review);
    }

    [Theory]
    [InlineData(AdmissionChange.OmittedAssignment)]
    [InlineData(AdmissionChange.ExtraAssignment)]
    [InlineData(AdmissionChange.Run)]
    [InlineData(AdmissionChange.Contract)]
    [InlineData(AdmissionChange.Funding)]
    [InlineData(AdmissionChange.Preference)]
    public async Task Owner_cannot_substitute_original_admission_bindings(AdmissionChange change) {
        var prepared = Prepared();
        var provider = new Provider(admissions => change switch {
            AdmissionChange.OmittedAssignment => admissions with { Steps = new Dictionary<ProcessStepInstanceId, ProcessDecisionDataPolicyAdmission>() },
            AdmissionChange.ExtraAssignment => admissions with { Steps = admissions.Steps.Append(new(ProcessStepInstanceId.New(), admissions.Steps.Single().Value)).ToDictionary() },
            AdmissionChange.Run => admissions with { Root = admissions.Root with { RunId = ProcessRunId.New() } },
            _ => admissions with { Steps = admissions.Steps.ToDictionary(pair => pair.Key, pair => change switch {
                AdmissionChange.Contract => pair.Value with { ExecutionContract = pair.Value.ExecutionContract! with { Version = 2 } },
                AdmissionChange.Funding => pair.Value with { Funding = new(admissions.Root.RunId, new(Guid.NewGuid())) },
                AdmissionChange.Preference => pair.Value with { AuthorizedInvocationOverride = new(ReadBasisMode: ReadBasisMode.RunSnapshot) },
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            }) }
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => new ProcessDecisionLaunchAdmissionCoordinator([provider])
            .PrepareAsync(prepared.Request, prepared.InitialCommit, prepared.Review, default));
    }

    [Theory]
    [InlineData(OnBasisChange.Replan)]
    public void Unsupported_parent_change_responses_are_never_silently_coerced(OnBasisChange change) {
        var workspace = new DecisionDataPolicy(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, change,
            new(change == OnBasisChange.Replan ? 2 : 0, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)));
        Assert.Equal(ProcessLaunchDecisionRejection.UnsupportedPolicy, Assert.Throws<ProcessLaunchDecisionRejectedException>(() =>
            ProjectTaskPlanProcessLaunchPolicyProvider.ResolvePolicy(workspace, null)).Reason);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public void Supported_policy_retains_read_choice_and_intersects_actual_owner_ceiling(ReadBasisMode mode) {
        var policy = ProjectTaskPlanProcessLaunchPolicyProvider.ResolvePolicy(
            DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30)), new(ReadBasisMode: mode));
        Assert.Equal(mode, policy.ReadBasisMode);
        Assert.Equal(TimeSpan.FromMinutes(10), policy.Budget.MaximumWorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(10), policy.Budget.MaximumBasisAge);
        Assert.Equal(0, policy.Budget.MaximumReplans);
        Assert.Throws<ProcessLaunchDecisionRejectedException>(() => ProjectTaskPlanProcessLaunchPolicyProvider.ResolvePolicy(policy,
            new(PublicationFreshness: PublicationFreshness.PinnedObservation)));
    }

    [Theory]
    [InlineData(PlanObligation.Tools)]
    [InlineData(PlanObligation.Host)]
    [InlineData(PlanObligation.Subprocess)]
    [InlineData(PlanObligation.Artifacts)]
    [InlineData(PlanObligation.Branch)]
    [InlineData(PlanObligation.NonActivity)]
    [InlineData(PlanObligation.AdditionalActivity)]
    [InlineData(PlanObligation.MissingTaskOperation)]
    public void Task_planner_cannot_discard_other_execution_obligations(PlanObligation obligation) {
        var original = Prepared().InitialCommit;
        Assert.Same(original.InitialAssignments![0], ProjectTaskPlanProcessLaunchPolicyProvider.RequireSingleActivity(original));
        var plan = original.InitialPlan!;
        var step = plan.Steps.Single();
        var assignment = original.InitialAssignments.Single();
        var changed = obligation switch {
            PlanObligation.Tools => original with { InitialPlan = plan with { Steps = [step with { RequiredRuntimeToolNames = ["required-tool"] }] } },
            PlanObligation.Host => original with { InitialPlan = plan with { Steps = [step with { RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { new("required-host") } }] } },
            PlanObligation.Subprocess => original with { InitialPlan = plan with { Steps = [step with { StartsSubprocess = true }] } },
            PlanObligation.Artifacts => original with { InitialAssignments = [assignment with { ProducedArtifactSlotIds = [ArtifactSlotId.New()] }] },
            PlanObligation.Branch => original with { InitialAssignments = [assignment with { BranchGate = new("source", "outcome") }] },
            PlanObligation.NonActivity => original with { InitialPlan = plan with { Steps = [step with { Kind = (ProcessStepKind)int.MaxValue }] } },
            PlanObligation.AdditionalActivity => original with { InitialPlan = plan with { Steps = [step, step with { StepInstanceId = ProcessStepInstanceId.New() }] } },
            PlanObligation.MissingTaskOperation => original with { InitialAssignments = [assignment with { AllowedOperations = [] }] },
            _ => throw new ArgumentOutOfRangeException(nameof(obligation))
        };
        Assert.Equal(ProcessLaunchDecisionRejection.UnsupportedPlan, Assert.Throws<ProcessLaunchDecisionRejectedException>(() =>
            ProjectTaskPlanProcessLaunchPolicyProvider.RequireSingleActivity(changed)).Reason);
    }

    [Fact]
    public void Public_admission_preserves_the_standard_managed_execution_requirement() {
        var initial = Prepared().InitialCommit;
        var plan = initial.InitialPlan!;
        var state = initial.Mutation.State;
        initial = initial with {
            InitialPlan = plan with { Steps = [plan.Steps.Single() with { RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.ManagedProcessAdapter } }] },
            Mutation = initial.Mutation with { State = state with { Steps = [state.Steps.Single() with {
                RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.ManagedProcessAdapter }
            }] } }
        };
        Assert.Same(initial.InitialAssignments!.Single(), ProjectTaskPlanProcessLaunchPolicyProvider.RequireSingleActivity(initial));
        Assert.Contains(ProcessHostCapabilityIds.ManagedProcessAdapter, initial.InitialPlan.Steps.Single().RequiredHostCapabilities);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_review_or_selected_preferences_cannot_be_substituted(bool changeReview) {
        var original = Prepared();
        var result = await new ProcessDecisionLaunchAdmissionCoordinator([new Provider()])
            .PrepareAsync(original.Request, original.InitialCommit, original.Review, default);
        var changed = original with { InitialCommit = result.Initial, Review = result.Review };
        changed = changeReview ? changed with { Review = result.Review with { Decision = null } }
            : changed with { Request = original.Request with { Decision = original.Request.Decision! with { Policy = new(ReadBasisMode: ReadBasisMode.RunSnapshot) } } };
        Assert.Throws<InvalidDataException>(() => ProcessPreparedLaunchCodec.ToEntity(changed));
    }

    private static ProcessPreparedLaunch Prepared(ReadBasisMode mode = ReadBasisMode.StepSnapshot) {
        ProcessProjectAdmission project = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var prepared = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId, project), new(Guid.NewGuid()));
        var request = prepared.Request with { Decision = new(1, ProjectTaskPlanProcessContract.Execution, new(ReadBasisMode: mode)) };
        return prepared with { Request = request, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(request),
            InitialCommit = prepared.InitialCommit with { InitialAssignments = prepared.InitialCommit.InitialAssignments!.Select(assignment =>
                assignment with { AllowedOperations = [.. assignment.AllowedOperations, ProcessOperationContractNames.ExecuteExternalAction] }).ToArray() }
        };
    }

    private sealed class Provider(Func<ProcessDecisionLaunchAdmissions, ProcessDecisionLaunchAdmissions>? change = null) : IProcessDecisionLaunchPolicyProvider {
        internal int Calls { get; private set; }
        public ProcessLaunchDecisionDescriptor Descriptor => ProjectTaskPlanProcessLaunchPolicyProvider.SupportedContract;
        private ProcessDecisionExecutionContract Contract => Descriptor.Execution;

        public Task<ProcessDecisionLaunchAdmissions> PrepareAsync(ProcessLaunchRequest request, ProcessRuntimeCommitRequest initial, CancellationToken cancellationToken) {
            Calls++;
            var policy = ProjectTaskPlanProcessLaunchPolicyProvider.ResolvePolicy(DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)), request.Decision!.Policy);
            ProcessDecisionRunAdmission root = new(initial.Mutation.State.RunId, new(Guid.NewGuid()), request.Authority!.DatabaseProfileId,
                Guid.NewGuid(), Guid.NewGuid(), initial.InitialPlan!.Definition.VersionId, policy, null, request.Decision.Policy,
                ProjectTaskPlanProcessLaunchPolicyProvider.Capabilities, [], policy);
            if (policy.ReadBasisMode == ReadBasisMode.RunSnapshot) {
                root = root with { MaterialScope = new(new(Guid.NewGuid()), root.Funding, root.DatabaseProfileId,
                    root.WorkspaceNamespaceId, ProjectTaskPlanProcessContract.Content) };
            }
            var step = initial.InitialAssignments!.Single().StepInstanceId;
            ProcessDecisionDataPolicyAdmission child = new(root.WorkspaceNamespaceId, root.WorkspaceHeadRevision, root.DefinitionVersionId, step,
                policy, null, null, request.Decision.Policy, root.OwnerCapabilities, [policy.Budget], policy) {
                ExecutionContract = Contract, MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1,
                ContentContract = ProjectTaskPlanProcessContract.Content, Funding = root.Funding, SharedRunScopeId = root.MaterialScope?.ScopeId
            };
            ProcessDecisionLaunchAdmissions result = new(root, new Dictionary<ProcessStepInstanceId, ProcessDecisionDataPolicyAdmission> { [step] = child });
            return Task.FromResult(change is null ? result : change(result));
        }

    }

    public enum SelectionChange { Contract, ContractVersion, ReadMode, WorkLimit, Removed }
    public enum AuthorityChange { Intent, Project, TaskPermission, Missing, InheritedRoot }
    public enum AdmissionChange { OmittedAssignment, ExtraAssignment, Run, Contract, Funding, Preference }
    public enum PlanObligation { Tools, Host, Subprocess, Artifacts, Branch, NonActivity, AdditionalActivity, MissingTaskOperation }
}
