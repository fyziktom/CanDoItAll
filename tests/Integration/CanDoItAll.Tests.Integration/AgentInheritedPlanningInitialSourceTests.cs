using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningInitialSourceTests {
    [Fact]
    public async Task Ordinary_source_support_does_not_implicitly_authorize_inherited_initialization() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var source = new OrdinarySource(fixture.Policy);
        await Assert.ThrowsAsync<NotSupportedException>(() => Factory(fixture, source).CreateAsync(Candidate(fixture),
            "Original input", AgentPlanningOwner.ProjectTaskPlan, fixture.Policy.ParentBinding,
            (_, _) => throw new InvalidOperationException("Unsupported initial authority must not reach binding.")));
        Assert.Equal(0, source.Observations);
    }

    [Fact]
    public async Task Selected_initial_authority_can_succeed_while_ordinary_initial_authority_is_refused() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var source = new SelectedSource(fixture);
        var journal = new AgentToolAdmissionJournal(fixture.Files.NewStore(), fixture.Files.Profile, fixture.Clock, backgroundSources: [source]);
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.CreateForNewBackgroundRunAsync(Candidate(fixture), "Ordinary input"));
        source.FinalObservations = 0;
        var admitted = await new AgentInheritedPlanningAdmission(journal).CreateAsync(Candidate(fixture), "Original input",
            AgentPlanningOwner.ProjectTaskPlan, fixture.Policy.ParentBinding,
            (context, _) => Task.FromResult(Bind(fixture, context)));
        Assert.Equal(AgentToolJournalRecord.InheritedPlanningSchemaVersion, admitted.SchemaVersion);
        Assert.Equal(AgentToolAdmissionSupport.EffectJournalOnly, admitted.Support);
        Assert.Equal(fixture.Policy.ParentBinding, admitted.PlanningDecision!.InheritedOrigin!.ParentBinding);
        Assert.Equal(1, source.InitialObservations);
        Assert.Equal(1, source.FinalObservations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Initial_read_or_dispatch_denial_prevents_binding(bool denyRead) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var source = new SelectedSource(fixture) { InitialRead = !denyRead, InitialDispatch = denyRead };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Factory(fixture, source).CreateAsync(Candidate(fixture),
            "Original input", AgentPlanningOwner.ProjectTaskPlan, fixture.Policy.ParentBinding,
            (_, _) => throw new InvalidOperationException("Denied initial authority must not reach binding.")));
        Assert.Equal(1, source.InitialObservations);
        Assert.Equal(0, source.FinalObservations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_or_oversized_parent_is_refused_before_owner_observation(bool missing) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var source = new SelectedSource(fixture);
        var parent = missing ? null : AgentPlanningDecisionTestData.Envelope("{\"text\":\"" +
            new string('\u00e9', AgentPlanningInheritedOrigin.MaximumBindingUtf8Bytes / 2 + 1) + "\"}");
        var pending = Factory(fixture, source).CreateAsync(Candidate(fixture), "Original input", AgentPlanningOwner.ProjectTaskPlan,
            parent!, (_, _) => throw new InvalidOperationException("Invalid initial parent must not reach binding."));
        if (missing) {
            await Assert.ThrowsAsync<ArgumentNullException>(() => pending);
        } else {
            await Assert.ThrowsAsync<InvalidDataException>(() => pending);
        }
        Assert.Equal(0, source.InitialObservations);
        Assert.Equal(0, source.FinalObservations);
    }

    [Fact]
    public async Task Binding_cannot_replace_the_parent_checked_by_initial_source_authority() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var source = new SelectedSource(fixture);
        var bound = 0;
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Factory(fixture, source).CreateAsync(Candidate(fixture),
            "Original input", AgentPlanningOwner.ProjectTaskPlan, fixture.Policy.ParentBinding, (context, _) => {
                bound++;
                return Task.FromResult(Bind(fixture, context, AgentPlanningDecisionTestData.Envelope("{\"parent\":2}")));
            }));
        Assert.Equal(1, bound);
        Assert.Equal(1, source.InitialObservations);
        Assert.Equal(0, source.FinalObservations);
    }

    [Theory]
    [InlineData(InvalidInput.Missing)]
    [InlineData(InvalidInput.Blank)]
    [InlineData(InvalidInput.Oversized)]
    public async Task Invalid_original_input_never_reaches_the_source_owner_or_binding(InvalidInput kind) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var source = new SelectedSource(fixture);
        var input = kind switch {
            InvalidInput.Missing => null,
            InvalidInput.Blank => " \t\r\n",
            InvalidInput.Oversized => new string('\u00e9', AgentToolProtocolEnvelope.MaximumUtf8Bytes / 2 + 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => Factory(fixture, source).CreateAsync(Candidate(fixture), input!,
            AgentPlanningOwner.ProjectTaskPlan, fixture.Policy.ParentBinding,
            (_, _) => throw new InvalidOperationException("Invalid original input must not reach binding.")));
        Assert.Equal(0, source.InitialObservations);
        Assert.Equal(0, source.FinalObservations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_after_initial_observation_cannot_reach_binding_even_when_owner_ignores_it(bool ignoreCancellation) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var source = new SelectedSource(fixture) {
            BeforeInitialResult = async token => {
                entered.SetResult();
                await release.Task.WaitAsync(ignoreCancellation ? CancellationToken.None : token);
            }
        };
        var pending = Factory(fixture, source).CreateAsync(Candidate(fixture), "Original input", AgentPlanningOwner.ProjectTaskPlan,
            fixture.Policy.ParentBinding, (_, _) => throw new InvalidOperationException("Canceled initial authority must not reach binding."), cancellation.Token);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await cancellation.CancelAsync();
        } finally {
            release.TrySetResult();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(1, source.InitialObservations);
        Assert.Equal(0, source.FinalObservations);
    }

    [Theory]
    [InlineData(FinalChange.Read)]
    [InlineData(FinalChange.Dispatch)]
    [InlineData(FinalChange.Fingerprint)]
    public async Task Completed_inherited_journal_must_pass_current_source_revalidation(FinalChange change) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var source = new SelectedSource(fixture) { Final = change };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Factory(fixture, source).CreateAsync(Candidate(fixture),
            "Original input", AgentPlanningOwner.ProjectTaskPlan, fixture.Policy.ParentBinding,
            (context, _) => Task.FromResult(Bind(fixture, context))));
        Assert.Equal(1, source.InitialObservations);
        Assert.Equal(1, source.FinalObservations);
    }

    [Fact]
    public async Task Retained_workspace_child_cannot_be_rebound_to_a_replacement_requested_parent() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var original = await fixture.SeedInitialAsync();
        var request = fixture.Request with {
            ParentBinding = AgentPlanningDecisionTestData.Envelope("{\"parent\":2}"),
            BindAsync = (_, _) => throw new InvalidOperationException("An original child cannot be rebound.")
        };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(request));
        var retained = await fixture.Files.Store.GetExecutionRunAsync(original.Id);
        Assert.NotNull(retained);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(retained));
        Assert.Equal(1, fixture.Bindings);
    }

    private static ExecutionRunRecord Candidate(AgentInheritedPlanningFixture fixture)
        => fixture.Original with { Id = Guid.NewGuid(), ToolAdmission = null };

    private static AgentInheritedPlanningAdmission Factory(AgentInheritedPlanningFixture fixture, IAgentToolBackgroundSourcePolicy source)
        => new(new(fixture.Files.NewStore(), fixture.Files.Profile, fixture.Clock, backgroundSources: [source]));

    private static AgentInheritedPlanningBinding Bind(AgentInheritedPlanningFixture fixture, AgentInheritedPlanningBindingContext context,
        AgentToolProtocolEnvelope? replacement = null) {
        var decision = fixture.Original.ToolAdmission!.PlanningDecision!;
        return new(decision.OriginalTarget, decision.InheritedOrigin! with {
            Source = context.Session.Reference.BackgroundSource!, ParentBinding = replacement ?? fixture.Policy.ParentBinding
        });
    }

    public enum FinalChange { Read, Dispatch, Fingerprint }
    public enum InvalidInput { Missing, Blank, Oversized }

    private sealed class OrdinarySource(AgentInheritedPlanningFixture.SourcePolicy original) : IAgentToolBackgroundSourcePolicy {
        public string SourceKind => original.SourceKind;
        internal int Observations { get; private set; }
        public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution, AgentToolProfileBinding profile,
            CancellationToken cancellationToken = default) {
            Observations++;
            return original.ObserveAsync(execution, profile, cancellationToken);
        }
    }

    private sealed class SelectedSource(AgentInheritedPlanningFixture fixture) : IAgentToolBackgroundSourcePolicy {
        public string SourceKind => fixture.Policy.SourceKind;
        internal int InitialObservations { get; private set; }
        internal int FinalObservations { get; set; }
        internal bool InitialRead { get; init; } = true;
        internal bool InitialDispatch { get; init; } = true;
        internal FinalChange? Final { get; init; }
        internal Func<CancellationToken, Task>? BeforeInitialResult { get; init; }
        public async ValueTask<AgentToolBackgroundSourceObservation> ObserveInitialInheritedAsync(ExecutionRunRecord candidate,
            AgentToolProfileBinding profile, AgentToolProtocolEnvelope originalParent, CancellationToken cancellationToken = default) {
            InitialObservations++;
            Assert.Null(candidate.ToolAdmission);
            Assert.Equal(fixture.Files.Profile, profile);
            Assert.Equal(fixture.Policy.ParentBinding, originalParent);
            if (BeforeInitialResult is not null) {
                await BeforeInitialResult(cancellationToken);
            }
            return new(fixture.Policy.Fingerprint, InitialRead, InitialDispatch);
        }

        public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution, AgentToolProfileBinding profile,
            CancellationToken cancellationToken = default) {
            FinalObservations++;
            if (execution.ToolAdmission?.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion) {
                throw new InvalidDataException("The selected fixture source refuses ordinary initial admission.");
            }
            Assert.Equal(fixture.Policy.ParentBinding, execution.ToolAdmission.PlanningDecision!.InheritedOrigin!.ParentBinding);
            return ValueTask.FromResult(new AgentToolBackgroundSourceObservation(Final == FinalChange.Fingerprint
                ? AgentPlanningDecisionTestData.Envelope("{\"changed\":true}").Digest : fixture.Policy.Fingerprint,
                Final != FinalChange.Read, Final != FinalChange.Dispatch));
        }
    }
}
