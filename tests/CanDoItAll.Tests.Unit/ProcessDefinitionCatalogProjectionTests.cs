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

    [Fact]
    public async Task Editor_projection_reads_authoring_sections_from_template_metadata()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        Assert.Equal("Architecture review", editor.Identity.Name);
        Assert.Equal("Architecture owner", editor.Identity.OwnerName);
        Assert.Equal(ProcessDefinitionCriticalityLevel.High, editor.Governance.Criticality);
        Assert.Equal(ProcessDefinitionAutonomyLevel.Guarded, editor.Governance.AutonomyLevel);
        Assert.Equal(ProcessDefinitionOperatingModeKind.GovernedLive, editor.Governance.OperatingMode);
        Assert.Equal("Manager override.", editor.Governance.ManagerOverrideSummary);
        Assert.Equal("Interface contract.", editor.Contracts.InterfaceContractSummary);
        Assert.Equal(1, editor.Simulation.StepCount);
        Assert.Equal(1, editor.Simulation.RequiredRoleCount);
        Assert.Equal(1, editor.Simulation.RequiredArtifactExpectationCount);
        Assert.False(editor.Lint.HasBlockingIssues);
    }

    [Fact]
    public async Task Publish_rejects_blocking_lint_and_returns_actionable_projection()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var invalidDraft = CreateDraft(editor) with
        {
            Identity = editor.Identity with { Name = string.Empty }
        };

        var result = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Publish,
            editor.VersionToken,
            invalidDraft));

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, result.Receipt.Status);
        Assert.Equal(ProcessDefinitionEditorCommandKind.Publish, result.Receipt.CommandKind);
        Assert.True(result.Projection.Lint.HasBlockingIssues);
        Assert.Contains(result.Projection.Lint.Issues, issue =>
            issue.Code == "processes.definition.identity.name-required" &&
            issue.Severity == ProcessDefinitionEditorLintSeverity.Error);
        Assert.Equal(string.Empty, result.Projection.Identity.Name);
    }

    [Fact]
    public async Task Save_publish_archive_and_delete_follow_typed_status_transitions()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var saved = await ExecuteEditorCommandAsync(service, editor, ProcessDefinitionEditorCommandKind.SaveDraft);
        var published = await ExecuteEditorCommandAsync(service, saved.Projection, ProcessDefinitionEditorCommandKind.Publish);
        var archived = await ExecuteEditorCommandAsync(service, published.Projection, ProcessDefinitionEditorCommandKind.Archive);
        var deleted = await ExecuteEditorCommandAsync(service, archived.Projection, ProcessDefinitionEditorCommandKind.Delete);

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, saved.Receipt.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, saved.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Published, published.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Archived, archived.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.TemplateDefault, deleted.Projection.Status);
        Assert.Contains("template default remains available", deleted.Receipt.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Archive_and_delete_reject_stale_version_tokens()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var saved = await ExecuteEditorCommandAsync(service, editor, ProcessDefinitionEditorCommandKind.SaveDraft);
        var staleDraft = CreateDraft(saved.Projection);

        var archived = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            saved.Projection.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Archive,
            editor.VersionToken,
            staleDraft));
        var deleted = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            saved.Projection.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Delete,
            editor.VersionToken,
            staleDraft));

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, archived.Receipt.Status);
        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, deleted.Receipt.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, archived.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, deleted.Projection.Status);
        Assert.Contains(archived.Projection.Lint.Issues, issue => issue.Code == "processes.definition.version-conflict");
        Assert.Contains(deleted.Projection.Lint.Issues, issue => issue.Code == "processes.definition.version-conflict");
    }

    private static Task<ProcessDefinitionEditorCommandResult> ExecuteEditorCommandAsync(
        ProcessDefinitionEditorProjectionService service,
        ProcessDefinitionEditorProjection editor,
        ProcessDefinitionEditorCommandKind commandKind)
        => service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            commandKind,
            editor.VersionToken,
            CreateDraft(editor)));

    private static ProcessDefinitionEditorDraftProjection CreateDraft(
        ProcessDefinitionEditorProjection editor)
        => new(
            editor.DefinitionKey,
            editor.Identity,
            editor.Governance,
            editor.Contracts,
            editor.Simulation);

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
                      "ValueStatement": "Template value statement.",
                      "CustomerName": "Architecture customer",
                      "OwnerName": "Architecture owner",
                      "InterfaceContractSummary": "Interface contract.",
                      "ManagerOverrideSummary": "Manager override.",
                      "GovernanceNotes": "Governance notes.",
                      "ChangeSummary": "Change summary.",
                      "GovernancePolicySummary": "Governance policy.",
                      "ConstitutionRuleSummary": "Constitution rule.",
                      "OperatingModeSummary": "Operating mode summary.",
                      "SimulationReadinessSummary": "Simulation readiness.",
                      "Criticality": "High",
                      "OperatingMode": "GovernedLive",
                      "AutonomyLevel": "Guarded",
                      "RoleUsages": [
                        {
                          "IsRequired": true
                        }
                      ],
                      "Steps": [
                        {
                          "ArtifactExpectations": [
                            {
                              "IsRequired": true
                            }
                          ]
                        }
                      ]
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
