using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDefinitionCatalogProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Catalog_query_filters_definitions_and_selects_requested_item()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var selectedKey = new ProcessDefinitionCatalogItemKey("architecture-review");

        var catalog = await service.GetCatalogAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogQueryProjection("architecture", selectedKey, ProcessDefinitionCatalogScopeKind.All, Take: 20));

        var item = Assert.Single(catalog.Items);
        Assert.Equal(selectedKey, item.Key);
        Assert.Equal(selectedKey, catalog.SelectedDefinitionKey);
        Assert.Equal("Architecture review", catalog.SelectedItem?.Name);
        Assert.Equal(2, catalog.PublishedDefinitionCount);
        Assert.Contains(catalog.ScopeGroups, group => group.ScopeKind == ProcessDefinitionCatalogScopeKind.Global && group.Count == 2);
    }

    [Fact]
    public async Task Feed_defaults_returns_command_receipt_and_refresh_token()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var receipt = await service.FeedDefaultDefinitionsAsync(
            new ProcessDefinitionFeedDefaultsCommand(ProcessWorkspaceShellScope.Global));

        Assert.Equal(ProcessDefinitionCatalogCommandStatus.Accepted, receipt.Status);
        Assert.Equal(ProcessDefinitionCatalogCommandKind.FeedDefaults, receipt.CommandKind);
        Assert.Equal(2, receipt.AffectedDefinitionCount);
        Assert.StartsWith("feed-defaults:test-pack:", receipt.RefreshToken.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Loader_rejects_manifest_definition_key_mismatch()
    {
        using var pack = TemporaryProcessTemplatePack.Create(
            ("manifest-key", "definition-key", "Mismatched definition", "Mismatch summary"));
        var loader = new ProcessTemplatePackLoader(pack.RootPath);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("does not match manifest key", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TemporaryProcessTemplatePack : IDisposable
    {
        private TemporaryProcessTemplatePack(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static TemporaryProcessTemplatePack CreateDefault()
            => Create(
                ("delivery-default", "delivery-default", "Delivery default", "Default delivery flow"),
                ("architecture-review", "architecture-review", "Architecture review", "Architecture governance flow"));

        public static TemporaryProcessTemplatePack Create(
            params (string ManifestKey, string DefinitionKey, string DisplayName, string Summary)[] definitions)
        {
            var root = Directory.CreateTempSubdirectory("process-template-pack-").FullName;
            var processes = definitions
                .Select(definition => $$"""
                    {
                      "Key": "{{definition.ManifestKey}}",
                      "RelativePath": "processes/{{definition.ManifestKey}}"
                    }
                    """)
                .ToArray();
            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                $$"""
                {
                  "PackKey": "test-pack",
                  "Name": "Test process template pack",
                  "Version": "test-pack",
                  "GeneratedAtUtc": "2026-06-16T00:00:00Z",
                  "Processes": [
                    {{string.Join("," + Environment.NewLine, processes)}}
                  ]
                }
                """);

            foreach (var definition in definitions)
            {
                var directory = Path.Combine(root, "processes", definition.ManifestKey);
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    Path.Combine(directory, "definition.json"),
                    $$"""
                    {
                      "Kind": "process-template-definition",
                      "Key": "{{definition.DefinitionKey}}",
                      "DisplayName": "{{definition.DisplayName}}",
                      "Summary": "{{definition.Summary}}",
                      "Criticality": "High",
                      "OperatingMode": "GovernedLive",
                      "AutonomyLevel": "Guarded"
                    }
                    """);
            }

            return new TemporaryProcessTemplatePack(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
