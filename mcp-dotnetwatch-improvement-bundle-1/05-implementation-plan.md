# 05. Implementation Plan

## Execution rule

Implement in phases.
Do not combine bridge hardening, launch-model refactor, atomic slots, and test rewrites into one unreviewable change set.

## Phase 1. Baseline, terminology, and contract shims

### Goals

- freeze the target vocabulary
- add compatibility shims without changing behavior yet
- prepare the codebase for lane-aware evolution

### Tasks

1. Introduce new shared models for:
   - `logicalAppId`
   - `RuntimeLaneKind`
   - `RuntimeRevisionData`
   - atomic transaction state
   - `WorkflowGuidanceData`
2. Add backward-compatible contract extensions to `workspace_info`, `app_status`, and `app_wait`.
3. Update key tool descriptions with one short static workflow sentence about small validated iteration.
4. Add request idempotency plumbing for non-idempotent tool calls.
5. Add tests for compatibility mapping from current `AppRunMode`.

### Likely files

- `src/CanDoItAll.Mcp.Core/Contracts/McpToolEnvelope.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendToolContracts.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs`
- `tests/CanDoItAll.Mcp.DotNetWatch.Tests/*`

### Exit criteria

- new fields compile and serialize cleanly
- workflow guidance contract exists without changing runtime behavior yet
- existing callers still pass without knowing about the new fields

## Phase 2. Bridge hardening and repair loop

### Goals

- make direct Codex-to-MCP calls self-repairing
- remove generic backend churn failure modes

### Tasks

1. Add a `BridgeRepairCoordinator` or equivalent repair policy.
2. Refactor `BackendConnectionManager` to support forced refresh and explicit repair attempts.
3. Refactor `BackendToolInvoker` so every request goes through:
   - send
   - classify failure
   - repair if permitted
   - retry if safe
4. Add bridge status to `workspace_info`.
5. Add typed failure codes for bridge categories.
6. Emit bridge/failure guidance such as `fix-current-failure` only when it is directly justified by the returned state.

### Likely files

- `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendConnectionManager.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendToolInvoker.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Program.cs`
- new `src/CanDoItAll.Mcp.DotNetWatch/Bridge/*`
- integration tests in `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/*`

### Exit criteria

- loss of backend registration or auth token mismatch produces typed repair behavior
- `workspace_info` succeeds after a forced bridge repair scenario

## Phase 3. Launch model refactor

### Goals

- decouple runtime source from launch behavior
- introduce published and executable launch support

### Tasks

1. Replace `AppStartTemplate` as the primary abstraction with launch-spec models.
2. Add launch handlers for:
   - project watch
   - project run
   - published DLL
   - executable path
3. Add endpoint allocation for candidate runtimes.
4. Preserve current watch/run flows through compatibility adapters.
5. Expose lane kind and revision data through `app_status`.
6. Add lane-aware workflow guidance for healthy watch flows and restart-heavy flows.

### Likely files

- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs`
- new `src/CanDoItAll.Mcp.DotNetWatch/Runtime/LaunchSpecs/*`

### Exit criteria

- existing watch/run integration tests still pass
- a published DLL can be launched under backend management

## Phase 4. Resource-scoped coordination

### Goals

- replace the single workspace lock with a resource graph
- enable slot preparation without serializing unrelated bridge work

### Tasks

1. Introduce `ResourceScopePlanner`.
2. Model resources:
   - bridge
   - backend registration
   - source tree
   - logical app
   - slot A/B
   - shadow build
3. Migrate build/test/app/update flows onto scoped leases.
4. Add deadlock-avoidance ordering rules.

### Likely files

- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/WorkspaceExecutionLock.cs`
- `src/CanDoItAll.Mcp.Core/Concurrency/ResourceMutationGate.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
- new `src/CanDoItAll.Mcp.DotNetWatch/Runtime/Coordination/*`

### Exit criteria

- concurrent non-conflicting operations can proceed
- conflicting operations fail fast with actionable scope-holder details

## Phase 5. Slot-based atomic runtime lane

### Goals

- add isolated publish slots
- add candidate prepare, commit, and rollback

### Tasks

1. Add `RuntimeSlotRegistry`.
2. Add candidate publish into inactive slot.
3. Launch candidate runtime on isolated ports.
4. Add `app_update_atomic`.
5. Add `app_rollback`.
6. Persist transaction and slot manifests.
7. Expose current active revision and rollback availability.
8. Persist endpoint leases for candidate sessions.
9. Emit atomic-lane guidance for candidate validation, commit, and rollback availability.

### Likely files

- new `src/CanDoItAll.Mcp.DotNetWatch/Runtime/Atomic/*`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs`
- `CanDoItAll.Mcp.DotNetWatch.settings.json`

### Exit criteria

- candidate prepare does not mutate active runtime
- commit changes logical active runtime only after health success
- rollback restores previous revision

## Phase 6. Structured events and manager surfacing

### Goals

- reduce log-parsing dependence
- make manager/Codex status transparent

### Tasks

1. Add `app_events`.
2. Add bridge, transaction, slot, and lane details to manager status models.
3. Update manager UI to show:
   - active logical app
   - current revision
   - candidate transaction
   - rollback availability
4. Surface current workflow mode and recommended next validation step for operators where useful.
5. Keep raw logs unchanged and free of workflow-coaching text.

### Exit criteria

- Codex can follow an update transaction without parsing free-form logs
- manager UI exposes slot and transaction state clearly
- guidance is emitted only on the intended low-volume tools and stays within the configured size budget

## Phase 7. Validation, rollout, and cleanup

### Goals

- prove the redesign works end to end
- leave legacy one-folder publish flows as deprecated, not silently authoritative

### Tasks

1. Extend unit tests.
2. Extend integration tests for bridge repair and slot transactions.
3. Add failure-injection coverage.
4. Add shadow build retention/cleanup tests.
5. Add self-host validation coverage proving the MCP server can build/test itself while the live backend remains running.
6. Mark the old `.artifacts\bundle-validation\webapp` workflow as manual-only or deprecated.
7. Document migration steps for existing Codex config.
8. Add guidance-budget and guidance-selection tests so the steering layer does not regress into noisy prose.

### Exit criteria

- all validation gates in `08-validation-criteria.md` pass
- final QA signoff is evidence-based, not inferred

## Recommended change-set strategy

Recommended pull request sequence:

1. Phase 1 + Phase 2
2. Phase 3
3. Phase 4
4. Phase 5
5. Phase 6 + Phase 7

Do not merge Phase 5 before Phase 2 and Phase 3.
Atomic update work is not safe if bridge repair and launch-model boundaries are still ambiguous.
