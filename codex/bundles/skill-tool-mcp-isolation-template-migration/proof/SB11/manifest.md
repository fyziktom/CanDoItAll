# SB11 Regression Proof For Processes Workflows

## Status

- Result: `Passed`
- Validation depth: `End-to-end regression and closure`
- Browser validation: `Passed`
- UI viewport validation: `Large desktop only; small and medium viewport tests skipped per user instruction`
- Next gate: `SB12 may start`

## Implementation Summary

- Added `SB11_INV_ACCESS_001` to prove one shared typed evaluator denies a representative Skill, Tool, MCP server, and MCP tool, with `RequiredCapabilityDenied` diagnostics when those capabilities are required.
- Added API-level negative regression coverage for external Tool JSON parse failure, MCP list-tools failure with masked detail, invalid access-policy selector validation, and denied required capability diagnostics.
- Added a large-screen-only workflow Playwright smoke for `/agents/workflows` that creates a starter workflow, runs preview, opens runtime detail, and captures browser-visible proof.
- Extended the integration test project with explicit capability/MCP contract project references needed by the API regression test.

## Evidence

| Evidence | Path |
| --- | --- |
| Web build | `proof/SB11/transcripts/dotnet-build-web.txt` |
| Unit regression matrix | `proof/SB11/transcripts/unit-capability-runtime-regression.txt` |
| Integration regression matrix | `proof/SB11/transcripts/integration-seed-filter-api-workflow-regression.txt` |
| Component regression matrix | `proof/SB11/transcripts/component-setup-process-workflow-regression.txt` |
| Large-screen Playwright matrix | `proof/SB11/transcripts/playwright-large-screen-regression.txt` |
| Process browser validation log | `proof/SB11/transcripts/process-shell-browser-validation-summary.txt` |
| Source assertions | `proof/SB11/transcripts/source-assertions.txt` |
| Anti-stub and secret scan | `proof/SB11/transcripts/anti-stub-and-secret-scan.txt` |
| File-size scan | `proof/SB11/transcripts/file-size-scan.txt` |
| Changed file hashes | `proof/SB11/changed-file-hashes.txt` |

## Browser Evidence

| Screen | Path | Viewport |
| --- | --- | --- |
| Capability setup and repairable Tool diagnostic | `proof/SB11/screenshots/agent-capability-setup-flow-large.png` | `1600x1000` |
| Workflow runtime detail after preview run | `proof/SB11/screenshots/workflow-shell-runtime-large.png` | `1600x1000` |
| Process live dashboard | `proof/SB11/screenshots/processes-live-dashboard.png` | `1440x900` |
| Process definition canvas | `proof/SB11/screenshots/processes-definition-canvas.png` | `1440x900` |
| Process role editor | `proof/SB11/screenshots/processes-definition-role-editor.png` | `1440x900` |
| Process step editor | `proof/SB11/screenshots/processes-definition-step-editor.png` | `1440x900` |
| Process template preview | `proof/SB11/screenshots/processes-template-library-preview.png` | `1440x900` |
| Process project-scoped shell | `proof/SB11/screenshots/processes-project-shell.png` | `1440x900` |

## Test Commands

```text
dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CapabilityContractsTests|FullyQualifiedName~CapabilityFoundationHardeningTests|FullyQualifiedName~CapabilityTemplateSeedMaterializationTests|FullyQualifiedName~CapabilityTemplateSeedHardeningCheckpointTests|FullyQualifiedName~SkillLoaderContractsTests|FullyQualifiedName~ToolImplementationContractsTests|FullyQualifiedName~McpRuntimeContractsTests|FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests|FullyQualifiedName~AgentToolInvocationPolicyTests"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~AgentFrameworkWorkspaceSeedIntegrationTests|FullyQualifiedName~AgentFrameworkExecutionCapabilityFilteringIntegrationTests|FullyQualifiedName~AgentCapabilitySetupApiIntegrationTests|FullyQualifiedName~WorkflowApiIntegrationTests.Workflow_api_saves_validates_and_runs_workflow"
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CapabilitySetupFlowServiceTests|FullyQualifiedName~ProcessWorkspaceShellTests|FullyQualifiedName~WorkflowsPageTests"
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-restore --filter "FullyQualifiedName~AgentCapabilitySetupFlowPlaywrightTests|FullyQualifiedName~ProcessShellSmokeTests|FullyQualifiedName~WorkflowShellSmokeTests"
```

## Results

- Web build: `0 warnings`, `0 errors`.
- Unit tests: `269 passed`.
- Integration tests: `34 passed`.
- Component tests: `60 passed`.
- Large-screen Playwright tests: `3 passed`.
- Process browser validation: `0` failed requests, `0` page errors; only expected Blazor disconnect requests were ignored.
- No local MCP process was launched by SB11 API proof; MCP list-tools failure uses `FakeMcpClientFactory` and deterministic cleanup proof.

## Regression Repairs

- Added explicit integration-test project references for capability/MCP contract assemblies so API tests can assert typed DTO and diagnostic contracts directly.
- Adjusted the API access-preview assertion to account for the service contract that includes the seeded catalog when no `CapabilityIds` filter is supplied; the test now verifies the required target capability is denied and absent from allowed capabilities rather than assuming an empty catalog.

## Accepted Risks

| Risk | Decision | Follow-up |
| --- | --- | --- |
| Access policy persistence into agent/process/workflow definitions remains outside SB11. | Accepted because SB11 proves shared evaluator semantics, API diagnostics, MAF/runtime composition, and browser behavior without adding a half-designed persistence model. | SB12 cleanup should document the remaining policy-persistence target or defer it explicitly to a follow-up bundle. |

## Progression Decision

- `SB11 completed; SB12 unblocked.`
