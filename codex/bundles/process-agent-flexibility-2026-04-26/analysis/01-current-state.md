# Current State

## Process Prompt

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs` builds the base process step prompt.
- The base prompt currently includes a large `RequiresConcreteImplementationProof` section that assumes `.NET`, `dotnet new`, Blazor Web App scaffolding, sibling test projects, `Home.razor`, `CalculatorEngine`, and calculator-specific UI behavior.
- The same file has `ContainsCalculatorContext`, `AppendCalculatorImplementationContract`, and `AppendCalculatorRecoveryChecklist`, which makes the base platform prompt responsible for a single app type.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.RecoveryDirective.cs` also emits .NET/Blazor/calculator retry instructions. This is adjacent risk even though the user named the execution prompt.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.GovernedRules.cs` detects implementation proof based on process step title/artifact shape, not based on agent specialization.

## Default Agents

- Seeded default agents are built in `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`.
- Existing managed agents include `Portfolio Architect`, `Programming Workspace Analyst`, `Delivery QA Observer`, `Code Review Lead`, `UI Review Lead`, `Security Reviewer`, `Release Readiness Manager`, `HR Staffing Manager`, `Spreadsheet Analyst`, `Mail Triage Analyst`, and `Research Deep Dive Analyst`.
- Agent instructions live under `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents`.
- Managed seed refresh and fallback lists are duplicated in `SandboxWorkspaceSeedNormalizer.cs` and `ManagedSeedProviderFallbacks.cs`; adding managed agents requires updating both.
- Existing specialized non-code agents cover mail and spreadsheet work, but business strategy, finance strategy, marketing, .NET-specific delivery, and JavaScript-specific delivery are not explicit default agents.

## Skills And Capabilities

- The seed catalog already exposes file skills for `aspnet-core`, `run-tests`, `writing-mstest-tests`, `candoitall-codeanalytics-mcp`, `candoitall-components-mcp`, `frontend-skill`, `spreadsheets`, and process bundle workflow.
- There is no explicit JavaScript/Node skill capability in the seed catalog yet; JS agents can still use workspace tools, local RAG, provider tools, and generic frontend guidance.
- Business/finance/marketing agents can use local document/spreadsheet tools, provider-native web search where enabled, and context/instructions without requiring coding capabilities.

## Process Templates

- Process templates are file-backed under `C:\repositories\CanDoItAll\Templates\Processes`.
- `manifest.json` currently lists software delivery, branching code review, hotfix rollout, customer onboarding, incident response, architecture decision governance, release readiness/deployment, OSS intake, and AI-assisted change delivery.
- Baseline process scenarios are loaded from `C:\repositories\CanDoItAll\Templates\Processes\seed-catalog\baseline-scenarios.json`.
- Existing non-coding templates cover customer onboarding and incident-style operations, but there is no explicit business-plan workflow matching the user's example.

## Test And Database State

- Prompt and dispatch behavior are covered by `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`.
- Seed behavior is covered by `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs` and unit tests for managed seed fallbacks.
- PostgreSQL test support exists in `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\PostgresTestAvailability.cs` and `CanDoItAllTestEnvironment`.
- Process MCP/integration tests already have PostgreSQL branches in `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesMcpStdioIntegrationTests.cs`.
