# SB10 UI API Setup And Test Flows

## Status

- Result: `Passed`
- Validation depth: `Critical UI foundation`
- Browser validation: `Passed`
- UI viewport validation: `Large desktop only; small and medium viewport tests skipped per user instruction`
- Next gate: `SB11 may start`

## Implementation Summary

- Added a focused `IAgentCapabilitySetupFlowService` application-layer seam for Tool setup tests, MCP setup tests, and typed access-policy previews.
- Added API endpoints for Tool setup test, MCP setup test, and capability access preview through `AgentsApi`.
- Extended the capability setup wizard and details dialog with first-class Tool configuration/edit/test forms.
- Added access-preview controls to the capabilities panel using catalog-backed selector choices and shared policy evaluation.
- Registered Tool setup-test services and access-policy evaluator dependencies through the AgentFramework module DI setup.
- Fixed Tool wizard identity defaults so a new Tool no longer starts with the invalid generated implementation key `external.`.
- Split the new setup-flow service and wizard setup-test code-behind into focused partial files after the first file-size scan identified new oversized files.

## Evidence

| Evidence | Path |
| --- | --- |
| Component setup-flow tests | `proof/SB10/transcripts/component-setup-flow-tests.txt` |
| Large-screen Playwright setup-flow test | `proof/SB10/transcripts/playwright-capability-setup-flow-large.txt` |
| Failing-first Playwright diagnostic for Tool default bug | `proof/SB10/transcripts/failing-first-playwright-tool-default.txt` |
| AgentFramework module build | `proof/SB10/transcripts/dotnet-build-agentframework-module.txt` |
| Source assertions | `proof/SB10/transcripts/source-assertions.txt` |
| Anti-stub and secret scan | `proof/SB10/transcripts/anti-stub-and-secret-scan.txt` |
| File-size scan | `proof/SB10/transcripts/file-size-scan.txt` |
| Large-screen screenshot | `proof/SB10/agent-capability-setup-flow-large.png` |
| Changed file hashes | `proof/SB10/changed-file-hashes.txt` |

## Test Commands

```text
dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj --no-restore
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter FullyQualifiedName~CapabilitySetupFlowServiceTests
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter FullyQualifiedName~AgentCapabilitySetupFlowPlaywrightTests
```

## Results

- AgentFramework module build: `0 warnings`, `0 errors`.
- Component tests: `4 passed`.
- Large-screen Playwright tests: `1 passed`.
- Browser viewport: `1600x1000`; no small/medium UI passes were run because the app targets large screens only.
- Screenshot review: `agent-capability-setup-flow-large.png` shows the Tool setup test form, failed setup state, visible `JsonParse` category, bounded masked detail, and repair hint.
- File-size scan: all new/touched SB10 code-behind/service/test files stayed below 500 lines after splitting.

## Accepted Risks

| Risk | Decision | Follow-up |
| --- | --- | --- |
| Production host does not currently register a live `IMcpSetupTestService` client factory. | Accepted for SB10 because the UI/API path returns a typed `ImplementationMissing` diagnostic instead of pretending success. | Register the production MCP setup-test adapter when live MCP host credentials and client factory ownership are finalized. |
| Access policy UI is preview-only and does not persist restrictions onto agents/processes/workflows yet. | Accepted for SB10 because current catalog models do not have a policy slot and SB11 owns end-to-end process/workflow regression. | SB11/SB12 should wire persistence once the final policy target model is chosen. |

## Progression Decision

- `SB10 completed; SB11 unblocked.`
