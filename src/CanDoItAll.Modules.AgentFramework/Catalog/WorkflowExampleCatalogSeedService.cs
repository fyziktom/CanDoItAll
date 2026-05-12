using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    ILogger<WorkflowExampleCatalogSeedService> logger)
{
    private const string SeedVersion = "2026-05-project-structure-email-switch-tasks-v3";
    private const string SeedMarker = "Managed workflow example seed";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        """
        {"type":"object","additionalProperties":true}
        """,
        "Workflow JSON payload");

    private static readonly WorkflowExecutorExecutionPolicy FastExecutorPolicy = WorkflowExecutorExecutionPolicy.Default with
    {
        TimeoutSeconds = 45,
        CaptureOutputArtifact = true
    };

    private static readonly WorkflowExecutorExecutionPolicy SlowExecutorPolicy = WorkflowExecutorExecutionPolicy.Default with
    {
        TimeoutSeconds = 90,
        MaxRetryAttempts = 1,
        CaptureOutputArtifact = true
    };

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
        var provider = await ResolveProviderOptionAsync(cancellationToken);
        var existingComponents = (await componentLibrary.ListComponentsAsync(cancellationToken)).ToList();
        var existingDefinitions = (await catalogService.ListDefinitionsAsync(cancellationToken)).ToList();
        var seededCount = 0;

        foreach (var spec in BuildExampleSpecs())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var component = await EnsureComponentAsync(spec, provider, existingComponents, cancellationToken);
            var graph = spec.BuildGraph(component.Id);
            var definitionName = $"Example: {spec.Name}";
            var description = $"{spec.Description} {SeedMarker}: {SeedVersion}.";
            var existing = existingDefinitions.FirstOrDefault(item => string.Equals(item.Name, definitionName, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                var detail = await catalogService.GetDefinitionAsync(existing.Id, existing.VersionId, cancellationToken);
                if (detail is not null &&
                    detail.Definition.Description.Contains(SeedMarker, StringComparison.OrdinalIgnoreCase) &&
                    detail.Definition.Description.Contains(SeedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (detail is not null &&
                    !detail.Definition.Description.Contains(SeedMarker, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Skipping workflow example seed '{WorkflowName}' because a non-managed definition with that name already exists.",
                        definitionName);
                    continue;
                }

                await SaveDefinitionAsync(
                    existing.Id,
                    existing.VersionId,
                    definitionName,
                    description,
                    graph,
                    cancellationToken);
            }
            else
            {
                await SaveDefinitionAsync(
                    null,
                    null,
                    definitionName,
                    description,
                    graph,
                    cancellationToken);
            }

            seededCount++;
        }

        if (seededCount > 0)
        {
            logger.LogInformation(
                "Seeded or refreshed {WorkflowCount} workflow examples with seed version {SeedVersion}.",
                seededCount,
                SeedVersion);
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
                    WorkflowRuntimeBackendKind.DurableTask,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: true,
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
                        WorkflowArtifactKind.ToolReceipt
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
        WorkflowExampleSpec spec,
        WorkflowProviderOption? provider,
        List<LlmCallComponent> existingComponents,
        CancellationToken cancellationToken)
    {
        var componentName = $"Example LLM: {spec.Name}";
        var current = existingComponents.FirstOrDefault(item => string.Equals(item.Name, componentName, StringComparison.OrdinalIgnoreCase));
        var component = await componentLibrary.SaveComponentAsync(
            new LlmCallComponentSaveRequest(
                current?.Id,
                componentName,
                provider?.ProviderProfileId,
                ResolveModel(provider),
                WorkflowModality.Text,
                new WorkflowModelSettings(
                    Temperature: 0.1,
                    MaxOutputTokens: 1400,
                    RequireJsonOutput: true,
                    ResponseFormatJsonSchema: ExampleResultSchema),
                BuildComponentInstructions(spec),
                JsonShape,
                JsonShape,
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

    private async Task SaveDefinitionAsync(
        WorkflowId? id,
        WorkflowVersionId? expectedVersionId,
        string name,
        string description,
        WorkflowGraph graph,
        CancellationToken cancellationToken)
    {
        await catalogService.SaveDefinitionAsync(
            new WorkflowDefinitionSaveRequest(
                id,
                expectedVersionId,
                name,
                description,
                WorkflowLifecycleStatus.Active,
                graph,
                new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.DurableTask,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: true,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)),
            cancellationToken);
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

    private static IReadOnlyList<WorkflowExampleSpec> BuildExampleSpecs()
        =>
        [
            new(
                "Document Summary Review Router",
                "Summarizes an input document, gates finance/security review with IF/ELSE, and captures either an auto-summary or review request.",
                "Return needsReview true when the document mentions missing approval, compliance risk, security risk, or legal risk. Preserve projectId when present.",
                BuildDocumentSummaryGraph),
            new(
                "Email Task Creation Router",
                "Classifies email as tasks, ASAP response, informative, or default with SWITCH/default and creates WorkItem/task nodes for actionable email requests.",
                """
                Classify route as `tasks`, `asap_response`, `informative`, or `no_action`.
                Use `tasks` when the sender asks me or my team to do concrete work.
                Use `asap_response` when a reply is explicitly urgent, same-day, blocked, escalated, or phrased as ASAP/immediate.
                Use `informative` when the email only gives FYI/context/status and no work is assigned to me.
                Use `no_action` when the message is delegated to someone else, spam-like, or not relevant; this goes through the SWITCH default branch.
                For `tasks` and `asap_response`, return a non-empty tasks array. Each task requires title, summary, owner, dueUtc when present, urgency, requiresResponse, asap, sourceEmailId, and evidence.
                Create tasks only for actions assigned to me or required from the current project team; do not create tasks for FYI text or work assigned solely to another person.
                Preserve projectId and nodeId so the project-structure executor creates WorkItem/task nodes under the workflow node.
                """,
                BuildEmailTaskGraph),
            new(
                "Email Reply Draft Gate",
                "Drafts a customer response only when the email requires a response; otherwise archives a compact summary.",
                "Set requiresResponse true when sender expects an answer or asks a question. Include responseDraft only when true.",
                BuildEmailReplyGraph),
            new(
                "Invoice Workbook Risk Switch",
                "Reads invoice rows from XLSX, classifies risk, and writes a branch-specific decision back to an output workbook.",
                "Classify risk as high, medium, low, or default from invoice amount, status, and due date. Return risk and summary.",
                BuildInvoiceWorkbookGraph),
            new(
                "Pipeline Workbook Fan-out",
                "Reads sales pipeline XLSX and fans out to sales, marketing, and executive update branches based on selected targets.",
                "Return targets containing sales, marketing, and/or executive based on segment, score, and next step.",
                BuildPipelineFanOutGraph),
            new(
                "Mouser Order Reconciliation",
                "Compares the supplied Mouser order workbook and receipt PDF from a project-structure workflow node and stores the reconciliation summary under that workflow node.",
                """
                Compare the loaded `Cart_Mar30_1059AM.xls` workbook rows with `MOUSER_Receipt_89566550.pdf`.
                Extract ordered quantity, shipped quantity, pending quantity, unit price, extended price, invoice number, order date, tracking numbers, and merchandise/freight/tax totals when present.
                Set route to `matched` only when the workbook and receipt agree on line items and totals; otherwise set `mismatch`.
                The markdown must contain a reconciliation table with Mouser part number, customer/manufacturer part number when visible, quantity, unit price, extended price, and source file evidence.
                Include any unreadable or missing source as a gap; do not mark matched if a required file was not loaded.
                """,
                BuildMouserOrderReconciliationGraph),
            new(
                "Mouser Purchasing Summary",
                "Turns a Mouser order folder into a purchasing brief with line-item highlights, open questions, and a result asset under the workflow node.",
                """
                Build a purchasing brief from all loaded Mouser order sources.
                Identify invoice/order number, order date, ship date, tracking numbers, destination, line-item highlights, large-cost items, total spend signals, and follow-up questions.
                Mention both the workbook and receipt source paths when loaded.
                Set risk to `low` when evidence is complete and internally consistent, `medium` when some values are missing, and `high` when documents conflict or no order document was readable.
                """,
                BuildMouserPurchasingSummaryGraph),
            new(
                "SEAMARK Xray Device Folder Summary",
                "Summarizes a folder of SEAMARK x-ray device PDFs and stores device/use-case findings under the workflow node.",
                """
                Treat the loaded SEAMARK folder as the primary source. Read every loaded PDF text and produce a real device summary, not a placeholder.
                Identify device/model families including X-5600, X-6600, X-6600A, product-series context, inspection use cases, tube voltage/current or focal-size evidence, stage size, detector/resolution evidence, operating system, power, weight/dimensions, and source file names.
                Include quotation evidence when loaded: ZM-x5600 at $35,000, ZM-x6600 at $41,500.00, ZM-x6600A at $66,000, plus the listed marketing price ranges.
                Critical price mapping: X-5600 is ZM-x5600 and costs $35,000; X-6600 is ZM-x6600 and costs $41,500.00; X-6600A is ZM-x6600A and costs $66,000. Never swap X-6600 and X-6600A prices.
                If the catalogue PDF has no extractable text, record it as a scanned/unreadable gap while still using the extractable specification and quotation PDFs.
                Set risk to `medium` when some sources are scanned or pricing needs current validation; set route to `summary`.
                """,
                BuildSeamarkFolderSummaryGraph),
            new(
                "SEAMARK Price List Extraction",
                "Extracts price-list highlights and uncertainty from the SEAMARK quote and device specification PDFs.",
                """
                Focus on `X Ray Machine Agent Quotation List2018.pdf` and related specification PDFs.
                Extract price evidence into a table with model, EWX Shenzhen USD price, marketing price range, quantity, and specification evidence from the device PDFs.
                Required price facts when present: ZM-x5600 $35,000 and USD39900-42000, ZM-x6600 $41,500.00 and USD46000-49000, ZM-x6600A $66,000 and USD73000-78000.
                Critical price mapping: X-5600 is ZM-x5600 and costs $35,000; X-6600 is ZM-x6600 and costs $41,500.00; X-6600A is ZM-x6600A and costs $66,000. Never swap X-6600 and X-6600A prices.
                Include quotation date, delivery/payment/service terms, warranty/travel-cost caveat, and validity period.
                Set needsReview true because the quotation is from 2018 and current prices must be confirmed.
                """,
                BuildSeamarkPriceListGraph),
            new(
                "IoTFactory Financial Plan Review",
                "Reviews the supplied IoTFactory financial workbook and stores budget risks, assumptions, and follow-up actions under the workflow node.",
                """
                Use the loaded `IoTFactory rozpočet-v1.xlsx` workbook as the financial-plan source.
                Summarize revenue assumptions, module/server unit assumptions, year-by-year budget shape, expense categories, cash/funding risks, and validation questions.
                Include Czech sheet names or labels such as `Summary Rozpočet`, `Příjmy z modulů`, `Výdaje`, and yearly tabs when loaded.
                Set risk to high when revenue depends on unvalidated unit-volume growth or missing cost evidence; otherwise medium.
                """,
                BuildIotFactoryFinancialPlanGraph),
            new(
                "Internet Research Capture",
                "Fetches bounded web content, summarizes it, and stores relevant research in project structure when projectId is supplied.",
                "From fetched content and carried inputPayload, return relevant, summary, projectId, and sourceUrl. Keep projectId empty when absent.",
                BuildInternetResearchGraph),
            new(
                "Support SLA Escalation",
                "Routes support tickets by SLA breach and severity, with IF and SWITCH branches for urgent operational handling.",
                "Set breachedSla true for overdue or sev-1/sev-2 tickets. Classify route as incident, billing, product, or default.",
                BuildSupportSlaGraph),
            new(
                "Sales Lead Qualification",
                "Classifies leads into enterprise handoff, nurture, disqualify, or default routes using SWITCH/default.",
                "Classify route as enterprise, nurture, disqualify, or default. Use score, segment, budget, and timeline evidence.",
                BuildSalesLeadGraph),
            new(
                "Release Readiness Gate",
                "Gates release publication with IF/ELSE based on blockers, unresolved approvals, and missing validation evidence.",
                "Set ready true only when blockers are empty, validation passed, and approvals are present.",
                BuildReleaseGateGraph),
            new(
                "Incident Response Fan-out",
                "Fans out incident response to communications, engineering, security, and leadership branches from one incident payload.",
                "Return targets containing comms, engineering, security, and/or leadership based on severity, data exposure, and customer impact.",
                BuildIncidentFanOutGraph),
            new(
                "HR Staffing Intake Switch",
                "Routes staffing requests to engineering, design, operations, or default review based on requested role family.",
                "Classify route as engineering, design, operations, or default. Include missingInputs for budget, location, or start date gaps.",
                BuildHrStaffingGraph),
            new(
                "Contract Renewal Watch",
                "Detects renewal risk from contract notes and routes risky renewals to review while archiving low-risk updates.",
                "Set needsReview true when renewal terms, price, security, legal, or finance risk requires explicit review.",
                BuildContractRenewalGraph),
            new(
                "Customer Feedback Action Fan-out",
                "Classifies customer feedback into product, support, success, and marketing follow-up lanes using fan-out selectors.",
                "Return targets containing product, support, success, and/or marketing based on feedback themes and urgency.",
                BuildCustomerFeedbackFanOutGraph),
            new(
                "Vendor Risk Intake Switch",
                "Routes vendor risk notes to security, legal, finance, or default handling using SWITCH/default.",
                "Classify route as security, legal, finance, or default. Include summary and evidence for the chosen route.",
                BuildVendorRiskGraph),
            new(
                "Meeting Notes Action Extractor",
                "Extracts meeting actions and uses SWITCH/default to separate blocked, owner-needed, informational, and default outcomes.",
                "Classify route as blocked, owner, info, or default. Include actionItems and preserve projectId when present.",
                BuildMeetingNotesGraph)
        ];

    private static string BuildComponentInstructions(WorkflowExampleSpec spec)
        => $"""
           You are a production workflow LLM component for "{spec.Name}".

           Contract:
           - Return one JSON object only. Do not wrap it in Markdown.
           - Always include: route, summary, actions, targets, risk, relevant, needsReview, requiresResponse, ready, projectId, nodeId, sourceUrl.
           - Always include markdown. The markdown field is the user-facing project-structure result asset.
           - Use empty string, false, or [] when a field does not apply.
           - Preserve projectId from top-level projectId or from project.id when either exists. Do not invent ids.
           - Preserve nodeId from top-level nodeId or from runContext.workflowNodeId when either exists. Do not invent ids.
           - For email task workflows, include emailCategory, isInformational, asapResponseRequired, and tasks. The tasks array must contain only concrete work assigned to me/current project team.
           - Email task objects must include title, summary, owner, dueUtc, urgency, requiresResponse, asap, sourceEmailId, and evidence. Use an empty dueUtc only when the email has no deadline.
           - When the email only informs or delegates work to someone else, set tasks to [] and route to informative or no_action.
           - When the input contains project or runContext objects, copy them to the output unchanged so project-structure storage can keep the original execution context.
           - Keep summary under 900 characters and actions as concrete imperative steps.
           - When the input has sourceDocuments or documents, base the result on those loaded document texts, not on file names alone.
           - In markdown, include: result summary, evidence table with source file names/paths, concrete findings, open gaps, and recommended next actions.
           - If a source has extractionStatus pdf-no-extractable-text, say that explicitly and do not pretend the document content was read.
           - Normalize route and target values to lowercase tokens used by the workflow branches.
           - Fields consumed by IF/SWITCH branches must be literal predicate data, not answers to the prose scenario name.
           - Do not invert booleans or route tokens because a label uses negative wording such as "not selected" or "does not need".

           Scenario-specific routing:
           {spec.RoutingInstructions}
           """;

    private static WorkflowGraph BuildDocumentSummaryGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            Llm("summarize", "Summarize document", componentId, 340, 220),
            Decision("review-gate", "IF", 600, 220),
            Logic("auto-summary", "Store auto summary", 900, 120),
            Human("request-review", "Request finance/security review", 900, 330),
            End(1180, 220)
        };
        return Graph(
            nodes,
            Direct("start", "summarize"),
            Direct("summarize", "review-gate"),
            Predicate("review-gate", "request-review", "$.needsReview", WorkflowRouteOperator.Equals, "true", WorkflowRouteValueKind.Boolean, "IF"),
            Predicate("review-gate", "auto-summary", "$.needsReview", WorkflowRouteOperator.Equals, "false", WorkflowRouteValueKind.Boolean, "ELSE"),
            Direct("auto-summary", "end"),
            Direct("request-review", "end"));
    }

    private static WorkflowGraph BuildEmailTaskGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            SourceIngest("ingest-email-sources", "Load email sources", 320, 220),
            Llm("classify-email", "Classify email", componentId, 620, 220),
            Decision("email-switch", "SWITCH", 900, 220),
            ProjectTaskNodes("create-email-task-nodes", "Create task nodes", 1210, 20),
            ProjectTaskNodes("create-asap-response-task", "Create ASAP response task", 1210, 170),
            ProjectAsset("store-informative-summary", "Store informative summary", 1210, 320, "Informative email summary"),
            ProjectAsset("store-default-summary", "Store no-action summary", 1210, 470, "No-action email summary"),
            End(1530, 245)
        };
        return Graph(
            nodes,
            Direct("start", "ingest-email-sources"),
            Direct("ingest-email-sources", "classify-email"),
            Direct("classify-email", "email-switch"),
            Switch("email-switch", "create-email-task-nodes", "$.route", "tasks", "Tasks"),
            Switch("email-switch", "create-asap-response-task", "$.route", "asap_response", "ASAP response"),
            Switch("email-switch", "store-informative-summary", "$.route", "informative", "Informative"),
            SwitchDefault("email-switch", "store-default-summary", "DEFAULT"),
            Direct("create-email-task-nodes", "end"),
            Direct("create-asap-response-task", "end"),
            Direct("store-informative-summary", "end"),
            Direct("store-default-summary", "end"));
    }

    private static WorkflowGraph BuildEmailReplyGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            Llm("analyze-email", "Analyze email response need", componentId, 340, 220),
            Decision("response-gate", "IF", 620, 220),
            StorageWrite("store-reply", "Store reply draft", 900, 120, "samples/workflows/reply-draft.md"),
            StorageWrite("archive-summary", "Archive email summary", 900, 330, "samples/workflows/email-archive.md"),
            End(1180, 220)
        };
        return Graph(
            nodes,
            Direct("start", "analyze-email"),
            Direct("analyze-email", "response-gate"),
            Predicate("response-gate", "store-reply", "$.requiresResponse", WorkflowRouteOperator.Equals, "true", WorkflowRouteValueKind.Boolean, "IF"),
            Predicate("response-gate", "archive-summary", "$.requiresResponse", WorkflowRouteOperator.Equals, "false", WorkflowRouteValueKind.Boolean, "ELSE"),
            Direct("store-reply", "end"),
            Direct("archive-summary", "end"));
    }

    private static WorkflowGraph BuildInvoiceWorkbookGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            SpreadsheetRead("read-invoices", "Read invoice rows", 300, 220, "samples/workflows/invoices.xlsx", "Invoices", "A1:F6"),
            Llm("classify-invoices", "Classify invoice risk", componentId, 560, 220),
            Decision("risk-switch", "SWITCH", 820, 220),
            SpreadsheetWrite("write-high-risk", "Write high risk decision", 1120, 20, "High risk - finance review"),
            SpreadsheetWrite("write-medium-risk", "Write medium risk decision", 1120, 160, "Medium risk - owner review"),
            SpreadsheetWrite("write-low-risk", "Write low risk decision", 1120, 300, "Low risk - auto approve"),
            SpreadsheetWrite("write-default-risk", "Write default decision", 1120, 440, "Unhandled - manual review"),
            End(1440, 220)
        };
        return Graph(
            nodes,
            Direct("start", "read-invoices"),
            Direct("read-invoices", "classify-invoices"),
            Direct("classify-invoices", "risk-switch"),
            Switch("risk-switch", "write-high-risk", "$.risk", "high", "High"),
            Switch("risk-switch", "write-medium-risk", "$.risk", "medium", "Medium"),
            Switch("risk-switch", "write-low-risk", "$.risk", "low", "Low"),
            SwitchDefault("risk-switch", "write-default-risk", "DEFAULT"),
            Direct("write-high-risk", "end"),
            Direct("write-medium-risk", "end"),
            Direct("write-low-risk", "end"),
            Direct("write-default-risk", "end"));
    }

    private static WorkflowGraph BuildPipelineFanOutGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            SpreadsheetRead("read-pipeline", "Read pipeline workbook", 300, 220, "samples/workflows/pipeline.xlsx", "Pipeline", "A1:E6"),
            Llm("plan-updates", "Plan branch updates", componentId, 560, 220),
            Decision("fanout", "FAN-OUT", 820, 220),
            SpreadsheetWrite("sales-update", "Sales update", 1120, 40, "Sales follow-up needed"),
            SpreadsheetWrite("marketing-update", "Marketing update", 1120, 220, "Marketing nurture needed"),
            SpreadsheetWrite("executive-update", "Executive update", 1120, 400, "Executive visibility needed"),
            End(1430, 220)
        };
        return Graph(
            nodes,
            Direct("start", "read-pipeline"),
            Direct("read-pipeline", "plan-updates"),
            Direct("plan-updates", "fanout"),
            FanOut("fanout", "sales-update", "$.targets", "sales", 0, "Sales"),
            FanOut("fanout", "marketing-update", "$.targets", "marketing", 1, "Marketing"),
            FanOut("fanout", "executive-update", "$.targets", "executive", 2, "Executive"),
            Direct("sales-update", "end"),
            Direct("marketing-update", "end"),
            Direct("executive-update", "end"));
    }

    private static WorkflowGraph BuildMouserOrderReconciliationGraph(WorkflowComponentId componentId)
        => BuildProjectStructureSummaryGraph(
            componentId,
            "reconcile-mouser-order",
            "Reconcile Mouser order",
            "Mouser order reconciliation summary");

    private static WorkflowGraph BuildMouserPurchasingSummaryGraph(WorkflowComponentId componentId)
        => BuildProjectStructureSummaryGraph(
            componentId,
            "summarize-mouser-purchase",
            "Summarize Mouser purchase",
            "Mouser purchasing summary");

    private static WorkflowGraph BuildSeamarkFolderSummaryGraph(WorkflowComponentId componentId)
        => BuildProjectStructureSummaryGraph(
            componentId,
            "summarize-seamark-folder",
            "Summarize SEAMARK folder",
            "SEAMARK xray device summary");

    private static WorkflowGraph BuildSeamarkPriceListGraph(WorkflowComponentId componentId)
        => BuildProjectStructureSummaryGraph(
            componentId,
            "extract-seamark-pricing",
            "Extract SEAMARK pricing",
            "SEAMARK price list summary");

    private static WorkflowGraph BuildIotFactoryFinancialPlanGraph(WorkflowComponentId componentId)
        => BuildProjectStructureSummaryGraph(
            componentId,
            "review-iotfactory-plan",
            "Review IoTFactory financial plan",
            "IoTFactory financial plan review");

    private static WorkflowGraph BuildProjectStructureSummaryGraph(
        WorkflowComponentId componentId,
        string llmId,
        string llmName,
        string assetTitle)
    {
        var nodes = new[]
        {
            Start(),
            SourceIngest("ingest-sources", "Load project-structure sources", 330, 220),
            Llm(llmId, llmName, componentId, 640, 220),
            ProjectAsset("store-project-structure-summary", "Store workflow summary", 950, 220, assetTitle),
            End(1260, 220)
        };
        return Graph(
            nodes,
            Direct("start", "ingest-sources"),
            Direct("ingest-sources", llmId),
            Direct(llmId, "store-project-structure-summary"),
            Direct("store-project-structure-summary", "end"));
    }

    private static WorkflowGraph BuildInternetResearchGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            HttpFetch("fetch-source", "Fetch source", 300, 220),
            Llm("summarize-source", "Summarize source", componentId, 580, 220),
            Decision("relevance-gate", "IF", 860, 220),
            ProjectAsset("store-research", "Store research asset", 1160, 120, "Internet research brief"),
            StorageWrite("store-not-relevant", "Store not relevant note", 1160, 330, "samples/workflows/research-not-relevant.md"),
            End(1460, 220)
        };
        return Graph(
            nodes,
            Direct("start", "fetch-source"),
            Direct("fetch-source", "summarize-source"),
            Direct("summarize-source", "relevance-gate"),
            Predicate("relevance-gate", "store-research", "$.relevant", WorkflowRouteOperator.Equals, "true", WorkflowRouteValueKind.Boolean, "IF"),
            Predicate("relevance-gate", "store-not-relevant", "$.relevant", WorkflowRouteOperator.Equals, "false", WorkflowRouteValueKind.Boolean, "ELSE"),
            Direct("store-research", "end"),
            Direct("store-not-relevant", "end"));
    }

    private static WorkflowGraph BuildSupportSlaGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            Llm("triage-ticket", "Triage support ticket", componentId, 330, 220),
            Decision("sla-gate", "IF", 600, 220),
            Human("escalate-sla", "Escalate SLA breach", 880, 100),
            Decision("ticket-switch", "SWITCH", 900, 330),
            Logic("billing-ticket", "Billing path", 1200, 180),
            Logic("product-ticket", "Product path", 1200, 330),
            Logic("default-ticket", "Default support path", 1200, 480),
            End(1500, 260)
        };
        return Graph(
            nodes,
            Direct("start", "triage-ticket"),
            Direct("triage-ticket", "sla-gate"),
            Predicate("sla-gate", "escalate-sla", "$.breachedSla", WorkflowRouteOperator.Equals, "true", WorkflowRouteValueKind.Boolean, "IF"),
            Predicate("sla-gate", "ticket-switch", "$.breachedSla", WorkflowRouteOperator.Equals, "false", WorkflowRouteValueKind.Boolean, "ELSE"),
            Switch("ticket-switch", "billing-ticket", "$.route", "billing", "Billing"),
            Switch("ticket-switch", "product-ticket", "$.route", "product", "Product"),
            SwitchDefault("ticket-switch", "default-ticket", "DEFAULT"),
            Direct("escalate-sla", "end"),
            Direct("billing-ticket", "end"),
            Direct("product-ticket", "end"),
            Direct("default-ticket", "end"));
    }

    private static WorkflowGraph BuildSalesLeadGraph(WorkflowComponentId componentId)
        => BuildSwitchOnlyGraph(
            componentId,
            "classify-lead",
            "Classify sales lead",
            "$.route",
            [
                ("enterprise", "enterprise-handoff", "Enterprise handoff"),
                ("nurture", "nurture-lead", "Nurture lead"),
                ("disqualify", "disqualify-lead", "Disqualify lead")
            ],
            "default-lead");

    private static WorkflowGraph BuildReleaseGateGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            Llm("check-release", "Check release readiness", componentId, 340, 220),
            Decision("release-gate", "IF", 620, 220),
            Logic("publish-release", "Publish release", 900, 120),
            Human("hold-release", "Hold for approval", 900, 330),
            End(1180, 220)
        };
        return Graph(
            nodes,
            Direct("start", "check-release"),
            Direct("check-release", "release-gate"),
            Predicate("release-gate", "publish-release", "$.ready", WorkflowRouteOperator.Equals, "true", WorkflowRouteValueKind.Boolean, "IF"),
            Predicate("release-gate", "hold-release", "$.ready", WorkflowRouteOperator.Equals, "false", WorkflowRouteValueKind.Boolean, "ELSE"),
            Direct("publish-release", "end"),
            Direct("hold-release", "end"));
    }

    private static WorkflowGraph BuildIncidentFanOutGraph(WorkflowComponentId componentId)
        => BuildFanOutOnlyGraph(
            componentId,
            "classify-incident",
            "Classify incident response",
            [
                ("comms", "Comms update"),
                ("engineering", "Engineering repair"),
                ("security", "Security review"),
                ("leadership", "Leadership brief")
            ]);

    private static WorkflowGraph BuildHrStaffingGraph(WorkflowComponentId componentId)
        => BuildSwitchOnlyGraph(
            componentId,
            "classify-staffing",
            "Classify staffing request",
            "$.route",
            [
                ("engineering", "engineering-staffing", "Engineering staffing"),
                ("design", "design-staffing", "Design staffing"),
                ("operations", "operations-staffing", "Operations staffing")
            ],
            "default-staffing");

    private static WorkflowGraph BuildContractRenewalGraph(WorkflowComponentId componentId)
    {
        var nodes = new[]
        {
            Start(),
            Llm("review-renewal", "Review renewal", componentId, 340, 220),
            Decision("renewal-gate", "IF", 620, 220),
            Human("renewal-review", "Renewal review", 900, 120),
            StorageWrite("renewal-archive", "Archive renewal summary", 900, 330, "samples/workflows/renewal-summary.md"),
            End(1180, 220)
        };
        return Graph(
            nodes,
            Direct("start", "review-renewal"),
            Direct("review-renewal", "renewal-gate"),
            Predicate("renewal-gate", "renewal-review", "$.needsReview", WorkflowRouteOperator.Equals, "true", WorkflowRouteValueKind.Boolean, "IF"),
            Predicate("renewal-gate", "renewal-archive", "$.needsReview", WorkflowRouteOperator.Equals, "false", WorkflowRouteValueKind.Boolean, "ELSE"),
            Direct("renewal-review", "end"),
            Direct("renewal-archive", "end"));
    }

    private static WorkflowGraph BuildCustomerFeedbackFanOutGraph(WorkflowComponentId componentId)
        => BuildFanOutOnlyGraph(
            componentId,
            "classify-feedback",
            "Classify feedback follow-up",
            [
                ("product", "Product follow-up"),
                ("support", "Support follow-up"),
                ("success", "Success follow-up"),
                ("marketing", "Marketing follow-up")
            ]);

    private static WorkflowGraph BuildVendorRiskGraph(WorkflowComponentId componentId)
        => BuildSwitchOnlyGraph(
            componentId,
            "classify-vendor-risk",
            "Classify vendor risk",
            "$.route",
            [
                ("security", "security-review", "Security review"),
                ("legal", "legal-review", "Legal review"),
                ("finance", "finance-review", "Finance review")
            ],
            "default-vendor-risk");

    private static WorkflowGraph BuildMeetingNotesGraph(WorkflowComponentId componentId)
        => BuildSwitchOnlyGraph(
            componentId,
            "extract-meeting-actions",
            "Extract meeting actions",
            "$.route",
            [
                ("blocked", "blocked-action", "Blocked action"),
                ("owner", "owner-confirmation", "Owner confirmation"),
                ("info", "information-only", "Information only")
            ],
            "default-meeting");

    private static WorkflowGraph BuildSwitchOnlyGraph(
        WorkflowComponentId componentId,
        string llmId,
        string llmName,
        string jsonPath,
        IReadOnlyList<(string Value, string NodeId, string NodeName)> cases,
        string defaultNodeId)
    {
        var caseNodes = cases
            .Select((item, index) => Logic(item.NodeId, item.NodeName, 900, 60 + (index * 150)))
            .ToArray();
        var nodes = new List<WorkflowNode>
        {
            Start(),
            Llm(llmId, llmName, componentId, 340, 220),
            Decision("switch", "SWITCH", 620, 220)
        };
        nodes.AddRange(caseNodes);
        nodes.Add(Logic(defaultNodeId, "Default review", 900, 60 + (cases.Count * 150)));
        nodes.Add(End(1180, 260));

        var edges = new List<WorkflowEdge>
        {
            Direct("start", llmId),
            Direct(llmId, "switch")
        };
        edges.AddRange(cases.Select(item => Switch("switch", item.NodeId, jsonPath, item.Value, ToLabel(item.Value))));
        edges.Add(SwitchDefault("switch", defaultNodeId, "DEFAULT"));
        edges.AddRange(cases.Select(item => Direct(item.NodeId, "end")));
        edges.Add(Direct(defaultNodeId, "end"));
        return Graph(nodes, edges);
    }

    private static WorkflowGraph BuildFanOutOnlyGraph(
        WorkflowComponentId componentId,
        string llmId,
        string llmName,
        IReadOnlyList<(string Target, string NodeName)> targets)
    {
        var nodes = new List<WorkflowNode>
        {
            Start(),
            Llm(llmId, llmName, componentId, 340, 220),
            Decision("fanout", "FAN-OUT", 620, 220)
        };
        nodes.AddRange(targets.Select((item, index) => Logic($"{item.Target}-branch", item.NodeName, 900, 50 + (index * 140))));
        nodes.Add(End(1180, 260));

        var edges = new List<WorkflowEdge>
        {
            Direct("start", llmId),
            Direct(llmId, "fanout")
        };
        edges.AddRange(targets.Select((item, index) => FanOut("fanout", $"{item.Target}-branch", "$.targets", item.Target, index, ToLabel(item.Target))));
        edges.AddRange(targets.Select(item => Direct($"{item.Target}-branch", "end")));
        return Graph(nodes, edges);
    }

    private static WorkflowGraph Graph(IReadOnlyList<WorkflowNode> nodes, params WorkflowEdge[] edges)
        => Graph(nodes, (IReadOnlyList<WorkflowEdge>)edges);

    private static WorkflowGraph Graph(IReadOnlyList<WorkflowNode> nodes, IReadOnlyList<WorkflowEdge> edges)
        => new(new WorkflowNodeId("start"), nodes, edges);

    private static WorkflowNode Start()
        => Node("start", WorkflowNodeKind.Start, "Start", "Accept the workflow input payload.", 80, 220);

    private static WorkflowNode End(double x, double y)
        => Node("end", WorkflowNodeKind.End, "End", "Return the final workflow payload.", x, y);

    private static WorkflowNode Llm(
        string id,
        string name,
        WorkflowComponentId componentId,
        double x,
        double y)
        => Node(
            id,
            WorkflowNodeKind.LlmCall,
            name,
            "Run the prepared LLM component and return the strict JSON contract.",
            x,
            y,
            componentId);

    private static WorkflowNode Decision(string id, string name, double x, double y)
        => Node(
            id,
            WorkflowNodeKind.Triage,
            name,
            "Route deterministic JSON branch values.",
            x,
            y);

    private static WorkflowNode Logic(string id, string name, double x, double y)
        => Node(
            id,
            WorkflowNodeKind.StrictLogic,
            name,
            "Apply deterministic branch handling for this workflow outcome.",
            x,
            y);

    private static WorkflowNode Human(string id, string name, double x, double y)
        => Node(
            id,
            WorkflowNodeKind.HumanInput,
            name,
            "Request explicit review before this workflow continues.",
            x,
            y,
            externalRequestKind: WorkflowExternalRequestKind.Approval);

    private static WorkflowNode StorageWrite(string id, string name, double x, double y, string path)
        => Executor(
            id,
            name,
            x,
            y,
            WorkflowExecutorIds.StorageFile,
            new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.WriteText,
                Path = path,
                ContentFromInput = true,
                Overwrite = true
            },
            "Write the workflow JSON payload to a workspace file.");

    private static WorkflowNode SourceIngest(string id, string name, double x, double y)
        => Executor(
            id,
            name,
            x,
            y,
            WorkflowExecutorIds.SourceIngestion,
            new WorkflowSourceIngestionExecutorSettings
            {
                IncludeAdditionalSources = true,
                IncludeParentNodePath = true,
                IncludeSelectedNodePaths = true,
                IncludeParentSubtreePaths = true,
                RecursiveFolders = true,
                AllowAbsoluteInputPaths = true,
                MaxFiles = 16,
                MaxCharactersPerFile = 14000,
                MaxTotalCharacters = 90000
            },
            "Load explicit project-structure file and folder sources into bounded text before the LLM summarizes them.");

    private static WorkflowNode HttpFetch(string id, string name, double x, double y)
        => Executor(
            id,
            name,
            x,
            y,
            WorkflowExecutorIds.HttpFetch,
            new WorkflowHttpExecutorSettings
            {
                Method = WorkflowHttpMethodKind.Get,
                UrlJsonPath = "$.url",
                MaxResponseBytes = 262144,
                IncludeInputPayload = true
            },
            "Fetch bounded HTTP content from the URL in the workflow input.");

    private static WorkflowNode SpreadsheetRead(
        string id,
        string name,
        double x,
        double y,
        string workbookPath,
        string worksheet,
        string range)
        => Executor(
            id,
            name,
            x,
            y,
            WorkflowExecutorIds.Spreadsheet,
            new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.RangeToMarkdown,
                WorkbookPath = workbookPath,
                WorksheetName = worksheet,
                RangeAddress = range,
                MaxRows = 50,
                MaxColumns = 12
            },
            "Read a bounded XLSX range for downstream LLM classification.");

    private static WorkflowNode SpreadsheetWrite(
        string id,
        string name,
        double x,
        double y,
        string value)
        => Executor(
            id,
            name,
            x,
            y,
            WorkflowExecutorIds.Spreadsheet,
            new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.WriteCell,
                WorkbookPath = "samples/workflows/invoices.xlsx",
                OutputWorkbookPath = "samples/workflows/invoices-reviewed.xlsx",
                WorksheetName = "Invoices",
                CellAddress = "G2",
                Value = value,
                CreateWorkbookIfMissing = false,
                Overwrite = true
            },
            "Write a branch decision to an output XLSX workbook.");

    private static WorkflowNode ProjectAsset(string id, string name, double x, double y, string title)
        => Executor(
            id,
            name,
            x,
            y,
            WorkflowExecutorIds.ProjectStructure,
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateAsset,
                ProjectIdJsonPath = "$.projectId",
                NodeIdJsonPath = "$.nodeId",
                Title = title,
                AssetKind = "md",
                ContentFromInput = true,
                ContentType = "text/markdown"
            },
            "Create a markdown asset in the selected project structure.");

    private static WorkflowNode ProjectTaskNodes(string id, string name, double x, double y)
        => Executor(
            id,
            name,
            x,
            y,
            WorkflowExecutorIds.ProjectStructure,
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
                ProjectIdJsonPath = "$.projectId",
                NodeIdJsonPath = "$.nodeId",
                TaskItemsJsonPath = "$.tasks",
                TaskObjectSubtype = "task",
                MaxTaskNodes = 12
            },
            "Create WorkItem/task nodes under the workflow node from the LLM tasks array.");

    private static WorkflowNode Executor<TSettings>(
        string id,
        string name,
        double x,
        double y,
        WorkflowExecutorId executorId,
        TSettings settings,
        string instructions)
        => Node(
            id,
            WorkflowNodeKind.Executor,
            name,
            instructions,
            x,
            y,
            executorId: executorId,
            executorSettingsJson: JsonSerializer.Serialize(settings, JsonOptions),
            executionPolicy: SlowExecutorPolicy);

    private static WorkflowNode Node(
        string id,
        WorkflowNodeKind kind,
        string name,
        string instructions,
        double x,
        double y,
        WorkflowComponentId? componentId = null,
        WorkflowExternalRequestKind? externalRequestKind = null,
        WorkflowExecutorId? executorId = null,
        string executorSettingsJson = "",
        WorkflowExecutorExecutionPolicy? executionPolicy = null)
        => new(
            new WorkflowNodeId(id),
            kind,
            name,
            BuildPorts(kind),
            new WorkflowNodeSettings(
                ComponentId: componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: externalRequestKind,
                Instructions: instructions,
                InputShape: kind == WorkflowNodeKind.Start ? null : JsonShape,
                ResultShape: kind == WorkflowNodeKind.End ? null : JsonShape)
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = executorSettingsJson,
                ExecutionPolicy = executionPolicy ?? (executorId is null ? null : FastExecutorPolicy)
            },
            x,
            y);

    private static IReadOnlyList<WorkflowPort> BuildPorts(WorkflowNodeKind kind)
    {
        var ports = new List<WorkflowPort>();
        if (kind != WorkflowNodeKind.Start)
        {
            ports.Add(new WorkflowPort(
                new WorkflowPortId("workflow:input"),
                "Input",
                WorkflowPortDirection.Input,
                JsonShape,
                Required: true));
        }

        if (kind != WorkflowNodeKind.End)
        {
            ports.Add(new WorkflowPort(
                new WorkflowPortId("workflow:output"),
                "Output",
                WorkflowPortDirection.Output,
                JsonShape,
                Required: true));
        }

        return ports;
    }

    private static WorkflowEdge Direct(string source, string target)
        => Edge(source, target, WorkflowEdgeKind.Direct, WorkflowEdgeRouting.Always);

    private static WorkflowEdge Predicate(
        string source,
        string target,
        string jsonPath,
        WorkflowRouteOperator @operator,
        string expectedValueJson,
        WorkflowRouteValueKind expectedValueKind,
        string label)
        => Edge(
            source,
            target,
            WorkflowEdgeKind.Conditional,
            WorkflowEdgeRouting.Predicate(jsonPath, @operator, expectedValueJson, expectedValueKind, label));

    private static WorkflowEdge Switch(
        string source,
        string target,
        string jsonPath,
        string value,
        string label)
        => Edge(
            source,
            target,
            WorkflowEdgeKind.Conditional,
            WorkflowEdgeRouting.SwitchCase(jsonPath, JsonSerializer.Serialize(value), WorkflowRouteValueKind.String, label));

    private static WorkflowEdge SwitchDefault(string source, string target, string label)
        => Edge(
            source,
            target,
            WorkflowEdgeKind.Conditional,
            WorkflowEdgeRouting.SwitchDefault(label));

    private static WorkflowEdge FanOut(
        string source,
        string target,
        string jsonPath,
        string value,
        int targetIndex,
        string label)
        => Edge(
            source,
            target,
            WorkflowEdgeKind.FanOut,
            WorkflowEdgeRouting.FanOutSelector(
                jsonPath,
                WorkflowRouteOperator.Contains,
                JsonSerializer.Serialize(value),
                WorkflowRouteValueKind.String,
                targetIndex,
                label));

    private static WorkflowEdge Edge(
        string source,
        string target,
        WorkflowEdgeKind kind,
        WorkflowEdgeRouting routing)
        => new(
            new WorkflowEdgeId($"{source}-to-{target}"),
            new WorkflowNodeId(source),
            new WorkflowPortId("workflow:output"),
            new WorkflowNodeId(target),
            new WorkflowPortId("workflow:input"),
            kind,
            ConditionExpression: string.Empty)
        {
            Routing = routing
        };

    private static string ToLabel(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Case"
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
    }

    private const string ExampleResultSchema =
        """
        {
          "type": "object",
          "additionalProperties": true,
          "properties": {
            "route": { "type": "string" },
            "summary": { "type": "string" },
            "markdown": { "type": "string" },
            "actions": { "type": "array", "items": { "type": "string" } },
            "tasks": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": true,
                "properties": {
                  "title": { "type": "string" },
                  "summary": { "type": "string" },
                  "owner": { "type": "string" },
                  "dueUtc": { "type": "string" },
                  "urgency": { "type": "string" },
                  "requiresResponse": { "type": "boolean" },
                  "asap": { "type": "boolean" },
                  "sourceEmailId": { "type": "string" },
                  "evidence": { "type": "array", "items": { "type": "string" } }
                },
                "required": ["title", "summary", "owner", "dueUtc", "urgency", "requiresResponse", "asap", "sourceEmailId", "evidence"]
              }
            },
            "evidence": { "type": "array", "items": { "type": "string" } },
            "targets": { "type": "array", "items": { "type": "string" } },
            "emailCategory": { "type": "string" },
            "isInformational": { "type": "boolean" },
            "asapResponseRequired": { "type": "boolean" },
            "risk": { "type": "string" },
            "relevant": { "type": "boolean" },
            "needsReview": { "type": "boolean" },
            "requiresResponse": { "type": "boolean" },
            "ready": { "type": "boolean" },
            "projectId": { "type": "string" },
            "nodeId": { "type": "string" },
            "sourceUrl": { "type": "string" },
            "project": { "type": "object", "additionalProperties": true },
            "runContext": { "type": "object", "additionalProperties": true }
          },
          "required": ["route", "summary", "markdown", "actions", "targets", "risk", "relevant", "needsReview", "requiresResponse", "ready", "projectId", "nodeId", "sourceUrl"]
        }
        """;

    private sealed record WorkflowExampleSpec(
        string Name,
        string Description,
        string RoutingInstructions,
        Func<WorkflowComponentId, WorkflowGraph> BuildGraph);
}
