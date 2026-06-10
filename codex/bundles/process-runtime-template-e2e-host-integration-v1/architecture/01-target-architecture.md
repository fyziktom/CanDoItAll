# Target Architecture

## Layers

1. **Process Core**
   - Pure deterministic read models/rules only.
   - No drivers, EF, UI, MAF, AgentFramework runtime, OpenAI, workspace/storage, or module dependencies.

2. **Process Contracts**
   - Stable DTOs for process runtime host contracts, request identity, dry-run sandbox decisions, denial models, audit references, and capability references.
   - Generic names only; no .NET/Office/business template leakage.

3. **Process Module Runtime**
   - Owns definitions, templates, launch plans, process runs, outbox, dispatch, claims, transitions, finalizers, artifacts, recovery, scheduler/workflow-origin starts, UI/API, and manager/operator readback.

4. **Verification/Dry-Run Runtime Host**
   - Process-module-owned implementation that uses contracts from Process Contracts.
   - Verification-only and dry-run-only.
   - No process mutation, transition/finalizer/claim/retry mutation, workspace/storage write, network, shell, Graph, CRM, or provider repair.

5. **Domain Verification Drivers**
   - Read-only packages over supplied evidence.
   - Explicit static descriptors; no reflection discovery, fallback selector, self-registration, or implicit DI discovery.

6. **Future Execution-Capable Host**
   - Not approved in this bundle.
   - Requires separate approval with sandbox, allowlist, authorization, revocation, emergency stop, lifecycle ownership, audit persistence, and red-team proof.
