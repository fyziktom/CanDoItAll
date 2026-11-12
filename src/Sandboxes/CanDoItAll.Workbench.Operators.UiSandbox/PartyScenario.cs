using CanDoItAll.Workbench.Operators.UI.Parties;

namespace CanDoItAll.Workbench.Operators.UiSandbox;

public enum PartyScenarioKind { Normal, Loading, Empty, Unavailable, Missing, Sensitive, Large, Delayed, Rejected, AcceptedReadFailure, Unknown }

public sealed class PartyScenario {
    public PartyEditorState State { get; }
    public int CreateCalls { get; private set; }
    public int SaveCalls { get; private set; }
    private readonly PartyScenarioKind scenario;

    public PartyScenario(PartyEditorKind kind, PartyScenarioKind scenario) {
        this.scenario = scenario;
        State = new(kind, "Operator collaboration") { IsLoading = scenario == PartyScenarioKind.Loading,
            IsUnavailable = scenario == PartyScenarioKind.Unavailable };
        State.Draft.KeepLocal = false;
        State.Choices = scenario is PartyScenarioKind.Empty or PartyScenarioKind.Unavailable ? [] : Enumerable.Range(1, scenario == PartyScenarioKind.Large ? 64 : 4)
            .Select(index => new PartyChoice(Guid.NewGuid(), index == 1 ? "Alex — project owner" : $"Delivery party {index}", index == 2 ? "Organization" : "Person",
                index == 2 ? "business" : "person", index != 2,
                scenario == PartyScenarioKind.Sensitive ? "" : $"party{index}@example.invalid", IsSensitive: scenario == PartyScenarioKind.Sensitive,
                IsMissing: scenario == PartyScenarioKind.Missing && index == 1)).ToArray();
        if (State.Choices.FirstOrDefault() is { } first) {
            State.ProjectDefaults = [first.Id];
            if (scenario == PartyScenarioKind.Missing) {
                State.Draft.Participant = first.Id;
                State.Draft.MeetingParties.Add(first.Id);
            }
        }
    }

    public async Task CreateAsync(PartyQuickCreateInput input) {
        if (!State.CanEdit || State.Created is not null) {
            return;
        }
        State.IsBusy = true;
        CreateCalls++;
        try {
            if (scenario == PartyScenarioKind.Delayed) {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            if (scenario == PartyScenarioKind.Rejected) {
                State.Message = "Fixture directory rejected this request. No native effect occurred.";
            } else if (scenario == PartyScenarioKind.Unknown) {
                State.RequiresObservation = true;
                State.Message = "Fixture result is unknown. Inspect before another create; no native effect occurred.";
            } else {
                var accepted = new PartyCreated(Guid.NewGuid(), input.Name, scenario == PartyScenarioKind.AcceptedReadFailure ? "Fixture option read failed." : null);
                State.Created = accepted;
                State.Choices = State.Choices.Append(new(accepted.Id, input.Name, input.Kind.ToString(), "person", true, input.Email, input.Phone)).ToArray();
                State.Draft.Participant = accepted.Id;
                State.Message = $"Fixture accepted identity {accepted.Id:D}. No native directory record was created.";
            }
        } finally {
            State.IsBusy = false;
        }
    }

    public Task SaveAsync(PartySelection selection) {
        if (!State.CanEdit) {
            return Task.CompletedTask;
        }
        SaveCalls++;
        var phase = scenario switch {
            PartyScenarioKind.Rejected => PartySavePhase.Rejected,
            PartyScenarioKind.Unknown => PartySavePhase.Unknown,
            PartyScenarioKind.AcceptedReadFailure => PartySavePhase.NodeCommitted,
            _ => PartySavePhase.Observed
        };
        State.Receipt = new(phase, phase is PartySavePhase.Rejected or PartySavePhase.Unknown ? [] : [Guid.NewGuid()],
            phase is PartySavePhase.NodeCommitted or PartySavePhase.Observed ? "fixture-node" : null, $"Fixture {phase}; no native assignment or node was written.");
        State.RequiresObservation = phase is PartySavePhase.Unknown or PartySavePhase.NodeCommitted;
        return Task.CompletedTask;
    }

    public Task RetryAsync() {
        State.Message = "Fixture read-only observation completed; create/save was not repeated.";
        if (State.Receipt is { Phase: PartySavePhase.NodeCommitted } receipt) {
            State.Receipt = receipt with { Phase = PartySavePhase.Observed };
            State.RequiresObservation = false;
        }
        return Task.CompletedTask;
    }
}
