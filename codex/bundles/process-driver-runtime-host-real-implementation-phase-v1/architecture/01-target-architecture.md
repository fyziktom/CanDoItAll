# Target Architecture

## Layers

1. **Process Core**
   - Pure deterministic rules and read models.
   - No drivers, EF, modules, UI, AgentFramework, OpenAI, storage, workspace, or runtime orchestration.

2. **Process Module Runtime**
   - Owns process definitions, run lifecycle, dispatch/outbox, finalizer, artifacts, recovery, manager diagnostics, scheduler/workflow-origin starts, and UI/API surfaces.

3. **Runtime Host Contracts**
   - Stable DTOs/enums/results for verification host, dry-run host, capability descriptors, audit references, authorization evidence, sandbox decisions, denial categories, and future execution-capable gates.
   - These contracts must not pull process module/infrastructure dependencies into Process Core.

4. **Verification Runtime Host**
   - Current allowed host.
   - Read-only over supplied facts/evidence.
   - Async, cancellable, options-governed, exact-lane-selected, audited, no mutation.

5. **Dry-Run Execution Host**
   - May model future effectful requests.
   - Produces denied/approved-for-dry-run plans only.
   - Does not execute commands or mutate state.

6. **Future Execution-Capable Host**
   - Not approved in this bundle.
   - Requires a later source-backed approval bundle with sandbox, allowlist, authorization, audit, revocation, emergency stop, cancellation, timeout, failure handoff, and red-team proof.

## Boundary Rule
Domain-specific concepts such as `.NET`, Office, business-analysis, OpenAI provider details, and concrete drivers must stay in driver packages, process-module adapters, or tests. They must not enter Process Core or generic runtime-host contracts except as opaque capability keys or operation categories.
