# Target Architecture

## Stable layers

1. **Process Core**
   - Pure deterministic read models and rules only.
   - No UI, EF, storage, workspace, AgentFramework, OpenAI, scheduler, workflow, template family names, Blazor/.NET, business-analysis, or driver concepts.

2. **Process Module Runtime**
   - Owns templates, projection, launch plans, process runs, outbox, dispatch, claims, finalizer, artifacts, project/project-structure integration, run detail, scheduler/workflow-origin launch, and manager/operator diagnostics.

3. **AgentFramework / MAF / process-mock runtime**
   - Owns agent execution and process-mock deterministic provider.
   - Process runtime calls it via existing automation execution boundaries.

4. **Verification / dry-run runtime host**
   - Process-module-owned read-only / dry-run-only host.
   - Can produce diagnostics, audit references, denial/readback DTOs, and dry-run plans.
   - Must not execute commands, restore packages, call Office/Graph, mutate workspace/storage/process/claim/transition/finalizer/retry, or self-register drivers.

5. **Future execution-capable drivers**
   - Still not approved.
   - Require a separate approval bundle with sandbox, allowlist, authorization/revocation, emergency stop, immutable audit, lifecycle owner, failure handoff, timeout/cancellation, UI/operator approval, and red-team proof.

## Current architectural focus

This bundle should prove that representative process templates can be launched and observed as a user/operator would use them, while preserving the refactored generic boundaries.
