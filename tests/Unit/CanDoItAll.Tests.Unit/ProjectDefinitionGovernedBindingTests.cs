using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDefinitionGovernedBindingTests {
    private static readonly Guid ProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AgentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ExecutionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AuthorityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ChatId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProjectWriteAdmission Target = new(ProfileId, Guid.Parse("66666666-6666-6666-6666-666666666666"), Guid.Parse("77777777-7777-7777-7777-777777777777"));

    [Fact]
    public void Agent_proposal_and_material_policy_change_effect_without_changing_saved_execution_caller() {
        var original = Agent();
        var changed = Agent(policy: "changed-policy");
        var first = ProjectDefinitionGovernedBindings.ForAgent(original.Admission, original.Session, original.Proposal);
        var policy = ProjectDefinitionGovernedBindings.ForAgent(changed.Admission, changed.Session, changed.Proposal);
        var proposal = original.Proposal with { IntentId = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")) };
        var next = ProjectDefinitionGovernedBindings.ForAgent(original.Admission, original.Session, proposal);

        Assert.Equal(first.Caller, policy.Caller);
        Assert.Equal(first.Caller, next.Caller);
        Assert.NotEqual(first.Effect, policy.Effect);
        Assert.NotEqual(first.Effect, next.Effect);
        Assert.Equal(ProjectMutationCallerKind.GovernedExecution, first.Caller.Kind);
        Assert.Equal(ProjectDefinitionEffectKind.Agent, first.Effect.Kind);
    }

    [Fact]
    public void Agent_grant_order_is_canonical_but_mismatched_session_or_unapproved_payload_is_rejected() {
        var first = Agent(operations: [ProjectStructureToolPolicy.ProjectStructureProjectUpdate, "another-operation"]);
        var reordered = Agent(operations: ["another-operation", ProjectStructureToolPolicy.ProjectStructureProjectUpdate]);
        Assert.Equal(ProjectDefinitionGovernedBindings.ForAgent(first.Admission, first.Session, first.Proposal),
            ProjectDefinitionGovernedBindings.ForAgent(reordered.Admission, reordered.Session, reordered.Proposal));

        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForAgent(first.Admission,
            first.Session with { Session = first.Session.Session with { AgentId = ChatId } }, first.Proposal));
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForAgent(first.Admission,
            first.Session, first.Proposal with { ApprovalStatus = ExecutionApprovalStatus.Rejected }));
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForAgent(first.Admission,
            first.Session, first.Proposal with { ApprovedDigest = new(new string('b', 64)) }));
    }

    [Fact]
    public void Different_saved_execution_mints_a_different_agent_caller_without_using_runtime_session_text() {
        var first = Agent();
        var reference = new AgentToolSessionReference(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), ChatId, new(AuthorityId));
        var secondSession = first.Session with { Session = first.Session.Session with { Reference = reference } };
        var secondProposal = first.Proposal with { Session = reference };

        Assert.NotEqual(ProjectDefinitionGovernedBindings.ForAgent(first.Admission, first.Session, first.Proposal).Caller,
            ProjectDefinitionGovernedBindings.ForAgent(first.Admission, secondSession, secondProposal).Caller);
    }

    [Fact]
    public void Definition_binding_rejects_an_approved_unrelated_agent_tool_or_wrong_mutation_domain() {
        var unrelated = Agent(tool: "project_structure_task_update");
        var wrongDomain = Agent(domain: ProjectAgentMutationDomain.Tasks);

        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForAgent(
            unrelated.Admission, unrelated.Session, unrelated.Proposal));
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForAgent(
            wrongDomain.Admission, wrongDomain.Session, wrongDomain.Proposal));
    }

    [Fact]
    public void Process_definition_binding_rejects_native_or_hierarchy_authority_and_an_unrelated_proposal() {
        var original = Process();
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForProcess(original, ProjectProcessProjectOperation.Native));
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForProcess(original, ProjectProcessProjectOperation.ChangeHierarchy));
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForProcess(original, ProjectProcessProjectOperation.MoveToChild));

        var background = new AgentToolBackgroundSourceBinding("process-step", original.Dispatch.Evidence.StepKey,
            AgentToolProtocolEnvelope.ComputeDigest(original.Dispatch.OwnerFingerprint), new(new string('b', 64)));
        var session = new AgentToolSessionReference(ExecutionId, Guid.Empty, default, background);
        var valid = Agent().Proposal with { Session = session };
        var unrelated = Agent(tool: ProjectStructureToolPolicy.ProjectStructureProjectCreate).Proposal with { Session = session };
        Assert.Equal(ProjectDefinitionEffectKind.Process,
            ProjectDefinitionGovernedBindings.ForProcess(original, ProjectProcessProjectOperation.Update, valid).Effect.Kind);
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForProcess(original, ProjectProcessProjectOperation.Update, unrelated));
    }

    [Fact]
    public void Process_claim_and_capability_changes_bind_effect_while_observation_liveness_does_not() {
        var original = Process();
        var observed = new ProjectProcessMutationAdmission(original.Dispatch with {
            ObservedAtUtc = Now.AddMinutes(5), ObservedCurrentDispatch = false,
            Evidence = original.Dispatch.Evidence with { ExecutionMayDispatch = false }
        });
        var claim = new ProjectProcessMutationAdmission(original.Dispatch with {
            Evidence = original.Dispatch.Evidence with { DispatchClaimToken = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") }
        });
        var capability = new ProjectProcessMutationAdmission(original.Dispatch with { CapabilityScope = new() {
            Directives = [new() { Kind = ProcessCapabilityScopeDirectiveKind.Deny,
                Target = new() { Kind = ProcessCapabilityScopeTargetKind.RuntimeToolName, Value = "another-tool" } }]
        } });
        var first = ProjectDefinitionGovernedBindings.ForProcess(original, ProjectProcessProjectOperation.Update);

        Assert.Equal(first, ProjectDefinitionGovernedBindings.ForProcess(observed, ProjectProcessProjectOperation.Update));
        Assert.Equal(first.Caller, ProjectDefinitionGovernedBindings.ForProcess(claim, ProjectProcessProjectOperation.Update).Caller);
        Assert.NotEqual(first.Effect, ProjectDefinitionGovernedBindings.ForProcess(claim, ProjectProcessProjectOperation.Update).Effect);
        Assert.NotEqual(first.Effect, ProjectDefinitionGovernedBindings.ForProcess(capability, ProjectProcessProjectOperation.Update).Effect);
    }

    [Fact]
    public void Agent_receipt_read_survives_write_revocation_but_denies_current_read_revocation_or_replaced_target() {
        var original = Agent();
        var readOnly = CurrentAgent(read: true, write: false);
        var revoked = CurrentAgent(read: false, write: false);
        Assert.True(ProjectDefinitionGovernedReadPolicy.AgentCanRead(original.Admission, readOnly, Target));
        Assert.False(AgentProjectStructureAccessMetadata.Read(revoked.ConfigurationJson).CanRead);
        Assert.False(ProjectDefinitionGovernedReadPolicy.AgentCanRead(original.Admission, revoked, Target));
        Assert.False(ProjectDefinitionGovernedReadPolicy.AgentCanRead(original.Admission, readOnly,
            new(ProfileId, Target.ProjectId, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))));
        Assert.False(ProjectDefinitionGovernedReadPolicy.AgentCanRead(original.Admission,
            readOnly with { Status = AgentLifecycleStatus.Suspended }, Target));
    }

    [Fact]
    public void Receipt_target_read_uses_current_profile_alias_with_original_project_lifetime() {
        var original = Agent();
        var currentTarget = new ProjectWriteAdmission(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Target.ProjectId, Target.LifetimeId);
        var current = CurrentAgent(read: true, write: false, target: currentTarget);

        Assert.True(ProjectDefinitionGovernedReadPolicy.AgentCanRead(original.Admission, current, currentTarget));
    }

    [Fact]
    public void Process_receipt_read_survives_expired_dispatch_without_allowing_another_execution_or_ungranted_target() {
        var original = Process();
        var expired = original.Dispatch with {
            ObservedCurrentDispatch = false,
            Evidence = original.Dispatch.Evidence with { ExecutionMayDispatch = false }
        };
        var current = new ProjectProcessExecutionAccess(expired, CanRead: true, CanWrite: false);

        Assert.True(ProjectDefinitionGovernedReadPolicy.ProcessCanRead(original, current, Target));
        Assert.False(ProjectDefinitionGovernedReadPolicy.ProcessCanRead(original, current with { CanRead = false }, Target));
        Assert.False(ProjectDefinitionGovernedReadPolicy.ProcessCanRead(original, current with {
            Dispatch = expired with { Evidence = expired.Evidence with { ExecutionRunId = ChatId } }
        }, Target));
        Assert.False(ProjectDefinitionGovernedReadPolicy.ProcessCanRead(original, current,
            new(ProfileId, ChatId, Target.LifetimeId)));
    }

    [Fact]
    public void Binding_rejects_oversize_material_source_before_hashing_unbounded_fields() {
        var original = Agent(policy: new string('x', 65_537));
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForAgent(original.Admission, original.Session, original.Proposal));
        var process = Process();
        var changed = new ProjectProcessMutationAdmission(process.Dispatch with { CapabilityScope = new() {
            InstructionFragments = [new() { Key = "brief", Content = new string('x', 65_537) }]
        } });
        Assert.Throws<InvalidOperationException>(() => ProjectDefinitionGovernedBindings.ForProcess(changed, ProjectProcessProjectOperation.Update));
    }

    private static (ProjectAgentMutationAdmission Admission, AgentToolSessionObservation Session, AgentToolAdmittedInvocation Proposal)
        Agent(string policy = "original-policy", IReadOnlyList<string>? operations = null,
            string tool = ProjectStructureToolPolicy.ProjectStructureProjectUpdate,
            ProjectAgentMutationDomain domain = ProjectAgentMutationDomain.NonTaskStructure) {
        var governance = new AgentExecutionGovernanceSnapshot(new(AuthorityId), AgentId, ProfileId, new(0),
            WorkspaceScopeDescriptor.Organization(ProfileId.ToString("N")), true, true, "1", policy,
            operations ?? [ProjectStructureToolPolicy.ProjectStructureProjectUpdate]);
        var access = new AgentProjectStructureAccessSettings { CanRead = true, CanWriteNonTaskStructure = true, AllowAllProjects = false,
            AllowedProjectIds = [Target.ProjectId], AllowedProjectLifetimes = [new(ProfileId, Target.ProjectId, Target.LifetimeId)] };
        var admission = new ProjectAgentMutationAdmission(AgentId, ProfileId, access, governance,
            domain, tool);
        var reference = new AgentToolSessionReference(ExecutionId, ChatId, new(AuthorityId));
        var session = new AgentToolSessionObservation(new(reference, AgentId, AgentRuntimeContextPurpose.InteractiveChat,
            new(ProfileId, "original-profile", new(0))), null, governance);
        var digest = new AgentToolSemanticDigest(new string('a', 64));
        var payload = new AgentToolPreparedPayload(tool, 1, digest,
            "{}", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
        return (admission, session, new(reference, new(Guid.Parse("88888888-8888-8888-8888-888888888888")),
            new(Guid.Parse("99999999-9999-9999-9999-999999999999")), payload, ExecutionApprovalStatus.Approved, digest));
    }

    private static ProjectProcessMutationAdmission Process() {
        var run = new ProcessRunId(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        var step = new ProcessStepInstanceId(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var evidence = new ProcessExecutionClaimEvidence(ExecutionId, AgentId, run, step, AuthorityId, "step", Now, true);
        var source = new ProcessLaunchAuthority(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
            ProfileId, new(ProfileId, Target.ProjectId, Target.LifetimeId), true, false, "original-policy");
        return new(new(evidence, run, Target.ProjectId, "owner-fingerprint", "readiness",
            [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction], "project", new(), source,
            new(new(ChatId), "preparation", run, step, "readiness"), true, Now));
    }

    private static AgentDefinition CurrentAgent(bool read, bool write, ProjectWriteAdmission? target = null) {
        target ??= Target;
        var config = AgentProjectStructureAccessMetadata.Write("{}", new() {
            CanRead = read, CanWriteNonTaskStructure = write, AllowAllProjects = false,
            AllowedProjectIds = read ? [target.ProjectId] : [],
            AllowedProjectLifetimes = read ? [new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId)] : []
        });
        return new(AgentId, "Fixture", "", "", "", AgentLifecycleStatus.Active, null, "", AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault, 0, false, false, config, false, "", AgentPermissionsPolicy.Default, [], [], Now, Now);
    }
}
