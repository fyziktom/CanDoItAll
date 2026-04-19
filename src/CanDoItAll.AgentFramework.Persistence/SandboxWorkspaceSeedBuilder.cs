using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class SandboxWorkspaceSeedBuilder
{
    private const string LatestVersion = "3.0";
    private const string SeriousDeliveryManagedSeedVersion = "2026-04-serious-delivery-v19";
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 4, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static SandboxWorkspaceDocument Build()
    {
        var now = SeedTimestamp;

        var openAiProviderId = CreateStableGuid("providers/openai-default");
        var openAiChatProviderId = CreateStableGuid("providers/openai-chat-completions");
        var ollamaProviderId = CreateStableGuid("providers/ollama-local");

        var projectStructureCapabilityId = CreateStableGuid("capabilities/project-structure-central");
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
        var officeInlineSkillCapabilityId = CreateStableGuid("capabilities/office-order-inline-skill");
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
        var spreadsheetAgentId = CreateStableGuid("agents/spreadsheet-analyst");
        var mailAgentId = CreateStableGuid("agents/mail-triage-analyst");
        var researchAgentId = CreateStableGuid("agents/research-deep-dive-analyst");
        var sessionId = CreateStableGuid("sessions/integration-target-summary");
        var projectStructureReadToolNames = new[]
        {
            "project_structure_projects_list",
            "project_structure_hierarchy_get",
            "project_structure_read",
            "project_structure_checklist",
            "project_structure_dependencies_query",
            "project_structure_asset_get",
            "project_structure_knowledge_query",
            "project_structure_analytics_query",
            "project_structure_project_lease_acquire",
            "project_structure_repo_branch_lease_acquire",
            "project_structure_lease_get",
            "project_structure_lease_release",
            "project_structure_approval_request"
        };

        var capabilities = new List<CapabilityCatalogItem>
        {
            new(
                projectStructureCapabilityId,
                CapabilityKind.McpServer,
                "project-structure-central",
                "Project Structure Central",
                "Local MCP bridge for reading CanDoItAll project structure, dependency state, leases, and approval-request context from the active workspace.",
                "dotnet",
                SerializeConfiguration(new
                {
                    transport = "stdio",
                    serverName = "candoitall-project-structure",
                    command = "dotnet",
                    arguments = new[]
                    {
                        "run",
                        "--project",
                        "src/CanDoItAll.Mcp.ProjectStructure/CanDoItAll.Mcp.ProjectStructure.csproj",
                        "--",
                        "--settings",
                        "CanDoItAll.Mcp.ProjectStructure.settings.local.json"
                    },
                    workingDirectory = ".",
                    allowedTools = projectStructureReadToolNames,
                    approvalMode = "NeverRequire"
                }),
                CapabilityProofStatus.NotRun,
                "Seeded as a local stdio MCP bridge against the repo project-structure settings file. Live proof should confirm read access, lease flow, and approval-request creation from real agent runs.",
                null,
                true),
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
                officeInlineSkillCapabilityId,
                "office-order-inline-skill",
                "Office Order Analysis Skill",
                "Task-specific workflow for comparing the Mouser spreadsheet and receipt before reporting stock and price findings.",
                "office-order-analysis",
                GetSeedText("skills/office-order-analysis.instructions"),
                [
                    new InlineSkillResourceSeed(
                        "office-json-example",
                        BuildOfficeComparisonJsonExample(),
                        "Reference JSON response shape for Mouser comparison tasks.")
                ]),
            CreateInlineSkillCapability(
                blazorSsrDeliveryInlineSkillCapabilityId,
                "blazor-ssr-delivery-inline-skill",
                "Blazor SSR Delivery Skill",
                "Reusable workflow guidance for creating and validating a .NET 10 Blazor SSR application.",
                "blazor-ssr-delivery",
                GetSeedText("skills/blazor-ssr-delivery.instructions"),
                [
                    new InlineSkillResourceSeed(
                        "net10-program-scaffold",
                        BuildCalculatorProgramExample(),
                        "Reference shape for the modern .NET 10 Blazor Web App Program.cs scaffold."),
                    new InlineSkillResourceSeed(
                        "net10-home-page-example",
                        BuildCalculatorHomeExample(),
                        "Reference shape for a static-SSR calculator page using query parameters and a GET form."),
                    new InlineSkillResourceSeed(
                        "dotnet-command-examples",
                        BuildCalculatorCommandExamples(),
                        "Concrete workspace_dotnet_new and workspace_dotnet_build examples for scaffolding and building the calculator app.")
                ]),
            CreateInlineSkillCapability(
                appSummaryInlineSkillCapabilityId,
                "generated-app-summary-inline-skill",
                "Generated App Summary Skill",
                "Task-specific workflow for summarizing the calculator app from its source files.",
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
                "Task-specific workflow for creating a Mermaid class diagram from the generated calculator app.",
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
            CreateToolCapability(workspaceListFilesCapabilityId, "workspace-list-files", "Workspace List Files", "Lists files and directories from the current workspace.", "workspace_list_files"),
            CreateToolCapability(workspaceSearchCapabilityId, "workspace-search", "Workspace Search", "Searches text across the current CanDoItAll.AgentFramework workspace for implementation clues.", "workspace_search"),
            CreateToolCapability(workspaceReadCapabilityId, "workspace-read-file", "Workspace Read File", "Reads text files from the current workspace so programming, mail, and analysis agents can inspect artifacts on demand.", "workspace_read_file"),
            CreateToolCapability(workspaceStatPathCapabilityId, "workspace-stat-path", "Workspace Stat Path", "Returns path metadata such as kind, existence, and size for a workspace file or directory.", "workspace_stat_path"),
            CreateToolCapability(workspaceCreateDirectoryCapabilityId, "workspace-create-directory", "Workspace Create Directory", "Creates a directory inside the current workspace before generated artifacts are written.", "workspace_create_directory", approvalRequired: true),
            CreateToolCapability(workspaceWriteFileCapabilityId, "workspace-write-file", "Workspace Write File", "Creates or overwrites a text file inside the current workspace.", "workspace_write_file", approvalRequired: true),
            CreateToolCapability(workspaceAppendFileCapabilityId, "workspace-append-file", "Workspace Append File", "Appends text to an existing workspace file or creates it when needed.", "workspace_append_file", approvalRequired: true),
            CreateToolCapability(workspaceCopyPathCapabilityId, "workspace-copy-path", "Workspace Copy Path", "Copies a file or directory inside the current workspace with receipt generation.", "workspace_copy_path", approvalRequired: true),
            CreateToolCapability(workspaceMovePathCapabilityId, "workspace-move-path", "Workspace Move Path", "Moves or renames a file or directory inside the current workspace with receipt generation.", "workspace_move_path", approvalRequired: true),
            CreateToolCapability(workspaceDeletePathCapabilityId, "workspace-delete-path", "Workspace Delete Path", "Deletes a file or directory inside the current workspace with receipt generation.", "workspace_delete_path", approvalRequired: true),
            CreateToolCapability(workspaceDiffTextCapabilityId, "workspace-diff-text", "Workspace Diff Text", "Shows a bounded line-level diff between two workspace text files.", "workspace_diff_text"),
            CreateToolCapability(workspaceExecutionBoundaryCapabilityId, "workspace-execution-boundary", "Workspace Execution Boundary", "Reports the actual tool-execution isolation mode and host enforcement guarantees.", "workspace_execution_boundary"),
            CreateToolCapability(workspaceGitStatusCapabilityId, "workspace-git-status", "Workspace Git Status", "Runs a bounded git status recipe in the current workspace.", "workspace_git_status"),
            CreateToolCapability(workspaceGitDiffCapabilityId, "workspace-git-diff", "Workspace Git Diff", "Runs a bounded git diff recipe in the current workspace.", "workspace_git_diff"),
            CreateToolCapability(workspaceDotnetRestoreCapabilityId, "workspace-dotnet-restore", "Workspace Dotnet Restore", "Runs a bounded dotnet restore recipe and records the actual isolation boundary.", "workspace_dotnet_restore", approvalRequired: true),
            CreateToolCapability(workspaceDotnetBuildCapabilityId, "workspace-dotnet-build", "Workspace Dotnet Build", "Runs a bounded dotnet build recipe in the current workspace.", "workspace_dotnet_build"),
            CreateToolCapability(workspaceDotnetTestCapabilityId, "workspace-dotnet-test", "Workspace Dotnet Test", "Runs a bounded dotnet test recipe in the current workspace.", "workspace_dotnet_test"),
            CreateToolCapability(workspaceDotnetNewCapabilityId, "workspace-dotnet-new", "Workspace Dotnet New", "Creates an approved dotnet project scaffold inside the current workspace.", "workspace_dotnet_new", approvalRequired: true),
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
                "gpt-4o-mini",
                ProviderTransportKind.Responses,
                true,
                true,
                true,
                false,
                true,
                SerializeConfiguration(new { history = "service-managed" }),
                "Responses profile for hosted routes, DevUI, and background-response scenarios.",
                "Not checked",
                null,
                ["gpt-4o-mini", "gpt-4.1-mini", "gpt-4.1"]),
            new(
                openAiChatProviderId,
                "OpenAI chat completions",
                ProviderKind.OpenAi,
                "https://api.openai.com/v1",
                "OPENAI_API_KEY",
                "gpt-4o-mini",
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
                ["gpt-4o-mini", "gpt-4.1-mini", "gpt-4.1"]),
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
            "gpt-4.1",
            AgentWorkloadKind.Research,
            AgentChatHistoryMode.ProviderDefault,
            0.2d,
            false,
            false,
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
            false,
            "portfolio-architect",
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, CanScheduleWork = true, AutoApproveExternalCallsByDefault = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
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
            "gpt-4.1",
            AgentWorkloadKind.Qa,
            AgentChatHistoryMode.FrameworkManaged,
            0.1d,
            false,
            false,
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                enableCompaction = true,
                maxLocalRagResults = 4
            }),
            false,
            "delivery-qa-observer",
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(runTestsCapabilityId, "run-tests", CapabilityKind.Skill),
                CreateAssignment(mstestCapabilityId, "writing-mstest-tests", CapabilityKind.Skill),
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
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                preferredSkillRoots = new[] { GetSeedSkillRoot("repository-playbook") },
                enableCompaction = true,
                slidingWindowTurns = 10,
                maxLocalRagResults = 5
            }),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(aspNetCoreCapabilityId, "aspnet-core-skill", CapabilityKind.Skill),
                CreateAssignment(codeanalyticsCapabilityId, "candoitall-codeanalytics-mcp", CapabilityKind.Skill),
                CreateAssignment(componentsCapabilityId, "candoitall-components-mcp", CapabilityKind.Skill),
                CreateAssignment(frontendThemeCapabilityId, "candoitall-frontend-theme", CapabilityKind.Skill),
                CreateAssignment(frontendSkillCapabilityId, "frontend-skill", CapabilityKind.Skill),
                CreateAssignment(playwrightWorkflowCapabilityId, "candoitall-watch-playwright-loop", CapabilityKind.Skill),
                CreateAssignment(runTestsCapabilityId, "run-tests", CapabilityKind.Skill),
                CreateAssignment(mstestCapabilityId, "writing-mstest-tests", CapabilityKind.Skill),
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
                CreateAssignment(workspaceDotnetNewCapabilityId, "workspace-dotnet-new", CapabilityKind.Tool),
                CreateAssignment(workspacePythonRunFileCapabilityId, "workspace-python-run-file", CapabilityKind.Tool),
                CreateAssignment(workspacePwshRunScriptCapabilityId, "workspace-pwsh-run-script", CapabilityKind.Tool),
                CreateAssignment(localDocsCapabilityId, "workspace-source-rag", CapabilityKind.Rag)
            ],
            ["programming", "workspace", "approval"],
            now,
            "gpt-4.1");

        var codeReviewAgent = CreateWorkloadAgent(
            codeReviewAgentId,
            "Code Review Lead",
            "Code reviewer",
            "Reviews C# and Blazor changes for regressions, architectural drift, weak evidence, and reviewable quality.",
            GetSeedText("agents/code-review-lead.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "code-review-lead",
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                enableCompaction = true,
                maxLocalRagResults = 5
            }),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
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
            "gpt-4.1");

        var uiReviewAgent = CreateWorkloadAgent(
            uiReviewAgentId,
            "UI Review Lead",
            "UI reviewer",
            "Reviews rendered Blazor UI with Playwright, screenshot analysis, and component-library expectations.",
            GetSeedText("agents/ui-review-lead.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "ui-review-lead",
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                enableCompaction = true,
                maxLocalRagResults = 4
            }),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
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
            "gpt-4.1");

        var securityReviewerAgent = CreateWorkloadAgent(
            securityReviewerAgentId,
            "Security Reviewer",
            "Security reviewer",
            "Reviews C# and Blazor changes for trust-boundary risk, hidden fallbacks, and explicit security posture.",
            GetSeedText("agents/security-reviewer.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Qa,
            "security-reviewer",
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                enableCompaction = true,
                maxLocalRagResults = 4
            }),
            AgentPermissionsPolicy.Default with { RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
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
            "gpt-4.1");

        var releaseManagerAgent = CreateWorkloadAgent(
            releaseManagerAgentId,
            "Release Readiness Manager",
            "Release readiness manager",
            "Synthesizes QA, security, rollback, and smoke-proof evidence into an explicit release-readiness decision.",
            GetSeedText("agents/release-readiness-manager.instructions"),
            openAiProviderId,
            AgentWorkloadKind.Management,
            "release-readiness-manager",
            SerializeConfiguration(new
            {
                managedSeedVersion = SeriousDeliveryManagedSeedVersion,
                enableCompaction = true,
                maxLocalRagResults = 4
            }),
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
                CreateAssignment(playwrightLocalMcpCapabilityId, "playwright-local-mcp", CapabilityKind.McpServer),
                CreateAssignment(bundleWorkflowCapabilityId, "candoitall-bundle-workflow", CapabilityKind.Skill),
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
            "gpt-4.1");

        var spreadsheetAgent = CreateWorkloadAgent(
            spreadsheetAgentId,
            "Spreadsheet Analyst",
            "Spreadsheet and tabular-data worker",
            "Uses spreadsheet skill guidance plus framework-native tools to inspect workbook-like artifacts, compare purchasing documents, and call out concrete findings.",
            GetSeedText("agents/spreadsheet-analyst.instructions"),
            openAiChatProviderId,
            AgentWorkloadKind.Spreadsheet,
            "spreadsheet-analyst",
            SerializeConfiguration(new { enableCompaction = true, maxLocalRagResults = 4 }),
            AgentPermissionsPolicy.Default,
            [
                CreateAssignment(spreadsheetCapabilityId, "spreadsheet-skill", CapabilityKind.Skill),
                CreateAssignment(officeInlineSkillCapabilityId, "office-order-inline-skill", CapabilityKind.Skill),
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

        var mailAgent = CreateWorkloadAgent(
            mailAgentId,
            "Mail Triage Analyst",
            "Mail and inbox worker",
            "Uses inline skill instructions, AI context, and workspace document tools to summarize mail-like artifacts and extract participant-owned tasks.",
            GetSeedText("agents/mail-triage-analyst.instructions"),
            openAiChatProviderId,
            AgentWorkloadKind.Mail,
            "mail-triage-analyst",
            SerializeConfiguration(new { enableCompaction = true, maxLocalRagResults = 4 }),
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
            "gpt-4o-mini",
            AgentWorkloadKind.Research,
            AgentChatHistoryMode.ProviderManaged,
            0.2d,
            false,
            true,
            SerializeConfiguration(new { enableCompaction = true, maxInjectedMemoryItems = 6, maxLocalRagResults = 5 }),
            false,
            "research-deep-dive-analyst",
            AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, RequiresApprovalForExternalCalls = true },
            [
                CreateAssignment(projectStructureCapabilityId, "project-structure-central", CapabilityKind.McpServer),
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

        return new SandboxWorkspaceDocument(
            LatestVersion,
            [architectAgent, qaAgent, programmingAgent, codeReviewAgent, uiReviewAgent, securityReviewerAgent, releaseManagerAgent, spreadsheetAgent, mailAgent, researchAgent],
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
            [new AgentRunMetric(CreateStableGuid("metrics/integration-target-summary"), architectAgentId, sessionId, now, RunOutcome.Succeeded, "OpenAI default", "gpt-4o-mini", 420, 10, 28, 0)],
            [
                new AgentMemoryRecord(CreateStableGuid("memory/future-candoitall-seam"), architectAgentId, MemoryKind.Architecture, "Future CanDoItAll seam", "Align with CRM and HR agent identity, project-node assignments, provider profiles, automation telemetry, and rights masks.", "seed", 5, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/proof-discipline"), qaAgentId, MemoryKind.FollowUp, "Proof discipline", "Reopen any phase when browser proof or dependency gates are weak.", "seed", 4, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/framework-first-coding"), programmingAgentId, MemoryKind.Context, "Framework-first coding", "Prefer Microsoft Agent Framework primitives before adding wrapper-specific coding behavior.", "seed", 4, "{}", now),
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
        string model = "gpt-4o-mini")
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
            configurationJson,
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

    private static string BuildCalculatorProgramExample() => GetSeedText("resources/net10-program-scaffold");

    private static string BuildOfficeComparisonJsonExample() => GetSeedText("resources/office-comparison-example");

    private static string BuildCalculatorHomeExample() => GetSeedText("resources/net10-home-page-example");

    private static string BuildCalculatorCommandExamples() => GetSeedText("resources/dotnet-command-examples");

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
