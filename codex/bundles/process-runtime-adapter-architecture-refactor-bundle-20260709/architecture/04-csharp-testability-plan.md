# C# Testability Plan

## Testability Goal

Every moved responsibility must be testable without constructing `AgentFrameworkProcessExecutionAdapter`. The refactor is not complete if tests still need the original monolith to prove receipt matching, gate evaluation, domain driver selection, managed artifact materialization, subprocess resolution, or recovery classification.

## Required Test Layers

### Characterization Tests

Add before moving behavior:

| Test area | Purpose |
|---|---|
| Current adapter completion gate behavior | Lock currently intended gate behavior for valid/invalid completion outcomes. |
| Current receipt matching behavior | Lock current handling of current-run receipts, successful exit requirement, minimum count, active launch-context tools, and branch applicability. |
| Current managed artifact acceptance behavior | Lock artifact write/append/readback behavior before extraction. |
| Current subprocess blocked-child behavior | Lock observed parent/child result behavior before bridge extraction. |
| Current `WorkspaceCommandReceiptWriter` request summary behavior | Lock generic receipt summary behavior and .NET lifecycle facts before replacing hardcoded method. |

### Isolated Unit Tests

Add after extraction:

| Extracted responsibility | Required direct tests |
|---|---|
| `ProcessCompletionGatePipeline` | Aggregates multiple issues, orders primary issue, dedupes duplicate rules, preserves retry/idempotency metadata. |
| `RequiredToolReceiptMatcher` | Matches tool name/provider/MCP/current run/success/minimum count/branch applicability; fails missing or wrong-run receipts. |
| `ProcessCompletionIssueRouter` | Routes configured branch-routable issue to target branch and manager/escalation only when unsafe or unconfigured. |
| `ManagedOutcomeArtifactMaterializer` | Writes primary managed artifact, appends runtime findings, rejects ungrounded references, handles readback failure. |
| `SubprocessRunStateResolver` | Distinguishes active child, accepted child, blocked child with diagnostics, stopped failed child, no matching child. |
| `ParentSubprocessArtifactBridge` | Uses ledger/slot evidence first; file fallback is explicit recovery diagnostic only. |
| `ProcessRecoveryClassifier` | Safe/idempotent completion gate issue becomes bounded retry; unsafe/policy/denied issue becomes manager/assignment repair. |
| `ProcessStepRecoveryInstructionBuilder` | Builds diagnostic-specific packet with observed vs expected receipts and no generic retry prose only. |
| `RuntimeOwnedStepExecutorFactory` | Selects driver executor by typed metadata; unsupported request produces explicit diagnostic. |
| `ToolReceiptLifecycleFactExtractor` | Generic writer invokes registered extractor; .NET implementation extracts startup path/loopback URL without core hardcode. |

### Negative Tests

Required negative cases:

- Missing `workspace_pwsh_run_script` receipt and empty product file produce more than one issue.
- `quality-accepted` acceptance proof requirement does not block a configured repair branch when defect evidence is valid.
- Failed build receipt can satisfy defect-proof expectation when `RequireSuccessfulExit` is false and branch metadata allows it.
- Unresolved `{CurrentProcessRunId}` in a tool-critical script ref fails validation.
- Unsupported domain runtime-owned executor does not silently fall back to generic agent execution.
- Generic runtime source assertions fail if `.NET`, Tetris, Calculator, or software-delivery step keys appear in forbidden files.
- Tests fail if a new adapter partial file is added.

### Composition Smoke Tests

Required after DI/project-reference changes:

- DI resolves adapter shell and all extracted services.
- Driver catalog contains the runtime-owned .NET executor policy through driver registration, not adapter direct construction.
- Generic process path without .NET domain driver still runs a minimal non-domain process.
- Software-delivery process path can select .NET driver policies.
- MAF receipt writer resolves lifecycle fact extractors from registration without depending on process module.

## Source Assertions

Implementation must include command transcripts or scripts for:

```text
rg -n "partial class AgentFrameworkProcessExecutionAdapter|sealed partial class AgentFrameworkProcessExecutionAdapter" src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration
rg -n "IsDotNetRuntimeLifecycleTool|workspace_dotnet_run|workspace_dotnet_stop" src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs
rg -n "Tetris|Calculator|qa-validation|quality-accepted|repair-required|create-dotnet-project|add-test-project|repair-solution-setup" src/Processes src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands
```

The third assertion may document allowed occurrences in templates, domain driver implementations, or tool catalog/protocol code. It must not allow domain decisions in generic runtime/dispatcher/adapter orchestration.

## Test Exit Condition

The test suite is acceptable only when:

- Extracted service tests would fail if behavior remained only in the old adapter.
- Negative tests prove shallow or fake extraction fails.
- Composition smoke tests prove the production path uses the new services.
- The final 5032/equivalent process validation exercises the new route/repair behavior end to end.

