# Process Dispatch Artifact Source Adapter Boundary v1

Bundle preparation status: `Ready`
Bundle readiness gate: `Passed`
Execution status: `Completed`
Profile: `initiative`

## Validation Summary

- Bundle preparation status: `Ready`.
- Bundle readiness gate: `Passed`.
- Execution status: `Completed`.
- Subbundle gate review: `Passed`; SB01 through SB12 completed in dependency order.
- Final closure gate: `Passed after completed-stage validator run`.
- Browser validation analytics: `N/A`; runtime/service refactor only, with proof-path scan showing no prohibited viewport artifacts.
- Validation proof includes focused unit architecture tests, focused integration projection slice, source scans, line-count comparison, and `dotnet build CanDoItAll.slnx`.

## Purpose

Continue the gradual decomposition of the huge `ProcessRunAutomationDispatchService` artifact/projection area **without** starting a full Process Core split. The previous bundle added process-owned execution snapshots and introduced first artifact projection helpers. This bundle should harden that foundation by isolating projection source adapters and the first write-side coordinator while proving that no original artifact behavior is dropped.

## Current Branch Assessment

The prior artifact boundary bundle completed its declared scope:

- `ProcessAutomationExecutionClient` maps AgentFramework runtime details into process-owned snapshots.
- `ProcessArtifactExpectationMatcher`, `ProcessArtifactProjectionLineageBuilder`, `ProcessArtifactProjectionPlanner`, and `ProcessArtifactEvidenceValidationRules` exist.
- The execution-artifact projection path is routed through `ProcessArtifactProjectionPlanner.PlanExecutionArtifact`.
- `ProcessRunAutomationDispatchService.ArtifactProjection.cs` still owns storage placement and DB recording and still contains multiple projection source paths.
- `ProcessRunAutomationDispatchService.ArtifactValidation.cs` and `ProcessRunAutomationDispatchService.ArtifactProjection.cs` remain very large and should be decomposed by source-specific adapters, not by a broad core split.

## Mission

Create a source-adapter boundary for artifact projection and migrate selected projection paths in dependency order:

1. Inventory every projection source and duplicate-skip/lineage rule.
2. Introduce process-module-local projection source snapshots that do **not** depend on `ProcessRunAutomationDispatchService` nested types.
3. Migrate process mock, workspace-written, existing-managed, assistant-response, and provider-native browser planning paths to source adapters.
4. Introduce a small write-side coordinator for storage placement + artifact recording and migrate only the execution-artifact write path first.
5. Preserve all previous behavior: artifact identity, external reference keys, lineage, trust status, duplicate protection, required artifact satisfaction, receipt metadata, and recovery semantics.

## Non-Goals

- Do **not** create `CanDoItAll.Processes.Core` in this bundle.
- Do **not** create process driver packs.
- Do **not** move EF entities, DbContext usage, Razor components, UI view models, MAF composition, Tooling contracts, Workbench providers, or storage implementations out of their current modules.
- Do **not** rename runtime tools or weaken access/approval policy.
- Do **not** test or optimize small, medium, mobile, phone, tablet, Android, or iPhone layouts. This bundle is runtime/service work; browser proof is expected to be `N/A`. If UI proof unexpectedly becomes necessary, use PC/large-screen only.

## Expected End State

- `ArtifactProjection.cs` becomes smaller by moving source-specific planning into helpers/adapters while retaining orchestration.
- Source adapters produce typed projection plans for process mock, workspace-written, existing-managed, response-text, and provider-native browser sources.
- The planner/helper layer stops depending on dispatcher nested artifact expectation snapshots where practical.
- A first write-side coordinator exists and is used by the execution-artifact projection path only.
- Refactor gates prove no behavior drop before any downstream source path migration continues.

## Required Validation Summary

At final closure Codex must provide:

- `dotnet build CanDoItAll.slnx`.
- Focused unit/architecture tests for projection helper boundaries.
- Focused integration tests for process artifact projection, artifact lineage, and required-artifact satisfaction.
- Source scans proving no `Processes.Core`, no driver-pack project, no MAF product dependency, and no prohibited viewport proof artifacts.
- Exact parity inventory showing unchanged external reference key formats and expected projection behavior for all migrated source paths.
- Line-count comparison before/after for artifact projection/validation partials.
