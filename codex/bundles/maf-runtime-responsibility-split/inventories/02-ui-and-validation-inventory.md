# UI And Validation Inventory

## Build And Test Commands

| Scope | Command |
| --- | --- |
| MAF project build | `dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --configuration Release` |
| Web project build | `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release` |
| Focused MAF unit tests | `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~MafAgentRuntime|FullyQualifiedName~AgentFinalizerPolicy"` |
| Focused execution integration tests | `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~AgentFrameworkExecution"` |
| Playwright agent UI tests | `dotnet test tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --filter "FullyQualifiedName~AiAgentFlowTests|FullyQualifiedName~AgentCapabilitySetupFlowPlaywrightTests"` |
| Playwright workflow/process UI tests | `dotnet test tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --filter "FullyQualifiedName~WorkflowShellSmokeTests|FullyQualifiedName~ProcessShellSmokeTests"` |

## Browser Routes

| Route | Purpose | Evidence required |
| --- | --- | --- |
| `/agents` | Agent Framework shell loads and exposes tabs. | Large-screen screenshot, no route errors, expected shell text visible. |
| `/agents?tab=agents` | Agent chat/runtime entry surface. | Agent list/chat panel visible, seeded agent selectable, no console errors. |
| `/agents?tab=capabilities&agentId={seed}` | Capability setup/runtime capability surface. | Capability table/setup flow visible; no broken tool/capability diagnostics after runtime refactor. |
| `/agents/workflows` | Workflow shell that can depend on runtime execution wiring. | Route loads, workflow canvas/shell visible, no runtime initialization errors. |
| `/processes` or existing process smoke route from Playwright fixture | Process runtime shell affected by required finalizer behavior. | Process shell smoke passes; finalizer/process runtime diagnostics do not regress. |

## Screenshot Review Questions

- Does the page load without error overlays or failed navigation?
- Are agent, capability, workflow, and process runtime panels readable at large desktop viewport?
- Are runtime diagnostics, provider status, and capability details present where expected?
- If any UI/CSS files changed, does the same route remain readable at a narrower viewport?
- Were screenshots actually reviewed and recorded in `reviews/01-execution-report.md`, not merely captured?
