using System.Collections.Immutable;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Workspace.StorageCatalog.UiSandbox;

public sealed class CatalogScenarioStore : IStorageCatalogOwner {
    private readonly Dictionary<Guid, CatalogEdit> entries;
    private readonly Dictionary<CatalogPurpose, Guid> routes = [];
    public CatalogScenarioStore(CatalogScenario scenario = CatalogScenario.Representative) {
        Scenario = scenario;
        entries = scenario == CatalogScenario.Empty ? [] : CatalogScenarioData.Entries.ToDictionary(item => item.Id!.Value);
        routes[CatalogPurpose.ProjectAsset] = CatalogScenarioData.FileSystemId;
        if (scenario == CatalogScenario.Empty) {
            routes.Clear();
        }
        if (scenario == CatalogScenario.Large) {
            for (var index = 0; index < 200; index++) {
                var id = Guid.NewGuid();
                entries[id] = Choices.Providers[index % 3].Template with { Id = id, Name = $"Catalog target {index:D3}", EndpointOrRoot = $"/scenario/target-{index}", DisplayOrder = index };
            }
        }
        if (scenario == CatalogScenario.ReadFailure) {
            Gates[CatalogScenarioStage.CatalogRead].FailNext = true;
            Gates[CatalogScenarioStage.EditorRead].FailNext = true;
        }
        if (scenario == CatalogScenario.PartialReferences) {
            Gates[CatalogScenarioStage.SecretsRead].FailNext = true;
        }
    }
    public CatalogScenario Scenario { get; }
    public CatalogContext Context { get; } = new(Guid.NewGuid(), 0);
    public bool IsCurrent { get; set; } = true;
    public CatalogChoices Choices => CatalogScenarioData.Choices;
    public Dictionary<CatalogScenarioStage, CatalogScenarioGate> Gates { get; } = Enum.GetValues<CatalogScenarioStage>().ToDictionary(stage => stage, _ => new CatalogScenarioGate());
    public int CatalogWrites { get; private set; }
    public int DriverCalls { get; private set; }
    public int RoutingWrites { get; private set; }
    public int ActivityCalls { get; private set; }
    public int PendingCommands { get; private set; }
    public IReadOnlyDictionary<Guid, CatalogEdit> Entries => entries;
    public async Task<ImmutableArray<CatalogRow>> ReadCatalogAsync(CancellationToken cancellationToken) {
        await Gates[CatalogScenarioStage.CatalogRead].EnterAsync();
        return [.. entries.Values.Select(value => new CatalogRow(value.Id!.Value, value.Name, value.ProviderKind, value.ConnectionMode,
            value.EndpointOrRoot, value.DisplayOrder, value.IsEnabled, value.IsSystemDefault, value.IsReadOnly, value.Health))];
    }
    public async Task<ImmutableArray<CatalogSecret>> ReadSecretsAsync(CancellationToken cancellationToken) {
        await Gates[CatalogScenarioStage.SecretsRead].EnterAsync();
        return Scenario == CatalogScenario.MissingReferences ? [] : [new(CatalogScenarioData.SecretId, "Scenario credential reference", true)];
    }
    public async Task<ImmutableArray<CatalogRoute>> ReadRoutesAsync(CancellationToken cancellationToken) {
        await Gates[CatalogScenarioStage.RoutesRead].EnterAsync();
        return [.. Choices.Purposes.Select(purpose => {
            var id = routes.GetValueOrDefault(purpose.Value);
            return new CatalogRoute(purpose.Value, id == Guid.Empty ? null : id, entries.GetValueOrDefault(id)?.Name ?? string.Empty, id != Guid.Empty);
        })];
    }
    public async Task<CatalogEdit?> ReadEditorAsync(Guid id, CancellationToken cancellationToken) {
        await Gates[CatalogScenarioStage.EditorRead].EnterAsync();
        if (CatalogWrites > 0) {
            await Gates[CatalogScenarioStage.ReadBack].EnterAsync();
            if (Scenario == CatalogScenario.ReadBackFailure) {
                throw new InvalidOperationException("Synthetic read-back failure.");
            }
        }
        return Scenario == CatalogScenario.MissingTarget ? null : entries.TryGetValue(id, out var entry)
            ? entry with { DefaultPurposes = [.. routes.Where(pair => pair.Value == id).Select(pair => pair.Key)] } : null;
    }
    public async Task<CatalogOutcome> ExecuteAsync(CatalogCommand command, CancellationToken cancellationToken) {
        if (!IsCurrent || command.Context != Context || Scenario == CatalogScenario.Refused) {
            return new() { Diagnostic = CatalogDiagnostic.Retired };
        }
        PendingCommands++;
        try {
            return await ExecuteCapturedAsync(command);
        } finally {
            PendingCommands--;
        }
    }
    private async Task<CatalogOutcome> ExecuteCapturedAsync(CatalogCommand command) {
        var outcome = new CatalogOutcome();
        var draft = command.Draft;
        await Gates[CatalogScenarioStage.Admission].EnterAsync();
        if (draft.IsSystemDefault || (draft.Id is { } existing && entries.GetValueOrDefault(existing)?.IsSystemDefault == true)) {
            return new() { Diagnostic = CatalogDiagnostic.Protected };
        }
        if (draft.Id is { } original && !entries.ContainsKey(original)) {
            return new() { Diagnostic = CatalogDiagnostic.Missing };
        }
        if (command.Effect == CatalogEffect.Test) {
            if (draft.CredentialSecretId.HasValue && (Scenario == CatalogScenario.MissingReferences || draft.CredentialSecretId != CatalogScenarioData.SecretId)) {
                return new() { Diagnostic = CatalogDiagnostic.CredentialDenied };
            }
            DriverCalls++;
            try {
                await Gates[CatalogScenarioStage.Driver].EnterAsync();
            } catch (Exception) {
                return new() { Driver = CatalogDriver.Unknown, Diagnostic = CatalogDiagnostic.DriverFailed };
            }
            var health = new CatalogHealthFact(Scenario == CatalogScenario.Degraded ? CatalogHealth.Degraded : Scenario == CatalogScenario.Unavailable ? CatalogHealth.Unavailable : CatalogHealth.Healthy,
                draft.IsReadOnly ? Choices.Providers[(int)draft.ProviderKind].ReadOnlyCapabilities : Choices.Providers[(int)draft.ProviderKind].WritableCapabilities,
                CatalogScenarioData.TestedAt, "Simulated remote health; no real driver or credential was used.");
            outcome = outcome with { Driver = CatalogDriver.Completed, Health = health };
            draft = draft with { Health = health };
        }
        if (command.Effect != CatalogEffect.Test || draft.Id.HasValue) {
            try {
                await Gates[CatalogScenarioStage.Persistence].EnterAsync();
                var id = draft.Id ?? Guid.NewGuid();
                if (command.Effect == CatalogEffect.Delete) {
                    entries.Remove(id);
                    foreach (var purpose in routes.Where(pair => pair.Value == id).Select(pair => pair.Key).ToArray()) {
                        routes.Remove(purpose);
                    }
                } else {
                    entries[id] = draft with { Id = id, Name = draft.Name.Trim(), EndpointOrRoot = draft.EndpointOrRoot.Trim() };
                }
                CatalogWrites++;
                if (Scenario == CatalogScenario.UnknownAcknowledgement) {
                    return outcome with { Write = CatalogWrite.Unknown, Diagnostic = CatalogDiagnostic.PersistenceFailed };
                }
                outcome = outcome with { Write = CatalogWrite.Committed, CatalogId = id };
            } catch (Exception) {
                return outcome with { Write = CatalogWrite.Unknown, Diagnostic = CatalogDiagnostic.PersistenceFailed };
            }
        }
        if (command.Effect == CatalogEffect.Save) {
            try {
                await Gates[CatalogScenarioStage.Routing].EnterAsync();
                foreach (var purpose in Choices.Purposes) {
                    if (draft.DefaultPurposes.Contains(purpose.Value)) {
                        routes[purpose.Value] = outcome.CatalogId!.Value;
                    } else if (routes.GetValueOrDefault(purpose.Value) == outcome.CatalogId) {
                        routes.Remove(purpose.Value);
                    }
                    RoutingWrites++;
                    if (Scenario == CatalogScenario.PartialRouting) {
                        throw new InvalidOperationException("Synthetic partial routing failure.");
                    }
                }
                outcome = outcome with { Routing = CatalogRouting.Complete };
            } catch (Exception) {
                return outcome with { Routing = CatalogRouting.PossiblyPartial, Diagnostic = CatalogDiagnostic.RoutingFailed };
            }
        }
        try {
            ActivityCalls++;
            await Gates[CatalogScenarioStage.Activity].EnterAsync();
            if (Scenario == CatalogScenario.ActivityFailure) {
                throw new InvalidOperationException("Synthetic Activity failure.");
            }
            return outcome with { Activity = CatalogActivity.Complete };
        } catch (Exception) {
            return outcome with { Activity = CatalogActivity.Failed, Diagnostic = CatalogDiagnostic.ActivityFailed };
        }
    }
    public void ReleaseAll() {
        foreach (var gate in Gates.Values) {
            gate.Release();
        }
    }
}
