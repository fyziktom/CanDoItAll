using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentInheritedPlanningRunReaderTests {
    public enum Disclosure { Allowed, MissingJournal, WrongSchema, MissingOrigin, SourceDenied, ParticipantDenied, MissingParticipant, AmbiguousParticipant }

    [Theory]
    [InlineData(Disclosure.Allowed)]
    [InlineData(Disclosure.MissingJournal)]
    [InlineData(Disclosure.WrongSchema)]
    [InlineData(Disclosure.MissingOrigin)]
    [InlineData(Disclosure.SourceDenied)]
    [InlineData(Disclosure.ParticipantDenied)]
    [InlineData(Disclosure.MissingParticipant)]
    [InlineData(Disclosure.AmbiguousParticipant)]
    public async Task Disclosure_uses_current_authority_without_any_execution_or_mutation_port(Disclosure selection) {
        var source = new Source();
        var now = AgentInheritedPlanningTestData.Start;
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), null, "Original input", source.SourceKind,
            Guid.NewGuid().ToString("N"), string.Empty, string.Empty, "fixture", "system", "{}", "Original input",
            string.Empty, string.Empty, string.Empty, ExecutionState.Preparing, null, now, now, now, null,
            string.Empty, null, [], Revision: 1);
        ExecutionRunDetail? detail = null;
        var preparing = true;
        var store = DispatchProxy.Create<IReadStore, ReadStoreProxy>();
        ((ReadStoreProxy)(object)store).Read = (method, arguments) => {
            Assert.Equal(run.Id, arguments[0]);
            ((CancellationToken)arguments[1]!).ThrowIfCancellationRequested();
            return method.Name switch {
                nameof(ISandboxWorkspaceExecutionRunStore.GetExecutionRunAsync) when preparing => Task.FromResult<ExecutionRunRecord?>(null),
                nameof(ISandboxWorkspaceExecutionRunStore.GetExecutionRunDetailAsync) when !preparing => Task.FromResult(detail),
                _ => throw new InvalidOperationException($"The read owner invoked an unexpected storage port: {method.Name}")
            };
        };
        var journal = new AgentToolAdmissionJournal(store, new(Guid.NewGuid(), "isolated-read", new(1)), backgroundSources: [source]);
        var admitted = await journal.CreateForNewBackgroundRunAsync(run, "Original input");
        var inherited = AgentInheritedPlanningTestData.Create(admitted.Session);
        var participant = new Participant(inherited.PlanningDecision!.InheritedOrigin!.ParentBinding);
        run = run with { ToolAdmission = selection switch {
            Disclosure.MissingJournal => null,
            Disclosure.WrongSchema => inherited with { SchemaVersion = AgentToolJournalRecord.BackgroundSchemaVersion },
            Disclosure.MissingOrigin => inherited with { PlanningDecision = inherited.PlanningDecision with { InheritedOrigin = null } },
            _ => inherited
        } };
        detail = new(run, null, [], []);
        preparing = false;
        source.Reads = 0;
        source.Allowed = selection != Disclosure.SourceDenied;
        participant.Allowed = selection != Disclosure.ParticipantDenied;
        var reader = new AgentInheritedPlanningRunReader(store, journal, selection switch {
            Disclosure.MissingParticipant => [],
            Disclosure.AmbiguousParticipant => [participant, participant],
            _ => [participant]
        });

        if (selection == Disclosure.Allowed) {
            Assert.Same(detail, await reader.ReadAsync(run.Id));
        } else if (selection == Disclosure.ParticipantDenied) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync(run.Id));
        } else if (selection is Disclosure.MissingParticipant or Disclosure.AmbiguousParticipant) {
            var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => reader.ReadAsync(run.Id));
            Assert.Equal(selection == Disclosure.MissingParticipant ? "planning.unsupported" : "planning.ambiguous-participant", failure.Code);
        } else {
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => reader.ReadAsync(run.Id));
        }
        Assert.Equal(selection is Disclosure.MissingJournal or Disclosure.WrongSchema or Disclosure.MissingOrigin ? 0 : 1, source.Reads);
        Assert.Equal(selection is Disclosure.Allowed or Disclosure.ParticipantDenied ? 1 : 0, participant.Reads);
        Assert.Same(run, detail.Run);
    }

    private sealed class Source : IAgentToolBackgroundSourcePolicy {
        internal bool Allowed { get; set; } = true;
        internal int Reads { get; set; }
        public string SourceKind => AgentInheritedPlanningTestData.SourceKind;
        public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution, AgentToolProfileBinding profile,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return ValueTask.FromResult(new AgentToolBackgroundSourceObservation(AgentPlanningDecisionTestData.Envelope().Digest, Allowed, true));
        }
    }

    private sealed class Participant(AgentToolProtocolEnvelope parent) : IAgentInheritedPlanningParticipant {
        internal bool Allowed { get; set; } = true;
        internal int Reads { get; private set; }
        public AgentPlanningOwner Owner => AgentPlanningOwner.ProjectTaskPlan;
        public AgentPlanningPromptContract Prompt => throw new InvalidOperationException("A read cannot request execution instructions.");
        public bool SupportsParent(AgentToolProtocolEnvelope candidate) => candidate == parent;
        public Task RequireCurrentReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return Allowed ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException());
        }
        public Task<AgentPlanningBasisRecord> ReadOriginalBasisAsync(AgentPlanningInheritedOrigin original, CancellationToken cancellationToken)
            => throw new InvalidOperationException("A read cannot capture another basis.");
        public AgentPlanningResponsePreparation PreparePublication(AgentPlanningDecisionRecord decision, AgentPlanningBasisRecord basis, string responseText)
            => throw new InvalidOperationException("A read cannot prepare a publication.");
    }

    public interface IReadStore : ISandboxWorkspaceStore, ISandboxWorkspaceExecutionRunStore,
        ISandboxWorkspaceExecutionRunMutationStore, ISandboxWorkspaceExecutionRunLeaseStore {
    }

    public class ReadStoreProxy : DispatchProxy {
        public Func<MethodInfo, object?[], object?> Read { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Read(targetMethod ?? throw new ArgumentNullException(nameof(targetMethod)), args ?? []);
    }
}
