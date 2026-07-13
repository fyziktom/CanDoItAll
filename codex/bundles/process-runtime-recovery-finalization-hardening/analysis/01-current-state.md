# Current State

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260707213600-f58ac646`
- Scope: 14 projects under Processes, `CanDoItAll.Modules.Processes`, unit tests, and integration tests.
- Health: snapshot completed with no blocking errors.
- Dependency result: `code_analytics_dependencies_get` reported no project cycles in scope.
- Project dependency concern: `CanDoItAll.Processes.Application` references `CanDoItAll.Processes.Runtime`, and `CanDoItAll.Processes.Persistence` also references Runtime for runtime-state persistence. This is currently functional, but extraction work must avoid making Runtime depend back on Application, Persistence, Modules, MAF, or AgentFramework.
- Finding concern: CodeAnalytics top findings identify large files and partial-heavy process surfaces, especially runtime dispatch, process launch, process projection query, process evidence provider, and AgentFramework adapter files.

## Runtime Flow As Observed

1. `ProcessLaunchApplicationService` builds a process plan, assignments, and initial runtime state.
2. `ProcessTemplateKernelBuilder` creates artifact slots from step artifact expectations and maps required slots using `(source step key, artifact expectation key)`.
3. `ProcessRuntimeScheduler` moves pending steps to ready only when dependency steps are terminal and required artifact slot ids exist in `AvailableArtifactSlots`.
4. `ProcessRuntimeDispatchApplicationService` claims ready work, invokes the selected strategy, submits the strategy result, and applies branch signal routing.
5. `AgentFrameworkProcessExecutionAdapter` executes agent-backed steps, parses the required finalizer result, materializes managed artifacts, checks tool receipts and product evidence, and converts issues into `NeedsManager` adapter results.
6. `ProcessRuntimeEngine.SubmitStrategyResult` stores the result receipt, produced artifact receipts, recovery decision, available artifact slots, and ledger events.
7. Recovery/background dispatch can enqueue runs again when recovery detects ready work or stopped child runs.

## Current Strengths

- Artifact slot definitions and references are strongly typed in Core.
- Runtime state has typed step status, claims, result receipts, produced artifact receipts, and recovery decision receipts.
- Scheduler already prevents a pending step from becoming ready when required slots are unavailable.
- Step briefs already include instructions for reading upstream managed artifact refs, using required tools, and returning a required finalizer.
- Required tool receipt gate exists and can distinguish active required tools by capability scope.
- Process drivers and driver abstractions already exist, but much policy is still in Module integration and partial adapter files.

## Current Gaps

- Artifact availability is tracked as slot presence, not as a concrete connected input artifact instance with producer step, source edge, content hash, storage ref, and readback proof.
- Runtime does not provide a durable per-step input package that an agent or finalizer can re-fetch after context compression.
- Manager signals and safe automatic retry are conflated. In `ProcessRuntimeEngine.ResultHelpers`, an adapter `NeedsManager` result with safe/idempotent `process.adapter.*` diagnostics becomes `Ready`, causing automatic retry.
- Missing produced artifacts, missing required tool receipts, and missing product state are often modeled as safe same-step retries even when the real repair is upstream artifact delivery, manager access grant, reassignment, or process-template repair.
- `RequestStepRework` already rejects blocked-step rework when dependencies or required artifacts are still missing, but there is no typed router that selects the responsible previous step from artifact connection lineage.
- The manager loop has recovery/resupply contracts, but finalization and next-step handoff are not modeled as a manager-confirmed gate.
- Agent instructions are prompt-only. They help, but they do not guarantee completion when the agent loses context.
- Context packaging is too coarse for software-development runs. A downstream agent can receive too much product code context or too little connected artifact context.
- Large partial clusters make the behavior hard to unit test independently.

## Relevant Source Evidence

- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs` has `IsAutomaticallyRetryableManagerResult`, `BuildRecoveryDecision`, `ClassifyFailureCategory`, and artifact ledger helper logic inside the runtime engine partial cluster.
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Rework.cs` checks whether a blocked step can be reworked from dependencies and required slots, but not from connected artifact lineage.
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs` checks required slots before ready work.
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs` suppresses repeated automatic retry only after a repeated automatic retry threshold.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs` keeps execution, child process, output validation, materialization, and completion issue conversion in one partial type.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs` supplies prompt guidance that should become runtime-verifiable contract checks.

## Architecture Judgment

The flawed approach would be to add more prompt text or another `SafeToRetry` heuristic. That will keep failing under context loss and missing upstream inputs. The correct direction is a typed contract: runtime records artifact lineage and step contract, driver/finalizer validates it, manager confirms the handoff when required, and retry routing is chosen from typed missing-input/access/finalization facts.
