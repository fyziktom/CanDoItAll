# Structured Input

## Core Objective

- Repair the process artifact completion path so direct agent, workflow, subprocess, and manager recovery completion transitions validate required artifacts with the same lineage context used by the process-owned finalizer.
- Preserve strict validation for manual transitions.
- Prove the Blazor app delivery process remains capable of starting generic Blazor WASM PWA delivery work after its first required contract artifact is produced.

## Success Criteria

- A current-run workspace-written artifact with matching typed execution lineage can complete a process-owned transition.
- A manual transition with stale execution lineage is still rejected.
- Artifact validation remains content-backed for managed files.
- Focused integration tests pass.
- Blazor process template governance tests pass for generic Blazor WASM PWA coverage.

## Hard Constraints

- Do not bypass required artifact validation.
- Do not treat missing execution context as safe for agent-produced artifacts.
- Do not mutate the user's live failed run as proof unless explicitly called out as a separate recovery action.
- Do not stop the existing web app process unless a separate fixed host cannot be provided.

## Validation Expectations

- Run a failing-first targeted test or negative command proving the current behavior rejects a matching automation lineage at transition time.
- Run passing targeted tests after implementation.
- Run source assertions and anti-stub audits for the changed production path.
- Verify `http://127.0.0.1:5032` remains responsive or document an explicit blocker.

