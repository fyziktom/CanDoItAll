# Architecture map of the current process + MAF flow

## Current high-level flow

1. **Templates**
   - `Templates/Processes/processes/*/definition.json`
   - Step definitions describe dependencies, role assignments, artifact expectations, allowed operations, capability scope and docs.

2. **Launch and assignment**
   - `ProcessLaunchApplicationService`
   - `AgentFrameworkProcessLaunchExecutorResolver`
   - Runtime creates step assignments with allowed operations, produced/required slot IDs, launch variables, capability scope and selected executor.

3. **Runtime scheduling and claims**
   - `ProcessRuntimeScheduler`
   - `ProcessRuntimeDispatchApplicationService`
   - Runtime selects ready steps, creates claims, dispatches agent strategy, applies manager recovery/rework.

4. **Strategy adapter**
   - `AgentFrameworkProcessExecutionAdapter`
   - Builds prompt, appends runtime step contract, calls `AgentFrameworkWorkspaceExecutionService.ExecuteRunAsync`, parses structured `ProcessStepOutcomeResult`, validates tool receipts and managed artifact refs, converts to `ProcessExecutionAdapterResult`.

5. **MAF workspace execution**
   - `AgentFrameworkWorkspaceExecutionService.*`
   - Executes agent run, persists `ExecutionRunRecord`, tool receipts, result summary, artifacts and usage.

6. **Tool providers and policies**
   - `ProjectStructureAgentRuntimeToolProvider`
   - Workspace tools, project-structure tools, browser/runtime tools, approvals, scoped process access.

7. **Runtime result finalization**
   - `ProcessRuntimeEngine.Results.cs`
   - Applies strategy result, enforces required input/output artifact slots, stores receipts, available slots and connected input receipts.

8. **Projection/operator UI**
   - `ProcessRuntimeProjectionQueryService`
   - `ProcessRuntimeOperatorActionDiagnostics`
   - Reads runtime state, assignments, AgentFramework observations and produces actionable operator/rework text.

## Where the current flow is fragile

### A. Template contracts are not fully executable

The templates contain many correct instructions, but several critical rules remain prose-only. Example: `prepare-solution-skeleton` says textually that both `setup-handoff` and `setup-handoff-after-repair` are accepted, while the machine-readable artifact mapping has only `SubprocessChildStepKey: setup-handoff`.

### B. Runtime contract is slot-centric

The runtime prompt says “Expected output artifacts: slot <guid>”. This is correct for the engine, but weak for an LLM agent. The agent needs semantic names, write refs and child mapping.

### C. Subprocess ownership is ambiguous

Runtime has code to coordinate mapped subprocesses. Templates also tell agents to call `project_structure_process_subprocess_launch`. That creates competing control paths.

### D. Parent evidence is not a deterministic projection of child evidence

Completed child evidence refs are collected from child assignment files. The parent produced slot is not deterministically tied to an accepted child handoff artifact and branch outcome.

### E. Observability can lose the exact failing step

AgentFramework observations are queried by run and limited by `TakePerRun`; then grouped by step after the fact. In nested/large runs, the exact blocked step can be missing from the observation set.

### F. Rework is under-informed

Rework prompt may only know “produce required evidence” and a slot count. It often lacks child run id, exact tool receipt failure, primary artifact ref and accepted output contract.

## Target architecture for this slice of fixes

The target is not a full rewrite. It is a hardening layer around the existing runtime:

- **Exact observation correlation** for operator and rework.
- **Runtime-owned subprocess bridge** for `StepKind=Subprocess`.
- **Semantic artifact descriptors** carried from template → assignment → prompt → diagnostics.
- **Actual content-grounded artifact receipts** instead of synthetic slot-only refs.
- **Exact tool preflight** before claim/dispatch.
- **Typed template gates** for child accepted/no-go outputs and manual skip.
