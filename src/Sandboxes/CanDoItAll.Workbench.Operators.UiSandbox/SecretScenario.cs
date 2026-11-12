using CanDoItAll.Modules.Security;
using CanDoItAll.Workbench.Operators.UI.Secrets;

namespace CanDoItAll.Workbench.Operators.UiSandbox;

public enum SecretScenarioKind { Normal, Loading, Empty, Unavailable, Missing, Large, LongError, Delayed, Rejected, AcceptedReadFailure, ReferenceReadFailure, Unknown }

public sealed class SecretScenario {
    public SecretReferenceState State { get; }
    public int Creates { get; private set; }
    public int References { get; private set; }
    private readonly SecretScenarioKind scenario;
    private SecretReferenceInput? original;

    public SecretScenario(SecretScenarioKind scenario, bool edit) {
        this.scenario = scenario;
        State = new(edit) { IsLoading = scenario == SecretScenarioKind.Loading, IsUnavailable = scenario == SecretScenarioKind.Unavailable };
        State.Items = scenario is SecretScenarioKind.Empty or SecretScenarioKind.Unavailable ? [] :
            Enumerable.Range(1, scenario == SecretScenarioKind.Large ? 64 : 3).Select(index =>
                new SecretListItem(Guid.NewGuid(), $"Fixture secret {index}", SecretKind.Token, "Fixture metadata only", DateTimeOffset.UnixEpoch)).ToArray();
        if (edit) {
            State.Draft.SelectedId = State.Items.FirstOrDefault()?.Id;
        }
        if (scenario == SecretScenarioKind.Missing) {
            State.Draft.SelectedId = Guid.NewGuid();
        }
        if (State.IsUnavailable) {
            State.Message = "Fixture metadata is unavailable. This is not an empty vault.";
        }
        if (scenario == SecretScenarioKind.LongError) {
            State.Message = string.Join(' ', Enumerable.Repeat("The original metadata read failed. Retry observation without repeating a vault write.", 80));
        }
    }

    public async Task CreateAsync(SecretReferenceInput input, SecretEditorModel model) {
        if (!State.CanEdit) {
            return;
        }
        Creates++;
        original = input;
        State.IsBusy = true;
        try {
            if (scenario == SecretScenarioKind.Delayed) {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            if (scenario == SecretScenarioKind.Rejected) {
                State.Message = "Fixture validation refused this draft before any native effect.";
                return;
            }
            State.Draft.Create.SecretValue = string.Empty;
            model.SecretValue = string.Empty;
            State.RequiresObservation = true;
            var id = Guid.NewGuid();
            State.Receipt = new(scenario == SecretScenarioKind.Unknown ? SecretReferencePhase.Unknown : SecretReferencePhase.VaultCommitted,
                id, null, "Fixture identity retained; no native vault operation occurred.");
            if (scenario is not (SecretScenarioKind.Unknown or SecretScenarioKind.AcceptedReadFailure) && !State.IsRetired) {
                Reference(id);
            }
        } finally {
            State.IsBusy = false;
        }
    }

    public Task UseAsync(SecretReferenceInput input) {
        if (State.CanEdit && input.SecretId is { } id) {
            original = input;
            Reference(id);
        }
        return Task.CompletedTask;
    }

    public Task RetryAsync() {
        if (State.Receipt is { Phase: SecretReferencePhase.VaultCommitted }) {
            State.CanFinishReference = true;
            State.Message = "Fixture metadata observed. Finish the captured reference explicitly; no create repeated.";
        } else if (State.Receipt is { Phase: SecretReferencePhase.ReferenceCommitted } receipt) {
            State.Receipt = receipt with { Phase = SecretReferencePhase.Observed, Message = "Fixture reference observed without another write." };
        } else if (State.Receipt is null) {
            State.IsLoading = false;
            State.IsUnavailable = false;
            State.Message = "Fixture metadata reloaded.";
        }
        return Task.CompletedTask;
    }

    public Task FinishAsync() {
        if (State.CanFinishReference && State.Receipt?.SecretId is { } id) {
            Reference(id);
        }
        return Task.CompletedTask;
    }

    private void Reference(Guid id) {
        References++;
        State.Message = string.Empty;
        State.Draft.SelectedId = id;
        if (State.Items.All(item => item.Id != id)) {
            State.Items = [.. State.Items, new(id, State.Draft.Create.Name, State.Draft.Create.Kind, "Fixture metadata only", DateTimeOffset.UnixEpoch)];
        }
        State.CanFinishReference = false;
        State.RequiresObservation = true;
        State.Receipt = new(scenario == SecretScenarioKind.ReferenceReadFailure ? SecretReferencePhase.ReferenceCommitted : SecretReferencePhase.Observed,
            id, $"fixture-reference-{Guid.NewGuid():N}", $"Fixture reference recorded for original purpose: {original?.Purpose}. No native write occurred.");
    }
}
