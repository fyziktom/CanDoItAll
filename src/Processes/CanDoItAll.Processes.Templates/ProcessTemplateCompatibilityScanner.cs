using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public static partial class ProcessTemplateCompatibilityScanner
{
    public const string LegacyCurrentModuleSchemaVersion = "process-definition/current-module-legacy";

    public static async Task<ProcessTemplateCompatibilityReport> AnalyzeAsync(
        ProcessTemplateCompatibilityScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TemplatePackRoot))
        {
            throw new ArgumentException("Template pack root is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.TargetSchemaVersion))
        {
            throw new ArgumentException("Target schema version is required.", nameof(request));
        }

        var root = Path.GetFullPath(request.TemplatePackRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Template pack root '{root}' does not exist.");
        }

        var manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Template pack manifest was not found.", manifestPath);
        }

        var processEntries = await ReadProcessEntriesAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var migrationItems = new List<ProcessTemplateMigrationDryRunItem>(processEntries.Count);
        var sidecars = new List<ProcessTemplateSidecarDrift>();
        var branchDiagnostics = new List<ProcessBranchMigrationDiagnostic>();
        var outcomeCount = 0;

        foreach (var entry in processEntries.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var processRoot = Path.GetFullPath(Path.Combine(root, entry.RelativePath));
            var definitionPath = Path.Combine(processRoot, "definition.json");
            if (!File.Exists(definitionPath))
            {
                migrationItems.Add(new ProcessTemplateMigrationDryRunItem(
                    entry.Key,
                    NormalizeRelative(root, definitionPath),
                    LegacyCurrentModuleSchemaVersion,
                    request.TargetSchemaVersion,
                    ProcessTemplateMigrationDryRunStatus.ManualReviewRequired,
                    [],
                    "TemplateCompatibility.MissingDefinition",
                    "Process manifest entry does not have a definition.json file."));
                sidecars.Add(new ProcessTemplateSidecarDrift(
                    entry.Key,
                    NormalizeRelative(root, definitionPath),
                    ProcessTemplateProjectionKind.ImportEnvelope,
                    ProcessTemplateSidecarDriftStatus.MissingCanonicalJson,
                    null,
                    null,
                    "Canonical definition JSON is missing."));
                continue;
            }

            using var definition = await ReadJsonDocumentAsync(definitionPath, cancellationToken).ConfigureAwait(false);
            var sourceHash = ProcessTemplateContentHasher.ComputeCanonicalHash(definition.RootElement);
            migrationItems.Add(CreateMigrationItem(
                request,
                entry.Key,
                NormalizeRelative(root, definitionPath),
                definition.RootElement));

            sidecars.AddRange(await AnalyzeSidecarsAsync(
                    root,
                    entry.Key,
                    processRoot,
                    sourceHash,
                    cancellationToken)
                .ConfigureAwait(false));

            var diagnostics = AnalyzeBranchOutcomes(entry.Key, definition.RootElement);
            outcomeCount += diagnostics.OutcomeCount;
            branchDiagnostics.AddRange(diagnostics.Diagnostics);
        }

        var sidecarCount = sidecars.Count;
        return new ProcessTemplateCompatibilityReport(
            root,
            request.ObservedAtUtc,
            new ProcessTemplateMigrationDryRunReport(
                processEntries.Count,
                migrationItems.Count(item => item.Status != ProcessTemplateMigrationDryRunStatus.ManualReviewRequired || item.ErrorCode != "TemplateCompatibility.MissingDefinition"),
                sidecarCount,
                false,
                migrationItems),
            new ProcessTemplateSidecarDriftReport(sidecarCount, sidecars),
            new ProcessBranchMigrationDiagnosticReport(outcomeCount, branchDiagnostics));
    }

    private static ProcessTemplateMigrationDryRunItem CreateMigrationItem(
        ProcessTemplateCompatibilityScanRequest request,
        string processKey,
        string relativeDefinitionPath,
        JsonElement definition)
    {
        var sourceSchema = TryGetString(definition, "schemaVersion", out var schemaVersion)
            ? schemaVersion
            : LegacyCurrentModuleSchemaVersion;

        var plan = request.MigrationRegistry.CreatePlan(sourceSchema, request.TargetSchemaVersion);
        if (!plan.Succeeded)
        {
            return new ProcessTemplateMigrationDryRunItem(
                processKey,
                relativeDefinitionPath,
                sourceSchema,
                request.TargetSchemaVersion,
                ProcessTemplateMigrationDryRunStatus.MigrationPlanFailed,
                [],
                plan.ErrorCode,
                plan.ErrorMessage);
        }

        if (plan.Migrations.Count == 0)
        {
            return new ProcessTemplateMigrationDryRunItem(
                processKey,
                relativeDefinitionPath,
                sourceSchema,
                request.TargetSchemaVersion,
                ProcessTemplateMigrationDryRunStatus.Compatible,
                [],
                null,
                null);
        }

        return new ProcessTemplateMigrationDryRunItem(
            processKey,
            relativeDefinitionPath,
            sourceSchema,
            request.TargetSchemaVersion,
            ProcessTemplateMigrationDryRunStatus.MigrationPlanned,
            plan.Migrations.Select(migration => migration.MigrationId).ToArray(),
            null,
            null);
    }

}
