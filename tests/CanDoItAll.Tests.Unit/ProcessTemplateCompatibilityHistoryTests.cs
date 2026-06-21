using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessTemplateCompatibilityHistoryTests
{
    [Fact]
    public async Task Template_compatibility_scan_reports_dry_run_sidecar_drift_and_branch_diagnostics()
    {
        var root = CreateTemplatePackRoot();
        await File.WriteAllTextAsync(
            Path.Combine(root, "manifest.json"),
            """
            {
              "processes": [
                {
                  "key": "decision-flow",
                  "relativePath": "processes/decision-flow"
                }
              ]
            }
            """);

        var processRoot = Path.Combine(root, "processes", "decision-flow");
        Directory.CreateDirectory(Path.Combine(processRoot, "projection"));
        await File.WriteAllTextAsync(
            Path.Combine(processRoot, "definition.json"),
            """
            {
              "kind": "process-template",
              "key": "decision-flow",
              "steps": [
                {
                  "key": "approval",
                  "branchOutcomes": [
                    {
                      "key": "approved",
                      "title": "Approved"
                    }
                  ]
                }
              ]
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(processRoot, "definition.md"),
            "# Generated sidecar");
        await File.WriteAllTextAsync(
            Path.Combine(processRoot, "projection", string.Concat("current-module", ".compatibility-report", ".json")),
            """
            {
              "schemaVersion": "process-template-projection-metadata/1.0",
              "projectionKind": "compatibilityReport",
              "sourceJsonHash": "sha256:stale",
              "generatorVersion": "legacy",
              "generatedAtUtc": "2026-06-15T00:00:00Z"
            }
            """);

        var registry = new ProcessTemplateMigrationRegistry(
            [ProcessTemplateCompatibilityScanner.LegacyCurrentModuleSchemaVersion, ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1],
            [new IdentityTemplateMigration(
                "legacy-current-module-to-v1",
                ProcessTemplateCompatibilityScanner.LegacyCurrentModuleSchemaVersion,
                ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1)]);
        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(
            new ProcessTemplateCompatibilityScanRequest(
                root,
                ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1,
                registry,
                new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(1, report.MigrationDryRun.ProcessCount);
        Assert.Equal(1, report.MigrationDryRun.CanonicalJsonCount);
        Assert.False(report.MigrationDryRun.WouldMutateFiles);
        Assert.Contains(report.SidecarDrift.Sidecars, sidecar => sidecar.Status == ProcessTemplateSidecarDriftStatus.SourceHashMismatch);
        var diagnostic = Assert.Single(report.BranchDiagnostics.Diagnostics);
        Assert.Equal(ProcessBranchMigrationDiagnosticKind.AmbiguousRouteTarget, diagnostic.Kind);
        Assert.True(report.RequiresManualReview);
    }

    [Fact]
    public void Legacy_history_projection_adapter_labels_runs_readonly_and_denies_actions()
    {
        var adapter = new LegacyProcessHistoryProjectionAdapter();
        var legacyRunId = new LegacyProcessRunId("legacy-run-1");
        var records = new[]
        {
            new LegacyProcessRuntimeHistoryRecord(
                legacyRunId,
                LegacyProcessRuntimeRecordKind.Run,
                "ProcessRun",
                new DateTimeOffset(2026, 6, 14, 10, 0, 0, TimeSpan.Zero),
                ["governanceSnapshot"],
                ProcessProjectedSensitivity.Restricted),
            new LegacyProcessRuntimeHistoryRecord(
                legacyRunId,
                LegacyProcessRuntimeRecordKind.StepRun,
                string.Concat("Process", "StepRun"),
                new DateTimeOffset(2026, 6, 14, 10, 5, 0, TimeSpan.Zero),
                [],
                ProcessProjectedSensitivity.Normal)
        };

        var inventory = adapter.Inventory(records);
        var projection = Assert.Single(adapter.ProjectReadOnlyRuns(records));
        var denial = adapter.DenyRuntimeAction(
            legacyRunId,
            "restart-run",
            "legacy-history://ProcessRun/legacy-run-1");

        Assert.Equal(2, inventory.TotalRecordCount);
        Assert.Equal(2, projection.RecordCount);
        Assert.True(projection.IsReadOnly);
        Assert.Equal(ProcessProjectedSensitivity.Restricted, projection.Sensitivity);
        Assert.Equal(LegacyProcessHistoryActionDenialReason.ReadOnlyLegacyHistory, denial.Reason);
        Assert.Contains("read-only", denial.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compatibility_decision_selects_readonly_legacy_projection_plus_archive_by_default()
    {
        var root = CreateTemplatePackRoot();
        await File.WriteAllTextAsync(
            Path.Combine(root, "manifest.json"),
            """
            {
              "processes": []
            }
            """);

        var scannerReport = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(
            new ProcessTemplateCompatibilityScanRequest(
                root,
                ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1,
                new ProcessTemplateMigrationRegistry([ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1], []),
                new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)));
        var historyAdapter = new LegacyProcessHistoryProjectionAdapter();
        var historyReport = historyAdapter.Inventory(
            [
                new LegacyProcessRuntimeHistoryRecord(
                    new LegacyProcessRunId("legacy-run-2"),
                    LegacyProcessRuntimeRecordKind.Run,
                    "ProcessRun",
                    null,
                    [],
                    ProcessProjectedSensitivity.Normal)
            ]);

        var service = new ProcessCompatibilityDecisionService();
        var decision = service.Decide(
            new ProcessCompatibilityDecisionRequest(
                scannerReport,
                historyReport,
                ProductOwnerApprovedDeletion: false,
                FullMigrationRequired: false,
                SignoffOwner: "process-governance"));

        Assert.Equal(ProcessRuntimeHistoryCompatibilityOption.ReadOnlyLegacyProjectionPlusArchive, decision.SelectedOption);
        Assert.False(decision.AllowsRuntimeActionsOnLegacyRuns);
        Assert.Contains("process-governance", decision.RequiredSignoffOwners);
    }

    [Fact]
    public async Task Template_compatibility_scan_rejects_invalid_manifest_entries()
    {
        var root = CreateTemplatePackRoot();
        await File.WriteAllTextAsync(
            Path.Combine(root, "manifest.json"),
            """
            {
              "processes": [
                {
                  "key": "missing-path"
                }
              ]
            }
            """);

        var request = new ProcessTemplateCompatibilityScanRequest(
            root,
            ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1,
            new ProcessTemplateMigrationRegistry([ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1], []),
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ProcessTemplateCompatibilityScanner.AnalyzeAsync(request));

        Assert.Contains("key and relativePath", exception.Message, StringComparison.Ordinal);
    }

    private static string CreateTemplatePackRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "candoitall-template-compatibility", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class IdentityTemplateMigration : IProcessTemplateMigration
    {
        public IdentityTemplateMigration(string migrationId, string fromSchemaVersion, string toSchemaVersion)
        {
            MigrationId = migrationId;
            FromSchemaVersion = fromSchemaVersion;
            ToSchemaVersion = toSchemaVersion;
        }

        public string MigrationId { get; }

        public string FromSchemaVersion { get; }

        public string ToSchemaVersion { get; }

        public JsonDocument Migrate(JsonDocument source)
        {
            return JsonDocument.Parse(source.RootElement.GetRawText());
        }
    }
}
