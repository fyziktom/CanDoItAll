# C# Testability Plan

## Characterization Tests

| Area | Tests to add or pin before moving code |
| --- | --- |
| Runtime turn orchestration | Existing `RunAsync` behavior around temperature retry, input attachments, runtime options, progress messages, and framework-managed session selection. |
| Approval continuation | Pending approval cache hit, session compatibility rehydration, no cached approval error. |
| Finalizer repair | Missing required finalizer triggers bounded repair; typed JSON fallback produces governed output; provider failure after finalizer preserves output. |
| Runtime build | Default build, handoff build, unsupported structured output, finalizer tool attach, approval tool filtering. |
| Capability access | Workspace tool disabled policy, process-step filtering, runtime tool provider operation requirements. |
| Workspace tools | Read-only vs mutation access, protected delete denial, command path normalization, image-analysis model resolution. |

## Isolated Unit Tests

| Extracted owner | Required fake dependencies |
| --- | --- |
| `MafRuntimeTurnCoordinator` | fake runtime build coordinator, fake turn executor, fake attachment preparer, fake progress recorder. |
| `MafRuntimeTurnExecutor` | fake provider streaming runner, fake session persistence driver, fake finalizer repair coordinator. |
| `MafFinalizerRepairCoordinator` | fake streaming runner, fake progress recorder, in-memory finalizer policies. |
| `MafRuntimeSessionPersistenceDriver` | fake session adapter, fake scrubber, controlled cancellation/timeouts. |
| `MafApprovalContinuationDriver` | in-memory cache, synthetic `ChatSessionRecord` compatibility data. |
| `MafRuntimeBuildCoordinator` | fake capability composer, fake provider agent factory, fake credential resolver. |
| `MafHandoffRuntimeBuilder` | fake participant catalog/provider factory and invalid handoff metadata. |
| `MafScriptPolicyInspectionService` | fake file reader/path resolver. |
| `RuntimeCapabilityAccessPlanner` | in-memory agent/capability records and policy evaluator. |
| `RuntimeCapabilityDescriptorCatalog` | in-memory catalog items for tool/skill/MCP/compatibility capabilities. |
| `RuntimeCapabilityAttachmentOrchestrator` | fake contributor interfaces for each attachment family. |
| Workspace tool sets | fake file/command/artifact/provider gateway plus policy service. |

## Negative Tests

- Unknown provider/model configuration does not silently fall back unless existing behavior explicitly says it should.
- Invalid handoff metadata throws the existing domain exception path.
- Missing required runtime-build field fails before provider invocation.
- Session serialization timeout returns no session state and emits the expected progress message.
- Request-scoped attachment payloads are scrubbed or session persistence is skipped.
- Mutating workspace tool call is denied under read-only process intent.
- Protected directory recursive delete is denied.
- Duplicate runtime tool provider keys are rejected through the new seam.

## Composition Smoke Tests

- `AddMafRuntimeArchitectureServices` registers the extracted services.
- `IAgentRuntime` resolves and calls through `MafAgentRuntime` facade.
- `MafAgentRuntimeHandoffTests` still pass.
- Runtime tool provider composition focused slice still passes.
- Provider diagnostics still route through `ProviderRuntimeDiagnostics`.

## Extension Seam Tests

- Add a fake runtime capability provider/tool family through registration/catalog without editing `MafAgentRuntime`, `RuntimeCapabilityComposer`, or `WorkspaceRuntimePlugin`.
- Source assertion checks the fake extension is not wired by a switch in the old large type.

## Performance Proof

- Baseline and after: runtime constructor timing for a minimal service collection.
- Baseline and after: capability composition stage metrics from `IMafRuntimeCompositionMetrics`.
- Baseline and after: focused unit test elapsed time for MAF runtime architecture slice.
- Record no claim if the measurement cannot be captured.

## Implementation Update - 2026-07-06

Direct extracted-owner tests added or updated:

- `MafApprovalContinuationDriver_maps_and_replays_pending_function_approval`
- `MafApprovalContinuationDriver_rehydrates_legacy_pending_approval_records`
- `MafRuntimeSessionPersistenceDriver_skips_governed_process_steps_without_pending_approvals`
- `RuntimeCapabilityDescriptorCatalog_creates_tool_descriptor_without_composer`
- `WorkspaceRuntimePlugin_no_longer_owns_image_model_resolution`
- `MafAgentRuntimeImageAnalysisModelTests` now target `WorkspaceImageAnalysisModelResolver` directly.

Composition and integration proof:

- `MafRuntimeArchitectureServicesTests|MafAgentRuntimeToolProviderCompositionTests|MafAgentRuntimeImageAnalysisModelTests` passed 56/56.
- `MafAgentRuntimeHandoffTests` passed 3/3.

Remaining testability gaps:

- Turn execution still needs direct tests against a real `MafRuntimeTurnCoordinator` and `MafRuntimeTurnExecutor`.
- Runtime build/handoff/tool instrumentation still needs direct tests against extracted build/handoff/instrumentor owners.
- Workspace tool families still need direct tests against separate file/command/script/artifact/image tool-set classes.
