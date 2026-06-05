# Process Dispatch Claim/Route Boundary v1

Status: Prepared  
Created: 2026-06-05 10:09:27Z  
Target branch: `maf-processes-refactor`  
Profile: `initiative`

## Purpose

This bundle continues the gradual dispatcher isolation path without introducing `CanDoItAll.Processes.Core` and without creating production process driver APIs.

The previous bundles removed direct MAF/Processes coupling, introduced process-owned execution snapshots, isolated artifact projection/write boundaries, extracted artifact validation rules, and reduced the step-completion finalizer. The next safe seam is the dispatch orchestration path:

- `ProcessRunAutomationDispatchService.Dispatch.cs` still owns the high-level dispatch loop, in-memory guard acquisition, durable claim acquisition, heartbeat lifecycle, candidate hydration, pre-execution routing, workflow/subprocess/agent routing, and finalization entry.
- `ProcessRunAutomationDispatchService.Concurrency.cs` still owns execution-run selection rules: blocking runs, stale runs, recoverable runs, competing runs, fresh-recovery skip, and reused response text resolution.
- These are Process module concerns today. They are not yet ready for Process Core extraction, but they can be decomposed into local helper boundaries that will make a later core split and future domain drivers safer.

## Hard Scope

Allowed:

- Add module-local helper files under `src/CanDoItAll.Modules.Processes/Automation/Dispatch`.
- Extract pure dispatch route facts, execution-run selection rules, claim/heartbeat wrapper objects, route planning decisions, and request/context builders.
- Preserve all current behavior via wrapper methods and focused parity tests.
- Add documentation-only driver-readiness mapping for future helper drivers.
- Keep browser validation as `N/A` unless UI files unexpectedly change; if UI proof becomes necessary, use large desktop/PC only.

Not allowed:

- No `CanDoItAll.Processes.Core` project.
- No process driver pack project or production `IProcessDriverPack` style API.
- No new MAF/Tooling dependency on Processes/Projects/Workbench.
- No EF entity movement.
- No UI rewrites.
- No mobile/small/medium viewport proof.
- No direct replacement of process lifecycle semantics.

## Expected Outcome

After this bundle, the dispatcher should still own orchestration, but the most error-prone dispatch decisions should be visible as named local helper boundaries. That prepares the next phase for candidate hydration / dispatch candidate model isolation or a later narrow core foundation.
