using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.AgentFramework.SharedProviders.UiSandbox;

public enum SharingScenario {
    Local, Ineligible, RuntimeOnly, Imported, Offline, UnavailableModel, Retired, Loading, ReadFailure,
    MetadataFailure, MissingCredential, EmptySources, LargeCatalog, Held, Rejected, Unknown,
    CommittedReadFailure, DeliveryFailure, ConcurrentLocalEdit, MetadataOnly, UnknownBeforeCommit,
    DisabledSource, IdentityMismatch, AuthorizationFailure, Unpublished, MissingImport
}

public sealed record ScenarioSource(Guid Id, Guid Token, SharedProviderSourceValues Values);

public sealed class SharedProviderScenarioStore {
    public Guid ProviderId { get; } = Guid.NewGuid();
    public Guid CredentialId { get; } = Guid.NewGuid();
    public SharedProviderPublicationId PublicationId { get; } = new(Guid.NewGuid());
    public Guid PublicationToken { get; private set; } = Guid.NewGuid();
    public bool Published { get; private set; }
    public SharedProviderImportBaseline Import { get; private set; }
    public string RemoteName { get; private set; } = "Research model 東京";
    public IReadOnlyList<SharedProviderCatalogPublication> Catalog { get; }
    public Dictionary<Guid, ScenarioSource> Sources { get; } = [];
    public HashSet<SharedProviderPublicationId> Selected { get; } = [];
    public int Writes { get; private set; }

    public SharedProviderScenarioStore(SharingScenario scenario = SharingScenario.Imported) {
        var source = new ScenarioSource(Guid.NewGuid(), Guid.NewGuid(),
            new("Research instance", "https://research.example.test/", CredentialId, scenario != SharingScenario.DisabledSource, false));
        Sources.Add(source.Id, source);
        Import = new(Guid.NewGuid(), ProviderId, source.Id, PublicationId, Guid.NewGuid(), Guid.NewGuid(), new("Team model", true));
        Catalog = Enumerable.Range(0, scenario == SharingScenario.LargeCatalog ? 200 : 3).Select(index => {
            var id = index == 0 ? PublicationId : new SharedProviderPublicationId(Guid.NewGuid());
            var model = SharedProviderRoutingModelIdCodec.Create(id, $"fixture-model-{index}");
            return new SharedProviderCatalogPublication(id, new($"sha256:{new string('a', 64)}"),
                index == 0 ? RemoteName : $"Shared model {index:000} · extended descriptive name 東京",
                SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible, model,
                [new(model, index == 0 ? "Reasoning model 東京" : $"Native model {index}", [SharedProviderCapability.Responses])],
                new(SharedProviderHealthState.Available));
        }).ToArray();
        Selected.Add(PublicationId);
        if (scenario == SharingScenario.Retired) {
            Selected.Clear();
        }
        if (scenario == SharingScenario.EmptySources) {
            Sources.Clear();
        }
    }

    public void SetPublished(bool published) {
        Published = published;
        PublicationToken = Guid.NewGuid();
        Writes++;
    }
    public SharedProviderImportBaseline SaveImport(SharedProviderImportSubmission submission) {
        if (submission.Baseline.ImportToken != Import.ImportToken || submission.Baseline.ProviderToken != Import.ProviderToken) {
            throw new InvalidOperationException("The fixture owner rejected stale local versions.");
        }
        Import = Import with { ImportToken = Guid.NewGuid(), ProviderToken = Guid.NewGuid(),
            Settings = submission.Settings with { LocalAlias = submission.Settings.LocalAlias.Trim() } };
        Writes++;
        return Import;
    }
    public void ChangeOtherOperator() => Import = Import with {
        ImportToken = Guid.NewGuid(), ProviderToken = Guid.NewGuid(), Settings = new("Operations model", false)
    };
    public void ChangeMetadata() {
        RemoteName = "Research model · new remote metadata";
        Import = Import with { ImportToken = Guid.NewGuid(), ProviderToken = Guid.NewGuid() };
    }
    public void RetireImport() {
        Selected.Remove(PublicationId);
        Import = Import with { ImportToken = Guid.NewGuid(), ProviderToken = Guid.NewGuid() };
        Writes++;
    }
    public ScenarioSource SaveSource(SharedProviderSourceSubmission submission) {
        var existing = Sources.GetValueOrDefault(submission.SourceId);
        if (existing?.Token != submission.ExpectedToken) {
            throw new InvalidOperationException("The fixture owner rejected a stale source version.");
        }
        var saved = new ScenarioSource(submission.SourceId, Guid.NewGuid(), submission.Values with {
            Name = submission.Values.Name.Trim(), BaseUri = new Uri(submission.Values.BaseUri.Trim()).AbsoluteUri
        });
        Sources[saved.Id] = saved;
        Writes++;
        return saved;
    }
    public void ChangeSource(SharedProviderSourceOrigin origin, bool delete) {
        if (!Sources.TryGetValue(origin.SourceId, out var source) || source.Token != origin.ConcurrencyToken) {
            throw new InvalidOperationException("The fixture source changed.");
        }
        if (delete) {
            Sources.Remove(source.Id);
        } else {
            Sources[source.Id] = source with { Token = Guid.NewGuid(), Values = source.Values with { IsEnabled = !source.Values.IsEnabled } };
        }
        Writes++;
    }
    public void Synchronize(SharedProviderSourceOrigin origin, IReadOnlySet<SharedProviderPublicationId> selected) {
        var source = Sources[origin.SourceId];
        if (source.Token != origin.ConcurrencyToken || !source.Values.IsEnabled) {
            throw new InvalidOperationException("The fixture source changed or is disabled.");
        }
        Sources[source.Id] = source with { Token = Guid.NewGuid() };
        Selected.Clear();
        Selected.UnionWith(selected);
        Writes++;
    }
}
