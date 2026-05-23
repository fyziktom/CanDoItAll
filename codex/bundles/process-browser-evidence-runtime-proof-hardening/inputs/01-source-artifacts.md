# Source Artifacts

## Development DB

Database profile inspected: `candoitall_development` on `127.0.0.1:5432`.

Primary run:

| Field | Value |
| --- | --- |
| Process run id | `4f218d64-2cb3-49fc-ad00-fc7dba917f79` |
| Name | `Basic Application / Multi-team software delivery and release governance` |
| Project | `TetrisGame` |
| Definition | `Multi-team software delivery and release governance` |
| Status | `Completed` |
| Created | `2026-05-22 14:41:48.291008+00` |
| Updated | `2026-05-22 15:06:23.73509+00` |

Step summary:

| Sequence | Step | Status | Branch outcome | Artifact records |
| --- | --- | --- | --- | --- |
| 0 | Clarify scope and release boundary | `Completed` | empty | 1 |
| 1 | Review architecture and canonical-model impact | `Completed` | empty | 2 |
| 2 | Implement bounded delivery change | `Completed` | empty | 14 |
| 3 | Complete peer review and integration readiness | `Completed` | empty | 1 |
| 4 | Run QA validation and runtime or browser proof | `Completed` | `Repair required` | 6 |
| 5 | Repair validation findings | `Completed` | empty | 8 |
| 6 | Re-run QA validation and runtime or browser proof after repair | `Completed` | `Quality accepted` | 12 |
| 7 | Perform security and data-handling review | `Skipped` | empty | 0 |
| 8 | Perform security review after repair | `Completed` | empty | 1 |
| 9 | Approve first-pass release readiness | `Skipped` | empty | 0 |
| 10 | Execute first-pass controlled release rollout | `Skipped` | empty | 0 |
| 11 | Capture first-pass post-release learning | `Skipped` | empty | 0 |
| 12 | Escalate unresolved repair findings | `Skipped` | empty | 0 |
| 13 | Approve repaired release readiness | `Completed` | empty | 1 |
| 14 | Execute repaired controlled release rollout | `Completed` | empty | 1 |
| 15 | Capture repaired-release learning | `Completed` | empty | 1 |

Screenshot expectations existed:

| Step | Artifact | Validation requirement |
| --- | --- | --- |
| 4 | `Regression evidence pack` | `Must name changed flows, assertion depth, warning counts, executed-test counts when tests are expected, shipped entrypoint and referenced runtime files or commands, runtime/API/browser evidence as applicable, screenshots for UI surfaces, stale/unreferenced artifact findings, and unresolved risks.` |
| 6 | `Repaired regression evidence pack` | `Must name repaired flows, assertion depth, warning counts, executed-test counts when tests are expected, shipped entrypoint and referenced runtime files or commands, runtime/API/browser evidence as applicable, screenshots for UI surfaces, stale/unreferenced artifact findings, and unresolved risks after the repair pass.` |

Artifact and conformance facts:

| Query result | Value |
| --- | --- |
| Total `Processes_ArtifactRecords` for run | 48 |
| Artifact records with `.png` managed path | 0 |
| Artifact records with `browser` in managed path | 0 |
| `Processes_ConformanceObservations` for run | 0 |

## Workspace Evidence

Process artifact root:

```text
C:\Users\lucys\AppData\Local\CanDoItAll\workspace\artifacts\scopes\organization\e5df9ad633dbc6974a0678a74976013c\process-runs\4f218d64-2cb3-49fc-ad00-fc7dba917f79
```

That directory contains markdown/text process deliverables and stop scripts, but no `.png`, `.yml`, `.log`, or browser proof artifacts recorded as process evidence.

Provider-native Playwright MCP outputs existed outside the process run artifact ledger:

```text
C:\Users\lucys\AppData\Local\CanDoItAll\workspace\.playwright-mcp\page-2026-05-22T14-59-45-865Z.png
C:\Users\lucys\AppData\Local\CanDoItAll\workspace\.playwright-mcp\page-2026-05-22T14-58-45-608Z.yml
C:\Users\lucys\AppData\Local\CanDoItAll\workspace\.playwright-mcp\console-2026-05-22T14-58-45-447Z.log
```

The repaired execution run `d60051f1-7166-4c58-8c96-31850cbd21ec` referenced the provider-native `.playwright-mcp` files in `resultSummary.evidenceRefs`, but those files were not projected into process artifacts. Its `SerializedSessionStateJson` had `stateBag.InMemoryChatHistoryProvider.messages: []`, so synthetic browser receipts based on chat history had no source data.

Browser invocation logs show requested filenames under the process run path:

```text
browser_take_screenshot filename="artifacts/process-runs/4f218d64-2cb3-49fc-ad00-fc7dba917f79/browser-proof.png"
browser_console_messages filename="artifacts/process-runs/4f218d64-2cb3-49fc-ad00-fc7dba917f79/browser-console.log"
browser_evaluate filename="artifacts/process-runs/4f218d64-2cb3-49fc-ad00-fc7dba917f79/page-dom.json"
```

Those requested files did not appear under `C:\Users\lucys\AppData\Local\CanDoItAll\workspace\artifacts\process-runs\...`, while default provider-native `.playwright-mcp\page-*` and `.playwright-mcp\console-*` files did.

The console log contains connection errors after about 86 seconds:

```text
[86823ms] Error: Cannot send data if the connection is not in the 'Connected' State.
[88800ms] [ERROR] Failed to load resource: net::ERR_CONNECTION_REFUSED @ http://127.0.0.1:52791/_blazor/negotiate?negotiateVersion=1
```

Interpretation for execution: the implementation must classify console diagnostics by proof phase. Errors after intentional app shutdown may be non-blocking shutdown noise, but they still must not be summarized as "0 errors/warnings" for the entire captured log without a durable phase marker.

## Source References

| Area | Reference | Why it matters |
| --- | --- | --- |
| Browser proof validation | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.BrowserProof.cs` | Detects starter-template and runtime-error browser proof from snapshots, but currently depends on successful browser output discovery. |
| Artifact validation | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` | Extracts expected artifact paths and maps `.png`, `.yml`, `.log`, `.txt` to provider-native browser tools. |
| Artifact projection | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projects provider-native browser outputs into managed process artifacts only when exact expected paths and output files line up. |
| Prompting | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs` | Already asks for navigate, interaction, snapshot, screenshot, console diagnostics, and browser_evaluate for canvas/game/custom controls. The current failure proves prompt-only enforcement is insufficient. |
| Browser-proof trigger | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs` | Determines whether a step is browser-proof gated. |
| Provider-native MCP receipt enrichment | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ProviderNativeMcp.cs` | Creates synthetic browser receipts from chat history, which was empty in the failing run. |
| Browser MCP wrapper | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | Mirrors screenshots to scoped artifact paths only if the requested file exists at the unscoped path. |
| Process tests | `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Existing tests cover many browser-proof cases but not this exact ledger/projection failure shape. |

## Related Completed Bundle

`repo://codex/bundles/process-multiteam-tetris-demo-hardening` is a prior completed bundle for a related but older hardening effort. It proved a different live run and is not reopened because the current DB run exposes a new evidence-ledger and proof-strength failure.
