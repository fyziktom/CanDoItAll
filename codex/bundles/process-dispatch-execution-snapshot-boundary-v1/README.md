# Process Dispatch Execution Snapshot Boundary v1

Bundle status: `Completed`
Profile: `initiative`
Primary branch target: `maf-processes-refactor`
Primary objective: continue the process-dispatch decoupling work without starting a broad Process Core split.

## Validation Summary

Bundle preparation status: `Prepared`
Bundle readiness gate: `Passed`
Execution status: `Completed`
Subbundle gate review: `Completed`
Final closure gate: `Completed`
Browser validation analytics: `N/A - no UI touched`
## Purpose

The previous bundle successfully introduced `CanDoItAll.Processes.Contracts` and routed dispatcher execution starts/detail/list calls through `IProcessAutomationExecutionClient`. The next step must keep the same incremental discipline: reduce the dispatcher's direct dependency on AgentFramework execution details by introducing process-owned execution snapshots, typed failure normalization, and focused receipt/tool observation helpers.

This bundle intentionally does **not** extract full `CanDoItAll.Processes.Core`, does **not** introduce driver packs, and does **not** move EF entities or UI surfaces. It prepares a stronger boundary around the execution/result/detail/receipt part of `ProcessRunAutomationDispatchService` so later bundles can isolate artifact validation, grounding, browser proof, and domain-specific tool checks one slice at a time.

## Final Source Findings

- `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.AgentFramework.Core`, and `CanDoItAll.AgentFramework.Tooling` have no source/project references to Processes, Projects, or Workbench in the final scan.
- `CanDoItAll.Processes.Contracts` remains neutral and now owns execution request/source/policy plus process execution run, detail, result, log, metric, artifact, receipt, usage, chat, pending approval, query, and failure snapshots.
- `ProcessAutomationExecutionClient` is the only execution adapter that maps AgentFramework execution runtime details into process-owned contracts.
- Dispatcher partials outside `ProcessAutomationExecutionClient` no longer contain direct `ExecutionRunResult`, `ExecutionRunDetail`, `ExecutionRunRecord`, `ExecutionRunQuery`, `AgentChatRunFailedException`, `AgentRunFailedException`, or `AgentStructuredOutputContracts` tokens.
- Required-tool and selected artifact-lineage consumers now route receipt success/provider observation logic through a process-snapshot helper.

## Scope

This bundle owns the next small slice:

1. Inventory all remaining AgentFramework execution model leakage in Processes automation dispatch.
2. Extend `CanDoItAll.Processes.Contracts` with **neutral execution snapshots** only for data already consumed by process automation.
3. Make `ProcessAutomationExecutionClient` map AgentFramework execution data into process-owned snapshots.
4. Move dispatcher execution-path consumers to those snapshots.
5. Normalize AgentFramework execution failures behind process-owned exception/result records.
6. Create a small, testable receipt/tool-observation helper that operates on process snapshots, not AgentFramework types.
7. Preserve process runtime behavior, required-tool checks, artifact lineage, provider metadata, access policy, and all 23 process tool names.

## Hard Prohibitions

- Do not create full `CanDoItAll.Processes.Core` yet.
- Do not move EF entities, DbContext usage, Razor components, UI view models, or storage implementations.
- Do not introduce `IProcessDriverPack`, driver-pack projects, Rust/.NET/domain process drivers, or external process runtime drivers.
- Do not reintroduce direct MAF references to Processes, Projects, or Workbench.
- Do not rename process tools or weaken process runtime provider access/approval behavior.
- Do not test small, medium, mobile, tablet, Android, or iPhone viewports. The product target for this workflow is PC/large-screen only. If UI proof is somehow needed, use large-screen only; otherwise record N/A.

## Subbundle Overview

1. SB01 entry audit, source inventory, and baseline proof.
2. SB02 execution snapshot contract design.
3. SB03 refactor gate A: neutral contracts and architecture guardrails.
4. SB04 client mapping foundation.
5. SB05 dispatcher detail/result migration.
6. SB06 failure normalization boundary.
7. SB07 refactor gate B: coupling scan and behavior parity.
8. SB08 execution receipt/tool observation helper foundation.
9. SB09 artifact-lineage and required-tool consumer migration.
10. SB10 refactor gate C: dispatcher source-size and dependency review.
11. SB11 process runtime smoke and large-screen policy proof.
12. SB12 final red-team, cutline, and next isolation bundle recommendation.

## Output Contract

The implementation is complete only when:

- Dispatcher source has no direct dependency on `ExecutionRunResult`, `ExecutionRunDetail`, `ExecutionRunRecord`, `AgentChatRunFailedException`, `AgentRunFailedException`, or `AgentStructuredOutputContracts`.
- `ProcessAutomationExecutionClient` is the only allowed adapter that maps those AgentFramework runtime details into process-owned contracts.
- Existing process runtime behavior is preserved by unit/integration tests and source scans.
- The bundle validator passes prepared and completed stages.
- The final report explicitly states whether the next bundle may isolate artifact validation/projection or whether one more execution-boundary cleanup is needed.

## Final Cutline

The next bundle may isolate artifact validation/projection. The execution result/detail/failure/receipt observation boundary is no longer the blocker. Keep the next slice narrow: do not start a full Process Core split, do not move EF/UI surfaces, and keep `ProcessAutomationExecutionClient` as the AgentFramework execution adapter until a later boundary plan replaces it deliberately.
