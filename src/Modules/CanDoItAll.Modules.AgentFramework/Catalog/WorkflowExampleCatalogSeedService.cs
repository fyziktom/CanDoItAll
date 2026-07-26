using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using DocumentCellWrite = CanDoItAll.Tools.Documents.SpreadsheetCellWrite;
using DocumentRangeWrite = CanDoItAll.Tools.Documents.SpreadsheetRangeWrite;
using DocumentWriteRequest = CanDoItAll.Tools.Documents.SpreadsheetWriteRequest;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowExampleCatalogSeedOptions
{
    public const string SectionName = "Workflows:ExampleSeed";

    public bool Enabled { get; set; }

    public bool SeedSampleWorkspaceFiles { get; set; } = true;
}

public sealed class WorkflowExampleCatalogSeedService(
    IWorkflowCatalogService catalogService,
    IWorkflowComponentLibraryService componentLibrary,
    IWorkflowSettingsService settingsService,
    IWorkspaceFileService workspaceFiles,
    IWorkspacePathResolutionService workspacePaths,
    ISpreadsheetDocumentService spreadsheets,
    IOptions<WorkflowExampleCatalogSeedOptions> options,
    ILogger<WorkflowExampleCatalogSeedService> logger,
    WorkflowTemplatePackLoader? templatePackLoader = null)
{
    private readonly WorkflowTemplatePackLoader templatePackLoader = templatePackLoader ?? new WorkflowTemplatePackLoader();

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        if (options.Value.SeedSampleWorkspaceFiles)
        {
            SeedWorkspaceAssets();
        }

        await EnsureWorkflowSettingsAsync(cancellationToken);
        var templatePack = this.templatePackLoader.Load();
        var provider = await ResolveProviderOptionAsync(cancellationToken);
        var existingComponents = (await componentLibrary.ListComponentsAsync(cancellationToken)).ToList();
        var existingDefinitions = (await catalogService.ListDefinitionsAsync(cancellationToken)).ToList();
        var seededCount = 0;

        foreach (var template in templatePack.Workflows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var component = await EnsureComponentAsync(templatePack, template, provider, existingComponents, cancellationToken);
            var graph = templatePack.CreateGraph(template, component.Id);
            var inputParameters = templatePack.CreateInputParameters(template);
            var definitionName = $"{templatePack.Manifest.DefinitionNamePrefix}{template.Name}";
            var description = $"{template.Description} {templatePack.Manifest.SeedMarker}: {templatePack.Manifest.SeedVersion}.";
            var provenance = CreateTemplateProvenance(templatePack, template);
            var stableMatches = existingDefinitions
                .Where(item => string.Equals(
                    item.TemplateKey,
                    provenance.TemplateKey,
                    StringComparison.Ordinal))
                .ToArray();
            if (stableMatches.Length > 1)
            {
                logger.LogWarning(
                    "Skipping workflow example seed template '{TemplateKey}' because {MaterializationCount} catalog definitions claim the same stable key.",
                    provenance.TemplateKey,
                    stableMatches.Length);
                continue;
            }

            var existing = stableMatches.SingleOrDefault() ??
                           existingDefinitions.FirstOrDefault(item =>
                               string.IsNullOrWhiteSpace(item.TemplateKey) &&
                               string.Equals(
                                   item.Name,
                                   definitionName,
                                   StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                var detail = await catalogService.GetDefinitionAsync(existing.Id, existing.VersionId, cancellationToken);
                if (detail is not null &&
                    HasExactTemplateProvenance(detail.Definition, provenance) &&
                    detail.Definition.Description.Contains(templatePack.Manifest.SeedMarker, StringComparison.OrdinalIgnoreCase) &&
                    detail.Definition.Description.Contains(templatePack.Manifest.SeedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (detail is not null &&
                    !detail.Definition.Description.Contains(templatePack.Manifest.SeedMarker, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Skipping workflow example seed '{WorkflowName}' because a non-managed definition with that name already exists.",
                        definitionName);
                    continue;
                }

                if (!await TrySaveDefinitionAsync(
                    existing.Id,
                    existing.VersionId,
                    definitionName,
                    description,
                    graph,
                    inputParameters,
                    templatePack.RuntimePolicy,
                    provenance,
                    cancellationToken))
                {
                    continue;
                }
            }
            else
            {
                if (!await TrySaveDefinitionAsync(
                    null,
                    null,
                    definitionName,
                    description,
                    graph,
                    inputParameters,
                    templatePack.RuntimePolicy,
                    provenance,
                    cancellationToken))
                {
                    continue;
                }
            }

            seededCount++;
        }

        if (seededCount > 0)
        {
            logger.LogInformation(
                "Seeded or refreshed {WorkflowCount} workflow examples with seed version {SeedVersion}.",
                seededCount,
                templatePack.Manifest.SeedVersion);
        }
    }

    private async Task EnsureWorkflowSettingsAsync(CancellationToken cancellationToken)
    {
        var current = await settingsService.GetSettingsAsync(cancellationToken);
        if (current != WorkflowSettings.Default)
        {
            return;
        }

        await settingsService.SaveSettingsAsync(
            new WorkflowSettings(
                new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.InProcess,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: false,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false),
                new WorkflowArtifactPolicy(
                    CaptureNodeOutputs: true,
                    MaxInlinePayloadCharacters: 128_000,
                    AllowedArtifactKinds:
                    [
                        WorkflowArtifactKind.Text,
                        WorkflowArtifactKind.Json,
                        WorkflowArtifactKind.File,
                        WorkflowArtifactKind.Image,
                        WorkflowArtifactKind.ToolReceipt,
                        WorkflowArtifactKind.PreviewSimulation
                    ]),
                new WorkflowHumanInLoopPolicy(
                    AllowHumanInputNodes: true,
                    RequireApprovalForToolUse: true,
                    DefaultRequestTimeoutMinutes: 240)),
            cancellationToken);
    }

    private async Task<WorkflowProviderOption?> ResolveProviderOptionAsync(CancellationToken cancellationToken)
    {
        var providers = await componentLibrary.ListProviderOptionsAsync(cancellationToken);
        return providers.FirstOrDefault(provider =>
                   provider.IsEnabled &&
                   provider.SupportsStructuredOutput &&
                   provider.ModelOptions.Contains(ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparer.OrdinalIgnoreCase)) ??
               providers.FirstOrDefault(provider => provider.IsEnabled && provider.SupportsStructuredOutput);
    }

    private async Task<LlmCallComponent> EnsureComponentAsync(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template,
        WorkflowProviderOption? provider,
        List<LlmCallComponent> existingComponents,
        CancellationToken cancellationToken)
    {
        var componentName = $"{templatePack.Manifest.ComponentNamePrefix}{template.Name}";
        var current = existingComponents.FirstOrDefault(item => string.Equals(item.Name, componentName, StringComparison.OrdinalIgnoreCase));
        var component = await componentLibrary.SaveComponentAsync(
            new LlmCallComponentSaveRequest(
                current?.Id,
                componentName,
                provider?.ProviderProfileId,
                ResolveModel(provider),
                WorkflowModality.Text,
                templatePack.CreateModelSettings(),
                templatePack.CreateComponentInstructions(template),
                templatePack.JsonShape,
                templatePack.JsonShape,
                AgentPermissionsPolicy.Default with
                {
                    CanUseTools = false,
                    CanAskOtherAgents = false,
                    CanEscalateToHuman = false,
                    RequiresApprovalForExternalCalls = false
                }),
            cancellationToken);

        if (current is null)
        {
            existingComponents.Add(component);
        }
        else
        {
            var index = existingComponents.FindIndex(item => item.Id == current.Id);
            existingComponents[index] = component;
        }

        return component;
    }

    private static string ResolveModel(WorkflowProviderOption? provider)
    {
        if (provider is null)
        {
            return ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        }

        return provider.ModelOptions.FirstOrDefault(model =>
                   string.Equals(model, ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparison.OrdinalIgnoreCase)) ??
               (string.IsNullOrWhiteSpace(provider.DefaultModel)
                   ? ManagedSeedProviderFallbacks.OpenAiDefaultModel
                   : provider.DefaultModel);
    }

    private async Task<bool> TrySaveDefinitionAsync(
        WorkflowId? id,
        WorkflowVersionId? expectedVersionId,
        string name,
        string description,
        WorkflowGraph graph,
        IReadOnlyList<WorkflowInputParameterDescriptor> inputParameters,
        WorkflowRuntimePolicy runtimePolicy,
        WorkflowTemplateProvenance provenance,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var validationDefinition = new WorkflowDefinition(
            id ?? WorkflowId.New(),
            WorkflowVersionId.New(),
            name.Trim(),
            description.Trim(),
            WorkflowLifecycleStatus.Active,
            graph,
            runtimePolicy,
            now,
            now)
        {
            InputParameters = inputParameters
        };
        var validation = await catalogService.ValidateDefinitionAsync(validationDefinition, cancellationToken);
        if (!validation.Succeeded)
        {
            var validationMessage = string.Join(" ", validation.Issues.Select(issue => issue.Message));
            if (IsUnavailableTemplateDependencyValidation(validation))
            {
                logger.LogWarning(
                    "Skipping workflow example seed '{WorkflowName}' because one or more optional workflow executors are unavailable in this host: {ValidationIssues}",
                    name,
                    validationMessage);
                return false;
            }

            throw new InvalidOperationException($"Workflow definition seed failed validation for '{name}': {validationMessage}");
        }

        await catalogService.SaveDefinitionAsync(
            new WorkflowDefinitionSaveRequest(
                id,
                expectedVersionId,
                name,
                description,
                WorkflowLifecycleStatus.Active,
                graph,
                runtimePolicy)
            {
                InputParameters = inputParameters,
                TemplateProvenance = provenance
            },
            cancellationToken);
        return true;
    }

    private static WorkflowTemplateProvenance CreateTemplateProvenance(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template)
    {
        var sourceBytes = File.ReadAllBytes(template.SourcePath);
        var sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        return new WorkflowTemplateProvenance(
            template.Key,
            templatePack.Manifest.PackKey,
            templatePack.Manifest.Version,
            sourceHash);
    }

    private static bool HasExactTemplateProvenance(
        WorkflowDefinition definition,
        WorkflowTemplateProvenance provenance)
        => string.Equals(definition.TemplateKey, provenance.TemplateKey, StringComparison.Ordinal) &&
           string.Equals(definition.TemplatePackKey, provenance.TemplatePackKey, StringComparison.Ordinal) &&
           string.Equals(definition.TemplatePackVersion, provenance.TemplatePackVersion, StringComparison.Ordinal) &&
           string.Equals(definition.SourceHash, provenance.SourceHash, StringComparison.Ordinal);

    private static bool IsUnavailableTemplateDependencyValidation(WorkflowValidationResult validation)
    {
        return validation.Issues.Count > 0 &&
               validation.Issues.All(issue =>
                   issue.Code == WorkflowValidationIssueCode.InvalidExecutorReference &&
                   issue.Message.Contains(" is not runnable:", StringComparison.OrdinalIgnoreCase));
    }

    private void SeedWorkspaceAssets()
    {
        EnsureDirectory("samples/workflows");
        WriteTextAsset(
            "samples/workflows/input-document.md",
            """
            # Vendor Renewal Brief

            Contract renewal is due in 18 days. The vendor asks for a 14 percent price increase and a two-year renewal.
            Security review is current, but finance approval is missing. Product owner asks for a short summary, risks, and a recommended next step.
            """);
        WriteTextAsset(
            "samples/workflows/support-email.md",
            """
            From: customer@example.test
            Subject: Renewal blocked by invoice mismatch

            We cannot approve the renewal until invoice INV-1042 matches the contract. Please create a task, summarize the risk,
            and draft a short response confirming that finance will review it today.
            """);
        WriteTextAsset(
            "samples/workflows/meeting-notes.md",
            """
            Weekly launch meeting: payment validation passed, inventory check is blocked by supplier ETA, shipment reservation needs owner confirmation.
            Send a concise recap and create follow-up tasks for blocked or owner-dependent items.
            """);
        WriteTextAsset(
            "samples/workflows/diff-before.md",
            """
            # Release checklist

            - Payment validation pending.
            - Inventory check pending.
            - Shipment reservation pending.
            """);
        WriteTextAsset(
            "samples/workflows/diff-after.md",
            """
            # Release checklist

            - Payment validation passed.
            - Inventory check blocked by supplier ETA.
            - Shipment reservation needs owner confirmation.
            - Customer notification draft required.
            """);
        WriteTextAsset(
            "samples/workflows/task-intake.json",
            """
            {
              "projectId": "00000000-0000-0000-0000-000000000000",
              "nodeId": "sample-workflow-node",
              "summary": "Sample task intake payload for workflow template testing.",
              "tasks": [
                {
                  "title": "Review release blocker",
                  "summary": "Check supplier ETA and confirm whether launch can proceed.",
                  "owner": "project team",
                  "dueUtc": "",
                  "urgency": "high",
                  "requiresResponse": true,
                  "asap": true,
                  "sourceEmailId": "sample",
                  "evidence": ["Inventory check is blocked by supplier ETA."]
                }
              ]
            }
            """);
        WriteWorkbook(
            "samples/workflows/invoices.xlsx",
            "Invoices",
            "A1:F6",
            [
                ["Invoice", "Customer", "Amount", "DueDate", "Region", "Status"],
                ["INV-1001", "Northwind", "1250", "2026-05-30", "US", "new"],
                ["INV-1042", "Contoso", "18450", "2026-05-20", "EU", "mismatch"],
                ["INV-1067", "Fabrikam", "480", "2026-06-04", "US", "ready"],
                ["INV-1099", "Adventure Works", "7300", "2026-05-19", "UK", "review"],
                ["INV-1120", "Tailspin", "990", "2026-06-10", "CA", "ready"]
            ]);
        WriteWorkbook(
            "samples/workflows/pipeline.xlsx",
            "Pipeline",
            "A1:E6",
            [
                ["Lead", "Score", "Segment", "NextStep", "Owner"],
                ["ACME", "91", "enterprise", "security review", "sales"],
                ["Globex", "76", "mid-market", "pricing", "sales"],
                ["Initech", "42", "smb", "nurture", "marketing"],
                ["Umbrella", "88", "enterprise", "legal review", "sales"],
                ["Soylent", "65", "mid-market", "case study", "marketing"]
            ]);
    }

    private void EnsureDirectory(string path)
    {
        var result = workspaceFiles.CreateDirectory(path);
        if (!result.Succeeded && !workspaceFiles.StatPath(path).Exists)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    private void WriteTextAsset(string path, string content)
    {
        if (workspaceFiles.StatPath(path).Exists)
        {
            return;
        }

        var result = workspaceFiles.WriteTextFile(path, content, overwrite: false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    private void WriteWorkbook(
        string path,
        string worksheetName,
        string rangeAddress,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var resolvedPath = workspacePaths.ResolveFilePath(path, allowMissing: true);
        if (File.Exists(resolvedPath.FullPath))
        {
            return;
        }

        spreadsheets.Write(new DocumentWriteRequest(
            resolvedPath.FullPath,
            resolvedPath.FullPath,
            worksheetName,
            Array.Empty<DocumentCellWrite>(),
            [new DocumentRangeWrite(rangeAddress, rows)],
            CreateWorkbookIfMissing: true,
            Overwrite: true));
    }
}
