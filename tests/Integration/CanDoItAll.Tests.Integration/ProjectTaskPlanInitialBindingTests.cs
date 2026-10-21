using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanInitialBindingTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Registered_initializer_admits_only_original_material_and_the_exact_source_executor_pair(bool agentSource, bool sameExecutor) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource, sameExecutor,
            registeredSource: true, useInitialBinder: true);
        var decision = fixture.Original.ToolAdmission!.PlanningDecision!;
        var target = ProjectTaskPlanProcessCodec.Target(decision);
        Assert.Equal(fixture.Sql.Project, target.Project);
        Assert.Equal(fixture.Process.Parent, ProjectTaskPlanProcessCodec.Parent(decision.InheritedOrigin!));
        var participant = Participant(fixture);
        var basis = await participant.ReadOriginalBasisAsync(decision.InheritedOrigin!, default);
        Assert.Equal(fixture.Process.Material.PayloadJson, basis.Value.PayloadJson);
        Assert.Equal(fixture.Process.Parent.Child.Grant, decision.InheritedOrigin!.Allocation.Grant);
        Assert.Empty(decision.Current.Bases);
        await fixture.Sql.VerifyAsync(0, 0);
    }

    public enum IdentityChange { CandidateId, CandidateAgent, CandidateRevision, CandidateState, CandidateCreated,
        SessionAgent, SessionSource, EvidenceClaim, EvidenceChild, EvidenceCreated }

    [Theory]
    [InlineData(IdentityChange.CandidateId)]
    [InlineData(IdentityChange.CandidateAgent)]
    [InlineData(IdentityChange.CandidateRevision)]
    [InlineData(IdentityChange.CandidateState)]
    [InlineData(IdentityChange.CandidateCreated)]
    [InlineData(IdentityChange.SessionAgent)]
    [InlineData(IdentityChange.SessionSource)]
    [InlineData(IdentityChange.EvidenceClaim)]
    [InlineData(IdentityChange.EvidenceChild)]
    [InlineData(IdentityChange.EvidenceCreated)]
    public async Task Changed_initial_identity_is_refused_without_modifying_the_original_journal(IdentityChange change) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        var original = await fixture.ReadAsync();
        var candidate = Candidate(original);
        var session = original.ToolAdmission!.Session;
        var evidence = Evidence(candidate);
        switch (change) {
            case IdentityChange.CandidateId:
                candidate = candidate with { Id = Guid.NewGuid() };
                break;
            case IdentityChange.CandidateAgent:
                candidate = candidate with { AgentId = Guid.NewGuid() };
                break;
            case IdentityChange.CandidateRevision:
                candidate = candidate with { Revision = 2 };
                break;
            case IdentityChange.CandidateState:
                candidate = candidate with { State = ExecutionState.Running };
                break;
            case IdentityChange.CandidateCreated:
                candidate = candidate with { CreatedAtUtc = candidate.CreatedAtUtc.AddTicks(1) };
                break;
            case IdentityChange.SessionAgent:
                session = session with { AgentId = Guid.NewGuid() };
                break;
            case IdentityChange.SessionSource:
                var source = session.Reference.BackgroundSource!;
                session = session with { Reference = new(session.Reference.ExecutionRunId, Guid.Empty, default,
                    new(source.SourceKind, "replacement-step", source.OwnerFingerprint, source.ExecutionFingerprint)) };
                break;
            case IdentityChange.EvidenceClaim:
                evidence = evidence with { DispatchClaimToken = Guid.NewGuid() };
                break;
            case IdentityChange.EvidenceChild:
                evidence = evidence with { ExecutionRunId = Guid.NewGuid() };
                break;
            case IdentityChange.EvidenceCreated:
                evidence = evidence with { ExecutionCreatedAtUtc = evidence.ExecutionCreatedAtUtc.AddTicks(1) };
                break;
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Participant(fixture).BindInitialAsync(new(candidate, session),
            original.ToolAdmission.PlanningDecision!.InheritedOrigin!.ParentBinding, Envelope(evidence), default));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await fixture.ReadAsync()));
        await fixture.Sql.VerifyAsync(0, 0);
    }

    public enum CurrentChange { LauncherTools, ExecutorTools, ExecutorWrite, LauncherMissing, ExecutorMissing,
        ExecutorTemplate, ExecutorInactive, ProfileGeneration, Cancelled }

    [Theory]
    [InlineData(CurrentChange.LauncherTools)]
    [InlineData(CurrentChange.ExecutorTools)]
    [InlineData(CurrentChange.ExecutorWrite)]
    [InlineData(CurrentChange.LauncherMissing)]
    [InlineData(CurrentChange.ExecutorMissing)]
    [InlineData(CurrentChange.ExecutorTemplate)]
    [InlineData(CurrentChange.ExecutorInactive)]
    [InlineData(CurrentChange.ProfileGeneration)]
    [InlineData(CurrentChange.Cancelled)]
    public async Task Initial_binding_rechecks_current_source_and_executor_before_accepting_original_material(CurrentChange change) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        var original = await fixture.ReadAsync();
        if (change == CurrentChange.ProfileGeneration) {
            fixture.Generations.Advance();
        } else if (change != CurrentChange.Cancelled) {
            var id = change is CurrentChange.LauncherTools or CurrentChange.LauncherMissing ? fixture.Launcher!.Id : fixture.Executor.Id;
            await fixture.RevokeAsync(id, agent => change switch {
                CurrentChange.ExecutorMissing or CurrentChange.LauncherMissing => null,
                CurrentChange.ExecutorWrite => agent with {
                    ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", new() { CanRead = true, AllowAllProjects = true })
                },
                CurrentChange.ExecutorTemplate => agent with { IsTemplate = true },
                CurrentChange.ExecutorInactive => agent with { Status = AgentLifecycleStatus.Suspended },
                _ => agent with { Permissions = agent.Permissions with { CanUseTools = false } }
            });
        }
        using var cancellation = new CancellationTokenSource();
        if (change == CurrentChange.Cancelled) {
            await cancellation.CancelAsync();
        }
        var candidate = Candidate(original);
        var failure = await Record.ExceptionAsync(() => Participant(fixture).BindInitialAsync(new(candidate, original.ToolAdmission!.Session),
            original.ToolAdmission.PlanningDecision!.InheritedOrigin!.ParentBinding, Envelope(Evidence(candidate)), cancellation.Token));
        Assert.NotNull(failure);
        if (change == CurrentChange.Cancelled) {
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        } else if (change is CurrentChange.LauncherTools or CurrentChange.LauncherMissing) {
            Assert.IsType<ProcessLaunchAuthorityRejectedException>(failure);
        } else {
            Assert.IsType<AgentToolAdmissionException>(failure);
        }
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await fixture.ReadAsync()));
        await fixture.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Changed_original_material_bytes_are_refused_without_capturing_a_replacement() {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        var original = await fixture.ReadAsync();
        await using (var database = Context(fixture.Sql.Services)) {
            var material = await database.DecisionMaterials.SingleAsync(item => item.MaterialId == fixture.Process.Material.Reference.MaterialId.Value);
            material.PayloadJson = "{}";
            material.PayloadUtf8Bytes = Encoding.UTF8.GetByteCount(material.PayloadJson);
            await database.SaveChangesAsync();
        }
        var candidate = Candidate(original);
        await Assert.ThrowsAsync<InvalidDataException>(() => Participant(fixture).BindInitialAsync(new(candidate, original.ToolAdmission!.Session),
            original.ToolAdmission.PlanningDecision!.InheritedOrigin!.ParentBinding, Envelope(Evidence(candidate)), default));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await fixture.ReadAsync()));
        await fixture.Sql.VerifyAsync(0, 0);
    }

    private static IAgentInheritedPlanningInitialBinder Participant(ProjectTaskPlanProcessFixture fixture)
        => Assert.IsAssignableFrom<IAgentInheritedPlanningInitialBinder>(Assert.Single(
            fixture.Sql.Services.GetServices<IAgentInheritedPlanningParticipant>(),
            participant => participant.Owner == AgentPlanningOwner.ProjectTaskPlan &&
                participant.SupportsParent(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentBinding)));

    private static ExecutionRunRecord Candidate(ExecutionRunRecord original)
        => original with { Revision = 1, ToolAdmission = null };

    private static ProcessExecutionClaimEvidence Evidence(ExecutionRunRecord candidate)
        => ProcessExecutionClaimEvidenceReader.Read(candidate, out _)
            ?? throw new InvalidOperationException("The fixture candidate lost its original Process claim.");

    private static AgentToolProtocolEnvelope Envelope(ProcessExecutionClaimEvidence evidence)
        => AgentToolProtocolEnvelope.Create(ProcessDecisionInitialSourceCodec.Format, ProcessDecisionInitialSourceCodec.Version,
            ProcessDecisionInitialSourceCodec.Serialize(evidence));
}
