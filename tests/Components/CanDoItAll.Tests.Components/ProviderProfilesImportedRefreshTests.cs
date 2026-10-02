using CanDoItAll.AgentFramework.SharedProviders.UI;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using CanDoItAll.SharedProviders.Abstractions;
using Management = CanDoItAll.Modules.AgentFramework.ProviderManagement;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderProfilesImportedRefreshTests {
    [Fact]
    public async Task Remote_metadata_refresh_keeps_the_separate_dirty_sharing_alias() {
        var reads = new Reads(imported: true);
        var publication = new SharedProviderPublicationId(Guid.NewGuid());
        var import = new Management.SharedProviderImportedProfileSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Central", publication,
            reads.Profile.Id, "Saved alias", true, "Remote V0", SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible,
            SharedProviderRoutingModelIdCodec.Create(publication, "alpha"), Management.SharedProviderSelectionState.Selected,
            Management.SharedProviderAvailabilityState.Available, [], Guid.NewGuid(), Guid.NewGuid());
        var sharing = DispatchProxy.Create<Management.ISharedProviderManagementService, SharedProviderLifetimeRegressionTests.SharingProxy>();
        ((SharedProviderLifetimeRegressionTests.SharingProxy)(object)sharing).Read = (id, _) => Task.FromResult(
            new Management.SharedProviderProfileSharingSnapshot(id, Management.SharedProviderProfileOwnership.Imported, null, null, import));
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProviderProfilesReads>(reads);
            services.AddSingleton(sharing);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        await Tab(cut, ProviderEditorSection.Sharing);
        var child = cut.FindComponent<SharedProviderImportedProfileContent>().Instance;
        cut.Find("[data-testid='shared-provider-import-alias']").Input("Unsubmitted alias");
        reads.Advance();
        import = import with { RemoteDisplayName = "Remote V1", ImportConcurrencyToken = Guid.NewGuid(), ProviderConcurrencyToken = Guid.NewGuid() };

        await cut.Find("[data-testid='providers-refresh']").ClickAsync();

        Assert.Equal("Unsubmitted alias", ((IHtmlInputElement)cut.Find("[data-testid='shared-provider-import-alias']")).Value);
        Assert.Same(child, cut.FindComponent<SharedProviderImportedProfileContent>().Instance);
        Assert.Contains("Remote V1", cut.Find("[data-testid='shared-provider-management']").TextContent);
    }

    [Theory]
    [InlineData(false, true, "Local model")]
    [InlineData(true, true, "Source model 東京")]
    [InlineData(true, false, "Unavailable shared model")]
    public async Task Actual_tree_description_uses_model_name_without_changing_identity(bool imported, bool metadata, string label) {
        var reads = new Reads(imported);
        reads.Profile = reads.Profile with {
            DefaultModel = imported ? Reads.ModelA : label,
            ModelCatalog = metadata ? [new(imported ? Reads.ModelA : label, label)] : []
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        var node = cut.Find("[data-testid='providers-tree-provider']");
        Assert.Contains(label, node.GetAttribute("aria-description"));
        Assert.Contains(reads.Profile.Id.ToString("N"), node.Id);
        if (imported) {
            Assert.DoesNotContain(Reads.ModelA, node.GetAttribute("aria-description"));
            Assert.Equal(Reads.ModelA, cut.Instance.Editor.Model.DefaultModel);
        }
        reads.Profile = reads.Profile with { Name = "Renamed provider", ModelCatalog = metadata ? [new(reads.Profile.DefaultModel, "Renamed model")] : [] };
        await cut.Find("[data-testid='providers-refresh']").ClickAsync();
        Assert.Contains("Renamed provider", cut.Find("[data-testid='providers-tree-provider']").GetAttribute("aria-description"));
        if (metadata && imported) {
            Assert.Contains("Renamed model", cut.Find("[data-testid='providers-tree-provider']").GetAttribute("aria-description"));
        }
    }

    [Fact]
    public async Task Toolbar_refresh_adopts_complete_imported_revision_and_unchanged_refresh_is_noop() {
        var reads = new Reads(imported: true);
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        var old = cut.Instance.Editor.Context;
        Assert.Equal("Alpha", ((IHtmlInputElement)cut.Find("[data-testid='providers-model-input']")).Value);
        reads.Advance();

        await cut.Find("[data-testid='providers-refresh']").ClickAsync();

        Assert.Equal(Reads.ModelC, cut.Instance.Editor.Model.DefaultModel);
        Assert.Equal("Gamma", ((IHtmlInputElement)cut.Find("[data-testid='providers-model-input']")).Value);
        Assert.NotSame(old, cut.Instance.Editor.Context);
        Assert.Equal(reads.Revision, cut.Instance.Editor.Model.ExpectedConcurrencyToken);
        Assert.Equal([Reads.ModelB, Reads.ModelC], cut.Instance.Editor.Model.SuggestedModels);
        Assert.False(cut.Instance.Editor.Model.SupportsTools);
        await Tab(cut, ProviderEditorSection.Runtime);
        Assert.Equal("Beta\nGamma", ((IHtmlTextAreaElement)cut.Find("[data-testid='providers-suggested-models']")).Value);
        Assert.Equal("{\"revision\":1}", ((IHtmlTextAreaElement)cut.Find("[data-testid='providers-config-json']")).Value);
        await Tab(cut, ProviderEditorSection.Prices);
        Assert.Equal(9m, cut.Instance.Editor.Model.ModelPrices[0].InputPerMillionTokensUsd);
        Assert.Equal("Gamma", ((IHtmlInputElement)cut.Find("[data-testid='provider-pricing-model-0']")).Value);
        Assert.DoesNotContain("Alpha", cut.Find("[data-testid='provider-pricing-table']").TextContent);
        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
        await Tab(cut, ProviderEditorSection.Thinking);
        Assert.Contains("Gamma", cut.Find("[data-testid='provider-thinking-table']").TextContent);
        Assert.DoesNotContain("Alpha", cut.Find("[data-testid='provider-thinking-table']").TextContent);
        Assert.Empty(cut.FindAll("button[aria-label^='Edit thinking for ']"));
        var adopted = cut.Instance.Editor.Context;
        var count = reads.EditorReads;
        await cut.Find("[data-testid='providers-refresh']").ClickAsync();
        await cut.Find("[data-testid='providers-tree-provider']").ClickAsync();
        Assert.Same(adopted, cut.Instance.Editor.Context);
        Assert.Equal(count, reads.EditorReads);
    }

    [Fact]
    public async Task Local_dirty_context_invalid_json_and_price_text_survive_toolbar_refresh() {
        var reads = new Reads(imported: false);
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        var context = cut.Instance.Editor.Context;
        cut.Find("[data-testid='providers-name-input']").Input("Unsaved local name");
        await Tab(cut, ProviderEditorSection.Prices);
        var row = cut.Instance.Editor.Model.ModelPrices[0];
        cut.Find("[data-testid='provider-pricing-input-0']").Input("1e-");
        await Tab(cut, ProviderEditorSection.Runtime);
        cut.Find("[data-testid='providers-config-json']").Input("{unfinished");
        reads.Advance();
        await cut.Find("[data-testid='providers-refresh']").ClickAsync();
        Assert.Same(context, cut.Instance.Editor.Context);
        Assert.Same(row, cut.Instance.Editor.Model.ModelPrices[0]);
        Assert.Equal("Unsaved local name", cut.Instance.Editor.Model.Name);
        Assert.Equal("{unfinished", ((IHtmlTextAreaElement)cut.Find("[data-testid='providers-config-json']")).Value);
        await Tab(cut, ProviderEditorSection.Prices);
        Assert.Equal("1e-", ((IHtmlInputElement)cut.Find("[data-testid='provider-pricing-input-0']")).Value);
        Assert.Equal(1, reads.EditorReads);
    }

    private static Task Tab(IRenderedComponent<AgentProviderProfilesPanel> cut, ProviderEditorSection section) =>
        cut.FindAll("[role='tab']")[ProviderEditorSections.IndexOf(section)].ClickAsync();

    private sealed class Reads : IProviderProfilesReads {
        public const string ModelA = "sp1.fixture-alpha";
        public const string ModelB = "sp1.fixture-beta";
        public const string ModelC = "sp1.fixture-gamma";
        public ProviderProfile Profile { get; set; }
        public Guid Revision { get; private set; } = Guid.NewGuid();
        public int EditorReads { get; private set; }

        public Reads(bool imported) {
            Profile = new(Guid.NewGuid(), "Source profile", ProviderKind.OpenAi, string.Empty, string.Empty,
                ModelA, ProviderTransportKind.ChatCompletions, true, true, true, false, false, "{}", string.Empty, "Available", null, [ModelA, ModelB]) {
                ConnectorPluginKey = imported ? ProviderConnectorKeys.SharedImport : ProviderConnectorKeys.OpenAi,
                CredentialBinding = imported ? new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken, ProviderCredentialConsumerKind.Source, Guid.NewGuid()) : null,
                ModelCatalog = [new(ModelA, "Alpha"), new(ModelB, "Beta")],
                ModelPrices = [new(ModelA, 1m, 0m, 2m)]
            };
        }

        public void Advance() {
            Revision = Guid.NewGuid();
            Profile = Profile with {
                DefaultModel = ModelC, SuggestedModels = [ModelB, ModelC], ModelCatalog = [new(ModelB, "Beta"), new(ModelC, "Gamma")],
                ConfigurationJson = "{\"revision\":1}", SupportsTools = false, ModelPrices = [new(ModelC, 9m, 0m, 12m)]
            };
        }

        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProviderProfilesCatalog([Profile], new([])) {
            Revisions = new Dictionary<Guid, CanDoItAll.AgentFramework.Core.ProviderConfigurationRevision> { [Profile.Id] = new(Revision) }
        });

        public Task<ProviderProfileEditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default) {
            EditorReads++;
            return Task.FromResult(new ProviderProfileEditorModel {
                Id = providerId, ExpectedConcurrencyToken = Revision, Name = Profile.Name, Kind = Profile.Kind, DefaultModel = Profile.DefaultModel,
                SuggestedModels = [.. Profile.SuggestedModels], Transport = Profile.Transport, ConfigurationJson = Profile.ConfigurationJson,
                SupportsTools = Profile.SupportsTools, SupportsStreaming = Profile.SupportsStreaming, IsEnabled = Profile.IsEnabled,
                ModelPrices = Profile.ModelPrices.Select(price => new ProviderModelTokenPriceEditorModel {
                    Model = price.Model, InputPerMillionTokensUsd = price.InputPerMillionTokensUsd,
                    CachedInputPerMillionTokensUsd = price.CachedInputPerMillionTokensUsd, OutputPerMillionTokensUsd = price.OutputPerMillionTokensUsd
                }).ToList()
            });
        }
    }
}
