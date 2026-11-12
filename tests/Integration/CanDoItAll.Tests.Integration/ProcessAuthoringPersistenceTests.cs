using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringPersistenceTests {
    [Fact]
    public async Task Accepted_definition_survives_independent_native_scope_and_catalog_read() {
        await using var app = await TestApplication.CreateAsync(new());
        ProcessWorkspaceShellRequest request = new(ProcessWorkspaceShellScope.Global, new(null, null, null),
            new(null, null, ProcessDefinitionCatalogScopeKind.All, 50),
            new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 50), false);
        var marker = "Durable definition " + Guid.NewGuid().ToString("N");
        int definitionCount;
        await using (var scope = app.Services.CreateAsyncScope()) {
            var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
            var baseline = await client.GetShellAsync(request);
            definitionCount = baseline.DefinitionCatalog.Items.Count;
            var editor = baseline.DefinitionCatalog.SelectedEditor!;
            request = request with { DefinitionCatalogQuery = request.DefinitionCatalogQuery with { SelectedDefinitionKey = editor.DefinitionKey } };
            var draft = new ProcessDefinitionEditorDraftProjection(editor.DefinitionKey,
                editor.Identity with { Name = marker }, editor.Governance, editor.Contracts, editor.Simulation);
            var saved = await client.ExecuteDefinitionEditorCommandAsync(new(request.Scope, editor.DefinitionKey,
                ProcessDefinitionEditorCommandKind.SaveDraft, editor.VersionToken, draft));
            Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, saved.Receipt.Status);
        }
        await using var independent = app.Services.CreateAsyncScope();
        var observed = await independent.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>().GetShellAsync(request);
        Assert.Equal(marker, observed.DefinitionCatalog.SelectedEditor!.Identity.Name);
        Assert.Equal(marker, observed.DefinitionCatalog.SelectedItem!.Name);
        Assert.Equal(1, observed.DefinitionCatalog.DraftDefinitionCount);
        Assert.Equal(definitionCount, observed.DefinitionCatalog.PublishedDefinitionCount);
        Assert.Equal(definitionCount, observed.DefinitionCatalog.TotalDefinitionCount);
        Assert.Equal(definitionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            observed.Tabs.Single(tab => tab.Key == ProcessWorkspaceTabKey.Definitions).CountText);
    }
}
