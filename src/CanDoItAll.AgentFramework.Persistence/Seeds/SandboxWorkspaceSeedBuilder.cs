using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class SandboxWorkspaceSeedBuilder
{
    private const string LatestVersion = "3.0";
    private const string SeriousDeliveryManagedSeedVersion = "2026-05-generic-app-delivery-agents-v72";
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 4, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<string> OpenAiSuggestedModels =
    [
        ManagedSeedProviderFallbacks.OpenAiDefaultModel,
        "gpt-5.4-mini",
        "gpt-4.1-mini",
        "gpt-4.1"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static SandboxWorkspaceDocument Build()
    {
        var now = SeedTimestamp;

        var openAiProviderId = CreateStableGuid("providers/openai-default");
        var openAiChatProviderId = CreateStableGuid("providers/openai-chat-completions");
        var ollamaProviderId = CreateStableGuid("providers/ollama-local");

        var playwrightLocalMcpCapabilityId = CreateStableGuid("capabilities/playwright-local-mcp");
        var bundleWorkflowCapabilityId = CreateStableGuid("capabilities/candoitall-bundle-workflow");
        var aspNetCoreCapabilityId = CreateStableGuid("capabilities/aspnet-core-skill");
        var codeanalyticsCapabilityId = CreateStableGuid("capabilities/candoitall-codeanalytics-mcp");
        var componentsCapabilityId = CreateStableGuid("capabilities/candoitall-components-mcp");
        var frontendThemeCapabilityId = CreateStableGuid("capabilities/candoitall-frontend-theme");
        var frontendSkillCapabilityId = CreateStableGuid("capabilities/frontend-skill");
        var playwrightWorkflowCapabilityId = CreateStableGuid("capabilities/candoitall-watch-playwright-loop");
        var spreadsheetCapabilityId = CreateStableGuid("capabilities/spreadsheet-skill");
        var runTestsCapabilityId = CreateStableGuid("capabilities/run-tests-skill");
        var mstestCapabilityId = CreateStableGuid("capabilities/writing-mstest-tests");
        var repositoryPlaybookCapabilityId = CreateStableGuid("capabilities/repository-playbook");
        var mailInlineSkillCapabilityId = CreateStableGuid("capabilities/mail-triage-inline-skill");
        var documentSpreadsheetReconciliationInlineSkillCapabilityId = CreateStableGuid("capabilities/document-spreadsheet-reconciliation-inline-skill");
        var concreteDeliverableDeliveryInlineSkillCapabilityId = CreateStableGuid("capabilities/concrete-deliverable-delivery-inline-skill");
        var dotnetAppDeliveryInlineSkillCapabilityId = CreateStableGuid("capabilities/dotnet-app-delivery-inline-skill");
        var blazorSsrDeliveryInlineSkillCapabilityId = CreateStableGuid("capabilities/blazor-ssr-delivery-inline-skill");
        var appSummaryInlineSkillCapabilityId = CreateStableGuid("capabilities/generated-app-summary-inline-skill");
        var architectureReviewInlineSkillCapabilityId = CreateStableGuid("capabilities/architecture-review-inline-skill");
        var architectureInlineSkillCapabilityId = CreateStableGuid("capabilities/architecture-map-inline-skill");
        var mailSummaryInlineSkillCapabilityId = CreateStableGuid("capabilities/mail-summary-inline-skill");
        var providerHealthCapabilityId = CreateStableGuid("capabilities/provider-health");
        var exportCapabilityId = CreateStableGuid("capabilities/agent-package-export");
        var providerNativeCodeInterpreterCapabilityId = CreateStableGuid("capabilities/provider-native-code-interpreter");
        var providerNativeFileSearchCapabilityId = CreateStableGuid("capabilities/provider-native-file-search");
        var providerNativeWebSearchCapabilityId = CreateStableGuid("capabilities/provider-native-web-search");
        var workspaceListFilesCapabilityId = CreateStableGuid("capabilities/workspace-list-files");
        var workspaceSearchCapabilityId = CreateStableGuid("capabilities/workspace-search");
        var workspaceReadCapabilityId = CreateStableGuid("capabilities/workspace-read-file");
        var workspaceStatPathCapabilityId = CreateStableGuid("capabilities/workspace-stat-path");
        var workspaceCreateDirectoryCapabilityId = CreateStableGuid("capabilities/workspace-create-directory");
        var workspaceWriteFileCapabilityId = CreateStableGuid("capabilities/workspace-write-file");
        var workspaceAppendFileCapabilityId = CreateStableGuid("capabilities/workspace-append-file");
        var workspaceCopyPathCapabilityId = CreateStableGuid("capabilities/workspace-copy-path");
        var workspaceMovePathCapabilityId = CreateStableGuid("capabilities/workspace-move-path");
        var workspaceDeletePathCapabilityId = CreateStableGuid("capabilities/workspace-delete-path");
        var workspaceDiffTextCapabilityId = CreateStableGuid("capabilities/workspace-diff-text");
        var workspaceExecutionBoundaryCapabilityId = CreateStableGuid("capabilities/workspace-execution-boundary");
        var workspaceGitStatusCapabilityId = CreateStableGuid("capabilities/workspace-git-status");
        var workspaceGitDiffCapabilityId = CreateStableGuid("capabilities/workspace-git-diff");
        var workspaceDotnetRestoreCapabilityId = CreateStableGuid("capabilities/workspace-dotnet-restore");
        var workspaceDotnetBuildCapabilityId = CreateStableGuid("capabilities/workspace-dotnet-build");
        var workspaceDotnetTestCapabilityId = CreateStableGuid("capabilities/workspace-dotnet-test");
        var workspaceDotnetRunCapabilityId = CreateStableGuid("capabilities/workspace-dotnet-run");
        var workspaceDotnetNewCapabilityId = CreateStableGuid("capabilities/workspace-dotnet-new");
        var workspacePythonRunFileCapabilityId = CreateStableGuid("capabilities/workspace-python-run-file");
        var workspacePwshRunScriptCapabilityId = CreateStableGuid("capabilities/workspace-pwsh-run-script");
        var workspaceConvertDocumentCapabilityId = CreateStableGuid("capabilities/workspace-convert-document");
        var workspaceInspectSpreadsheetCapabilityId = CreateStableGuid("capabilities/workspace-inspect-spreadsheet");
        var localDocsCapabilityId = CreateStableGuid("capabilities/workspace-source-rag");
        var architectureDocsCapabilityId = CreateStableGuid("capabilities/architecture-source-rag");
        var mailContextCapabilityId = CreateStableGuid("capabilities/mail-triage-context");
        var researchContextCapabilityId = CreateStableGuid("capabilities/research-briefing-context");
        var mem0CapabilityId = CreateStableGuid("capabilities/mem0-shared-memory");

        var architectAgentId = CreateStableGuid("agents/portfolio-architect");
        var qaAgentId = CreateStableGuid("agents/delivery-qa-observer");
        var programmingAgentId = CreateStableGuid("agents/programming-workspace-analyst");
        var codeReviewAgentId = CreateStableGuid("agents/code-review-lead");
        var uiReviewAgentId = CreateStableGuid("agents/ui-review-lead");
        var securityReviewerAgentId = CreateStableGuid("agents/security-reviewer");
        var releaseManagerAgentId = CreateStableGuid("agents/release-readiness-manager");
        var hrStaffingManagerAgentId = CreateStableGuid("agents/hr-staffing-manager");
        var spreadsheetAgentId = CreateStableGuid("agents/spreadsheet-analyst");
        var mailAgentId = CreateStableGuid("agents/mail-triage-analyst");
        var researchAgentId = CreateStableGuid("agents/research-deep-dive-analyst");
        var dotnetArchitectAgentId = CreateStableGuid("agents/dotnet-solution-architect");
        var dotnetDeveloperAgentId = CreateStableGuid("agents/dotnet-application-developer");
        var blazorDeveloperAgentId = CreateStableGuid("agents/blazor-application-developer");
        var dotnetQaAgentId = CreateStableGuid("agents/dotnet-qa-review-lead");
        var javascriptArchitectAgentId = CreateStableGuid("agents/javascript-solution-architect");
        var javascriptDeveloperAgentId = CreateStableGuid("agents/javascript-application-developer");
        var javascriptQaAgentId = CreateStableGuid("agents/javascript-qa-review-lead");
        var businessStrategistAgentId = CreateStableGuid("agents/business-strategist");
        var financialStrategistAgentId = CreateStableGuid("agents/financial-strategist");
        var marketingSpecialistAgentId = CreateStableGuid("agents/marketing-specialist");
        var sessionId = CreateStableGuid("sessions/integration-target-summary");
        var capabilities = new List<CapabilityCatalogItem>
        {
            new(
                playwrightLocalMcpCapabilityId,
                CapabilityKind.McpServer,
                "playwright-local-mcp",
                "Playwright Local MCP",
                "Local Playwright MCP for browser control, console inspection, and screenshot-backed UI proof inside agent workspaces.",
                "npx",
                SerializeConfiguration(new
                {
                    transport = "stdio",
                    serverName = "playwright-local",
                    command = "npx",
                    arguments = new[]
                    {
                        "@playwright/mcp@latest",
                        "--headless",
                        "--caps",
                        "vision",
                        "--ignore-https-errors",
                        "--isolated"
                    },
                    workingDirectory = ".",
                    allowedTools = new[]
                    {
                        "browser_navigate",
                        "browser_snapshot",
                        "browser_console_messages",
                        "browser_take_screenshot",
                        "browser_click",
                        "browser_type",
                        "browser_fill_form",
                        "browser_wait_for",
                        "browser_select_option",
                        "browser_hover",
                        "browser_press_key",
                        "browser_evaluate",
                        "browser_resize",
                        "browser_close"
                    },
                    approvalMode = "NeverRequire"
                }),
                CapabilityProofStatus.NotRun,
                "Seeded built-in for UI-capable delivery agents. Runtime proof should confirm browser navigation, screenshot capture, and console evidence through real agent execution.",
                null,
                true),
            CreateFileSkillCapability(
                bundleWorkflowCapabilityId,
                "candoitall-bundle-workflow",
                "Bundle Workflow Skill",
                "Research and execution skill for complex delivery initiatives.",
                GetSeedSkillRoot("candoitall-bundle-workflow"),
                "Seeded from the active Codex skill registry. Local proof has not been run yet."),
            CreateFileSkillCapability(
                aspNetCoreCapabilityId,
                "aspnet-core-skill",
                "ASP.NET Core Skill",
                "Framework guidance for web, API, Blazor, hosting, and ASP.NET Core debugging tasks.",
                GetSeedSkillRoot("aspnet-core"),
                "Seeded from the shared Codex skill registry for programming-oriented workflows."),
            CreateFileSkillCapability(
                codeanalyticsCapabilityId,
                "candoitall-codeanalytics-mcp",
                "Codeanalytics MCP Skill",
                "Repository-guided C# architecture and symbol inspection through the CanDoItAll codeanalytics MCP.",
                GetSeedSkillRoot("candoitall-codeanalytics-mcp"),
                "Seeded from the shared Codex skill registry for architecture, review, and source-of-truth investigations."),
            CreateFileSkillCapability(
                componentsCapabilityId,
                "candoitall-components-mcp",
                "Components MCP Skill",
                "Guidance for using the shared CanDoItAll component library before falling back to raw Blazor markup.",
                GetSeedSkillRoot("candoitall-components-mcp"),
                "Seeded from the shared Codex skill registry for Blazor UI work and review."),
            CreateFileSkillCapability(
                frontendThemeCapabilityId,
                "candoitall-frontend-theme",
                "Frontend Theme Skill",
                "Guidance for intentional CanDoItAll frontend theming, composition, and semantic-token usage in Blazor delivery.",
                GetSeedSkillRoot("candoitall-frontend-theme"),
                "Seeded from the shared Codex skill registry for serious Blazor delivery and UI review."),
            CreateFileSkillCapability(
                frontendSkillCapabilityId,
                "frontend-skill",
                "Frontend Delivery Skill",
                "Guidance for strong, intentional product surfaces that avoid generic scaffold output in UI delivery.",
                GetSeedSkillRoot("frontend-skill"),
                "Seeded from the shared Codex skill registry for product-facing UI implementation, QA, and UI review."),
            CreateFileSkillCapability(
                playwrightWorkflowCapabilityId,
                "candoitall-watch-playwright-loop",
                "Watch And Playwright Loop Skill",
                "Guidance for fast, real-browser validation with Playwright and the shared CanDoItAll watch workflow.",
                GetSeedSkillRoot("candoitall-watch-playwright-loop"),
                "Seeded from the shared Codex skill registry for UI delivery, QA, and release smoke validation."),
            CreateFileSkillCapability(
                spreadsheetCapabilityId,
                "spreadsheet-skill",
                "Spreadsheet Skill",
                "Guidance and assets for creating, reading, and analyzing spreadsheet content.",
                GetSeedSkillRoot("spreadsheet"),
                "Seeded from the shared Codex skill registry for spreadsheet-oriented workflows."),
            CreateFileSkillCapability(
                runTestsCapabilityId,
                "run-tests",
                "Run Tests Skill",
                "Guidance for selecting and executing the right focused dotnet test command for the active .NET test framework and runner.",
                GetSeedSkillRoot("run-tests"),
                "Seeded from the shared Codex skill registry for targeted build and test execution in serious delivery workflows."),
            CreateFileSkillCapability(
                mstestCapabilityId,
                "writing-mstest-tests",
                "Writing MSTest Tests Skill",
                "Guidance for writing targeted MSTest validation in C# delivery workflows.",
                GetSeedSkillRoot("writing-mstest-tests"),
                "Seeded from the shared Codex skill registry for implementation and QA validation work."),
            CreateFileSkillCapability(
                repositoryPlaybookCapabilityId,
                "repository-playbook",
                "Repository Playbook",
                "Local workspace skill with references and scripts for delivery-oriented repository work.",
                GetSeedSkillRoot("repository-playbook"),
                "Repo-local file skill that demonstrates Microsoft Agent Framework file-skill loading over workspace assets."),
            new(
                mailInlineSkillCapabilityId,
                CapabilityKind.Skill,
                "mail-triage-inline-skill",
                "Mail Triage Inline Skill",
                "Inline skill instructions for summarizing inbox threads, classifying urgency, and drafting replies.",
                "inline://mail-triage-inline-skill",
                SerializeConfiguration(new
                {
                    skillSource = "inline",
                    inlineSkill = new
                    {
                        name = "mail-triage-inline",
                        description = "Classify urgency, surface follow-ups, and draft a concise reply.",
                        instructions = GetSeedText("skills/mail-triage-inline.instructions"),
                        resources = new[]
                        {
                            new
                            {
                                name = "mail-source-path",
                                content = "Seeded mail fixture: samples/workloads/support-inbox.md",
                                description = "Workspace path for the sample inbox thread."
                            }
                        }
                    }
                }),
                CapabilityProofStatus.NotRun,
                "Inline skill support proves that the wrapper can carry in-memory Microsoft Agent Framework skill definitions without inventing a parallel skill system.",
                null,
                true),
            CreateInlineSkillCapability(
                documentSpreadsheetReconciliationInlineSkillCapabilityId,
                "document-spreadsheet-reconciliation-inline-skill",
                "Document and Spreadsheet Reconciliation Skill",
                "Reusable workflow for reconciling records and facts across documents, spreadsheets, CSV files, and other structured artifacts.",
                "document-spreadsheet-reconciliation",
                GetSeedText("skills/document-spreadsheet-reconciliation.instructions"),
                [
                    new InlineSkillResourceSeed(
                        "reconciliation-output-example",
                        BuildReconciliationOutputExample(),
                        "Generic JSON response shape for reconciliation tasks.")
                ]),
            CreateInlineSkillCapability(
                concreteDeliverableDeliveryInlineSkillCapabilityId,
                "concrete-deliverable-delivery-inline-skill",
                "Concrete Deliverable Delivery Skill",
                "Technology-neutral workflow for creating, validating, and proving apps, services, documents, spreadsheets, decks, scripts, and other durable deliverables.",
                "concrete-deliverable-delivery",
                GetSeedText("skills/concrete-deliverable-delivery.instructions")),
            CreateInlineSkillCapability(
                dotnetAppDeliveryInlineSkillCapabilityId,
                "dotnet-app-delivery-inline-skill",
                ".NET App Delivery Skill",
                "Reusable workflow guidance for scaffolding, building, testing, running, and proving generic .NET applications.",
                "dotnet-app-delivery",
                GetSeedText("skills/dotnet-app-delivery.instructions")),
            CreateInlineSkillCapability(
                blazorSsrDeliveryInlineSkillCapabilityId,
                "blazor-ssr-delivery-inline-skill",
                "Blazor App Delivery Skill",
                "Reusable workflow guidance for creating, repairing, running, and validating Blazor Web App and component-driven ASP.NET Core UI deliverables.",
                "blazor-ssr-delivery",
                GetSeedText("skills/blazor-ssr-delivery.instructions"),
                [
                    new InlineSkillResourceSeed(
                        "net10-program-scaffold",
                        BuildBlazorProgramExample(),
                        "Reference shape for the modern .NET 10 Blazor Web App Program.cs scaffold."),
                    new InlineSkillResourceSeed(
                        "net10-home-page-example",
                        BuildBlazorHomeExample(),
                        "Reference shape for a static-SSR page using query parameters and a GET form."),
                    new InlineSkillResourceSeed(
                        "dotnet-command-examples",
                        BuildBlazorCommandExamples(),
                        "Concrete workspace_dotnet_new, workspace_dotnet_build, workspace_dotnet_test, and workspace_dotnet_run examples for generic .NET and Blazor app delivery.")
                ]),
            CreateInlineSkillCapability(
                appSummaryInlineSkillCapabilityId,
                "generated-app-summary-inline-skill",
                "Generated App Summary Skill",
                "Workflow for summarizing a generated application or runnable deliverable from concrete files and validation receipts.",
                "generated-app-summary",
                GetSeedText("skills/generated-app-summary.instructions"),
                [
                    new InlineSkillResourceSeed(
                        "summary-checklist",
                        BuildGeneratedAppSummaryChecklist(),
                        "Required facts to include in the generated-app summary.")
                ]),
            CreateInlineSkillCapability(
                architectureReviewInlineSkillCapabilityId,
                "architecture-review-inline-skill",
                "Architecture Review Skill",
                "Task-specific workflow for reviewing the CanDoItAll.AgentFramework architecture from real source files.",
                "architecture-review",
                GetSeedText("skills/architecture-review.instructions")),
            CreateInlineSkillCapability(
                architectureInlineSkillCapabilityId,
                "architecture-map-inline-skill",
                "Architecture Map Skill",
                "Workflow for creating a Mermaid class diagram from generated app source files.",
                "architecture-map",
                GetSeedText("skills/architecture-map.instructions")),
            CreateInlineSkillCapability(
                mailSummaryInlineSkillCapabilityId,
                "mail-summary-inline-skill",
                "Mail Summary Skill",
                "Task-specific workflow for summarizing the provided email PDF and extracting participant-owned tasks.",
                "mail-summary",
                GetSeedText("skills/mail-summary.instructions")),
            CreateToolCapability(providerHealthCapabilityId, "provider-health", "Provider Health Check", "Operational tool for verifying connectivity and available models.", "provider_health"),
            CreateToolCapability(exportCapabilityId, "agent-package-export", "Agent Package Export", "Creates readable zip bundles with json, markdown, and text artifacts.", "agent_package_export"),
            CreateToolCapability(providerNativeCodeInterpreterCapabilityId, "provider-native-code-interpreter", "Provider-Native Code Interpreter", "Attaches the hosted code interpreter tool for Responses-backed OpenAI or Azure OpenAI providers so analysis can avoid unnecessary local shell work.", ProviderNativeToolKeys.CodeInterpreter),
            CreateToolCapability(providerNativeFileSearchCapabilityId, "provider-native-file-search", "Provider-Native File Search", "Attaches the hosted file search tool for Responses-backed providers when provider-managed indexes are available.", ProviderNativeToolKeys.FileSearch, additionalConfiguration: new { maximumResultCount = 8 }),
            CreateToolCapability(providerNativeWebSearchCapabilityId, "provider-native-web-search", "Provider-Native Web Search", "Attaches the hosted web search tool for Responses-backed providers instead of inventing a local search wrapper.", ProviderNativeToolKeys.WebSearch),
            CreateToolCapability(workspaceListFilesCapabilityId, "workspace-list-files", "Workspace List Files", "Lists files and directories from the managed workspace or a grounded external-target alias. Supports simple patterns and recursive globstar patterns such as **/* and **/*.cs. In external-target process runs, broad managed-root browsing is denied; list current-run artifacts or the grounded product alias instead.", "workspace_list_files"),
            CreateToolCapability(workspaceSearchCapabilityId, "workspace-search", "Workspace Search", "Searches text across the current CanDoItAll.AgentFramework workspace for implementation clues; external-target paths require explicit current-run grounding. In external-target process runs, broad managed-root search is denied; search current-run artifacts or the grounded product alias instead.", "workspace_search"),
            CreateToolCapability(workspaceReadCapabilityId, "workspace-read-file", "Workspace Read File", "Reads text files from the managed workspace or a grounded external-target alias so agents can inspect artifacts and concrete deliverables on demand. In external-target process runs, do not read unmanaged source or helper roots unless they are current-run artifacts.", "workspace_read_file"),
            CreateToolCapability(workspaceStatPathCapabilityId, "workspace-stat-path", "Workspace Stat Path", "Returns path metadata such as kind, existence, and size for a managed workspace path or grounded external-target alias. In external-target process runs, prefer current-run artifacts and the grounded product alias.", "workspace_stat_path"),
            CreateToolCapability(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", "Workspace Create Directory", "Creates a directory in the managed workspace or in a grounded external-target alias before generated artifacts or deliverables are written. In external-target process runs, product source, tests, scripts, and assets must stay under the grounded product alias or current-run artifact folders.", "workspace_create_directory", approvalRequired: true),
            CreateToolCapability(workspaceWriteFileCapabilityId, "workspace-write-file", "Workspace Write File", "Creates or overwrites a text file in the managed workspace or in a grounded external-target alias. In external-target process runs, product source and tests must be written under the grounded product alias, not managed src/tests/tools roots.", "workspace_write_file", approvalRequired: true),
            CreateToolCapability(workspaceAppendFileCapabilityId, "workspace-append-file", "Workspace Append File", "Appends text to an existing workspace file or creates it when needed. In external-target process runs, product source and tests must be written under the grounded product alias, not managed src/tests/tools roots.", "workspace_append_file", approvalRequired: true),
            CreateToolCapability(workspaceCopyPathCapabilityId, "workspace-copy-path", "Workspace Copy Path", "Copies a file or directory inside the current workspace with receipt generation.", "workspace_copy_path", approvalRequired: true),
            CreateToolCapability(workspaceMovePathCapabilityId, "workspace-move-path", "Workspace Move Path", "Moves or renames a file or directory inside the current workspace with receipt generation.", "workspace_move_path", approvalRequired: true),
            CreateToolCapability(workspaceDeletePathCapabilityId, "workspace-delete-path", "Workspace Delete Path", "Deletes a file or directory inside the current workspace with receipt generation.", "workspace_delete_path", approvalRequired: true),
            CreateToolCapability(workspaceDiffTextCapabilityId, "workspace-diff-text", "Workspace Diff Text", "Shows a bounded line-level diff between two workspace text files.", "workspace_diff_text"),
            CreateToolCapability(workspaceExecutionBoundaryCapabilityId, "workspace-execution-boundary", "Workspace Execution Boundary", "Reports the actual tool-execution isolation mode and host enforcement guarantees.", "workspace_execution_boundary"),
            CreateToolCapability(workspaceGitStatusCapabilityId, "workspace-git-status", "Workspace Git Status", "Runs a bounded git status recipe in the current workspace.", "workspace_git_status"),
            CreateToolCapability(workspaceGitDiffCapabilityId, "workspace-git-diff", "Workspace Git Diff", "Runs a bounded git diff recipe in the current workspace.", "workspace_git_diff"),
            CreateToolCapability(workspaceDotnetRestoreCapabilityId, "workspace-dotnet-restore", "Workspace Dotnet Restore", "Runs a bounded dotnet restore recipe in the managed workspace or a grounded external-target alias.", "workspace_dotnet_restore", approvalRequired: true),
            CreateToolCapability(workspaceDotnetBuildCapabilityId, "workspace-dotnet-build", "Workspace Dotnet Build", "Runs a bounded dotnet build recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying.", "workspace_dotnet_build"),
            CreateToolCapability(workspaceDotnetTestCapabilityId, "workspace-dotnet-test", "Workspace Dotnet Test", "Runs a bounded dotnet test recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying.", "workspace_dotnet_test"),
            CreateToolCapability(workspaceDotnetRunCapabilityId, "workspace-dotnet-run", "Workspace Dotnet Run", "Runs a bounded dotnet run recipe or loopback HTTP startup smoke in the managed workspace or a grounded external-target alias, with durable launch evidence. HTTP smoke stops the launched process tree by default; use keepAlive true only for same-step browser proof and stop it with the recorded startup.json stopCommand before finalizing. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying.", "workspace_dotnet_run"),
            CreateToolCapability(workspaceDotnetNewCapabilityId, "workspace-dotnet-new", "Workspace Dotnet New", "Creates an approved dotnet project scaffold in the managed workspace or a grounded external-target alias. Use approved current SDK template names and inspect an unsuccessful result before retrying. For an exact output root, pass its parent as parentDirectory and the root leaf as name. For test projects, pass a parentDirectory under the grounded product root, such as <product-root>/tests, with name <AppName>.Tests; never reuse the product parent to create <AppName>.Tests as a sibling. Keep tests and support projects under child folders of the grounded product root unless another root is explicitly grounded. Do not scaffold product or test projects into managed src/tests/tools roots or sibling external-target roots during an external-target run.", "workspace_dotnet_new", approvalRequired: true),
            CreateToolCapability(workspacePythonRunFileCapabilityId, "workspace-python-run-file", "Workspace Python Run File", "Runs a workspace Python file through the controlled execution plane with durable receipts.", "workspace_python_run_file", approvalRequired: true),
            CreateToolCapability(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", "Workspace PowerShell Run Script", "Runs a workspace PowerShell script in non-interactive mode through the controlled execution plane.", "workspace_pwsh_run_script", approvalRequired: true),
            CreateToolCapability(workspaceConvertDocumentCapabilityId, "workspace-convert-document", "Workspace Convert Document", "Converts a workspace document such as a PDF into markdown using markitdown.", "workspace_convert_document", approvalRequired: true),
            CreateToolCapability(workspaceInspectSpreadsheetCapabilityId, "workspace-inspect-spreadsheet", "Workspace Inspect Spreadsheet", "Inspects a workspace .xls, .xlsx, .csv, or .tsv file and returns a compact preview.", "workspace_inspect_spreadsheet", approvalRequired: true),
            new(
                localDocsCapabilityId,
                CapabilityKind.Rag,
                "workspace-source-rag",
                "Workspace Source RAG",
                "Injects local source, skills, and sample workload files while excluding planning bundles and generated runtime artifacts.",
                ".",
                SerializeConfiguration(new
                {
                    ragRoot = ".",
                    extensions = new[] { ".cs", ".md", ".razor", ".json", ".ps1", ".yaml", ".yml", ".csv", ".tsv", ".txt", ".eml" },
                    excludePaths = WorkspaceRetrievalNoisePolicy.BuildSeedWorkspaceRagExcludedPaths(),
                    searchTime = "BeforeAIInvoke",
                    maxResults = 5
                }),
                CapabilityProofStatus.NotRun,
                string.Empty,
                null,
                true),
            new(
                architectureDocsCapabilityId,
                CapabilityKind.Rag,
                "architecture-source-rag",
                "Architecture Source RAG",
                "Injects only source and project-definition files for architecture reviews so persisted chat data and generated artifacts do not dominate the context.",
                ".",
                SerializeConfiguration(new
                {
                    ragRoot = ".",
                    extensions = new[] { ".cs", ".razor", ".csproj", ".sln", ".slnx", ".props", ".targets" },
                    excludePaths = new[] { "data", "artifacts", "output", ".playwright-cli", ".vs", "tools", "src/CanDoItAll.AgentFramework.Sandbox/Components/Pages" },
                    searchTime = "BeforeAIInvoke",
                    maxResults = 4
                }),
                CapabilityProofStatus.NotRun,
                string.Empty,
                null,
                true),
            CreateAiContextCapability(mailContextCapabilityId, "mail-triage-context", "Mail Triage Context", "System reminder that mail work should classify urgency, next actions, and reply drafts clearly.", "When handling mail-oriented work, identify urgency, the next concrete action, and a draft reply before you close the task."),
            CreateAiContextCapability(researchContextCapabilityId, "research-briefing-context", "Research Briefing Context", "System reminder to cite concrete repo evidence and distinguish proven behavior from inference.", "When researching or comparing options, cite the concrete provider, skill, route, capability, or file you used before making product-level claims."),
            new(
                mem0CapabilityId,
                CapabilityKind.Memory,
                "mem0-shared-memory",
                "Mem0 Shared Memory",
                "Optional Mem0Provider integration for external memory once credentials are available.",
                "https://api.mem0.ai",
                SerializeConfiguration(new { provider = "mem0", endpoint = "https://api.mem0.ai", apiKeyEnvironmentVariable = "MEM0_API_KEY", applicationId = "CanDoItAll.AgentFramework", agentId = "{agentId}" }),
                CapabilityProofStatus.NotRun,
                "Configured as an optional external memory provider. Live proof requires MEM0_API_KEY.",
                null,
                true)
        };

        var providers = new List<ProviderProfile>
        {
            new(
                openAiProviderId,
                "OpenAI default",
                ProviderKind.OpenAi,
                "https://api.openai.com/v1",
                "OPENAI_API_KEY",
                ManagedSeedProviderFallbacks.OpenAiDefaultModel,
                ProviderTransportKind.Responses,
                true,
                true,
                true,
                false,
                true,
                CreateOpenAiProviderConfigurationJson("service-managed"),
                "Responses profile for hosted routes, DevUI, and background-response scenarios.",
                "Not checked",
                null,
                OpenAiSuggestedModels),
            new(
                openAiChatProviderId,
                "OpenAI chat completions",
                ProviderKind.OpenAi,
                "https://api.openai.com/v1",
                "OPENAI_API_KEY",
                ManagedSeedProviderFallbacks.OpenAiDefaultModel,
                ProviderTransportKind.ChatCompletions,
                true,
                true,
                true,
                true,
                false,
                SerializeConfiguration(new { history = "framework-managed" }),
                "Chat-completions profile for local history, approvals, compaction, and workload-specific skill runs.",
                "Not checked",
                null,
                OpenAiSuggestedModels),
            new(
                ollamaProviderId,
                "Remote Ollama",
                ProviderKind.Ollama,
                "http://192.168.10.132:11434",
                string.Empty,
                "qwen3.5:9b",
                ProviderTransportKind.ChatCompletions,
                true,
                true,
                true,
                true,
                false,
                SerializeConfiguration(new { history = "framework-managed" }),
                "Targets the remote host validated during the latest Ollama repair and networking checks.",
                "Not checked",
                null,
                ["qwen3.5:9b", "gemma3-12b-128k:latest", "deepseek-r1:8b-32k", "qwen3.5:2b", "phi4-16k", "mistral-nemo"])
        };

        var architectAgent = new AgentDefinition(
            architectAgentId,
            "Portfolio Architect",
            "Architecture steward",
            "Owns architecture direction, source-of-truth choices, and bounded design guidance for governed delivery work.",
            GetSeedText("agents/portfolio-architect.instructions"),
            AgentLifecycleStatus.Active,
            openAiProviderId,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            AgentWorkloadKind.Research,
            AgentChatHistoryMode.ProviderDefault,
            0.2d,
            false,
            false,
            WithDefaultReasoningEffort(
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        maxLocalRagResults = 4,
                        hostedExposure = new
                        {
                            publish = true,
                            routeSegment = "portfolio-architect",
                            profile = "readonly-research"
                        }
                    }),
                    canRead: true,
                    canWrite: true,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.ArchitectureReview)),
            false,
            "portfolio-architect",
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, CanScheduleWork = true, AutoApproveExternalCallsByDefault = true },
            [
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceExecutionBoundaryCapabilityId, "workspace-execution-boundary", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(architectureDocsCapabilityId, "architecture-source-rag", CapabilityKind.Rag),
                CreateAssignment(researchContextCapabilityId, "research-briefing-context", CapabilityKind.AiContext),
                CreateAssignment(providerNativeWebSearchCapabilityId, "provider-native-web-search", CapabilityKind.Tool),
                CreateAssignment(providerHealthCapabilityId, "provider-health", CapabilityKind.Tool)
            ],
            ["architecture", "oversight", "integration"],
            now,
            now);

        var qaAgent = new AgentDefinition(
            qaAgentId,
            "Delivery QA Observer",
            "QA lead and browser-proof reviewer",
            "Owns validation depth, screenshot-backed browser proof, and explicit QA gating for governed delivery work.",
            GetSeedText("agents/delivery-qa-observer.instructions"),
            AgentLifecycleStatus.Active,
            openAiProviderId,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            AgentWorkloadKind.Qa,
            AgentChatHistoryMode.FrameworkManaged,
            0.1d,
            false,
            false,
            WithDefaultReasoningEffort(
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 4
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.QualityValidation)),
            false,
            "delivery-qa-observer",
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(runTestsCapabilityId, "run-tests", CapabilityKind.Skill),
                CreateAssignment(mstestCapabilityId, "writing-mstest-tests", CapabilityKind.Skill),
                CreateAssignment(dotnetAppDeliveryInlineSkillCapabilityId, "dotnet-app-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(blazorSsrDeliveryInlineSkillCapabilityId, "blazor-ssr-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetBuildCapabilityId, "workspace-dotnet-build", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetTestCapabilityId, "workspace-dotnet-test", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRunCapabilityId, "workspace-dotnet-run", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool)
            ],
            ["qa", "browser", "governance"],
            now,
            now);

        var programmingAgent = CreateWorkloadAgent(
            programmingAgentId,
            "Programming Workspace Analyst",
            "Programming and repository worker",
            "Implements C# and Blazor changes with bounded source inspection, concrete validation, and real UI proof when needed.",
            GetSeedText("agents/programming-workspace-analyst.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Programming,
            "programming-workspace-analyst",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        preferredSkillRoots = new[] { GetSeedSkillRoot("repository-playbook") },
                        enableCompaction = true,
                        slidingWindowTurns = 10,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(runTestsCapabilityId, "run-tests", CapabilityKind.Skill),
                CreateAssignment(mstestCapabilityId, "writing-mstest-tests", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(dotnetAppDeliveryInlineSkillCapabilityId, "dotnet-app-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(blazorSsrDeliveryInlineSkillCapabilityId, "blazor-ssr-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceCopyPathCapabilityId, "workspace-copy-path", CapabilityKind.Tool),
                CreateAssignment(workspaceMovePathCapabilityId, "workspace-move-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDeletePathCapabilityId, "workspace-delete-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceExecutionBoundaryCapabilityId, "workspace-execution-boundary", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRestoreCapabilityId, "workspace-dotnet-restore", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetBuildCapabilityId, "workspace-dotnet-build", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetTestCapabilityId, "workspace-dotnet-test", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRunCapabilityId, "workspace-dotnet-run", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetNewCapabilityId, "workspace-dotnet-new", CapabilityKind.Tool),
                CreateAssignment(workspacePythonRunFileCapabilityId, "workspace-python-run-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["programming", "workspace", "approval"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var codeReviewAgent = CreateWorkloadAgent(
            codeReviewAgentId,
            "Code Review Lead",
            "Code reviewer",
            "Reviews C# and Blazor changes for regressions, architectural drift, weak evidence, and reviewable quality.",
            GetSeedText("agents/code-review-lead.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "code-review-lead",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.QualityValidation),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(architectureDocsCapabilityId, "architecture-source-rag", CapabilityKind.Rag)
            ],
            ["review", "code", "quality"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var uiReviewAgent = CreateWorkloadAgent(
            uiReviewAgentId,
            "UI Review Lead",
            "UI reviewer",
            "Reviews rendered Blazor UI with Playwright, screenshot analysis, and component-library expectations.",
            GetSeedText("agents/ui-review-lead.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "ui-review-lead",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 4
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.QualityValidation),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool)
            ],
            ["ui", "review", "browser"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var securityReviewerAgent = CreateWorkloadAgent(
            securityReviewerAgentId,
            "Security Reviewer",
            "Security reviewer",
            "Reviews C# and Blazor changes for trust-boundary risk, hidden fallbacks, and explicit security posture.",
            GetSeedText("agents/security-reviewer.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "security-reviewer",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 4
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.SecurityReview),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(architectureDocsCapabilityId, "architecture-source-rag", CapabilityKind.Rag)
            ],
            ["security", "review", "risk"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var releaseManagerAgent = CreateWorkloadAgent(
            releaseManagerAgentId,
            "Release Readiness Manager",
            "Release readiness manager",
            "Synthesizes QA, security, rollback, and smoke-proof evidence into an explicit release-readiness decision.",
            GetSeedText("agents/release-readiness-manager.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Management,
            "release-readiness-manager",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 4
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.QualityValidation),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool)
            ],
            ["release", "readiness", "operations"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var spreadsheetAgent = CreateWorkloadAgent(
            spreadsheetAgentId,
            "Spreadsheet Analyst",
            "Spreadsheet and tabular-data worker",
            "Uses spreadsheet skill guidance plus framework-native tools to inspect workbook-like artifacts, compare purchasing documents, and call out concrete findings.",
            GetSeedText("agents/spreadsheet-analyst.instructions"),
            openAiChatProviderId,
            AgentWorkloadKind.Spreadsheet,
            "spreadsheet-analyst",
            WithWorkspaceToolProfile(
                SerializeConfiguration(new { enableCompaction = true, maxLocalRagResults = 4 }),
                AgentWorkspaceToolProfileKind.BusinessAnalysis),
            AgentPermissionsPolicy.Default,
            [
                CreateAssignment(spreadsheetCapabilityId, "spreadsheet-skill", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(documentSpreadsheetReconciliationInlineSkillCapabilityId, "document-spreadsheet-reconciliation-inline-skill", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceConvertDocumentCapabilityId, "workspace-convert-document", CapabilityKind.Tool),
                CreateAssignment(workspaceInspectSpreadsheetCapabilityId, "workspace-inspect-spreadsheet", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["spreadsheet", "analysis", "tabular"],
            now);

        var hrStaffingManagerAgent = CreateWorkloadAgent(
            hrStaffingManagerAgentId,
            "HR Staffing Manager",
            "HR staffing and assignment manager",
            "Matches governed process roles to actual project assignments, workforce records, and AI agents using factual fit instead of convenience or guesswork.",
            GetSeedText("agents/hr-staffing-manager.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Management,
            "hr-staffing-manager",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 4
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: true,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.ReadOnly),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["hr", "staffing", "assignment"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var mailAgent = CreateWorkloadAgent(
            mailAgentId,
            "Mail Triage Analyst",
            "Mail and inbox worker",
            "Uses inline skill instructions, AI context, and workspace document tools to summarize mail-like artifacts and extract participant-owned tasks.",
            GetSeedText("agents/mail-triage-analyst.instructions"),
            openAiChatProviderId,
            AgentWorkloadKind.Mail,
            "mail-triage-analyst",
            WithWorkspaceToolProfile(
                SerializeConfiguration(new { enableCompaction = true, maxLocalRagResults = 4 }),
                AgentWorkspaceToolProfileKind.BusinessAnalysis),
            AgentPermissionsPolicy.Default,
            [
                CreateAssignment(mailInlineSkillCapabilityId, "mail-triage-inline-skill", CapabilityKind.Skill),
                CreateAssignment(mailSummaryInlineSkillCapabilityId, "mail-summary-inline-skill", CapabilityKind.Skill),
                CreateAssignment(mailContextCapabilityId, "mail-triage-context", CapabilityKind.AiContext),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceConvertDocumentCapabilityId, "workspace-convert-document", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["mail", "triage", "reply"],
            now);

        var researchAgent = new AgentDefinition(
            researchAgentId,
            "Research Deep Dive Analyst",
            "Research and long-context worker",
            "Uses provider-managed responses, RAG, memory, and framework-native tools for multi-step research and generated-app summary flows.",
            GetSeedText("agents/research-deep-dive-analyst.instructions"),
            AgentLifecycleStatus.Active,
            openAiProviderId,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            AgentWorkloadKind.Research,
            AgentChatHistoryMode.ProviderManaged,
            0.2d,
            false,
            true,
            WithDefaultReasoningEffort(
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxInjectedMemoryItems = 6,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.ReadOnly)),
            false,
            "research-deep-dive-analyst",
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(appSummaryInlineSkillCapabilityId, "generated-app-summary-inline-skill", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceExecutionBoundaryCapabilityId, "workspace-execution-boundary", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag),
                CreateAssignment(researchContextCapabilityId, "research-briefing-context", CapabilityKind.AiContext),
                CreateAssignment(providerNativeCodeInterpreterCapabilityId, "provider-native-code-interpreter", CapabilityKind.Tool),
                CreateAssignment(providerNativeWebSearchCapabilityId, "provider-native-web-search", CapabilityKind.Tool),
                CreateAssignment(providerHealthCapabilityId, "provider-health", CapabilityKind.Tool)
            ],
            ["research", "background", "evidence"],
            now,
            now);

        var dotnetArchitectAgent = CreateWorkloadAgent(
            dotnetArchitectAgentId,
            ".NET Solution Architect",
            ".NET architecture specialist",
            "Designs maintainable C#, ASP.NET Core, and Blazor project structures with explicit boundaries and validation plans.",
            GetSeedText("agents/dotnet-solution-architect.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Research,
            "dotnet-solution-architect",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.ArchitectureReview),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(architectureDocsCapabilityId, "architecture-source-rag", CapabilityKind.Rag),
                CreateAssignment(researchContextCapabilityId, "research-briefing-context", CapabilityKind.AiContext),
                CreateAssignment(providerNativeWebSearchCapabilityId, "provider-native-web-search", CapabilityKind.Tool)
            ],
            ["dotnet", "architecture", "blazor"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var dotnetDeveloperAgent = CreateWorkloadAgent(
            dotnetDeveloperAgentId,
            ".NET Application Developer",
            ".NET implementation specialist",
            "Implements C#, ASP.NET Core, and Blazor deliverables with real source changes, focused tests, and runnable proof.",
            GetSeedText("agents/dotnet-application-developer.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Programming,
            "dotnet-application-developer",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        slidingWindowTurns = 10,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(runTestsCapabilityId, "run-tests", CapabilityKind.Skill),
                CreateAssignment(mstestCapabilityId, "writing-mstest-tests", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(dotnetAppDeliveryInlineSkillCapabilityId, "dotnet-app-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(blazorSsrDeliveryInlineSkillCapabilityId, "blazor-ssr-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceCopyPathCapabilityId, "workspace-copy-path", CapabilityKind.Tool),
                CreateAssignment(workspaceMovePathCapabilityId, "workspace-move-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDeletePathCapabilityId, "workspace-delete-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRestoreCapabilityId, "workspace-dotnet-restore", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetBuildCapabilityId, "workspace-dotnet-build", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetTestCapabilityId, "workspace-dotnet-test", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRunCapabilityId, "workspace-dotnet-run", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetNewCapabilityId, "workspace-dotnet-new", CapabilityKind.Tool),
                CreateAssignment(workspacePythonRunFileCapabilityId, "workspace-python-run-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["dotnet", "programming", "blazor"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var blazorDeveloperAgent = CreateWorkloadAgent(
            blazorDeveloperAgentId,
            "Blazor Application Developer",
            "Blazor implementation specialist",
            "Builds Blazor Web App and component-driven ASP.NET Core UI deliverables with BaseLib/component-library guidance, focused tests, startup proof, and browser evidence.",
            GetSeedText("agents/blazor-application-developer.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Programming,
            "blazor-application-developer",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        slidingWindowTurns = 10,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(runTestsCapabilityId, "run-tests", CapabilityKind.Skill),
                CreateAssignment(mstestCapabilityId, "writing-mstest-tests", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(dotnetAppDeliveryInlineSkillCapabilityId, "dotnet-app-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(blazorSsrDeliveryInlineSkillCapabilityId, "blazor-ssr-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceCopyPathCapabilityId, "workspace-copy-path", CapabilityKind.Tool),
                CreateAssignment(workspaceMovePathCapabilityId, "workspace-move-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDeletePathCapabilityId, "workspace-delete-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRestoreCapabilityId, "workspace-dotnet-restore", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetBuildCapabilityId, "workspace-dotnet-build", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetTestCapabilityId, "workspace-dotnet-test", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRunCapabilityId, "workspace-dotnet-run", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetNewCapabilityId, "workspace-dotnet-new", CapabilityKind.Tool),
                CreateAssignment(workspacePythonRunFileCapabilityId, "workspace-python-run-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["blazor", "dotnet", "frontend", "programming"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var dotnetQaAgent = CreateWorkloadAgent(
            dotnetQaAgentId,
            ".NET QA Review Lead",
            ".NET QA specialist",
            "Reviews C#, ASP.NET Core, and Blazor deliverables with build, test, launch, browser, and durable artifact proof.",
            GetSeedText("agents/dotnet-qa-review-lead.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "dotnet-qa-review-lead",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 4
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.QualityValidation),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(runTestsCapabilityId, "run-tests", CapabilityKind.Skill),
                CreateAssignment(mstestCapabilityId, "writing-mstest-tests", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(dotnetAppDeliveryInlineSkillCapabilityId, "dotnet-app-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(blazorSsrDeliveryInlineSkillCapabilityId, "blazor-ssr-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetBuildCapabilityId, "workspace-dotnet-build", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetTestCapabilityId, "workspace-dotnet-test", CapabilityKind.Tool),
                CreateAssignment(workspaceDotnetRunCapabilityId, "workspace-dotnet-run", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool)
            ],
            ["dotnet", "qa", "browser"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var javascriptArchitectAgent = CreateWorkloadAgent(
            javascriptArchitectAgentId,
            "JavaScript Solution Architect",
            "JavaScript architecture specialist",
            "Designs maintainable JavaScript and TypeScript app structures, package boundaries, and validation paths.",
            GetSeedText("agents/javascript-solution-architect.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Research,
            "javascript-solution-architect",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.ArchitectureReview),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag),
                CreateAssignment(researchContextCapabilityId, "research-briefing-context", CapabilityKind.AiContext),
                CreateAssignment(providerNativeWebSearchCapabilityId, "provider-native-web-search", CapabilityKind.Tool)
            ],
            ["javascript", "typescript", "architecture"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var javascriptDeveloperAgent = CreateWorkloadAgent(
            javascriptDeveloperAgentId,
            "JavaScript Application Developer",
            "JavaScript implementation specialist",
            "Implements JavaScript and TypeScript deliverables with package-script validation and browser proof when needed.",
            GetSeedText("agents/javascript-application-developer.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Programming,
            "javascript-application-developer",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        slidingWindowTurns = 10,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceCopyPathCapabilityId, "workspace-copy-path", CapabilityKind.Tool),
                CreateAssignment(workspaceMovePathCapabilityId, "workspace-move-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDeletePathCapabilityId, "workspace-delete-path", CapabilityKind.Tool),
                CreateAssignment(workspaceDiffTextCapabilityId, "workspace-diff-text", CapabilityKind.Tool),
                CreateAssignment(workspaceGitStatusCapabilityId, "workspace-git-status", CapabilityKind.Tool),
                CreateAssignment(workspaceGitDiffCapabilityId, "workspace-git-diff", CapabilityKind.Tool),
                CreateAssignment(workspacePythonRunFileCapabilityId, "workspace-python-run-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["javascript", "typescript", "programming"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var javascriptQaAgent = CreateWorkloadAgent(
            javascriptQaAgentId,
            "JavaScript QA Review Lead",
            "JavaScript QA specialist",
            "Reviews JavaScript and TypeScript deliverables with package-script, browser, console, and screenshot evidence.",
            GetSeedText("agents/javascript-qa-review-lead.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "javascript-qa-review-lead",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 4
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.QualityValidation),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(repositoryPlaybookCapabilityId, "repository-playbook", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspacePythonRunFileCapabilityId, "workspace-python-run-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool)
            ],
            ["javascript", "qa", "browser"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var businessStrategistAgent = CreateWorkloadAgent(
            businessStrategistAgentId,
            "Business Strategist",
            "Business planning specialist",
            "Creates grounded business plans, operating assumptions, risk views, and cross-functional handoffs for non-code processes.",
            GetSeedText("agents/business-strategist.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Management,
            "business-strategist",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.BusinessAnalysis),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, CanScheduleWork = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceConvertDocumentCapabilityId, "workspace-convert-document", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag),
                CreateAssignment(researchContextCapabilityId, "research-briefing-context", CapabilityKind.AiContext),
                CreateAssignment(providerNativeWebSearchCapabilityId, "provider-native-web-search", CapabilityKind.Tool)
            ],
            ["business", "strategy", "planning"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var financialStrategistAgent = CreateWorkloadAgent(
            financialStrategistAgentId,
            "Financial Strategist",
            "Financial planning specialist",
            "Builds assumption-driven budgets, unit economics, spreadsheet analysis, and sensitivity views for planning processes.",
            GetSeedText("agents/financial-strategist.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Spreadsheet,
            "financial-strategist",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.BusinessAnalysis),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(spreadsheetCapabilityId, "spreadsheet-skill", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(documentSpreadsheetReconciliationInlineSkillCapabilityId, "document-spreadsheet-reconciliation-inline-skill", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceConvertDocumentCapabilityId, "workspace-convert-document", CapabilityKind.Tool),
                CreateAssignment(workspaceInspectSpreadsheetCapabilityId, "workspace-inspect-spreadsheet", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag),
                CreateAssignment(researchContextCapabilityId, "research-briefing-context", CapabilityKind.AiContext),
                CreateAssignment(providerNativeCodeInterpreterCapabilityId, "provider-native-code-interpreter", CapabilityKind.Tool)
            ],
            ["finance", "strategy", "spreadsheet"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        var marketingSpecialistAgent = CreateWorkloadAgent(
            marketingSpecialistAgentId,
            "Marketing Specialist",
            "Marketing planning specialist",
            "Creates positioning, messaging, go-to-market plans, campaign briefs, and validation experiments for non-code processes.",
            GetSeedText("agents/marketing-specialist.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Sales,
            "marketing-specialist",
            WithWorkspaceToolProfile(
                WithProcessAccess(
                WithProjectStructureAccess(
                    SerializeConfiguration(new
                    {
                        managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                        enableCompaction = true,
                        maxLocalRagResults = 5
                    }),
                    canRead: true,
                    canWrite: false,
                    allowAllProjects: true),
                canRead: true,
                canWrite: false,
                allowAllDefinitions: true),
                AgentWorkspaceToolProfileKind.BusinessAnalysis),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(concreteDeliverableDeliveryInlineSkillCapabilityId, "concrete-deliverable-delivery-inline-skill", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(workspaceListFilesCapabilityId, "workspace-list-files", CapabilityKind.Tool),
                CreateAssignment(workspaceSearchCapabilityId, "workspace-search", CapabilityKind.Tool),
                CreateAssignment(workspaceReadCapabilityId, "workspace-read-file", CapabilityKind.Tool),
                CreateAssignment(workspaceStatPathCapabilityId, "workspace-stat-path", CapabilityKind.Tool),
                CreateAssignment(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", CapabilityKind.Tool),
                CreateAssignment(workspaceWriteFileCapabilityId, "workspace-write-file", CapabilityKind.Tool),
                CreateAssignment(workspaceAppendFileCapabilityId, "workspace-append-file", CapabilityKind.Tool),
                CreateAssignment(workspaceConvertDocumentCapabilityId, "workspace-convert-document", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag),
                CreateAssignment(researchContextCapabilityId, "research-briefing-context", CapabilityKind.AiContext),
                CreateAssignment(providerNativeWebSearchCapabilityId, "provider-native-web-search", CapabilityKind.Tool)
            ],
            ["marketing", "strategy", "go-to-market"],
            now,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);

        return new SandboxWorkspaceDocument(
            LatestVersion,
            [
                architectAgent,
                qaAgent,
                programmingAgent,
                codeReviewAgent,
                uiReviewAgent,
                securityReviewerAgent,
                releaseManagerAgent,
                hrStaffingManagerAgent,
                spreadsheetAgent,
                mailAgent,
                researchAgent,
                dotnetArchitectAgent,
                dotnetDeveloperAgent,
                blazorDeveloperAgent,
                dotnetQaAgent,
                javascriptArchitectAgent,
                javascriptDeveloperAgent,
                javascriptQaAgent,
                businessStrategistAgent,
                financialStrategistAgent,
                marketingSpecialistAgent
            ],
            providers,
            capabilities,
            [
                new ChatSessionRecord(
                    sessionId,
                    architectAgentId,
                    "Integration target summary",
                    now,
                    now,
                    string.Empty,
                    null,
                    [
                        new ChatMessageRecord(CreateStableGuid("messages/integration-target-summary/user"), ChatMessageRole.User, "Summarize the integration target for this sandbox.", now, 10),
                        new ChatMessageRecord(CreateStableGuid("messages/integration-target-summary/assistant"), ChatMessageRole.Assistant, "The sandbox should stay standalone today while aligning with CanDoItAll identity, provider, automation, assignment, and rights seams for later integration.", now, 28)
                    ],
                    [])
            ],
            [new ExecutionLogEntry(CreateStableGuid("execution-log/integration-target-summary"), architectAgentId, sessionId, now, ExecutionState.Completed, "Seeded run", "Created the initial sandbox summary conversation.")],
            [new AgentRunMetric(CreateStableGuid("metrics/integration-target-summary"), architectAgentId, sessionId, now, RunOutcome.Succeeded, "OpenAI default", ManagedSeedProviderFallbacks.OpenAiDefaultModel, 420, 10, 28, 0)],
            [
                new AgentMemoryRecord(CreateStableGuid("memory/future-candoitall-seam"), architectAgentId, MemoryKind.Architecture, "Future CanDoItAll seam", "Align with CRM and HR agent identity, project-node assignments, provider profiles, automation telemetry, and rights masks.", "seed", 5, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/proof-discipline"), qaAgentId, MemoryKind.FollowUp, "Proof discipline", "Reopen any phase when browser proof or dependency gates are weak.", "seed", 4, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/framework-first-coding"), programmingAgentId, MemoryKind.Context, "Framework-first coding", "Prefer Microsoft Agent Framework primitives before adding wrapper-specific coding behavior.", "seed", 4, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/staffing-grounding"), hrStaffingManagerAgentId, MemoryKind.Context, "Staffing grounding", "Prefer currently assigned project resources and bound AI agents when they satisfy the role facts. Escalate unresolved gaps instead of inventing a confident-looking match.", "seed", 4, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/spreadsheet-review-checklist"), spreadsheetAgentId, MemoryKind.Context, "Spreadsheet review checklist", "Explain key metrics, anomalies, and any rows that deserve follow-up.", "seed", 3, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/reply-style"), mailAgentId, MemoryKind.Preference, "Reply style", "Keep drafted replies concise, direct, and explicit about the next action.", "seed", 3, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/evidence-first-claims"), researchAgentId, MemoryKind.Context, "Evidence-first claims", "Separate proven repo evidence from inference and capture any validation gap honestly.", "seed", 5, "{}", now)
            ]);
    }

    private static AgentDefinition CreateWorkloadAgent(
        Guid id,
        string name,
        string roleTitle,
        string summary,
        string instructions,
        Guid providerId,
        AgentWorkloadKind workload,
        string templateKey,
        string configurationJson,
        AgentPermissionsPolicy permissions,
        IReadOnlyList<AgentCapabilityAssignment> capabilities,
        IReadOnlyList<string> tags,
        DateTimeOffset now,
        string model = ManagedSeedProviderFallbacks.OpenAiDefaultModel)
    {
        return new AgentDefinition(
            id,
            name,
            roleTitle,
            summary,
            instructions,
            AgentLifecycleStatus.Active,
            providerId,
            model,
            workload,
            AgentChatHistoryMode.FrameworkManaged,
            0.2d,
            false,
            false,
            WithDefaultReasoningEffort(configurationJson),
            false,
            templateKey,
            permissions,
            capabilities,
            tags,
            now,
            now);
    }

    private static AgentCapabilityAssignment CreateAssignment(
        Guid capabilityId,
        string capabilityKey,
        CapabilityKind kind,
        CapabilityProofStatus status = CapabilityProofStatus.NotRun,
        string notes = "")
    {
        return new AgentCapabilityAssignment(capabilityId, capabilityKey, kind, status, null, notes);
    }

    private static string WithProjectStructureAccess(
        string configurationJson,
        bool canRead,
        bool canWrite,
        bool allowAllProjects)
    {
        return AgentProjectStructureAccessMetadata.Write(
            configurationJson,
            new AgentProjectStructureAccessSettings
            {
                CanRead = canRead,
                CanWrite = canWrite,
                AllowAllProjects = allowAllProjects
            });
    }

    private static string WithProcessAccess(
        string configurationJson,
        bool canRead,
        bool canWrite,
        bool allowAllDefinitions)
    {
        return AgentProcessAccessMetadata.Write(
            configurationJson,
            new AgentProcessAccessSettings
            {
                CanRead = canRead,
                CanWrite = canWrite,
                AllowAllDefinitions = allowAllDefinitions
            });
    }

    private static string WithWorkspaceToolProfile(
        string configurationJson,
        AgentWorkspaceToolProfileKind profile)
    {
        return AgentWorkspaceToolAccessMetadata.Write(
            configurationJson,
            AgentWorkspaceToolAccessProfiles.CreateSettings(profile));
    }

    private static string WithDefaultReasoningEffort(string configurationJson)
    {
        var configuration = ReadConfigurationObject(configurationJson);
        configuration[AgentProviderModelParameterPolicy.ReasoningEffortConfigurationPropertyName] = ManagedSeedProviderFallbacks.DefaultReasoningEffort;

        var modelParameters = configuration.TryGetValue(
                AgentProviderModelParameterPolicy.ModelParametersConfigurationPropertyName,
                out var existingModelParameters) &&
            existingModelParameters is IDictionary<string, object?> existingModelParameterDictionary
                ? new Dictionary<string, object?>(existingModelParameterDictionary, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        modelParameters[AgentProviderModelParameterPolicy.ReasoningEffortConfigurationPropertyName] = ManagedSeedProviderFallbacks.DefaultReasoningEffort;
        configuration[AgentProviderModelParameterPolicy.ModelParametersConfigurationPropertyName] = modelParameters;
        return SerializeConfiguration(configuration);
    }

    private static string CreateOpenAiProviderConfigurationJson(string history)
    {
        return SerializeConfiguration(new
        {
            history,
            reasoningEffort = ManagedSeedProviderFallbacks.DefaultReasoningEffort,
            modelParameters = new
            {
                reasoningEffort = ManagedSeedProviderFallbacks.DefaultReasoningEffort
            }
        });
    }

    private static Dictionary<string, object?> ReadConfigurationObject(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(configurationJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return document.RootElement
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ConvertSeedConfigurationValue(property.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSeedSkillRoot(string key)
    {
        return SandboxWorkspaceSeedAssets.Current.GetSkillRoot(key);
    }

    private static string GetSeedText(string key)
    {
        return SandboxWorkspaceSeedAssets.Current.GetText(key);
    }

    private static CapabilityCatalogItem CreateFileSkillCapability(Guid id, string key, string name, string description, string skillRoot, string notes)
    {
        var skillFile = Path.IsPathRooted(skillRoot) ? Path.Combine(skillRoot, "SKILL.md") : Path.Combine(skillRoot, "SKILL.md");
        var allowExternalRoot = Path.IsPathRooted(skillRoot) || skillRoot.StartsWith("~", StringComparison.Ordinal);
        return new CapabilityCatalogItem(
            id,
            CapabilityKind.Skill,
            key,
            name,
            description,
            skillFile,
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                skillSource = "file",
                skillRoot,
                allowedExternalRoots = allowExternalRoot ? new[] { skillRoot } : Array.Empty<string>(),
                scriptApproval = true,
                scriptExecution = new
                {
                    approvalRequired = true,
                    trustLevel = allowExternalRoot ? "ExternalSkillRoot" : "WorkspaceSkillRoot"
                }
            }),
            CapabilityProofStatus.NotRun,
            notes,
            null,
            true);
    }

    private static CapabilityCatalogItem CreateInlineSkillCapability(
        Guid id,
        string key,
        string name,
        string description,
        string inlineName,
        string instructions,
        IReadOnlyList<InlineSkillResourceSeed>? resources = null)
    {
        return new CapabilityCatalogItem(
            id,
            CapabilityKind.Skill,
            key,
            name,
            description,
            $"inline://{key}",
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                skillSource = "inline",
                inlineSkill = new
                {
                    name = inlineName,
                    description,
                    instructions,
                    resources = resources?.Select(resource => new
                    {
                        resource.Name,
                        resource.Content,
                        resource.Description
                    })
                }
            }),
            CapabilityProofStatus.NotRun,
            "Inline task workflow guidance seeded directly in the workspace catalog.",
            null,
            true);
    }

    private static string BuildBlazorProgramExample() => GetSeedText("resources/net10-program-scaffold");

    private static string BuildReconciliationOutputExample() => GetSeedText("resources/reconciliation-output-example");

    private static string BuildBlazorHomeExample() => GetSeedText("resources/net10-home-page-example");

    private static string BuildBlazorCommandExamples() => GetSeedText("resources/dotnet-command-examples");

    private static string BuildGeneratedAppSummaryChecklist() => GetSeedText("resources/generated-app-summary-checklist");

    private static Guid CreateStableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        Span<byte> buffer = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(buffer);
        buffer[6] = (byte)((buffer[6] & 0x0F) | 0x50);
        buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);
        return new Guid(buffer);
    }

    private static CapabilityCatalogItem CreateToolCapability(Guid id, string key, string name, string description, string toolName, bool approvalRequired = false, object? additionalConfiguration = null)
    {
        return new CapabilityCatalogItem(
            id,
            CapabilityKind.Tool,
            key,
            name,
            description,
            $"sandbox://{key}",
            SerializeConfiguration(BuildToolConfiguration(toolName, approvalRequired, additionalConfiguration)),
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
    }

    private static IReadOnlyDictionary<string, object?> BuildToolConfiguration(string toolName, bool approvalRequired, object? additionalConfiguration)
    {
        var configuration = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["managedSeedVersion"] = SeriousDeliveryManagedSeedVersion,
            ["tool"] = toolName,
            ["approvalRequired"] = approvalRequired
        };

        if (additionalConfiguration is null)
        {
            return configuration;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(additionalConfiguration, SerializerOptions));
        foreach (var property in document.RootElement.EnumerateObject())
        {
            configuration[property.Name] = ConvertSeedConfigurationValue(property.Value);
        }

        return configuration;
    }

    private static object? ConvertSeedConfigurationValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertSeedConfigurationValue).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ConvertSeedConfigurationValue(property.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => value.ToString()
        };
    }

    private static CapabilityCatalogItem CreateAiContextCapability(Guid id, string key, string name, string description, string message)
    {
        return new CapabilityCatalogItem(
            id,
            CapabilityKind.AiContext,
            key,
            name,
            description,
            string.Empty,
            SerializeConfiguration(new { message, role = "system" }),
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
    }

    private sealed record InlineSkillResourceSeed(string Name, string Content, string? Description);

    private static string SerializeConfiguration<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }
}
